namespace Wonder.Infrastructure
{
    using global::System.Collections;
    using global::System.Collections.ObjectModel;
    using global::System.Diagnostics;
    using global::System.Diagnostics.CodeAnalysis;
    using global::System.Diagnostics.Contracts;
    using global::System.Runtime.InteropServices;
    using global::System.Runtime.Serialization;
    using System;
    using System.Runtime.CompilerServices;
    using System.Runtime.ConstrainedExecution;
    using System.Collections.Generic;
    using System.Threading;
    using System.Diagnostics.Tracing;

    [DebuggerDisplay("Count = {Count}")]
    [Serializable]
    public class KeyedList<TKey, TValue> : IDictionary<TKey, TValue>, IDictionary, IReadOnlyDictionary<TKey, TValue>, ISerializable, IDeserializationCallback
    {
        private struct Entry
        {
            public int hashCode;    // Lower 31 bits of hash code, -1 if unused
            public int next;        // Index of next entry, -1 if last
            public TKey key;           // Key of entry
            public TValue value;         // Value of entry
        }

        private int[] buckets;
        private Entry[] entries;
        private int count;
        private int version;
        private int freeList;
        private int freeCount;
        private IEqualityComparer<TKey> comparer;
        private KeyCollection keys;
        private ValueCollection values;
        private Object _syncRoot;

        // constants for serialization
        private const String VersionName = "Version";
        private const String HashSizeName = "HashSize";  // Must save buckets.Length
        private const String KeyValuePairsName = "KeyValuePairs";
        private const String ComparerName = "Comparer";

        public KeyedList() : this(0, null) { }

        public KeyedList(int capacity) : this(capacity, null) { }

        public KeyedList(IEqualityComparer<TKey> comparer) : this(0, comparer) { }

        public KeyedList(int capacity, IEqualityComparer<TKey> comparer)
        {
            if (capacity < 0) ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.capacity);
            if (capacity > 0) Initialize(capacity);
            this.comparer = comparer ?? EqualityComparer<TKey>.Default;

#if FEATURE_CORECLR
            if (HashHelpers.s_UseRandomizedStringHashing && comparer == EqualityComparer<string>.Default)
            {
                this.comparer = (IEqualityComparer<TKey>) NonRandomizedStringEqualityComparer.Default;
            }
#endif // FEATURE_CORECLR
        }

        public KeyedList(IDictionary<TKey, TValue> dictionary) : this(dictionary, null) { }

        public KeyedList(IDictionary<TKey, TValue> dictionary, IEqualityComparer<TKey> comparer) :
            this(dictionary != null ? dictionary.Count : 0, comparer)
        {

            if (dictionary == null)
            {
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.dictionary);
            }

            foreach (KeyValuePair<TKey, TValue> pair in dictionary)
            {
                Add(pair.Key, pair.Value);
            }
        }

        protected KeyedList(SerializationInfo info, StreamingContext context)
        {
            //We can't do anything with the keys and values until the entire graph has been deserialized
            //and we have a resonable estimate that GetHashCode is not going to fail.  For the time being,
            //we'll just cache this.  The graph is not valid until OnDeserialization has been called.
            HashHelpers.SerializationInfoTable.Add(this, info);
        }

        public IEqualityComparer<TKey> Comparer
        {
            get
            {
                return comparer;
            }
        }

        public int Count
        {
            get { return count - freeCount; }
        }

        public KeyCollection Keys
        {
            get
            {
                Contract.Ensures(Contract.Result<KeyCollection>() != null);
                if (keys == null) keys = new KeyCollection(this);
                return keys;
            }
        }

        ICollection<TKey> IDictionary<TKey, TValue>.Keys
        {
            get
            {
                if (keys == null) keys = new KeyCollection(this);
                return keys;
            }
        }

        IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys
        {
            get
            {
                if (keys == null) keys = new KeyCollection(this);
                return keys;
            }
        }

        public ValueCollection Values
        {
            get
            {
                Contract.Ensures(Contract.Result<ValueCollection>() != null);
                if (values == null) values = new ValueCollection(this);
                return values;
            }
        }

        ICollection<TValue> IDictionary<TKey, TValue>.Values
        {
            get
            {
                if (values == null) values = new ValueCollection(this);
                return values;
            }
        }

        IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values
        {
            get
            {
                if (values == null) values = new ValueCollection(this);
                return values;
            }
        }

        public TValue this[TKey key]
        {
            get
            {
                int i = FindEntry(key);
                if (i >= 0) return entries[i].value;
                ThrowHelper.ThrowKeyNotFoundException();
                return default(TValue);
            }
            set
            {
                Insert(key, value, false);
            }
        }

        public void Add(TKey key, TValue value)
        {
            Insert(key, value, true);
        }

        void ICollection<KeyValuePair<TKey, TValue>>.Add(KeyValuePair<TKey, TValue> keyValuePair)
        {
            Add(keyValuePair.Key, keyValuePair.Value);
        }

        bool ICollection<KeyValuePair<TKey, TValue>>.Contains(KeyValuePair<TKey, TValue> keyValuePair)
        {
            int i = FindEntry(keyValuePair.Key);
            if (i >= 0 && EqualityComparer<TValue>.Default.Equals(entries[i].value, keyValuePair.Value))
            {
                return true;
            }
            return false;
        }

        bool ICollection<KeyValuePair<TKey, TValue>>.Remove(KeyValuePair<TKey, TValue> keyValuePair)
        {
            int i = FindEntry(keyValuePair.Key);
            if (i >= 0 && EqualityComparer<TValue>.Default.Equals(entries[i].value, keyValuePair.Value))
            {
                Remove(keyValuePair.Key);
                return true;
            }
            return false;
        }

        public void Clear()
        {
            if (count > 0)
            {
                for (int i = 0; i < buckets.Length; i++) buckets[i] = -1;
                Array.Clear(entries, 0, count);
                freeList = -1;
                count = 0;
                freeCount = 0;
                version++;
            }
        }

        public bool ContainsKey(TKey key)
        {
            return FindEntry(key) >= 0;
        }

        public bool ContainsValue(TValue value)
        {
            if (value == null)
            {
                for (int i = 0; i < count; i++)
                {
                    if (entries[i].hashCode >= 0 && entries[i].value == null) return true;
                }
            }
            else
            {
                EqualityComparer<TValue> c = EqualityComparer<TValue>.Default;
                for (int i = 0; i < count; i++)
                {
                    if (entries[i].hashCode >= 0 && c.Equals(entries[i].value, value)) return true;
                }
            }
            return false;
        }

        private void CopyTo(KeyValuePair<TKey, TValue>[] array, int index)
        {
            if (array == null)
            {
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.array);
            }

            if (index < 0 || index > array.Length)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.index, ExceptionResource.ArgumentOutOfRange_NeedNonNegNum);
            }

            if (array.Length - index < Count)
            {
                ThrowHelper.ThrowArgumentException(ExceptionResource.Arg_ArrayPlusOffTooSmall);
            }

            int count = this.count;
            Entry[] entries = this.entries;
            for (int i = 0; i < count; i++)
            {
                if (entries[i].hashCode >= 0)
                {
                    array[index++] = new KeyValuePair<TKey, TValue>(entries[i].key, entries[i].value);
                }
            }
        }

        public Enumerator GetEnumerator()
        {
            return new Enumerator(this, Enumerator.KeyValuePair);
        }

        IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator()
        {
            return new Enumerator(this, Enumerator.KeyValuePair);
        }

        //[System.Security.SecurityCritical]  // auto-generated_required
        public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null)
            {
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.info);
            }
            info.AddValue(VersionName, version);

#if FEATURE_RANDOMIZED_STRING_HASHING
            info.AddValue(ComparerName, HashHelpers.GetEqualityComparerForSerialization(comparer), typeof(IEqualityComparer<TKey>));
#else
            info.AddValue(ComparerName, comparer, typeof(IEqualityComparer<TKey>));
#endif

            info.AddValue(HashSizeName, buckets == null ? 0 : buckets.Length); //This is the length of the bucket array.
            if (buckets != null)
            {
                KeyValuePair<TKey, TValue>[] array = new KeyValuePair<TKey, TValue>[Count];
                CopyTo(array, 0);
                info.AddValue(KeyValuePairsName, array, typeof(KeyValuePair<TKey, TValue>[]));
            }
        }

        private int FindEntry(TKey key)
        {
            if (key == null)
            {
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.key);
            }

            if (buckets != null)
            {
                int hashCode = comparer.GetHashCode(key) & 0x7FFFFFFF;
                for (int i = buckets[hashCode % buckets.Length]; i >= 0; i = entries[i].next)
                {
                    if (entries[i].hashCode == hashCode && comparer.Equals(entries[i].key, key)) return i;
                }
            }
            return -1;
        }

        private void Initialize(int capacity)
        {
            int size = HashHelpers.GetPrime(capacity);
            buckets = new int[size];
            for (int i = 0; i < buckets.Length; i++) buckets[i] = -1;
            entries = new Entry[size];
            freeList = -1;
        }

        private void Insert(TKey key, TValue value, bool add)
        {

            if (key == null)
            {
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.key);
            }

            if (buckets == null) Initialize(0);
            int hashCode = comparer.GetHashCode(key) & 0x7FFFFFFF;
            int targetBucket = hashCode % buckets.Length;

#if FEATURE_RANDOMIZED_STRING_HASHING
            int collisionCount = 0;
#endif

            for (int i = buckets[targetBucket]; i >= 0; i = entries[i].next)
            {
                if (entries[i].hashCode == hashCode && comparer.Equals(entries[i].key, key))
                {
                    if (add)
                    {
                        ThrowHelper.ThrowArgumentException(ExceptionResource.Argument_AddingDuplicate);
                    }
                    entries[i].value = value;
                    version++;
                    return;
                }

#if FEATURE_RANDOMIZED_STRING_HASHING
                collisionCount++;
#endif
            }
            int index;
            if (freeCount > 0)
            {
                index = freeList;
                freeList = entries[index].next;
                freeCount--;
            }
            else
            {
                if (count == entries.Length)
                {
                    Resize();
                    targetBucket = hashCode % buckets.Length;
                }
                index = count;
                count++;
            }

            entries[index].hashCode = hashCode;
            entries[index].next = buckets[targetBucket];
            entries[index].key = key;
            entries[index].value = value;
            buckets[targetBucket] = index;
            version++;

#if FEATURE_RANDOMIZED_STRING_HASHING
 
#if FEATURE_CORECLR
            // In case we hit the collision threshold we'll need to switch to the comparer which is using randomized string hashing
            // in this case will be EqualityComparer<string>.Default.
            // Note, randomized string hashing is turned on by default on coreclr so EqualityComparer<string>.Default will 
            // be using randomized string hashing
 
            if (collisionCount > HashHelpers.HashCollisionThreshold && comparer == NonRandomizedStringEqualityComparer.Default) 
            {
                comparer = (IEqualityComparer<TKey>) EqualityComparer<string>.Default;
                Resize(entries.Length, true);
            }
#else
            if(collisionCount > HashHelpers.HashCollisionThreshold && HashHelpers.IsWellKnownEqualityComparer(comparer)) 
            {
                comparer = (IEqualityComparer<TKey>) HashHelpers.GetRandomizedEqualityComparer(comparer);
                Resize(entries.Length, true);
            }
#endif // FEATURE_CORECLR
 
#endif

        }

        public virtual void OnDeserialization(Object sender)
        {
            SerializationInfo siInfo;
            HashHelpers.SerializationInfoTable.TryGetValue(this, out siInfo);

            if (siInfo == null)
            {
                // It might be necessary to call OnDeserialization from a container if the container object also implements
                // OnDeserialization. However, remoting will call OnDeserialization again.
                // We can return immediately if this function is called twice. 
                // Note we set remove the serialization info from the table at the end of this method.
                return;
            }

            int realVersion = siInfo.GetInt32(VersionName);
            int hashsize = siInfo.GetInt32(HashSizeName);
            comparer = (IEqualityComparer<TKey>)siInfo.GetValue(ComparerName, typeof(IEqualityComparer<TKey>));

            if (hashsize != 0)
            {
                buckets = new int[hashsize];
                for (int i = 0; i < buckets.Length; i++) buckets[i] = -1;
                entries = new Entry[hashsize];
                freeList = -1;

                KeyValuePair<TKey, TValue>[] array = (KeyValuePair<TKey, TValue>[])
                    siInfo.GetValue(KeyValuePairsName, typeof(KeyValuePair<TKey, TValue>[]));

                if (array == null)
                {
                    ThrowHelper.ThrowSerializationException(ExceptionResource.Serialization_MissingKeys);
                }

                for (int i = 0; i < array.Length; i++)
                {
                    if (array[i].Key == null)
                    {
                        ThrowHelper.ThrowSerializationException(ExceptionResource.Serialization_NullKey);
                    }
                    Insert(array[i].Key, array[i].Value, true);
                }
            }
            else
            {
                buckets = null;
            }

            version = realVersion;
            HashHelpers.SerializationInfoTable.Remove(this);
        }

        private void Resize()
        {
            Resize(HashHelpers.ExpandPrime(count), false);
        }

        private void Resize(int newSize, bool forceNewHashCodes)
        {
            Contract.Assert(newSize >= entries.Length);
            int[] newBuckets = new int[newSize];
            for (int i = 0; i < newBuckets.Length; i++) newBuckets[i] = -1;
            Entry[] newEntries = new Entry[newSize];
            Array.Copy(entries, 0, newEntries, 0, count);
            if (forceNewHashCodes)
            {
                for (int i = 0; i < count; i++)
                {
                    if (newEntries[i].hashCode != -1)
                    {
                        newEntries[i].hashCode = (comparer.GetHashCode(newEntries[i].key) & 0x7FFFFFFF);
                    }
                }
            }
            for (int i = 0; i < count; i++)
            {
                if (newEntries[i].hashCode >= 0)
                {
                    int bucket = newEntries[i].hashCode % newSize;
                    newEntries[i].next = newBuckets[bucket];
                    newBuckets[bucket] = i;
                }
            }
            buckets = newBuckets;
            entries = newEntries;
        }

        public bool Remove(TKey key)
        {
            if (key == null)
            {
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.key);
            }

            if (buckets != null)
            {
                int hashCode = comparer.GetHashCode(key) & 0x7FFFFFFF;
                int bucket = hashCode % buckets.Length;
                int last = -1;
                for (int i = buckets[bucket]; i >= 0; last = i, i = entries[i].next)
                {
                    if (entries[i].hashCode == hashCode && comparer.Equals(entries[i].key, key))
                    {
                        if (last < 0)
                        {
                            buckets[bucket] = entries[i].next;
                        }
                        else
                        {
                            entries[last].next = entries[i].next;
                        }
                        entries[i].hashCode = -1;
                        entries[i].next = freeList;
                        entries[i].key = default(TKey);
                        entries[i].value = default(TValue);
                        freeList = i;
                        freeCount++;
                        version++;
                        return true;
                    }
                }
            }
            return false;
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            int i = FindEntry(key);
            if (i >= 0)
            {
                value = entries[i].value;
                return true;
            }
            value = default(TValue);
            return false;
        }

        // This is a convenience method for the internal callers that were converted from using Hashtable.
        // Many were combining key doesn't exist and key exists but null value (for non-value types) checks.
        // This allows them to continue getting that behavior with minimal code delta. This is basically
        // TryGetValue without the out param
        internal TValue GetValueOrDefault(TKey key)
        {
            int i = FindEntry(key);
            if (i >= 0)
            {
                return entries[i].value;
            }
            return default(TValue);
        }

        bool ICollection<KeyValuePair<TKey, TValue>>.IsReadOnly
        {
            get { return false; }
        }

        void ICollection<KeyValuePair<TKey, TValue>>.CopyTo(KeyValuePair<TKey, TValue>[] array, int index)
        {
            CopyTo(array, index);
        }

        void ICollection.CopyTo(Array array, int index)
        {
            if (array == null)
            {
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.array);
            }

            if (array.Rank != 1)
            {
                ThrowHelper.ThrowArgumentException(ExceptionResource.Arg_RankMultiDimNotSupported);
            }

            if (array.GetLowerBound(0) != 0)
            {
                ThrowHelper.ThrowArgumentException(ExceptionResource.Arg_NonZeroLowerBound);
            }

            if (index < 0 || index > array.Length)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.index, ExceptionResource.ArgumentOutOfRange_NeedNonNegNum);
            }

            if (array.Length - index < Count)
            {
                ThrowHelper.ThrowArgumentException(ExceptionResource.Arg_ArrayPlusOffTooSmall);
            }

            KeyValuePair<TKey, TValue>[] pairs = array as KeyValuePair<TKey, TValue>[];
            if (pairs != null)
            {
                CopyTo(pairs, index);
            }
            else if (array is DictionaryEntry[])
            {
                DictionaryEntry[] dictEntryArray = array as DictionaryEntry[];
                Entry[] entries = this.entries;
                for (int i = 0; i < count; i++)
                {
                    if (entries[i].hashCode >= 0)
                    {
                        dictEntryArray[index++] = new DictionaryEntry(entries[i].key, entries[i].value);
                    }
                }
            }
            else
            {
                object[] objects = array as object[];
                if (objects == null)
                {
                    ThrowHelper.ThrowArgumentException(ExceptionResource.Argument_InvalidArrayType);
                }

                try
                {
                    int count = this.count;
                    Entry[] entries = this.entries;
                    for (int i = 0; i < count; i++)
                    {
                        if (entries[i].hashCode >= 0)
                        {
                            objects[index++] = new KeyValuePair<TKey, TValue>(entries[i].key, entries[i].value);
                        }
                    }
                }
                catch (ArrayTypeMismatchException)
                {
                    ThrowHelper.ThrowArgumentException(ExceptionResource.Argument_InvalidArrayType);
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return new Enumerator(this, Enumerator.KeyValuePair);
        }

        bool ICollection.IsSynchronized
        {
            get { return false; }
        }

        object ICollection.SyncRoot
        {
            get
            {
                if (_syncRoot == null)
                {
                    System.Threading.Interlocked.CompareExchange<Object>(ref _syncRoot, new Object(), null);
                }
                return _syncRoot;
            }
        }

        bool IDictionary.IsFixedSize
        {
            get { return false; }
        }

        bool IDictionary.IsReadOnly
        {
            get { return false; }
        }

        ICollection IDictionary.Keys
        {
            get { return (ICollection)Keys; }
        }

        ICollection IDictionary.Values
        {
            get { return (ICollection)Values; }
        }

        object IDictionary.this[object key]
        {
            get
            {
                if (IsCompatibleKey(key))
                {
                    int i = FindEntry((TKey)key);
                    if (i >= 0)
                    {
                        return entries[i].value;
                    }
                }
                return null;
            }
            set
            {
                if (key == null)
                {
                    ThrowHelper.ThrowArgumentNullException(ExceptionArgument.key);
                }
                ThrowHelper.IfNullAndNullsAreIllegalThenThrow<TValue>(value, ExceptionArgument.value);

                try
                {
                    TKey tempKey = (TKey)key;
                    try
                    {
                        this[tempKey] = (TValue)value;
                    }
                    catch (InvalidCastException)
                    {
                        ThrowHelper.ThrowWrongValueTypeArgumentException(value, typeof(TValue));
                    }
                }
                catch (InvalidCastException)
                {
                    ThrowHelper.ThrowWrongKeyTypeArgumentException(key, typeof(TKey));
                }
            }
        }

        private static bool IsCompatibleKey(object key)
        {
            if (key == null)
            {
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.key);
            }
            return (key is TKey);
        }

        void IDictionary.Add(object key, object value)
        {
            if (key == null)
            {
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.key);
            }
            ThrowHelper.IfNullAndNullsAreIllegalThenThrow<TValue>(value, ExceptionArgument.value);

            try
            {
                TKey tempKey = (TKey)key;

                try
                {
                    Add(tempKey, (TValue)value);
                }
                catch (InvalidCastException)
                {
                    ThrowHelper.ThrowWrongValueTypeArgumentException(value, typeof(TValue));
                }
            }
            catch (InvalidCastException)
            {
                ThrowHelper.ThrowWrongKeyTypeArgumentException(key, typeof(TKey));
            }
        }

        bool IDictionary.Contains(object key)
        {
            if (IsCompatibleKey(key))
            {
                return ContainsKey((TKey)key);
            }

            return false;
        }

        IDictionaryEnumerator IDictionary.GetEnumerator()
        {
            return new Enumerator(this, Enumerator.DictEntry);
        }

        void IDictionary.Remove(object key)
        {
            if (IsCompatibleKey(key))
            {
                Remove((TKey)key);
            }
        }

        [Serializable]
        public struct Enumerator : IEnumerator<KeyValuePair<TKey, TValue>>, IDictionaryEnumerator
        {
            private KeyedList<TKey, TValue> dictionary;
            private int version;
            private int index;
            private KeyValuePair<TKey, TValue> current;
            private int getEnumeratorRetType;  // What should Enumerator.Current return?

            internal const int DictEntry = 1;
            internal const int KeyValuePair = 2;

            internal Enumerator(KeyedList<TKey, TValue> dictionary, int getEnumeratorRetType)
            {
                this.dictionary = dictionary;
                version = dictionary.version;
                index = 0;
                this.getEnumeratorRetType = getEnumeratorRetType;
                current = new KeyValuePair<TKey, TValue>();
            }

            public bool MoveNext()
            {
                if (version != dictionary.version)
                {
                    ThrowHelper.ThrowInvalidOperationException(ExceptionResource.InvalidOperation_EnumFailedVersion);
                }

                // Use unsigned comparison since we set index to dictionary.count+1 when the enumeration ends.
                // dictionary.count+1 could be negative if dictionary.count is Int32.MaxValue
                while ((uint)index < (uint)dictionary.count)
                {
                    if (dictionary.entries[index].hashCode >= 0)
                    {
                        current = new KeyValuePair<TKey, TValue>(dictionary.entries[index].key, dictionary.entries[index].value);
                        index++;
                        return true;
                    }
                    index++;
                }

                index = dictionary.count + 1;
                current = new KeyValuePair<TKey, TValue>();
                return false;
            }

            public KeyValuePair<TKey, TValue> Current
            {
                get { return current; }
            }

            public void Dispose()
            {
            }

            object IEnumerator.Current
            {
                get
                {
                    if (index == 0 || (index == dictionary.count + 1))
                    {
                        ThrowHelper.ThrowInvalidOperationException(ExceptionResource.InvalidOperation_EnumOpCantHappen);
                    }

                    if (getEnumeratorRetType == DictEntry)
                    {
                        return new System.Collections.DictionaryEntry(current.Key, current.Value);
                    }
                    else
                    {
                        return new KeyValuePair<TKey, TValue>(current.Key, current.Value);
                    }
                }
            }

            void IEnumerator.Reset()
            {
                if (version != dictionary.version)
                {
                    ThrowHelper.ThrowInvalidOperationException(ExceptionResource.InvalidOperation_EnumFailedVersion);
                }

                index = 0;
                current = new KeyValuePair<TKey, TValue>();
            }

            DictionaryEntry IDictionaryEnumerator.Entry
            {
                get
                {
                    if (index == 0 || (index == dictionary.count + 1))
                    {
                        ThrowHelper.ThrowInvalidOperationException(ExceptionResource.InvalidOperation_EnumOpCantHappen);
                    }

                    return new DictionaryEntry(current.Key, current.Value);
                }
            }

            object IDictionaryEnumerator.Key
            {
                get
                {
                    if (index == 0 || (index == dictionary.count + 1))
                    {
                        ThrowHelper.ThrowInvalidOperationException(ExceptionResource.InvalidOperation_EnumOpCantHappen);
                    }

                    return current.Key;
                }
            }

            object IDictionaryEnumerator.Value
            {
                get
                {
                    if (index == 0 || (index == dictionary.count + 1))
                    {
                        ThrowHelper.ThrowInvalidOperationException(ExceptionResource.InvalidOperation_EnumOpCantHappen);
                    }

                    return current.Value;
                }
            }
        }

        //[DebuggerTypeProxy(typeof(Mscorlib_DictionaryKeyCollectionDebugView<,>))]
        //[DebuggerDisplay("Count = {Count}")]
        [Serializable]
        public sealed class KeyCollection : ICollection<TKey>, ICollection, IReadOnlyCollection<TKey>
        {
            private KeyedList<TKey, TValue> dictionary;

            public KeyCollection(KeyedList<TKey, TValue> dictionary)
            {
                if (dictionary == null)
                {
                    ThrowHelper.ThrowArgumentNullException(ExceptionArgument.dictionary);
                }
                this.dictionary = dictionary;
            }

            public Enumerator GetEnumerator()
            {
                return new Enumerator(dictionary);
            }

            public void CopyTo(TKey[] array, int index)
            {
                if (array == null)
                {
                    ThrowHelper.ThrowArgumentNullException(ExceptionArgument.array);
                }

                if (index < 0 || index > array.Length)
                {
                    ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.index, ExceptionResource.ArgumentOutOfRange_NeedNonNegNum);
                }

                if (array.Length - index < dictionary.Count)
                {
                    ThrowHelper.ThrowArgumentException(ExceptionResource.Arg_ArrayPlusOffTooSmall);
                }

                int count = dictionary.count;
                Entry[] entries = dictionary.entries;
                for (int i = 0; i < count; i++)
                {
                    if (entries[i].hashCode >= 0) array[index++] = entries[i].key;
                }
            }

            public int Count
            {
                get { return dictionary.Count; }
            }

            bool ICollection<TKey>.IsReadOnly
            {
                get { return true; }
            }

            void ICollection<TKey>.Add(TKey item)
            {
                ThrowHelper.ThrowNotSupportedException(ExceptionResource.NotSupported_KeyCollectionSet);
            }

            void ICollection<TKey>.Clear()
            {
                ThrowHelper.ThrowNotSupportedException(ExceptionResource.NotSupported_KeyCollectionSet);
            }

            bool ICollection<TKey>.Contains(TKey item)
            {
                return dictionary.ContainsKey(item);
            }

            bool ICollection<TKey>.Remove(TKey item)
            {
                ThrowHelper.ThrowNotSupportedException(ExceptionResource.NotSupported_KeyCollectionSet);
                return false;
            }

            IEnumerator<TKey> IEnumerable<TKey>.GetEnumerator()
            {
                return new Enumerator(dictionary);
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return new Enumerator(dictionary);
            }

            void ICollection.CopyTo(Array array, int index)
            {
                if (array == null)
                {
                    ThrowHelper.ThrowArgumentNullException(ExceptionArgument.array);
                }

                if (array.Rank != 1)
                {
                    ThrowHelper.ThrowArgumentException(ExceptionResource.Arg_RankMultiDimNotSupported);
                }

                if (array.GetLowerBound(0) != 0)
                {
                    ThrowHelper.ThrowArgumentException(ExceptionResource.Arg_NonZeroLowerBound);
                }

                if (index < 0 || index > array.Length)
                {
                    ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.index, ExceptionResource.ArgumentOutOfRange_NeedNonNegNum);
                }

                if (array.Length - index < dictionary.Count)
                {
                    ThrowHelper.ThrowArgumentException(ExceptionResource.Arg_ArrayPlusOffTooSmall);
                }

                TKey[] keys = array as TKey[];
                if (keys != null)
                {
                    CopyTo(keys, index);
                }
                else
                {
                    object[] objects = array as object[];
                    if (objects == null)
                    {
                        ThrowHelper.ThrowArgumentException(ExceptionResource.Argument_InvalidArrayType);
                    }

                    int count = dictionary.count;
                    Entry[] entries = dictionary.entries;
                    try
                    {
                        for (int i = 0; i < count; i++)
                        {
                            if (entries[i].hashCode >= 0) objects[index++] = entries[i].key;
                        }
                    }
                    catch (ArrayTypeMismatchException)
                    {
                        ThrowHelper.ThrowArgumentException(ExceptionResource.Argument_InvalidArrayType);
                    }
                }
            }

            bool ICollection.IsSynchronized
            {
                get { return false; }
            }

            Object ICollection.SyncRoot
            {
                get { return ((ICollection)dictionary).SyncRoot; }
            }

            [Serializable]
            public struct Enumerator : IEnumerator<TKey>, System.Collections.IEnumerator
            {
                private KeyedList<TKey, TValue> dictionary;
                private int index;
                private int version;
                private TKey currentKey;

                internal Enumerator(KeyedList<TKey, TValue> dictionary)
                {
                    this.dictionary = dictionary;
                    version = dictionary.version;
                    index = 0;
                    currentKey = default(TKey);
                }

                public void Dispose()
                {
                }

                public bool MoveNext()
                {
                    if (version != dictionary.version)
                    {
                        ThrowHelper.ThrowInvalidOperationException(ExceptionResource.InvalidOperation_EnumFailedVersion);
                    }

                    while ((uint)index < (uint)dictionary.count)
                    {
                        if (dictionary.entries[index].hashCode >= 0)
                        {
                            currentKey = dictionary.entries[index].key;
                            index++;
                            return true;
                        }
                        index++;
                    }

                    index = dictionary.count + 1;
                    currentKey = default(TKey);
                    return false;
                }

                public TKey Current
                {
                    get
                    {
                        return currentKey;
                    }
                }

                Object System.Collections.IEnumerator.Current
                {
                    get
                    {
                        if (index == 0 || (index == dictionary.count + 1))
                        {
                            ThrowHelper.ThrowInvalidOperationException(ExceptionResource.InvalidOperation_EnumOpCantHappen);
                        }

                        return currentKey;
                    }
                }

                void System.Collections.IEnumerator.Reset()
                {
                    if (version != dictionary.version)
                    {
                        ThrowHelper.ThrowInvalidOperationException(ExceptionResource.InvalidOperation_EnumFailedVersion);
                    }

                    index = 0;
                    currentKey = default(TKey);
                }

                public void Reset()
                {
                    throw new NotImplementedException();
                }
            }
        }

        //[DebuggerTypeProxy(typeof(Mscorlib_DictionaryValueCollectionDebugView<,>))]
        [DebuggerDisplay("Count = {Count}")]
        [Serializable]
        public sealed class ValueCollection : ICollection<TValue>, ICollection, IReadOnlyCollection<TValue>
        {
            private KeyedList<TKey, TValue> dictionary;

            public ValueCollection(KeyedList<TKey, TValue> dictionary)
            {
                if (dictionary == null)
                {
                    ThrowHelper.ThrowArgumentNullException(ExceptionArgument.dictionary);
                }
                this.dictionary = dictionary;
            }

            public Enumerator GetEnumerator()
            {
                return new Enumerator(dictionary);
            }

            public void CopyTo(TValue[] array, int index)
            {
                if (array == null)
                {
                    ThrowHelper.ThrowArgumentNullException(ExceptionArgument.array);
                }

                if (index < 0 || index > array.Length)
                {
                    ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.index, ExceptionResource.ArgumentOutOfRange_NeedNonNegNum);
                }

                if (array.Length - index < dictionary.Count)
                {
                    ThrowHelper.ThrowArgumentException(ExceptionResource.Arg_ArrayPlusOffTooSmall);
                }

                int count = dictionary.count;
                Entry[] entries = dictionary.entries;
                for (int i = 0; i < count; i++)
                {
                    if (entries[i].hashCode >= 0) array[index++] = entries[i].value;
                }
            }

            public int Count
            {
                get { return dictionary.Count; }
            }

            bool ICollection<TValue>.IsReadOnly
            {
                get { return true; }
            }

            void ICollection<TValue>.Add(TValue item)
            {
                ThrowHelper.ThrowNotSupportedException(ExceptionResource.NotSupported_ValueCollectionSet);
            }

            bool ICollection<TValue>.Remove(TValue item)
            {
                ThrowHelper.ThrowNotSupportedException(ExceptionResource.NotSupported_ValueCollectionSet);
                return false;
            }

            void ICollection<TValue>.Clear()
            {
                ThrowHelper.ThrowNotSupportedException(ExceptionResource.NotSupported_ValueCollectionSet);
            }

            bool ICollection<TValue>.Contains(TValue item)
            {
                return dictionary.ContainsValue(item);
            }

            IEnumerator<TValue> IEnumerable<TValue>.GetEnumerator()
            {
                return new Enumerator(dictionary);
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return new Enumerator(dictionary);
            }

            void ICollection.CopyTo(Array array, int index)
            {
                if (array == null)
                {
                    ThrowHelper.ThrowArgumentNullException(ExceptionArgument.array);
                }

                if (array.Rank != 1)
                {
                    ThrowHelper.ThrowArgumentException(ExceptionResource.Arg_RankMultiDimNotSupported);
                }

                if (array.GetLowerBound(0) != 0)
                {
                    ThrowHelper.ThrowArgumentException(ExceptionResource.Arg_NonZeroLowerBound);
                }

                if (index < 0 || index > array.Length)
                {
                    ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.index, ExceptionResource.ArgumentOutOfRange_NeedNonNegNum);
                }

                if (array.Length - index < dictionary.Count)
                    ThrowHelper.ThrowArgumentException(ExceptionResource.Arg_ArrayPlusOffTooSmall);

                TValue[] values = array as TValue[];
                if (values != null)
                {
                    CopyTo(values, index);
                }
                else
                {
                    object[] objects = array as object[];
                    if (objects == null)
                    {
                        ThrowHelper.ThrowArgumentException(ExceptionResource.Argument_InvalidArrayType);
                    }

                    int count = dictionary.count;
                    Entry[] entries = dictionary.entries;
                    try
                    {
                        for (int i = 0; i < count; i++)
                        {
                            if (entries[i].hashCode >= 0) objects[index++] = entries[i].value;
                        }
                    }
                    catch (ArrayTypeMismatchException)
                    {
                        ThrowHelper.ThrowArgumentException(ExceptionResource.Argument_InvalidArrayType);
                    }
                }
            }

            bool ICollection.IsSynchronized
            {
                get { return false; }
            }

            Object ICollection.SyncRoot
            {
                get { return ((ICollection)dictionary).SyncRoot; }
            }

            [Serializable]
            public struct Enumerator : IEnumerator<TValue>, System.Collections.IEnumerator
            {
                private KeyedList<TKey, TValue> dictionary;
                private int index;
                private int version;
                private TValue currentValue;

                internal Enumerator(KeyedList<TKey, TValue> dictionary)
                {
                    this.dictionary = dictionary;
                    version = dictionary.version;
                    index = 0;
                    currentValue = default(TValue);
                }

                public void Dispose()
                {
                }

                public bool MoveNext()
                {
                    if (version != dictionary.version)
                    {
                        ThrowHelper.ThrowInvalidOperationException(ExceptionResource.InvalidOperation_EnumFailedVersion);
                    }

                    while ((uint)index < (uint)dictionary.count)
                    {
                        if (dictionary.entries[index].hashCode >= 0)
                        {
                            currentValue = dictionary.entries[index].value;
                            index++;
                            return true;
                        }
                        index++;
                    }
                    index = dictionary.count + 1;
                    currentValue = default(TValue);
                    return false;
                }

                public TValue Current
                {
                    get
                    {
                        return currentValue;
                    }
                }

                Object System.Collections.IEnumerator.Current
                {
                    get
                    {
                        if (index == 0 || (index == dictionary.count + 1))
                        {
                            ThrowHelper.ThrowInvalidOperationException(ExceptionResource.InvalidOperation_EnumOpCantHappen);
                        }

                        return currentValue;
                    }
                }

                void System.Collections.IEnumerator.Reset()
                {
                    if (version != dictionary.version)
                    {
                        ThrowHelper.ThrowInvalidOperationException(ExceptionResource.InvalidOperation_EnumFailedVersion);
                    }
                    index = 0;
                    currentValue = default(TValue);
                }
            }
        }
    }

    /// <summary>
    /// Represents a thread-safe collection of keys and values.
    /// </summary>
    /// <typeparam name="TKey">The type of the keys in the dictionary.</typeparam>
    /// <typeparam name="TValue">The type of the values in the dictionary.</typeparam>
    /// <remarks>
    /// All public and protected members of <see cref="ConcurrentKeyedList{TKey,TValue}"/> are thread-safe and may be used
    /// concurrently from multiple threads.
    /// </remarks>
#if !FEATURE_CORECLR
    [Serializable]
#endif
    [ComVisible(false)]
    //[DebuggerTypeProxy(typeof(Mscorlib_DictionaryDebugView<,>))]
    [DebuggerDisplay("Count = {Count}")]
    //[HostProtection(Synchronization = true, ExternalThreading = true)]
    public class ConcurrentKeyedList<TKey, TValue> : IDictionary<TKey, TValue>, IDictionary, IReadOnlyDictionary<TKey, TValue>
    {
        /// <summary>
        /// Tables that hold the internal state of the ConcurrentDictionary
        ///
        /// Wrapping the three tables in a single object allows us to atomically
        /// replace all tables at once.
        /// </summary>
        private class Tables
        {
            internal readonly Node[] m_buckets; // A singly-linked list for each bucket.
            internal readonly object[] m_locks; // A set of locks, each guarding a section of the table.
            internal volatile int[] m_countPerLock; // The number of elements guarded by each lock.
            internal readonly IEqualityComparer<TKey> m_comparer; // Key equality comparer

            internal Tables(Node[] buckets, object[] locks, int[] countPerLock, IEqualityComparer<TKey> comparer)
            {
                m_buckets = buckets;
                m_locks = locks;
                m_countPerLock = countPerLock;
                m_comparer = comparer;
            }
        }
#if !FEATURE_CORECLR
        [NonSerialized]
#endif
        private volatile Tables m_tables; // Internal tables of the dictionary       
                                          // NOTE: this is only used for compat reasons to serialize the comparer.
                                          // This should not be accessed from anywhere else outside of the serialization methods.
        internal IEqualityComparer<TKey> m_comparer;
#if !FEATURE_CORECLR
        [NonSerialized]
#endif
        private readonly bool m_growLockArray; // Whether to dynamically increase the size of the striped lock

        // How many times we resized becaused of collisions. 
        // This is used to make sure we don't resize the dictionary because of multi-threaded Add() calls
        // that generate collisions. Whenever a GrowTable() should be the only place that changes this
#if !FEATURE_CORECLR
        // The field should be have been marked as NonSerialized but because we shipped it without that attribute in 4.5.1.
        // we can't add it back without breaking compat. To maximize compat we are going to keep the OptionalField attribute 
        // This will prevent cases where the field was not serialized.
        [OptionalField]
#endif
        private int m_keyRehashCount;

#if !FEATURE_CORECLR
        [NonSerialized]
#endif
        private int m_budget; // The maximum number of elements per lock before a resize operation is triggered

#if !FEATURE_CORECLR // These fields are not used in CoreCLR
        private KeyValuePair<TKey, TValue>[] m_serializationArray; // Used for custom serialization

        private int m_serializationConcurrencyLevel; // used to save the concurrency level in serialization

        private int m_serializationCapacity; // used to save the capacity in serialization
#endif

        // The default capacity, i.e. the initial # of buckets. When choosing this value, we are making
        // a trade-off between the size of a very small dictionary, and the number of resizes when
        // constructing a large dictionary. Also, the capacity should not be divisible by a small prime.
        private const int DEFAULT_CAPACITY = 31;

        // The maximum size of the striped lock that will not be exceeded when locks are automatically
        // added as the dictionary grows. However, the user is allowed to exceed this limit by passing
        // a concurrency level larger than MAX_LOCK_NUMBER into the constructor.
        private const int MAX_LOCK_NUMBER = 1024;

        // Whether TValue is a type that can be written atomically (i.e., with no danger of torn reads)
        private static readonly bool s_isValueWriteAtomic = IsValueWriteAtomic();


        /// <summary>
        /// Determines whether type TValue can be written atomically
        /// </summary>
        private static bool IsValueWriteAtomic()
        {
            Type valueType = typeof(TValue);

            //
            // Section 12.6.6 of ECMA CLI explains which types can be read and written atomically without
            // the risk of tearing.
            //
            // See http://www.ecma-international.org/publications/files/ECMA-ST/Ecma-335.pdf
            //
            if (valueType.IsClass)
            {
                return true;
            }
            switch (Type.GetTypeCode(valueType))
            {
                case TypeCode.Boolean:
                case TypeCode.Byte:
                case TypeCode.Char:
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.SByte:
                case TypeCode.Single:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                    return true;
                case TypeCode.Int64:
                case TypeCode.Double:
                case TypeCode.UInt64:
                    return IntPtr.Size == 8;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see
        /// cref="ConcurrentKeyedList{TKey,TValue}"/>
        /// class that is empty, has the default concurrency level, has the default initial capacity, and
        /// uses the default comparer for the key type.
        /// </summary>
        public ConcurrentKeyedList() : this(DefaultConcurrencyLevel, DEFAULT_CAPACITY, true, EqualityComparer<TKey>.Default) { }

        /// <summary>
        /// Initializes a new instance of the <see
        /// cref="ConcurrentKeyedList{TKey,TValue}"/>
        /// class that is empty, has the specified concurrency level and capacity, and uses the default
        /// comparer for the key type.
        /// </summary>
        /// <param name="concurrencyLevel">The estimated number of threads that will update the
        /// <see cref="ConcurrentKeyedList{TKey,TValue}"/> concurrently.</param>
        /// <param name="capacity">The initial number of elements that the <see
        /// cref="ConcurrentKeyedList{TKey,TValue}"/>
        /// can contain.</param>
        /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="concurrencyLevel"/> is
        /// less than 1.</exception>
        /// <exception cref="T:System.ArgumentOutOfRangeException"> <paramref name="capacity"/> is less than
        /// 0.</exception>
        public ConcurrentKeyedList(int concurrencyLevel, int capacity) : this(concurrencyLevel, capacity, false, EqualityComparer<TKey>.Default) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="ConcurrentKeyedList{TKey,TValue}"/>
        /// class that contains elements copied from the specified <see
        /// cref="T:System.Collections.IEnumerable{KeyValuePair{TKey,TValue}}"/>, has the default concurrency
        /// level, has the default initial capacity, and uses the default comparer for the key type.
        /// </summary>
        /// <param name="collection">The <see
        /// cref="T:System.Collections.IEnumerable{KeyValuePair{TKey,TValue}}"/> whose elements are copied to
        /// the new
        /// <see cref="ConcurrentKeyedList{TKey,TValue}"/>.</param>
        /// <exception cref="T:System.ArgumentNullException"><paramref name="collection"/> is a null reference
        /// (Nothing in Visual Basic).</exception>
        /// <exception cref="T:System.ArgumentException"><paramref name="collection"/> contains one or more
        /// duplicate keys.</exception>
        public ConcurrentKeyedList(IEnumerable<KeyValuePair<TKey, TValue>> collection) : this(collection, EqualityComparer<TKey>.Default) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="ConcurrentKeyedList{TKey,TValue}"/>
        /// class that is empty, has the specified concurrency level and capacity, and uses the specified
        /// <see cref="T:System.Collections.Generic.IEqualityComparer{TKey}"/>.
        /// </summary>
        /// <param name="comparer">The <see cref="T:System.Collections.Generic.IEqualityComparer{TKey}"/>
        /// implementation to use when comparing keys.</param>
        /// <exception cref="T:System.ArgumentNullException"><paramref name="comparer"/> is a null reference
        /// (Nothing in Visual Basic).</exception>
        public ConcurrentKeyedList(IEqualityComparer<TKey> comparer) : this(DefaultConcurrencyLevel, DEFAULT_CAPACITY, true, comparer) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="ConcurrentKeyedList{TKey,TValue}"/>
        /// class that contains elements copied from the specified <see
        /// cref="T:System.Collections.IEnumerable"/>, has the default concurrency level, has the default
        /// initial capacity, and uses the specified
        /// <see cref="T:System.Collections.Generic.IEqualityComparer{TKey}"/>.
        /// </summary>
        /// <param name="collection">The <see
        /// cref="T:System.Collections.IEnumerable{KeyValuePair{TKey,TValue}}"/> whose elements are copied to
        /// the new
        /// <see cref="ConcurrentKeyedList{TKey,TValue}"/>.</param>
        /// <param name="comparer">The <see cref="T:System.Collections.Generic.IEqualityComparer{TKey}"/>
        /// implementation to use when comparing keys.</param>
        /// <exception cref="T:System.ArgumentNullException"><paramref name="collection"/> is a null reference
        /// (Nothing in Visual Basic). -or-
        /// <paramref name="comparer"/> is a null reference (Nothing in Visual Basic).
        /// </exception>
        public ConcurrentKeyedList(IEnumerable<KeyValuePair<TKey, TValue>> collection, IEqualityComparer<TKey> comparer)
            : this(comparer)
        {
            if (collection == null) throw new ArgumentNullException("collection");

            InitializeFromCollection(collection);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ConcurrentKeyedList{TKey,TValue}"/> 
        /// class that contains elements copied from the specified <see cref="T:System.Collections.IEnumerable"/>, 
        /// has the specified concurrency level, has the specified initial capacity, and uses the specified 
        /// <see cref="T:System.Collections.Generic.IEqualityComparer{TKey}"/>.
        /// </summary>
        /// <param name="concurrencyLevel">The estimated number of threads that will update the 
        /// <see cref="ConcurrentKeyedList{TKey,TValue}"/> concurrently.</param>
        /// <param name="collection">The <see cref="T:System.Collections.IEnumerable{KeyValuePair{TKey,TValue}}"/> whose elements are copied to the new 
        /// <see cref="ConcurrentKeyedList{TKey,TValue}"/>.</param>
        /// <param name="comparer">The <see cref="T:System.Collections.Generic.IEqualityComparer{TKey}"/> implementation to use 
        /// when comparing keys.</param>
        /// <exception cref="T:System.ArgumentNullException">
        /// <paramref name="collection"/> is a null reference (Nothing in Visual Basic).
        /// -or-
        /// <paramref name="comparer"/> is a null reference (Nothing in Visual Basic).
        /// </exception>
        /// <exception cref="T:System.ArgumentOutOfRangeException">
        /// <paramref name="concurrencyLevel"/> is less than 1.
        /// </exception>
        /// <exception cref="T:System.ArgumentException"><paramref name="collection"/> contains one or more duplicate keys.</exception>
        public ConcurrentKeyedList(
            int concurrencyLevel, IEnumerable<KeyValuePair<TKey, TValue>> collection, IEqualityComparer<TKey> comparer)
            : this(concurrencyLevel, DEFAULT_CAPACITY, false, comparer)
        {
            if (collection == null) throw new ArgumentNullException("collection");
            if (comparer == null) throw new ArgumentNullException("comparer");

            InitializeFromCollection(collection);
        }

        private void InitializeFromCollection(IEnumerable<KeyValuePair<TKey, TValue>> collection)
        {
            TValue dummy;
            foreach (KeyValuePair<TKey, TValue> pair in collection)
            {
                if (pair.Key == null) throw new ArgumentNullException("key");

                if (!TryAddInternal(pair.Key, pair.Value, false, false, out dummy))
                {
                    throw new ArgumentException(GetResource("ConcurrentDictionary_SourceContainsDuplicateKeys"));
                }
            }

            if (m_budget == 0)
            {
                m_budget = m_tables.m_buckets.Length / m_tables.m_locks.Length;
            }

        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ConcurrentKeyedList{TKey,TValue}"/>
        /// class that is empty, has the specified concurrency level, has the specified initial capacity, and
        /// uses the specified <see cref="T:System.Collections.Generic.IEqualityComparer{TKey}"/>.
        /// </summary>
        /// <param name="concurrencyLevel">The estimated number of threads that will update the
        /// <see cref="ConcurrentKeyedList{TKey,TValue}"/> concurrently.</param>
        /// <param name="capacity">The initial number of elements that the <see
        /// cref="ConcurrentKeyedList{TKey,TValue}"/>
        /// can contain.</param>
        /// <param name="comparer">The <see cref="T:System.Collections.Generic.IEqualityComparer{TKey}"/>
        /// implementation to use when comparing keys.</param>
        /// <exception cref="T:System.ArgumentOutOfRangeException">
        /// <paramref name="concurrencyLevel"/> is less than 1. -or-
        /// <paramref name="capacity"/> is less than 0.
        /// </exception>
        /// <exception cref="T:System.ArgumentNullException"><paramref name="comparer"/> is a null reference
        /// (Nothing in Visual Basic).</exception>
        public ConcurrentKeyedList(int concurrencyLevel, int capacity, IEqualityComparer<TKey> comparer)
            : this(concurrencyLevel, capacity, false, comparer)
        {
        }

        internal ConcurrentKeyedList(int concurrencyLevel, int capacity, bool growLockArray, IEqualityComparer<TKey> comparer)
        {
            if (concurrencyLevel < 1)
            {
                throw new ArgumentOutOfRangeException("concurrencyLevel", GetResource("ConcurrentDictionary_ConcurrencyLevelMustBePositive"));
            }
            if (capacity < 0)
            {
                throw new ArgumentOutOfRangeException("capacity", GetResource("ConcurrentDictionary_CapacityMustNotBeNegative"));
            }
            if (comparer == null) throw new ArgumentNullException("comparer");

            // The capacity should be at least as large as the concurrency level. Otherwise, we would have locks that don't guard
            // any buckets.
            if (capacity < concurrencyLevel)
            {
                capacity = concurrencyLevel;
            }

            object[] locks = new object[concurrencyLevel];
            for (int i = 0; i < locks.Length; i++)
            {
                locks[i] = new object();
            }

            int[] countPerLock = new int[locks.Length];
            Node[] buckets = new Node[capacity];
            m_tables = new Tables(buckets, locks, countPerLock, comparer);

            m_growLockArray = growLockArray;
            m_budget = buckets.Length / locks.Length;
        }


        /// <summary>
        /// Attempts to add the specified key and value to the <see cref="ConcurrentKeyedList{TKey,
        /// TValue}"/>.
        /// </summary>
        /// <param name="key">The key of the element to add.</param>
        /// <param name="value">The value of the element to add. The value can be a null reference (Nothing
        /// in Visual Basic) for reference types.</param>
        /// <returns>true if the key/value pair was added to the <see cref="ConcurrentKeyedList{TKey,
        /// TValue}"/>
        /// successfully; otherwise, false.</returns>
        /// <exception cref="T:System.ArgumentNullException"><paramref name="key"/> is null reference
        /// (Nothing in Visual Basic).</exception>
        /// <exception cref="T:System.OverflowException">The <see cref="ConcurrentKeyedList{TKey, TValue}"/>
        /// contains too many elements.</exception>
        public bool TryAdd(TKey key, TValue value)
        {
            if (key == null) throw new ArgumentNullException("key");
            TValue dummy;
            return TryAddInternal(key, value, false, true, out dummy);
        }

        /// <summary>
        /// Determines whether the <see cref="ConcurrentKeyedList{TKey, TValue}"/> contains the specified
        /// key.
        /// </summary>
        /// <param name="key">The key to locate in the <see cref="ConcurrentKeyedList{TKey,
        /// TValue}"/>.</param>
        /// <returns>true if the <see cref="ConcurrentKeyedList{TKey, TValue}"/> contains an element with
        /// the specified key; otherwise, false.</returns>
        /// <exception cref="T:System.ArgumentNullException"><paramref name="key"/> is a null reference
        /// (Nothing in Visual Basic).</exception>
        public bool ContainsKey(TKey key)
        {
            if (key == null) throw new ArgumentNullException("key");

            TValue throwAwayValue;
            return TryGetValue(key, out throwAwayValue);
        }

        /// <summary>
        /// Attempts to remove and return the the value with the specified key from the
        /// <see cref="ConcurrentKeyedList{TKey, TValue}"/>.
        /// </summary>
        /// <param name="key">The key of the element to remove and return.</param>
        /// <param name="value">When this method returns, <paramref name="value"/> contains the object removed from the
        /// <see cref="ConcurrentKeyedList{TKey,TValue}"/> or the default value of <typeparamref
        /// name="TValue"/>
        /// if the operation failed.</param>
        /// <returns>true if an object was removed successfully; otherwise, false.</returns>
        /// <exception cref="T:System.ArgumentNullException"><paramref name="key"/> is a null reference
        /// (Nothing in Visual Basic).</exception>
        public bool TryRemove(TKey key, out TValue value)
        {
            if (key == null) throw new ArgumentNullException("key");

            return TryRemoveInternal(key, out value, false, default(TValue));
        }

        /// <summary>
        /// Removes the specified key from the dictionary if it exists and returns its associated value.
        /// If matchValue flag is set, the key will be removed only if is associated with a particular
        /// value.
        /// </summary>
        /// <param name="key">The key to search for and remove if it exists.</param>
        /// <param name="value">The variable into which the removed value, if found, is stored.</param>
        /// <param name="matchValue">Whether removal of the key is conditional on its value.</param>
        /// <param name="oldValue">The conditional value to compare against if <paramref name="matchValue"/> is true</param>
        /// <returns></returns>
        [SuppressMessage("Microsoft.Concurrency", "CA8001", Justification = "Reviewed for thread safety")]
        private bool TryRemoveInternal(TKey key, out TValue value, bool matchValue, TValue oldValue)
        {
            while (true)
            {
                Tables tables = m_tables;

                IEqualityComparer<TKey> comparer = tables.m_comparer;

                int bucketNo, lockNo;
                GetBucketAndLockNo(comparer.GetHashCode(key), out bucketNo, out lockNo, tables.m_buckets.Length, tables.m_locks.Length);

                lock (tables.m_locks[lockNo])
                {
                    // If the table just got resized, we may not be holding the right lock, and must retry.
                    // This should be a rare occurence.
                    if (tables != m_tables)
                    {
                        continue;
                    }

                    Node prev = null;
                    for (Node curr = tables.m_buckets[bucketNo]; curr != null; curr = curr.m_next)
                    {
                        Assert((prev == null && curr == tables.m_buckets[bucketNo]) || prev.m_next == curr);

                        if (comparer.Equals(curr.m_key, key))
                        {
                            if (matchValue)
                            {
                                bool valuesMatch = EqualityComparer<TValue>.Default.Equals(oldValue, curr.m_value);
                                if (!valuesMatch)
                                {
                                    value = default(TValue);
                                    return false;
                                }
                            }

                            if (prev == null)
                            {
                                Volatile.Write<Node>(ref tables.m_buckets[bucketNo], curr.m_next);
                            }
                            else
                            {
                                prev.m_next = curr.m_next;
                            }

                            value = curr.m_value;
                            tables.m_countPerLock[lockNo]--;
                            return true;
                        }
                        prev = curr;
                    }
                }

                value = default(TValue);
                return false;
            }
        }

        /// <summary>
        /// Attempts to get the value associated with the specified key from the <see
        /// cref="ConcurrentKeyedList{TKey,TValue}"/>.
        /// </summary>
        /// <param name="key">The key of the value to get.</param>
        /// <param name="value">When this method returns, <paramref name="value"/> contains the object from
        /// the
        /// <see cref="ConcurrentKeyedList{TKey,TValue}"/> with the specified key or the default value of
        /// <typeparamref name="TValue"/>, if the operation failed.</param>
        /// <returns>true if the key was found in the <see cref="ConcurrentKeyedList{TKey,TValue}"/>;
        /// otherwise, false.</returns>
        /// <exception cref="T:System.ArgumentNullException"><paramref name="key"/> is a null reference
        /// (Nothing in Visual Basic).</exception>
        [SuppressMessage("Microsoft.Concurrency", "CA8001", Justification = "Reviewed for thread safety")]
        public bool TryGetValue(TKey key, out TValue value)
        {
            if (key == null) throw new ArgumentNullException("key");

            int bucketNo, lockNoUnused;

            // We must capture the m_buckets field in a local variable. It is set to a new table on each table resize.
            Tables tables = m_tables;
            IEqualityComparer<TKey> comparer = tables.m_comparer;
            GetBucketAndLockNo(comparer.GetHashCode(key), out bucketNo, out lockNoUnused, tables.m_buckets.Length, tables.m_locks.Length);

            // We can get away w/out a lock here.
            // The Volatile.Read ensures that the load of the fields of 'n' doesn't move before the load from buckets[i].
            Node n = Volatile.Read<Node>(ref tables.m_buckets[bucketNo]);

            while (n != null)
            {
                if (comparer.Equals(n.m_key, key))
                {
                    value = n.m_value;
                    return true;
                }
                n = n.m_next;
            }

            value = default(TValue);
            return false;
        }

        /// <summary>
        /// Compares the existing value for the specified key with a specified value, and if they're equal,
        /// updates the key with a third value.
        /// </summary>
        /// <param name="key">The key whose value is compared with <paramref name="comparisonValue"/> and
        /// possibly replaced.</param>
        /// <param name="newValue">The value that replaces the value of the element with <paramref
        /// name="key"/> if the comparison results in equality.</param>
        /// <param name="comparisonValue">The value that is compared to the value of the element with
        /// <paramref name="key"/>.</param>
        /// <returns>true if the value with <paramref name="key"/> was equal to <paramref
        /// name="comparisonValue"/> and replaced with <paramref name="newValue"/>; otherwise,
        /// false.</returns>
        /// <exception cref="T:System.ArgumentNullException"><paramref name="key"/> is a null
        /// reference.</exception>
        [SuppressMessage("Microsoft.Concurrency", "CA8001", Justification = "Reviewed for thread safety")]
        public bool TryUpdate(TKey key, TValue newValue, TValue comparisonValue)
        {
            if (key == null) throw new ArgumentNullException("key");

            IEqualityComparer<TValue> valueComparer = EqualityComparer<TValue>.Default;

            while (true)
            {
                int bucketNo;
                int lockNo;
                int hashcode;

                Tables tables = m_tables;
                IEqualityComparer<TKey> comparer = tables.m_comparer;

                hashcode = comparer.GetHashCode(key);
                GetBucketAndLockNo(hashcode, out bucketNo, out lockNo, tables.m_buckets.Length, tables.m_locks.Length);

                lock (tables.m_locks[lockNo])
                {
                    // If the table just got resized, we may not be holding the right lock, and must retry.
                    // This should be a rare occurence.
                    if (tables != m_tables)
                    {
                        continue;
                    }

                    // Try to find this key in the bucket
                    Node prev = null;
                    for (Node node = tables.m_buckets[bucketNo]; node != null; node = node.m_next)
                    {
                        Assert((prev == null && node == tables.m_buckets[bucketNo]) || prev.m_next == node);
                        if (comparer.Equals(node.m_key, key))
                        {
                            if (valueComparer.Equals(node.m_value, comparisonValue))
                            {
                                if (s_isValueWriteAtomic)
                                {
                                    node.m_value = newValue;
                                }
                                else
                                {
                                    Node newNode = new Node(node.m_key, newValue, hashcode, node.m_next);

                                    if (prev == null)
                                    {
                                        tables.m_buckets[bucketNo] = newNode;
                                    }
                                    else
                                    {
                                        prev.m_next = newNode;
                                    }
                                }

                                return true;
                            }

                            return false;
                        }

                        prev = node;
                    }

                    //didn't find the key
                    return false;
                }
            }
        }

        /// <summary>
        /// Removes all keys and values from the <see cref="ConcurrentKeyedList{TKey,TValue}"/>.
        /// </summary>
        public void Clear()
        {
            int locksAcquired = 0;
            try
            {
                AcquireAllLocks(ref locksAcquired);

                Tables newTables = new Tables(new Node[DEFAULT_CAPACITY], m_tables.m_locks, new int[m_tables.m_countPerLock.Length], m_tables.m_comparer);
                m_tables = newTables;
                m_budget = Math.Max(1, newTables.m_buckets.Length / newTables.m_locks.Length);
            }
            finally
            {
                ReleaseLocks(0, locksAcquired);
            }
        }

        /// <summary>
        /// Copies the elements of the <see cref="T:System.Collections.Generic.ICollection"/> to an array of
        /// type <see cref="T:System.Collections.Generic.KeyValuePair{TKey,TValue}"/>, starting at the
        /// specified array index.
        /// </summary>
        /// <param name="array">The one-dimensional array of type <see
        /// cref="T:System.Collections.Generic.KeyValuePair{TKey,TValue}"/>
        /// that is the destination of the <see
        /// cref="T:System.Collections.Generic.KeyValuePair{TKey,TValue}"/> elements copied from the <see
        /// cref="T:System.Collections.ICollection"/>. The array must have zero-based indexing.</param>
        /// <param name="index">The zero-based index in <paramref name="array"/> at which copying
        /// begins.</param>
        /// <exception cref="T:System.ArgumentNullException"><paramref name="array"/> is a null reference
        /// (Nothing in Visual Basic).</exception>
        /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="index"/> is less than
        /// 0.</exception>
        /// <exception cref="T:System.ArgumentException"><paramref name="index"/> is equal to or greater than
        /// the length of the <paramref name="array"/>. -or- The number of elements in the source <see
        /// cref="T:System.Collections.ICollection"/>
        /// is greater than the available space from <paramref name="index"/> to the end of the destination
        /// <paramref name="array"/>.</exception>
        [SuppressMessage("Microsoft.Concurrency", "CA8001", Justification = "ConcurrencyCop just doesn't know about these locks")]
        void ICollection<KeyValuePair<TKey, TValue>>.CopyTo(KeyValuePair<TKey, TValue>[] array, int index)
        {
            if (array == null) throw new ArgumentNullException("array");
            if (index < 0) throw new ArgumentOutOfRangeException("index", GetResource("ConcurrentDictionary_IndexIsNegative"));

            int locksAcquired = 0;
            try
            {
                AcquireAllLocks(ref locksAcquired);

                int count = 0;

                for (int i = 0; i < m_tables.m_locks.Length && count >= 0; i++)
                {
                    count += m_tables.m_countPerLock[i];
                }

                if (array.Length - count < index || count < 0) //"count" itself or "count + index" can overflow
                {
                    throw new ArgumentException(GetResource("ConcurrentDictionary_ArrayNotLargeEnough"));
                }

                CopyToPairs(array, index);
            }
            finally
            {
                ReleaseLocks(0, locksAcquired);
            }
        }

        /// <summary>
        /// Copies the key and value pairs stored in the <see cref="ConcurrentKeyedList{TKey,TValue}"/> to a
        /// new array.
        /// </summary>
        /// <returns>A new array containing a snapshot of key and value pairs copied from the <see
        /// cref="ConcurrentKeyedList{TKey,TValue}"/>.</returns>
        [SuppressMessage("Microsoft.Concurrency", "CA8001", Justification = "ConcurrencyCop just doesn't know about these locks")]
        public KeyValuePair<TKey, TValue>[] ToArray()
        {
            int locksAcquired = 0;
            try
            {
                AcquireAllLocks(ref locksAcquired);
                int count = 0;
                checked
                {
                    for (int i = 0; i < m_tables.m_locks.Length; i++)
                    {
                        count += m_tables.m_countPerLock[i];
                    }
                }

                if (count == 0)
                {
                    return Array.Empty<KeyValuePair<TKey, TValue>>();
                }

                KeyValuePair<TKey, TValue>[] array = new KeyValuePair<TKey, TValue>[count];

                CopyToPairs(array, 0);
                return array;
            }
            finally
            {
                ReleaseLocks(0, locksAcquired);
            }
        }

        /// <summary>
        /// Copy dictionary contents to an array - shared implementation between ToArray and CopyTo.
        /// 
        /// Important: the caller must hold all locks in m_locks before calling CopyToPairs.
        /// </summary>
        private void CopyToPairs(KeyValuePair<TKey, TValue>[] array, int index)
        {
            Node[] buckets = m_tables.m_buckets;
            for (int i = 0; i < buckets.Length; i++)
            {
                for (Node current = buckets[i]; current != null; current = current.m_next)
                {
                    array[index] = new KeyValuePair<TKey, TValue>(current.m_key, current.m_value);
                    index++; //this should never flow, CopyToPairs is only called when there's no overflow risk
                }
            }
        }

        /// <summary>
        /// Copy dictionary contents to an array - shared implementation between ToArray and CopyTo.
        /// 
        /// Important: the caller must hold all locks in m_locks before calling CopyToEntries.
        /// </summary>
        private void CopyToEntries(DictionaryEntry[] array, int index)
        {
            Node[] buckets = m_tables.m_buckets;
            for (int i = 0; i < buckets.Length; i++)
            {
                for (Node current = buckets[i]; current != null; current = current.m_next)
                {
                    array[index] = new DictionaryEntry(current.m_key, current.m_value);
                    index++;  //this should never flow, CopyToEntries is only called when there's no overflow risk
                }
            }
        }

        /// <summary>
        /// Copy dictionary contents to an array - shared implementation between ToArray and CopyTo.
        /// 
        /// Important: the caller must hold all locks in m_locks before calling CopyToObjects.
        /// </summary>
        private void CopyToObjects(object[] array, int index)
        {
            Node[] buckets = m_tables.m_buckets;
            for (int i = 0; i < buckets.Length; i++)
            {
                for (Node current = buckets[i]; current != null; current = current.m_next)
                {
                    array[index] = new KeyValuePair<TKey, TValue>(current.m_key, current.m_value);
                    index++; //this should never flow, CopyToObjects is only called when there's no overflow risk
                }
            }
        }

        /// <summary>Returns an enumerator that iterates through the <see
        /// cref="ConcurrentKeyedList{TKey,TValue}"/>.</summary>
        /// <returns>An enumerator for the <see cref="ConcurrentKeyedList{TKey,TValue}"/>.</returns>
        /// <remarks>
        /// The enumerator returned from the dictionary is safe to use concurrently with
        /// reads and writes to the dictionary, however it does not represent a moment-in-time snapshot
        /// of the dictionary.  The contents exposed through the enumerator may contain modifications
        /// made to the dictionary after <see cref="GetEnumerator"/> was called.
        /// </remarks>
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            Node[] buckets = m_tables.m_buckets;

            for (int i = 0; i < buckets.Length; i++)
            {
                // The Volatile.Read ensures that the load of the fields of 'current' doesn't move before the load from buckets[i].
                Node current = Volatile.Read<Node>(ref buckets[i]);

                while (current != null)
                {
                    yield return new KeyValuePair<TKey, TValue>(current.m_key, current.m_value);
                    current = current.m_next;
                }
            }
        }

        /// <summary>
        /// Shared internal implementation for inserts and updates.
        /// If key exists, we always return false; and if updateIfExists == true we force update with value;
        /// If key doesn't exist, we always add value and return true;
        /// </summary>
        [SuppressMessage("Microsoft.Concurrency", "CA8001", Justification = "Reviewed for thread safety")]
        private bool TryAddInternal(TKey key, TValue value, bool updateIfExists, bool acquireLock, out TValue resultingValue)
        {
            while (true)
            {
                int bucketNo, lockNo;
                int hashcode;

                Tables tables = m_tables;
                IEqualityComparer<TKey> comparer = tables.m_comparer;
                hashcode = comparer.GetHashCode(key);
                GetBucketAndLockNo(hashcode, out bucketNo, out lockNo, tables.m_buckets.Length, tables.m_locks.Length);

                bool resizeDesired = false;
                bool lockTaken = false;
#if FEATURE_RANDOMIZED_STRING_HASHING
#if !FEATURE_CORECLR
                bool resizeDueToCollisions = false;
#endif // !FEATURE_CORECLR
#endif

                try
                {
                    if (acquireLock)
                        Monitor.Enter(tables.m_locks[lockNo], ref lockTaken);

                    // If the table just got resized, we may not be holding the right lock, and must retry.
                    // This should be a rare occurence.
                    if (tables != m_tables)
                    {
                        continue;
                    }

#if FEATURE_RANDOMIZED_STRING_HASHING
#if !FEATURE_CORECLR
                    int collisionCount = 0;
#endif // !FEATURE_CORECLR
#endif

                    // Try to find this key in the bucket
                    Node prev = null;
                    for (Node node = tables.m_buckets[bucketNo]; node != null; node = node.m_next)
                    {
                        Assert((prev == null && node == tables.m_buckets[bucketNo]) || prev.m_next == node);
                        if (comparer.Equals(node.m_key, key))
                        {
                            // The key was found in the dictionary. If updates are allowed, update the value for that key.
                            // We need to create a new node for the update, in order to support TValue types that cannot
                            // be written atomically, since lock-free reads may be happening concurrently.
                            if (updateIfExists)
                            {
                                if (s_isValueWriteAtomic)
                                {
                                    node.m_value = value;
                                }
                                else
                                {
                                    Node newNode = new Node(node.m_key, value, hashcode, node.m_next);
                                    if (prev == null)
                                    {
                                        tables.m_buckets[bucketNo] = newNode;
                                    }
                                    else
                                    {
                                        prev.m_next = newNode;
                                    }
                                }
                                resultingValue = value;
                            }
                            else
                            {
                                resultingValue = node.m_value;
                            }
                            return false;
                        }
                        prev = node;

#if FEATURE_RANDOMIZED_STRING_HASHING
#if !FEATURE_CORECLR
                        collisionCount++;
#endif // !FEATURE_CORECLR
#endif
                    }

#if FEATURE_RANDOMIZED_STRING_HASHING
#if !FEATURE_CORECLR
                    if(collisionCount > HashHelpers.HashCollisionThreshold && HashHelpers.IsWellKnownEqualityComparer(comparer)) 
                    {
                        resizeDesired = true;
                        resizeDueToCollisions = true;
                    }
#endif // !FEATURE_CORECLR
#endif

                    // The key was not found in the bucket. Insert the key-value pair.
                    Volatile.Write<Node>(ref tables.m_buckets[bucketNo], new Node(key, value, hashcode, tables.m_buckets[bucketNo]));
                    checked
                    {
                        tables.m_countPerLock[lockNo]++;
                    }

                    //
                    // If the number of elements guarded by this lock has exceeded the budget, resize the bucket table.
                    // It is also possible that GrowTable will increase the budget but won't resize the bucket table.
                    // That happens if the bucket table is found to be poorly utilized due to a bad hash function.
                    //
                    if (tables.m_countPerLock[lockNo] > m_budget)
                    {
                        resizeDesired = true;
                    }
                }
                finally
                {
                    if (lockTaken)
                        Monitor.Exit(tables.m_locks[lockNo]);
                }

                //
                // The fact that we got here means that we just performed an insertion. If necessary, we will grow the table.
                //
                // Concurrency notes:
                // - Notice that we are not holding any locks at when calling GrowTable. This is necessary to prevent deadlocks.
                // - As a result, it is possible that GrowTable will be called unnecessarily. But, GrowTable will obtain lock 0
                //   and then verify that the table we passed to it as the argument is still the current table.
                //
                if (resizeDesired)
                {
#if FEATURE_RANDOMIZED_STRING_HASHING
#if !FEATURE_CORECLR
                    if (resizeDueToCollisions)
                    {
                        GrowTable(tables, (IEqualityComparer<TKey>)HashHelpers.GetRandomizedEqualityComparer(comparer), true, m_keyRehashCount);
                    }
                    else
#endif // !FEATURE_CORECLR
                    {
                        GrowTable(tables, tables.m_comparer, false, m_keyRehashCount);
                    }
#else
                    GrowTable(tables, tables.m_comparer, false, m_keyRehashCount);
#endif
                }

                resultingValue = value;
                return true;
            }
        }

        /// <summary>
        /// Gets or sets the value associated with the specified key.
        /// </summary>
        /// <param name="key">The key of the value to get or set.</param>
        /// <value>The value associated with the specified key. If the specified key is not found, a get
        /// operation throws a
        /// <see cref="T:Sytem.Collections.Generic.KeyNotFoundException"/>, and a set operation creates a new
        /// element with the specified key.</value>
        /// <exception cref="T:System.ArgumentNullException"><paramref name="key"/> is a null reference
        /// (Nothing in Visual Basic).</exception>
        /// <exception cref="T:System.Collections.Generic.KeyNotFoundException">The property is retrieved and
        /// <paramref name="key"/>
        /// does not exist in the collection.</exception>
        public TValue this[TKey key]
        {
            get
            {
                TValue value;
                if (!TryGetValue(key, out value))
                {
                    throw new KeyNotFoundException();
                }
                return value;
            }
            set
            {
                if (key == null) throw new ArgumentNullException("key");
                TValue dummy;
                TryAddInternal(key, value, true, true, out dummy);
            }
        }

        /// <summary>
        /// Gets the number of key/value pairs contained in the <see
        /// cref="ConcurrentKeyedList{TKey,TValue}"/>.
        /// </summary>
        /// <exception cref="T:System.OverflowException">The dictionary contains too many
        /// elements.</exception>
        /// <value>The number of key/value paris contained in the <see
        /// cref="ConcurrentKeyedList{TKey,TValue}"/>.</value>
        /// <remarks>Count has snapshot semantics and represents the number of items in the <see
        /// cref="ConcurrentKeyedList{TKey,TValue}"/>
        /// at the moment when Count was accessed.</remarks>
        public int Count
        {
            [SuppressMessage("Microsoft.Concurrency", "CA8001", Justification = "ConcurrencyCop just doesn't know about these locks")]
            get
            {
                int acquiredLocks = 0;
                try
                {
                    // Acquire all locks
                    AcquireAllLocks(ref acquiredLocks);

                    return GetCountInternal();
                }
                finally
                {
                    // Release locks that have been acquired earlier
                    ReleaseLocks(0, acquiredLocks);
                }
            }
        }

        /// <summary>
        /// Gets the number of key/value pairs contained in the <see
        /// cref="ConcurrentKeyedList{TKey,TValue}"/>. Should only be used after all locks
        /// have been acquired.
        /// </summary>
        /// <exception cref="T:System.OverflowException">The dictionary contains too many
        /// elements.</exception>
        /// <value>The number of key/value pairs contained in the <see
        /// cref="ConcurrentKeyedList{TKey,TValue}"/>.</value>
        /// <remarks>Count has snapshot semantics and represents the number of items in the <see
        /// cref="ConcurrentKeyedList{TKey,TValue}"/>
        /// at the moment when Count was accessed.</remarks>
        private int GetCountInternal()
        {
            int count = 0;

            // Compute the count, we allow overflow
            for (int i = 0; i < m_tables.m_countPerLock.Length; i++)
            {
                count += m_tables.m_countPerLock[i];
            }

            return count;
        }

        /// <summary>
        /// Adds a key/value pair to the <see cref="ConcurrentKeyedList{TKey,TValue}"/> 
        /// if the key does not already exist.
        /// </summary>
        /// <param name="key">The key of the element to add.</param>
        /// <param name="valueFactory">The function used to generate a value for the key</param>
        /// <exception cref="T:System.ArgumentNullException"><paramref name="key"/> is a null reference
        /// (Nothing in Visual Basic).</exception>
        /// <exception cref="T:System.ArgumentNullException"><paramref name="valueFactory"/> is a null reference
        /// (Nothing in Visual Basic).</exception>
        /// <exception cref="T:System.OverflowException">The dictionary contains too many
        /// elements.</exception>
        /// <returns>The value for the key.  This will be either the existing value for the key if the
        /// key is already in the dictionary, or the new value for the key as returned by valueFactory
        /// if the key was not in the dictionary.</returns>
        public TValue GetOrAdd(TKey key, Func<TKey, TValue> valueFactory)
        {
            if (key == null) throw new ArgumentNullException("key");
            if (valueFactory == null) throw new ArgumentNullException("valueFactory");

            TValue resultingValue;
            if (TryGetValue(key, out resultingValue))
            {
                return resultingValue;
            }
            TryAddInternal(key, valueFactory(key), false, true, out resultingValue);
            return resultingValue;
        }

        /// <summary>
        /// Adds a key/value pair to the <see cref="ConcurrentKeyedList{TKey,TValue}"/> 
        /// if the key does not already exist.
        /// </summary>
        /// <param name="key">The key of the element to add.</param>
        /// <param name="value">the value to be added, if the key does not already exist</param>
        /// <exception cref="T:System.ArgumentNullException"><paramref name="key"/> is a null reference
        /// (Nothing in Visual Basic).</exception>
        /// <exception cref="T:System.OverflowException">The dictionary contains too many
        /// elements.</exception>
        /// <returns>The value for the key.  This will be either the existing value for the key if the 
        /// key is already in the dictionary, or the new value if the key was not in the dictionary.</returns>
        public TValue GetOrAdd(TKey key, TValue value)
        {
            if (key == null) throw new ArgumentNullException("key");

            TValue resultingValue;
            TryAddInternal(key, value, false, true, out resultingValue);
            return resultingValue;
        }

        /// <summary>
        /// Adds a key/value pair to the <see cref="ConcurrentKeyedList{TKey,TValue}"/> 
        /// if the key does not already exist.
        /// </summary>
        /// <param name="key">The key of the element to add.</param>
        /// <param name="valueFactory">The function used to generate a value for the key</param>
        /// <param name="factoryArgument">An argument value to pass into <paramref name="valueFactory"/>.</param>
        /// <exception cref="T:System.ArgumentNullException"><paramref name="key"/> is a null reference
        /// (Nothing in Visual Basic).</exception>
        /// <exception cref="T:System.ArgumentNullException"><paramref name="valueFactory"/> is a null reference
        /// (Nothing in Visual Basic).</exception>
        /// <exception cref="T:System.OverflowException">The dictionary contains too many
        /// elements.</exception>
        /// <returns>The value for the key.  This will be either the existing value for the key if the
        /// key is already in the dictionary, or the new value for the key as returned by valueFactory
        /// if the key was not in the dictionary.</returns>
        public TValue GetOrAdd<TArg>(TKey key, Func<TKey, TArg, TValue> valueFactory, TArg factoryArgument)
        {
            if (key == null) throw new ArgumentNullException("key");
            if (valueFactory == null) throw new ArgumentNullException("valueFactory");

            TValue resultingValue;
            if (!TryGetValue(key, out resultingValue))
            {
                TryAddInternal(key, valueFactory(key, factoryArgument), false, true, out resultingValue);
            }
            return resultingValue;
        }

        /// <summary>
        /// Adds a key/value pair to the <see cref="ConcurrentKeyedList{TKey,TValue}"/> if the key does not already 
        /// exist, or updates a key/value pair in the <see cref="ConcurrentKeyedList{TKey,TValue}"/> if the key 
        /// already exists.
        /// </summary>
        /// <param name="key">The key to be added or whose value should be updated</param>
        /// <param name="addValueFactory">The function used to generate a value for an absent key</param>
        /// <param name="updateValueFactory">The function used to generate a new value for an existing key
        /// based on the key's existing value</param>
        /// <param name="factoryArgument">An argument to pass into <paramref name="addValueFactory"/> and <paramref name="updateValueFactory"/>.</param>
        /// <exception cref="T:System.ArgumentNullException"><paramref name="key"/> is a null reference
        /// (Nothing in Visual Basic).</exception>
        /// <exception cref="T:System.ArgumentNullException"><paramref name="addValueFactory"/> is a null reference
        /// (Nothing in Visual Basic).</exception>
        /// <exception cref="T:System.ArgumentNullException"><paramref name="updateValueFactory"/> is a null reference
        /// (Nothing in Visual Basic).</exception>
        /// <exception cref="T:System.OverflowException">The dictionary contains too many
        /// elements.</exception>
        /// <returns>The new value for the key.  This will be either be the result of addValueFactory (if the key was 
        /// absent) or the result of updateValueFactory (if the key was present).</returns>
        public TValue AddOrUpdate<TArg>(
            TKey key, Func<TKey, TArg, TValue> addValueFactory, Func<TKey, TValue, TArg, TValue> updateValueFactory, TArg factoryArgument)
        {
            if (key == null) throw new ArgumentNullException("key");
            if (addValueFactory == null) throw new ArgumentNullException("addValueFactory");
            if (updateValueFactory == null) throw new ArgumentNullException("updateValueFactory");

            while (true)
            {
                TValue oldValue;
                if (TryGetValue(key, out oldValue))
                {
                    // key exists, try to update
                    TValue newValue = updateValueFactory(key, oldValue, factoryArgument);
                    if (TryUpdate(key, newValue, oldValue))
                    {
                        return newValue;
                    }
                }
                else
                {
                    // key doesn't exist, try to add
                    TValue resultingValue;
                    if (TryAddInternal(key, addValueFactory(key, factoryArgument), false, true, out resultingValue))
                    {
                        return resultingValue;
                    }
                }
            }
        }

        /// <summary>
        /// Adds a key/value pair to the <see cref="ConcurrentKeyedList{TKey,TValue}"/> if the key does not already 
        /// exist, or updates a key/value pair in the <see cref="ConcurrentKeyedList{TKey,TValue}"/> if the key 
        /// already exists.
        /// </summary>
        /// <param name="key">The key to be added or whose value should be updated</param>
        /// <param name="addValueFactory">The function used to generate a value for an absent key</param>
        /// <param name="updateValueFactory">The function used to generate a new value for an existing key
        /// based on the key's existing value</param>
        /// <exception cref="T:System.ArgumentNullException"><paramref name="key"/> is a null reference
        /// (Nothing in Visual Basic).</exception>
        /// <exception cref="T:System.ArgumentNullException"><paramref name="addValueFactory"/> is a null reference
        /// (Nothing in Visual Basic).</exception>
        /// <exception cref="T:System.ArgumentNullException"><paramref name="updateValueFactory"/> is a null reference
        /// (Nothing in Visual Basic).</exception>
        /// <exception cref="T:System.OverflowException">The dictionary contains too many
        /// elements.</exception>
        /// <returns>The new value for the key.  This will be either be the result of addValueFactory (if the key was 
        /// absent) or the result of updateValueFactory (if the key was present).</returns>
        public TValue AddOrUpdate(TKey key, Func<TKey, TValue> addValueFactory, Func<TKey, TValue, TValue> updateValueFactory)
        {
            if (key == null) throw new ArgumentNullException("key");
            if (addValueFactory == null) throw new ArgumentNullException("addValueFactory");
            if (updateValueFactory == null) throw new ArgumentNullException("updateValueFactory");

            TValue newValue, resultingValue;
            while (true)
            {
                TValue oldValue;
                if (TryGetValue(key, out oldValue))
                //key exists, try to update
                {
                    newValue = updateValueFactory(key, oldValue);
                    if (TryUpdate(key, newValue, oldValue))
                    {
                        return newValue;
                    }
                }
                else //try add
                {
                    newValue = addValueFactory(key);
                    if (TryAddInternal(key, newValue, false, true, out resultingValue))
                    {
                        return resultingValue;
                    }
                }
            }
        }

        /// <summary>
        /// Adds a key/value pair to the <see cref="ConcurrentKeyedList{TKey,TValue}"/> if the key does not already 
        /// exist, or updates a key/value pair in the <see cref="ConcurrentKeyedList{TKey,TValue}"/> if the key 
        /// already exists.
        /// </summary>
        /// <param name="key">The key to be added or whose value should be updated</param>
        /// <param name="addValue">The value to be added for an absent key</param>
        /// <param name="updateValueFactory">The function used to generate a new value for an existing key based on 
        /// the key's existing value</param>
        /// <exception cref="T:System.ArgumentNullException"><paramref name="key"/> is a null reference
        /// (Nothing in Visual Basic).</exception>
        /// <exception cref="T:System.ArgumentNullException"><paramref name="updateValueFactory"/> is a null reference
        /// (Nothing in Visual Basic).</exception>
        /// <exception cref="T:System.OverflowException">The dictionary contains too many
        /// elements.</exception>
        /// <returns>The new value for the key.  This will be either be the result of addValueFactory (if the key was 
        /// absent) or the result of updateValueFactory (if the key was present).</returns>
        public TValue AddOrUpdate(TKey key, TValue addValue, Func<TKey, TValue, TValue> updateValueFactory)
        {
            if (key == null) throw new ArgumentNullException("key");
            if (updateValueFactory == null) throw new ArgumentNullException("updateValueFactory");
            TValue newValue, resultingValue;
            while (true)
            {
                TValue oldValue;
                if (TryGetValue(key, out oldValue))
                //key exists, try to update
                {
                    newValue = updateValueFactory(key, oldValue);
                    if (TryUpdate(key, newValue, oldValue))
                    {
                        return newValue;
                    }
                }
                else //try add
                {
                    if (TryAddInternal(key, addValue, false, true, out resultingValue))
                    {
                        return resultingValue;
                    }
                }
            }
        }



        /// <summary>
        /// Gets a value that indicates whether the <see cref="ConcurrentKeyedList{TKey,TValue}"/> is empty.
        /// </summary>
        /// <value>true if the <see cref="ConcurrentKeyedList{TKey,TValue}"/> is empty; otherwise,
        /// false.</value>
        public bool IsEmpty
        {
            [SuppressMessage("Microsoft.Concurrency", "CA8001", Justification = "ConcurrencyCop just doesn't know about these locks")]
            get
            {
                int acquiredLocks = 0;
                try
                {
                    // Acquire all locks
                    AcquireAllLocks(ref acquiredLocks);

                    for (int i = 0; i < m_tables.m_countPerLock.Length; i++)
                    {
                        if (m_tables.m_countPerLock[i] != 0)
                        {
                            return false;
                        }
                    }
                }
                finally
                {
                    // Release locks that have been acquired earlier
                    ReleaseLocks(0, acquiredLocks);
                }

                return true;
            }
        }

        #region IDictionary<TKey,TValue> members

        /// <summary>
        /// Adds the specified key and value to the <see
        /// cref="T:System.Collections.Generic.IDictionary{TKey,TValue}"/>.
        /// </summary>
        /// <param name="key">The object to use as the key of the element to add.</param>
        /// <param name="value">The object to use as the value of the element to add.</param>
        /// <exception cref="T:System.ArgumentNullException"><paramref name="key"/> is a null reference
        /// (Nothing in Visual Basic).</exception>
        /// <exception cref="T:System.OverflowException">The dictionary contains too many
        /// elements.</exception>
        /// <exception cref="T:System.ArgumentException">
        /// An element with the same key already exists in the <see
        /// cref="ConcurrentKeyedList{TKey,TValue}"/>.</exception>
        void IDictionary<TKey, TValue>.Add(TKey key, TValue value)
        {
            if (!TryAdd(key, value))
            {
                throw new ArgumentException(GetResource("ConcurrentDictionary_KeyAlreadyExisted"));
            }
        }

        /// <summary>
        /// Removes the element with the specified key from the <see
        /// cref="T:System.Collections.Generic.IDictionary{TKey,TValue}"/>.
        /// </summary>
        /// <param name="key">The key of the element to remove.</param>
        /// <returns>true if the element is successfully remove; otherwise false. This method also returns
        /// false if
        /// <paramref name="key"/> was not found in the original <see
        /// cref="T:System.Collections.Generic.IDictionary{TKey,TValue}"/>.
        /// </returns>
        /// <exception cref="T:System.ArgumentNullException"><paramref name="key"/> is a null reference
        /// (Nothing in Visual Basic).</exception>
        bool IDictionary<TKey, TValue>.Remove(TKey key)
        {
            TValue throwAwayValue;
            return TryRemove(key, out throwAwayValue);
        }

        /// <summary>
        /// Gets a collection containing the keys in the <see
        /// cref="T:System.Collections.Generic.Dictionary{TKey,TValue}"/>.
        /// </summary>
        /// <value>An <see cref="T:System.Collections.Generic.ICollection{TKey}"/> containing the keys in the
        /// <see cref="T:System.Collections.Generic.Dictionary{TKey,TValue}"/>.</value>
        public ICollection<TKey> Keys
        {
            get { return GetKeys(); }
        }

        /// <summary>
        /// Gets an <see cref="T:System.Collections.Generic.IEnumerable{TKey}"/> containing the keys of
        /// the <see cref="T:System.Collections.Generic.IReadOnlyDictionary{TKey,TValue}"/>.
        /// </summary>
        /// <value>An <see cref="T:System.Collections.Generic.IEnumerable{TKey}"/> containing the keys of
        /// the <see cref="T:System.Collections.Generic.IReadOnlyDictionary{TKey,TValue}"/>.</value>
        IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys
        {
            get { return GetKeys(); }
        }

        /// <summary>
        /// Gets a collection containing the values in the <see
        /// cref="T:System.Collections.Generic.Dictionary{TKey,TValue}"/>.
        /// </summary>
        /// <value>An <see cref="T:System.Collections.Generic.ICollection{TValue}"/> containing the values in
        /// the
        /// <see cref="T:System.Collections.Generic.Dictionary{TKey,TValue}"/>.</value>
        public ICollection<TValue> Values
        {
            get { return GetValues(); }
        }

        /// <summary>
        /// Gets an <see cref="T:System.Collections.Generic.IEnumerable{TValue}"/> containing the values
        /// in the <see cref="T:System.Collections.Generic.IReadOnlyDictionary{TKey,TValue}"/>.
        /// </summary>
        /// <value>An <see cref="T:System.Collections.Generic.IEnumerable{TValue}"/> containing the
        /// values in the <see cref="T:System.Collections.Generic.IReadOnlyDictionary{TKey,TValue}"/>.</value>
        IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values
        {
            get { return GetValues(); }
        }
        #endregion

        #region ICollection<KeyValuePair<TKey,TValue>> Members

        /// <summary>
        /// Adds the specified value to the <see cref="T:System.Collections.Generic.ICollection{TValue}"/>
        /// with the specified key.
        /// </summary>
        /// <param name="keyValuePair">The <see cref="T:System.Collections.Generic.KeyValuePair{TKey,TValue}"/>
        /// structure representing the key and value to add to the <see
        /// cref="T:System.Collections.Generic.Dictionary{TKey,TValue}"/>.</param>
        /// <exception cref="T:System.ArgumentNullException">The <paramref name="keyValuePair"/> of <paramref
        /// name="keyValuePair"/> is null.</exception>
        /// <exception cref="T:System.OverflowException">The <see
        /// cref="T:System.Collections.Generic.Dictionary{TKey,TValue}"/>
        /// contains too many elements.</exception>
        /// <exception cref="T:System.ArgumentException">An element with the same key already exists in the
        /// <see cref="T:System.Collections.Generic.Dictionary{TKey,TValue}"/></exception>
        void ICollection<KeyValuePair<TKey, TValue>>.Add(KeyValuePair<TKey, TValue> keyValuePair)
        {
            ((IDictionary<TKey, TValue>)this).Add(keyValuePair.Key, keyValuePair.Value);
        }

        /// <summary>
        /// Determines whether the <see cref="T:System.Collections.Generic.ICollection{TKey,TValue}"/>
        /// contains a specific key and value.
        /// </summary>
        /// <param name="keyValuePair">The <see cref="T:System.Collections.Generic.KeyValuePair{TKey,TValue}"/>
        /// structure to locate in the <see
        /// cref="T:System.Collections.Generic.ICollection{TValue}"/>.</param>
        /// <returns>true if the <paramref name="keyValuePair"/> is found in the <see
        /// cref="T:System.Collections.Generic.ICollection{TKey,TValue}"/>; otherwise, false.</returns>
        bool ICollection<KeyValuePair<TKey, TValue>>.Contains(KeyValuePair<TKey, TValue> keyValuePair)
        {
            TValue value;
            if (!TryGetValue(keyValuePair.Key, out value))
            {
                return false;
            }
            return EqualityComparer<TValue>.Default.Equals(value, keyValuePair.Value);
        }

        /// <summary>
        /// Gets a value indicating whether the dictionary is read-only.
        /// </summary>
        /// <value>true if the <see cref="T:System.Collections.Generic.ICollection{TKey,TValue}"/> is
        /// read-only; otherwise, false. For <see
        /// cref="T:System.Collections.Generic.Dictionary{TKey,TValue}"/>, this property always returns
        /// false.</value>
        bool ICollection<KeyValuePair<TKey, TValue>>.IsReadOnly
        {
            get { return false; }
        }

        /// <summary>
        /// Removes a key and value from the dictionary.
        /// </summary>
        /// <param name="keyValuePair">The <see
        /// cref="T:System.Collections.Generic.KeyValuePair{TKey,TValue}"/>
        /// structure representing the key and value to remove from the <see
        /// cref="T:System.Collections.Generic.Dictionary{TKey,TValue}"/>.</param>
        /// <returns>true if the key and value represented by <paramref name="keyValuePair"/> is successfully
        /// found and removed; otherwise, false.</returns>
        /// <exception cref="T:System.ArgumentNullException">The Key property of <paramref
        /// name="keyValuePair"/> is a null reference (Nothing in Visual Basic).</exception>
        bool ICollection<KeyValuePair<TKey, TValue>>.Remove(KeyValuePair<TKey, TValue> keyValuePair)
        {
            if (keyValuePair.Key == null) throw new ArgumentNullException(GetResource("ConcurrentDictionary_ItemKeyIsNull"));

            TValue throwAwayValue;
            return TryRemoveInternal(keyValuePair.Key, out throwAwayValue, true, keyValuePair.Value);
        }

        #endregion

        #region IEnumerable Members

        /// <summary>Returns an enumerator that iterates through the <see
        /// cref="ConcurrentKeyedList{TKey,TValue}"/>.</summary>
        /// <returns>An enumerator for the <see cref="ConcurrentKeyedList{TKey,TValue}"/>.</returns>
        /// <remarks>
        /// The enumerator returned from the dictionary is safe to use concurrently with
        /// reads and writes to the dictionary, however it does not represent a moment-in-time snapshot
        /// of the dictionary.  The contents exposed through the enumerator may contain modifications
        /// made to the dictionary after <see cref="GetEnumerator"/> was called.
        /// </remarks>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((ConcurrentKeyedList<TKey, TValue>)this).GetEnumerator();
        }

        #endregion

        #region IDictionary Members

        /// <summary>
        /// Adds the specified key and value to the dictionary.
        /// </summary>
        /// <param name="key">The object to use as the key.</param>
        /// <param name="value">The object to use as the value.</param>
        /// <exception cref="T:System.ArgumentNullException"><paramref name="key"/> is a null reference
        /// (Nothing in Visual Basic).</exception>
        /// <exception cref="T:System.OverflowException">The dictionary contains too many
        /// elements.</exception>
        /// <exception cref="T:System.ArgumentException">
        /// <paramref name="key"/> is of a type that is not assignable to the key type <typeparamref
        /// name="TKey"/> of the <see cref="T:System.Collections.Generic.Dictionary{TKey,TValue}"/>. -or-
        /// <paramref name="value"/> is of a type that is not assignable to <typeparamref name="TValue"/>,
        /// the type of values in the <see cref="T:System.Collections.Generic.Dictionary{TKey,TValue}"/>.
        /// -or- A value with the same key already exists in the <see
        /// cref="T:System.Collections.Generic.Dictionary{TKey,TValue}"/>.
        /// </exception>
        void IDictionary.Add(object key, object value)
        {
            if (key == null) throw new ArgumentNullException("key");
            if (!(key is TKey)) throw new ArgumentException(GetResource("ConcurrentDictionary_TypeOfKeyIncorrect"));

            TValue typedValue;
            try
            {
                typedValue = (TValue)value;
            }
            catch (InvalidCastException)
            {
                throw new ArgumentException(GetResource("ConcurrentDictionary_TypeOfValueIncorrect"));
            }

            ((IDictionary<TKey, TValue>)this).Add((TKey)key, typedValue);
        }

        /// <summary>
        /// Gets whether the <see cref="T:System.Collections.Generic.IDictionary{TKey,TValue}"/> contains an
        /// element with the specified key.
        /// </summary>
        /// <param name="key">The key to locate in the <see
        /// cref="T:System.Collections.Generic.IDictionary{TKey,TValue}"/>.</param>
        /// <returns>true if the <see cref="T:System.Collections.Generic.IDictionary{TKey,TValue}"/> contains
        /// an element with the specified key; otherwise, false.</returns>
        /// <exception cref="T:System.ArgumentNullException"> <paramref name="key"/> is a null reference
        /// (Nothing in Visual Basic).</exception>
        bool IDictionary.Contains(object key)
        {
            if (key == null) throw new ArgumentNullException("key");

            return (key is TKey) && ((ConcurrentKeyedList<TKey, TValue>)this).ContainsKey((TKey)key);
        }

        /// <summary>Provides an <see cref="T:System.Collections.Generics.IDictionaryEnumerator"/> for the
        /// <see cref="T:System.Collections.Generic.IDictionary{TKey,TValue}"/>.</summary>
        /// <returns>An <see cref="T:System.Collections.Generics.IDictionaryEnumerator"/> for the <see
        /// cref="T:System.Collections.Generic.IDictionary{TKey,TValue}"/>.</returns>
        IDictionaryEnumerator IDictionary.GetEnumerator()
        {
            return new DictionaryEnumerator(this);
        }

        /// <summary>
        /// Gets a value indicating whether the <see
        /// cref="T:System.Collections.Generic.IDictionary{TKey,TValue}"/> has a fixed size.
        /// </summary>
        /// <value>true if the <see cref="T:System.Collections.Generic.IDictionary{TKey,TValue}"/> has a
        /// fixed size; otherwise, false. For <see
        /// cref="T:System.Collections.Generic.ConcurrentDictionary{TKey,TValue}"/>, this property always
        /// returns false.</value>
        bool IDictionary.IsFixedSize
        {
            get { return false; }
        }

        /// <summary>
        /// Gets a value indicating whether the <see
        /// cref="T:System.Collections.Generic.IDictionary{TKey,TValue}"/> is read-only.
        /// </summary>
        /// <value>true if the <see cref="T:System.Collections.Generic.IDictionary{TKey,TValue}"/> is
        /// read-only; otherwise, false. For <see
        /// cref="T:System.Collections.Generic.ConcurrentDictionary{TKey,TValue}"/>, this property always
        /// returns false.</value>
        bool IDictionary.IsReadOnly
        {
            get { return false; }
        }

        /// <summary>
        /// Gets an <see cref="T:System.Collections.ICollection"/> containing the keys of the <see
        /// cref="T:System.Collections.Generic.IDictionary{TKey,TValue}"/>.
        /// </summary>
        /// <value>An <see cref="T:System.Collections.ICollection"/> containing the keys of the <see
        /// cref="T:System.Collections.Generic.IDictionary{TKey,TValue}"/>.</value>
        ICollection IDictionary.Keys
        {
            get { return GetKeys(); }
        }

        /// <summary>
        /// Removes the element with the specified key from the <see
        /// cref="T:System.Collections.IDictionary"/>.
        /// </summary>
        /// <param name="key">The key of the element to remove.</param>
        /// <exception cref="T:System.ArgumentNullException"><paramref name="key"/> is a null reference
        /// (Nothing in Visual Basic).</exception>
        void IDictionary.Remove(object key)
        {
            if (key == null) throw new ArgumentNullException("key");

            TValue throwAwayValue;
            if (key is TKey)
            {
                this.TryRemove((TKey)key, out throwAwayValue);
            }
        }

        /// <summary>
        /// Gets an <see cref="T:System.Collections.ICollection"/> containing the values in the <see
        /// cref="T:System.Collections.IDictionary"/>.
        /// </summary>
        /// <value>An <see cref="T:System.Collections.ICollection"/> containing the values in the <see
        /// cref="T:System.Collections.IDictionary"/>.</value>
        ICollection IDictionary.Values
        {
            get { return GetValues(); }
        }

        /// <summary>
        /// Gets or sets the value associated with the specified key.
        /// </summary>
        /// <param name="key">The key of the value to get or set.</param>
        /// <value>The value associated with the specified key, or a null reference (Nothing in Visual Basic)
        /// if <paramref name="key"/> is not in the dictionary or <paramref name="key"/> is of a type that is
        /// not assignable to the key type <typeparamref name="TKey"/> of the <see
        /// cref="T:System.Collections.Generic.ConcurrentDictionary{TKey,TValue}"/>.</value>
        /// <exception cref="T:System.ArgumentNullException"><paramref name="key"/> is a null reference
        /// (Nothing in Visual Basic).</exception>
        /// <exception cref="T:System.ArgumentException">
        /// A value is being assigned, and <paramref name="key"/> is of a type that is not assignable to the
        /// key type <typeparamref name="TKey"/> of the <see
        /// cref="T:System.Collections.Generic.ConcurrentDictionary{TKey,TValue}"/>. -or- A value is being
        /// assigned, and <paramref name="key"/> is of a type that is not assignable to the value type
        /// <typeparamref name="TValue"/> of the <see
        /// cref="T:System.Collections.Generic.ConcurrentDictionary{TKey,TValue}"/>
        /// </exception>
        object IDictionary.this[object key]
        {
            get
            {
                if (key == null) throw new ArgumentNullException("key");

                TValue value;
                if (key is TKey && this.TryGetValue((TKey)key, out value))
                {
                    return value;
                }

                return null;
            }
            set
            {
                if (key == null) throw new ArgumentNullException("key");

                if (!(key is TKey)) throw new ArgumentException(GetResource("ConcurrentDictionary_TypeOfKeyIncorrect"));
                if (!(value is TValue)) throw new ArgumentException(GetResource("ConcurrentDictionary_TypeOfValueIncorrect"));

                ((ConcurrentKeyedList<TKey, TValue>)this)[(TKey)key] = (TValue)value;
            }
        }

        #endregion

        #region ICollection Members

        /// <summary>
        /// Copies the elements of the <see cref="T:System.Collections.ICollection"/> to an array, starting
        /// at the specified array index.
        /// </summary>
        /// <param name="array">The one-dimensional array that is the destination of the elements copied from
        /// the <see cref="T:System.Collections.ICollection"/>. The array must have zero-based
        /// indexing.</param>
        /// <param name="index">The zero-based index in <paramref name="array"/> at which copying
        /// begins.</param>
        /// <exception cref="T:System.ArgumentNullException"><paramref name="array"/> is a null reference
        /// (Nothing in Visual Basic).</exception>
        /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="index"/> is less than
        /// 0.</exception>
        /// <exception cref="T:System.ArgumentException"><paramref name="index"/> is equal to or greater than
        /// the length of the <paramref name="array"/>. -or- The number of elements in the source <see
        /// cref="T:System.Collections.ICollection"/>
        /// is greater than the available space from <paramref name="index"/> to the end of the destination
        /// <paramref name="array"/>.</exception>
        [SuppressMessage("Microsoft.Concurrency", "CA8001", Justification = "ConcurrencyCop just doesn't know about these locks")]
        void ICollection.CopyTo(Array array, int index)
        {
            if (array == null) throw new ArgumentNullException("array");
            if (index < 0) throw new ArgumentOutOfRangeException("index", GetResource("ConcurrentDictionary_IndexIsNegative"));

            int locksAcquired = 0;
            try
            {
                AcquireAllLocks(ref locksAcquired);
                Tables tables = m_tables;

                int count = 0;

                for (int i = 0; i < tables.m_locks.Length && count >= 0; i++)
                {
                    count += tables.m_countPerLock[i];
                }

                if (array.Length - count < index || count < 0) //"count" itself or "count + index" can overflow
                {
                    throw new ArgumentException(GetResource("ConcurrentDictionary_ArrayNotLargeEnough"));
                }

                // To be consistent with the behavior of ICollection.CopyTo() in Dictionary<TKey,TValue>,
                // we recognize three types of target arrays:
                //    - an array of KeyValuePair<TKey, TValue> structs
                //    - an array of DictionaryEntry structs
                //    - an array of objects

                KeyValuePair<TKey, TValue>[] pairs = array as KeyValuePair<TKey, TValue>[];
                if (pairs != null)
                {
                    CopyToPairs(pairs, index);
                    return;
                }

                DictionaryEntry[] entries = array as DictionaryEntry[];
                if (entries != null)
                {
                    CopyToEntries(entries, index);
                    return;
                }

                object[] objects = array as object[];
                if (objects != null)
                {
                    CopyToObjects(objects, index);
                    return;
                }

                throw new ArgumentException(GetResource("ConcurrentDictionary_ArrayIncorrectType"), "array");
            }
            finally
            {
                ReleaseLocks(0, locksAcquired);
            }
        }

        /// <summary>
        /// Gets a value indicating whether access to the <see cref="T:System.Collections.ICollection"/> is
        /// synchronized with the SyncRoot.
        /// </summary>
        /// <value>true if access to the <see cref="T:System.Collections.ICollection"/> is synchronized
        /// (thread safe); otherwise, false. For <see
        /// cref="T:System.Collections.Concurrent.ConcurrentDictionary{TKey,TValue}"/>, this property always
        /// returns false.</value>
        bool ICollection.IsSynchronized
        {
            get { return false; }
        }

        /// <summary>
        /// Gets an object that can be used to synchronize access to the <see
        /// cref="T:System.Collections.ICollection"/>. This property is not supported.
        /// </summary>
        /// <exception cref="T:System.NotSupportedException">The SyncRoot property is not supported.</exception>
        object ICollection.SyncRoot
        {
            get
            {
                throw new NotSupportedException(ResourceManager.GetResourceString("ConcurrentCollection_SyncRoot_NotSupported"));
            }
        }

        #endregion

        /// <summary>
        /// Replaces the bucket table with a larger one. To prevent multiple threads from resizing the
        /// table as a result of ----s, the Tables instance that holds the table of buckets deemed too
        /// small is passed in as an argument to GrowTable(). GrowTable() obtains a lock, and then checks
        /// the Tables instance has been replaced in the meantime or not. 
        /// The <paramref name="rehashCount"/> will be used to ensure that we don't do two subsequent resizes
        /// because of a collision
        /// </summary>
        private void GrowTable(Tables tables, IEqualityComparer<TKey> newComparer, bool regenerateHashKeys, int rehashCount)
        {
            int locksAcquired = 0;
            try
            {
                // The thread that first obtains m_locks[0] will be the one doing the resize operation
                AcquireLocks(0, 1, ref locksAcquired);

                if (regenerateHashKeys && rehashCount == m_keyRehashCount)
                {
                    // This method is called with regenerateHashKeys==true when we detected 
                    // more than HashHelpers.HashCollisionThreshold collisions when adding a new element.
                    // In that case we are in the process of switching to another (randomized) comparer
                    // and we have to re-hash all the keys in the table.
                    // We are only going to do this if we did not just rehash the entire table while waiting for the lock
                    tables = m_tables;
                }
                else
                {
                    // If we don't require a regeneration of hash keys we want to make sure we don't do work when
                    // we don't have to
                    if (tables != m_tables)
                    {
                        // We assume that since the table reference is different, it was already resized (or the budget
                        // was adjusted). If we ever decide to do table shrinking, or replace the table for other reasons,
                        // we will have to revisit this logic.
                        return;
                    }

                    // Compute the (approx.) total size. Use an Int64 accumulation variable to avoid an overflow.
                    long approxCount = 0;
                    for (int i = 0; i < tables.m_countPerLock.Length; i++)
                    {
                        approxCount += tables.m_countPerLock[i];
                    }

                    //
                    // If the bucket array is too empty, double the budget instead of resizing the table
                    //
                    if (approxCount < tables.m_buckets.Length / 4)
                    {
                        m_budget = 2 * m_budget;
                        if (m_budget < 0)
                        {
                            m_budget = int.MaxValue;
                        }

                        return;
                    }
                }
                // Compute the new table size. We find the smallest integer larger than twice the previous table size, and not divisible by
                // 2,3,5 or 7. We can consider a different table-sizing policy in the future.
                int newLength = 0;
                bool maximizeTableSize = false;
                try
                {
                    checked
                    {
                        // Double the size of the buckets table and add one, so that we have an odd integer.
                        newLength = tables.m_buckets.Length * 2 + 1;

                        // Now, we only need to check odd integers, and find the first that is not divisible
                        // by 3, 5 or 7.
                        while (newLength % 3 == 0 || newLength % 5 == 0 || newLength % 7 == 0)
                        {
                            newLength += 2;
                        }

                        Assert(newLength % 2 != 0);

                        if (newLength > 0X7FEFFFFF)
                        {
                            maximizeTableSize = true;
                        }
                    }
                }
                catch (OverflowException)
                {
                    maximizeTableSize = true;
                }

                if (maximizeTableSize)
                {
                    newLength = 0X7FEFFFFF;

                    // We want to make sure that GrowTable will not be called again, since table is at the maximum size.
                    // To achieve that, we set the budget to int.MaxValue.
                    //
                    // (There is one special case that would allow GrowTable() to be called in the future: 
                    // calling Clear() on the ConcurrentDictionary will shrink the table and lower the budget.)
                    m_budget = int.MaxValue;
                }

                // Now acquire all other locks for the table
                AcquireLocks(1, tables.m_locks.Length, ref locksAcquired);

                object[] newLocks = tables.m_locks;

                // Add more locks
                if (m_growLockArray && tables.m_locks.Length < MAX_LOCK_NUMBER)
                {
                    newLocks = new object[tables.m_locks.Length * 2];
                    Array.Copy(tables.m_locks, newLocks, tables.m_locks.Length);

                    for (int i = tables.m_locks.Length; i < newLocks.Length; i++)
                    {
                        newLocks[i] = new object();
                    }
                }

                Node[] newBuckets = new Node[newLength];
                int[] newCountPerLock = new int[newLocks.Length];

                // Copy all data into a new table, creating new nodes for all elements
                for (int i = 0; i < tables.m_buckets.Length; i++)
                {
                    Node current = tables.m_buckets[i];
                    while (current != null)
                    {
                        Node next = current.m_next;
                        int newBucketNo, newLockNo;
                        int nodeHashCode = current.m_hashcode;

                        if (regenerateHashKeys)
                        {
                            // Recompute the hash from the key
                            nodeHashCode = newComparer.GetHashCode(current.m_key);
                        }

                        GetBucketAndLockNo(nodeHashCode, out newBucketNo, out newLockNo, newBuckets.Length, newLocks.Length);

                        newBuckets[newBucketNo] = new Node(current.m_key, current.m_value, nodeHashCode, newBuckets[newBucketNo]);

                        checked
                        {
                            newCountPerLock[newLockNo]++;
                        }

                        current = next;
                    }
                }

                // If this resize regenerated the hashkeys, increment the count
                if (regenerateHashKeys)
                {
                    // We use unchecked here because we don't want to throw an exception if 
                    // an overflow happens
                    unchecked
                    {
                        m_keyRehashCount++;
                    }
                }

                // Adjust the budget
                m_budget = Math.Max(1, newBuckets.Length / newLocks.Length);

                // Replace tables with the new versions
                m_tables = new Tables(newBuckets, newLocks, newCountPerLock, newComparer);
            }
            finally
            {
                // Release all locks that we took earlier
                ReleaseLocks(0, locksAcquired);
            }
        }

        /// <summary>
        /// Computes the bucket and lock number for a particular key. 
        /// </summary>
        private void GetBucketAndLockNo(
                int hashcode, out int bucketNo, out int lockNo, int bucketCount, int lockCount)
        {
            bucketNo = (hashcode & 0x7fffffff) % bucketCount;
            lockNo = bucketNo % lockCount;

            Assert(bucketNo >= 0 && bucketNo < bucketCount);
            Assert(lockNo >= 0 && lockNo < lockCount);
        }

        /// <summary>
        /// The number of concurrent writes for which to optimize by default.
        /// </summary>
        private static int DefaultConcurrencyLevel
        {

            get { return PlatformHelper.ProcessorCount; }
        }

        /// <summary>
        /// Acquires all locks for this hash table, and increments locksAcquired by the number
        /// of locks that were successfully acquired. The locks are acquired in an increasing
        /// order.
        /// </summary>
        private void AcquireAllLocks(ref int locksAcquired)
        {
#if !FEATURE_PAL && !FEATURE_CORECLR    // PAL and CoreClr don't support  eventing
            if (CDSCollectionETWBCLProvider.Log.IsEnabled())
            {
                CDSCollectionETWBCLProvider.Log.ConcurrentDictionary_AcquiringAllLocks(m_tables.m_buckets.Length);
            }
#endif //!FEATURE_PAL && !FEATURE_CORECLR

            // First, acquire lock 0
            AcquireLocks(0, 1, ref locksAcquired);

            // Now that we have lock 0, the m_locks array will not change (i.e., grow),
            // and so we can safely read m_locks.Length.
            AcquireLocks(1, m_tables.m_locks.Length, ref locksAcquired);
            Assert(locksAcquired == m_tables.m_locks.Length);
        }

        /// <summary>
        /// Acquires a contiguous range of locks for this hash table, and increments locksAcquired
        /// by the number of locks that were successfully acquired. The locks are acquired in an
        /// increasing order.
        /// </summary>
        private void AcquireLocks(int fromInclusive, int toExclusive, ref int locksAcquired)
        {
            Assert(fromInclusive <= toExclusive);
            object[] locks = m_tables.m_locks;

            for (int i = fromInclusive; i < toExclusive; i++)
            {
                bool lockTaken = false;
                try
                {
#if CDS_COMPILE_JUST_THIS
                    Monitor.Enter(m_tables.m_locks[i]);
                    lockTaken = true;
#else
                    Monitor.Enter(locks[i], ref lockTaken);
#endif
                }
                finally
                {
                    if (lockTaken)
                    {
                        locksAcquired++;
                    }
                }
            }
        }

        /// <summary>
        /// Releases a contiguous range of locks.
        /// </summary>
        [SuppressMessage("Microsoft.Concurrency", "CA8001", Justification = "Reviewed for thread safety")]
        private void ReleaseLocks(int fromInclusive, int toExclusive)
        {
            Assert(fromInclusive <= toExclusive);

            for (int i = fromInclusive; i < toExclusive; i++)
            {
                Monitor.Exit(m_tables.m_locks[i]);
            }
        }

        /// <summary>
        /// Gets a collection containing the keys in the dictionary.
        /// </summary>
        [SuppressMessage("Microsoft.Concurrency", "CA8001", Justification = "ConcurrencyCop just doesn't know about these locks")]
        private ReadOnlyCollection<TKey> GetKeys()
        {
            int locksAcquired = 0;
            try
            {
                AcquireAllLocks(ref locksAcquired);

                int count = GetCountInternal();
                if (count < 0) throw new OutOfMemoryException();

                List<TKey> keys = new List<TKey>(count);
                for (int i = 0; i < m_tables.m_buckets.Length; i++)
                {
                    Node current = m_tables.m_buckets[i];
                    while (current != null)
                    {
                        keys.Add(current.m_key);
                        current = current.m_next;
                    }
                }

                return new ReadOnlyCollection<TKey>(keys);
            }
            finally
            {
                ReleaseLocks(0, locksAcquired);
            }
        }

        /// <summary>
        /// Gets a collection containing the values in the dictionary.
        /// </summary>
        [SuppressMessage("Microsoft.Concurrency", "CA8001", Justification = "ConcurrencyCop just doesn't know about these locks")]
        private ReadOnlyCollection<TValue> GetValues()
        {
            int locksAcquired = 0;
            try
            {
                AcquireAllLocks(ref locksAcquired);

                int count = GetCountInternal();
                if (count < 0) throw new OutOfMemoryException();

                List<TValue> values = new List<TValue>(count);

                for (int i = 0; i < m_tables.m_buckets.Length; i++)
                {
                    Node current = m_tables.m_buckets[i];
                    while (current != null)
                    {
                        values.Add(current.m_value);
                        current = current.m_next;
                    }
                }

                return new ReadOnlyCollection<TValue>(values);
            }
            finally
            {
                ReleaseLocks(0, locksAcquired);
            }
        }

        /// <summary>
        /// A helper method for asserts.
        /// </summary>
        [Conditional("DEBUG")]
        private void Assert(bool condition)
        {
#if CDS_COMPILE_JUST_THIS
            if (!condition)
            {
                throw new Exception("Assertion failed.");
            }
#else
            Contract.Assert(condition);
#endif
        }

        /// <summary>
        /// A helper function to obtain the string for a particular resource key.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        private string GetResource(string key)
        {
            Assert(key != null);

#if CDS_COMPILE_JUST_THIS
            return key;
#else
            return ResourceManager.GetResourceString(key);
#endif
        }

        /// <summary>
        /// A node in a singly-linked list representing a particular hash table bucket.
        /// </summary>
        private class Node
        {
            internal TKey m_key;
            internal TValue m_value;
            internal volatile Node m_next;
            internal int m_hashcode;

            internal Node(TKey key, TValue value, int hashcode, Node next)
            {
                m_key = key;
                m_value = value;
                m_next = next;
                m_hashcode = hashcode;
            }
        }

        /// <summary>
        /// A private class to represent enumeration over the dictionary that implements the 
        /// IDictionaryEnumerator interface.
        /// </summary>
        private class DictionaryEnumerator : IDictionaryEnumerator
        {
            IEnumerator<KeyValuePair<TKey, TValue>> m_enumerator; // Enumerator over the dictionary.

            internal DictionaryEnumerator(ConcurrentKeyedList<TKey, TValue> dictionary)
            {
                m_enumerator = dictionary.GetEnumerator();
            }

            public DictionaryEntry Entry
            {
                get { return new DictionaryEntry(m_enumerator.Current.Key, m_enumerator.Current.Value); }
            }

            public object Key
            {
                get { return m_enumerator.Current.Key; }
            }

            public object Value
            {
                get { return m_enumerator.Current.Value; }
            }

            public object Current
            {
                get { return this.Entry; }
            }

            public bool MoveNext()
            {
                return m_enumerator.MoveNext();
            }

            public void Reset()
            {
                m_enumerator.Reset();
            }
        }

#if !FEATURE_CORECLR
        /// <summary>
        /// Get the data array to be serialized
        /// </summary>
        //[OnSerializing]
        //private void OnSerializing(StreamingContext context)
        //{
        //    Tables tables = m_tables;

        //    // save the data into the serialization array to be saved
        //    m_serializationArray = ToArray();
        //    m_serializationConcurrencyLevel = tables.m_locks.Length;
        //    m_serializationCapacity = tables.m_buckets.Length;
        //    m_comparer = (IEqualityComparer<TKey>)HashHelpers.GetEqualityComparerForSerialization(tables.m_comparer);
        //}

        /// <summary>
        /// Construct the dictionary from a previously serialized one
        /// </summary>
        //[OnDeserialized]
        //private void OnDeserialized(StreamingContext context)
        //{
        //    KeyValuePair<TKey, TValue>[] array = m_serializationArray;

        //    var buckets = new Node[m_serializationCapacity];
        //    var countPerLock = new int[m_serializationConcurrencyLevel];

        //    var locks = new object[m_serializationConcurrencyLevel];
        //    for (int i = 0; i < locks.Length; i++)
        //    {
        //        locks[i] = new object();
        //    }

        //    m_tables = new Tables(buckets, locks, countPerLock, m_comparer);

        //    InitializeFromCollection(array);
        //    m_serializationArray = null;
        //}
#endif
    }




    internal static class HashHelpers
    {

#if FEATURE_RANDOMIZED_STRING_HASHING
        public const int HashCollisionThreshold = 100;
        public static bool s_UseRandomizedStringHashing = String.UseRandomizedHashing();
#endif

        // Table of prime numbers to use as hash table sizes. 
        // A typical resize algorithm would pick the smallest prime number in this array
        // that is larger than twice the previous capacity. 
        // Suppose our Hashtable currently has capacity x and enough elements are added 
        // such that a resize needs to occur. Resizing first computes 2x then finds the 
        // first prime in the table greater than 2x, i.e. if primes are ordered 
        // p_1, p_2, ..., p_i, ..., it finds p_n such that p_n-1 < 2x < p_n. 
        // Doubling is important for preserving the asymptotic complexity of the 
        // hashtable operations such as add.  Having a prime guarantees that double 
        // hashing does not lead to infinite loops.  IE, your hash function will be 
        // h1(key) + i*h2(key), 0 <= i < size.  h2 and the size must be relatively prime.
        public static readonly int[] primes = {
            3, 7, 11, 17, 23, 29, 37, 47, 59, 71, 89, 107, 131, 163, 197, 239, 293, 353, 431, 521, 631, 761, 919,
            1103, 1327, 1597, 1931, 2333, 2801, 3371, 4049, 4861, 5839, 7013, 8419, 10103, 12143, 14591,
            17519, 21023, 25229, 30293, 36353, 43627, 52361, 62851, 75431, 90523, 108631, 130363, 156437,
            187751, 225307, 270371, 324449, 389357, 467237, 560689, 672827, 807403, 968897, 1162687, 1395263,
            1674319, 2009191, 2411033, 2893249, 3471899, 4166287, 4999559, 5999471, 7199369};

        // Used by Hashtable and Dictionary's SeralizationInfo .ctor's to store the SeralizationInfo
        // object until OnDeserialization is called.
        private static ConditionalWeakTable<object, SerializationInfo> s_SerializationInfoTable;

        internal static ConditionalWeakTable<object, SerializationInfo> SerializationInfoTable
        {
            get
            {
                if (s_SerializationInfoTable == null)
                {
                    ConditionalWeakTable<object, SerializationInfo> newTable = new ConditionalWeakTable<object, SerializationInfo>();
                    Interlocked.CompareExchange(ref s_SerializationInfoTable, newTable, null);
                }

                return s_SerializationInfoTable;
            }

        }

        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        public static bool IsPrime(int candidate)
        {
            if ((candidate & 1) != 0)
            {
                int limit = (int)Math.Sqrt(candidate);
                for (int divisor = 3; divisor <= limit; divisor += 2)
                {
                    if ((candidate % divisor) == 0)
                        return false;
                }
                return true;
            }
            return (candidate == 2);
        }

        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        public static int GetPrime(int min)
        {
            if (min < 0)
                throw new ArgumentException(ResourceManager.GetResourceString("Arg_HTCapacityOverflow"));
            Contract.EndContractBlock();

            for (int i = 0; i < primes.Length; i++)
            {
                int prime = primes[i];
                if (prime >= min) return prime;
            }

            //outside of our predefined table. 
            //compute the hard way. 
            for (int i = (min | 1); i < Int32.MaxValue; i += 2)
            {
                if (IsPrime(i) && ((i - 1) % Hashtable_HashPrime != 0))
                    return i;
            }
            return min;
        }
        internal const Int32 Hashtable_HashPrime = 101;
        public static int GetMinPrime()
        {
            return primes[0];
        }

        // Returns size of hashtable to grow to.
        public static int ExpandPrime(int oldSize)
        {
            int newSize = 2 * oldSize;

            // Allow the hashtables to grow to maximum possible size (~2G elements) before encoutering capacity overflow.
            // Note that this check works even when _items.Length overflowed thanks to the (uint) cast
            if ((uint)newSize > MaxPrimeArrayLength && MaxPrimeArrayLength > oldSize)
            {
                Contract.Assert(MaxPrimeArrayLength == GetPrime(MaxPrimeArrayLength), "Invalid MaxPrimeArrayLength");
                return MaxPrimeArrayLength;
            }

            return GetPrime(newSize);
        }

        // This is the maximum prime smaller than Array.MaxArrayLength
        public const int MaxPrimeArrayLength = 0x7FEFFFFD;

#if FEATURE_RANDOMIZED_STRING_HASHING
        public static bool IsWellKnownEqualityComparer(object comparer)
        {
            return (comparer == null || comparer == System.Collections.Generic.EqualityComparer<string>.Default || comparer is IWellKnownStringEqualityComparer); 
        }
 
        public static IEqualityComparer GetRandomizedEqualityComparer(object comparer)
        {
            Contract.Assert(comparer == null || comparer == System.Collections.Generic.EqualityComparer<string>.Default || comparer is IWellKnownStringEqualityComparer); 
 
            if(comparer == null) {
                return new System.Collections.Generic.RandomizedObjectEqualityComparer();
            } 
 
            if(comparer == System.Collections.Generic.EqualityComparer<string>.Default) {
                return new System.Collections.Generic.RandomizedStringEqualityComparer();
            }
 
            IWellKnownStringEqualityComparer cmp = comparer as IWellKnownStringEqualityComparer;
 
            if(cmp != null) {
                return cmp.GetRandomizedEqualityComparer();
            }
 
            Contract.Assert(false, "Missing case in GetRandomizedEqualityComparer!");
 
            return null;
        }
 
        public static object GetEqualityComparerForSerialization(object comparer)
        {
            if(comparer == null)
            {
                return null;
            }
 
            IWellKnownStringEqualityComparer cmp = comparer as IWellKnownStringEqualityComparer;
 
            if(cmp != null)
            {
                return cmp.GetEqualityComparerForSerialization();
            }
 
            return comparer;
        }
 
        private const int bufferSize = 1024;
        private static RandomNumberGenerator rng;
        private static byte[] data;
        private static int currentIndex = bufferSize;
        private static readonly object lockObj = new Object();
 
        internal static long GetEntropy() 
        {
            lock(lockObj) {
                long ret;
 
                if(currentIndex == bufferSize) 
                {
                    if(null == rng)
                    {
                        rng = RandomNumberGenerator.Create();
                        data = new byte[bufferSize];
                        Contract.Assert(bufferSize % 8 == 0, "We increment our current index by 8, so our buffer size must be a multiple of 8");
                    }
 
                    rng.GetBytes(data);
                    currentIndex = 0;
                }
 
                ret = BitConverter.ToInt64(data, currentIndex);
                currentIndex += 8;
 
                return ret;
            }
        }
#endif // FEATURE_RANDOMIZED_STRING_HASHING
    }


    internal static class ResourceManager
    {
        public static string GetResourceString(string keyName)
        {
            return string.Empty;
        }
        public static string GetResourceString(string keyName, object key, Type type)
        {
            return string.Empty;
        }
    }


    [Pure]
    internal static class ThrowHelper
    {
        internal static void ThrowArgumentOutOfRangeException()
        {
            ThrowArgumentOutOfRangeException(ExceptionArgument.index, ExceptionResource.ArgumentOutOfRange_Index);
        }

        internal static void ThrowWrongKeyTypeArgumentException(object key, Type targetType)
        {
            throw new ArgumentException(ResourceManager.GetResourceString("Arg_WrongType", key, targetType), "key");
        }

        internal static void ThrowWrongValueTypeArgumentException(object value, Type targetType)
        {
            throw new ArgumentException(ResourceManager.GetResourceString("Arg_WrongType", value, targetType), "value");
        }

        internal static void ThrowKeyNotFoundException()
        {
            throw new System.Collections.Generic.KeyNotFoundException();
        }

        internal static void ThrowArgumentException(ExceptionResource resource)
        {
            throw new ArgumentException(ResourceManager.GetResourceString(GetResourceName(resource)));
        }

        internal static void ThrowArgumentException(ExceptionResource resource, ExceptionArgument argument)
        {
            throw new ArgumentException(ResourceManager.GetResourceString(GetResourceName(resource)), GetArgumentName(argument));
        }

        internal static void ThrowArgumentNullException(ExceptionArgument argument)
        {
            throw new ArgumentNullException(GetArgumentName(argument));
        }

        internal static void ThrowArgumentOutOfRangeException(ExceptionArgument argument)
        {
            throw new ArgumentOutOfRangeException(GetArgumentName(argument));
        }

        internal static void ThrowArgumentOutOfRangeException(ExceptionArgument argument, ExceptionResource resource)
        {

            if (CompatibilitySwitches.IsAppEarlierThanWindowsPhone8)
            {
                // Dev11 474369 quirk: Mango had an empty message string:
                throw new ArgumentOutOfRangeException(GetArgumentName(argument), String.Empty);
            }
            else
            {
                throw new ArgumentOutOfRangeException(GetArgumentName(argument),
                                                      ResourceManager.GetResourceString(GetResourceName(resource)));
            }
        }

        internal static void ThrowInvalidOperationException(ExceptionResource resource)
        {
            throw new InvalidOperationException(ResourceManager.GetResourceString(GetResourceName(resource)));
        }

        internal static void ThrowSerializationException(ExceptionResource resource)
        {
            throw new SerializationException(ResourceManager.GetResourceString(GetResourceName(resource)));
        }

        internal static void ThrowSecurityException(ExceptionResource resource)
        {
            throw new System.Security.SecurityException(ResourceManager.GetResourceString(GetResourceName(resource)));
        }

        internal static void ThrowNotSupportedException(ExceptionResource resource)
        {
            throw new NotSupportedException(ResourceManager.GetResourceString(GetResourceName(resource)));
        }

        internal static void ThrowUnauthorizedAccessException(ExceptionResource resource)
        {
            throw new UnauthorizedAccessException(ResourceManager.GetResourceString(GetResourceName(resource)));
        }

        internal static void ThrowObjectDisposedException(string objectName, ExceptionResource resource)
        {
            throw new ObjectDisposedException(objectName, GetResourceName(resource));
        }

        // Allow nulls for reference types and Nullable<U>, but not for value types.
        internal static void IfNullAndNullsAreIllegalThenThrow<T>(object value, ExceptionArgument argName)
        {
            // Note that default(T) is not equal to null for value types except when T is Nullable<U>. 
            if (value == null && !(default(T) == null))
                ThrowHelper.ThrowArgumentNullException(argName);
        }

        //
        // This function will convert an ExceptionArgument enum value to the argument name string.
        //
        internal static string GetArgumentName(ExceptionArgument argument)
        {
            string argumentName = null;

            switch (argument)
            {
                case ExceptionArgument.array:
                    argumentName = "array";
                    break;

                case ExceptionArgument.arrayIndex:
                    argumentName = "arrayIndex";
                    break;

                case ExceptionArgument.capacity:
                    argumentName = "capacity";
                    break;

                case ExceptionArgument.collection:
                    argumentName = "collection";
                    break;

                case ExceptionArgument.list:
                    argumentName = "list";
                    break;

                case ExceptionArgument.converter:
                    argumentName = "converter";
                    break;

                case ExceptionArgument.count:
                    argumentName = "count";
                    break;

                case ExceptionArgument.dictionary:
                    argumentName = "dictionary";
                    break;

                case ExceptionArgument.dictionaryCreationThreshold:
                    argumentName = "dictionaryCreationThreshold";
                    break;

                case ExceptionArgument.index:
                    argumentName = "index";
                    break;

                case ExceptionArgument.info:
                    argumentName = "info";
                    break;

                case ExceptionArgument.key:
                    argumentName = "key";
                    break;

                case ExceptionArgument.match:
                    argumentName = "match";
                    break;

                case ExceptionArgument.obj:
                    argumentName = "obj";
                    break;

                case ExceptionArgument.queue:
                    argumentName = "queue";
                    break;

                case ExceptionArgument.stack:
                    argumentName = "stack";
                    break;

                case ExceptionArgument.startIndex:
                    argumentName = "startIndex";
                    break;

                case ExceptionArgument.value:
                    argumentName = "value";
                    break;

                case ExceptionArgument.name:
                    argumentName = "name";
                    break;

                case ExceptionArgument.mode:
                    argumentName = "mode";
                    break;

                case ExceptionArgument.item:
                    argumentName = "item";
                    break;

                case ExceptionArgument.options:
                    argumentName = "options";
                    break;

                case ExceptionArgument.view:
                    argumentName = "view";
                    break;

                case ExceptionArgument.sourceBytesToCopy:
                    argumentName = "sourceBytesToCopy";
                    break;

                default:
                    Contract.Assert(false, "The enum value is not defined, please checked ExceptionArgumentName Enum.");
                    return string.Empty;
            }

            return argumentName;
        }

        //
        // This function will convert an ExceptionResource enum value to the resource string.
        //
        internal static string GetResourceName(ExceptionResource resource)
        {
            string resourceName = null;

            switch (resource)
            {
                case ExceptionResource.Argument_ImplementIComparable:
                    resourceName = "Argument_ImplementIComparable";
                    break;

                case ExceptionResource.Argument_AddingDuplicate:
                    resourceName = "Argument_AddingDuplicate";
                    break;

                case ExceptionResource.ArgumentOutOfRange_BiggerThanCollection:
                    resourceName = "ArgumentOutOfRange_BiggerThanCollection";
                    break;

                case ExceptionResource.ArgumentOutOfRange_Count:
                    resourceName = "ArgumentOutOfRange_Count";
                    break;

                case ExceptionResource.ArgumentOutOfRange_Index:
                    resourceName = "ArgumentOutOfRange_Index";
                    break;

                case ExceptionResource.ArgumentOutOfRange_InvalidThreshold:
                    resourceName = "ArgumentOutOfRange_InvalidThreshold";
                    break;

                case ExceptionResource.ArgumentOutOfRange_ListInsert:
                    resourceName = "ArgumentOutOfRange_ListInsert";
                    break;

                case ExceptionResource.ArgumentOutOfRange_NeedNonNegNum:
                    resourceName = "ArgumentOutOfRange_NeedNonNegNum";
                    break;

                case ExceptionResource.ArgumentOutOfRange_SmallCapacity:
                    resourceName = "ArgumentOutOfRange_SmallCapacity";
                    break;

                case ExceptionResource.Arg_ArrayPlusOffTooSmall:
                    resourceName = "Arg_ArrayPlusOffTooSmall";
                    break;

                case ExceptionResource.Arg_RankMultiDimNotSupported:
                    resourceName = "Arg_RankMultiDimNotSupported";
                    break;

                case ExceptionResource.Arg_NonZeroLowerBound:
                    resourceName = "Arg_NonZeroLowerBound";
                    break;

                case ExceptionResource.Argument_InvalidArrayType:
                    resourceName = "Argument_InvalidArrayType";
                    break;

                case ExceptionResource.Argument_InvalidOffLen:
                    resourceName = "Argument_InvalidOffLen";
                    break;

                case ExceptionResource.Argument_ItemNotExist:
                    resourceName = "Argument_ItemNotExist";
                    break;

                case ExceptionResource.InvalidOperation_CannotRemoveFromStackOrQueue:
                    resourceName = "InvalidOperation_CannotRemoveFromStackOrQueue";
                    break;

                case ExceptionResource.InvalidOperation_EmptyQueue:
                    resourceName = "InvalidOperation_EmptyQueue";
                    break;

                case ExceptionResource.InvalidOperation_EnumOpCantHappen:
                    resourceName = "InvalidOperation_EnumOpCantHappen";
                    break;

                case ExceptionResource.InvalidOperation_EnumFailedVersion:
                    resourceName = "InvalidOperation_EnumFailedVersion";
                    break;

                case ExceptionResource.InvalidOperation_EmptyStack:
                    resourceName = "InvalidOperation_EmptyStack";
                    break;

                case ExceptionResource.InvalidOperation_EnumNotStarted:
                    resourceName = "InvalidOperation_EnumNotStarted";
                    break;

                case ExceptionResource.InvalidOperation_EnumEnded:
                    resourceName = "InvalidOperation_EnumEnded";
                    break;

                case ExceptionResource.NotSupported_KeyCollectionSet:
                    resourceName = "NotSupported_KeyCollectionSet";
                    break;

                case ExceptionResource.NotSupported_ReadOnlyCollection:
                    resourceName = "NotSupported_ReadOnlyCollection";
                    break;

                case ExceptionResource.NotSupported_ValueCollectionSet:
                    resourceName = "NotSupported_ValueCollectionSet";
                    break;


                case ExceptionResource.NotSupported_SortedListNestedWrite:
                    resourceName = "NotSupported_SortedListNestedWrite";
                    break;


                case ExceptionResource.Serialization_InvalidOnDeser:
                    resourceName = "Serialization_InvalidOnDeser";
                    break;

                case ExceptionResource.Serialization_MissingKeys:
                    resourceName = "Serialization_MissingKeys";
                    break;

                case ExceptionResource.Serialization_NullKey:
                    resourceName = "Serialization_NullKey";
                    break;

                case ExceptionResource.Argument_InvalidType:
                    resourceName = "Argument_InvalidType";
                    break;

                case ExceptionResource.Argument_InvalidArgumentForComparison:
                    resourceName = "Argument_InvalidArgumentForComparison";
                    break;

                case ExceptionResource.InvalidOperation_NoValue:
                    resourceName = "InvalidOperation_NoValue";
                    break;

                case ExceptionResource.InvalidOperation_RegRemoveSubKey:
                    resourceName = "InvalidOperation_RegRemoveSubKey";
                    break;

                case ExceptionResource.Arg_RegSubKeyAbsent:
                    resourceName = "Arg_RegSubKeyAbsent";
                    break;

                case ExceptionResource.Arg_RegSubKeyValueAbsent:
                    resourceName = "Arg_RegSubKeyValueAbsent";
                    break;

                case ExceptionResource.Arg_RegKeyDelHive:
                    resourceName = "Arg_RegKeyDelHive";
                    break;

                case ExceptionResource.Security_RegistryPermission:
                    resourceName = "Security_RegistryPermission";
                    break;

                case ExceptionResource.Arg_RegSetStrArrNull:
                    resourceName = "Arg_RegSetStrArrNull";
                    break;

                case ExceptionResource.Arg_RegSetMismatchedKind:
                    resourceName = "Arg_RegSetMismatchedKind";
                    break;

                case ExceptionResource.UnauthorizedAccess_RegistryNoWrite:
                    resourceName = "UnauthorizedAccess_RegistryNoWrite";
                    break;

                case ExceptionResource.ObjectDisposed_RegKeyClosed:
                    resourceName = "ObjectDisposed_RegKeyClosed";
                    break;

                case ExceptionResource.Arg_RegKeyStrLenBug:
                    resourceName = "Arg_RegKeyStrLenBug";
                    break;

                case ExceptionResource.Argument_InvalidRegistryKeyPermissionCheck:
                    resourceName = "Argument_InvalidRegistryKeyPermissionCheck";
                    break;

                case ExceptionResource.NotSupported_InComparableType:
                    resourceName = "NotSupported_InComparableType";
                    break;

                case ExceptionResource.Argument_InvalidRegistryOptionsCheck:
                    resourceName = "Argument_InvalidRegistryOptionsCheck";
                    break;

                case ExceptionResource.Argument_InvalidRegistryViewCheck:
                    resourceName = "Argument_InvalidRegistryViewCheck";
                    break;

                default:
                    Contract.Assert(false, "The enum value is not defined, please checked ExceptionArgumentName Enum.");
                    return string.Empty;
            }

            return resourceName;
        }

    }

    //
    // The convention for this enum is using the argument name as the enum name
    // 
    internal enum ExceptionArgument
    {
        obj,
        dictionary,
        dictionaryCreationThreshold,
        array,
        info,
        key,
        collection,
        list,
        match,
        converter,
        queue,
        stack,
        capacity,
        index,
        startIndex,
        value,
        count,
        arrayIndex,
        name,
        mode,
        item,
        options,
        view,
        sourceBytesToCopy,
    }

    //
    // The convention for this enum is using the resource name as the enum name
    // 
    internal enum ExceptionResource
    {
        Argument_ImplementIComparable,
        Argument_InvalidType,
        Argument_InvalidArgumentForComparison,
        Argument_InvalidRegistryKeyPermissionCheck,
        ArgumentOutOfRange_NeedNonNegNum,

        Arg_ArrayPlusOffTooSmall,
        Arg_NonZeroLowerBound,
        Arg_RankMultiDimNotSupported,
        Arg_RegKeyDelHive,
        Arg_RegKeyStrLenBug,
        Arg_RegSetStrArrNull,
        Arg_RegSetMismatchedKind,
        Arg_RegSubKeyAbsent,
        Arg_RegSubKeyValueAbsent,

        Argument_AddingDuplicate,
        Serialization_InvalidOnDeser,
        Serialization_MissingKeys,
        Serialization_NullKey,
        Argument_InvalidArrayType,
        NotSupported_KeyCollectionSet,
        NotSupported_ValueCollectionSet,
        ArgumentOutOfRange_SmallCapacity,
        ArgumentOutOfRange_Index,
        Argument_InvalidOffLen,
        Argument_ItemNotExist,
        ArgumentOutOfRange_Count,
        ArgumentOutOfRange_InvalidThreshold,
        ArgumentOutOfRange_ListInsert,
        NotSupported_ReadOnlyCollection,
        InvalidOperation_CannotRemoveFromStackOrQueue,
        InvalidOperation_EmptyQueue,
        InvalidOperation_EnumOpCantHappen,
        InvalidOperation_EnumFailedVersion,
        InvalidOperation_EmptyStack,
        ArgumentOutOfRange_BiggerThanCollection,
        InvalidOperation_EnumNotStarted,
        InvalidOperation_EnumEnded,
        NotSupported_SortedListNestedWrite,
        InvalidOperation_NoValue,
        InvalidOperation_RegRemoveSubKey,
        Security_RegistryPermission,
        UnauthorizedAccess_RegistryNoWrite,
        ObjectDisposed_RegKeyClosed,
        NotSupported_InComparableType,
        Argument_InvalidRegistryOptionsCheck,
        Argument_InvalidRegistryViewCheck
    }


    //[FriendAccessAllowed]
    internal static class CompatibilitySwitches
    {
        private static bool s_AreSwitchesSet;
#if FEATURE_CORECLR && !FEATURE_CORESYSTEM
        private static bool s_isAppEarlierThanSilverlight4;
#endif //FEATURE_CORECLR && !FEATURE_CORESYSTEM

#if FEATURE_LEGACYNETCF
        private static bool s_isAppEarlierThanWindowsPhone8;
        private static bool s_isAppEarlierThanWindowsPhoneMango;
#endif //FEATURE_LEGACYNETCF

#if FEATURE_CORECLR
        private static bool s_isAppSilverlight81;  // The app targets SL8.1 version
        private static bool s_useLatestBehaviorWhenTFMNotSpecified; // Which behavior to use when the TFM is not specified.
#endif //FEATURE_CORECLR

#if !FEATURE_CORECLR
        private static bool s_isNetFx40TimeSpanLegacyFormatMode;
        private static bool s_isNetFx40LegacySecurityPolicy;
        private static bool s_isNetFx45LegacyManagedDeflateStream;
#endif //!FEATURE_CORECLR

        public static bool IsCompatibilityBehaviorDefined
        {
            get
            {
                return s_AreSwitchesSet;
            }
        }

        private static bool IsCompatibilitySwitchSet(string compatibilitySwitch)
        {
            bool? result = AppDomain.CurrentDomain.IsCompatibilitySwitchSet(compatibilitySwitch);
            return (result.HasValue && result.Value);
        }

        internal static void InitializeSwitches()
        {
#if FEATURE_CORECLR && !FEATURE_CORESYSTEM
            s_isAppEarlierThanSilverlight4 = IsCompatibilitySwitchSet("APP_EARLIER_THAN_SL4.0");
#endif //FEATURE_CORECLR && !FEATURE_CORESYSTEM


#if FEATURE_CORECLR
            s_isAppSilverlight81 = IsCompatibilitySwitchSet("WindowsPhone_5.1.0.0");
            s_useLatestBehaviorWhenTFMNotSpecified = IsCompatibilitySwitchSet("UseLatestBehaviorWhenTFMMissing");
#endif //FEATURE_CORECLR

#if FEATURE_LEGACYNETCF
            s_isAppEarlierThanWindowsPhoneMango = IsCompatibilitySwitchSet("WindowsPhone_3.7.0.0");
            s_isAppEarlierThanWindowsPhone8 = s_isAppEarlierThanWindowsPhoneMango || 
                                                IsCompatibilitySwitchSet("WindowsPhone_3.8.0.0"); 
                    
#endif //FEATURE_LEGACYNETCF

#if !FEATURE_CORECLR
            s_isNetFx40TimeSpanLegacyFormatMode = IsCompatibilitySwitchSet("NetFx40_TimeSpanLegacyFormatMode");
            s_isNetFx40LegacySecurityPolicy = IsCompatibilitySwitchSet("NetFx40_LegacySecurityPolicy");
            s_isNetFx45LegacyManagedDeflateStream = IsCompatibilitySwitchSet("NetFx45_LegacyManagedDeflateStream");
#endif //FEATURE_CORECLR

            s_AreSwitchesSet = true;
        }

        public static bool IsAppEarlierThanSilverlight4
        {
            get
            {
#if FEATURE_CORECLR && !FEATURE_CORESYSTEM
                return s_isAppEarlierThanSilverlight4;
#else
                return false;
#endif //FEATURE_CORECLR && !FEATURE_CORESYSTEM
            }
        }

#if FEATURE_CORECLR
        /// <summary>
        /// This property returns whether the app is hosted under SL 8.1 version
        /// </summary>
        internal static bool IsAppSilverlight81
        {
            get
            {
                // PS - Do not use this property for adding quirks. Please use the exposed properties of BinaryCompatiblity class instead.
                return s_isAppSilverlight81;
            }
        }
 
        /// <summary>
        /// This property returns whether to give the latest behavior when the TFM is missing
        /// </summary>
        internal static bool UseLatestBehaviorWhenTFMNotSpecified
        {
            get
            {
                return s_useLatestBehaviorWhenTFMNotSpecified;
            }
        }
#endif //FEATURE_CORECLR

        public static bool IsAppEarlierThanWindowsPhone8
        {
            get
            {
#if FEATURE_LEGACYNETCF
                return s_isAppEarlierThanWindowsPhone8;
#else
                return false;
#endif //FEATURE_LEGACYNETCF
            }
        }

        public static bool IsAppEarlierThanWindowsPhoneMango
        {
            get
            {
#if FEATURE_LEGACYNETCF
                return s_isAppEarlierThanWindowsPhoneMango;
#else
                return false;
#endif //FEATURE_LEGACYNETCF
            }
        }

        public static bool IsNetFx40TimeSpanLegacyFormatMode
        {
            get
            {
#if !FEATURE_CORECLR
                return s_isNetFx40TimeSpanLegacyFormatMode;
#else
                return false;
#endif //!FEATURE_CORECLR
            }
        }

        public static bool IsNetFx40LegacySecurityPolicy
        {
            get
            {
#if !FEATURE_CORECLR
                return s_isNetFx40LegacySecurityPolicy;
#else
                return false;
#endif //!FEATURE_CORECLR
            }
        }

        public static bool IsNetFx45LegacyManagedDeflateStream
        {
            get
            {
#if !FEATURE_CORECLR
                return s_isNetFx45LegacyManagedDeflateStream;
#else
                return false;
#endif //!FEATURE_CORECLR
            }
        }
    }


    /// <summary>
    /// A helper class to get the number of processors, it updates the numbers of processors every sampling interval.
    /// </summary>
    internal static class PlatformHelper
    {
        private const int PROCESSOR_COUNT_REFRESH_INTERVAL_MS = 30000; // How often to refresh the count, in milliseconds.
        private static volatile int s_processorCount; // The last count seen.
        private static volatile int s_lastProcessorCountRefreshTicks; // The last time we refreshed.

        /// <summary>
        /// Gets the number of available processors
        /// </summary>
        internal static int ProcessorCount
        {
            get
            {
                int now = Environment.TickCount;
                int procCount = s_processorCount;
                if (procCount == 0 || (now - s_lastProcessorCountRefreshTicks) >= PROCESSOR_COUNT_REFRESH_INTERVAL_MS)
                {
                    s_processorCount = procCount = Environment.ProcessorCount;
                    s_lastProcessorCountRefreshTicks = now;
                }

                Contract.Assert(procCount > 0 && procCount <= 64,
                    "Processor count not within the expected range (1 - 64).");

                return procCount;
            }
        }

        /// <summary>
        /// Gets whether the current machine has only a single processor.
        /// </summary>
        internal static bool IsSingleProcessor
        {
            get { return ProcessorCount == 1; }
        }
    }


#if !FEATURE_PAL    // PAL doesn't support  eventing


    /// <summary>Provides an event source for tracing CDS collection information.</summary>
    //[System.Runtime.CompilerServices.FriendAccessAllowed]
    [EventSource(
        Name = "System.Collections.Concurrent.ConcurrentCollectionsEventSource",
        Guid = "35167F8E-49B2-4b96-AB86-435B59336B5E",
        LocalizationResources = "mscorlib")]
    internal sealed class CDSCollectionETWBCLProvider : EventSource
    {
        /// <summary>
        /// Defines the singleton instance for the collection ETW provider.
        /// The collection provider GUID is {35167F8E-49B2-4b96-AB86-435B59336B5E}.
        /// </summary>
        public static CDSCollectionETWBCLProvider Log = new CDSCollectionETWBCLProvider();
        /// <summary>Prevent external instantiation.  All logging should go through the Log instance.</summary>
        private CDSCollectionETWBCLProvider() { }

        /// <summary>Enabled for all keywords.</summary>
        private const EventKeywords ALL_KEYWORDS = (EventKeywords)(-1);

        //-----------------------------------------------------------------------------------
        //        
        // CDS Collection Event IDs (must be unique)
        //

        private const int CONCURRENTSTACK_FASTPUSHFAILED_ID = 1;
        private const int CONCURRENTSTACK_FASTPOPFAILED_ID = 2;
        private const int CONCURRENTDICTIONARY_ACQUIRINGALLLOCKS_ID = 3;
        private const int CONCURRENTBAG_TRYTAKESTEALS_ID = 4;
        private const int CONCURRENTBAG_TRYPEEKSTEALS_ID = 5;


        /////////////////////////////////////////////////////////////////////////////////////
        //
        // ConcurrentStack Events
        //

        [Event(CONCURRENTSTACK_FASTPUSHFAILED_ID, Level = EventLevel.Warning)]
        public void ConcurrentStack_FastPushFailed(int spinCount)
        {
            if (IsEnabled(EventLevel.Warning, ALL_KEYWORDS))
            {
                WriteEvent(CONCURRENTSTACK_FASTPUSHFAILED_ID, spinCount);
            }
        }

        [Event(CONCURRENTSTACK_FASTPOPFAILED_ID, Level = EventLevel.Warning)]
        public void ConcurrentStack_FastPopFailed(int spinCount)
        {
            if (IsEnabled(EventLevel.Warning, ALL_KEYWORDS))
            {
                WriteEvent(CONCURRENTSTACK_FASTPOPFAILED_ID, spinCount);
            }
        }


        /////////////////////////////////////////////////////////////////////////////////////
        //
        // ConcurrentDictionary Events
        //

        [Event(CONCURRENTDICTIONARY_ACQUIRINGALLLOCKS_ID, Level = EventLevel.Warning)]
        public void ConcurrentDictionary_AcquiringAllLocks(int numOfBuckets)
        {
            if (IsEnabled(EventLevel.Warning, ALL_KEYWORDS))
            {
                WriteEvent(CONCURRENTDICTIONARY_ACQUIRINGALLLOCKS_ID, numOfBuckets);
            }
        }



        //
        // Events below this point are used by the CDS types in System.DLL
        //

        /////////////////////////////////////////////////////////////////////////////////////
        //
        // ConcurrentBag Events
        //

        [Event(CONCURRENTBAG_TRYTAKESTEALS_ID, Level = EventLevel.Verbose)]
        public void ConcurrentBag_TryTakeSteals()
        {
            if (IsEnabled(EventLevel.Verbose, ALL_KEYWORDS))
            {
                WriteEvent(CONCURRENTBAG_TRYTAKESTEALS_ID);
            }
        }

        [Event(CONCURRENTBAG_TRYPEEKSTEALS_ID, Level = EventLevel.Verbose)]
        public void ConcurrentBag_TryPeekSteals()
        {
            if (IsEnabled(EventLevel.Verbose, ALL_KEYWORDS))
            {
                WriteEvent(CONCURRENTBAG_TRYPEEKSTEALS_ID);
            }
        }
    }

#endif // !FEATURE_PAL



}
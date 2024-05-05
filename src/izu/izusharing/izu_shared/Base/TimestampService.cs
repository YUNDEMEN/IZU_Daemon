using System.Collections.Concurrent;

namespace IZU.Base
{
    public class TimestampService
    {
        static readonly DateTime time1970;
        static long _timestamp;
        static ConcurrentDictionary<string, long> _timestamps;
        static TimestampService()
        {
            time1970 = new(1970, 1, 1, 0, 0, 0, 0);
            _timestamps = new();

        }

        public static long Current()
        {
            return Convert.ToInt64((DateTime.UtcNow - time1970).TotalMilliseconds);
        }

        public static long Pinning(string key)
        {
            _timestamp = Current();
            if (_timestamps.TryAdd(key, _timestamp))
                return _timestamp;
            return 0;
        }

        public static long Difference(string key)
        {
            if (_timestamps.TryRemove(key, out _timestamp))
                return Current() - _timestamp;
            return 0;
        }
    }
}

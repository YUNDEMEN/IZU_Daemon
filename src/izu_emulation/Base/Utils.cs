using System.Xml;
using System.Xml.Serialization;
using static IZU.Base.Utils;

namespace IZU.Base
{
    public partial class Utils
    {
        /// <summary>
        /// Serializes an object.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="serializableObject"></param>
        /// <param name="fileName"></param>
        public void SerializeObject<T>(T serializableObject, string fileName)
        {
            if (serializableObject == null) { return; }

            try
            {
                XmlDocument xmlDocument = new XmlDocument();
                XmlSerializer serializer = new XmlSerializer(serializableObject.GetType());
                using (MemoryStream stream = new MemoryStream())
                {
                    serializer.Serialize(stream, serializableObject);
                    stream.Position = 0;
                    xmlDocument.Load(stream);
                    xmlDocument.Save(fileName);
                }
            }
            catch (Exception ex)
            {
                //Log exception here
            }
        }


        /// <summary>
        /// Deserializes an xml file into an object list
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public T DeSerializeObject<T>(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) { return default(T); }

            T objectOut = default(T);

            try
            {
                XmlDocument xmlDocument = new XmlDocument();
                xmlDocument.Load(fileName);
                string xmlString = xmlDocument.OuterXml;

                using (StringReader read = new StringReader(xmlString))
                {
                    Type outType = typeof(T);

                    XmlSerializer serializer = new XmlSerializer(outType);
                    using (XmlReader reader = new XmlTextReader(read))
                    {
                        objectOut = (T)serializer.Deserialize(reader);
                    }
                }
            }
            catch (Exception ex)
            {
                //Log exception here
            }

            return objectOut;
        }
    }

    public partial class Utils
    {
        /// <summary>
        /// Writes the given object instance to a binary file.
        /// <para>Object type (and all child types) must be decorated with the [Serializable] attribute.</para>
        /// <para>To prevent a variable from being serialized, decorate it with the [NonSerialized] attribute; cannot be applied to properties.</para>
        /// </summary>
        /// <typeparam name="T">The type of object being written to the XML file.</typeparam>
        /// <param name="filePath">The file path to write the object instance to.</param>
        /// <param name="objectToWrite">The object instance to write to the XML file.</param>
        /// <param name="append">If false the file will be overwritten if it already exists. If true the contents will be appended to the file.</param>
        public static void WriteToBinaryFile<T>(string filePath, T objectToWrite, bool append = false)
        {
            using Stream stream = File.Open(filePath, append ? FileMode.Append : FileMode.Create);
            var binaryFormatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
            binaryFormatter.Serialize(stream, objectToWrite);
        }

        /// <summary>
        /// Reads an object instance from a binary file.
        /// </summary>
        /// <typeparam name="T">The type of object to read from the XML.</typeparam>
        /// <param name="filePath">The file path to read the object instance from.</param>
        /// <returns>Returns a new instance of the object read from the binary file.</returns>
        public static T ReadFromBinaryFile<T>(string filePath)
        {
            using Stream stream = File.Open(filePath, FileMode.Open);
            var binaryFormatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
            return (T)binaryFormatter.Deserialize(stream);
        }


        [Serializable]
        public class Person
        {
            public string Name { get; set; }
            public int Age = 20;
            public Address HomeAddress { get; set; }
            private string _thisWillGetWrittenToTheFileToo = "even though it is a private variable.";

            [NonSerialized]
            public string ThisWillNotBeWrittenToTheFile = "because of the [NonSerialized] attribute.";
        }

        [Serializable]
        public class Address
        {
            public string StreetAddress { get; set; }
            public string City { get; set; }
        }


        public void sample()
        {
            // And then in some function.
            Person person = new Person()
            {
                Name = "Dan",
                Age = 30,
                HomeAddress = new Address()
                {
                    StreetAddress = "123 My St",
                    City = "Regina"
                },
            };
            List<Person> people = new() { person };
            WriteToBinaryFile<Person>(@"C:\person.bin", person);
            WriteToBinaryFile<List<Person>>(@"C:\people.bin", people);

            // Then in some other function.
            person = ReadFromBinaryFile<Person>(@"C:\person.bin");
            people = ReadFromBinaryFile<List<Person>>(@"C:\people.bin");
        }
    }



}

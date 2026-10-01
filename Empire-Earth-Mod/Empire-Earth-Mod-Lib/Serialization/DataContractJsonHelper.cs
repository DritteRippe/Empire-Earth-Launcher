using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;

namespace Empire_Earth_Mod_Lib.Serialization
{
    /// <summary>
    /// Writes and reads <typeparamref name="TType"/> as UTF-8 JSON with <see cref="DataContractJsonSerializer"/>:
    /// only [DataMember] members are written, and reading only creates the types of the data contract, never
    /// a type named by the data.
    /// </summary>
    public static class DataContractJsonHelper<TType> where TType : class
    {
        public static string Serialize(TType instance)
        {
            var serializer = new DataContractJsonSerializer(typeof(TType));
            using (var stream = new MemoryStream())
            {
                serializer.WriteObject(stream, instance);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        public static TType Deserialize(string json)
        {
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                return Deserialize(stream);
            }
        }

        /// <summary>
        /// Reads one object from the current position of <paramref name="stream"/>; the stream is not closed.
        /// </summary>
        /// <exception cref="System.Runtime.Serialization.SerializationException">The data is not valid JSON for
        /// <typeparamref name="TType"/>.</exception>
        public static TType Deserialize(Stream stream)
        {
            var serializer = new DataContractJsonSerializer(typeof(TType));
            return serializer.ReadObject(stream) as TType;
        }
    }
}

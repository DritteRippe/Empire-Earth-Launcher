using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
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
        /// <exception cref="SerializationException">The data is not valid JSON for <typeparamref name="TType"/>,
        /// or a [DataMember] setter rejected a value (e.g. an invalid mod version).</exception>
        public static TType Deserialize(Stream stream)
        {
            var serializer = new DataContractJsonSerializer(typeof(TType));
            try
            {
                return serializer.ReadObject(stream) as TType;
            }
            catch (TargetInvocationException ex) when (ex.InnerException is SerializationException)
            {
                // Depending on the runtime, the serializer calls the [DataMember] setters directly (their
                // exceptions arrive unchanged) or through reflection, which wraps them (seen on Mono). Unwrap
                // them, so that callers get the documented SerializationException on every runtime.
                throw new SerializationException(ex.InnerException.Message, ex.InnerException);
            }
        }
    }
}

using System;
using System.Globalization;
using System.Text;

namespace Empire_Earth_Launcher.RealMachineTests.Json
{
    /// <summary>The JSON the harness writes: quoted strings, with every character outside printable ASCII escaped.</summary>
    internal static class JsonWriter
    {
        /// <summary>
        /// <paramref name="value"/> as a JSON string in double quotes; <c>null</c> for null. Characters outside printable ASCII
        /// are written as <c>\uXXXX</c>, so the output survives any console code page.
        /// </summary>
        public static string Quote(string value)
        {
            if (value == null)
                return "null";
            var text = new StringBuilder(value.Length + 2);
            text.Append('"');
            foreach (char c in value)
            {
                switch (c)
                {
                    case '"':
                        text.Append("\\\"");
                        break;
                    case '\\':
                        text.Append("\\\\");
                        break;
                    case '\n':
                        text.Append("\\n");
                        break;
                    case '\r':
                        text.Append("\\r");
                        break;
                    case '\t':
                        text.Append("\\t");
                        break;
                    default:
                        if (c < ' ' || c > '~')
                            text.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else
                            text.Append(c);
                        break;
                }
            }
            text.Append('"');
            return text.ToString();
        }

        /// <summary><c>"name": value</c> for an object member whose value is already JSON.</summary>
        public static string Member(string name, string json)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));
            return Quote(name) + ": " + (json ?? "null");
        }
    }
}

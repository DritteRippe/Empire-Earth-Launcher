using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using Empire_Earth_Launcher.Core.Platform;
using Microsoft.Win32;

namespace Empire_Earth_Launcher.Core.Backup
{
    /// <summary>
    /// One key of a <c>.reg</c> file: the values it restores and the values it deletes (ADR 0007 amendment, "exact
    /// restore": a value an action creates gets a delete line, so importing the file brings back the previous state).
    /// </summary>
    public sealed class RegFileKey
    {
        private readonly List<KeyValuePair<string, RegistryValue>> values = new List<KeyValuePair<string, RegistryValue>>();
        private readonly List<string> deletedValueNames = new List<string>();

        /// <exception cref="ArgumentException">The key is a hive, or a name in it cannot be written (<see cref="RegFileWriter.CanWrite(RegistryLocation)"/>).</exception>
        public RegFileKey(RegistryLocation key)
        {
            Key = key ?? throw new ArgumentNullException(nameof(key));
            if (key.IsRoot)
                throw new ArgumentException("A .reg file cannot name a hive as a key.", nameof(key));
            if (!RegFileWriter.CanWrite(key))
                throw new ArgumentException("The key " + RegFileWriter.Describe(key.Path) + " cannot be written to a .reg file.", nameof(key));
        }

        public RegistryLocation Key { get; }

        /// <summary>The values in the order they were added; the empty name is the default value (<c>@</c>).</summary>
        public IReadOnlyList<KeyValuePair<string, RegistryValue>> Values
        {
            get { return new ReadOnlyCollection<KeyValuePair<string, RegistryValue>>(values); }
        }

        /// <summary>The values the file deletes (<c>"name"=-</c>), in the order they were added.</summary>
        public IReadOnlyList<string> DeletedValueNames
        {
            get { return new ReadOnlyCollection<string>(deletedValueNames); }
        }

        /// <summary>Adds a value the file writes.</summary>
        public RegFileKey Add(string valueName, RegistryValue value)
        {
            if (valueName == null)
                throw new ArgumentNullException(nameof(valueName));
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            CheckName(valueName);
            if (Contains(valueName))
                throw new ArgumentException("The value \"" + valueName + "\" is already part of " + Key + ".", nameof(valueName));
            values.Add(new KeyValuePair<string, RegistryValue>(valueName, value));
            return this;
        }

        /// <summary>Adds a value the file deletes: one that does not exist now and that an action will create.</summary>
        public RegFileKey Delete(string valueName)
        {
            if (valueName == null)
                throw new ArgumentNullException(nameof(valueName));
            CheckName(valueName);
            if (Contains(valueName))
                throw new ArgumentException("The value \"" + valueName + "\" is already part of " + Key + ".", nameof(valueName));
            deletedValueNames.Add(valueName);
            return this;
        }

        private static void CheckName(string valueName)
        {
            if (!RegFileWriter.CanWriteValueName(valueName))
                throw new ArgumentException("The value name " + RegFileWriter.Describe(valueName) + " cannot be written to a .reg file.",
                    nameof(valueName));
        }

        /// <summary>True if the file writes or deletes <paramref name="valueName"/> (names ignore case, as in the registry).</summary>
        public bool Contains(string valueName)
        {
            return values.Any(value => string.Equals(value.Key, valueName, StringComparison.OrdinalIgnoreCase)) ||
                   deletedValueNames.Contains(valueName, StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Writes <c>.reg</c> files in the format of the Registry Editor ("Windows Registry Editor Version 5.00", UTF-16 LE
    /// with BOM, CRLF), so that double-clicking a backup restores it (ADR 0007).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Value lines: <c>REG_SZ</c> as <c>"text"</c> with <c>\</c> and <c>"</c> escaped, or as <c>hex(1):</c> (UTF-16 LE with
    /// the terminating NUL) if it contains CR, LF or NUL, which the quoted form cannot hold; <c>REG_DWORD</c> as
    /// <c>dword:</c> with 8 hex digits; <c>REG_QWORD</c> as <c>hex(b):</c>; <c>REG_EXPAND_SZ</c> as <c>hex(2):</c>;
    /// <c>REG_MULTI_SZ</c> as <c>hex(7):</c>; <c>REG_BINARY</c> as <c>hex:</c>; <c>REG_NONE</c> as <c>hex(0):</c> and
    /// every other type as <c>hex(&lt;type in hex&gt;):</c> with its bytes. The default value is <c>@</c>; value names
    /// escape <c>\</c> and <c>"</c>. Delete lines (<c>"name"=-</c>) follow the values of their key, before the next key.
    /// </para>
    /// <para>
    /// Hex data is wrapped like the Registry Editor does: a line ends with <c>,\</c> before it would get longer than 77
    /// characters, and the next line starts with two spaces. Keys are written as <c>HKEY_CURRENT_USER\...</c>;
    /// <c>HKLM32\Software\X</c> becomes <c>HKEY_LOCAL_MACHINE\Software\WOW6432Node\X</c>, the key the 32-bit view
    /// names on 64-bit Windows. Golden files in the tests fix every case.
    /// </para>
    /// <para>
    /// Names with a control character (CR, LF, NUL, ...) and key names with <c>]</c> cannot be written: the format has no
    /// escape for them, and a line break in a name would end its line and let the rest of the name become a line of its own,
    /// e.g. <c>[-HKEY_LOCAL_MACHINE\SOFTWARE\Sierra\CDKeys]</c> (security review, D6). <see cref="RegFileKey"/> refuses
    /// them; <see cref="RegistryExport"/> reports such a tree as <see cref="RegistryStatus.InvalidName"/>, so the backup
    /// fails and nothing is changed.
    /// </para>
    /// </remarks>
    public static class RegFileWriter
    {
        /// <summary>The first line of every file.</summary>
        public const string Header = "Windows Registry Editor Version 5.00";

        private const string NewLine = "\r\n";
        private const int MaxHexLineLength = 77;

        /// <summary>True if <paramref name="valueName"/> can be written to a .reg file: no control character.</summary>
        public static bool CanWriteValueName(string valueName)
        {
            if (valueName == null)
                throw new ArgumentNullException(nameof(valueName));
            return valueName.All(c => c >= ' ');
        }

        /// <summary>True if every name of <paramref name="key"/> can be written to a .reg file: no control character, no <c>]</c>.</summary>
        public static bool CanWrite(RegistryLocation key)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));
            return key.Segments.All(segment => CanWriteValueName(segment) && segment.IndexOf(']') < 0);
        }

        /// <summary>A name for an error message or the log, with control characters as <c>\uXXXX</c>.</summary>
        internal static string Describe(string name)
        {
            var text = new StringBuilder("\"");
            foreach (char c in name)
            {
                if (c < ' ')
                    text.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                else
                    text.Append(c);
            }
            return text.Append('"').ToString();
        }

        /// <summary>The bytes of the file: UTF-16 LE with BOM.</summary>
        public static byte[] ToBytes(IEnumerable<RegFileKey> keys)
        {
            string text = ToText(keys);
            byte[] bom = Encoding.Unicode.GetPreamble();
            byte[] body = Encoding.Unicode.GetBytes(text);
            var bytes = new byte[bom.Length + body.Length];
            Buffer.BlockCopy(bom, 0, bytes, 0, bom.Length);
            Buffer.BlockCopy(body, 0, bytes, bom.Length, body.Length);
            return bytes;
        }

        /// <summary>The text of the file with CRLF line ends: the header, then every key with its lines and an empty line.</summary>
        public static string ToText(IEnumerable<RegFileKey> keys)
        {
            if (keys == null)
                throw new ArgumentNullException(nameof(keys));
            var text = new StringBuilder();
            text.Append(Header).Append(NewLine).Append(NewLine);
            foreach (RegFileKey key in keys)
            {
                if (key == null)
                    throw new ArgumentException("The keys contain null.", nameof(keys));
                text.Append('[').Append(KeyName(key.Key)).Append(']').Append(NewLine);
                foreach (KeyValuePair<string, RegistryValue> value in key.Values)
                    text.Append(ValueLine(value.Key, value.Value)).Append(NewLine);
                foreach (string name in key.DeletedValueNames)
                    text.Append(Name(name)).Append("=-").Append(NewLine);
                text.Append(NewLine);
            }
            return text.ToString();
        }

        /// <summary>The key as the Registry Editor names it, e.g. <c>HKEY_CURRENT_USER\Software\Neo\Empire Earth</c>.</summary>
        public static string KeyName(RegistryLocation key)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));
            string path = key.Path;
            switch (key.Hive)
            {
                case RegistryHive.CurrentUser:
                    return Join("HKEY_CURRENT_USER", path);
                case RegistryHive.LocalMachine:
                    if (key.View == RegistryView.Registry32)
                    {
                        IReadOnlyList<string> segments = key.Segments;
                        if (segments.Count > 0 && string.Equals(segments[0], "Software", StringComparison.OrdinalIgnoreCase) &&
                            (segments.Count == 1 || !string.Equals(segments[1], "WOW6432Node", StringComparison.OrdinalIgnoreCase)))
                            path = segments[0] + @"\WOW6432Node" + path.Substring(segments[0].Length);
                    }
                    return Join("HKEY_LOCAL_MACHINE", path);
                case RegistryHive.Users:
                    return Join("HKEY_USERS", path);
                case RegistryHive.ClassesRoot:
                    return Join("HKEY_CLASSES_ROOT", path);
                default:
                    return Join("HKEY_CURRENT_CONFIG", path);
            }
        }

        /// <summary>One value line without its line end, wrapped hex data included.</summary>
        public static string ValueLine(string valueName, RegistryValue value)
        {
            if (valueName == null)
                throw new ArgumentNullException(nameof(valueName));
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            string prefix = Name(valueName) + "=";
            switch (value.Type)
            {
                case RegistryValueType.String:
                    string text = value.StringValue;
                    if (text.IndexOfAny(new[] { '\r', '\n', '\0' }) < 0)
                        return prefix + "\"" + Escape(text) + "\"";
                    return Hex(prefix + "hex(1):", Encoding.Unicode.GetBytes(text + "\0"));
                case RegistryValueType.DWord:
                    return prefix + "dword:" + ((uint)value.DWordValue).ToString("x8", CultureInfo.InvariantCulture);
                case RegistryValueType.QWord:
                    return Hex(prefix + "hex(b):", BitConverter.GetBytes(value.QWordValue));
                case RegistryValueType.ExpandString:
                    return Hex(prefix + "hex(2):", Encoding.Unicode.GetBytes(value.StringValue + "\0"));
                case RegistryValueType.MultiString:
                    string joined = string.Concat(value.MultiStringValue.Select(item => item + "\0")) + "\0";
                    return Hex(prefix + "hex(7):", Encoding.Unicode.GetBytes(joined));
                case RegistryValueType.Binary:
                    return Hex(prefix + "hex:", value.GetBytes());
                default:
                    return Hex(prefix + "hex(" + ((int)value.Type).ToString("x", CultureInfo.InvariantCulture) + "):",
                        value.GetBytes());
            }
        }

        private static string Join(string hive, string path)
        {
            return path.Length == 0 ? hive : hive + @"\" + path;
        }

        /// <summary><c>@</c> for the default value, else the quoted, escaped name.</summary>
        private static string Name(string valueName)
        {
            return valueName.Length == 0 ? "@" : "\"" + Escape(valueName) + "\"";
        }

        private static string Escape(string text)
        {
            return text.Replace(@"\", @"\\").Replace("\"", "\\\"");
        }

        /// <summary>The bytes as <c>xx,xx,...</c> after <paramref name="prefix"/>, wrapped with <c>,\</c> and two spaces.</summary>
        private static string Hex(string prefix, byte[] bytes)
        {
            var line = new StringBuilder(prefix);
            int lineLength = prefix.Length;
            for (int i = 0; i < bytes.Length; i++)
            {
                string token = bytes[i].ToString("x2", CultureInfo.InvariantCulture) + (i < bytes.Length - 1 ? "," : string.Empty);
                if (lineLength + token.Length > MaxHexLineLength)
                {
                    line.Append('\\').Append(NewLine).Append("  ");
                    lineLength = 2;
                }
                line.Append(token);
                lineLength += token.Length;
            }
            return line.ToString();
        }
    }
}

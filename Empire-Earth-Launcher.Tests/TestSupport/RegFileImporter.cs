using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Tests.Fakes;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.TestSupport
{
    /// <summary>
    /// Imports a <c>.reg</c> file into an <see cref="InMemoryRegistry"/> the way the Registry Editor does it on a
    /// double-click: every key is created, every value written, every <c>"name"=-</c> deleted; keys are never deleted.
    /// Only for the tests of the backups ("importing the file restores the previous values exactly", ADR 0007); it reads
    /// the HKCU keys and value forms <c>RegFileWriter</c> writes.
    /// </summary>
    internal static class RegFileImporter
    {
        public static void Import(byte[] file, InMemoryRegistry registry)
        {
            Assert.That(file.Take(2), Is.EqualTo(new byte[] { 0xFF, 0xFE }), "UTF-16 LE with BOM");
            string text = Encoding.Unicode.GetString(file, 2, file.Length - 2);
            var lines = new List<string>();
            foreach (string raw in text.Split(new[] { "\r\n" }, StringSplitOptions.None))
            {
                if (lines.Count > 0 && lines[lines.Count - 1].EndsWith(@",\", StringComparison.Ordinal))
                    lines[lines.Count - 1] = lines[lines.Count - 1].Substring(0, lines[lines.Count - 1].Length - 1) + raw.TrimStart();
                else
                    lines.Add(raw);
            }
            Assert.That(lines[0], Is.EqualTo("Windows Registry Editor Version 5.00"));

            RegistryLocation key = null;
            foreach (string line in lines.Skip(1).Where(line => line.Length > 0))
            {
                if (line.StartsWith("[", StringComparison.Ordinal))
                {
                    string name = line.Substring(1, line.Length - 2);
                    Assert.That(name, Does.StartWith(@"HKEY_CURRENT_USER\"));
                    key = RegistryLocation.CurrentUser(name.Substring(@"HKEY_CURRENT_USER\".Length));
                    registry.SeedKey(key);
                    continue;
                }
                Assert.That(key, Is.Not.Null, "a value before the first key");
                int position;
                string valueName = ReadName(line, out position);
                string data = line.Substring(position + 1);
                if (data == "-")
                    registry.DeleteValue(key, valueName);
                else
                    registry.Seed(key, valueName, ReadValue(data));
            }
        }

        private static string ReadName(string line, out int equals)
        {
            if (line.StartsWith("@=", StringComparison.Ordinal))
            {
                equals = 1;
                return string.Empty;
            }
            var name = new StringBuilder();
            int i = 1;
            for (; line[i] != '"'; i++)
            {
                if (line[i] == '\\')
                    i++;
                name.Append(line[i]);
            }
            equals = i + 1;
            Assert.That(line[equals], Is.EqualTo('='));
            return name.ToString();
        }

        private static RegistryValue ReadValue(string data)
        {
            if (data.StartsWith("\"", StringComparison.Ordinal))
            {
                var text = new StringBuilder();
                for (int i = 1; i < data.Length - 1; i++)
                {
                    if (data[i] == '\\')
                        i++;
                    text.Append(data[i]);
                }
                return RegistryValue.FromString(text.ToString());
            }
            if (data.StartsWith("dword:", StringComparison.Ordinal))
                return RegistryValue.FromDWord(unchecked((int)uint.Parse(data.Substring(6), NumberStyles.HexNumber, CultureInfo.InvariantCulture)));

            int colon = data.IndexOf(':');
            string kind = data.Substring(0, colon);
            byte[] bytes = data.Substring(colon + 1).Length == 0
                ? new byte[0]
                : data.Substring(colon + 1).Split(',').Select(b => byte.Parse(b, NumberStyles.HexNumber, CultureInfo.InvariantCulture)).ToArray();
            int type = kind == "hex" ? 3 : int.Parse(kind.Substring(4, kind.Length - 5), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            switch (type)
            {
                case 1:
                    return RegistryValue.FromString(Encoding.Unicode.GetString(bytes, 0, bytes.Length - 2));
                case 2:
                    return RegistryValue.FromExpandString(Encoding.Unicode.GetString(bytes, 0, bytes.Length - 2));
                case 3:
                    return RegistryValue.FromBinary(bytes);
                case 7:
                    string joined = Encoding.Unicode.GetString(bytes);
                    return RegistryValue.FromMultiString(joined.Length <= 1 ? new string[0] : joined.Substring(0, joined.Length - 2).Split('\0'));
                case 11:
                    return RegistryValue.FromQWord(BitConverter.ToInt64(bytes, 0));
                default:
                    return RegistryValue.FromRaw((RegistryValueType)type, bytes);
            }
        }
    }
}

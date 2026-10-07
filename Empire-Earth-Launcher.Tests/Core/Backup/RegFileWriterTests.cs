using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Empire_Earth_Launcher.Core.Backup;
using Empire_Earth_Launcher.Core.Platform;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.Backup
{
    /// <summary>
    /// <see cref="RegFileWriter"/> against golden files (ADR 0007 amendment, ADR 0012): every value type, the escaping of
    /// names and strings, the default value and the delete lines. The golden files in <c>Golden/</c> were written by an
    /// independent script in the format of the Registry Editor (UTF-16 LE with BOM, CRLF); <c>.gitattributes</c> keeps
    /// their bytes. They are copied next to the test program, so the tests also run outside the source tree.
    /// </summary>
    [TestFixture]
    public class RegFileWriterTests
    {
        private static readonly RegistryLocation Key = RegistryLocation.CurrentUser(@"Software\Neo\Empire Earth");

        /// <summary>Security review: no name with a control character reaches a .reg file, no key name with ']'.</summary>
        [TestCase("a\rb")]
        [TestCase("a\nb")]
        [TestCase("a\0b")]
        [TestCase("tab\there")]
        public void NamesWithControlCharacters_AreRefused(string name)
        {
            Assert.That(RegFileWriter.CanWriteValueName(name), Is.False);
            Assert.That(() => new RegFileKey(Key).Add(name, RegistryValue.FromDWord(1)), Throws.ArgumentException);
            Assert.That(() => new RegFileKey(Key).Delete(name), Throws.ArgumentException);
            Assert.That(() => new RegFileKey(Key.Child(name)), Throws.ArgumentException);
        }

        [Test]
        public void KeyNamesWithAClosingBracket_AreRefused_ValueNamesMayHaveOne()
        {
            Assert.That(RegFileWriter.CanWrite(Key.Child("a]b")), Is.False);
            Assert.That(() => new RegFileKey(Key.Child("a]b")), Throws.ArgumentException);
            Assert.That(RegFileWriter.CanWriteValueName("a]b \"quoted\" \\ [x]"), Is.True);
            Assert.That(RegFileWriter.CanWrite(Key.Child(@"Game Options")), Is.True);
        }

        private static readonly RegistryLocation TestKey = RegistryLocation.CurrentUser(@"Software\Empire Earth Launcher Tests");

        private static byte[] Golden(string name)
        {
            return File.ReadAllBytes(Path.Combine(TestContext.CurrentContext.TestDirectory, "Core", "Backup", "Golden", name));
        }

        private static void AssertGolden(string name, params RegFileKey[] keys)
        {
            byte[] expected = Golden(name);
            byte[] actual = RegFileWriter.ToBytes(keys);
            Assert.That(Encoding.Unicode.GetString(actual), Is.EqualTo(Encoding.Unicode.GetString(expected)), name);
            Assert.That(actual, Is.EqualTo(expected), name + ": bytes");
        }

        [Test]
        public void RegSz_EscapesBackslashesAndQuotes()
        {
            AssertGolden("RegSzEscaping.reg", new RegFileKey(TestKey)
                .Add("Plain", RegistryValue.FromString("Empire Earth"))
                .Add("Path", RegistryValue.FromString(@"C:\Games\EE\"))
                .Add("Quote", RegistryValue.FromString("say \"hi\""))
                .Add("Both", RegistryValue.FromString("a\\\"b"))
                .Add("Empty", RegistryValue.FromString(string.Empty))
                .Add("Umlaut", RegistryValue.FromString("Spielstände é ü")));
        }

        [Test]
        public void DWord_HasEightLowerCaseHexDigits()
        {
            AssertGolden("DWord.reg", new RegFileKey(TestKey)
                .Add("Zero", RegistryValue.FromDWord(0))
                .Add("AutoSave In Milliseconds", RegistryValue.FromDWord(1200000))
                .Add("Minus One", RegistryValue.FromDWord(-1))
                .Add("Game Bit Depth", RegistryValue.FromDWord(32)));
        }

        [Test]
        public void QWord_IsHexB_LittleEndian()
        {
            AssertGolden("QWord.reg", new RegFileKey(TestKey)
                .Add("Big", RegistryValue.FromQWord(0x1122334455667788))
                .Add("Zero", RegistryValue.FromQWord(0))
                .Add("Minus One", RegistryValue.FromQWord(-1)));
        }

        [Test]
        public void ExpandString_IsHex2_WithTheTerminatingNul_Wrapped()
        {
            AssertGolden("ExpandString.reg", new RegFileKey(TestKey)
                .Add("Folder", RegistryValue.FromExpandString(@"%LOCALAPPDATA%\Empire Earth Launcher\Backups"))
                .Add("Empty", RegistryValue.FromExpandString(string.Empty)));
        }

        [Test]
        public void MultiString_IsHex7_WithTheDoubleNul()
        {
            AssertGolden("MultiString.reg", new RegFileKey(TestKey)
                .Add("Two", RegistryValue.FromMultiString(new[] { "one", "two" }))
                .Add("None", RegistryValue.FromMultiString(new string[0]))
                .Add("With Empty", RegistryValue.FromMultiString(new[] { "a", string.Empty, "b" })));
        }

        [Test]
        public void Binary_IsHex_Wrapped()
        {
            AssertGolden("Binary.reg", new RegFileKey(TestKey)
                .Add("Forty Bytes", RegistryValue.FromBinary(Enumerable.Range(0, 40).Select(i => (byte)i).ToArray()))
                .Add("Empty", RegistryValue.FromBinary(new byte[0]))
                .Add("One", RegistryValue.FromBinary(new byte[] { 0xFF })));
        }

        [Test]
        public void None_IsHex0()
        {
            AssertGolden("None.reg", new RegFileKey(TestKey)
                .Add("Nothing", RegistryValue.FromRaw(RegistryValueType.None, new byte[] { 1, 2 }))
                .Add("Empty", RegistryValue.FromRaw(RegistryValueType.None, new byte[0])));
        }

        [Test]
        public void RegSzWithCrLfOrNul_IsHex1()
        {
            AssertGolden("StringWithLineBreaks.reg", new RegFileKey(TestKey)
                .Add("CRLF", RegistryValue.FromString("line 1\r\nline 2"))
                .Add("LF", RegistryValue.FromString("\n"))
                .Add("CR", RegistryValue.FromString("a\rb"))
                .Add("NUL", RegistryValue.FromString("a\0b")));
        }

        [Test]
        public void UnknownTypes_AreHexWithTheTypeNumber()
        {
            AssertGolden("UnknownTypes.reg", new RegFileKey(TestKey)
                .Add("Resource List", RegistryValue.FromRaw((RegistryValueType)8, new byte[] { 1, 0, 0, 0 }))
                .Add("Type 10", RegistryValue.FromRaw((RegistryValueType)10, new byte[] { 0xAB }))
                .Add("Type 256", RegistryValue.FromRaw((RegistryValueType)256, new byte[] { 0, 1 })));
        }

        [Test]
        public void DefaultValueIsAt_NamesAreEscaped()
        {
            AssertGolden("DefaultValueAndNames.reg", new RegFileKey(TestKey)
                .Add(string.Empty, RegistryValue.FromString("default"))
                .Add(@"Name\With\Backslashes", RegistryValue.FromString("x"))
                .Add("Name \"Quoted\"", RegistryValue.FromDWord(1))
                .Add("Back\\\"Slash", RegistryValue.FromDWord(2)));
        }

        [Test]
        public void DeleteLines_FollowTheirKeyHeader()
        {
            var settings = RegistryLocation.CurrentUser(@"Software\Neo\Empire Earth");
            AssertGolden("DeleteLines.reg",
                new RegFileKey(settings)
                    .Add("Rasterizer Name", RegistryValue.FromString("Direct3D"))
                    .Add("Game Window Width", RegistryValue.FromDWord(1920))
                    .Delete("Wait for VSync")
                    .Delete("Texture Bit Depth")
                    .Delete(string.Empty),
                new RegFileKey(settings.Child("Game Options"))
                    .Add("Map Type", RegistryValue.FromString("Continental"))
                    .Delete("Ending Epoch"),
                new RegFileKey(settings.Child(@"Game Options\Empty")).Delete("Map Size"),
                new RegFileKey(RegistryLocation.CurrentUser(@"Software\Empire Earth Community\GameDefaults\NeoEE")).Delete("EE"));
        }

        /// <summary>Every delete line is below the header of its own key and above the next header (ADR 0007 amendment).</summary>
        [Test]
        public void DeleteLines_AreBetweenTheirHeaderAndTheNextOne()
        {
            string text = RegFileWriter.ToText(new[]
            {
                new RegFileKey(RegistryLocation.CurrentUser(@"Software\A")).Delete("One").Add("Two", RegistryValue.FromDWord(2)),
                new RegFileKey(RegistryLocation.CurrentUser(@"Software\A\B")).Delete("Three"),
            });
            string[] lines = text.Split(new[] { "\r\n" }, StringSplitOptions.None);

            int headerA = Array.IndexOf(lines, @"[HKEY_CURRENT_USER\Software\A]");
            int headerB = Array.IndexOf(lines, @"[HKEY_CURRENT_USER\Software\A\B]");
            Assert.That(Array.IndexOf(lines, "\"One\"=-"), Is.GreaterThan(headerA).And.LessThan(headerB));
            Assert.That(Array.IndexOf(lines, "\"Three\"=-"), Is.GreaterThan(headerB));
            Assert.That(Array.IndexOf(lines, "\"Two\"=dword:00000002"), Is.LessThan(Array.IndexOf(lines, "\"One\"=-")),
                "values first, then the delete lines");
        }

        [Test]
        public void File_IsUtf16LittleEndianWithBomAndCrLf()
        {
            byte[] bytes = RegFileWriter.ToBytes(new[] { new RegFileKey(TestKey).Add("A", RegistryValue.FromDWord(1)) });

            Assert.That(bytes.Take(2), Is.EqualTo(new byte[] { 0xFF, 0xFE }));
            string text = Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
            Assert.That(text, Does.StartWith(RegFileWriter.Header + "\r\n\r\n"));
            Assert.That(text.Replace("\r\n", string.Empty), Does.Not.Contain("\n").And.Not.Contain("\r"));
        }

        [TestCase(@"HKCU\Software\Neo\Empire Earth", @"HKEY_CURRENT_USER\Software\Neo\Empire Earth")]
        [TestCase(@"HKLM64\Software\SSSI\Empire Earth", @"HKEY_LOCAL_MACHINE\Software\SSSI\Empire Earth")]
        [TestCase(@"HKLM32\Software\SSSI\Empire Earth", @"HKEY_LOCAL_MACHINE\Software\WOW6432Node\SSSI\Empire Earth")]
        [TestCase(@"HKLM32\SOFTWARE", @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node")]
        [TestCase(@"HKLM32\Software\WOW6432Node\X", @"HKEY_LOCAL_MACHINE\Software\WOW6432Node\X")]
        [TestCase(@"HKLM32\System\X", @"HKEY_LOCAL_MACHINE\System\X")]
        [TestCase(@"HKU\S-1-5-21-1\Software", @"HKEY_USERS\S-1-5-21-1\Software")]
        [TestCase(@"HKCR\.eem", @"HKEY_CLASSES_ROOT\.eem")]
        public void KeyName_UsesTheNamesOfTheRegistryEditor(string location, string expected)
        {
            Assert.That(RegFileWriter.KeyName(RegistryLocation.Parse(location)), Is.EqualTo(expected));
        }

        [Test]
        public void Key_RefusesAValueTwiceAndTheHive()
        {
            var key = new RegFileKey(TestKey).Add("A", RegistryValue.FromDWord(1));

            Assert.That(() => key.Add("a", RegistryValue.FromDWord(2)), Throws.ArgumentException);
            Assert.That(() => key.Delete("A"), Throws.ArgumentException);
            Assert.That(() => new RegFileKey(RegistryLocation.CurrentUser(string.Empty)), Throws.ArgumentException);
        }

        [Test]
        public void HexLines_AreNeverLongerThan78Characters()
        {
            string text = RegFileWriter.ToText(new[]
            {
                new RegFileKey(TestKey).Add("A long name of a binary value", RegistryValue.FromBinary(new byte[500]))
            });
            IEnumerable<string> lines = text.Split(new[] { "\r\n" }, StringSplitOptions.None);

            Assert.That(lines.Max(line => line.Length), Is.LessThanOrEqualTo(78));
            Assert.That(lines.Where(line => line.EndsWith(@"\", StringComparison.Ordinal)).All(line => line.EndsWith(@",\", StringComparison.Ordinal)),
                Is.True);
        }
    }
}

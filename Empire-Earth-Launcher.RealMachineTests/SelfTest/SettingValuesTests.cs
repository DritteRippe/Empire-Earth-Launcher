using System;
using System.Collections.Generic;
using System.Text;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.RealMachineTests.Checks;
using Empire_Earth_Launcher.RealMachineTests.Harness;
using NUnit.Framework;

namespace Empire_Earth_Launcher.RealMachineTests.SelfTest
{
    /// <summary>The saved game settings of a step and their comparison, and the redaction of hashes in every message.</summary>
    [TestFixture]
    [Category(RealMachineCategories.SelfTest)]
    public class SettingValuesTests
    {
        private static readonly SettingValue[] Values =
        {
            new SettingValue(@"C:\Spiele\Ägypten", Game.EmpireEarth, "Music Volume", "DWord:44"),
            new SettingValue(@"C:\Spiele\Ägypten", Game.ArtOfConquest, "Installed From Directory", "String:\\SPIELE\\ÄGYPTEN\\\"x\"\\"),
            new SettingValue(@"C:\Spiele\Ägypten", Game.EmpireEarth, "GpuPreference", SettingValues.Missing)
        };

        [Test]
        public void TheFile_KeepsEveryValue()
        {
            byte[] file = SettingValues.ToJson(Values);

            IReadOnlyList<SettingValue> read = SettingValues.FromJson(file);

            Assert.That(read, Has.Count.EqualTo(3));
            Assert.That(SettingValues.Compare(Values, read, null, "saved"), Is.Empty);
            Assert.That(read[1].Data, Is.EqualTo(Values[1].Data));
            Assert.That(Encoding.ASCII.GetString(file), Does.Contain("\\u00c4gypten"), "written as ASCII");
        }

        [TestCase("{ \"schema\": 2, \"values\": [] }")]
        [TestCase("{ \"schema\": 1, \"values\": [], \"more\": 1 }")]
        [TestCase("{ \"schema\": 1, \"values\": [ { \"root\": \"Games\", \"game\": \"EE\", \"name\": \"x\", \"data\": \"y\" } ] }")]
        [TestCase("{ \"schema\": 1, \"values\": [ { \"root\": \"C:\\\\G\", \"game\": \"Neo\", \"name\": \"x\", \"data\": \"y\" } ] }")]
        [TestCase("{ \"schema\": 1, \"values\": [ { \"root\": \"C:\\\\G\", \"game\": \"EE\", \"name\": \"x\" } ] }")]
        public void AnotherFile_IsRefused(string json)
        {
            Assert.Throws<FormatException>(() => SettingValues.FromJson(Encoding.UTF8.GetBytes(json)));
        }

        [Test]
        public void Compare_NamesEveryDifference_ExceptTheAllowedOnes()
        {
            var now = new[]
            {
                new SettingValue(@"c:\spiele\ägypten\", Game.EmpireEarth, "music volume", "DWord:10"),
                new SettingValue(@"C:\Spiele\Ägypten", Game.ArtOfConquest, "Installed From Directory", "String:\\OTHER\\"),
                new SettingValue(@"C:\Spiele\Ägypten", Game.EmpireEarth, "GpuPreference", SettingValues.Missing),
                new SettingValue(@"C:\Spiele\Ägypten", Game.EmpireEarth, "Marker", "DWord:1")
            };

            IReadOnlyList<string> problems = SettingValues.Compare(Values, now, new[] { "INSTALLED FROM DIRECTORY" }, "saved");

            Assert.That(problems, Is.EqualTo(new[]
            {
                @"c:\spiele\ägypten EE ""music volume"" is DWord:10, saved DWord:44",
                @"C:\Spiele\Ägypten EE ""Marker"": not in saved"
            }));
        }

        [Test]
        public void Describe_WritesTypeAndData()
        {
            Assert.That(SettingValues.Describe(RegistryValue.FromDWord(-1)), Is.EqualTo("DWord:-1"));
            Assert.That(SettingValues.Describe(RegistryValue.FromString("Direct3D")), Is.EqualTo("String:Direct3D"));
            Assert.That(SettingValues.Describe(RegistryValue.FromBinary(new byte[] { 1, 2 })), Is.EqualTo("Binary:REG_BINARY 01 02"));
            Assert.That(SettingValues.Describe((RegistryValue)null), Is.EqualTo("Missing"));
            Assert.That(SettingValues.Describe(RegistryResult<RegistryValue>.Failure(RegistryStatus.AccessDenied, "x")), Is.EqualTo("Unreadable AccessDenied"));
        }

        /// <summary>64 hex digits, built at run time so that no source holds a token that looks like a hash.</summary>
        private static readonly string Hex64 = string.Concat("0123456789abcdef", "0123456789abcdef", "0123456789abcdef", "0123456789ABCDEF");

        [TestCase("hash {64}.", "hash <sha256>.")]
        [TestCase("a/b.dll: {64}, {64}", "a/b.dll: <sha256>, <sha256>")]
        [TestCase("63 {63}", "63 {63}")]
        [TestCase("65 {64}0", "65 {64}0")]
        [TestCase("{64}x", "<sha256>x")]
        public void SafeText_RedactsEverySha256(string text, string redacted)
        {
            string Expand(string template) => template.Replace("{64}", Hex64).Replace("{63}", Hex64.Substring(1));

            Assert.That(SafeText.Redact(Expand(text)), Is.EqualTo(Expand(redacted)));
        }
    }
}

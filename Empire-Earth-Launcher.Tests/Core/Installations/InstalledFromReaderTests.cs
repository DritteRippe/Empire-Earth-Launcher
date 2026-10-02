using System.Collections.Generic;
using System.Linq;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Tests.Fakes;
using Empire_Earth_Launcher.Tests.TestSupport;
using Microsoft.Win32;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.Installations
{
    /// <summary>
    /// The "Installed From" values as source 4 of the discovery: key before hive (ADR 0015, forum report section 8 test
    /// case 8), the EE folder with its parent as root, the AoC folder of foreign installations.
    /// </summary>
    [TestFixture]
    public class InstalledFromReaderTests
    {
        private InstallationWorld world;

        [SetUp]
        public void SetUp()
        {
            world = new InstallationWorld();
        }

        private IReadOnlyList<InstalledFromEntry> Read()
        {
            return new InstalledFromReader(new WriteForbiddingRegistry(world.Registry), world.Logger).Read();
        }

        private static RegistryLocation Key(string key, string hive)
        {
            switch (hive)
            {
                case "HKCU":
                    return InstallationWorld.Hkcu(key);
                case "HKLM32":
                    return InstallationWorld.Hklm32(key);
                default:
                    return InstallationWorld.Hklm64(key);
            }
        }

        /// <summary>
        /// The table of the order: two values in different keys and hives, and which comes first. The Neo key always wins,
        /// whatever the hive; within a key HKCU, HKLM32, HKLM64.
        /// </summary>
        [TestCase("SSSI", "HKCU", "Neo", "HKLM32", "Neo", TestName = "SSSI in HKCU + Neo in HKLM32 -> Neo first")]
        [TestCase("SSSI", "HKCU", "Neo", "HKLM64", "Neo", TestName = "SSSI in HKCU + Neo in HKLM64 -> Neo first")]
        [TestCase("SSSI", "HKLM32", "Neo", "HKLM64", "Neo", TestName = "SSSI in HKLM32 + Neo in HKLM64 -> Neo first")]
        [TestCase("Neo", "HKCU", "SSSI", "HKCU", "Neo", TestName = "Neo in HKCU + SSSI in HKCU -> Neo first")]
        [TestCase("Neo", "HKLM64", "Neo", "HKLM32", "HKLM32", TestName = "Neo in HKLM64 + Neo in HKLM32 -> HKLM32 first")]
        [TestCase("Neo", "HKLM32", "Neo", "HKCU", "HKCU", TestName = "Neo in HKLM32 + Neo in HKCU -> HKCU first")]
        [TestCase("SSSI", "HKLM64", "SSSI", "HKLM32", "HKLM32", TestName = "SSSI in HKLM64 + SSSI in HKLM32 -> HKLM32 first")]
        [TestCase("SSSI", "HKLM64", "SSSI", "HKCU", "HKCU", TestName = "SSSI in HKLM64 + SSSI in HKCU -> HKCU first")]
        public void Order_KeyBeforeHive(string firstKey, string firstHive, string secondKey, string secondHive, string winner)
        {
            string FolderOf(string key, string hive) => @"C:\" + key + "-" + hive + @"\Empire Earth";
            RegistryLocation Location(string key, string hive) =>
                Key((key == "Neo" ? Product.NeoEE : Product.EE).GetGameSettingsKey(Game.EmpireEarth), hive);
            world.SetInstalledFrom(Location(firstKey, firstHive), FolderOf(firstKey, firstHive));
            world.SetInstalledFrom(Location(secondKey, secondHive), FolderOf(secondKey, secondHive));

            IReadOnlyList<InstalledFromEntry> entries = Read();

            Assert.That(entries, Has.Count.EqualTo(2));
            string expected = winner == "Neo" ? FolderOf("Neo", firstKey == "Neo" ? firstHive : secondHive)
                : winner == firstHive ? FolderOf(firstKey, firstHive) : FolderOf(secondKey, secondHive);
            Assert.That(entries[0].EeFolder, Is.EqualTo(expected).IgnoreCase);
        }

        [Test]
        public void TheValues_NameTheEEFolder_AndTheRootIsItsParent()
        {
            world.SetInstalledFrom(InstallationWorld.Hkcu(@"Software\SSSI\Empire Earth"), @"C:\Games\EE");

            InstalledFromEntry entry = Read().Single();

            Assert.That(entry.KeyProduct, Is.SameAs(Product.EE));
            Assert.That(entry.EeFolder, Is.EqualTo(@"C:\GAMES\EE"), "the setup upper-cases the parent, not the folder");
            Assert.That(entry.Root, Is.EqualTo(@"C:\GAMES"));
            Assert.That(entry.AocFolder, Is.Null);
        }

        [Test]
        public void FolderOnADriveRoot_HasTheDriveAsRoot()
        {
            world.SetInstalledFrom(InstallationWorld.Hkcu(@"Software\SSSI\Empire Earth"), @"D:\Empire Earth");
            Assert.That(world.Registry.GetValue(InstallationWorld.Hkcu(@"Software\SSSI\Empire Earth"),
                ContractNames.InstalledFromDirectoryName).Value.StringValue, Is.EqualTo(@"\Empire Earth\"), "ADR 0015");

            InstalledFromEntry entry = Read().Single();

            Assert.That(entry.EeFolder, Is.EqualTo(@"D:\Empire Earth"));
            Assert.That(entry.Root, Is.EqualTo(@"D:\"));
        }

        [Test]
        public void AocFolder_ComesFromTheAocKeyOfTheSameProductAndHive()
        {
            world.SetInstalledFrom(InstallationWorld.Hkcu(@"Software\SSSI\Empire Earth"), @"C:\Games\EE");
            world.SetInstalledFrom(InstallationWorld.Hkcu(@"Software\Mad Doc Software\EE-AOC"), @"C:\Games\AoC");
            world.SetInstalledFrom(InstallationWorld.Hklm32(@"Software\Mad Doc Software\EE-AOC"), @"C:\Other\AoC");
            world.SetInstalledFrom(InstallationWorld.Hkcu(@"Software\Neo\Art of Conquest"), @"C:\Neo\AoC");

            InstalledFromEntry entry = Read().Single();

            Assert.That(entry.AocFolder, Is.EqualTo(@"C:\GAMES\AoC"));
            Assert.That(entry.AocKey.ToString(), Is.EqualTo(@"HKCU\Software\Mad Doc Software\EE-AOC"));
        }

        [Test]
        public void KeyWithoutValues_IsNoCandidate_WithOneLogLine()
        {
            RegistryLocation key = InstallationWorld.Hkcu(@"Software\Neo\Empire Earth");
            world.Registry.Seed(key, "Music Volume", RegistryValue.FromDWord(44));

            Assert.That(Read(), Is.Empty);
            Assert.That(world.LogLinesAbout(key.ToString()), Has.Length.EqualTo(1));
        }

        [TestCase(ContractNames.InstalledFromVolumeName)]
        [TestCase(ContractNames.InstalledFromDirectoryName)]
        public void OnlyOneValue_IsDroppedWithOneLogLine(string valueName)
        {
            RegistryLocation key = InstallationWorld.Hkcu(@"Software\SSSI\Empire Earth");
            world.Registry.Seed(key, valueName, RegistryValue.FromString(valueName == ContractNames.InstalledFromVolumeName ? "C:" : @"\EE\"));
            world.SetInstalledFrom(InstallationWorld.Hklm32(@"Software\SSSI\Empire Earth"), @"C:\Retail\Empire Earth");

            Assert.That(Read().Select(entry => entry.EeFolder), Is.EqualTo(new[] { @"C:\RETAIL\Empire Earth" }));
            Assert.That(world.LogLinesAbout(key.ToString()), Has.Length.EqualTo(1));
        }

        [TestCase("C", @"\GAMES\EE\")]
        [TestCase(@"\\", @"\SERVER\SHARE\EE\")]
        [TestCase("C:", @"\")]
        [TestCase("  ", @"\GAMES\EE\")]
        public void ValuesThatNameNoFolderBelowADrive_AreDroppedWithOneLogLine(string volume, string directory)
        {
            RegistryLocation key = InstallationWorld.Hkcu(@"Software\SSSI\Empire Earth");
            world.Registry.Seed(key, ContractNames.InstalledFromVolumeName, RegistryValue.FromString(volume));
            world.Registry.Seed(key, ContractNames.InstalledFromDirectoryName, RegistryValue.FromString(directory));

            Assert.That(Read(), Is.Empty);
            Assert.That(world.LogLinesAbout(key.ToString()), Has.Length.EqualTo(1));
        }

        [Test]
        public void UnreadableKey_IsDroppedWithOneLogLine()
        {
            RegistryLocation key = InstallationWorld.Hkcu(@"Software\Neo\Empire Earth");
            world.SetInstalledFrom(key, @"C:\Neo\Empire Earth");
            world.SetInstalledFrom(InstallationWorld.Hkcu(@"Software\SSSI\Empire Earth"), @"C:\EE\Empire Earth");
            world.Registry.SetFault(key, RegistryStatus.AccessDenied);

            Assert.That(Read().Select(entry => entry.KeyProduct), Is.EqualTo(new[] { Product.EE }));
            Assert.That(world.LogLinesAbout(key.ToString()), Has.Length.EqualTo(1));
        }

        [Test]
        public void SameFolderInSeveralHives_IsOneEntry()
        {
            world.SetInstalledFrom(InstallationWorld.Hkcu(@"Software\SSSI\Empire Earth"), @"C:\Sierra\Empire Earth");
            world.SetInstalledFrom(InstallationWorld.Hklm32(@"Software\SSSI\Empire Earth"), @"C:\Sierra\Empire Earth");

            Assert.That(Read().Single().Key.ToString(), Is.EqualTo(@"HKCU\Software\SSSI\Empire Earth"));
        }

        [Test]
        public void ThirtyTwoBitWindows_ReadsTheValuesOnce()
        {
            world = new InstallationWorld(is32BitWindows: true);
            world.SetInstalledFrom(InstallationWorld.Hklm64(@"Software\SSSI\Empire Earth"), @"C:\Sierra\Empire Earth");

            Assert.That(Read(), Has.Count.EqualTo(1));
        }

        [Test]
        public void SettingsKeys_AreInTheOrderOfSource4()
        {
            Assert.That(InstalledFromReader.SettingsKeys().Select(key => key.ToString()), Is.EqualTo(new[]
            {
                @"HKCU\Software\Neo\Empire Earth", @"HKLM32\Software\Neo\Empire Earth", @"HKLM64\Software\Neo\Empire Earth",
                @"HKCU\Software\SSSI\Empire Earth", @"HKLM32\Software\SSSI\Empire Earth", @"HKLM64\Software\SSSI\Empire Earth"
            }));
        }
    }
}

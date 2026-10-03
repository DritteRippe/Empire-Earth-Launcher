using System.Collections.Generic;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.RealMachineTests.Harness;
using Empire_Earth_Launcher.Tests.Fakes;
using NUnit.Framework;

namespace Empire_Earth_Launcher.RealMachineTests.SelfTest
{
    /// <summary>
    /// The snapshot before and after the checks: every protected key, value and file that changes is named, the CD keys without
    /// a name or value, the uninstall keys of other installers are not compared, and a symbolic registry link is not followed.
    /// </summary>
    [TestFixture]
    [Category(RealMachineCategories.SelfTest)]
    public class MachineSnapshotTests
    {
        private const string Root = @"C:\Games\EE";

        private static readonly RegistryLocation Uninstall = RegistryLocation.LocalMachine64(ContractNames.UninstallKey);

        private InMemoryRegistry registry;
        private InMemoryFileSystem fileSystem;

        [SetUp]
        public void SetUp()
        {
            registry = new InMemoryRegistry();
            fileSystem = new InMemoryFileSystem();
            registry.Seed(RegistryLocation.CurrentUser(ContractNames.CdKeysKey), "CI-Dummy", RegistryValue.FromString("NOT-A-KEY-0000"));
            registry.Seed(Uninstall.Child("{4C0B46D8-E7EB-4B95-97D4-A578D9B914C6}_is1"), "DisplayVersion", RegistryValue.FromString("2.0.0.0"));
            registry.Seed(Uninstall.Child("Some Browser"), "DisplayVersion", RegistryValue.FromString("1"));
            registry.Seed(RegistryLocation.LocalMachine32(@"Software\Neo\Empire Earth"), "Installed From Volume", RegistryValue.FromString("C:"));
            fileSystem.AddFile(Root + @"\Empire Earth\Empire Earth.exe", "exe");
            fileSystem.AddFile(Root + @"\Empire Earth\Data\a.dat", "data");
        }

        private MachineSnapshot Take()
        {
            return MachineSnapshot.Take(registry, fileSystem, new[] { Root, @"C:\Gone" });
        }

        [Test]
        public void NothingChanged_NoDifference()
        {
            MachineSnapshot before = Take();

            Assert.That(before.DifferencesTo(Take()), Is.Empty);
            Assert.That(before.Count, Is.GreaterThan(10));
        }

        [Test]
        public void EveryChange_IsNamed_WithoutItsData()
        {
            MachineSnapshot before = Take();
            registry.Seed(Uninstall.Child("{4C0B46D8-E7EB-4B95-97D4-A578D9B914C6}_is1"), "DisplayVersion", RegistryValue.FromString("1.7.2"));
            registry.Seed(Uninstall.Child("{A24FCC7A-5491-4FEA-837B-4E4430C349DA}_is1"), "Publisher", RegistryValue.FromString("x"));
            registry.DeleteSubKeyTree(RegistryLocation.LocalMachine32(@"Software\Neo\Empire Earth"));
            fileSystem.AddFile(Root + @"\Empire Earth\Data\a.dat", "changed");
            fileSystem.DeleteFile(Root + @"\Empire Earth\Empire Earth.exe");
            fileSystem.AddFile(Root + @"\Empire Earth\new.txt", "new");
            fileSystem.AddFile(@"C:\Gone\x.txt", "x");

            IReadOnlyList<string> differences = before.DifferencesTo(Take());

            Assert.That(differences, Is.EquivalentTo(new[]
            {
                @"HKLM64\Software\Microsoft\Windows\CurrentVersion\Uninstall\{4C0B46D8-E7EB-4B95-97D4-A578D9B914C6}_is1\@DisplayVersion: changed",
                @"HKLM64\Software\Microsoft\Windows\CurrentVersion\Uninstall\{A24FCC7A-5491-4FEA-837B-4E4430C349DA}_is1\: added",
                @"HKLM64\Software\Microsoft\Windows\CurrentVersion\Uninstall\{A24FCC7A-5491-4FEA-837B-4E4430C349DA}_is1\@Publisher: added",
                @"HKLM32\Software\Neo\Empire Earth: changed",
                @"HKLM32\Software\Neo\Empire Earth\@Installed From Volume: removed",
                @"C:\Games\EE\Empire Earth\Data\a.dat: changed",
                @"C:\Games\EE\Empire Earth\Empire Earth.exe: removed",
                @"C:\Games\EE\Empire Earth\new.txt: added",
                @"C:\Gone: changed",
                @"C:\Gone\x.txt: added"
            }));
        }

        [Test]
        public void ChangedCdKeys_AreReportedWithoutNamesOrValues()
        {
            MachineSnapshot before = Take();
            registry.Seed(RegistryLocation.CurrentUser(ContractNames.CdKeysKey), "CI-Dummy", RegistryValue.FromString("NOT-A-KEY-0001"));
            registry.Seed(RegistryLocation.LocalMachine64(ContractNames.CdKeysKey), "Other", RegistryValue.FromString("NOT-A-KEY-0002"));

            Assert.That(before.DifferencesTo(Take()), Is.EqualTo(new[]
            {
                @"HKCU\Software\Sierra\CDKeys: changed (1 entries; names and values are not shown)",
                @"HKLM64\Software\Sierra\CDKeys: changed (2 entries; names and values are not shown)"
            }));
        }

        [Test]
        public void OtherUninstallKeys_AreNotCompared()
        {
            MachineSnapshot before = Take();
            registry.Seed(Uninstall.Child("Some Browser"), "DisplayVersion", RegistryValue.FromString("2"));
            registry.Seed(Uninstall.Child("Another Updater"), "DisplayVersion", RegistryValue.FromString("1"));

            Assert.That(before.DifferencesTo(Take()), Is.Empty);
        }

        [Test]
        public void ASymbolicLink_IsNotFollowed()
        {
            RegistryLocation link = RegistryLocation.CurrentUser(ContractNames.InstallRecordsKey + @"\Loop");
            registry.SeedLink(link);
            registry.Seed(link, "Target", RegistryValue.FromString("a"));
            MachineSnapshot before = Take();
            registry.Seed(link, "Target", RegistryValue.FromString("b"));

            Assert.That(before.DifferencesTo(Take()), Is.Empty);
        }
    }
}

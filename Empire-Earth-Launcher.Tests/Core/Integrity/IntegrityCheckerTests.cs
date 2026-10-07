using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.GameSettings;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Integrity;
using Empire_Earth_Launcher.Core.Logging;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Core.Play;
using Empire_Earth_Launcher.Tests.Fakes;
using Empire_Earth_Launcher.Tests.TestSupport;
using Microsoft.Win32;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.Integrity
{
    /// <summary>
    /// <see cref="IntegrityChecker"/> with an in-memory computer (contract 2.1 to 2.6, 4.2; ADR 0012, ADR 0016 plan review):
    /// the state table of 2.5 with "the worst finding wins", the uninstall key rule, the texts' inputs for community, legacy
    /// and foreign installations, NeoEE wording (O2), two products in one root (O11), the counted cost of the quick and full
    /// check, the setup that starts during a check, and that the check never changes anything and never blocks a start.
    /// Every file content is synthetic (<c>sample-&lt;n&gt;</c>).
    /// </summary>
    [TestFixture]
    public class IntegrityCheckerTests
    {
        private const string Root = @"C:\Program Files (x86)\Neo Empire Earth";
        private const string DataFolder = Root + @"\_setupdata_NeoEE";
        private const string InstallIni = DataFolder + @"\install.ini";
        private const string Manifest = DataFolder + @"\files.sha256";

        private const string EeProgram = "Empire Earth/Empire Earth.exe";
        private const string NeoDll = "Empire Earth/neoee.dll";
        private const string AocProgram = "Empire Earth - The Art of Conquest/EE-AOC.exe";
        private const string EeData = "Empire Earth/Data/file0001.dat";
        private const string EeSounds = "Empire Earth/Data/Sounds/file0002.wav";
        private const string NeoConfig = "Empire Earth/NeoEE.cfg";
        private const string AocData = "Empire Earth - The Art of Conquest/Data/file0003.dat";

        /// <summary>The files of the standard installation and the sample of their content.</summary>
        private static readonly Tuple<string, int>[] StandardFiles =
        {
            Tuple.Create(EeProgram, 1), Tuple.Create(NeoDll, 2), Tuple.Create(AocProgram, 3), Tuple.Create(EeData, 4),
            Tuple.Create(EeSounds, 5), Tuple.Create(NeoConfig, 6), Tuple.Create(AocData, 7)
        };

        private InstallationWorld world;
        private FakeMutexProbe mutexes;

        [SetUp]
        public void SetUp()
        {
            world = new InstallationWorld();
            mutexes = new FakeMutexProbe();
        }

        private static string Full(string manifestPath, string root = Root)
        {
            return WinPath.Combine(root, manifestPath);
        }

        private IntegrityChecker Checker()
        {
            // The check must only read (contract 2.5): both fakes fail the test at the first change.
            return new IntegrityChecker(new WriteForbiddingFileSystem(world.FileSystem), new WriteForbiddingRegistry(world.Registry),
                mutexes, world.Logger);
        }

        /// <summary>
        /// A NeoEE installation of a setup since v2 (admin): the files with their samples, the manifest with their hashes,
        /// <c>install.ini</c> (with <paramref name="installIniExtra"/> appended), the record and the uninstall key with the
        /// contract version (unless <paramref name="uninstallContractVersion"/> is null).
        /// </summary>
        private void Install(string installIniExtra = null, int? uninstallContractVersion = 1, string mode = "admin",
            Product product = null, string root = Root)
        {
            product = product ?? Product.NeoEE;
            foreach (Tuple<string, int> file in StandardFiles)
                world.FileSystem.AddFile(Full(file.Item1, root), SampleHashes.Content(file.Item2));
            WriteManifest(StandardFiles.Select(file => SampleHashes.Of(file.Item2) + "  " + file.Item1), product, root);
            world.AddInstallInfo(root, product, text: InstallIniText(product, mode, installIniExtra));
            if (mode != "portable")
            {
                RegistryHive hive = mode == "admin" ? RegistryHive.LocalMachine : RegistryHive.CurrentUser;
                RegistryView view = mode == "admin" ? RegistryView.Registry64 : RegistryView.Default;
                world.AddRecord(hive, view, product, root, 1, mode);
                world.AddUninstallKey(hive, view, product, root, contractVersion: uninstallContractVersion);
            }
            // The "Installed From" values of the account that ran the setup: the discovery finds portable installations so.
            world.SetInstalledFrom(InstallationWorld.EmpireEarthKey(product, RegistryHive.CurrentUser, RegistryView.Default),
                Full("Empire Earth", root));
        }

        private static string InstallIniText(Product product, string mode, string extra, int contractVersion = 1)
        {
            return string.Join("\r\n",
                "[Install]",
                "ContractVersion=" + contractVersion,
                "Product=" + product.Id,
                "AppId=" + InstallationWorld.AppIdOf(product),
                "InstallMode=" + mode,
                "GameVersion=2.0.0.5",
                "SetupVersion=2.0.0",
                "Components=game,gameaoc",
                "Tasks=compatibility",
                "Written=2026-10-02 18:04:31",
                "") + (extra ?? string.Empty);
        }

        private void WriteManifest(IEnumerable<string> lines, Product product = null, string root = Root)
        {
            product = product ?? Product.NeoEE;
            string path = WinPath.Combine(root, product.SetupDataFolderName + @"\files.sha256");
            world.FileSystem.AddFile(path, string.Join("\n", lines) + "\n");
        }

        private Installation Discovered(string root = Root)
        {
            Installation installation = InstallationWorld.ByRoot(world.Discover(), root);
            Assert.That(installation, Is.Not.Null, "the discovery finds the installation");
            return installation;
        }

        private IntegrityReport Check(IntegrityCheckKind kind = IntegrityCheckKind.Quick, string root = Root)
        {
            return Checker().Check(Discovered(root), kind);
        }

        // --- OK and the counted cost (ADR 0012 amendment: counted, not timed) ----------------------------------------------

        [Test]
        public void QuickCheck_Ok_OpensEachFileAtMostOnce_AndHashesNoDataFile()
        {
            Install();
            Installation installation = Discovered();
            int opensOfTheDiscovery = world.FileSystem.TotalOpenCount;

            IntegrityReport report = Checker().Check(installation, IntegrityCheckKind.Quick);

            Assert.That(report.State, Is.EqualTo(IntegrityState.Ok), report.ToString());
            Assert.That(report.Findings, Is.Empty);
            Assert.That(report.ListedFiles, Is.EqualTo(7));
            Assert.That(report.HashedFiles, Is.EqualTo(3), "the three code files");
            foreach (string file in new[] { EeProgram, NeoDll, AocProgram })
                Assert.That(world.FileSystem.OpenCount(Full(file)), Is.EqualTo(1), file);
            foreach (string file in new[] { EeData, EeSounds, NeoConfig, AocData })
                Assert.That(world.FileSystem.OpenCount(Full(file)), Is.EqualTo(0), file + " is not opened by the quick check");
            Assert.That(world.FileSystem.TotalOpenCount - opensOfTheDiscovery, Is.EqualTo(5),
                "install.ini, the manifest and the three code files, each once");
            Assert.That(world.FileSystem.OpenStreamCount, Is.EqualTo(0), "every file is closed");
        }

        [Test]
        public void FullCheck_HashesCodeAndData_NeverMutable_EachFileOnce()
        {
            Install();
            Installation installation = Discovered();
            int opensOfTheDiscovery = world.FileSystem.TotalOpenCount;
            var reports = new List<IntegrityProgress>();

            IntegrityReport report = Checker().Check(installation, IntegrityCheckKind.Full, new SynchronousProgress(reports.Add));

            Assert.That(report.State, Is.EqualTo(IntegrityState.Ok), report.ToString());
            Assert.That(report.Kind, Is.EqualTo(IntegrityCheckKind.Full));
            Assert.That(report.HashedFiles, Is.EqualTo(6));
            Assert.That(world.FileSystem.OpenCount(Full(NeoConfig)), Is.EqualTo(0), "mutable files are never hashed");
            Assert.That(StandardFiles.Select(file => world.FileSystem.OpenCount(Full(file.Item1))), Is.All.LessThanOrEqualTo(1));
            Assert.That(world.FileSystem.TotalOpenCount - opensOfTheDiscovery, Is.EqualTo(8));
            Assert.That(reports.Select(progress => progress.CheckedFiles), Is.EqualTo(Enumerable.Range(1, 7)));
            Assert.That(reports.Last().Percent, Is.EqualTo(100));
        }

        // --- The state table of contract 2.5 ---------------------------------------------------------------------------

        [TestCase(EeProgram, IntegrityState.Damaged)]
        [TestCase(NeoDll, IntegrityState.Damaged)]
        [TestCase(EeData, IntegrityState.Incomplete)]
        [TestCase(NeoConfig, IntegrityState.Incomplete)]
        public void Section2_5_AMissingFile(string missing, IntegrityState expected)
        {
            Install();
            Installation installation = Discovered();
            RemoveFile(Full(missing));

            IntegrityReport report = Checker().Check(installation, IntegrityCheckKind.Quick);

            Assert.That(report.State, Is.EqualTo(expected), report.ToString());
            IntegrityFinding finding = report.Findings.Single();
            Assert.That(finding.Path, Is.EqualTo(missing));
            Assert.That(finding.FullPath, Is.EqualTo(Full(missing)));
            Assert.That(finding.Kind, Is.EqualTo(FindingKind.Missing));
            Assert.That(report.SeriousFindings, Is.EqualTo(new[] { finding }));
            Assert.That(report.OffersRepair, Is.True, "Damaged and Incomplete offer the repair (contract 2.5)");
        }

        [TestCase(EeProgram, IntegrityCheckKind.Quick, IntegrityState.Damaged)]
        [TestCase(AocProgram, IntegrityCheckKind.Full, IntegrityState.Damaged)]
        [TestCase(EeData, IntegrityCheckKind.Full, IntegrityState.Modified)]
        [TestCase(EeData, IntegrityCheckKind.Quick, IntegrityState.Ok)]
        [TestCase(NeoConfig, IntegrityCheckKind.Full, IntegrityState.Ok)]
        public void Section2_5_AChangedFile(string changed, IntegrityCheckKind kind, IntegrityState expected)
        {
            Install();
            world.FileSystem.AddFile(Full(changed), SampleHashes.Content(42));

            IntegrityReport report = Check(kind);

            Assert.That(report.State, Is.EqualTo(expected), report.ToString());
            if (expected == IntegrityState.Ok)
            {
                Assert.That(report.Findings, Is.Empty, "a changed mutable file is not reported; the quick check hashes no data file");
                return;
            }
            IntegrityFinding finding = report.Findings.Single();
            Assert.That(finding.Kind, Is.EqualTo(FindingKind.HashDiffers));
            Assert.That(finding.ExpectedHash, Is.EqualTo(SampleHashes.Of(StandardFiles.Single(file => file.Item1 == changed).Item2)));
            Assert.That(finding.ActualHash, Is.EqualTo(SampleHashes.Of(42)));
            Assert.That(report.OffersRepair, Is.EqualTo(expected == IntegrityState.Damaged), "Modified is informative only");
        }

        [TestCase("Empire Earth/DDraw.dll", IntegrityState.Damaged)]
        [TestCase("Empire Earth/Data/file0009.dat", IntegrityState.Incomplete)]
        [TestCase("Empire Earth/dxwrapper.ini", IntegrityState.Incomplete)]
        public void Section2_5_AFileOfMissingAfterInstall(string path, IntegrityState expected)
        {
            Install("\r\n[MissingAfterInstall]\r\n1=" + path + "\r\n");

            IntegrityReport report = Check();

            Assert.That(report.State, Is.EqualTo(expected), report.ToString());
            IntegrityFinding finding = report.Findings.Single();
            Assert.That(finding.Kind, Is.EqualTo(FindingKind.MissingAfterInstall));
            Assert.That(finding.Path, Is.EqualTo(path));
            Assert.That(finding.ExpectedHash, Is.Null, "not in the manifest");
            Assert.That(world.FileSystem.OpenCount(Full(path)), Is.EqualTo(0), "such a file is never opened");
        }

        [Test]
        public void Section2_5_TheWorstFindingWins()
        {
            Install("\r\n[MissingAfterInstall]\r\n1=Empire Earth/Data/file0009.dat\r\n");
            Installation installation = Discovered();
            world.FileSystem.AddFile(Full(EeData), SampleHashes.Content(42));
            RemoveFile(Full(AocData));

            IntegrityReport incomplete = Checker().Check(installation, IntegrityCheckKind.Full);
            RemoveFile(Full(NeoDll));
            IntegrityReport damaged = Checker().Check(installation, IntegrityCheckKind.Full);

            Assert.That(incomplete.State, Is.EqualTo(IntegrityState.Incomplete), "Incomplete before Modified");
            Assert.That(incomplete.Findings.Count, Is.EqualTo(3));
            Assert.That(damaged.State, Is.EqualTo(IntegrityState.Damaged), "Damaged before Incomplete and Modified");
            Assert.That(damaged.SeriousFindings.First().Path, Is.EqualTo(NeoDll), "the damaged file first");
            Assert.That(damaged.SeriousFindings.Select(finding => finding.Path), Does.Not.Contain(EeData), "Modified is not serious");
            Assert.That(IntegrityReport.Worst(new[] { IntegrityState.Modified, IntegrityState.Ok }), Is.EqualTo(IntegrityState.Modified));
            Assert.That(IntegrityReport.Worst(new IntegrityState[0]), Is.EqualTo(IntegrityState.Ok));
        }

        [Test]
        public void Section2_5_NoManifest_IsUnknown_WithTheRepair()
        {
            Install();
            Installation installation = Discovered();
            RemoveFile(Manifest);

            IntegrityReport report = Checker().Check(installation, IntegrityCheckKind.Quick);

            Assert.That(report.State, Is.EqualTo(IntegrityState.Unknown));
            Assert.That(report.UnknownReason, Is.EqualTo(UnknownReason.NoManifest));
            Assert.That(report.OffersRepair, Is.True, "community: the last setup run did not finish");
            Assert.That(report.Findings, Is.Empty);
        }

        [Test]
        public void Section2_5_NoInstallIni_IsUnknown_WithTheRepair()
        {
            Install();
            RemoveFile(InstallIni);
            Installation installation = Discovered();
            Assert.That(installation.Kind, Is.EqualTo(InstallationKind.Community), "the record makes it community (ARCHITECTURE 4.1)");

            IntegrityReport report = Checker().Check(installation, IntegrityCheckKind.Quick);

            Assert.That(report.UnknownReason, Is.EqualTo(UnknownReason.NoInstallInfo));
            Assert.That(report.OffersRepair, Is.True);
            Assert.That(world.FileSystem.OpenCount(Manifest), Is.EqualTo(0));
        }

        [TestCase("not a manifest")]
        [TestCase("{0}  ../outside.dll")]
        [TestCase("{0}  C:/Windows/x.dll")]
        public void Section2_5_AnInvalidManifest_IsUnknown_AndNoFileIsOpened(string line)
        {
            Install();
            world.FileSystem.AddFile(@"C:\Program Files (x86)\outside.dll", SampleHashes.Content(8));
            WriteManifest(new[] { SampleHashes.Of(1) + "  " + EeProgram, string.Format(line, SampleHashes.Of(8)) });
            Installation installation = Discovered();
            var touched = new List<string>();
            world.FileSystem.OnFileExists = touched.Add;
            int opens = world.FileSystem.TotalOpenCount;

            IntegrityReport report = Checker().Check(installation, IntegrityCheckKind.Full);

            Assert.That(report.UnknownReason, Is.EqualTo(UnknownReason.InvalidManifest));
            Assert.That(report.Findings, Is.Empty);
            Assert.That(world.FileSystem.TotalOpenCount - opens, Is.EqualTo(2), "only install.ini and the manifest");
            Assert.That(world.FileSystem.OpenCount(@"C:\Program Files (x86)\outside.dll"), Is.EqualTo(0));
            Assert.That(touched.Select(entry => entry.Substring("exists ".Length)), Is.All.StartsWith(DataFolder),
                "nothing outside the setup data folder is touched");
        }

        [Test]
        public void Section2_5_AnUnsafePathInMissingAfterInstall_IsUnknown()
        {
            Install("\r\n[MissingAfterInstall]\r\n1=../../Windows/x.dll\r\n");

            IntegrityReport report = Check();

            Assert.That(report.UnknownReason, Is.EqualTo(UnknownReason.InvalidManifest));
        }

        [Test]
        public void Section2_5_AnUnreadableManifest_IsUnknown()
        {
            Install();
            Installation installation = Discovered();
            world.FileSystem.FailOn(Manifest, FileSystemOperation.Read, FileSystemStatus.AccessDenied);

            IntegrityReport report = Checker().Check(installation, IntegrityCheckKind.Quick);

            Assert.That(report.UnknownReason, Is.EqualTo(UnknownReason.ManifestUnreadable));
            Assert.That(report.OffersRepair, Is.True);
        }

        [Test]
        public void Section5_AHigherContractVersion_IsUnknown_WithoutRepair_AndWithoutReading()
        {
            Install();
            world.AddInstallInfo(Root, Product.NeoEE, text: InstallIniText(Product.NeoEE, "admin", null, 2));
            Installation installation = Discovered();
            Assert.That(installation.HasNewerContract, Is.True);
            int opens = world.FileSystem.TotalOpenCount;

            IntegrityReport report = Checker().Check(installation, IntegrityCheckKind.Quick);

            Assert.That(report.UnknownReason, Is.EqualTo(UnknownReason.NewerContract));
            Assert.That(report.OffersRepair, Is.False, "the launcher is to be updated, not the game (contract 5)");
            Assert.That(world.FileSystem.TotalOpenCount, Is.EqualTo(opens), "contract 5: only root, product and AppId are used");
        }

        [Test]
        public void Section5_AHigherVersionInInstallIniOnly_IsUnknown()
        {
            Install();
            Installation installation = Discovered();
            world.AddInstallInfo(Root, Product.NeoEE, text: InstallIniText(Product.NeoEE, "admin", null, 3));

            Assert.That(Checker().Check(installation, IntegrityCheckKind.Quick).UnknownReason, Is.EqualTo(UnknownReason.NewerContract));
        }

        // --- Contract 2.5: a later run of an older setup (the uninstall key rule) ---------------------------------------------

        [TestCase("admin")]
        [TestCase("user")]
        public void Section2_5_UninstallKeyWithoutTheValue_SameRoot_IsUnknown(string mode)
        {
            Install(uninstallContractVersion: null, mode: mode);

            IntegrityReport report = Check();

            Assert.That(report.State, Is.EqualTo(IntegrityState.Unknown));
            Assert.That(report.UnknownReason, Is.EqualTo(UnknownReason.OlderSetupRanAfter));
            Assert.That(report.OffersRepair, Is.True, "run the current setup");
            Assert.That(world.FileSystem.OpenCount(Full(EeProgram)), Is.EqualTo(0), "the manifest no longer describes the files");
            Assert.That(world.LogLinesAbout("lacks " + ContractNames.UninstallContractVersionName).Length, Is.EqualTo(1));
        }

        [Test]
        public void Section2_5_UninstallKeyWithTheValue_IsOk()
        {
            Install(uninstallContractVersion: 1);

            Assert.That(Check().State, Is.EqualTo(IntegrityState.Ok));
        }

        [Test]
        public void Section2_5_TheValueOfAnyType_CountsAsPresent()
        {
            Install(uninstallContractVersion: null);
            world.Registry.Seed(RegistryLocation.LocalMachine64(ContractNames.UninstallKey + @"\{" + InstallationWorld.NeoEEAppId + "}_is1"),
                ContractNames.UninstallContractVersionName, RegistryValue.FromString("1"));

            Assert.That(Check().State, Is.EqualTo(IntegrityState.Ok));
        }

        [Test]
        public void Section2_5_NoUninstallKey_TheRuleDoesNotApply()
        {
            Install(mode: "portable");

            IntegrityReport report = Check();

            Assert.That(report.State, Is.EqualTo(IntegrityState.Ok), "portable: not detectable");
        }

        [Test]
        public void Section2_5_UninstallKeyMissing_ForAnAdminInstallation_TheRuleDoesNotApply()
        {
            Install(mode: "portable");
            // install.ini says admin, but no uninstall key exists (removed by hand, another computer's copy).
            world.AddInstallInfo(Root, Product.NeoEE, text: InstallIniText(Product.NeoEE, "admin", null));

            Assert.That(Check().State, Is.EqualTo(IntegrityState.Ok));
        }

        [Test]
        public void Section2_5_UninstallKeyOfAnotherRootWithoutTheValue_TheRuleDoesNotApply()
        {
            Install(mode: "portable");
            world.AddInstallInfo(Root, Product.NeoEE, text: InstallIniText(Product.NeoEE, "admin", null));
            world.AddUninstallKey(RegistryHive.LocalMachine, RegistryView.Registry64, Product.NeoEE, @"D:\Games\Neo Empire Earth",
                contractVersion: null);

            Assert.That(Check().State, Is.EqualTo(IntegrityState.Ok), "another root: the rule does not apply");
        }

        [Test]
        public void Section2_5_TheUninstallKeyRule_On32BitWindows()
        {
            world = new InstallationWorld(is32BitWindows: true);
            Install(uninstallContractVersion: null);

            Assert.That(Check().UnknownReason, Is.EqualTo(UnknownReason.OlderSetupRanAfter), "HKLM64 is the only view there");
        }

        // --- Kinds, NeoEE wording, two products -----------------------------------------------------------------------

        [Test]
        public void Section2_5_Legacy_IsUnknown_OnlyAsABadge_WithoutReadingAnything()
        {
            world.AddLegacyInstallation(Root, Product.NeoEE);
            Installation installation = Discovered();
            int opens = world.FileSystem.TotalOpenCount;

            IntegrityReport report = Checker().Check(installation, IntegrityCheckKind.Full);

            Assert.That(installation.Kind, Is.EqualTo(InstallationKind.CommunityLegacy));
            Assert.That(report.State, Is.EqualTo(IntegrityState.Unknown));
            Assert.That(report.UnknownReason, Is.EqualTo(UnknownReason.LegacySetup));
            Assert.That(report.OffersRepair, Is.False, "legacy: a badge, never a dialog");
            Assert.That(world.FileSystem.TotalOpenCount, Is.EqualTo(opens));
        }

        [Test]
        public void Section2_5_Foreign_IsNotChecked_AndSaysNothing()
        {
            world.AddForeignInstallation(@"C:\Games\EE");
            Installation installation = Discovered(@"C:\Games");
            int opens = world.FileSystem.TotalOpenCount;
            int warnings = world.Logger.MessagesOf(LogLevel.Warning).Count;

            IntegrityReport report = Checker().Check(installation, IntegrityCheckKind.Full);

            Assert.That(report.State, Is.EqualTo(IntegrityState.NotChecked));
            Assert.That(report.OffersRepair, Is.False);
            Assert.That(world.FileSystem.TotalOpenCount, Is.EqualTo(opens));
            Assert.That(world.Logger.MessagesOf(LogLevel.Warning).Count, Is.EqualTo(warnings), "no message (contract 2.5)");
        }

        [Test]
        public void Section2_6_NeoEE_ACodeFileWithAnotherHash_IsWordedNeutrally()
        {
            Install();
            world.FileSystem.AddFile(Full(NeoDll), SampleHashes.Content(43));

            IntegrityReport report = Check();

            Assert.That(report.State, Is.EqualTo(IntegrityState.Damaged), "same state and repair offer");
            Assert.That(report.UsesNeutralWording, Is.True, "O2: the NeoEE updater may replace NeoEE files");
            Assert.That(report.OffersRepair, Is.True);
        }

        [Test]
        public void Section2_6_EE_OrAMissingFile_IsNotWordedNeutrally()
        {
            Install(product: Product.EE, root: @"C:\Program Files (x86)\Empire Earth");
            world.FileSystem.AddFile(Full(EeProgram, @"C:\Program Files (x86)\Empire Earth"), SampleHashes.Content(43));
            Assert.That(Check(root: @"C:\Program Files (x86)\Empire Earth").UsesNeutralWording, Is.False, "EE");

            Install();
            RemoveFile(Full(NeoDll));
            Assert.That(Check().UsesNeutralWording, Is.False, "NeoEE, but the file is missing");
        }

        [Test]
        public void O11_TwoProductsInOneRoot_AreUnreliable()
        {
            Install();
            world.AddInstallInfo(Root, Product.EE, text: InstallIniText(Product.EE, "admin", null));
            world.Clock.Advance(TimeSpan.FromMinutes(1));
            world.AddInstallInfo(Root, Product.NeoEE, text: InstallIniText(Product.NeoEE, "admin", null));

            IntegrityReport report = Check();

            Assert.That(report.Installation.Product, Is.EqualTo(Product.NeoEE), "installed last");
            Assert.That(report.IsUnreliable, Is.True);
            Assert.That(report.Installation.HasUnreliableIntegrity, Is.True);
            Assert.That(report.ToString(), Does.Contain("unreliable (EE shares the root)"));
        }

        // --- Unreadable files ------------------------------------------------------------------------------------------

        [Test]
        public void AnUnreadableFile_IsAFinding_AndTheStateUnknownWithoutOthers()
        {
            Install();
            Installation installation = Discovered();
            world.FileSystem.FailOn(Full(NeoDll), FileSystemOperation.Read, FileSystemStatus.AccessDenied);

            IntegrityReport report = Checker().Check(installation, IntegrityCheckKind.Quick);

            Assert.That(report.State, Is.EqualTo(IntegrityState.Unknown));
            Assert.That(report.UnknownReason, Is.EqualTo(UnknownReason.FilesUnreadable));
            Assert.That(report.OffersRepair, Is.False, "check again, the setup cannot help");
            Assert.That(report.Findings.Single().Kind, Is.EqualTo(FindingKind.Unreadable));

            RemoveFile(Full(EeProgram));
            Assert.That(Checker().Check(installation, IntegrityCheckKind.Quick).State, Is.EqualTo(IntegrityState.Damaged),
                "a damaged file still makes it Damaged");
        }

        // --- Logging ---------------------------------------------------------------------------------------------------

        [Test]
        public void Section2_5_EveryFinding_IsLoggedOnce_WithPathClassExpectedAndActualHash()
        {
            Install("\r\n[MissingAfterInstall]\r\n1=Empire Earth/DDraw.dll\r\n");
            Installation installation = Discovered();
            RemoveFile(Full(EeData));
            world.FileSystem.AddFile(Full(NeoDll), SampleHashes.Content(44));
            world.FileSystem.AddFile(Full(AocData), SampleHashes.Content(45));

            Checker().Check(installation, IntegrityCheckKind.Full);

            string[] findings = world.LogLinesAbout("Integrity finding in " + Root);
            Assert.That(findings.Length, Is.EqualTo(4), string.Join(Environment.NewLine, world.Logger.Messages));
            Assert.That(findings, Has.Exactly(1).EqualTo("Warning: Integrity finding in " + Root + ": " + NeoDll +
                " (code): hash differs; expected " + SampleHashes.Of(2) + ", actual " + SampleHashes.Of(44) + "."));
            Assert.That(findings, Has.Exactly(1).EqualTo("Warning: Integrity finding in " + Root + ": " + EeData +
                " (data): missing; expected " + SampleHashes.Of(4) + ", actual none (missing)."));
            Assert.That(findings, Has.Exactly(1).EqualTo("Info: Integrity finding in " + Root + ": " + AocData +
                " (data): hash differs; expected " + SampleHashes.Of(7) + ", actual " + SampleHashes.Of(45) + "."));
            Assert.That(findings, Has.Exactly(1).EqualTo("Warning: Integrity finding in " + Root +
                ": Empire Earth/DDraw.dll (code): missing after the installation ([MissingAfterInstall] of install.ini); " +
                "expected none (not in the manifest), actual none (missing)."));
            Assert.That(world.LogLinesAbout("Integrity: full check of " + Root + ": Damaged").Length, Is.EqualTo(1));
        }

        // --- Setups (contract 4.2, ADR 0016 plan review) -----------------------------------------------------------------

        [TestCase("NeoEE_Setup")]
        [TestCase("EE_Setup")]
        public void Section4_2_NotStarted_WhileASetupMutexExists(string setupMutex)
        {
            Install();
            Installation installation = Discovered();
            int opens = world.FileSystem.TotalOpenCount;
            mutexes.With(setupMutex);

            IntegrityReport report = Checker().Check(installation, IntegrityCheckKind.Full);

            Assert.That(report.State, Is.EqualTo(IntegrityState.Cancelled));
            Assert.That(report.CancelReason, Is.EqualTo(CancelReason.SetupRunning));
            Assert.That(report.Findings, Is.Empty);
            Assert.That(world.FileSystem.TotalOpenCount, Is.EqualTo(opens), "neither install.ini nor files.sha256 is read");
        }

        [Test]
        public void Section4_2_ASetupThatStartsWhileAFileIsHashed_CancelsTheCheck_AndTheFileIsClosed()
        {
            Install();
            RemoveFile(Full(EeData));
            const string bigFile = "Empire Earth/Data/file0010.dat";
            byte[] big = new byte[3 * 1024 * 1024];
            world.FileSystem.AddFile(Full(bigFile), big);
            WriteManifest(StandardFiles.Select(file => SampleHashes.Of(file.Item2) + "  " + file.Item1)
                .Concat(new[] { SampleHashes.Sha256(big) + "  " + bigFile, SampleHashes.Of(9) + "  Empire Earth/Data/file0011.dat" }));
            Installation installation = Discovered();
            int reads = 0;
            world.FileSystem.OnRead = path =>
            {
                if (path.EndsWith("file0010.dat", StringComparison.Ordinal) && ++reads == 2)
                    mutexes.With("NeoEE_Setup");
            };

            IntegrityReport report = Checker().Check(installation, IntegrityCheckKind.Full);

            Assert.That(report.State, Is.EqualTo(IntegrityState.Cancelled), report.ToString());
            Assert.That(report.CancelReason, Is.EqualTo(CancelReason.SetupRunning));
            Assert.That(report.Findings, Is.Empty, "the missing file found before is dropped");
            Assert.That(world.FileSystem.OpenStreamCount, Is.EqualTo(0), "the file is closed");
            Assert.That(reads, Is.LessThan(48), "stopped within the file (3 MiB are 48 reads)");
            Assert.That(world.FileSystem.OpenCount(Full("Empire Earth/Data/file0011.dat")), Is.EqualTo(0), "no later file is opened");
            Assert.That(world.LogLinesAbout("was cancelled").Length, Is.EqualTo(1));
            Assert.That(world.LogLinesAbout("Integrity finding").Length, Is.EqualTo(0), "no finding is logged");
        }

        [Test]
        public void ACancelledToken_EndsTheCheck_AndTheFileIsClosed()
        {
            Install();
            Installation installation = Discovered();
            var cancel = new CancellationTokenSource();
            world.FileSystem.OnRead = path =>
            {
                if (path.EndsWith("neoee.dll", StringComparison.Ordinal))
                    cancel.Cancel();
            };

            IntegrityReport report = Checker().Check(installation, IntegrityCheckKind.Full, null, cancel.Token);

            Assert.That(report.State, Is.EqualTo(IntegrityState.Cancelled));
            Assert.That(report.CancelReason, Is.EqualTo(CancelReason.Requested));
            Assert.That(world.FileSystem.OpenStreamCount, Is.EqualTo(0));
            Assert.That(world.FileSystem.OpenCount(Full(AocProgram)), Is.EqualTo(0));
        }

        [Test]
        public async Task CheckAsync_WithACancelledToken_ReturnsACancelledReport()
        {
            Install();
            Installation installation = Discovered();

            IntegrityReport report = await Checker().CheckAsync(installation, IntegrityCheckKind.Quick, null, new CancellationToken(true));

            Assert.That(report.State, Is.EqualTo(IntegrityState.Cancelled));
            Assert.That(report.CancelReason, Is.EqualTo(CancelReason.Requested));
        }

        // --- Never blocks, never changes -------------------------------------------------------------------------------

        [Test]
        public async Task CheckAsync_RunsInTheBackground_AndNeverBlocksAGameStart()
        {
            Install();
            Installation installation = Discovered();
            using (var gate = new ManualResetEventSlim(false))
            {
                world.FileSystem.OnRead = path =>
                {
                    if (path.EndsWith("EE-AOC.exe", StringComparison.Ordinal))
                        gate.Wait(TimeSpan.FromSeconds(10));
                };

                Task<IntegrityReport> check = Checker().CheckAsync(installation, IntegrityCheckKind.Quick);
                var starter = new GameStarter(new RunningGameDetector(mutexes, new FakeProcessList()), world.FileSystem,
                    new NothingToPrepare(), new FakeProcessStarter(), world.Logger);
                StartResult start = starter.Start(installation, Game.EmpireEarth, false);

                Assert.That(start.IsStarted, Is.True, "the start does not wait for the check");
                Assert.That(check.IsCompleted, Is.False, "the check still hashes");
                gate.Set();
                IntegrityReport report = await check;
                Assert.That(report.State, Is.EqualTo(IntegrityState.Ok));
            }
        }

        [Test]
        public void ADamagedInstallation_StillStarts()
        {
            Install();
            Installation installation = Discovered();
            world.FileSystem.AddFile(Full(NeoDll), SampleHashes.Content(46));
            Assert.That(Checker().Check(installation, IntegrityCheckKind.Quick).State, Is.EqualTo(IntegrityState.Damaged));

            var starter = new GameStarter(new RunningGameDetector(mutexes, new FakeProcessList()), world.FileSystem,
                new NothingToPrepare(), new FakeProcessStarter(), world.Logger);

            Assert.That(starter.Start(installation, Game.EmpireEarth, false).IsStarted, Is.True,
                "contract 2.5: a finding never refuses a start");
        }

        [Test]
        public void TheCheck_NeverWritesDeletesOrMovesAFile()
        {
            Install("\r\n[MissingAfterInstall]\r\n1=Empire Earth/DDraw.dll\r\n");
            Installation installation = Discovered();
            RemoveFile(Full(EeData));
            world.FileSystem.AddFile(Full(NeoDll), SampleHashes.Content(47));
            world.FileSystem.AddFile(Full(AocData), SampleHashes.Content(48));
            IReadOnlyList<string> before = world.FileSystem.AllFiles;
            byte[] manifestBefore = world.FileSystem.GetContent(Manifest);

            // WriteForbiddingFileSystem and WriteForbiddingRegistry fail at the first change.
            IntegrityReport report = Checker().Check(installation, IntegrityCheckKind.Full);

            Assert.That(report.Findings.Count, Is.EqualTo(4));
            Assert.That(world.FileSystem.AllFiles, Is.EqualTo(before));
            Assert.That(world.FileSystem.GetContent(Manifest), Is.EqualTo(manifestBefore));
            Assert.That(world.Registry.Changes, Is.Empty);
        }

        private void RemoveFile(string path)
        {
            Assert.That(world.FileSystem.DeleteFile(path).IsOk, Is.True, path);
        }

        /// <summary>Calls the handler at once (<see cref="Progress{T}"/> would post to a synchronization context).</summary>
        private sealed class SynchronousProgress : IProgress<IntegrityProgress>
        {
            private readonly Action<IntegrityProgress> handler;

            public SynchronousProgress(Action<IntegrityProgress> handler)
            {
                this.handler = handler;
            }

            public void Report(IntegrityProgress value)
            {
                handler(value);
            }
        }

        /// <summary>A start without class S and first run (they are tested by the game settings tests).</summary>
        private sealed class NothingToPrepare : IGameStartPreparation
        {
            public GameSettingsResult SynchronizeInstalledFrom(Installation installation, Game game)
            {
                return new GameSettingsResult(GameSettingsOutcome.Done, null, null, null, null);
            }

            public DefaultsAtStart ApplyDefaultsIfNeeded(Installation installation, Game game, out DisplayQuestion question)
            {
                question = null;
                return DefaultsAtStart.None;
            }
        }
    }
}

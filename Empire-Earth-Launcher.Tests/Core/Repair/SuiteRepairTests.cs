using System;
using System.ComponentModel;
using System.Linq;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Integrity;
using Empire_Earth_Launcher.Core.Logging;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Core.Repair;
using Empire_Earth_Launcher.Tests.Fakes;
using Empire_Earth_Launcher.Tests.TestSupport;
using Microsoft.Win32;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.Repair
{
    /// <summary>
    /// The advice of contract 4.4 with the suite (revision 4): <see cref="RepairAdvice"/> chooses the suite step only when the
    /// suite record lists the product and its <c>SourceDir</c> exists (<see cref="SuiteRepairLocator"/>); the folder is opened in
    /// the Explorer and never a program of it. Since launcher 1.1.1 an installation of the suite is never sent to the product
    /// setup of the community website (its page leads to another build with the same AppId, which replaces the installation of
    /// the package): the second option, and without the folder the only one, is the release page of the package.
    /// </summary>
    [TestFixture]
    public class SuiteRepairTests
    {
        private const string NeoRoot = @"C:\Program Files (x86)\Neo Empire Earth";
        private const string EERoot = @"C:\Program Files (x86)\Empire Earth";
        private const string Source = @"C:\Users\Anna\Downloads\Empire Earth Community";

        private InstallationWorld world;

        [SetUp]
        public void SetUp()
        {
            world = new InstallationWorld();
        }

        private static Installation Community(Product product, string root, InstallationKind kind = InstallationKind.Community,
            InstallMode mode = InstallMode.Admin)
        {
            return new Installation(product, root, root + @"\Empire Earth", root + @"\Empire Earth - The Art of Conquest", kind, mode,
                new[] { InstallationSource.RegistryRecord });
        }

        private void SeedRecord(string products, string source = Source, string eeAppId = null, string neoEeAppId = null)
        {
            RegistryLocation key = InstallationWorld.Hklm64(@"Software\Empire Earth Community\Suite");
            world.Registry.SeedKey(key);
            world.Registry.Seed(key, "Products", RegistryValue.FromString(products));
            if (source != null)
                world.Registry.Seed(key, "SourceDir", RegistryValue.FromString(source));
            if (eeAppId != null)
                world.Registry.Seed(key, "EEAppId", RegistryValue.FromString(eeAppId));
            if (neoEeAppId != null)
                world.Registry.Seed(key, "NeoEEAppId", RegistryValue.FromString(neoEeAppId));
        }

        private SuiteRepairLocator Locator()
        {
            return new SuiteRepairLocator(new SuiteRecordReader(new WriteForbiddingRegistry(world.Registry), world.Logger),
                world.FileSystem, world.Logger);
        }

        /// <summary>The package of the suite as the locator gives it, with the folder <paramref name="folder"/>.</summary>
        private static SuitePackage Package(string folder = Source)
        {
            return new SuitePackage(folder);
        }

        /// <summary>The pages of the product setups on the community website (contract 4.3).</summary>
        private static readonly string[] ProductSetupPages =
        {
            SetupDownloadPage.EmpireEarth, SetupDownloadPage.NeoEE, SetupDownloadPage.General
        };

        // --- The locator ------------------------------------------------------------------------------------------------

        [Test]
        public void TheRecordListsTheProduct_AndTheFolderExists_GivesTheFolder()
        {
            SeedRecord("EE,NeoEE");
            world.FileSystem.AddDirectory(Source);

            Assert.That(Locator().FolderFor(Community(Product.NeoEE, NeoRoot)), Is.EqualTo(Source));
            Assert.That(Locator().FolderFor(Community(Product.EE, EERoot)), Is.EqualTo(Source));
        }

        [Test]
        public void TheRecordDoesNotListTheProduct_GivesNothing()
        {
            SeedRecord("EE");
            world.FileSystem.AddDirectory(Source);

            Assert.That(Locator().FolderFor(Community(Product.NeoEE, NeoRoot)), Is.Null);
            Assert.That(Locator().FolderFor(Community(Product.EE, EERoot)), Is.EqualTo(Source));
        }

        [Test]
        public void TheFolderIsGone_GivesNothing()
        {
            SeedRecord("EE,NeoEE");

            Assert.That(Locator().FolderFor(Community(Product.NeoEE, NeoRoot)), Is.Null);
        }

        [TestCase(Source, Description = "the unpacked folder was deleted")]
        [TestCase(null, Description = "the record has no SourceDir")]
        [TestCase(@"\\server\share\Empire Earth Community", Description = "a network folder, not probed on the UI thread")]
        public void TheRecordListsTheProduct_WithoutItsFolder_GivesThePackageWithoutAFolder(string source)
        {
            SeedRecord("EE,NeoEE", source);

            SuitePackage package = Locator().PackageFor(Community(Product.EE, EERoot));

            Assert.That(package, Is.Not.Null, "the suite installed EE, also when its folder is gone");
            Assert.That(package.Folder, Is.Null);
        }

        [Test]
        public void TheRecordListsTheProduct_AndTheFolderExists_GivesThePackageWithTheFolder()
        {
            SeedRecord("EE,NeoEE");
            world.FileSystem.AddDirectory(Source);

            Assert.That(Locator().PackageFor(Community(Product.NeoEE, NeoRoot)).Folder, Is.EqualTo(Source));
        }

        [Test]
        public void NoPackage_WithoutARecord_ForAProductItDoesNotList_OrForAnInstallationTheSuiteDidNotMake()
        {
            Assert.That(Locator().PackageFor(Community(Product.EE, EERoot)), Is.Null, "no record: an installation of another setup");

            SeedRecord("EE", eeAppId: "AAAAAAAA-0000-0000-0000-000000000001");
            Installation other = Community(Product.EE, @"D:\Games\Empire Earth");
            other.AppId = "BBBBBBBB-0000-0000-0000-000000000001";

            Assert.That(Locator().PackageFor(Community(Product.NeoEE, NeoRoot)), Is.Null, "the record does not list NeoEE");
            Assert.That(Locator().PackageFor(other), Is.Null, "another AppId than the suite embeds");
            Assert.That(Locator().PackageFor(Community(Product.EE, EERoot, InstallationKind.Community, InstallMode.User)), Is.Null,
                "the suite installs for all users only");
            Assert.That(Locator().PackageFor(Community(Product.EE, EERoot, InstallationKind.Foreign, InstallMode.Unknown)), Is.Null,
                "a foreign installation");
        }

        [Test]
        public void NoRecord_GivesNothing_AndLogsNothing()
        {
            Assert.That(Locator().FolderFor(Community(Product.NeoEE, NeoRoot)), Is.Null);
            Assert.That(world.Logger.Messages, Is.Empty);
        }

        [Test]
        public void AForeignInstallation_GivesNothing_EvenIfTheRecordListsTheProduct()
        {
            SeedRecord("EE,NeoEE");
            world.FileSystem.AddDirectory(Source);

            Assert.That(Locator().FolderFor(Community(Product.EE, EERoot, InstallationKind.Foreign, InstallMode.Unknown)), Is.Null);
        }

        [TestCase(InstallMode.User)]
        [TestCase(InstallMode.Portable)]
        [TestCase(InstallMode.Unknown)]
        public void AnInstallationThatIsNotInAdminMode_GivesNothing_TheSuiteRepairsOnlyTheHklmInstallation(InstallMode mode)
        {
            SeedRecord("EE,NeoEE");
            world.FileSystem.AddDirectory(Source);

            Assert.That(Locator().FolderFor(Community(Product.EE, EERoot, InstallationKind.Community, mode)), Is.Null);
        }

        [Test]
        public void AnotherAppIdThanTheSuiteEmbeds_GivesNothing_AMatchingOrUnknownOneGivesTheFolder()
        {
            SeedRecord("EE,NeoEE", eeAppId: "AAAAAAAA-0000-0000-0000-000000000001", neoEeAppId: "AAAAAAAA-0000-0000-0000-000000000002");
            world.FileSystem.AddDirectory(Source);
            Installation other = Community(Product.EE, @"D:\Games\Empire Earth");
            other.AppId = "BBBBBBBB-0000-0000-0000-000000000001";
            Installation same = Community(Product.EE, EERoot);
            same.AppId = "{aaaaaaaa-0000-0000-0000-000000000001}";
            Installation unknown = Community(Product.NeoEE, NeoRoot);

            Assert.That(Locator().FolderFor(other), Is.Null, "a second installation in another folder");
            Assert.That(Locator().FolderFor(same), Is.EqualTo(Source), "case and braces do not matter");
            Assert.That(Locator().FolderFor(unknown), Is.EqualTo(Source), "without an AppId only the mode counts");
        }

        [Test]
        public void ANetworkSourceFolder_GivesNothing_ItIsNotProbedOnTheUiThread()
        {
            const string unc = @"\\server\share\Empire Earth Community";
            SeedRecord("EE,NeoEE", unc);
            world.FileSystem.AddDirectory(unc);

            Assert.That(Locator().FolderFor(Community(Product.EE, EERoot)), Is.Null);
        }

        [Test]
        public void TheLocator_LogsAChangeOnce()
        {
            SeedRecord("EE");
            world.FileSystem.AddDirectory(Source);
            SuiteRepairLocator locator = Locator();

            locator.FolderFor(Community(Product.EE, EERoot));
            locator.FolderFor(Community(Product.EE, EERoot));
            locator.FolderFor(Community(Product.NeoEE, NeoRoot));

            Assert.That(world.Logger.MessagesOf(LogLevel.Info), Has.Count.EqualTo(2));
            Assert.That(world.Logger.Messages[0], Does.Contain("the EE installation can be repaired by the suite from " + Source));
            Assert.That(world.Logger.Messages[1], Does.Contain("no folder to repair the NeoEE installation from"));
        }

        [Test]
        public void TheLocator_LogsThatTheFolderIsGone()
        {
            SeedRecord("EE");

            Locator().PackageFor(Community(Product.EE, EERoot));

            Assert.That(world.Logger.MessagesOf(LogLevel.Info).Single(), Does.Contain(
                "the suite installed the EE installation, but there is no folder to run it from again: the advice is to download the package again."));
        }

        [Test]
        public void TheLocator_ChecksItsArguments()
        {
            var reader = new SuiteRecordReader(world.Registry, world.Logger);
            Assert.That(() => new SuiteRepairLocator(null, world.FileSystem, world.Logger), Throws.ArgumentNullException);
            Assert.That(() => new SuiteRepairLocator(reader, null, world.Logger), Throws.ArgumentNullException);
            Assert.That(() => new SuiteRepairLocator(reader, world.FileSystem, null), Throws.ArgumentNullException);
            Assert.That(() => Locator().FolderFor(null), Throws.ArgumentNullException);
            Assert.That(() => Locator().PackageFor(null), Throws.ArgumentNullException);
        }

        // --- The advice -------------------------------------------------------------------------------------------------

        [Test]
        public void Contract_4_4_WithTheSuiteFolder_TheSuiteStepReplacesTheDownloadStep_AndThePackageStaysTheSecondOption()
        {
            RepairAdvice advice = RepairAdvice.For(Community(Product.NeoEE, NeoRoot), RepairReason.Requested, suite: Package());

            Assert.That(advice.Steps, Is.EqualTo(new[]
            {
                RepairStep.RunSuiteSetupAgain, RepairStep.KeepFolderAndMode, RepairStep.KeepCdKeysTask
            }));
            Assert.That(advice.SuiteFolder, Is.EqualTo(Source));
            Assert.That(advice.InstalledBySuite, Is.True);
            Assert.That(advice.DownloadUrl, Is.EqualTo(SetupDownloadPage.PackageRelease),
                "the second option is the package again, not the product setup of the community website");
        }

        [Test]
        public void WithoutTheSuite_TheAdviceIsTheDownloadOfTheProductSetupAsBefore()
        {
            RepairAdvice advice = RepairAdvice.For(Community(Product.NeoEE, NeoRoot), RepairReason.Requested);

            Assert.That(advice.Steps, Is.EqualTo(new[] { RepairStep.CloseGameAndRunSetup, RepairStep.KeepFolderAndMode, RepairStep.KeepCdKeysTask }));
            Assert.That(advice.SuiteFolder, Is.Null);
            Assert.That(advice.InstalledBySuite, Is.False);
            Assert.That(advice.DownloadUrl, Is.EqualTo(SetupDownloadPage.NeoEE));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void ThePackageWithoutItsFolder_GetsTheDownloadOfThePackage_NeverTheProductSetup(string folder)
        {
            RepairAdvice advice = RepairAdvice.For(Community(Product.NeoEE, NeoRoot), RepairReason.Requested, suite: Package(folder));

            Assert.That(advice.Steps, Is.EqualTo(new[]
            {
                RepairStep.DownloadPackageAndRunSuite, RepairStep.KeepFolderAndMode, RepairStep.KeepCdKeysTask
            }));
            Assert.That(advice.SuiteFolder, Is.Null, "no folder to open");
            Assert.That(advice.InstalledBySuite, Is.True);
            Assert.That(advice.DownloadUrl, Is.EqualTo(SetupDownloadPage.PackageRelease));
            Assert.That(ProductSetupPages, Has.No.Member(advice.DownloadUrl));
        }

        [Test]
        public void TheRecordWithoutTheFolder_LeadsToTheReleasePageOfThePackage_ForEveryReason()
        {
            SeedRecord("EE,NeoEE");
            Installation installation = Community(Product.EE, EERoot);
            installation.AppId = InstallationWorld.EEAppId;
            SuitePackage package = Locator().PackageFor(installation);
            var update = new VersionCheckResult(installation, VersionKind.Setup, "1.7.2", VersionCheckOutcome.UpdateAvailable, "1.8.0",
                UpdateApiFailure.None);

            RepairAdvice[] advices =
            {
                RepairAdvice.For(installation, RepairReason.ProgramMissing, new[] { Game.EmpireEarth }, package),
                RepairAdvice.For(installation, RepairReason.Requested, suite: package),
                RepairAdvice.ForIntegrity(IntegrityReport.Unknown(installation, IntegrityCheckKind.Quick, UnknownReason.NoManifest), package),
                RepairAdvice.ForUpdate(update, package)
            };

            foreach (RepairAdvice advice in advices)
            {
                Assert.That(advice.DownloadUrl, Is.EqualTo(SetupDownloadPage.PackageRelease), advice.Reason.ToString());
                Assert.That(advice.Steps, Has.No.Member(RepairStep.CloseGameAndRunSetup), advice.Reason + ": no product setup");
            }
            Assert.That(advices[0].Steps, Is.EqualTo(new[]
            {
                RepairStep.AddAntivirusException, RepairStep.DownloadPackageAndRunSuite, RepairStep.KeepFolderAndMode
            }));
        }

        [TestCase(true, RepairStep.RunSuiteSetupAgain)]
        [TestCase(false, RepairStep.DownloadPackageAndRunSuite)]
        public void AnOlderSetupOverTheSuiteInstallation_IsRepairedWithThePackage_NotAgainWithTheProductSetup(
            bool folderExists, RepairStep expected)
        {
            // Contract 1.5: a setup up to 1.7.2 over a v2 installation leaves the record, install.ini and the manifest and
            // recreates the uninstall key without ContractVersion; the launcher reports Unknown (OlderSetupRanAfter, setup e2e C3).
            SeedRecord("EE,NeoEE");
            if (folderExists)
                world.FileSystem.AddDirectory(Source);
            Installation installation = Community(Product.EE, EERoot);
            IntegrityReport report = IntegrityReport.Unknown(installation, IntegrityCheckKind.Quick, UnknownReason.OlderSetupRanAfter);

            RepairAdvice advice = RepairAdvice.ForIntegrity(report, Locator().PackageFor(installation));

            Assert.That(advice.Reason, Is.EqualTo(RepairReason.IntegrityUnknown));
            Assert.That(advice.Steps, Is.EqualTo(new[] { expected, RepairStep.KeepFolderAndMode }));
            Assert.That(advice.DownloadUrl, Is.EqualTo(SetupDownloadPage.PackageRelease), "the loop ends: not the page of the setup that ran");
        }

        [Test]
        public void AnInstallationOfAnotherSetup_KeepsTheDownloadOfTheProductSetup_AlsoNextToASuiteRecord()
        {
            SeedRecord("NeoEE");
            Installation upstream = Community(Product.EE, EERoot, InstallationKind.CommunityLegacy);

            RepairAdvice advice = RepairAdvice.For(upstream, RepairReason.Requested, suite: Locator().PackageFor(upstream));

            Assert.That(advice.Steps, Is.EqualTo(new[] { RepairStep.CloseGameAndRunSetup, RepairStep.KeepFolderAndMode }));
            Assert.That(advice.DownloadUrl, Is.EqualTo(SetupDownloadPage.EmpireEarth), "the suite did not install EE");
        }

        [Test]
        public void TheAntivirusStepComesFirst_AlsoWithTheSuite()
        {
            RepairAdvice advice = RepairAdvice.For(Community(Product.EE, EERoot), RepairReason.ProgramMissing,
                new[] { Game.EmpireEarth }, Package());

            Assert.That(advice.Steps, Is.EqualTo(new[]
            {
                RepairStep.AddAntivirusException, RepairStep.RunSuiteSetupAgain, RepairStep.KeepFolderAndMode
            }));
        }

        [Test]
        public void AForeignInstallation_GetsNoSuiteStep()
        {
            RepairAdvice advice = RepairAdvice.For(Community(Product.EE, @"D:\Games", InstallationKind.Foreign, InstallMode.Unknown),
                RepairReason.Requested, suite: Package());

            Assert.That(advice.Steps, Is.EqualTo(new[] { RepairStep.ForeignNotRepaired }));
            Assert.That(advice.SuiteFolder, Is.Null);
            Assert.That(advice.InstalledBySuite, Is.False);
            Assert.That(advice.DownloadUrl, Is.EqualTo(SetupDownloadPage.General));
        }

        [Test]
        public void ForIntegrity_TakesTheSuiteFolderToo()
        {
            Installation installation = Community(Product.NeoEE, NeoRoot);
            IntegrityReport report = IntegrityReport.Unknown(installation, IntegrityCheckKind.Quick, UnknownReason.NoManifest);

            RepairAdvice advice = RepairAdvice.ForIntegrity(report, Package());

            Assert.That(advice.Steps.First(), Is.EqualTo(RepairStep.RunSuiteSetupAgain));
            Assert.That(advice.SuiteFolder, Is.EqualTo(Source));
        }

        [TestCase(Source)]
        [TestCase(null)]
        public void AnUpdateOfTheSuiteInstallation_IsANewReleaseOfThePackage_NeverTheSuiteAgain(string folder)
        {
            // The suite of the folder embeds the product setups it was built with and cannot install a newer version that the
            // update API reports for the setups of the community website.
            Installation installation = Community(Product.NeoEE, NeoRoot);
            var update = new VersionCheckResult(installation, VersionKind.Game, "2.0.0.5", VersionCheckOutcome.UpdateAvailable, "2.1.0",
                UpdateApiFailure.None);

            RepairAdvice advice = RepairAdvice.ForUpdate(update, Package(folder));

            Assert.That(advice.Steps, Is.EqualTo(new[]
            {
                RepairStep.UpdateWithNewPackage, RepairStep.KeepFolderAndMode, RepairStep.KeepCdKeysTask
            }));
            Assert.That(advice.Steps, Has.No.Member(RepairStep.RunSuiteSetupAgain));
            Assert.That(advice.SuiteFolder, Is.Null, "no button that opens the folder of the old package");
            Assert.That(advice.InstalledBySuite, Is.True);
            Assert.That(advice.DownloadUrl, Is.EqualTo(SetupDownloadPage.PackageRelease));
        }

        [Test]
        public void AnUpdateOfAnotherInstallation_KeepsTheProductSetup()
        {
            Installation installation = Community(Product.NeoEE, NeoRoot);
            var update = new VersionCheckResult(installation, VersionKind.Setup, "1.7.2", VersionCheckOutcome.UpdateAvailable, "1.8.0",
                UpdateApiFailure.None);

            RepairAdvice advice = RepairAdvice.ForUpdate(update);

            Assert.That(advice.Steps.First(), Is.EqualTo(RepairStep.CloseGameAndRunSetup));
            Assert.That(advice.DownloadUrl, Is.EqualTo(SetupDownloadPage.NeoEE));
        }

        // --- Opening the folder (contract 4.1, 4.4) ----------------------------------------------------------------------

        [Test]
        public void TheFolder_OpensThroughTheShell_NeverAProgram()
        {
            var starter = new FakeProcessStarter();
            var logger = new RecordingLogger();
            RepairAdvice advice = RepairAdvice.For(Community(Product.NeoEE, NeoRoot), RepairReason.Requested, suite: Package());

            bool opened = advice.OpenSuiteFolder(starter, logger);

            Assert.That(opened, Is.True);
            Assert.That(starter.OpenedFolders, Is.EqualTo(new[] { Source }));
            Assert.That(starter.Started, Is.Empty, "no program is started, the setup runs when the player starts it (contract 4.1)");
            Assert.That(starter.OpenedUrls, Is.Empty);
            Assert.That(logger.Messages.Single(), Does.Contain("opening the folder of the suite setup " + Source));
        }

        [Test]
        public void WithoutAFolder_NothingOpens()
        {
            var starter = new FakeProcessStarter();
            RepairAdvice advice = RepairAdvice.For(Community(Product.NeoEE, NeoRoot), RepairReason.Requested);
            RepairAdvice gone = RepairAdvice.For(Community(Product.NeoEE, NeoRoot), RepairReason.Requested, suite: Package(null));

            Assert.That(advice.OpenSuiteFolder(starter, new RecordingLogger()), Is.False);
            Assert.That(gone.OpenSuiteFolder(starter, new RecordingLogger()), Is.False);
            Assert.That(starter.OpenedFolders, Is.Empty);
        }

        [Test]
        public void TheReleasePageOfThePackage_OpensThroughTheShell_WithoutARequest()
        {
            var starter = new FakeProcessStarter();
            var logger = new RecordingLogger();
            RepairAdvice advice = RepairAdvice.For(Community(Product.EE, EERoot), RepairReason.Requested, suite: Package(null));

            Assert.That(advice.OpenDownloadPage(starter, logger), Is.EqualTo(DownloadPageResult.Opened));

            Assert.That(starter.OpenedUrls, Is.EqualTo(new[] { "https://github.com/DritteRippe/Empire-Earth-Community/releases/latest" }));
            Assert.That(starter.Started, Is.Empty, "the launcher never downloads or starts the setup (contract 4.1)");
            Assert.That(logger.Messages.Single(), Does.Contain("opening the download page " + SetupDownloadPage.PackageRelease)
                                                      .And.Contain("package release page"));
        }

        [TestCase(1155)]
        [TestCase(5)]
        public void AFailedOpen_IsLoggedNotThrown(int error)
        {
            var starter = new FakeProcessStarter { OpenException = new Win32Exception(error) };
            var logger = new RecordingLogger();
            RepairAdvice advice = RepairAdvice.For(Community(Product.EE, EERoot), RepairReason.Requested, suite: Package());

            Assert.That(advice.OpenSuiteFolder(starter, logger), Is.False);

            Assert.That(logger.Entries.Last().Exception, Is.SameAs(starter.OpenException));
            Assert.That(logger.Entries.Last().Message, Does.Contain("could not be opened"));
        }

        [Test]
        public void OpenSuiteFolder_ChecksItsArguments()
        {
            RepairAdvice advice = RepairAdvice.For(Community(Product.EE, EERoot), RepairReason.Requested, suite: Package());

            Assert.That(() => advice.OpenSuiteFolder(null, new RecordingLogger()), Throws.ArgumentNullException);
            Assert.That(() => advice.OpenSuiteFolder(new FakeProcessStarter(), null), Throws.ArgumentNullException);
        }

        [Test]
        public void ToString_NamesTheSuiteFolder()
        {
            RepairAdvice advice = RepairAdvice.For(Community(Product.EE, EERoot), RepairReason.Requested, suite: Package());

            Assert.That(advice.ToString(), Does.EndWith("; suite folder " + Source));
        }
    }
}

using System;
using System.ComponentModel;
using System.Linq;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Installations;
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
    /// suite record lists the product and its <c>SourceDir</c> exists (<see cref="SuiteRepairLocator"/>); the download of the
    /// product setup stays the second option; the folder is opened in the Explorer and never a program of it.
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
        public void TheLocator_ChecksItsArguments()
        {
            var reader = new SuiteRecordReader(world.Registry, world.Logger);
            Assert.That(() => new SuiteRepairLocator(null, world.FileSystem, world.Logger), Throws.ArgumentNullException);
            Assert.That(() => new SuiteRepairLocator(reader, null, world.Logger), Throws.ArgumentNullException);
            Assert.That(() => new SuiteRepairLocator(reader, world.FileSystem, null), Throws.ArgumentNullException);
            Assert.That(() => Locator().FolderFor(null), Throws.ArgumentNullException);
        }

        // --- The advice -------------------------------------------------------------------------------------------------

        [Test]
        public void Contract_4_4_WithTheSuiteFolder_TheSuiteStepReplacesTheDownloadStep_AndTheDownloadStaysAvailable()
        {
            RepairAdvice advice = RepairAdvice.For(Community(Product.NeoEE, NeoRoot), RepairReason.Requested, suiteFolder: Source);

            Assert.That(advice.Steps, Is.EqualTo(new[]
            {
                RepairStep.RunSuiteSetupAgain, RepairStep.KeepFolderAndMode, RepairStep.KeepCdKeysTask
            }));
            Assert.That(advice.SuiteFolder, Is.EqualTo(Source));
            Assert.That(advice.DownloadUrl, Is.EqualTo(SetupDownloadPage.NeoEE), "the download page of the product stays, as the second option");
        }

        [Test]
        public void WithoutTheSuiteFolder_TheAdviceIsTheDownloadAsBefore()
        {
            RepairAdvice advice = RepairAdvice.For(Community(Product.NeoEE, NeoRoot), RepairReason.Requested);

            Assert.That(advice.Steps, Is.EqualTo(new[] { RepairStep.CloseGameAndRunSetup, RepairStep.KeepFolderAndMode, RepairStep.KeepCdKeysTask }));
            Assert.That(advice.SuiteFolder, Is.Null);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void ANoFolder_IsNoSuiteAdvice(string folder)
        {
            RepairAdvice advice = RepairAdvice.For(Community(Product.EE, EERoot), RepairReason.Requested, suiteFolder: folder);

            Assert.That(advice.Steps, Does.Contain(RepairStep.CloseGameAndRunSetup));
            Assert.That(advice.Steps, Does.Not.Contain(RepairStep.RunSuiteSetupAgain));
            Assert.That(advice.SuiteFolder, Is.Null);
        }

        [Test]
        public void TheAntivirusStepComesFirst_AlsoWithTheSuite()
        {
            RepairAdvice advice = RepairAdvice.For(Community(Product.EE, EERoot), RepairReason.ProgramMissing,
                new[] { Game.EmpireEarth }, Source);

            Assert.That(advice.Steps, Is.EqualTo(new[]
            {
                RepairStep.AddAntivirusException, RepairStep.RunSuiteSetupAgain, RepairStep.KeepFolderAndMode
            }));
        }

        [Test]
        public void AForeignInstallation_GetsNoSuiteStep()
        {
            RepairAdvice advice = RepairAdvice.For(Community(Product.EE, @"D:\Games", InstallationKind.Foreign, InstallMode.Unknown),
                RepairReason.Requested, suiteFolder: Source);

            Assert.That(advice.Steps, Is.EqualTo(new[] { RepairStep.ForeignNotRepaired }));
            Assert.That(advice.SuiteFolder, Is.Null);
        }

        [Test]
        public void ForIntegrity_And_ForUpdate_TakeTheSuiteFolderToo()
        {
            Installation installation = Community(Product.NeoEE, NeoRoot);
            var update = new VersionCheckResult(installation, VersionKind.Game, "2.0.0.5", VersionCheckOutcome.UpdateAvailable, "2.1.0",
                UpdateApiFailure.None);

            RepairAdvice advice = RepairAdvice.ForUpdate(update, Source);

            Assert.That(advice.Steps.First(), Is.EqualTo(RepairStep.RunSuiteSetupAgain));
            Assert.That(advice.SuiteFolder, Is.EqualTo(Source));
            Assert.That(RepairAdvice.ForUpdate(update).Steps.First(), Is.EqualTo(RepairStep.CloseGameAndRunSetup));
        }

        // --- Opening the folder (contract 4.1, 4.4) ----------------------------------------------------------------------

        [Test]
        public void TheFolder_OpensThroughTheShell_NeverAProgram()
        {
            var starter = new FakeProcessStarter();
            var logger = new RecordingLogger();
            RepairAdvice advice = RepairAdvice.For(Community(Product.NeoEE, NeoRoot), RepairReason.Requested, suiteFolder: Source);

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

            Assert.That(advice.OpenSuiteFolder(starter, new RecordingLogger()), Is.False);
            Assert.That(starter.OpenedFolders, Is.Empty);
        }

        [TestCase(1155)]
        [TestCase(5)]
        public void AFailedOpen_IsLoggedNotThrown(int error)
        {
            var starter = new FakeProcessStarter { OpenException = new Win32Exception(error) };
            var logger = new RecordingLogger();
            RepairAdvice advice = RepairAdvice.For(Community(Product.EE, EERoot), RepairReason.Requested, suiteFolder: Source);

            Assert.That(advice.OpenSuiteFolder(starter, logger), Is.False);

            Assert.That(logger.Entries.Last().Exception, Is.SameAs(starter.OpenException));
            Assert.That(logger.Entries.Last().Message, Does.Contain("could not be opened"));
        }

        [Test]
        public void OpenSuiteFolder_ChecksItsArguments()
        {
            RepairAdvice advice = RepairAdvice.For(Community(Product.EE, EERoot), RepairReason.Requested, suiteFolder: Source);

            Assert.That(() => advice.OpenSuiteFolder(null, new RecordingLogger()), Throws.ArgumentNullException);
            Assert.That(() => advice.OpenSuiteFolder(new FakeProcessStarter(), null), Throws.ArgumentNullException);
        }

        [Test]
        public void ToString_NamesTheSuiteFolder()
        {
            RepairAdvice advice = RepairAdvice.For(Community(Product.EE, EERoot), RepairReason.Requested, suiteFolder: Source);

            Assert.That(advice.ToString(), Does.EndWith("; suite folder " + Source));
        }
    }
}

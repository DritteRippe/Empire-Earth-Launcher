using System;
using System.ComponentModel;
using System.Linq;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Integrity;
using Empire_Earth_Launcher.Core.Repair;
using Empire_Earth_Launcher.Tests.Fakes;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.Repair
{
    /// <summary>
    /// <see cref="RepairAdvice"/>: the steps of contract 4.4 per kind, product and reason, the download page of the product
    /// (contract 4.3), and opening it through the shell without elevation and without a request (R9, REV-11).
    /// </summary>
    [TestFixture]
    public class RepairAdviceTests
    {
        private const string NeoRoot = @"C:\Program Files (x86)\Neo Empire Earth";
        private const string EERoot = @"C:\Users\Player\AppData\Local\Programs\Empire Earth";

        private static Installation Community(Product product, string root, InstallMode mode,
            InstallationKind kind = InstallationKind.Community, params Game[] missing)
        {
            var installation = new Installation(product, root, root + @"\Empire Earth", root + @"\Empire Earth - The Art of Conquest",
                kind, mode, new[] { InstallationSource.RegistryRecord });
            if (missing.Length > 0)
            {
                installation.State = InstallationState.Damaged;
                installation.MissingPrograms = missing;
            }
            return installation;
        }

        private static Installation Foreign()
        {
            return new Installation(Product.EE, @"C:\Games", @"C:\Games\EE", null, InstallationKind.Foreign, InstallMode.Unknown,
                new[] { InstallationSource.InstalledFrom });
        }

        [Test]
        public void Contract_4_4_NeoEE_ProgramMissing_AllSteps()
        {
            RepairAdvice advice = RepairAdvice.For(Community(Product.NeoEE, NeoRoot, InstallMode.Admin, missing: new[] { Game.EmpireEarth }),
                RepairReason.ProgramMissing);

            Assert.That(advice.Steps, Is.EqualTo(new[]
            {
                RepairStep.AddAntivirusException, RepairStep.CloseGameAndRunSetup, RepairStep.KeepFolderAndMode,
                RepairStep.KeepCdKeysTask
            }), "the antivirus exception comes first, then the run of the setup with the same folder, mode and CD-key task");
            Assert.That(advice.MissingPrograms, Is.EqualTo(new[] { Game.EmpireEarth }));
            Assert.That(advice.Installation.Root, Is.EqualTo(NeoRoot));
            Assert.That(advice.Installation.Mode, Is.EqualTo(InstallMode.Admin));
        }

        [Test]
        public void Contract_4_4_EE_HasNoCdKeyStep()
        {
            RepairAdvice advice = RepairAdvice.For(Community(Product.EE, EERoot, InstallMode.User), RepairReason.Requested);

            Assert.That(advice.Steps, Is.EqualTo(new[] { RepairStep.CloseGameAndRunSetup, RepairStep.KeepFolderAndMode }));
            Assert.That(advice.MissingPrograms, Is.Empty);
        }

        [Test]
        public void Contract_4_4_OnRequest_NoAntivirusStep()
        {
            RepairAdvice advice = RepairAdvice.For(Community(Product.NeoEE, NeoRoot, InstallMode.Admin), RepairReason.Requested);

            Assert.That(advice.Steps, Does.Not.Contain(RepairStep.AddAntivirusException));
            Assert.That(advice.Steps, Does.Contain(RepairStep.KeepCdKeysTask));
        }

        [Test]
        public void Contract_4_4_SetupUpTo172_IsRepairedLikeACommunityInstallation()
        {
            RepairAdvice advice = RepairAdvice.For(
                Community(Product.NeoEE, NeoRoot, InstallMode.Admin, InstallationKind.CommunityLegacy, Game.ArtOfConquest),
                RepairReason.ProgramMissing);

            Assert.That(advice.Steps, Is.EqualTo(new[]
            {
                RepairStep.AddAntivirusException, RepairStep.CloseGameAndRunSetup, RepairStep.KeepFolderAndMode,
                RepairStep.KeepCdKeysTask
            }));
            Assert.That(advice.MissingPrograms, Is.EqualTo(new[] { Game.ArtOfConquest }));
        }

        [Test]
        public void Contract_4_4_ForeignInstallations_AreNotRepaired()
        {
            RepairAdvice requested = RepairAdvice.For(Foreign(), RepairReason.Requested);
            RepairAdvice missing = RepairAdvice.For(Foreign(), RepairReason.ProgramMissing, new[] { Game.EmpireEarth });

            Assert.That(requested.Steps, Is.EqualTo(new[] { RepairStep.ForeignNotRepaired }));
            Assert.That(missing.Steps, Is.EqualTo(new[] { RepairStep.ForeignNotRepaired, RepairStep.AddAntivirusException }));
            Assert.That(missing.MissingPrograms, Is.EqualTo(new[] { Game.EmpireEarth }));
            Assert.That(missing.Folder, Is.EqualTo(@"C:\Games\EE"), "never a whole drive as antivirus exception");
        }

        [Test]
        public void Contract_4_4_TheFolderOfACommunityInstallation_IsItsRoot()
        {
            RepairAdvice advice = RepairAdvice.For(Community(Product.NeoEE, NeoRoot, InstallMode.Admin), RepairReason.Requested);

            Assert.That(advice.Folder, Is.EqualTo(NeoRoot));
        }

        [Test]
        public void MissingPrograms_GivenAtPlay_WinOverTheDiscovery()
        {
            RepairAdvice advice = RepairAdvice.For(Community(Product.EE, EERoot, InstallMode.User), RepairReason.ProgramMissing,
                new[] { Game.ArtOfConquest, Game.ArtOfConquest });

            Assert.That(advice.MissingPrograms, Is.EqualTo(new[] { Game.ArtOfConquest }));
        }

        [Test]
        public void Contract_4_3_TheDownloadPageIsTheHttpsPageOfTheProduct()
        {
            RepairAdvice neo = RepairAdvice.For(Community(Product.NeoEE, NeoRoot, InstallMode.Admin), RepairReason.Requested);
            RepairAdvice ee = RepairAdvice.For(Community(Product.EE, EERoot, InstallMode.User), RepairReason.Requested);
            RepairAdvice foreign = RepairAdvice.For(Foreign(), RepairReason.Requested);

            Assert.That(neo.DownloadUrl, Is.EqualTo("https://empireearth.eu/download/neo/"));
            Assert.That(ee.DownloadUrl, Is.EqualTo("https://empireearth.eu/download/ee/"));
            Assert.That(foreign.DownloadUrl, Is.EqualTo("https://empireearth.eu/download/"));
            foreach (RepairAdvice advice in new[] { neo, ee, foreign })
            {
                var uri = new Uri(advice.DownloadUrl);
                Assert.That(uri.Scheme, Is.EqualTo(Uri.UriSchemeHttps));
                Assert.That(uri.Host, Is.EqualTo("empireearth.eu"));
            }
        }

        [Test]
        public void Contract_4_3_EveryReason_HasThePageAtOnce()
        {
            Installation installation = Community(Product.EE, EERoot, InstallMode.User);
            var update = new VersionCheckResult(installation, VersionKind.Game, "2.0.0.5", VersionCheckOutcome.UpdateAvailable,
                "2.0.1.0", UpdateApiFailure.None);

            Assert.That(RepairAdvice.For(installation, RepairReason.Requested).DownloadUrl, Is.EqualTo(SetupDownloadPage.EmpireEarth));
            Assert.That(RepairAdvice.ForUpdate(update).DownloadUrl, Is.EqualTo(SetupDownloadPage.EmpireEarth));
            Assert.That(RepairAdvice.ForIntegrity(IntegrityReport.Unknown(installation, IntegrityCheckKind.Quick,
                UnknownReason.OlderSetupRanAfter)).DownloadUrl, Is.EqualTo(SetupDownloadPage.EmpireEarth));
        }

        [Test]
        public void Contract_4_3_ThePageOpensThroughTheShellStarter()
        {
            var starter = new FakeProcessStarter();
            var logger = new RecordingLogger();
            RepairAdvice advice = RepairAdvice.For(Community(Product.NeoEE, NeoRoot, InstallMode.Admin), RepairReason.Requested);

            DownloadPageResult result = advice.OpenDownloadPage(starter, logger);

            Assert.That(result, Is.EqualTo(DownloadPageResult.Opened));
            Assert.That(starter.OpenedUrls, Is.EqualTo(new[] { "https://empireearth.eu/download/neo/" }));
            Assert.That(starter.Started, Is.Empty, "the launcher never starts the setup itself (contract 4.1)");
            Assert.That(logger.Messages.Single(), Does.Contain("opening the download page https://empireearth.eu/download/neo/")
                                                      .And.Contain("contract 4.3, NeoEE page"));
        }

        [TestCase(1155, TestName = "OpenDownloadPage_NoBrowser_IsAResult")]
        [TestCase(5, TestName = "OpenDownloadPage_AccessDenied_IsAResult")]
        public void Contract_4_3_AFailedOpen_IsLoggedNotThrown(int error)
        {
            var starter = new FakeProcessStarter { OpenException = new Win32Exception(error) };
            var logger = new RecordingLogger();
            RepairAdvice advice = RepairAdvice.For(Foreign(), RepairReason.Requested);

            DownloadPageResult result = advice.OpenDownloadPage(starter, logger);

            Assert.That(result, Is.EqualTo(DownloadPageResult.Failed));
            Assert.That(logger.Entries.Last().Exception, Is.SameAs(starter.OpenException));
            Assert.That(logger.Entries.Last().Message, Does.Contain("could not be opened"));
        }

        // --- L-WP7: the integrity check, the version check and the update API -----------------------------------------

        private static IntegrityFinding Finding(string path, FindingKind kind)
        {
            return new IntegrityFinding(path, NeoRoot + @"\" + path.Replace('/', '\\'), FileClassifier.Classify(path), kind,
                kind == FindingKind.MissingAfterInstall ? null : "expected", kind == FindingKind.HashDiffers ? "actual" : null, null);
        }

        [Test]
        public void Contract_2_5_DamagedOrIncomplete_NameTheFiles_AndTheAntivirusExceptionComesFirst()
        {
            Installation installation = Community(Product.NeoEE, NeoRoot, InstallMode.Admin);
            IntegrityReport report = IntegrityReport.Finished(installation, IntegrityCheckKind.Full, new[]
            {
                Finding("Empire Earth/Data/file0001.dat", FindingKind.Missing),
                Finding("Empire Earth/Data/file0002.dat", FindingKind.HashDiffers),
                Finding("Empire Earth/neoee.dll", FindingKind.Missing)
            }, 10, 9);

            RepairAdvice advice = RepairAdvice.ForIntegrity(report);

            Assert.That(advice.Reason, Is.EqualTo(RepairReason.IntegrityFindings));
            Assert.That(advice.Steps, Is.EqualTo(new[]
            {
                RepairStep.AddAntivirusException, RepairStep.CloseGameAndRunSetup, RepairStep.KeepFolderAndMode,
                RepairStep.KeepCdKeysTask
            }));
            Assert.That(advice.Files.Select(file => file.Path), Is.EqualTo(new[]
            {
                "Empire Earth/neoee.dll", "Empire Earth/Data/file0001.dat"
            }), "the damaged file first; a modified data file is not named");
            Assert.That(advice.MissingPrograms, Is.Empty);
        }

        [Test]
        public void Contract_2_5_UnknownOfACommunityInstallation_RunTheCurrentSetup_WithoutAntivirus()
        {
            IntegrityReport report = IntegrityReport.Unknown(Community(Product.EE, EERoot, InstallMode.User),
                IntegrityCheckKind.Quick, UnknownReason.OlderSetupRanAfter);

            RepairAdvice advice = RepairAdvice.ForIntegrity(report);

            Assert.That(advice.Reason, Is.EqualTo(RepairReason.IntegrityUnknown));
            Assert.That(advice.Steps, Is.EqualTo(new[] { RepairStep.CloseGameAndRunSetup, RepairStep.KeepFolderAndMode }));
            Assert.That(advice.Files, Is.Empty);
        }

        [TestCase(UnknownReason.LegacySetup)]
        [TestCase(UnknownReason.NewerContract)]
        [TestCase(UnknownReason.FilesUnreadable)]
        public void Contract_2_5_AReportWithoutRepair_HasNoAdvice(UnknownReason reason)
        {
            IntegrityReport report = IntegrityReport.Unknown(Community(Product.NeoEE, NeoRoot, InstallMode.Admin),
                IntegrityCheckKind.Quick, reason);

            Assert.That(report.OffersRepair, Is.False);
            Assert.Throws<ArgumentException>(() => RepairAdvice.ForIntegrity(report));
        }

        [Test]
        public void Contract_4_5_AnAvailableUpdate_UsesTheHandOff()
        {
            var update = new VersionCheckResult(Community(Product.NeoEE, NeoRoot, InstallMode.Admin), VersionKind.Game, "2.0.0.5",
                VersionCheckOutcome.UpdateAvailable, "2.0.1.0", UpdateApiFailure.None);

            RepairAdvice advice = RepairAdvice.ForUpdate(update);

            Assert.That(advice.Reason, Is.EqualTo(RepairReason.UpdateAvailable));
            Assert.That(advice.Update, Is.SameAs(update));
            Assert.That(advice.Steps, Is.EqualTo(new[]
            {
                RepairStep.CloseGameAndRunSetup, RepairStep.KeepFolderAndMode, RepairStep.KeepCdKeysTask
            }));
            Assert.Throws<ArgumentException>(() => RepairAdvice.ForUpdate(new VersionCheckResult(update.Installation,
                VersionKind.Game, "2.0.0.5", VersionCheckOutcome.UpToDate, null, UpdateApiFailure.None)));
        }

        [Test]
        public void TheReasonsOfTheChecks_AreNotMadeByFor()
        {
            Installation installation = Community(Product.NeoEE, NeoRoot, InstallMode.Admin);

            Assert.Throws<ArgumentException>(() => RepairAdvice.For(installation, RepairReason.IntegrityFindings));
            Assert.Throws<ArgumentException>(() => RepairAdvice.For(installation, RepairReason.UpdateAvailable));
        }

        [Test]
        public void Contract_4_3_ThePageOfAnInstallationWithoutAnAppId_NeedsNoRequestEither()
        {
            var starter = new FakeProcessStarter();
            var logger = new RecordingLogger();
            RepairAdvice advice = RepairAdvice.For(Foreign(), RepairReason.Requested);

            advice.OpenDownloadPage(starter, logger);

            Assert.That(starter.OpenedUrls, Is.EqualTo(new[] { "https://empireearth.eu/download/" }));
            Assert.That(logger.Messages.Last(), Does.Contain("contract 4.3, general page"));
        }

        [Test]
        public void ToString_NamesReasonStepsAndPage()
        {
            RepairAdvice advice = RepairAdvice.For(Community(Product.EE, EERoot, InstallMode.User, missing: new[] { Game.EmpireEarth }),
                RepairReason.ProgramMissing);

            Assert.That(advice.ToString(), Is.EqualTo("ProgramMissing for EE " + EERoot + " (missing Empire Earth.exe): " +
                                                      "AddAntivirusException, CloseGameAndRunSetup, KeepFolderAndMode; " +
                                                      "https://empireearth.eu/download/ee/"));
        }
    }
}

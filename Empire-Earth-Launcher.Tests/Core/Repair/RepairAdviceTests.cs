using System;
using System.ComponentModel;
using System.Linq;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Repair;
using Empire_Earth_Launcher.Tests.Fakes;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.Repair
{
    /// <summary>
    /// <see cref="RepairAdvice"/>: the steps of contract 4.4 per kind, product and reason, the fixed download page of contract
    /// 4.3 step 3, and opening it through the shell without elevation (R9, REV-11).
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
        public void Contract_4_3_TheDownloadPageIsTheFixedHttpsPage()
        {
            RepairAdvice advice = RepairAdvice.For(Community(Product.NeoEE, NeoRoot, InstallMode.Admin), RepairReason.Requested);

            Assert.That(advice.DownloadUrl, Is.EqualTo("https://empireearth.eu/download"));
            Assert.That(advice.IsFixedPage, Is.True);
            var uri = new Uri(advice.DownloadUrl);
            Assert.That(uri.Scheme, Is.EqualTo(Uri.UriSchemeHttps));
            Assert.That(uri.Host, Is.EqualTo("empireearth.eu"));
        }

        [Test]
        public void Contract_4_3_ThePageOpensThroughTheShellStarter()
        {
            var starter = new FakeProcessStarter();
            var logger = new RecordingLogger();
            RepairAdvice advice = RepairAdvice.For(Community(Product.NeoEE, NeoRoot, InstallMode.Admin), RepairReason.Requested);

            DownloadPageResult result = advice.OpenDownloadPage(starter, logger);

            Assert.That(result, Is.EqualTo(DownloadPageResult.Opened));
            Assert.That(starter.OpenedUrls, Is.EqualTo(new[] { RepairAdvice.DownloadPageUrl }));
            Assert.That(starter.Started, Is.Empty, "the launcher never starts the setup itself (contract 4.1)");
            Assert.That(logger.Messages.Single(), Does.Contain("opening the download page https://empireearth.eu/download")
                                                      .And.Contain("fixed page"));
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

        [Test]
        public void ToString_NamesReasonStepsAndPage()
        {
            RepairAdvice advice = RepairAdvice.For(Community(Product.EE, EERoot, InstallMode.User, missing: new[] { Game.EmpireEarth }),
                RepairReason.ProgramMissing);

            Assert.That(advice.ToString(), Is.EqualTo("ProgramMissing for EE " + EERoot + " (missing Empire Earth.exe): " +
                                                      "AddAntivirusException, CloseGameAndRunSetup, KeepFolderAndMode; " +
                                                      "https://empireearth.eu/download"));
        }
    }
}

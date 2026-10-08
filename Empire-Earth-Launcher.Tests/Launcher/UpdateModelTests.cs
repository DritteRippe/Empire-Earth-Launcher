using System;
using System.Linq;
using System.Threading.Tasks;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Core.Play;
using Empire_Earth_Launcher.Core.Repair;
using Empire_Earth_Launcher.Core.Settings;
using Empire_Earth_Launcher.Tests.Fakes;
using Empire_Earth_Launcher.Tests.TestSupport;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Launcher
{
    /// <summary>
    /// <see cref="UpdateModel"/>, the update API for the pages and the repair advice (L-WP7, contract 4.3 and 4.5, ADR 0008):
    /// the game version check of the Play page, the game and setup version check of the Tools page, the hand-off of an
    /// available update (for an installation of the suite the release page of the package, since launcher 1.1.1), and the
    /// download page of the product through the shell (no request). With the fake HTTPS client: no test uses the network.
    /// </summary>
    [TestFixture]
    public class UpdateModelTests
    {
        private const string SettingsFile = @"C:\Users\Player\AppData\Local\Empire Earth Launcher\settings.json";
        private const string Root = @"C:\Program Files (x86)\Neo Empire Earth";
        private const string OtherRoot = @"D:\Games\Empire Earth Community";
        private const string ForeignFolder = @"D:\Retail\Empire Earth";
        private static readonly string Api = "https://api.empireearth.eu/setup/?product=" + InstallationWorld.NeoEEAppId;

        private InstallationWorld world;
        private FakeHttpsClient client;
        private FakeProcessStarter shell;
        private InstallationService installations;
        private UpdateModel model;
        private int changed;

        [SetUp]
        public void SetUp()
        {
            world = new InstallationWorld();
            client = new FakeHttpsClient();
            shell = new FakeProcessStarter();
            var settings = new SettingsStore(world.FileSystem, SettingsFile, world.Logger);
            settings.Load();
            var watcher = new SetupWatcher(new FakeMutexProbe(), world.Clock, world.Logger);
            installations = new InstallationService(world.Logger, settings, world.CreateDiscovery(), world.FileSystem, null,
                watcher);
            model = new UpdateModel(new UpdateChecker(client, world.Logger), installations, shell, world.Logger);
            model.Changed += (sender, e) => changed++;
        }

        [Test]
        public void BeforeTheFirstSearch_NoCheckIsPossible()
        {
            Assert.That(model.CanCheck, Is.False);
            Assert.That(model.GameResult, Is.Null);
            Assert.That(model.UpdateAdvice, Is.Null);
            Assert.ThrowsAsync<InvalidOperationException>(() => model.CheckAsync(false));
        }

        [Test]
        public async Task Contract_4_5_PlayPage_AsksOnlyTheGameVersion_AndAnUpdateUsesTheHandOff()
        {
            world.AddCommunityInstallation(Root, Product.NeoEE);
            await installations.RefreshAsync();
            client.Answer(Api + "&type=game&version=2.0.0.5", 200, "false").Answer(Api + "&type=game", 200, "2.0.1.0");

            VersionCheckResult game = await model.CheckAsync(false);

            Assert.That(client.Requests, Is.EqualTo(new[] { Api + "&type=game&version=2.0.0.5", Api + "&type=game" }),
                "the query of the setup, nothing else");
            Assert.That(game.Outcome, Is.EqualTo(VersionCheckOutcome.UpdateAvailable));
            Assert.That(game.LatestVersion, Is.EqualTo("2.0.1.0"));
            Assert.That(model.GameResult, Is.SameAs(game));
            Assert.That(model.SetupResult, Is.Null);
            Assert.That(model.IsChecking, Is.False);
            Assert.That(changed, Is.GreaterThanOrEqualTo(2), "check started, check ended");
            RepairAdvice advice = model.UpdateAdvice;
            Assert.That(advice.Reason, Is.EqualTo(RepairReason.UpdateAvailable));
            Assert.That(advice.Update, Is.SameAs(game));
        }

        [Test]
        public async Task Contract_4_5_ToolsPage_AlsoAsksTheSetupVersion()
        {
            world.AddCommunityInstallation(Root, Product.NeoEE);
            await installations.RefreshAsync();
            client.Answer(Api + "&type=game&version=2.0.0.5", 200, "true")
                  .Answer(Api + "&type=setup&version=2.0.0", 200, "false")
                  .Answer(Api + "&type=setup", 200, "2.1.0");

            await model.CheckAsync(true);

            Assert.That(model.GameResult.Outcome, Is.EqualTo(VersionCheckOutcome.UpToDate));
            Assert.That(model.SetupResult.Outcome, Is.EqualTo(VersionCheckOutcome.UpdateAvailable));
            Assert.That(model.SetupResult.LatestVersion, Is.EqualTo("2.1.0"));
            Assert.That(model.UpdateAdvice.Update, Is.SameAs(model.SetupResult), "the game is current, the setup is not");
        }

        [Test]
        public async Task NoAnswer_IsFailed_AndOffersNoUpdate()
        {
            world.AddCommunityInstallation(Root, Product.NeoEE);
            await installations.RefreshAsync();
            client.Fail(Api + "&type=game&version=2.0.0.5", HttpsOutcome.TlsError,
                "HttpRequestException/WebException/AuthenticationException");

            VersionCheckResult game = await model.CheckAsync(false);

            Assert.That(game.Outcome, Is.EqualTo(VersionCheckOutcome.Failed));
            Assert.That(game.Failure, Is.EqualTo(UpdateApiFailure.TlsError));
            Assert.That(model.UpdateAdvice, Is.Null);
        }

        [Test]
        public async Task AForeignInstallation_IsNotAsked()
        {
            world.AddForeignInstallation(ForeignFolder);
            await installations.RefreshAsync();

            VersionCheckResult game = await model.CheckAsync(true);

            Assert.That(game.Outcome, Is.EqualTo(VersionCheckOutcome.NotPossible));
            Assert.That(model.SetupResult.Outcome, Is.EqualTo(VersionCheckOutcome.NotPossible));
            Assert.That(client.Requests, Is.Empty);
        }

        [Test]
        public async Task AnotherInstallation_DropsTheResults_TheSameOneKeepsThem()
        {
            world.AddCommunityInstallation(Root, Product.NeoEE);
            world.AddCommunityInstallation(OtherRoot, Product.EE, "user");
            await installations.RefreshAsync();
            client.DefaultResponse = HttpsResponse.Answered(200, "true", TimeSpan.Zero);
            await model.CheckAsync(false);

            await installations.RefreshAsync();
            Assert.That(model.GameResult, Is.Not.Null, "a new search of the same installation keeps the result");

            await installations.ChooseFolderAsync(OtherRoot);
            Assert.That(model.GameResult, Is.Null);
            Assert.That(model.CheckedInstallation, Is.Null);
        }

        [Test]
        public async Task Contract_4_3_OpenDownloadPage_OpensThePageOfTheProduct_WithoutARequest()
        {
            world.AddCommunityInstallation(Root, Product.NeoEE);
            await installations.RefreshAsync();
            RepairAdvice advice = RepairAdvice.For(installations.Selected, RepairReason.Requested);

            Assert.That(model.OpenDownloadPage(advice), Is.EqualTo(DownloadPageResult.Opened));

            Assert.That(shell.OpenedUrls, Is.EqualTo(new[] { "https://empireearth.eu/download/neo/" }));
            Assert.That(client.Requests, Is.Empty, "the page is chosen by the installation alone, the update API is not asked");
        }

        [Test]
        public async Task Contract_4_3_AnUpdateAdvice_OpensThePageOfTheProduct_AfterTheVersionRequestsOnly()
        {
            world.AddCommunityInstallation(Root, Product.NeoEE);
            await installations.RefreshAsync();
            client.Answer(Api + "&type=game&version=2.0.0.5", 200, "false").Answer(Api + "&type=game", 200, "2.0.1.0");
            await model.CheckAsync(false);
            RepairAdvice advice = model.UpdateAdvice;
            string[] versionRequests = client.Requests.ToArray();

            model.OpenDownloadPage(advice);

            Assert.That(shell.OpenedUrls, Is.EqualTo(new[] { "https://empireearth.eu/download/neo/" }));
            Assert.That(client.Requests, Is.EqualTo(versionRequests), "opening the page sends no request");
            Assert.That(client.Requests, Has.All.Contain("&type=game"), "the update API gets only the questions of contract 4.5");
        }

        [TestCase(true)]
        [TestCase(false)]
        public async Task AnUpdateOfAnInstallationOfTheSuite_LeadsToTheReleasePageOfThePackage_NeverToTheSuiteAgain(bool folderExists)
        {
            const string source = @"C:\Users\Player\Downloads\Empire-Earth-Community-1.1.0";
            world.AddCommunityInstallation(Root, Product.NeoEE);
            RegistryLocation record = world.AddSuiteRecord();
            world.Registry.Seed(record, ContractNames.SuiteSourceDirName, RegistryValue.FromString(source));
            if (folderExists)
                world.FileSystem.AddDirectory(source);
            var suiteModel = new UpdateModel(new UpdateChecker(client, world.Logger), installations, shell, world.Logger,
                new SuiteRepairLocator(new SuiteRecordReader(world.Registry, world.Logger), world.FileSystem, world.Logger));
            await installations.RefreshAsync();
            client.Answer(Api + "&type=game&version=2.0.0.5", 200, "false").Answer(Api + "&type=game", 200, "2.0.1.0");

            await suiteModel.CheckAsync(false);
            RepairAdvice advice = suiteModel.UpdateAdvice;
            suiteModel.OpenDownloadPage(advice);

            Assert.That(advice.Steps, Is.EqualTo(new[]
            {
                RepairStep.UpdateWithNewPackage, RepairStep.KeepFolderAndMode, RepairStep.KeepCdKeysTask
            }), "the suite of the folder installs the versions it embeds, not the one the update API reports");
            Assert.That(advice.SuiteFolder, Is.Null);
            Assert.That(shell.OpenedUrls, Is.EqualTo(new[] { SetupDownloadPage.PackageRelease }));
            Assert.That(shell.OpenedFolders, Is.Empty);
        }

        [Test]
        public void ThePackageReleasePage_OpensThroughTheShell_WithoutARequest_AlsoWithoutAnInstallation()
        {
            Assert.That(model.OpenPackageReleasePage(), Is.EqualTo(DownloadPageResult.Opened));

            Assert.That(shell.OpenedUrls, Is.EqualTo(new[] { "https://github.com/DritteRippe/Empire-Earth-Community/releases/latest" }));
            Assert.That(shell.Started, Is.Empty, "the launcher downloads and starts nothing");
            Assert.That(client.Requests, Is.Empty, "no request to the update API or to GitHub");
            Assert.That(world.Logger.Messages.Last(), Does.Contain("Opening the release page of the package " + SetupDownloadPage.PackageRelease));
        }

        [Test]
        public void ThePackageReleasePage_AFailureIsAResult()
        {
            shell.OpenException = new System.ComponentModel.Win32Exception(1155);

            Assert.That(model.OpenPackageReleasePage(), Is.EqualTo(DownloadPageResult.Failed));
            Assert.That(world.Logger.Entries.Last().Exception, Is.SameAs(shell.OpenException));
        }
    }
}

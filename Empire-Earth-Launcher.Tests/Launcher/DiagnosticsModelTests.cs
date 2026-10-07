using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Diagnostics;
using Empire_Earth_Launcher.Core.GameSettings;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Core.Play;
using Empire_Earth_Launcher.Core.Repair;
using Empire_Earth_Launcher.Core.Settings;
using Empire_Earth_Launcher.Tests.Core.Diagnostics;
using Empire_Earth_Launcher.Tests.Fakes;
using Empire_Earth_Launcher.Tests.TestSupport;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Launcher
{
    /// <summary>
    /// <see cref="DiagnosticsModel"/> (L-WP9): the network check only on request with the AppId of the selected (else the first)
    /// community installation, one check at a time, the report with the latest check, and saving it as UTF-8 into the file the
    /// player chose but never into an installation. With fakes only.
    /// </summary>
    [TestFixture]
    public class DiagnosticsModelTests
    {
        private const string SettingsFile = @"C:\Users\Player\AppData\Local\Empire Earth Launcher\settings.json";
        private const string Root = @"C:\Program Files (x86)\Neo Empire Earth";

        private InstallationWorld world;
        private InstallationService installations;
        private FakeNetworkInfo network;
        private FakeHttpsClient https;
        private FakeNeoStatusServer statusServer;
        private DiagnosticsModel model;
        private int changed;

        [SetUp]
        public void SetUp()
        {
            changed = 0;
            world = new InstallationWorld();
            var settings = new SettingsStore(world.FileSystem, SettingsFile, world.Logger);
            settings.Load();
            var watcher = new SetupWatcher(new FakeMutexProbe(), world.Clock, world.Logger);
            installations = new InstallationService(world.Logger, settings, world.CreateDiscovery(), world.FileSystem, null, watcher);
            network = new FakeNetworkInfo().WithHomeEthernet().Resolve(NetworkDiagnosticsTests.StatusHost, "192.0.2.10");
            https = new FakeHttpsClient();
            statusServer = new FakeNeoStatusServer();
            var anonymizer = new ReportAnonymizer(new PrivateNames("Player", new[] { "PLAYER-PC" }, null, @"C:\Users\Player",
                @"C:\Users\Player\AppData\Local"));
            var diagnostics = new NetworkDiagnostics(network, statusServer, https, world.FileSystem,
                new EffectivePathResolver(world.FileSystem, @"C:\Users\Player\AppData\Local\VirtualStore", new[] { @"C:\Program Files (x86)" }),
                anonymizer, world.Clock, world.Logger);
            model = new DiagnosticsModel(diagnostics, installations,
                () => new DiagnosticsInput { LauncherVersion = "0.1.0-alpha", CreatedAt = world.Clock.Now, Discovery = installations.Result },
                anonymizer, world.FileSystem, world.Logger);
            model.Changed += (sender, e) => changed++;
        }

        [Test]
        public async Task TheCheck_RunsOnRequest_WithTheAppIdOfTheSelectedInstallation()
        {
            world.AddCommunityInstallation(Root, Product.NeoEE);
            await installations.RefreshAsync();
            Assert.That(network.Lookups, Is.Empty, "nothing is asked before the player asks");

            Task check = model.CheckNetworkAsync();
            await check;

            Assert.That(model.Network, Is.Not.Null);
            Assert.That(model.IsChecking, Is.False);
            Assert.That(changed, Is.EqualTo(2), "started and finished");
            Assert.That(https.Requests, Is.EqualTo(new[] { UpdateApi.QueryUrl(InstallationWorld.NeoEEAppId, "game") }));
            Assert.That(model.Network.Installation.Root, Is.EqualTo(Root));
        }

        /// <summary>ADR 0008: a foreign installation has no AppId; the update API is asked with one of a community installation.</summary>
        [Test]
        public async Task AForeignSelection_UsesTheAppIdOfACommunityInstallation()
        {
            world.AddCommunityInstallation(Root, Product.NeoEE);
            world.AddForeignInstallation(@"D:\Games\Empire Earth");
            await installations.ChooseFolderAsync(@"D:\Games\Empire Earth");
            Assert.That(installations.Selected.AppId, Is.Null);

            Assert.That(model.AppId, Is.EqualTo(InstallationWorld.NeoEEAppId));
        }

        [Test]
        public async Task WithoutAnyAppId_TheUpdateApiIsNotAsked()
        {
            world.AddForeignInstallation(@"D:\Games\Empire Earth");
            await installations.RefreshAsync();

            await model.CheckNetworkAsync();

            Assert.That(model.AppId, Is.Null);
            Assert.That(https.Requests, Is.Empty);
            Assert.That(model.Network.UpdateApi, Is.EqualTo(UpdateApiAnswer.NotAsked));
        }

        [Test]
        public async Task ASecondRequest_WhileTheCheckRuns_IsIgnored()
        {
            world.AddCommunityInstallation(Root, Product.NeoEE);
            await installations.RefreshAsync();
            https.Hold();

            Task first = model.CheckNetworkAsync();
            Assert.That(model.IsChecking, Is.True);
            Task second = model.CheckNetworkAsync();
            Assert.That(second.IsCompleted, Is.True);
            https.Release();
            await first;

            Assert.That(https.Requests, Has.Count.EqualTo(1));
            Assert.That(statusServer.Requests, Is.EqualTo(1));
        }

        [Test]
        public async Task TheReport_HasTheLatestCheck()
        {
            world.AddCommunityInstallation(Root, Product.NeoEE);
            await installations.RefreshAsync();
            Assert.That(model.BuildReport(), Does.Contain("Network: not checked"));

            await model.CheckNetworkAsync();

            string report = model.BuildReport();
            Assert.That(report, Does.Contain("Network (checked "));
            Assert.That(report, Does.Contain("Installations (1)"));
        }

        [Test]
        public void SaveReport_WritesUtf8WithBom_IntoTheChosenFile()
        {
            const string file = @"C:\Users\Player\Documents\report.txt";
            world.FileSystem.AddDirectory(@"C:\Users\Player\Documents");

            ReportSaveResult result = model.SaveReport(file, "line 1\r\nline 2\r\n");

            Assert.That(result.Outcome, Is.EqualTo(ReportSaveOutcome.Saved));
            byte[] bytes = world.FileSystem.GetContent(file);
            Assert.That(bytes.Take(3), Is.EqualTo(new byte[] { 0xEF, 0xBB, 0xBF }));
            Assert.That(Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3), Is.EqualTo("line 1\r\nline 2\r\n"));
            Assert.That(world.Logger.Messages, Has.Some.Contains(@"saved to %USERPROFILE%\Documents\report.txt"));
        }

        /// <summary>The launcher never writes into the installation (contract 2.5, ARCHITECTURE 3).</summary>
        [TestCase(Root + @"\report.txt")]
        [TestCase(Root + @"\Empire Earth\report.txt")]
        [TestCase(Root + @"\Empire Earth - The Art of Conquest\Data\report.txt")]
        public async Task SaveReport_RefusesTheInstallation(string file)
        {
            world.AddCommunityInstallation(Root, Product.NeoEE);
            await installations.RefreshAsync();

            ReportSaveResult result = model.SaveReport(file, "text");

            Assert.That(result.Outcome, Is.EqualTo(ReportSaveOutcome.InsideInstallation));
            Assert.That(world.FileSystem.FileExists(file), Is.False);
        }

        [Test]
        public void SaveReport_AFailure_IsAResult()
        {
            const string file = @"C:\Users\Player\Documents\report.txt";
            world.FileSystem.AddDirectory(@"C:\Users\Player\Documents");
            world.FileSystem.FailOn(file + FileSystemExtensions.TemporaryFileSuffix, FileSystemOperation.Write, FileSystemStatus.AccessDenied);

            ReportSaveResult result = model.SaveReport(file, "text");

            Assert.That(result.Outcome, Is.EqualTo(ReportSaveOutcome.Failed));
            Assert.That(result.Problem, Is.Not.Empty);
        }

        /// <summary>The report says whether a DirectX wrapper is installed (ADR 0014, design review), for each game installed.</summary>
        [Test]
        public async Task TheDirectXWrapper_OfEachGame()
        {
            world.AddCommunityInstallation(Root, Product.NeoEE);
            world.AddForeignInstallation(@"D:\Games\Empire Earth");
            world.FileSystem.AddFile(@"D:\Games\Empire Earth\D3D8.dll", "dll");
            await installations.RefreshAsync();
            Installation foreign = installations.Result.Installations.Single(installation => installation.Kind == InstallationKind.Foreign);

            var wrappers = DiagnosticsModel.DirectXWrappers(foreign, world.FileSystem);

            Assert.That(wrappers.Select(wrapper => wrapper.Key), Is.EqualTo(new[] { Game.EmpireEarth }), "no AoC folder, no AoC line");
            Assert.That(wrappers[0].Value.Reason, Is.EqualTo(RasterizerReason.WrapperFile));
            Assert.That(DiagnosticsModel.DirectXWrappers(InstallationWorld.ByRoot(installations.Result, Root), world.FileSystem)
                                        .Select(wrapper => wrapper.Key),
                Is.EqualTo(new[] { Game.EmpireEarth, Game.ArtOfConquest }));
        }

        [Test]
        public void ACopy_IsLoggedWithoutItsText()
        {
            model.ReportCopied("secret line 1\r\nsecret line 2\r\n");

            Assert.That(world.Logger.Messages.Last(), Does.Contain("copied to the clipboard (3 lines)"));
            Assert.That(world.Logger.Messages, Has.None.Contains("secret"));
        }
    }
}

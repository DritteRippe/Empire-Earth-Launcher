using System;
using System.Linq;
using System.Threading;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Diagnostics;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Core.Repair;
using Empire_Earth_Launcher.Tests.Core.Diagnostics;
using Empire_Earth_Launcher.Tests.Fakes;
using Empire_Earth_Launcher.Tests.TestSupport;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Launcher
{
    /// <summary>
    /// The texts of the network diagnostics and the report (<see cref="Texts"/>, L-WP9) in English, the neutral language, for
    /// results of the real network diagnostics over fakes: every verdict and every hint has its text, the details show
    /// addresses only as the privacy rules allow (ADR 0013 plan review). German and French have the same keys
    /// (<c>ResourceParityTests</c>).
    /// </summary>
    [TestFixture]
    [SetUICulture("en-US")]
    public class NetworkTextsTests
    {
        private const string Root = @"C:\Program Files (x86)\Neo Empire Earth";

        private InstallationWorld world;
        private Installation installation;
        private FakeNetworkInfo network;
        private FakeHttpsClient https;
        private FakeNeoStatusServer statusServer;

        [SetUp]
        public void SetUp()
        {
            world = new InstallationWorld();
            world.AddCommunityInstallation(Root, Product.NeoEE);
            installation = InstallationWorld.ByRoot(world.Discover(), Root);
            network = new FakeNetworkInfo().WithHomeEthernet().Resolve(NetworkDiagnosticsTests.StatusHost, "192.0.2.10");
            https = new FakeHttpsClient().Answer(UpdateApi.QueryUrl(InstallationWorld.NeoEEAppId, "game"), 200, "2.0.1.0");
            statusServer = new FakeNeoStatusServer();
        }

        private NetworkReport Run(string appId = InstallationWorld.NeoEEAppId, bool configured = true)
        {
            var anonymizer = new ReportAnonymizer(new PrivateNames("Player", null, null, null, null));
            return new NetworkDiagnostics(network, configured ? statusServer : null, https, world.FileSystem,
                    new EffectivePathResolver(world.FileSystem, null, new string[0]), anonymizer, world.Clock, new RecordingLogger())
                .RunAsync(installation, appId, CancellationToken.None).GetAwaiter().GetResult();
        }

        [Test]
        public void TheServerAnswers()
        {
            Assert.That(Texts.NetworkVerdict(Run()), Does.StartWith("The NeoEE server answers (2 players online)."));
        }

        /// <summary>The outage hint of forum report section 8 row 9, in the words of the plan.</summary>
        [Test]
        public void TheOutage_IsNotYourComputer()
        {
            statusServer.Failure = new TimeoutException("injected");

            Assert.That(Texts.NetworkVerdict(Run()), Does.StartWith("Probably a server outage, not your computer"));
        }

        [Test]
        public void EveryVerdict_HasItsOwnText()
        {
            statusServer.Failure = new TimeoutException("injected");
            string outage = Texts.NetworkVerdict(Run());
            https = new FakeHttpsClient();
            string noServer = Texts.NetworkVerdict(Run());
            network.FailLookup(NetworkDiagnosticsTests.StatusHost, DnsOutcome.Failed);
            string noConnection = Texts.NetworkVerdict(Run());
            https = new FakeHttpsClient().Answer(UpdateApi.QueryUrl(InstallationWorld.NeoEEAppId, "game"), 404, "");
            string notResolved = Texts.NetworkVerdict(Run());
            network.Resolve(NetworkDiagnosticsTests.StatusHost, "192.0.2.10");
            string undetermined = Texts.NetworkVerdict(Run(appId: null));
            string notConfigured = Texts.NetworkVerdict(Run(configured: false));
            statusServer.Failure = null;
            string answers = Texts.NetworkVerdict(Run());

            var texts = new[] { outage, noServer, noConnection, notResolved, undetermined, notConfigured, answers };
            Assert.That(texts.Distinct().Count(), Is.EqualTo(Enum.GetValues(typeof(OutageVerdict)).Length));
            Assert.That(notResolved, Does.Contain(NetworkDiagnosticsTests.StatusHost));
            Assert.That(noServer, Does.Contain("firewall"));
        }

        [Test]
        public void EveryHint_HasItsOwnText()
        {
            var texts = Enum.GetValues(typeof(NetworkHintCode)).Cast<NetworkHintCode>()
                            .Select(code => Texts.NetworkHintText(new NetworkHint(code, Game.ArtOfConquest, 2))).ToList();

            Assert.That(texts.Distinct().Count(), Is.EqualTo(texts.Count));
            Assert.That(texts.All(text => !text.Contains("{")), Is.True);
            Assert.That(Texts.NetworkHintText(new NetworkHint(NetworkHintCode.CdKeyCheckNotTrue, Game.ArtOfConquest)),
                Does.StartWith("WONLobby.cfg of The Art of Conquest: CDKeyCheck is not true."));
        }

        /// <summary>ADR 0013 plan review: a public address is shown as its class, IPv6 only as a class, never a MAC or adapter name.</summary>
        [Test]
        public void TheDetails_ShowOnlyWhatMayBeShown()
        {
            network.AddAdapter(NetworkAdapterKind.Tunnel, "Sample VPN Client", new[] { "198.51.100.77/24", "2001:db8::77/64" },
                id: "{AAAAAAAA-0000-0000-0000-000000000001}", name: "Office VPN", mac: "AA-BB-CC-DD-EE-FF");
            world.FileSystem.AddFile(Root + @"\Empire Earth\NeoEE.cfg", GameConfigReadersTests.NeoEeCfg);
            world.FileSystem.AddFile(Root + @"\Empire Earth\WONLobby.cfg", GameConfigReadersTests.WonLobbyCfg);
            world.FileSystem.AddFile(Root + @"\Empire Earth\upnp_info.txt", "ExternalIPAddress = 203.0.113.9\r\n");

            string details = Texts.NetworkDetails(Run());

            Assert.That(details, Does.Contain("Ethernet \"Sample Ethernet Controller\": IPv4 192.168.178.20/24, gateway 192.168.178.1, IPv6 link-local only"));
            Assert.That(details, Does.Contain("Tunnel (VPN) \"Sample VPN Client\": IPv4 public address, gateway none, IPv6 available, virtual or VPN"));
            Assert.That(details, Does.Contain("Name lookup " + NetworkDiagnosticsTests.StatusHost + ": OK"));
            Assert.That(details, Does.Contain("Update server: answers (HTTP 200)"));
            Assert.That(details, Does.Contain("NeoEE status server " + NetworkDiagnosticsTests.StatusHost + ":10005: answers, 2 players online"));
            Assert.That(details, Does.Contain("Empire Earth, NeoEE.cfg: RIP hosting on, server rip.neoee.example, game port 33334, relay ports 33340, port check on, UPnP on"));
            Assert.That(details, Does.Contain("Empire Earth, WONLobby.cfg: CDKeyCheck true, file transfer port 33335, lobby port 33336"));
            Assert.That(details, Does.Contain("Empire Earth, upnp_info.txt: external address public address, local address none"));
            Assert.That(details, Does.Contain("The Art of Conquest, NeoEE.cfg: not found"));
            Assert.That(details, Does.Contain("Port forwarding for hosting (Empire Earth, The Art of Conquest): 33334 TCP+UDP, 33335 TCP, 33336 TCP+UDP to 192.168.178.20."));
            foreach (string secret in new[] { "198.51.100.77", "2001:db8::77", "203.0.113.9", "AA-BB-CC", "Office VPN", "AAAAAAAA" })
                Assert.That(details, Does.Not.Contain(secret), secret);
        }

        [Test]
        public void TheSaveResults()
        {
            Assert.That(Texts.ReportSaved(new ReportSaveResult(ReportSaveOutcome.Saved, @"C:\Users\Player\Documents\r.txt", null)),
                Is.EqualTo(@"The report was saved: C:\Users\Player\Documents\r.txt"));
            Assert.That(Texts.ReportSaved(new ReportSaveResult(ReportSaveOutcome.InsideInstallation, @"C:\x", null)),
                Does.StartWith("The report is not saved in a game folder"));
            Assert.That(Texts.ReportSaved(new ReportSaveResult(ReportSaveOutcome.Failed, @"C:\x", "Access denied")),
                Is.EqualTo("The report could not be saved: Access denied"));
        }
    }
}

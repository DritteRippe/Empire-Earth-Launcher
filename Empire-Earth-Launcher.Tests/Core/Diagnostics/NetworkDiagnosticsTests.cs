using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Diagnostics;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Logging;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Core.Repair;
using Empire_Earth_Launcher.Tests.Fakes;
using Empire_Earth_Launcher.Tests.TestSupport;
using Empire_Earth_WON;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.Diagnostics
{
    /// <summary>A status server without a network: it answers with a player list or fails, and counts the requests.</summary>
    internal sealed class FakeNeoStatusServer : INeoStatusServer
    {
        private int requests;

        public FakeNeoStatusServer(string host = NetworkDiagnosticsTests.StatusHost)
        {
            Host = host;
        }

        public string Host { get; }

        public string Endpoint
        {
            get { return Host + ":10005"; }
        }

        /// <summary>Null: the server answers; otherwise the error it fails with.</summary>
        public Exception Failure { get; set; }

        public int Requests
        {
            get { return requests; }
        }

        public bool TryGetConnectedPlayers(out NeoApiClient.ConnectedPlayersMessage message, out Exception error)
        {
            Interlocked.Increment(ref requests);
            if (Failure != null)
            {
                message = null;
                error = Failure;
                return false;
            }
            message = NeoApiClient.ConnectedPlayersMessage.Parse(new[] { "2", "SamplePlayerOne", "1", "0", "SamplePlayerTwo", "2", "1" });
            error = null;
            return true;
        }
    }

    /// <summary>
    /// <see cref="NetworkDiagnostics"/> (R7, ARCHITECTURE 4.6) with fakes: what it asks (only DNS, the update API with the
    /// request of contract 4.3, and the status server; no service for the public address, nothing to 10002/10003), what it
    /// reads (both game folders, read-only), the outage verdict, the port table, the hints, and the privacy of its log lines
    /// (ADR 0013 plan review).
    /// </summary>
    [TestFixture]
    public class NetworkDiagnosticsTests
    {
        internal const string StatusHost = "status.neoee.example";
        private const string Root = @"C:\Program Files (x86)\Neo Empire Earth";
        private const string EeFolder = Root + @"\Empire Earth";
        private const string AocFolder = Root + @"\Empire Earth - The Art of Conquest";
        private const string VirtualStore = @"C:\Users\Player\AppData\Local\VirtualStore";

        private InstallationWorld world;
        private Installation installation;
        private FakeNetworkInfo network;
        private FakeNeoStatusServer statusServer;
        private FakeHttpsClient https;
        private RecordingLogger logger;
        private string query;

        [SetUp]
        public void SetUp()
        {
            world = new InstallationWorld();
            world.AddCommunityInstallation(Root, Product.NeoEE);
            installation = InstallationWorld.ByRoot(world.Discover(), Root);
            foreach (string folder in new[] { EeFolder, AocFolder })
            {
                world.FileSystem.AddFile(folder + @"\NeoEE.cfg", GameConfigReadersTests.NeoEeCfg);
                world.FileSystem.AddFile(folder + @"\WONLobby.cfg", GameConfigReadersTests.WonLobbyCfg);
            }
            network = new FakeNetworkInfo().WithHomeEthernet().Resolve(StatusHost, "192.0.2.10").Resolve("rip.neoee.example", "192.0.2.11");
            statusServer = new FakeNeoStatusServer();
            query = SetupDownloadLocator.QueryUrl(InstallationWorld.NeoEEAppId);
            https = new FakeHttpsClient().Answer(query, 200, SetupDownloadLocator.FixedPageUrl);
            logger = new RecordingLogger();
        }

        private NetworkDiagnostics Diagnostics(INeoStatusServer server)
        {
            return new NetworkDiagnostics(network, server, https, new WriteForbiddingFileSystem(world.FileSystem),
                new EffectivePathResolver(world.FileSystem, VirtualStore, new[] { @"C:\Program Files (x86)" }),
                new ReportAnonymizer(new PrivateNames("Player", new[] { "PLAYER-PC" }, null, @"C:\Users\Player",
                    @"C:\Users\Player\AppData\Local")),
                world.Clock, logger);
        }

        private NetworkReport Run(Installation selected = null, string appId = InstallationWorld.NeoEEAppId, bool configured = true)
        {
            return Diagnostics(configured ? statusServer : null)
                   .RunAsync(selected ?? installation, appId, CancellationToken.None).GetAwaiter().GetResult();
        }

        [Test]
        public void AHomeComputerWithNeoEE_TheServerAnswers()
        {
            NetworkReport report = Run();

            Assert.That(report.Verdict, Is.EqualTo(OutageVerdict.ServerAnswers));
            Assert.That(report.OnlinePlayers, Is.EqualTo(2));
            Assert.That(report.Hints, Is.Empty);
            Assert.That(report.StatusEndpoint, Is.EqualTo(StatusHost + ":10005"));
            Assert.That(report.NeoEeConfigs.Select(config => config.Game), Is.EqualTo(new[] { Game.EmpireEarth, Game.ArtOfConquest }));
            Assert.That(report.NeoEeConfigs.All(config => config.Active == true && config.DefaultPort == 33334), Is.True);
            Assert.That(report.WonLobbyConfigs.All(config => config.CdKeyCheck == true), Is.True);
            Assert.That(report.UpnpInfos.All(info => info.Status == UpnpInfoStatus.Missing), Is.True);
            Assert.That(report.PortForwarding.Select(table => table.ToString()),
                Is.EqualTo(new[] { "33334 TCP+UDP, 33335 TCP, 33336 TCP+UDP", "33334 TCP+UDP, 33335 TCP, 33336 TCP+UDP" }));
            Assert.That(report.PortForwarding[0].Ports.All(port => port.FromFile), Is.True);
            Assert.That(report.ForwardingTarget, Is.EqualTo(IPAddress.Parse("192.168.178.20")));
            Assert.That(report.CheckedAt, Is.EqualTo(world.Clock.Now));
        }

        /// <summary>ADR 0008, ARCHITECTURE 10: DNS of the NeoEE servers, the request of contract 4.3, the player list; nothing else.</summary>
        [Test]
        public void ItAsks_OnlyDnsTheUpdateApiAndTheStatusServer()
        {
            Run();

            Assert.That(network.Lookups, Is.EqualTo(new[] { StatusHost, "rip.neoee.example" }), "each server once, the status server first");
            Assert.That(https.Requests, Is.EqualTo(new[] { "https://api.empireearth.eu/setup/?product=" + InstallationWorld.NeoEEAppId }));
            Assert.That(statusServer.Requests, Is.EqualTo(1));
            Assert.That(network.AdapterReads, Is.EqualTo(1));
        }

        /// <summary>Forum report section 8 row 9: DNS works, the update API answers (any status), the status server does not.</summary>
        [TestCase(200)]
        [TestCase(404)]
        [TestCase(503)]
        public void TheOutageHint_EndToEnd(int apiStatus)
        {
            statusServer.Failure = new TimeoutException("injected");
            https = new FakeHttpsClient().Answer(query, apiStatus, string.Empty);

            NetworkReport report = Run();

            Assert.That(report.Verdict, Is.EqualTo(OutageVerdict.ProbablyServerOutage));
            Assert.That(report.UpdateApi, Is.EqualTo(UpdateApiAnswer.Answered));
            Assert.That(report.StatusServer, Is.EqualTo(StatusServerAnswer.NoAnswer));
            Assert.That(report.StatusError, Is.EqualTo("TimeoutException"));
            Assert.That(logger.Messages, Has.Some.Contains("probably a server outage, not this computer"));
        }

        [TestCase(HttpsOutcome.Timeout, true, OutageVerdict.NoServerReached)]
        [TestCase(HttpsOutcome.TlsError, true, OutageVerdict.NoServerReached)]
        [TestCase(HttpsOutcome.NetworkError, false, OutageVerdict.NoConnection)]
        public void WithoutTheUpdateApi_ItIsNoOutage(HttpsOutcome failure, bool dns, OutageVerdict expected)
        {
            statusServer.Failure = new System.Net.Sockets.SocketException(10060);
            https = new FakeHttpsClient().Fail(query, failure);
            if (!dns)
                network.FailLookup(StatusHost, DnsOutcome.Failed);

            NetworkReport report = Run();

            Assert.That(report.Verdict, Is.EqualTo(expected));
            Assert.That(report.StatusHostResolves, Is.EqualTo(dns));
        }

        [Test]
        public void ANameThatDoesNotResolve_WhileTheUpdateApiAnswers()
        {
            statusServer.Failure = new System.Net.Sockets.SocketException(11001);
            network.FailLookup(StatusHost, DnsOutcome.NotFound);

            Assert.That(Run().Verdict, Is.EqualTo(OutageVerdict.ServerNameNotResolved));
        }

        /// <summary>ADR 0008: the AppId is the only thing the launcher may send; without one the update API is not asked.</summary>
        [Test]
        public void WithoutAnAppId_TheUpdateApiIsNotAsked()
        {
            statusServer.Failure = new TimeoutException("injected");

            NetworkReport report = Run(appId: null);

            Assert.That(https.Requests, Is.Empty);
            Assert.That(report.UpdateApi, Is.EqualTo(UpdateApiAnswer.NotAsked));
            Assert.That(report.Verdict, Is.EqualTo(OutageVerdict.Undetermined));
        }

        [Test]
        public void WithoutAnInstallation_OnlyTheComputerIsChecked()
        {
            NetworkReport report = Diagnostics(statusServer).RunAsync(null, null, CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(report.NeoEeConfigs, Is.Empty);
            Assert.That(report.WonLobbyConfigs, Is.Empty);
            Assert.That(network.Lookups, Is.EqualTo(new[] { StatusHost }));
            Assert.That(report.PortForwarding.Single().ToString(), Is.EqualTo("33334 TCP+UDP, 33335 TCP, 33336 TCP+UDP"));
            Assert.That(report.PortForwarding.Single().Ports.Any(port => port.FromFile), Is.False, "the defaults of the forum");
            Assert.That(report.Verdict, Is.EqualTo(OutageVerdict.ServerAnswers));
        }

        [Test]
        public void WithoutStatusServerSettings_ItSaysSo()
        {
            NetworkReport report = Run(configured: false);

            Assert.That(report.Verdict, Is.EqualTo(OutageVerdict.StatusServerNotConfigured));
            Assert.That(report.StatusEndpoint, Is.Null);
            Assert.That(network.Lookups, Is.EqualTo(new[] { "rip.neoee.example" }));
        }

        [Test]
        public void ThePortTable_ComesFromTheFiles()
        {
            world.FileSystem.AddFile(EeFolder + @"\NeoEE.cfg", "DefaultPort:\t40000\r\n");
            world.FileSystem.AddFile(EeFolder + @"\WONLobby.cfg", "EEFileTransferPort: 40001\r\nLobbyPort: 40002\r\n");

            NetworkReport report = Run();

            Assert.That(report.PortForwarding[0].ToString(), Is.EqualTo("40000 TCP+UDP, 40001 TCP, 40002 TCP+UDP"));
            Assert.That(report.PortForwarding[0].SamePortsAs(report.PortForwarding[1]), Is.False);
        }

        // --- Hints ----------------------------------------------------------------------------------------------------

        private static IEnumerable<NetworkHintCode> Codes(NetworkReport report)
        {
            return report.Hints.Select(hint => hint.Code);
        }

        /// <summary>Forum 4.10: Hamachi and VPN adapters; the lobby and the game must use the real one.</summary>
        [Test]
        public void AVpnAdapter_IsMarked()
        {
            network.AddAdapter(NetworkAdapterKind.Ethernet, "Sample Hamachi Virtual Ethernet Adapter", new[] { "25.1.2.3/8" });
            network.AddAdapter(NetworkAdapterKind.Tunnel, "Sample VPN Client", new[] { "10.8.0.2/24" });
            network.AddAdapter(NetworkAdapterKind.Wireless, "Sample Virtual Wi-Fi", new[] { "169.254.1.1/16" });

            NetworkReport report = Run();

            Assert.That(Codes(report), Is.EqualTo(new[] { NetworkHintCode.VirtualAdapters }));
            Assert.That(report.Hints.Single().Count, Is.EqualTo(2), "an adapter with only a link-local address is not counted");
            Assert.That(report.ForwardingTarget, Is.EqualTo(IPAddress.Parse("192.168.178.20")), "the real adapter");
            Assert.That(NetworkDiagnostics.Describe(report.Adapters.Adapters[1]), Does.EndWith("virtual or VPN"));
        }

        [Test]
        public void CableAndWifi_BothWithAGateway()
        {
            network.AddAdapter(NetworkAdapterKind.Wireless, "Sample Wireless", new[] { "192.168.178.21/24" }, new[] { "192.168.178.1" });

            NetworkReport report = Run();

            Assert.That(Codes(report), Is.EqualTo(new[] { NetworkHintCode.SeveralAdapters }));
            Assert.That(report.ForwardingTarget, Is.Null, "two candidates: the text names no address");
        }

        [Test]
        public void IPv6Only_AndOffline()
        {
            network = new FakeNetworkInfo().AddAdapter(NetworkAdapterKind.Ethernet, "Sample Ethernet", new[] { "fe80::1/64", "2001:db8::20/64" },
                new[] { "fe80::1" }).Resolve(StatusHost, "2001:db8::10");
            Assert.That(Codes(Run()), Is.EqualTo(new[] { NetworkHintCode.IPv6Only }));

            network = new FakeNetworkInfo().AddAdapter(NetworkAdapterKind.Ethernet, "Sample Ethernet", new[] { "192.168.178.20/24" },
                new[] { "192.168.178.1" }, isUp: false);
            Assert.That(Codes(Run()), Is.EqualTo(new[] { NetworkHintCode.NoConnection }));
        }

        [Test]
        public void AdaptersThatCannotBeListed_GiveNoAdapterHint()
        {
            network.AdapterProblem = "NetworkInformationException: injected";

            NetworkReport report = Run();

            Assert.That(report.Hints, Is.Empty);
            Assert.That(logger.MessagesOf(LogLevel.Warning), Has.Some.Contains("could not be listed"));
        }

        /// <summary>t=11057 p=48100: CGNAT and DS-Lite make port forwarding impossible; a private external address is double NAT.</summary>
        [TestCase("WAN IP: 100.64.12.13", false, NetworkHintCode.CgnatAddress)]
        [TestCase("WAN IP: 100.64.12.13", true, NetworkHintCode.DsLite)]
        [TestCase("external address = 0.0.0.0", true, NetworkHintCode.DsLite)]
        [TestCase("external address = 0.0.0.0", false, NetworkHintCode.NoExternalIPv4)]
        [TestCase("ExternalIPAddress = 192.168.0.2", false, NetworkHintCode.PrivateExternalAddress)]
        public void TheExternalAddressOfUpnpInfo(string line, bool globalIPv6, NetworkHintCode expected)
        {
            world.FileSystem.AddFile(EeFolder + @"\upnp_info.txt", line + "\r\n");
            if (globalIPv6)
                network.AddAdapter(NetworkAdapterKind.Wireless, "Sample Wireless", new[] { "2001:db8::30/64" }, isUp: true);

            NetworkReport report = Run();

            Assert.That(Codes(report), Is.EqualTo(new[] { expected }));
        }

        [Test]
        public void APublicExternalAddress_GivesNoHint()
        {
            world.FileSystem.AddFile(EeFolder + @"\upnp_info.txt", "ExternalIPAddress = 203.0.113.45\r\n");

            Assert.That(Run().Hints, Is.Empty);
        }

        [Test]
        public void RipHostingOff_AndCdKeyCheckOff_PerGame()
        {
            world.FileSystem.AddFile(AocFolder + @"\NeoEE.cfg", "Active: false\r\n");
            world.FileSystem.AddFile(EeFolder + @"\WONLobby.cfg", "CDKeyCheck: false\r\n");

            NetworkReport report = Run();

            Assert.That(report.Hints.Select(hint => hint.ToString()), Is.EqualTo(new[] { "RipHostingOff (AoC)", "CdKeyCheckNotTrue (EE)" }));
        }

        /// <summary>Forum 4.3: in EE without NeoEE <c>CDKeyCheck: false</c> is normal, so there is no hint.</summary>
        [Test]
        public void CdKeyCheckOff_InEE_IsNoHint()
        {
            var eeWorld = new InstallationWorld();
            eeWorld.AddCommunityInstallation(@"C:\Games\Empire Earth", Product.EE, artOfConquest: false);
            Installation ee = InstallationWorld.ByRoot(eeWorld.Discover(), @"C:\Games\Empire Earth");
            world.FileSystem.AddFile(ee.EeFolder + @"\WONLobby.cfg", "CDKeyCheck: false\r\n");

            NetworkReport report = Run(ee, InstallationWorld.EEAppId);

            Assert.That(report.WonLobbyConfigs.Single().CdKeyCheck, Is.False);
            Assert.That(report.Hints, Is.Empty);
        }

        // --- Privacy and safety -----------------------------------------------------------------------------------------

        /// <summary>
        /// ADR 0013 plan review, for the log lines of the network diagnostics: no MAC, adapter GUID, adapter name, DNS suffix,
        /// public or external address, IPv6 address or user name, whatever the fake holds.
        /// </summary>
        [Test]
        public void TheLogLines_KeepThePrivacyRules()
        {
            network.AddAdapter(NetworkAdapterKind.Wireless, "Sample Wireless Adapter", new[] { "198.51.100.77/24", "2001:db8:abcd::77/64" },
                new[] { "198.51.100.1" }, id: "{6F1E2D3C-AAAA-BBBB-CCCC-123456789ABC}", name: "Wohnzimmer-WLAN von Player",
                mac: "AA-BB-CC-DD-EE-FF", dnsSuffix: "player-family.example");
            world.FileSystem.AddFile(VirtualStore + @"\Program Files (x86)\Neo Empire Earth\Empire Earth\upnp_info.txt",
                "ExternalIPAddress = 203.0.113.99\r\n");
            world.FileSystem.AddFile(VirtualStore + @"\Program Files (x86)\Neo Empire Earth\Empire Earth\NeoEE.cfg",
                GameConfigReadersTests.NeoEeCfg);

            Run();

            string log = string.Join("\n", logger.Messages);
            foreach (string secret in new[]
                     {
                         "6F1E2D3C", "Wohnzimmer", "AA-BB-CC", "AABBCC", "player-family", "198.51.100.77", "198.51.100.1",
                         "2001:db8:abcd", "203.0.113.99", @"Users\Player", "PLAYER-PC"
                     })
                Assert.That(log, Does.Not.Contain(secret), secret);
            Assert.That(log, Does.Contain("<public address>"));
            Assert.That(log, Does.Contain(@"%LOCALAPPDATA%\VirtualStore\Program Files (x86)\Neo Empire Earth\Empire Earth\NeoEE.cfg"));
            Assert.That(log, Does.Contain("external address public"));
        }

        [Test]
        public void TheGameFolders_AreOnlyRead()
        {
            // The diagnostics run on a WriteForbiddingFileSystem: any write would fail the test.
            Assert.That(() => Run(), Throws.Nothing);
        }

        [Test]
        public void ACancelledCheck_Ends()
        {
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();
                Assert.That(() => Diagnostics(statusServer).RunAsync(installation, InstallationWorld.NeoEEAppId, cancellation.Token)
                                                           .GetAwaiter().GetResult(),
                    Throws.InstanceOf<OperationCanceledException>());
            }
        }

        [Test]
        public void RunAsync_WithASlowAdapterList_ReturnsAnUnfinishedTaskAtOnce()
        {
            // ADR 0004: the adapters and the files are read on the thread pool, not on the caller's (UI) thread.
            using (var gate = new ManualResetEventSlim())
            {
                network.AdapterGate = gate;

                Task<NetworkReport> task = Diagnostics(statusServer).RunAsync(installation, InstallationWorld.NeoEEAppId,
                    CancellationToken.None);

                Assert.That(task.IsCompleted, Is.False, "the caller must not wait for the adapters");
                Assert.That(network.AdaptersEntered.Wait(TimeSpan.FromSeconds(10)), Is.True, "the check runs elsewhere");
                Assert.That(task.IsCompleted, Is.False);
                gate.Set();
                Assert.That(task.Wait(TimeSpan.FromSeconds(10)), Is.True);
                Assert.That(task.Result.Verdict, Is.EqualTo(OutageVerdict.ServerAnswers));
            }
        }

        [TestCase(NetworkAdapterKind.Tunnel, "Anything", true)]
        [TestCase(NetworkAdapterKind.Ppp, "Anything", true)]
        [TestCase(NetworkAdapterKind.Ethernet, "LogMeIn Hamachi Virtual Ethernet Adapter", true)]
        [TestCase(NetworkAdapterKind.Ethernet, "TAP-Windows Adapter V9", true)]
        [TestCase(NetworkAdapterKind.Ethernet, "Hyper-V Virtual Ethernet Adapter", true)]
        [TestCase(NetworkAdapterKind.Ethernet, "VirtualBox Host-Only Ethernet Adapter", true)]
        [TestCase(NetworkAdapterKind.Other, "WireGuard Tunnel", true)]
        [TestCase(NetworkAdapterKind.Ethernet, "Intel(R) Ethernet Connection I219-V", false)]
        [TestCase(NetworkAdapterKind.Wireless, "Realtek RTL8822CE 802.11ac PCIe Adapter", false)]
        public void VirtualAdapters_ByTypeOrDriverName(NetworkAdapterKind kind, string description, bool expected)
        {
            var adapter = new NetworkAdapter("id", "name", description, kind, true, "mac", new AdapterAddress[0], new IPAddress[0], "");

            Assert.That(NetworkDiagnostics.IsVirtual(adapter), Is.EqualTo(expected));
        }

        [Test]
        public void AnAdapterLine_ShowsWhatMayBeShown()
        {
            var adapter = new NetworkAdapter("{GUID}", "My Network", "Sample Ethernet", NetworkAdapterKind.Ethernet, true, "AA-BB",
                new[] { new AdapterAddress(IPAddress.Parse("192.168.1.20"), 24), new AdapterAddress(IPAddress.Parse("2001:db8::1"), 64) },
                new[] { IPAddress.Parse("192.168.1.1") }, "suffix.example");

            Assert.That(NetworkDiagnostics.Describe(adapter),
                Is.EqualTo("Ethernet \"Sample Ethernet\", connected, IPv4 192.168.1.20/24, gateway 192.168.1.1, IPv6 global"));
        }
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Logging;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Core.Repair;
using Empire_Earth_WON;

namespace Empire_Earth_Launcher.Core.Diagnostics
{
    /// <summary>The NeoEE status server, asked once by the network diagnostics (the request of the online player list).</summary>
    public interface INeoStatusServer
    {
        /// <summary>The host name, for the name lookup.</summary>
        string Host { get; }

        /// <summary><c>host:port</c>, for the log and the report.</summary>
        string Endpoint { get; }

        /// <summary>Requests the connected players. Never throws for network or protocol errors.</summary>
        bool TryGetConnectedPlayers(out NeoApiClient.ConnectedPlayersMessage message, out Exception error);
    }

    /// <summary><see cref="INeoStatusServer"/> of the configured server (<see cref="NeoApiClient"/>, plain TCP, public data).</summary>
    public sealed class NeoStatusServer : INeoStatusServer
    {
        private readonly NeoApiClient client;

        public NeoStatusServer(NeoApiClient client)
        {
            this.client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public string Host
        {
            get { return client.Endpoint.Host; }
        }

        public string Endpoint
        {
            get { return client.Endpoint.ToString(); }
        }

        public bool TryGetConnectedPlayers(out NeoApiClient.ConnectedPlayersMessage message, out Exception error)
        {
            return client.TryGetConnectedPlayers(out message, out error);
        }
    }

    /// <summary>The hints of the network diagnostics (forum 4.9 to 4.11, 4.19), each with its text and advice in the UI.</summary>
    public enum NetworkHintCode
    {
        /// <summary>No connected adapter has an IPv4 gateway and there is no IPv6 internet either: the computer seems offline.</summary>
        NoConnection,

        /// <summary>No connected adapter has an IPv4 gateway, but there is a global IPv6 address: the game needs IPv4.</summary>
        IPv6Only,

        /// <summary>
        /// Connected virtual or VPN adapters with IPv4 (Hamachi, VPN clients, virtual machines): the lobby and the game must use
        /// the same, real network adapter, a virtual one can crash the game (forum 4.10).
        /// </summary>
        VirtualAdapters,

        /// <summary>Several connected adapters with an IPv4 gateway (cable and Wi-Fi): the lobby and the game must use the same one (forum 4.10).</summary>
        SeveralAdapters,

        /// <summary><c>upnp_info.txt</c>: the router's external address is in 100.64.0.0/10, the provider shares it (CGNAT, t=11057 p=48100).</summary>
        CgnatAddress,

        /// <summary>
        /// <c>upnp_info.txt</c>: the router has a CGNAT address or none (0.0.0.0) and the computer has IPv6 internet: probably
        /// DS-Lite, where port forwarding for IPv4 cannot work (t=11057 p=48100).
        /// </summary>
        DsLite,

        /// <summary><c>upnp_info.txt</c>: the router reports no external IPv4 address (0.0.0.0).</summary>
        NoExternalIPv4,

        /// <summary><c>upnp_info.txt</c>: the router's external address is private: a second router or modem in front (double NAT, forum 4.9).</summary>
        PrivateExternalAddress,

        /// <summary><c>NeoEE.cfg</c>: <c>Active: false</c>, RIP hosting is off (it hosts most games without forwarding, t=5843 p=39204).</summary>
        RipHostingOff,

        /// <summary><c>WONLobby.cfg</c> of NeoEE: <c>CDKeyCheck</c> is not <c>true</c> (t=10950 p=47202); the setup's repair installs it again.</summary>
        CdKeyCheckNotTrue
    }

    /// <summary>One hint: its code, the game of a file hint, and the number of adapters of an adapter hint.</summary>
    public sealed class NetworkHint
    {
        internal NetworkHint(NetworkHintCode code, Game game = null, int count = 0)
        {
            Code = code;
            Game = game;
            Count = count;
        }

        public NetworkHintCode Code { get; }

        /// <summary>The game whose file the hint is about; null for the computer.</summary>
        public Game Game { get; }

        /// <summary>For the adapter hints: how many adapters.</summary>
        public int Count { get; }

        public override string ToString()
        {
            return Code + (Game == null ? string.Empty : " (" + Game.Id + ")") +
                   (Count == 0 ? string.Empty : " (" + Count.ToString(CultureInfo.InvariantCulture) + ")");
        }
    }

    /// <summary>A port a host forwards to this computer (forum 4.9: 33334 and 33336 TCP+UDP, 33335 TCP).</summary>
    public sealed class PortForward
    {
        internal PortForward(int port, bool tcp, bool udp, string source, bool fromFile)
        {
            Port = port;
            Tcp = tcp;
            Udp = udp;
            Source = source;
            FromFile = fromFile;
        }

        public int Port { get; }

        public bool Tcp { get; }

        public bool Udp { get; }

        /// <summary>The value it comes from: <c>NeoEE.cfg DefaultPort</c>, <c>WONLobby.cfg EEFileTransferPort</c> or <c>WONLobby.cfg LobbyPort</c>.</summary>
        public string Source { get; }

        /// <summary>True if the file named the port; false for the default of the forum.</summary>
        public bool FromFile { get; }

        /// <summary><c>TCP+UDP</c>, <c>TCP</c> or <c>UDP</c>.</summary>
        public string Protocols
        {
            get { return Tcp && Udp ? "TCP+UDP" : Tcp ? "TCP" : "UDP"; }
        }

        public override string ToString()
        {
            return Port.ToString(CultureInfo.InvariantCulture) + " " + Protocols;
        }
    }

    /// <summary>The port forwarding table of one game folder, from its <c>NeoEE.cfg</c> and <c>WONLobby.cfg</c>.</summary>
    public sealed class PortForwarding
    {
        /// <summary>The game port of the forum (33334, t=11057 p=48100), also <c>DefaultPort</c> of NeoEE.cfg.</summary>
        public const int DefaultGamePort = 33334;

        /// <summary>The file transfer port of the forum (33335), also <c>EEFileTransferPort</c> of WONLobby.cfg.</summary>
        public const int DefaultFileTransferPort = 33335;

        /// <summary>The lobby port of the forum (33336), also <c>LobbyPort</c> of WONLobby.cfg.</summary>
        public const int DefaultLobbyPort = 33336;

        internal PortForwarding(Game game, IEnumerable<PortForward> ports)
        {
            Game = game;
            Ports = new ReadOnlyCollection<PortForward>(ports.ToList());
        }

        public Game Game { get; }

        /// <summary>The game port (TCP+UDP), the file transfer port (TCP) and the lobby port (TCP+UDP), in this order.</summary>
        public IReadOnlyList<PortForward> Ports { get; }

        /// <summary>
        /// The table from the values of the files, else the defaults of the forum (33334 and 33336 TCP+UDP, 33335 TCP, forum 4.9:
        /// t=4266 p=30400, t=2586 p=17377, t=5496 p=36591).
        /// </summary>
        public static PortForwarding From(Game game, NeoEeConfig neoEe, WonLobbyConfig wonLobby)
        {
            int? gamePort = neoEe?.DefaultPort;
            int? fileTransferPort = wonLobby?.FileTransferPort;
            int? lobbyPort = wonLobby?.LobbyPort;
            return new PortForwarding(game, new[]
            {
                new PortForward(gamePort ?? DefaultGamePort, true, true, "NeoEE.cfg DefaultPort", gamePort != null),
                new PortForward(fileTransferPort ?? DefaultFileTransferPort, true, false, "WONLobby.cfg EEFileTransferPort",
                    fileTransferPort != null),
                new PortForward(lobbyPort ?? DefaultLobbyPort, true, true, "WONLobby.cfg LobbyPort", lobbyPort != null)
            });
        }

        /// <summary>True if both tables name the same ports and protocols.</summary>
        public bool SamePortsAs(PortForwarding other)
        {
            return other != null && Ports.Select(port => port.ToString()).SequenceEqual(other.Ports.Select(port => port.ToString()));
        }

        public override string ToString()
        {
            return string.Join(", ", Ports.Select(port => port.ToString()));
        }
    }

    /// <summary>The result of <see cref="NetworkDiagnostics.RunAsync"/>: what was asked and found, the outage verdict and the hints.</summary>
    public sealed class NetworkReport
    {
        internal NetworkReport(DateTime checkedAt, Installation installation, NetworkAdapters adapters, string statusEndpoint,
            IEnumerable<DnsLookup> lookups, bool statusHostResolves, UpdateApiAnswer updateApi, HttpsResponse updateApiResponse,
            StatusServerAnswer statusServer, int? onlinePlayers, string statusError, OutageVerdict verdict,
            IEnumerable<NeoEeConfig> neoEeConfigs, IEnumerable<WonLobbyConfig> wonLobbyConfigs, IEnumerable<UpnpInfo> upnpInfos,
            IEnumerable<PortForwarding> portForwarding, IPAddress forwardingTarget, IEnumerable<NetworkHint> hints)
        {
            CheckedAt = checkedAt;
            Installation = installation;
            Adapters = adapters;
            StatusEndpoint = statusEndpoint;
            Lookups = new ReadOnlyCollection<DnsLookup>(lookups.ToList());
            StatusHostResolves = statusHostResolves;
            UpdateApi = updateApi;
            UpdateApiResponse = updateApiResponse;
            StatusServer = statusServer;
            OnlinePlayers = onlinePlayers;
            StatusError = statusError;
            Verdict = verdict;
            NeoEeConfigs = new ReadOnlyCollection<NeoEeConfig>(neoEeConfigs.ToList());
            WonLobbyConfigs = new ReadOnlyCollection<WonLobbyConfig>(wonLobbyConfigs.ToList());
            UpnpInfos = new ReadOnlyCollection<UpnpInfo>(upnpInfos.ToList());
            PortForwarding = new ReadOnlyCollection<PortForwarding>(portForwarding.ToList());
            ForwardingTarget = forwardingTarget;
            Hints = new ReadOnlyCollection<NetworkHint>(hints.ToList());
        }

        /// <summary>When the check ran (local time of the computer).</summary>
        public DateTime CheckedAt { get; }

        /// <summary>The installation whose game folders were read; null without one.</summary>
        public Installation Installation { get; }

        public NetworkAdapters Adapters { get; }

        /// <summary><c>host:port</c> of the status server; null if it is not configured.</summary>
        public string StatusEndpoint { get; }

        /// <summary>The name lookups: the status server first, then the servers of <c>NeoEE.cfg</c>.</summary>
        public IReadOnlyList<DnsLookup> Lookups { get; }

        public bool StatusHostResolves { get; }

        public UpdateApiAnswer UpdateApi { get; }

        /// <summary>The answer or error of the update API (never shown with its body); null if it was not asked.</summary>
        public HttpsResponse UpdateApiResponse { get; }

        public StatusServerAnswer StatusServer { get; }

        /// <summary>The number of online players the status server reported; null without an answer.</summary>
        public int? OnlinePlayers { get; }

        /// <summary>The type of the error of the status server, for the log and the report; null otherwise.</summary>
        public string StatusError { get; }

        public OutageVerdict Verdict { get; }

        /// <summary><c>NeoEE.cfg</c> of the EE folder and, if the installation has one, the AoC folder.</summary>
        public IReadOnlyList<NeoEeConfig> NeoEeConfigs { get; }

        /// <summary><c>WONLobby.cfg</c> of the EE folder and the AoC folder.</summary>
        public IReadOnlyList<WonLobbyConfig> WonLobbyConfigs { get; }

        /// <summary><c>upnp_info.txt</c> of the EE folder and the AoC folder.</summary>
        public IReadOnlyList<UpnpInfo> UpnpInfos { get; }

        /// <summary>The forwarding table of each game folder (the defaults of the forum without an installation).</summary>
        public IReadOnlyList<PortForwarding> PortForwarding { get; }

        /// <summary>The private IPv4 address the ports go to (the one real adapter with a gateway); null if not exactly one.</summary>
        public IPAddress ForwardingTarget { get; }

        public IReadOnlyList<NetworkHint> Hints { get; }

        /// <summary>True if the computer has a global IPv6 address on a connected adapter.</summary>
        public bool HasGlobalIPv6
        {
            get { return Adapters.Adapters.Where(adapter => adapter.IsUp).Any(NetworkDiagnostics.HasGlobalIPv6); }
        }
    }

    /// <summary>
    /// The network diagnostics of R7 (ARCHITECTURE 4.6), only on request: the adapters (IPv4, gateway, type, virtual and VPN
    /// adapters), the name lookups of the NeoEE status server and of the server in <c>NeoEE.cfg</c>, the update API, the status
    /// server, <c>NeoEE.cfg</c> and <c>WONLobby.cfg</c> of both game folders (read-only) with the port forwarding table,
    /// <c>upnp_info.txt</c>, the hints and the outage verdict (forum report section 8 rows 8 and 9).
    /// </summary>
    /// <remarks>
    /// <para>
    /// It connects to nothing but what the launcher already asks (ADR 0008, ARCHITECTURE 10): DNS, the update API with a
    /// request of contract 4.5 (the latest game version: only the AppId and <c>&amp;type=game</c> are sent) and the status server with the request of the player list. No "what is
    /// my IP" service, no connection to the auth or firewall ports of NeoEE (10002, 10003). The lookups and both requests run
    /// at the same time.
    /// </para>
    /// <para>
    /// The log lines follow the privacy rules of the report (ADR 0013 plan review): adapters by type and driver name, no MAC,
    /// GUID or adapter name, IPv4 only where it may be shown, IPv6 and the external address only as their class, paths through
    /// <see cref="ReportAnonymizer"/>.
    /// </para>
    /// </remarks>
    public sealed class NetworkDiagnostics
    {
        private static readonly string[] VirtualAdapterWords =
        {
            "virtual", "vpn", "hamachi", "tap-windows", "tap-win32", "tap adapter", "wintun", "wireguard", "openvpn", "zerotier",
            "tailscale", "radmin", "nordlynx", "anyconnect", "fortinet", "vmware", "virtualbox", "hyper-v", "vethernet",
            "npcap", "loopback", "tunnel", "pangp", "globalprotect", "softether"
        };

        private readonly INetworkInfo network;
        private readonly INeoStatusServer statusServer;
        private readonly IHttpsClient https;
        private readonly IFileSystem fileSystem;
        private readonly EffectivePathResolver effectivePaths;
        private readonly ReportAnonymizer anonymizer;
        private readonly IClock clock;
        private readonly ILogger logger;

        /// <param name="network">The adapters and the name lookups.</param>
        /// <param name="statusServer">The NeoEE status server; null if its settings are invalid.</param>
        /// <param name="https">The HTTPS client of the update API (ADR 0008).</param>
        /// <param name="fileSystem">Reads the configuration files of the game folders.</param>
        /// <param name="effectivePaths">The files the game really uses (ADR 0016).</param>
        /// <param name="anonymizer">The privacy rules of the log lines.</param>
        /// <param name="clock">The time of the check.</param>
        /// <param name="logger">Log of the launcher.</param>
        public NetworkDiagnostics(INetworkInfo network, INeoStatusServer statusServer, IHttpsClient https, IFileSystem fileSystem,
            EffectivePathResolver effectivePaths, ReportAnonymizer anonymizer, IClock clock, ILogger logger)
        {
            this.network = network ?? throw new ArgumentNullException(nameof(network));
            this.statusServer = statusServer;
            this.https = https ?? throw new ArgumentNullException(nameof(https));
            this.fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            this.effectivePaths = effectivePaths ?? throw new ArgumentNullException(nameof(effectivePaths));
            this.anonymizer = anonymizer ?? throw new ArgumentNullException(nameof(anonymizer));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>Runs the network diagnostics once; never throws for a network or file problem.</summary>
        /// <param name="installation">The installation whose game folders are read; null without one.</param>
        /// <param name="appId">The AppId for the request to the update API (contract 4.5); null if no installation has one.</param>
        /// <param name="cancellationToken">Ends the check with <see cref="OperationCanceledException"/>.</param>
        /// <remarks>
        /// The whole check runs on the thread pool (ADR 0004): the adapter list of Windows (slow with VPN and virtual
        /// adapters) and the configuration files (a game folder on a network drive) are read before the first request, so
        /// the caller, the UI thread, gets an unfinished task at once.
        /// </remarks>
        public Task<NetworkReport> RunAsync(Installation installation, string appId, CancellationToken cancellationToken)
        {
            return Task.Run(() => RunOnThreadPoolAsync(installation, appId, cancellationToken), cancellationToken);
        }

        private async Task<NetworkReport> RunOnThreadPoolAsync(Installation installation, string appId,
            CancellationToken cancellationToken)
        {
            DateTime checkedAt = clock.Now;
            logger.Info("Network diagnostics: started on request.");

            NetworkAdapters adapters = network.GetAdapters();
            LogAdapters(adapters);

            var neoEeConfigs = new List<NeoEeConfig>();
            var wonLobbyConfigs = new List<WonLobbyConfig>();
            var upnpInfos = new List<UpnpInfo>();
            var forwarding = new List<PortForwarding>();
            foreach (Game game in GamesOf(installation))
            {
                string folder = installation.GetGameFolder(game);
                NeoEeConfig neoEe = NeoEeConfigReader.Read(fileSystem, effectivePaths, folder, game);
                WonLobbyConfig wonLobby = WonLobbyConfigReader.Read(fileSystem, effectivePaths, folder, game);
                UpnpInfo upnp = UpnpInfoParser.Read(fileSystem, effectivePaths, folder, game);
                neoEeConfigs.Add(neoEe);
                wonLobbyConfigs.Add(wonLobby);
                upnpInfos.Add(upnp);
                forwarding.Add(PortForwarding.From(game, neoEe, wonLobby));
                LogFiles(game, neoEe, wonLobby, upnp);
            }
            if (forwarding.Count == 0)
                forwarding.Add(PortForwarding.From(Game.EmpireEarth, null, null));

            // The name lookups, the update API and the status server at the same time.
            List<string> hosts = HostsToLookUp(neoEeConfigs);
            Task<DnsLookup[]> lookups = Task.WhenAll(hosts.Select(host => network.ResolveAsync(host, cancellationToken)));
            // A request of contract 4.5 (the latest game version); the query without &type= is no longer sent (contract 4.3).
            string query = string.IsNullOrWhiteSpace(appId) ? null : UpdateApi.QueryUrl(appId, UpdateChecker.TypeName(VersionKind.Game));
            Task<HttpsResponse> updateApi = query == null
                ? Task.FromResult<HttpsResponse>(null)
                : https.GetAsync(new Uri(query), cancellationToken);
            Task<StatusResult> status = statusServer == null
                ? Task.FromResult<StatusResult>(null)
                : Task.Run(() => AskStatusServer(), cancellationToken);
            await Task.WhenAll(lookups, updateApi, status).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            DnsLookup[] lookupResults = lookups.Result;
            foreach (DnsLookup lookup in lookupResults)
                logger.Info("Network diagnostics: DNS " + lookup.Host + ": " + lookup + ".");
            bool statusHostResolves = statusServer != null && lookupResults.Length > 0 && lookupResults[0].IsResolved;

            HttpsResponse apiResponse = updateApi.Result;
            UpdateApiAnswer apiAnswer = apiResponse == null ? UpdateApiAnswer.NotAsked
                : apiResponse.Outcome == HttpsOutcome.Answered ? UpdateApiAnswer.Answered : UpdateApiAnswer.NoAnswer;
            if (apiResponse == null)
                logger.Info("Network diagnostics: the update API is not asked (no installation with an AppId).");
            else
                logger.Info("Update API: GET " + query + ": " + apiResponse + ".");

            StatusResult statusResult = status.Result;
            StatusServerAnswer statusAnswer = statusResult == null ? StatusServerAnswer.NotConfigured
                : statusResult.Message != null ? StatusServerAnswer.Answered : StatusServerAnswer.NoAnswer;
            string statusError = statusResult?.Error?.GetType().Name;
            if (statusResult == null)
                logger.Info("Network diagnostics: the NeoEE status server is not configured.");
            else if (statusResult.Message != null)
                logger.Info("Network diagnostics: the NeoEE status server " + statusServer.Endpoint + " answered (" +
                            statusResult.Message.OnlinePlayers.ToString(CultureInfo.InvariantCulture) + " players online).");
            else
                logger.Info("Network diagnostics: the NeoEE status server " + statusServer.Endpoint + " did not answer (" +
                            (statusError ?? "no reason") + ").");

            OutageVerdict verdict = OutageHint.Evaluate(statusHostResolves, apiAnswer, statusAnswer);
            IPAddress target = ForwardingTargetOf(adapters);
            List<NetworkHint> hints = HintsFor(installation, adapters, neoEeConfigs, wonLobbyConfigs, upnpInfos);
            logger.Info("Network diagnostics: " + OutageHint.Describe(verdict) + "; port forwarding " +
                        string.Join("; ", forwarding.Select(table => table.Game.Id + " " + table)) + " to " +
                        (target == null ? "the IPv4 address of this computer" : ReportAnonymizer.Address(target)) +
                        (hints.Count == 0 ? "; no hints." : "; hints: " + string.Join(", ", hints) + "."));

            return new NetworkReport(checkedAt, installation, adapters, statusServer?.Endpoint, lookupResults,
                statusHostResolves, apiAnswer, apiResponse, statusAnswer, statusResult?.Message?.OnlinePlayers, statusError, verdict,
                neoEeConfigs, wonLobbyConfigs, upnpInfos, forwarding, target, hints);
        }

        private sealed class StatusResult
        {
            public NeoApiClient.ConnectedPlayersMessage Message;
            public Exception Error;
        }

        private StatusResult AskStatusServer()
        {
            bool answered = statusServer.TryGetConnectedPlayers(out NeoApiClient.ConnectedPlayersMessage message, out Exception error);
            return new StatusResult { Message = answered ? message : null, Error = answered ? null : error };
        }

        private static IEnumerable<Game> GamesOf(Installation installation)
        {
            if (installation == null)
                yield break;
            yield return Game.EmpireEarth;
            if (installation.HasArtOfConquest)
                yield return Game.ArtOfConquest;
        }

        /// <summary>The status server first, then every other server of <c>NeoEE.cfg</c> (each name once).</summary>
        private List<string> HostsToLookUp(IEnumerable<NeoEeConfig> configs)
        {
            var hosts = new List<string>();
            if (statusServer != null)
                hosts.Add(statusServer.Host);
            foreach (string server in configs.Select(config => config.Server).Where(server => server != null))
            {
                if (!hosts.Contains(server, StringComparer.OrdinalIgnoreCase))
                    hosts.Add(server);
            }
            return hosts;
        }

        /// <summary>True for a virtual or VPN adapter: a tunnel, PPP, or a driver name of a known virtual adapter (forum 4.10).</summary>
        public static bool IsVirtual(NetworkAdapter adapter)
        {
            if (adapter == null)
                throw new ArgumentNullException(nameof(adapter));
            if (adapter.Kind == NetworkAdapterKind.Tunnel || adapter.Kind == NetworkAdapterKind.Ppp)
                return true;
            string description = adapter.Description.ToLowerInvariant();
            return VirtualAdapterWords.Any(word => description.Contains(word));
        }

        /// <summary>True if the adapter has an IPv4 default gateway (it reaches the internet over IPv4).</summary>
        public static bool HasIPv4Gateway(NetworkAdapter adapter)
        {
            return adapter.Gateways.Any(gateway => gateway.AddressFamily == AddressFamily.InterNetwork &&
                                                   AddressClassifier.ClassOf(gateway) != IPv4Class.Unspecified);
        }

        /// <summary>True if the adapter has a global IPv6 address.</summary>
        public static bool HasGlobalIPv6(NetworkAdapter adapter)
        {
            return adapter.Addresses.Any(address => AddressClassifier.IsGlobalIPv6(address.Address));
        }

        /// <summary>
        /// One English line for the log and the report: type, driver name, state, IPv4 with prefix where it may be shown (else
        /// its class), gateway, IPv6 class, virtual. Never the adapter name, GUID, MAC or DNS suffix (ADR 0013 plan review).
        /// </summary>
        public static string Describe(NetworkAdapter adapter)
        {
            if (adapter == null)
                throw new ArgumentNullException(nameof(adapter));
            List<string> ipv4 = adapter.Addresses.Where(address => address.IsIPv4)
                .Select(address => ReportAnonymizer.Address(address.Address) +
                                   (AddressClassifier.MayShow(address.Address) && address.PrefixLength > 0
                                       ? "/" + address.PrefixLength.ToString(CultureInfo.InvariantCulture)
                                       : string.Empty))
                .ToList();
            List<string> gateways = adapter.Gateways.Where(gateway => gateway.AddressFamily == AddressFamily.InterNetwork)
                .Select(ReportAnonymizer.Address).ToList();
            IPv6Class ipv6 = AddressClassifier.IPv6ClassOf(adapter.Addresses.Select(address => address.Address));
            return KindName(adapter.Kind) + " \"" + adapter.Description + "\", " + (adapter.IsUp ? "connected" : "not connected") +
                   ", IPv4 " + (ipv4.Count == 0 ? "none" : string.Join(" ", ipv4)) +
                   ", gateway " + (gateways.Count == 0 ? "none" : string.Join(" ", gateways)) +
                   ", IPv6 " + ReportAnonymizer.ClassName(ipv6) + (IsVirtual(adapter) ? ", virtual or VPN" : string.Empty);
        }

        /// <summary>The English name of an adapter type.</summary>
        public static string KindName(NetworkAdapterKind kind)
        {
            switch (kind)
            {
                case NetworkAdapterKind.Ethernet:
                    return "Ethernet";
                case NetworkAdapterKind.Wireless:
                    return "Wi-Fi";
                case NetworkAdapterKind.Tunnel:
                    return "Tunnel";
                case NetworkAdapterKind.Ppp:
                    return "PPP";
                case NetworkAdapterKind.MobileBroadband:
                    return "Mobile broadband";
                default:
                    return "Other";
            }
        }

        /// <summary>The private IPv4 of the only connected, real adapter with an IPv4 gateway; null if there is not exactly one.</summary>
        private static IPAddress ForwardingTargetOf(NetworkAdapters adapters)
        {
            List<NetworkAdapter> real = adapters.Adapters.Where(adapter => adapter.IsUp && !IsVirtual(adapter) && HasIPv4Gateway(adapter))
                                                .ToList();
            if (real.Count != 1)
                return null;
            return real[0].Addresses.Where(address => address.IsIPv4 && AddressClassifier.ClassOf(address.Address) == IPv4Class.Private)
                          .Select(address => address.Address).FirstOrDefault();
        }

        /// <summary>The hints of the adapters, of <c>upnp_info.txt</c> and of the configuration files.</summary>
        internal static List<NetworkHint> HintsFor(Installation installation, NetworkAdapters adapters,
            IReadOnlyList<NeoEeConfig> neoEeConfigs, IReadOnlyList<WonLobbyConfig> wonLobbyConfigs, IReadOnlyList<UpnpInfo> upnpInfos)
        {
            var hints = new List<NetworkHint>();
            List<NetworkAdapter> up = adapters.Adapters.Where(adapter => adapter.IsUp).ToList();
            bool globalIPv6 = up.Any(HasGlobalIPv6);
            if (adapters.Problem == null)
            {
                if (!up.Any(HasIPv4Gateway))
                    hints.Add(new NetworkHint(globalIPv6 ? NetworkHintCode.IPv6Only : NetworkHintCode.NoConnection));
                int virtualAdapters = up.Count(adapter => IsVirtual(adapter) && adapter.Addresses.Any(address => address.IsIPv4 &&
                    AddressClassifier.ClassOf(address.Address) != IPv4Class.LinkLocal));
                if (virtualAdapters > 0)
                    hints.Add(new NetworkHint(NetworkHintCode.VirtualAdapters, count: virtualAdapters));
                int realWithGateway = up.Count(adapter => !IsVirtual(adapter) && HasIPv4Gateway(adapter));
                if (realWithGateway > 1)
                    hints.Add(new NetworkHint(NetworkHintCode.SeveralAdapters, count: realWithGateway));
            }

            IPv4Class? external = upnpInfos.Select(info => info.ExternalAddressClass).FirstOrDefault(value => value != null);
            if (external == IPv4Class.Cgnat)
                hints.Add(new NetworkHint(globalIPv6 ? NetworkHintCode.DsLite : NetworkHintCode.CgnatAddress));
            else if (external == IPv4Class.Unspecified)
                hints.Add(new NetworkHint(globalIPv6 ? NetworkHintCode.DsLite : NetworkHintCode.NoExternalIPv4));
            else if (external == IPv4Class.Private)
                hints.Add(new NetworkHint(NetworkHintCode.PrivateExternalAddress));

            foreach (NeoEeConfig config in neoEeConfigs.Where(config => config.Active == false))
                hints.Add(new NetworkHint(NetworkHintCode.RipHostingOff, config.Game));
            if (installation != null)
            {
                foreach (WonLobbyConfig config in wonLobbyConfigs.Where(config => config.NeedsCdKeyCheckHint(installation.Product)))
                    hints.Add(new NetworkHint(NetworkHintCode.CdKeyCheckNotTrue, config.Game));
            }
            return hints;
        }

        private void LogAdapters(NetworkAdapters adapters)
        {
            if (adapters.Problem != null)
            {
                logger.Warning("Network diagnostics: the network adapters could not be listed (" + adapters.Problem + ").");
                return;
            }
            logger.Info("Network diagnostics: " + ReportAnonymizer.Count(adapters.Adapters.Count, "network adapter", "network adapters") +
                        (adapters.Adapters.Count == 0 ? "." : ":"));
            foreach (NetworkAdapter adapter in adapters.Adapters)
                logger.Info("Network diagnostics: adapter " + Describe(adapter) + ".");
        }

        private void LogFiles(Game game, NeoEeConfig neoEe, WonLobbyConfig wonLobby, UpnpInfo upnp)
        {
            logger.Info("Network diagnostics: " + game.Id + " " + NeoEeConfigReader.FileName + " (" + anonymizer.Path(neoEe.Path) + "): " +
                        DescribeValues(neoEe) + ".");
            logger.Info("Network diagnostics: " + game.Id + " " + WonLobbyConfigReader.FileName + " (" + anonymizer.Path(wonLobby.Path) +
                        "): " + DescribeValues(wonLobby) + ".");
            logger.Info("Network diagnostics: " + game.Id + " " + UpnpInfoParser.FileName + ": " + DescribeValues(upnp) + ".");
        }

        /// <summary>The values of <c>NeoEE.cfg</c> in one English line (the report uses it too).</summary>
        public static string DescribeValues(NeoEeConfig config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));
            if (config.Status != ConfigFileStatus.Read)
                return config.Status == ConfigFileStatus.Missing ? "missing" : "unreadable";
            return "Active " + Value(config.Active) + ", Server " + (config.Server ?? "?") + ", DefaultPort " + Value(config.DefaultPort) +
                   ", MemberPorts " + Value(config.MemberPorts) + ", PortCheck " + Value(config.PortCheck) + ", TryUPnP " +
                   Value(config.TryUpnp) + (config.InvalidKeys.Count == 0 ? string.Empty : ", invalid: " + string.Join(" ", config.InvalidKeys)) +
                   (config.IsVirtualStoreCopy ? ", VirtualStore copy" : string.Empty);
        }

        /// <summary>The values of <c>WONLobby.cfg</c> in one English line.</summary>
        public static string DescribeValues(WonLobbyConfig config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));
            if (config.Status != ConfigFileStatus.Read)
                return config.Status == ConfigFileStatus.Missing ? "missing" : "unreadable";
            return "CDKeyCheck " + (config.CdKeyCheckInvalid ? "invalid" : Value(config.CdKeyCheck)) + ", EEFileTransferPort " +
                   Value(config.FileTransferPort) + ", LobbyPort " + Value(config.LobbyPort) +
                   (config.IsVirtualStoreCopy ? ", VirtualStore copy" : string.Empty);
        }

        /// <summary>What <c>upnp_info.txt</c> said in one English line: never the external address, only its class.</summary>
        public static string DescribeValues(UpnpInfo info)
        {
            if (info == null)
                throw new ArgumentNullException(nameof(info));
            switch (info.Status)
            {
                case UpnpInfoStatus.Missing:
                    return "missing";
                case UpnpInfoStatus.Unreadable:
                    return "unreadable";
                case UpnpInfoStatus.UnknownFormat:
                    return "unknown format";
            }
            var parts = new List<string>
            {
                "external address " + (info.ExternalAddressClass == null ? "not found" : ReportAnonymizer.ClassName(info.ExternalAddressClass.Value)),
                "local address " + (info.LocalAddress == null ? "not found" : ReportAnonymizer.Address(info.LocalAddress))
            };
            parts.AddRange(info.Ports.Select(port => port.Port.ToString(CultureInfo.InvariantCulture) + " " + port.Protocol + " " +
                                                     (port.Succeeded == true ? "ok" : port.Succeeded == false ? "failed" : "?")));
            return string.Join(", ", parts);
        }

        private static string Value(bool? value)
        {
            return value == null ? "?" : value.Value ? "true" : "false";
        }

        private static string Value(int? value)
        {
            return value == null ? "?" : value.Value.ToString(CultureInfo.InvariantCulture);
        }
    }
}

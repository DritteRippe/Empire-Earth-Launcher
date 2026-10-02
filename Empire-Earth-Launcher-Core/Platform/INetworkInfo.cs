using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Empire_Earth_Launcher.Core.Platform
{
    /// <summary>What kind of network adapter Windows reports (its interface type, grouped).</summary>
    public enum NetworkAdapterKind
    {
        /// <summary>Wired Ethernet (every Ethernet speed Windows names).</summary>
        Ethernet,

        /// <summary>Wi-Fi (802.11).</summary>
        Wireless,

        /// <summary>A tunnel: VPN clients, Teredo, 6to4, IP-HTTPS.</summary>
        Tunnel,

        /// <summary>A dial-up or PPP connection (also some VPN clients and mobile broadband).</summary>
        Ppp,

        /// <summary>Mobile broadband (3G/4G/5G).</summary>
        MobileBroadband,

        /// <summary>Anything else.</summary>
        Other
    }

    /// <summary>One unicast address of an adapter with its prefix length (24 for 255.255.255.0; 0 if unknown).</summary>
    public sealed class AdapterAddress
    {
        public AdapterAddress(IPAddress address, int prefixLength)
        {
            Address = address ?? throw new ArgumentNullException(nameof(address));
            if (prefixLength < 0 || prefixLength > 128)
                throw new ArgumentOutOfRangeException(nameof(prefixLength), prefixLength, "A prefix length is between 0 and 128.");
            PrefixLength = prefixLength;
        }

        public IPAddress Address { get; }

        public int PrefixLength { get; }

        public bool IsIPv4
        {
            get { return Address.AddressFamily == AddressFamily.InterNetwork; }
        }

        public bool IsIPv6
        {
            get { return Address.AddressFamily == AddressFamily.InterNetworkV6; }
        }
    }

    /// <summary>
    /// One network adapter as Windows reports it (R7, forum 4.10). It carries what Windows gives, also what identifies a
    /// computer or its owner (<see cref="Id"/>, <see cref="Name"/>, <see cref="PhysicalAddress"/>, <see cref="DnsSuffix"/>):
    /// those are never shown in the diagnostics report and never logged (ADR 0013 plan review); an adapter is described by
    /// its <see cref="Kind"/> and <see cref="Description"/> (the driver name).
    /// </summary>
    public sealed class NetworkAdapter
    {
        public NetworkAdapter(string id, string name, string description, NetworkAdapterKind kind, bool isUp,
            string physicalAddress, IEnumerable<AdapterAddress> addresses, IEnumerable<IPAddress> gateways, string dnsSuffix)
        {
            Id = id ?? string.Empty;
            Name = name ?? string.Empty;
            Description = description ?? string.Empty;
            Kind = kind;
            IsUp = isUp;
            PhysicalAddress = physicalAddress ?? string.Empty;
            Addresses = new ReadOnlyCollection<AdapterAddress>(
                (addresses ?? throw new ArgumentNullException(nameof(addresses))).ToList());
            Gateways = new ReadOnlyCollection<IPAddress>((gateways ?? throw new ArgumentNullException(nameof(gateways))).ToList());
            DnsSuffix = dnsSuffix ?? string.Empty;
        }

        /// <summary>The adapter GUID of Windows. Never shown, never logged.</summary>
        public string Id { get; }

        /// <summary>The name the player can change ("Ethernet 2", "WLAN Home"). Never shown, never logged.</summary>
        public string Name { get; }

        /// <summary>The description of the driver ("Intel(R) Ethernet Connection I219-V", "TAP-Windows Adapter V9").</summary>
        public string Description { get; }

        public NetworkAdapterKind Kind { get; }

        /// <summary>True if Windows reports the adapter as up (connected).</summary>
        public bool IsUp { get; }

        /// <summary>The MAC address. Never shown, never logged.</summary>
        public string PhysicalAddress { get; }

        /// <summary>The unicast addresses, IPv4 and IPv6.</summary>
        public IReadOnlyList<AdapterAddress> Addresses { get; }

        /// <summary>The default gateways, IPv4 and IPv6.</summary>
        public IReadOnlyList<IPAddress> Gateways { get; }

        /// <summary>The connection-specific DNS suffix (often a domain name). Never shown, never logged.</summary>
        public string DnsSuffix { get; }
    }

    /// <summary>The adapters Windows reported, or why they could not be listed.</summary>
    public sealed class NetworkAdapters
    {
        private NetworkAdapters(IEnumerable<NetworkAdapter> adapters, string problem)
        {
            Adapters = new ReadOnlyCollection<NetworkAdapter>(adapters.ToList());
            Problem = problem;
        }

        /// <summary>The adapters, without the loopback adapter; empty if they could not be listed.</summary>
        public IReadOnlyList<NetworkAdapter> Adapters { get; }

        /// <summary>Why the list is unknown (the error of Windows, for the log); null if it was read.</summary>
        public string Problem { get; }

        public static NetworkAdapters Listed(IEnumerable<NetworkAdapter> adapters)
        {
            return new NetworkAdapters(adapters ?? throw new ArgumentNullException(nameof(adapters)), null);
        }

        public static NetworkAdapters Failed(string problem)
        {
            if (string.IsNullOrEmpty(problem))
                throw new ArgumentException("A failure has a reason.", nameof(problem));
            return new NetworkAdapters(Enumerable.Empty<NetworkAdapter>(), problem);
        }
    }

    /// <summary>How a name lookup ended.</summary>
    public enum DnsOutcome
    {
        /// <summary>The name has at least one address.</summary>
        Resolved,

        /// <summary>The DNS server answered that the name does not exist (or has no address).</summary>
        NotFound,

        /// <summary>No answer within the time limit.</summary>
        Timeout,

        /// <summary>Any other error (no network, no DNS server reachable).</summary>
        Failed
    }

    /// <summary>The result of <see cref="INetworkInfo.ResolveAsync"/>: the addresses of a server name, or why there are none.</summary>
    public sealed class DnsLookup
    {
        private DnsLookup(string host, DnsOutcome outcome, IEnumerable<IPAddress> addresses, string error, TimeSpan duration)
        {
            Host = host;
            Outcome = outcome;
            Addresses = new ReadOnlyCollection<IPAddress>(addresses.ToList());
            Error = error;
            Duration = duration;
        }

        /// <summary>The name that was looked up.</summary>
        public string Host { get; }

        public DnsOutcome Outcome { get; }

        /// <summary>The addresses of a server (public data); empty unless resolved.</summary>
        public IReadOnlyList<IPAddress> Addresses { get; }

        /// <summary>The error type and message for the log; null if resolved.</summary>
        public string Error { get; }

        public TimeSpan Duration { get; }

        public bool IsResolved
        {
            get { return Outcome == DnsOutcome.Resolved; }
        }

        public static DnsLookup Resolved(string host, IEnumerable<IPAddress> addresses, TimeSpan duration)
        {
            var list = (addresses ?? throw new ArgumentNullException(nameof(addresses))).ToList();
            if (list.Count == 0)
                return new DnsLookup(host, DnsOutcome.NotFound, list, "no address", duration);
            return new DnsLookup(host, DnsOutcome.Resolved, list, null, duration);
        }

        public static DnsLookup Unresolved(string host, DnsOutcome outcome, string error, TimeSpan duration)
        {
            if (outcome == DnsOutcome.Resolved)
                throw new ArgumentException("An unresolved lookup needs an outcome other than Resolved.", nameof(outcome));
            return new DnsLookup(host, outcome, Enumerable.Empty<IPAddress>(), error ?? outcome.ToString(), duration);
        }

        /// <summary>One line for the log: the number of addresses, or the error, and the duration.</summary>
        public override string ToString()
        {
            string milliseconds = ((long)Duration.TotalMilliseconds).ToString(CultureInfo.InvariantCulture) + " ms";
            if (IsResolved)
            {
                int ipv4 = Addresses.Count(address => address.AddressFamily == AddressFamily.InterNetwork);
                return string.Format(CultureInfo.InvariantCulture, "resolved ({0} IPv4, {1} IPv6) in {2}", ipv4,
                    Addresses.Count - ipv4, milliseconds);
            }
            return Outcome + " (" + Error + ") after " + milliseconds;
        }
    }

    /// <summary>
    /// The network of the computer for the network diagnostics (R7, ADR 0006): the adapters and name lookups. Nothing else:
    /// no connection, no "what is my IP" service (ADR 0008, ARCHITECTURE 10). Implemented by <see cref="WindowsNetworkInfo"/>
    /// and checked on real Windows by the test plan; the tests use a fake.
    /// </summary>
    public interface INetworkInfo
    {
        /// <summary>The network adapters of the computer, without the loopback adapter. Never throws for a Windows error.</summary>
        NetworkAdapters GetAdapters();

        /// <summary>Looks up the addresses of <paramref name="host"/> (DNS). Never throws for a network error.</summary>
        /// <param name="host">A server name.</param>
        /// <param name="cancellationToken">Ends the wait with <see cref="OperationCanceledException"/>.</param>
        Task<DnsLookup> ResolveAsync(string host, CancellationToken cancellationToken);
    }
}

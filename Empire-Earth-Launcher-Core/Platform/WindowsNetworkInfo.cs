using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Empire_Earth_Launcher.Core.Platform
{
    /// <summary>
    /// <see cref="INetworkInfo"/> on Windows: the adapters from <see cref="NetworkInterface.GetAllNetworkInterfaces"/>, the
    /// name lookups through <see cref="Dns.GetHostAddressesAsync(string)"/> with a time limit of
    /// <see cref="DnsTimeout"/>. It opens no connection.
    /// </summary>
    /// <remarks>
    /// A thin adapter, checked on real Windows by the test plan (WP9-01 to WP9-04); the unit tests use a fake. A Windows error
    /// is a result (ADR 0013). Nothing here logs: the network diagnostics log only what the privacy rules allow (ADR 0013
    /// plan review).
    /// </remarks>
    public sealed class WindowsNetworkInfo : INetworkInfo
    {
        /// <summary>How long a name lookup may take; Windows itself retries for up to about 15 seconds.</summary>
        public static readonly TimeSpan DnsTimeout = TimeSpan.FromSeconds(5);

        public NetworkAdapters GetAdapters()
        {
            try
            {
                var adapters = new List<NetworkAdapter>();
                foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                        continue;
                    IPInterfaceProperties properties = adapter.GetIPProperties();
                    var addresses = properties.UnicastAddresses
                        .Select(unicast => new AdapterAddress(unicast.Address, PrefixLengthOf(unicast)))
                        .ToList();
                    var gateways = properties.GatewayAddresses.Select(gateway => gateway.Address)
                        .Where(address => address != null).ToList();
                    adapters.Add(new NetworkAdapter(adapter.Id, adapter.Name, adapter.Description, KindOf(adapter.NetworkInterfaceType),
                        adapter.OperationalStatus == OperationalStatus.Up, adapter.GetPhysicalAddress()?.ToString(), addresses,
                        gateways, properties.DnsSuffix));
                }
                return NetworkAdapters.Listed(adapters);
            }
            catch (NetworkInformationException ex)
            {
                return NetworkAdapters.Failed(ex.GetType().Name + ": " + ex.Message);
            }
        }

        public async Task<DnsLookup> ResolveAsync(string host, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(host))
                throw new ArgumentException("A host name is required.", nameof(host));
            Stopwatch watch = Stopwatch.StartNew();
            try
            {
                Task<IPAddress[]> lookup = Dns.GetHostAddressesAsync(host);
                Task finished = await Task.WhenAny(lookup, Task.Delay(DnsTimeout, cancellationToken)).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                if (finished != lookup)
                {
                    // The lookup goes on in Windows; its result or error is observed and dropped.
                    _ = lookup.ContinueWith(task => task.Exception, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted,
                        TaskScheduler.Default);
                    return DnsLookup.Unresolved(host, DnsOutcome.Timeout, "no answer within " + DnsTimeout.TotalSeconds + " s",
                        watch.Elapsed);
                }
                return DnsLookup.Resolved(host, await lookup.ConfigureAwait(false), watch.Elapsed);
            }
            catch (SocketException ex)
            {
                DnsOutcome outcome = ex.SocketErrorCode == SocketError.HostNotFound || ex.SocketErrorCode == SocketError.NoData
                    ? DnsOutcome.NotFound
                    : ex.SocketErrorCode == SocketError.TimedOut || ex.SocketErrorCode == SocketError.TryAgain
                        ? DnsOutcome.Timeout
                        : DnsOutcome.Failed;
                return DnsLookup.Unresolved(host, outcome, "SocketException " + ex.SocketErrorCode, watch.Elapsed);
            }
            catch (ArgumentException ex)
            {
                return DnsLookup.Unresolved(host, DnsOutcome.Failed, ex.GetType().Name + ": " + ex.Message, watch.Elapsed);
            }
        }

        /// <summary>The prefix length of an address: from the IPv4 mask (also on old Windows), else from Windows.</summary>
        private static int PrefixLengthOf(UnicastIPAddressInformation unicast)
        {
            if (unicast.Address.AddressFamily == AddressFamily.InterNetwork)
            {
                IPAddress mask = unicast.IPv4Mask;
                if (mask == null)
                    return 0;
                int bits = 0;
                foreach (byte part in mask.GetAddressBytes())
                {
                    for (int bit = 7; bit >= 0 && (part & (1 << bit)) != 0; bit--)
                        bits++;
                }
                return bits;
            }
            try
            {
                return Math.Max(0, Math.Min(128, unicast.PrefixLength));
            }
            catch (NotImplementedException)
            {
                return 0;
            }
        }

        private static NetworkAdapterKind KindOf(NetworkInterfaceType type)
        {
            switch (type)
            {
                case NetworkInterfaceType.Ethernet:
                case NetworkInterfaceType.Ethernet3Megabit:
                case NetworkInterfaceType.FastEthernetFx:
                case NetworkInterfaceType.FastEthernetT:
                case NetworkInterfaceType.GigabitEthernet:
                    return NetworkAdapterKind.Ethernet;
                case NetworkInterfaceType.Wireless80211:
                    return NetworkAdapterKind.Wireless;
                case NetworkInterfaceType.Tunnel:
                    return NetworkAdapterKind.Tunnel;
                case NetworkInterfaceType.Ppp:
                    return NetworkAdapterKind.Ppp;
                case NetworkInterfaceType.Wman:
                case NetworkInterfaceType.Wwanpp:
                case NetworkInterfaceType.Wwanpp2:
                    return NetworkAdapterKind.MobileBroadband;
                default:
                    return NetworkAdapterKind.Other;
            }
        }
    }
}

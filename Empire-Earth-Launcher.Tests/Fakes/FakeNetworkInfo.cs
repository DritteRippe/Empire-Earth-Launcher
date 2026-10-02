using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Empire_Earth_Launcher.Core.Platform;

namespace Empire_Earth_Launcher.Tests.Fakes
{
    /// <summary>
    /// <see cref="INetworkInfo"/> without a network: the adapters a test adds, and name lookups answered from a table (a name
    /// that is not in it is <see cref="DnsOutcome.NotFound"/>). It records every lookup, so a test can check what was asked.
    /// </summary>
    internal sealed class FakeNetworkInfo : INetworkInfo
    {
        private readonly object sync = new object();
        private readonly List<NetworkAdapter> adapters = new List<NetworkAdapter>();
        private readonly Dictionary<string, DnsLookup> answers = new Dictionary<string, DnsLookup>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> lookups = new List<string>();

        /// <summary>If set, <see cref="GetAdapters"/> fails with this reason.</summary>
        public string AdapterProblem { get; set; }

        /// <summary>Every name looked up, in order.</summary>
        public IReadOnlyList<string> Lookups
        {
            get
            {
                lock (sync)
                    return lookups.ToList();
            }
        }

        /// <summary>How often <see cref="GetAdapters"/> was called.</summary>
        public int AdapterReads { get; private set; }

        /// <summary>If set, <see cref="GetAdapters"/> waits for it (a slow adapter list); <see cref="AdaptersEntered"/> is set first.</summary>
        public ManualResetEventSlim AdapterGate { get; set; }

        /// <summary>Set when <see cref="GetAdapters"/> is entered.</summary>
        public ManualResetEventSlim AdaptersEntered { get; } = new ManualResetEventSlim();

        /// <summary>Adds an adapter; addresses as text (<c>192.168.1.20/24</c>, <c>fe80::1/64</c>), gateways as text.</summary>
        public FakeNetworkInfo AddAdapter(NetworkAdapterKind kind, string description, string[] addresses, string[] gateways = null,
            bool isUp = true, string id = "{00000000-0000-0000-0000-000000000001}", string name = "Ethernet",
            string mac = "00-00-00-00-00-01", string dnsSuffix = "")
        {
            adapters.Add(new NetworkAdapter(id, name, description, kind, isUp, mac,
                (addresses ?? new string[0]).Select(ParseAddress), (gateways ?? new string[0]).Select(IPAddress.Parse), dnsSuffix));
            return this;
        }

        /// <summary>The usual home computer: one Ethernet adapter with a private IPv4, a gateway and a link-local IPv6.</summary>
        public FakeNetworkInfo WithHomeEthernet()
        {
            return AddAdapter(NetworkAdapterKind.Ethernet, "Sample Ethernet Controller", new[] { "192.168.178.20/24", "fe80::1/64" },
                new[] { "192.168.178.1" });
        }

        /// <summary><paramref name="host"/> resolves to <paramref name="addresses"/>.</summary>
        public FakeNetworkInfo Resolve(string host, params string[] addresses)
        {
            answers[host] = DnsLookup.Resolved(host, addresses.Select(IPAddress.Parse), TimeSpan.FromMilliseconds(20));
            return this;
        }

        /// <summary>The lookup of <paramref name="host"/> ends with <paramref name="outcome"/>.</summary>
        public FakeNetworkInfo FailLookup(string host, DnsOutcome outcome)
        {
            answers[host] = DnsLookup.Unresolved(host, outcome, "injected " + outcome, TimeSpan.FromMilliseconds(20));
            return this;
        }

        public NetworkAdapters GetAdapters()
        {
            AdaptersEntered.Set();
            AdapterGate?.Wait();
            AdapterReads++;
            return AdapterProblem != null ? NetworkAdapters.Failed(AdapterProblem) : NetworkAdapters.Listed(adapters);
        }

        public Task<DnsLookup> ResolveAsync(string host, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (sync)
                lookups.Add(host);
            return Task.FromResult(answers.TryGetValue(host, out DnsLookup answer)
                ? answer
                : DnsLookup.Unresolved(host, DnsOutcome.NotFound, "not in the table of the fake", TimeSpan.FromMilliseconds(20)));
        }

        private static AdapterAddress ParseAddress(string text)
        {
            string[] parts = text.Split('/');
            return new AdapterAddress(IPAddress.Parse(parts[0]), parts.Length > 1 ? int.Parse(parts[1], CultureInfo.InvariantCulture) : 0);
        }
    }
}

using System;
using System.Linq;
using System.Net;
using System.Threading;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Tests.Fakes;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.Platform
{
    /// <summary>
    /// The result types of <see cref="INetworkInfo"/> (R7) and the fake the diagnostics tests use. <see cref="WindowsNetworkInfo"/>
    /// itself reads the adapters of Windows and asks DNS, so it is checked by the test plan (WP9-01 to WP9-04), never here.
    /// </summary>
    [TestFixture]
    public class NetworkInfoTests
    {
        [Test]
        public void ALookupWithoutAddresses_IsNotFound()
        {
            DnsLookup lookup = DnsLookup.Resolved("example.invalid", Enumerable.Empty<IPAddress>(), TimeSpan.Zero);

            Assert.That(lookup.Outcome, Is.EqualTo(DnsOutcome.NotFound));
            Assert.That(lookup.IsResolved, Is.False);
        }

        [Test]
        public void ALookup_DescribesItselfWithoutTheAddresses()
        {
            DnsLookup lookup = DnsLookup.Resolved("server.example",
                new[] { IPAddress.Parse("192.0.2.10"), IPAddress.Parse("2001:db8::10") }, TimeSpan.FromMilliseconds(12));

            Assert.That(lookup.ToString(), Is.EqualTo("resolved (1 IPv4, 1 IPv6) in 12 ms"));
            Assert.That(DnsLookup.Unresolved("server.example", DnsOutcome.Timeout, "no answer", TimeSpan.FromSeconds(5)).ToString(),
                Is.EqualTo("Timeout (no answer) after 5000 ms"));
        }

        [Test]
        public void AnUnresolvedLookup_CannotBeResolved()
        {
            Assert.Throws<ArgumentException>(() => DnsLookup.Unresolved("x", DnsOutcome.Resolved, null, TimeSpan.Zero));
            Assert.Throws<ArgumentException>(() => NetworkAdapters.Failed(""));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AdapterAddress(IPAddress.Loopback, 129));
        }

        [Test]
        public void AnAdapter_KeepsWhatWindowsReports()
        {
            var adapter = new NetworkAdapter(null, null, null, NetworkAdapterKind.Wireless, true, null,
                new[] { new AdapterAddress(IPAddress.Parse("10.0.0.5"), 8), new AdapterAddress(IPAddress.Parse("fe80::5"), 64) },
                new[] { IPAddress.Parse("10.0.0.1") }, null);

            Assert.That(adapter.Id, Is.Empty);
            Assert.That(adapter.Name, Is.Empty);
            Assert.That(adapter.PhysicalAddress, Is.Empty);
            Assert.That(adapter.DnsSuffix, Is.Empty);
            Assert.That(adapter.Addresses.Count(address => address.IsIPv4), Is.EqualTo(1));
            Assert.That(adapter.Addresses.Count(address => address.IsIPv6), Is.EqualTo(1));
        }

        [Test]
        public void TheFake_AnswersFromItsTable_AndRecordsEveryLookup()
        {
            var network = new FakeNetworkInfo().WithHomeEthernet().Resolve("server.example", "192.0.2.10")
                                               .FailLookup("down.example", DnsOutcome.Timeout);

            Assert.That(network.ResolveAsync("server.example", CancellationToken.None).Result.IsResolved, Is.True);
            Assert.That(network.ResolveAsync("down.example", CancellationToken.None).Result.Outcome, Is.EqualTo(DnsOutcome.Timeout));
            Assert.That(network.ResolveAsync("other.example", CancellationToken.None).Result.Outcome, Is.EqualTo(DnsOutcome.NotFound));
            Assert.That(network.Lookups, Is.EqualTo(new[] { "server.example", "down.example", "other.example" }));
            Assert.That(network.GetAdapters().Adapters.Single().Gateways.Single(), Is.EqualTo(IPAddress.Parse("192.168.178.1")));

            network.AdapterProblem = "NetworkInformationException: injected";
            Assert.That(network.GetAdapters().Adapters, Is.Empty);
            Assert.That(network.GetAdapters().Problem, Does.Contain("injected"));
        }
    }
}

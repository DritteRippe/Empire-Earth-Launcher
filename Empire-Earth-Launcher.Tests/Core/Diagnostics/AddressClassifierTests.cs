using System;
using System.Linq;
using System.Net;
using Empire_Earth_Launcher.Core.Diagnostics;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.Diagnostics
{
    /// <summary>
    /// <see cref="AddressClassifier"/>: which addresses the network diagnostics may show (ADR 0013 plan review) and the classes
    /// behind the hints for CGNAT (100.64.0.0/10), a private external address and IPv6 (forum 4.9, t=11057 p=48100).
    /// </summary>
    [TestFixture]
    public class AddressClassifierTests
    {
        [TestCase("10.0.0.1", IPv4Class.Private, true)]
        [TestCase("172.16.0.1", IPv4Class.Private, true)]
        [TestCase("172.31.255.254", IPv4Class.Private, true)]
        [TestCase("172.32.0.1", IPv4Class.Public, false)]
        [TestCase("192.168.178.20", IPv4Class.Private, true)]
        [TestCase("169.254.10.10", IPv4Class.LinkLocal, true)]
        [TestCase("100.64.0.0", IPv4Class.Cgnat, false)]
        [TestCase("100.127.255.255", IPv4Class.Cgnat, false)]
        [TestCase("100.128.0.1", IPv4Class.Public, false)]
        [TestCase("100.63.255.255", IPv4Class.Public, false)]
        [TestCase("25.1.2.3", IPv4Class.Public, false)]
        [TestCase("203.0.113.45", IPv4Class.Public, false)]
        [TestCase("0.0.0.0", IPv4Class.Unspecified, false)]
        [TestCase("127.0.0.1", IPv4Class.Special, false)]
        [TestCase("224.0.0.1", IPv4Class.Special, false)]
        [TestCase("255.255.255.255", IPv4Class.Special, false)]
        public void IPv4_ClassAndWhetherItMayBeShown(string address, IPv4Class expected, bool mayShow)
        {
            Assert.That(AddressClassifier.ClassOf(IPAddress.Parse(address)), Is.EqualTo(expected));
            Assert.That(AddressClassifier.MayShow(IPAddress.Parse(address)), Is.EqualTo(mayShow));
        }

        [TestCase(new string[0], IPv6Class.None)]
        [TestCase(new[] { "192.168.1.2" }, IPv6Class.None)]
        [TestCase(new[] { "fe80::1" }, IPv6Class.LinkLocalOnly)]
        [TestCase(new[] { "fd00::1", "fe80::2" }, IPv6Class.LinkLocalOnly)]
        [TestCase(new[] { "fe80::1", "2001:db8::5" }, IPv6Class.Global)]
        [TestCase(new[] { "2a00:1450::1" }, IPv6Class.Global)]
        [TestCase(new[] { "::1" }, IPv6Class.LinkLocalOnly)]
        public void IPv6_OnlyAsAClass(string[] addresses, IPv6Class expected)
        {
            Assert.That(AddressClassifier.IPv6ClassOf(addresses.Select(IPAddress.Parse)), Is.EqualTo(expected));
        }

        [Test]
        public void AnIPv6Address_HasNoIPv4Class()
        {
            Assert.Throws<ArgumentException>(() => AddressClassifier.ClassOf(IPAddress.Parse("2001:db8::1")));
        }
    }
}

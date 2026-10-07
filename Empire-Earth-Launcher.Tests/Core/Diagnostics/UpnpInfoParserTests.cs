using System.Linq;
using System.Net;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Diagnostics;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Tests.Fakes;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.Diagnostics
{
    /// <summary>
    /// <see cref="UpnpInfoParser"/>: the format of <c>upnp_info.txt</c> is unknown, so the parser takes what it recognizes in
    /// any layout and says "unknown format" otherwise; the external address leaves it only as its class (ADR 0013 plan
    /// review). The samples are synthetic, with documentation addresses (RFC 5737).
    /// </summary>
    [TestFixture]
    public class UpnpInfoParserTests
    {
        [Test]
        public void ALayoutLikeMiniupnpc_IsRecognized()
        {
            const string text =
                "Found valid IGD : http://192.168.178.1:49000/igddesc.xml\r\n" +
                "Local LAN ip address : 192.168.178.20\r\n" +
                "ExternalIPAddress = 203.0.113.45\r\n" +
                "external 203.0.113.45:33334 TCP is redirected to internal 192.168.178.20:33334 (duration=0)\r\n" +
                "external 203.0.113.45:33334 UDP is redirected to internal 192.168.178.20:33334 (duration=0)\r\n" +
                "AddPortMapping(33335, 33335, 192.168.178.20) failed with code 718 (ConflictInMappingEntry) TCP\r\n";

            UpnpInfo info = UpnpInfoParser.Parse(text, Game.EmpireEarth);

            Assert.That(info.Status, Is.EqualTo(UpnpInfoStatus.Recognized));
            Assert.That(info.ExternalAddressClass, Is.EqualTo(IPv4Class.Public));
            Assert.That(info.LocalAddress, Is.EqualTo(IPAddress.Parse("192.168.178.20")));
            Assert.That(info.Ports.Select(port => port.Port + "/" + port.Protocol + "/" + port.Succeeded),
                Is.EqualTo(new[] { "33334/TCP/True", "33334/UDP/True", "33335/TCP/False" }));
        }

        [TestCase("WAN address: 100.72.10.4", IPv4Class.Cgnat)]
        [TestCase("Public IP=10.1.2.3", IPv4Class.Private)]
        [TestCase("external ip address: 0.0.0.0", IPv4Class.Unspecified)]
        [TestCase("ExternalIP 198.51.100.200", IPv4Class.Public)]
        public void TheExternalAddress_IsKeptAsItsClassOnly(string line, IPv4Class expected)
        {
            UpnpInfo info = UpnpInfoParser.Parse(line, Game.EmpireEarth);

            Assert.That(info.ExternalAddressClass, Is.EqualTo(expected));
            // The result type has no member that holds the external address.
            Assert.That(typeof(UpnpInfo).GetProperties().Where(property => property.PropertyType == typeof(IPAddress))
                                        .Select(property => property.Name), Is.EqualTo(new[] { "LocalAddress" }));
        }

        [TestCase("")]
        [TestCase("hello world\r\n")]
        [TestCase("192.0.2.1\r\n")]
        [TestCase("<html><body>12345</body></html>")]
        [TestCase("Router: 999.1.2.3")]
        [TestCase("\0\0\0\u00ff\u00fe")]
        public void AnythingElse_IsAnUnknownFormat(string text)
        {
            UpnpInfo info = UpnpInfoParser.Parse(text, Game.EmpireEarth);

            Assert.That(info.Status, Is.EqualTo(UpnpInfoStatus.UnknownFormat));
            Assert.That(info.ExternalAddressClass, Is.Null);
            Assert.That(info.LocalAddress, Is.Null);
            Assert.That(info.Ports, Is.Empty);
        }

        [Test]
        public void APublicLocalAddress_IsNotKept()
        {
            UpnpInfo info = UpnpInfoParser.Parse("local address: 198.51.100.7\r\nTCP 33334 OK", Game.EmpireEarth);

            Assert.That(info.LocalAddress, Is.Null, "only private and link-local addresses may be shown");
            Assert.That(info.Ports.Single().Succeeded, Is.True);
        }

        [Test]
        public void PortLines_AreLimited_AndLowPortsIgnored()
        {
            string text = string.Join("\n", Enumerable.Range(0, 30).Select(i => "TCP " + (40000 + i) + " added")) + "\nTCP 80 added";

            UpnpInfo info = UpnpInfoParser.Parse(text, Game.EmpireEarth);

            Assert.That(info.Ports, Has.Count.EqualTo(UpnpInfoParser.MaxPortLines));
            Assert.That(info.Ports.All(port => port.Port >= 40000), Is.True);
        }

        [Test]
        public void TheFile_IsReadWhereTheGameWritesIt()
        {
            var fileSystem = new InMemoryFileSystem();
            const string folder = @"C:\Program Files (x86)\Neo Empire Earth\Empire Earth";
            fileSystem.AddDirectory(folder);
            var paths = new EffectivePathResolver(fileSystem, @"C:\Users\Player\AppData\Local\VirtualStore",
                new[] { @"C:\Program Files (x86)" });

            Assert.That(UpnpInfoParser.Read(fileSystem, paths, folder, Game.EmpireEarth).Status, Is.EqualTo(UpnpInfoStatus.Missing));

            fileSystem.AddFile(@"C:\Users\Player\AppData\Local\VirtualStore\Program Files (x86)\Neo Empire Earth\Empire Earth\upnp_info.txt",
                "WAN IP: 100.64.0.1\r\n");
            UpnpInfo info = UpnpInfoParser.Read(new WriteForbiddingFileSystem(fileSystem), paths, folder, Game.EmpireEarth);
            Assert.That(info.ExternalAddressClass, Is.EqualTo(IPv4Class.Cgnat));
            Assert.That(info.Path, Does.Contain("VirtualStore"));

            fileSystem.FailOn(info.Path, FileSystemOperation.Read, FileSystemStatus.IoError);
            Assert.That(UpnpInfoParser.Read(fileSystem, paths, folder, Game.EmpireEarth).Status, Is.EqualTo(UpnpInfoStatus.Unreadable));
        }
    }
}

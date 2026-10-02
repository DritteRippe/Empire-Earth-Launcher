using System.Net;
using Empire_Earth_Launcher.Core.Diagnostics;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.Diagnostics
{
    /// <summary>
    /// <see cref="ReportAnonymizer"/>: the privacy rules of ADR 0013 (plan review) for paths and addresses. The names are
    /// synthetic.
    /// </summary>
    [TestFixture]
    public class ReportAnonymizerTests
    {
        private static readonly PrivateNames Names = new PrivateNames("Jonas", new[] { "JONAS-PC", "jonas-pc" }, "home.example",
            @"C:\Users\Jonas.HOME", @"C:\Users\Jonas.HOME\AppData\Local");

        private readonly ReportAnonymizer anonymizer = new ReportAnonymizer(Names);

        [TestCase(@"C:\Users\Jonas.HOME\AppData\Local\Empire Earth Launcher\log.txt", @"%LOCALAPPDATA%\Empire Earth Launcher\log.txt")]
        [TestCase(@"C:\Users\Jonas.HOME\AppData\Local\VirtualStore\Program Files (x86)\Neo Empire Earth\Empire Earth",
            @"%LOCALAPPDATA%\VirtualStore\Program Files (x86)\Neo Empire Earth\Empire Earth")]
        [TestCase(@"c:\users\jonas.home\Documents\Saves", @"%USERPROFILE%\Documents\Saves")]
        [TestCase(@"C:\Users\Jonas.HOME", @"%USERPROFILE%")]
        [TestCase(@"D:\Users\Jonas\Games\Empire Earth", @"D:\Users\<user>\Games\Empire Earth")]
        [TestCase(@"D:\Users\JONAS.HOME\Games", @"D:\Users\<user>\Games")]
        [TestCase(@"D:\Backup\Jonas.old\EE", @"D:\Backup\<user>\EE")]
        [TestCase(@"E:\jonas\Empire Earth", @"E:\<user>\Empire Earth")]
        [TestCase(@"C:\Users\Jonasx\EE", @"C:\Users\Jonasx\EE")]
        [TestCase(@"C:\Spiele\JONAS-PC\EE", @"C:\Spiele\<computer>\EE")]
        [TestCase(@"\\JONAS-PC\Spiele\Empire Earth", @"\\<computer>\Spiele\Empire Earth")]
        [TestCase(@"\\jonas-pc.home.example\Spiele", @"\\<computer>\Spiele")]
        [TestCase(@"\\nas-keller\Spiele\Jonas\EE", @"\\<server>\Spiele\<user>\EE")]
        [TestCase(@"\\192.168.178.5\Spiele", @"\\<server>\Spiele")]
        [TestCase(@"\\home.example\dfs\Empire Earth", @"\\<server>\dfs\Empire Earth")]
        [TestCase(@"D:\home.example\EE", @"D:\<domain>\EE")]
        [TestCase(@"C:\Program Files (x86)\Neo Empire Earth\Empire Earth", @"C:\Program Files (x86)\Neo Empire Earth\Empire Earth")]
        [TestCase(@"C:\Games/Jonas/x.ees", @"C:\Games/<user>/x.ees")]
        [TestCase("", "")]
        [TestCase(null, "")]
        public void Paths_LoseTheUserAndTheComputer(string path, string expected)
        {
            Assert.That(anonymizer.Path(path), Is.EqualTo(expected));
        }

        [Test]
        public void ARenamedAccount_ProfileFolderNameIsReplacedToo()
        {
            var renamed = new ReportAnonymizer(new PrivateNames("jonas", new string[0], null, @"C:\Users\Jonas Beispiel", null));

            Assert.That(renamed.Path(@"D:\Users\Jonas Beispiel\EE"), Is.EqualTo(@"D:\Users\<user>\EE"));
            Assert.That(renamed.Path(@"D:\Users\jonas\EE"), Is.EqualTo(@"D:\Users\<user>\EE"));
        }

        [Test]
        public void WithoutKnownNames_PathsStay()
        {
            var empty = new ReportAnonymizer(new PrivateNames(null, null, null, null, null));

            Assert.That(empty.Path(@"C:\Users\Somebody\EE"), Is.EqualTo(@"C:\Users\Somebody\EE"));
            Assert.That(empty.Path(@"\\HOST\Share"), Is.EqualTo(@"\\<server>\Share"), "a server name is never shown");
        }

        [TestCase("192.168.178.20", "192.168.178.20")]
        [TestCase("10.8.0.2", "10.8.0.2")]
        [TestCase("169.254.3.4", "169.254.3.4")]
        [TestCase("203.0.113.45", "<public address>")]
        [TestCase("25.10.20.30", "<public address>")]
        [TestCase("100.70.1.2", "<CGNAT address>")]
        [TestCase("0.0.0.0", "<unspecified (0.0.0.0) address>")]
        [TestCase("2001:db8::5", "<IPv6 address>")]
        [TestCase("fe80::5", "<IPv6 address>")]
        public void Addresses_OnlyPrivateOnesAsTheyAre(string address, string expected)
        {
            Assert.That(ReportAnonymizer.Address(IPAddress.Parse(address)), Is.EqualTo(expected));
        }
    }
}

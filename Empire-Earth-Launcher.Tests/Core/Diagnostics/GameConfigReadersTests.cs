using System.Linq;
using System.Text;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Diagnostics;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Tests.Fakes;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.Diagnostics
{
    /// <summary>
    /// <see cref="NeoEeConfigReader"/> and <see cref="WonLobbyConfigReader"/> (R7, ARCHITECTURE 4.6): both files are read where
    /// the game reads them (ADR 0016), never written, and every line the game would ignore is ignored here too. The samples are
    /// synthetic and only follow the layout of the files the setup installs (tabs, comments after <c>#</c> or <c>//</c>).
    /// </summary>
    [TestFixture]
    public class GameConfigReadersTests
    {
        private const string GameFolder = @"C:\Program Files (x86)\Neo Empire Earth\Empire Earth";
        private const string VirtualStore = @"C:\Users\Player\AppData\Local\VirtualStore";

        /// <summary>The layout of the RIP hosting configuration of NeoEE: a banner of comments, then <c>Key:</c>, tabs, value, comment.</summary>
        internal const string NeoEeCfg =
            "###############################\r\n" +
            "#  Sample RIP configuration   #\r\n" +
            "###############################\r\n" +
            "\r\n" +
            "Active:\t\t\t\ttrue\t\t\t# Enable/Disable, default = true\r\n" +
            "\r\n" +
            "############ Advanced settings ############\r\n" +
            "Server:\t\t\t\trip.neoee.example\t# the server, default = rip.neoee.example\r\n" +
            "FirewallServerPort:\t10002\t\t# not used by the launcher\r\n" +
            "DefaultPort:\t\t33334\t\t\t# default game port, default = 33334\r\n" +
            "MemberPorts:\t\t33340\t\t\t# relay member ports, default = 33340\r\n" +
            "PortCheck:\t\t\ttrue\t\t\t# check if DefaultPort is forwarded, default = true\r\n" +
            "TryUPnP:\t\t\ttrue\t\t\t# try UPnP forwarding, default = true\r\n" +
            "ShowGui:\t\t\tfalse\r\n";

        /// <summary>The layout of the lobby configuration: <c>Key: value</c>, comments after <c>//</c>.</summary>
        internal const string WonLobbyCfg =
            "Language: EN\r\n" +
            "ProductName: EmpireEarth\r\n" +
            "\r\n" +
            "DirServer: lobby.neoee.example:15106, lobby2.neoee.example:15106\r\n" +
            "EEFileTransferPort: 33335\t// new\r\n" +
            "LobbyPort: 33336\t\t\t// new\r\n" +
            "CDKeyCheck: true\t\t\t// was false\r\n" +
            "IncludeFile: \"zzWONVersion.cfg\"\r\n";

        private InMemoryFileSystem fileSystem;
        private EffectivePathResolver effectivePaths;

        [SetUp]
        public void SetUp()
        {
            fileSystem = new InMemoryFileSystem();
            fileSystem.AddDirectory(GameFolder);
            effectivePaths = new EffectivePathResolver(fileSystem, VirtualStore, new[] { @"C:\Program Files (x86)" });
        }

        private NeoEeConfig ReadNeoEe()
        {
            return NeoEeConfigReader.Read(new WriteForbiddingFileSystem(fileSystem), effectivePaths, GameFolder, Game.EmpireEarth);
        }

        private WonLobbyConfig ReadWonLobby()
        {
            return WonLobbyConfigReader.Read(new WriteForbiddingFileSystem(fileSystem), effectivePaths, GameFolder, Game.EmpireEarth);
        }

        [Test]
        public void NeoEeCfg_TheValuesOfTheDiagnostics()
        {
            fileSystem.AddFile(GameFolder + @"\NeoEE.cfg", NeoEeCfg);

            NeoEeConfig config = ReadNeoEe();

            Assert.That(config.Status, Is.EqualTo(ConfigFileStatus.Read));
            Assert.That(config.Active, Is.True);
            Assert.That(config.Server, Is.EqualTo("rip.neoee.example"));
            Assert.That(config.DefaultPort, Is.EqualTo(33334));
            Assert.That(config.MemberPorts, Is.EqualTo(33340));
            Assert.That(config.PortCheck, Is.True);
            Assert.That(config.TryUpnp, Is.True);
            Assert.That(config.InvalidKeys, Is.Empty);
            Assert.That(config.IsVirtualStoreCopy, Is.False);
        }

        [Test]
        public void NeoEeCfg_KeysIgnoreCase_ValuesAcceptOneAndZero_LfAndBom()
        {
            fileSystem.AddFile(GameFolder + @"\NeoEE.cfg", Encoding.UTF8.GetPreamble().Concat(Encoding.ASCII.GetBytes(
                "active: 0\nSERVER: 192.0.2.7 # an address\ntryupnp:1\nDefaultPort:  40000\n")).ToArray());

            NeoEeConfig config = ReadNeoEe();

            Assert.That(config.Active, Is.False);
            Assert.That(config.Server, Is.EqualTo("192.0.2.7"));
            Assert.That(config.TryUpnp, Is.True);
            Assert.That(config.DefaultPort, Is.EqualTo(40000));
            Assert.That(config.PortCheck, Is.Null, "a missing key is unknown, not false");
        }

        [TestCase("Active:\tmaybe", "Active")]
        [TestCase("DefaultPort:\t70000", "DefaultPort")]
        [TestCase("MemberPorts:\t-1", "MemberPorts")]
        [TestCase("Server:\thttp://evil.example/x", "Server")]
        [TestCase("Server:\ta_b.example", "Server")]
        public void NeoEeCfg_InvalidValues_AreNamed_AndNotUsed(string line, string key)
        {
            fileSystem.AddFile(GameFolder + @"\NeoEE.cfg", line + "\r\n");

            NeoEeConfig config = ReadNeoEe();

            Assert.That(config.InvalidKeys, Is.EqualTo(new[] { key }));
            Assert.That(config.Active, Is.Null);
            Assert.That(config.Server, Is.Null, "only a host name is ever looked up");
            Assert.That(config.DefaultPort, Is.Null);
        }

        [Test]
        public void NeoEeCfg_FirstLineOfAKeyWins_AndCommentsAreNoValues()
        {
            fileSystem.AddFile(GameFolder + @"\NeoEE.cfg", "# Active: false\r\nActive: true\r\nActive: false\r\nPortCheck: # none\r\n");

            NeoEeConfig config = ReadNeoEe();

            Assert.That(config.Active, Is.True);
            Assert.That(config.PortCheck, Is.Null);
            Assert.That(config.InvalidKeys, Is.EqualTo(new[] { "PortCheck" }));
        }

        [Test]
        public void NeoEeCfg_MissingAndUnreadable()
        {
            Assert.That(ReadNeoEe().Status, Is.EqualTo(ConfigFileStatus.Missing), "EE without NeoEE has none");

            fileSystem.AddFile(GameFolder + @"\NeoEE.cfg", NeoEeCfg);
            fileSystem.FailOn(GameFolder + @"\NeoEE.cfg", FileSystemOperation.Read, FileSystemStatus.AccessDenied);
            NeoEeConfig config = ReadNeoEe();
            Assert.That(config.Status, Is.EqualTo(ConfigFileStatus.Unreadable));
            Assert.That(config.Problem, Does.Contain("AccessDenied"));
            Assert.That(config.Active, Is.Null);
        }

        [Test]
        public void NeoEeCfg_ALargeFile_IsNotRead()
        {
            fileSystem.AddFile(GameFolder + @"\NeoEE.cfg", new byte[KeyValueFile.MaxBytes + 1]);

            Assert.That(ReadNeoEe().Status, Is.EqualTo(ConfigFileStatus.Unreadable));
        }

        /// <summary>ADR 0016: an edit without administrator rights below Program Files lands in the VirtualStore, where the game reads it.</summary>
        [Test]
        public void TheVirtualStoreCopy_IsTheFileTheGameReads()
        {
            fileSystem.AddFile(GameFolder + @"\NeoEE.cfg", NeoEeCfg);
            fileSystem.AddFile(VirtualStore + @"\Program Files (x86)\Neo Empire Earth\Empire Earth\NeoEE.cfg", "Active: false\r\n");
            fileSystem.AddFile(VirtualStore + @"\Program Files (x86)\Neo Empire Earth\Empire Earth\WONLobby.cfg", "CDKeyCheck: false\r\n");

            NeoEeConfig config = ReadNeoEe();
            Assert.That(config.IsVirtualStoreCopy, Is.True);
            Assert.That(config.Active, Is.False);
            Assert.That(config.Path, Does.StartWith(VirtualStore));
            Assert.That(ReadWonLobby().CdKeyCheck, Is.False);
        }

        [Test]
        public void WonLobbyCfg_CdKeyCheckAndThePorts()
        {
            fileSystem.AddFile(GameFolder + @"\WONLobby.cfg", WonLobbyCfg);

            WonLobbyConfig config = ReadWonLobby();

            Assert.That(config.Status, Is.EqualTo(ConfigFileStatus.Read));
            Assert.That(config.CdKeyCheck, Is.True);
            Assert.That(config.CdKeyCheckInvalid, Is.False);
            Assert.That(config.FileTransferPort, Is.EqualTo(33335));
            Assert.That(config.LobbyPort, Is.EqualTo(33336));
            Assert.That(config.NeedsCdKeyCheckHint(Product.NeoEE), Is.False);
        }

        /// <summary>t=10950 p=47202: NeoEE needs <c>CDKeyCheck: true</c>; in EE without NeoEE <c>false</c> is normal (forum 4.3).</summary>
        [TestCase("CDKeyCheck: false", false, false, true, false)]
        [TestCase("CDKeyCheck: FALSE // was false", false, false, true, false)]
        [TestCase("CDKeyCheck: yes", null, true, true, false)]
        [TestCase("Language: EN", null, false, true, false)]
        [TestCase("CDKeyCheck: true", true, false, false, false)]
        public void WonLobbyCfg_TheCdKeyCheckHint_OnlyForNeoEE(string line, bool? value, bool invalid, bool hintNeoEe, bool hintEe)
        {
            fileSystem.AddFile(GameFolder + @"\WONLobby.cfg", line + "\r\n");

            WonLobbyConfig config = ReadWonLobby();

            Assert.That(config.CdKeyCheck, Is.EqualTo(value));
            Assert.That(config.CdKeyCheckInvalid, Is.EqualTo(invalid));
            Assert.That(config.NeedsCdKeyCheckHint(Product.NeoEE), Is.EqualTo(hintNeoEe));
            Assert.That(config.NeedsCdKeyCheckHint(Product.EE), Is.EqualTo(hintEe));
        }

        [Test]
        public void WonLobbyCfg_AMissingFile_GivesNoHint()
        {
            WonLobbyConfig config = ReadWonLobby();

            Assert.That(config.Status, Is.EqualTo(ConfigFileStatus.Missing));
            Assert.That(config.NeedsCdKeyCheckHint(Product.NeoEE), Is.False, "the integrity check reports a missing file");
        }

        [TestCase("rip.neoee.example", true)]
        [TestCase("192.0.2.7", true)]
        [TestCase("localhost", true)]
        [TestCase("", false)]
        [TestCase("-bad.example", false)]
        [TestCase("a..example", false)]
        [TestCase("host name.example", false)]
        [TestCase("host.example:10002", false)]
        public void OnlyHostNames_AreLookedUp(string text, bool expected)
        {
            Assert.That(NeoEeConfigReader.IsHostName(text), Is.EqualTo(expected));
        }
    }
}

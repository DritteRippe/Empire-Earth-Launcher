using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Empire_Earth_Launcher.Core.Backup;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Diagnostics;
using Empire_Earth_Launcher.Core.GameSettings;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Integrity;
using Empire_Earth_Launcher.Core.Lobby;
using Empire_Earth_Launcher.Core.Maintenance;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Core.Play;
using Empire_Earth_Launcher.Core.Repair;
using Empire_Earth_Launcher.Tests.Fakes;
using Empire_Earth_Launcher.Tests.TestSupport;
using Empire_Earth_Launcher.Tests.Won;
using Empire_Earth_WON;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.Diagnostics
{
    /// <summary>
    /// <see cref="DiagnosticsReport"/> (ARCHITECTURE 4.6, ADR 0013 plan review): a golden file of the whole report, built by the
    /// real checks of the core over an in-memory computer with synthetic names, and a negative test on a computer full of
    /// what must never appear (addresses, MAC, adapter GUID and name, computer, domain, user and player names, the CD keys
    /// <c>NOT-A-KEY-0000</c>).
    /// </summary>
    [TestFixture]
    public class DiagnosticsReportTests
    {
        /// <summary>Who uses the computer and what the fakes hold.</summary>
        private sealed class Persona
        {
            public string UserName = "Player";
            public string ProfileFolder = "Player";
            public string ComputerName = "PLAYER-PC";
            public string DomainName;
            public string Root = @"C:\Program Files (x86)\Neo Empire Earth";
            public string ForeignFolder = @"D:\Games\Empire Earth";
            public string LauncherFolder;
            public string[] LobbyProfiles = { "SamplePlayer", "Jürgen" };
            public string PlayerFolder = "Zoë";
            public string AdapterId = "{00000000-0000-0000-0000-000000000001}";
            public string AdapterName = "Ethernet";
            public string Mac = "00-00-00-00-00-01";
            public string DnsSuffix = string.Empty;
            public string[] Addresses = { "192.168.178.20/24", "fe80::20/64" };
            public string[] Gateways = { "192.168.178.1" };
            public string UpnpExternal = "100.64.12.13";

            public string Profile
            {
                get { return @"C:\Users\" + ProfileFolder; }
            }

            public string LocalAppData
            {
                get { return Profile + @"\AppData\Local"; }
            }

            public string VirtualStore
            {
                get { return LocalAppData + @"\VirtualStore"; }
            }
        }

        private static readonly string[] Virtualized =
            { @"C:\Program Files", @"C:\Program Files (x86)", @"C:\ProgramData", @"C:\Windows" };

        /// <summary>Builds the report with the real checks of the core over an in-memory computer of <paramref name="persona"/>.</summary>
        private static string ReportOf(Persona persona)
        {
            var world = new InstallationWorld();
            string root = persona.Root;
            world.AddCommunityInstallation(root, Product.NeoEE);
            // The manifest of the setup: three files as installed, neoee.dll changed since (a code file: Damaged).
            var files = new[]
            {
                Tuple.Create("Empire Earth/Empire Earth.exe", 1), Tuple.Create("Empire Earth/neoee.dll", 2),
                Tuple.Create("Empire Earth - The Art of Conquest/EE-AOC.exe", 3), Tuple.Create("Empire Earth/Data/file0001.dat", 4)
            };
            foreach (var file in files)
                world.FileSystem.AddFile(WinPath.Combine(root, file.Item1), SampleHashes.Content(file.Item2));
            world.FileSystem.AddFile(WinPath.Combine(root, "Empire Earth/neoee.dll"), SampleHashes.Content(9));
            world.FileSystem.AddFile(WinPath.Combine(root, @"_setupdata_NeoEE\files.sha256"),
                string.Join("\n", files.Select(file => SampleHashes.Of(file.Item2) + "  " + file.Item1)) + "\n");
            string eeFolder = WinPath.Combine(root, "Empire Earth");
            string aocFolder = WinPath.Combine(root, "Empire Earth - The Art of Conquest");
            foreach (string folder in new[] { eeFolder, aocFolder })
            {
                world.FileSystem.AddFile(folder + @"\NeoEE.cfg", GameConfigReadersTests.NeoEeCfg);
                world.FileSystem.AddFile(folder + @"\WONLobby.cfg", GameConfigReadersTests.WonLobbyCfg);
            }
            world.AddForeignInstallation(persona.ForeignFolder);
            if (persona.LauncherFolder != null)
                world.AddEmpireEarth(persona.LauncherFolder);

            // The VirtualStore of the player: a shadowed program file, the lobby profiles, a player folder, upnp_info.txt.
            string eeCopy = persona.VirtualStore + @"\Program Files (x86)\Neo Empire Earth\Empire Earth";
            world.FileSystem.AddFile(eeCopy + @"\neoee.dll", SampleHashes.Content(5));
            world.FileSystem.AddFile(eeCopy + @"\" + LobbyPersistentData.GlobalDataFileName,
                LobbyFileBuilder.GlobalFile(true, true, 1, persona.LobbyProfiles));
            world.FileSystem.AddDirectory(eeCopy + @"\Users\" + persona.PlayerFolder);
            world.FileSystem.AddFile(eeCopy + @"\upnp_info.txt",
                "Local LAN ip address : 192.168.178.20\r\nExternalIPAddress = " + persona.UpnpExternal + "\r\n");

            // The game settings of the player: different bit depths; the CD keys of the setup, which must never be shown.
            RegistryLocation settings = InstallationWorld.Hkcu(@"Software\Neo\Empire Earth");
            world.Registry.Seed(settings, "Game Bit Depth", RegistryValue.FromDWord(16));
            world.Registry.Seed(settings, "Texture Bit Depth", RegistryValue.FromDWord(32));
            world.Registry.Seed(InstallationWorld.Hklm32(@"Software\Sierra\CDKeys"), "NeoEE", RegistryValue.FromString("NOT-A-KEY-0000"));
            world.Registry.Seed(InstallationWorld.Hkcu(@"Software\Sierra\CDKeys"), "NeoEE", RegistryValue.FromString("NOT-A-KEY-0000"));

            DiscoveryResult discovery = world.Discover(launcherFolder: persona.LauncherFolder);
            Installation selected = discovery.Selected;
            var fileSystem = new WriteForbiddingFileSystem(world.FileSystem);
            var registry = new WriteForbiddingRegistry(world.Registry);
            var paths = new EffectivePathResolver(fileSystem, persona.VirtualStore, Virtualized);
            var system = new FakeSystemInfo().WithScreen(1920, 1080, 125);
            var mutexes = new FakeMutexProbe();
            var anonymizer = new ReportAnonymizer(new PrivateNames(persona.UserName, new[] { persona.ComputerName },
                persona.DomainName, persona.Profile, persona.LocalAppData));

            var network = new FakeNetworkInfo()
                .AddAdapter(NetworkAdapterKind.Ethernet, "Sample Ethernet Controller", persona.Addresses, persona.Gateways,
                    id: persona.AdapterId, name: persona.AdapterName, mac: persona.Mac, dnsSuffix: persona.DnsSuffix)
                .AddAdapter(NetworkAdapterKind.Ethernet, "Sample Hamachi Virtual Ethernet Adapter", new[] { "25.10.20.30/8" },
                    id: persona.AdapterId + "2", name: persona.AdapterName + " 2", mac: persona.Mac + "2")
                .Resolve(NetworkDiagnosticsTests.StatusHost, "192.0.2.10").Resolve("rip.neoee.example", "192.0.2.11");
            var https = new FakeHttpsClient().Answer(SetupDownloadLocator.QueryUrl(selected.AppId), 200, SetupDownloadLocator.FixedPageUrl);
            var statusServer = new FakeNeoStatusServer { Failure = new TimeoutException("injected") };
            NetworkReport networkReport = new NetworkDiagnostics(network, statusServer, https, fileSystem, paths, anonymizer,
                world.Clock, new RecordingLogger()).RunAsync(selected, selected.AppId, CancellationToken.None).GetAwaiter().GetResult();

            var logger = new RecordingLogger();
            var input = new DiagnosticsInput
            {
                LauncherVersion = "0.1.0-alpha",
                CreatedAt = world.Clock.Now,
                SystemInfo = system,
                Is64BitWindows = true,
                UiCulture = "de-DE",
                Discovery = discovery,
                ProgramVersions = new ProgramVersions(fileSystem, new FakeFileVersionReader()
                    .With(WinPath.Combine(eeFolder, "Empire Earth.exe"), "2.0.0.5")).Read(selected),
                DirectXWrappers = new[] { Game.EmpireEarth, Game.ArtOfConquest }
                    .Select(game => new KeyValuePair<Game, RasterizerRecommendation>(game,
                        ComputedValues.DirectXWrapper(selected, game, fileSystem)))
                    .ToList(),
                Integrity = new IntegrityChecker(fileSystem, registry, mutexes, logger).Check(selected, IntegrityCheckKind.Quick),
                Defaults = new[]
                {
                    new KeyValuePair<Game, DefaultsStatus>(Game.EmpireEarth, DefaultsStatus.Applied),
                    new KeyValuePair<Game, DefaultsStatus>(Game.ArtOfConquest, DefaultsStatus.Pending)
                },
                ConsistencyFindings = new ConsistencyChecker(registry, fileSystem, system).Check(selected),
                VirtualStore = new VirtualStoreScanner(fileSystem, paths, new MutationGuard(mutexes, logger), logger).Scan(selected),
                Names = new NameChecks(fileSystem, paths, new LobbyProfileRepository(logger, fileSystem, paths), logger).Check(selected),
                Cleanup = new RegistryCleanup(registry, fileSystem, new MutationGuard(mutexes, logger),
                    new BackupLocations(@"C:\Backups", fileSystem, world.Clock, logger), logger).Scan(discovery),
                Network = networkReport
            };
            return DiagnosticsReport.Build(input, anonymizer);
        }

        private static string Golden(string name)
        {
            return File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "Core", "Diagnostics", "Golden", name),
                Encoding.UTF8);
        }

        /// <summary>
        /// The whole report of a synthetic computer, byte for byte (CRLF): every section of ARCHITECTURE 4.6. A change of the
        /// report text is a change of this file in the same commit.
        /// </summary>
        [Test]
        public void TheReport_OfASyntheticComputer_IsTheGoldenFile()
        {
            string report = ReportOf(new Persona());

            Assert.That(report, Is.EqualTo(Golden("DiagnosticsReport.txt")), "actual report:\n" + report);
        }

        /// <summary>
        /// ADR 0013 plan review: a computer full of personal data and filled <c>Software\Sierra\CDKeys</c>: none of it is in the
        /// report.
        /// </summary>
        [Test]
        public void TheReport_ContainsNoneOfThePersonalData()
        {
            var persona = new Persona
            {
                UserName = "Jonas",
                ProfileFolder = "Jonas.FAMILY",
                ComputerName = "JONAS-PC",
                DomainName = "jonas-family.example",
                ForeignFolder = @"D:\Users\Jonas\Games\Empire Earth",
                LauncherFolder = @"\\JONAS-PC\Spiele\Empire Earth",
                LobbyProfiles = new[] { "JönasPro", "Kämpfer" },
                PlayerFolder = "Jürgen der Große",
                AdapterId = "{6F1E2D3C-AAAA-BBBB-CCCC-123456789ABC}",
                AdapterName = "Wohnzimmer-WLAN",
                Mac = "AA-BB-CC-DD-EE-FF",
                DnsSuffix = "fritz.box.jonas-family.example",
                Addresses = new[] { "198.51.100.77/24", "2001:db8:abcd::77/64", "fe80::77/64" },
                Gateways = new[] { "198.51.100.1" },
                UpnpExternal = "203.0.113.99"
            };

            string report = ReportOf(persona);

            foreach (string secret in new[]
                     {
                         "Jonas", "JONAS-PC", "jonas-family", "6F1E2D3C", "AA-BB-CC", "AABBCC", "Wohnzimmer", "fritz.box",
                         "198.51.100.77", "198.51.100.1", "2001:db8:abcd", "fe80::77", "203.0.113.99", "25.10.20.30",
                         "JönasPro", "Kämpfer", "Jürgen", "NOT-A-KEY"
                     })
                Assert.That(report.IndexOf(secret, StringComparison.OrdinalIgnoreCase), Is.EqualTo(-1),
                    "\"" + secret + "\" must not be in the report:\n" + report);
            // The sections are there, with placeholders where data was removed.
            Assert.That(report, Does.Contain(@"D:\USERS\<user>\GAMES\Empire Earth"), "Installed From is upper case (contract 3.3)");
            Assert.That(report, Does.Contain(@"\\<computer>\Spiele"));
            Assert.That(report, Does.Contain(@"%LOCALAPPDATA%\VirtualStore"));
            Assert.That(report, Does.Contain("<public address>"));
            Assert.That(report, Does.Contain("external address public"));
            Assert.That(report, Does.Contain(@"HKLM32\Software\Sierra\CDKeys exists"));
            Assert.That(report, Does.Contain("EE lobby profile 1: characters outside printable ASCII"));
        }

        [Test]
        public void WithoutResults_EverySectionSaysSo()
        {
            var anonymizer = new ReportAnonymizer(new PrivateNames(null, null, null, null, null));

            string report = DiagnosticsReport.Build(new DiagnosticsInput { CreatedAt = new DateTime(2026, 10, 2, 20, 4, 0) }, anonymizer);

            Assert.That(report, Does.Contain("Installations: not searched yet"));
            Assert.That(report, Does.Contain("Selected installation: none"));
            Assert.That(report, Does.Contain("CD keys:      not checked yet"));
            Assert.That(report, Does.Contain("Network: not checked"));
            Assert.That(report, Does.EndWith("\r\n"));
            Assert.That(report.Replace("\r\n", string.Empty), Does.Not.Contain("\n"), "every line ends with CRLF");
        }
    }
}

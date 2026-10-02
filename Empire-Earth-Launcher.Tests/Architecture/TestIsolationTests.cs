using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Empire_Earth_Launcher.Tests.TestSupport;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Architecture
{
    /// <summary>
    /// The tests touch nothing of the computer they run on (ADR 0012 plan review, REV-06): the test program also runs from
    /// the <c>Tests\</c> folder of the laptop package on real Windows (test plan WP1-11), so it must not create a
    /// <c>WindowsRegistry</c>, use <c>Microsoft.Win32.Registry</c> directly, create an HTTP client (also the launcher's
    /// <c>HttpsClient</c>; its handler settings are inspected without a request) or a socket, ask a server
    /// or write into the launcher's real folder below <c>%LOCALAPPDATA%</c>. Checked on the sources of the test project;
    /// temporary folders and mutexes with random names are allowed.
    /// </summary>
    [TestFixture]
    [Category(TestCategories.SourceTree)]
    public class TestIsolationTests
    {
        private const string TestProjectFolder = "Empire-Earth-Launcher.Tests";

        /// <summary>A forbidden use, with the reason shown when a source line matches it.</summary>
        private sealed class Rule
        {
            public Rule(string reason, string pattern)
            {
                Reason = reason;
                Pattern = new Regex(pattern, RegexOptions.CultureInvariant);
            }

            public string Reason { get; }

            public Regex Pattern { get; }

            public override string ToString()
            {
                return Reason;
            }
        }

        private static readonly Rule[] Rules =
        {
            new Rule("the real registry adapter", @"\bnew\s+WindowsRegistry\s*\("),
            new Rule("Microsoft.Win32.Registry directly",
                @"(?<![\w.])(Microsoft\.Win32\.)?Registry\s*\.\s*(CurrentUser|LocalMachine|ClassesRoot|Users|CurrentConfig|PerformanceData|GetValue|SetValue)\b"),
            new Rule("a real registry key", @"\b(RegistryKey|OpenBaseKey|OpenRemoteBaseKey|RegLoadAppKey)\b"),
            new Rule("an HTTP client", @"\bnew\s+(HttpClient|HttpClientHandler|WebClient|WebRequestHandler)\s*\("),
            new Rule("the HTTPS client of the launcher", @"\bnew\s+HttpsClient\s*\("),
            new Rule("an HTTP request", @"\b(HttpWebRequest|WebRequest\s*\.\s*Create(Http)?)\b"),
            new Rule("a socket", @"\bnew\s+(Socket|TcpClient|UdpClient|TcpListener)\s*\("),
            new Rule("a DNS lookup", @"\bDns\s*\.\s*(GetHost\w*|Resolve)\b"),
            new Rule("a request to the NeoEE status server", @"\.\s*(TryGetConnectedPlayers|TryGetServerInfo|SendRequest)\s*\("),
            new Rule("the real %LOCALAPPDATA%", @"\bSpecialFolder\s*\.\s*LocalApplicationData\b"),
            new Rule("a file of the launcher's real folder", @"\bnew\s+\w+\s*\([^;]*\bLauncherPaths\s*\.\s*(LogFile|SettingsFile|BackupsDirectory|ThemesDirectory|UserDataDirectory)\b"),
        };

        private static IEnumerable<string> TestSources()
        {
            string folder = RepositoryRoot.GetFullPath(TestProjectFolder);
            return Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories)
                            .Where(file => !RepositoryRoot.ToRelativePath(file).Split('/').Any(part => part == "bin" || part == "obj"))
                            .Where(file => Path.GetFileName(file) != "TestIsolationTests.cs")
                            .OrderBy(file => file, StringComparer.Ordinal);
        }

        /// <summary>Every line of <paramref name="lines"/> (comments skipped) that matches a rule, as "line: reason: text".</summary>
        private static IEnumerable<string> Offenders(string name, IReadOnlyList<string> lines)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i].Trim();
                if (line.StartsWith("//", StringComparison.Ordinal))
                    continue;
                foreach (Rule rule in Rules.Where(rule => rule.Pattern.IsMatch(line)))
                    yield return name + ":" + (i + 1) + ": " + rule.Reason + ": " + line;
            }
        }

        [Test]
        public void TheTests_TouchNoRegistryNetworkOrLauncherFolder()
        {
            List<string> sources = TestSources().ToList();
            Assert.That(sources.Count, Is.GreaterThan(50), "the test project was not found");

            var offenders = sources.SelectMany(file => Offenders(RepositoryRoot.ToRelativePath(file), File.ReadAllLines(file))).ToList();

            Assert.That(offenders, Is.Empty, "the test program also runs on the laptop (ADR 0012 plan review)");
        }

        [TestCase("var registry = new WindowsRegistry();", "the real registry adapter")]
        [TestCase("Registry.CurrentUser.OpenSubKey(\"Software\");", "Microsoft.Win32.Registry directly")]
        [TestCase("Registry.GetValue(@\"HKEY_CURRENT_USER\\Software\", \"x\", null);", "Microsoft.Win32.Registry directly")]
        [TestCase("Microsoft.Win32.Registry.LocalMachine.OpenSubKey(\"Software\");", "Microsoft.Win32.Registry directly")]
        [TestCase("using (RegistryKey key = RegistryKey.OpenBaseKey(hive, view))", "a real registry key")]
        [TestCase("using (var client = new HttpClient())", "an HTTP client")]
        [TestCase("new WebClient().DownloadString(url);", "an HTTP client")]
        [TestCase("using (var client = new HttpsClient())", "the HTTPS client of the launcher")]
        [TestCase("var request = WebRequest.Create(url);", "an HTTP request")]
        [TestCase("var client = new TcpClient(\"titan.empireearth.eu\", 10005);", "a socket")]
        [TestCase("Dns.GetHostAddresses(\"neoee.net\");", "a DNS lookup")]
        [TestCase("client.TryGetConnectedPlayers(out message, out error);", "a request to the NeoEE status server")]
        [TestCase("Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);", "the real %LOCALAPPDATA%")]
        [TestCase("var store = new SettingsStore(fileSystem, LauncherPaths.SettingsFile, logger);", "a file of the launcher's real folder")]
        public void TheRules_FindEachForbiddenUse(string line, string reason)
        {
            Assert.That(Offenders("Sample.cs", new[] { line }).Single(), Does.Contain(": " + reason + ": "));
        }

        [TestCase("var fault = new SocketException(10060);")]
        [TestCase("Assert.That(LauncherPaths.LogFile, Does.EndWith(\"log.txt\"));")]
        [TestCase("public bool TryGetConnectedPlayers(out NeoApiClient.ConnectedPlayersMessage message, out Exception error)")]
        [TestCase("RegistryLocation.CurrentUser(@\"Software\\Neo\")")]
        [TestCase("Assert.That(world.Registry.GetValue(key, \"Installed From Volume\").IsOk, Is.True);")]
        [TestCase("// new WindowsRegistry() is created only by Program")]
        [TestCase("var client = new FakeHttpsClient();")]
        [TestCase("using (HttpClientHandler handler = HttpsClient.CreateHandler())")]
        public void TheRules_AllowWhatTouchesNothing(string line)
        {
            Assert.That(Offenders("Sample.cs", new[] { line }), Is.Empty);
        }
    }
}

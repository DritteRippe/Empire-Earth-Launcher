using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Empire_Earth_Launcher.Tests.TestSupport;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Architecture
{
    /// <summary>
    /// The launcher contacts three destinations only (ARCHITECTURE 10, ADR 0008 "no further destinations", L-WP9): the NeoEE
    /// status server (<c>NeoApiClient</c>), the update API (<c>HttpsClient</c>, one fixed URL) and DNS (<c>WindowsNetworkInfo</c>).
    /// The only other URLs in the code are the three download pages of contract 4.3 and, since launcher 1.1.1, the release page
    /// of the package "Empire Earth Community" (ADR 0008 amendment of 1.1.1), which only open in the browser. Checked on the
    /// production sources: name lookups, sockets and HTTP clients exist only in their one adapter, no other URL is written in
    /// the code (no "what is my IP" service, no download host such as the retired <c>cdn.empireearth.eu</c>), and the auth and
    /// firewall ports of NeoEE (10002, 10003) appear nowhere.
    /// </summary>
    [TestFixture]
    [Category(TestCategories.SourceTree)]
    public class NetworkDestinationTests
    {
        private sealed class Rule
        {
            public Rule(string reason, string pattern, params string[] allowedFiles)
            {
                Reason = reason;
                Pattern = new Regex(pattern, RegexOptions.CultureInvariant);
                AllowedFiles = allowedFiles;
            }

            public string Reason { get; }

            public Regex Pattern { get; }

            public string[] AllowedFiles { get; }
        }

        private static readonly Rule[] Rules =
        {
            new Rule("a name lookup outside WindowsNetworkInfo", @"\bDns\s*\.", "Empire-Earth-Launcher-Core/Platform/WindowsNetworkInfo.cs"),
            new Rule("a socket outside NeoApiClient", @"\bnew\s+(TcpClient|UdpClient|Socket|TcpListener|UdpListener)\s*\(",
                "Empire-Earth-WON/NeoApiClient.cs"),
            new Rule("an HTTP client outside HttpsClient",
                @"\bnew\s+(HttpClient|HttpClientHandler|WebClient|WebRequestHandler)\s*\(|\bWebRequest\s*\.\s*Create|\bHttpWebRequest\b",
                "Empire-Earth-Launcher-Core/Platform/HttpsClient.cs"),
            new Rule("the auth or firewall port of NeoEE", @"(?<![\w.])1000[23](?![\w.])"),
        };

        /// <summary>
        /// The URL literals the launcher may contain: the update API, the three download pages of contract 4.3 and the release
        /// page of the package; the pages are only opened in the browser.
        /// </summary>
        private static readonly string[] AllowedUrls =
        {
            "https://api.empireearth.eu/setup/", "https://empireearth.eu/download/", "https://empireearth.eu/download/ee/",
            "https://empireearth.eu/download/neo/", "https://github.com/DritteRippe/Empire-Earth-Community/releases/latest"
        };

        private static readonly Regex UrlLiteral = new Regex("\"(https?://[^\"]*)\"", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        private static IEnumerable<string> Offenders(IEnumerable<SourceLine> lines)
        {
            foreach (SourceLine line in lines)
            {
                foreach (Rule rule in Rules.Where(rule => rule.Pattern.IsMatch(line.Text) && !rule.AllowedFiles.Contains(line.File)))
                    yield return line + " (" + rule.Reason + ")";
                foreach (Match url in UrlLiteral.Matches(line.Text))
                {
                    if (!AllowedUrls.Contains(url.Groups[1].Value, StringComparer.Ordinal))
                        yield return line + " (a URL that is not a destination of ADR 0008)";
                }
            }
        }

        [Test]
        public void TheLauncher_ContactsOnlyItsThreeDestinations()
        {
            List<SourceLine> lines = ProductionSources.CodeLines().ToList();
            Assert.That(lines.Select(line => line.File).Distinct().Count(), Is.GreaterThan(100), "the sources were found");

            Assert.That(Offenders(lines), Is.Empty);
        }

        [TestCase("Empire-Earth-Launcher-Core/Diagnostics/NetworkDiagnostics.cs", "var addresses = Dns.GetHostAddresses(host);")]
        [TestCase("Empire Earth Launcher/Program.cs", "using (var client = new TcpClient())")]
        [TestCase("Empire-Earth-Launcher-Core/Diagnostics/NetworkDiagnostics.cs", "var client = new HttpClient();")]
        [TestCase("Empire-Earth-Launcher-Core/Diagnostics/NetworkDiagnostics.cs", "var request = WebRequest.Create(url);")]
        [TestCase("Empire-Earth-WON/NeoApiClient.cs", "private const int AuthPort = 10003;")]
        [TestCase("Empire-Earth-Launcher-Core/Diagnostics/NetworkDiagnostics.cs", "const string Echo = \"https://api.ipify.org\";")]
        [TestCase("Empire-Earth-Launcher-Core/Repair/UpdateApi.cs", "public const string Url = \"http://api.empireearth.eu/setup/\";")]
        [TestCase("Empire-Earth-Launcher-Core/Repair/SetupDownloadPage.cs", "public const string General = \"https://empireearth.eu/download\";",
            Description = "the old page without the slash")]
        [TestCase("Empire-Earth-Launcher-Core/Repair/SetupDownloadPage.cs", "public const string EmpireEarth = \"https://cdn.empireearth.eu/setup/game/EE_Setup.exe\";",
            Description = "the retired download host that the update API named")]
        [TestCase("Empire-Earth-Launcher-Core/Repair/UpdateUrlPolicy.cs", "private const string HttpsPrefix = \"https://\";",
            Description = "the URL policy of the update API's download URL is gone")]
        [TestCase("Empire-Earth-Launcher-Core/Repair/SetupDownloadPage.cs",
            "public const string PackageRelease = \"https://github.com/DritteRippe/Empire-Earth-Community/releases/download/v1.1.0/Empire-Earth-Community-1.1.0.zip\";",
            Description = "a download of the package itself: the launcher never downloads, it opens the release page")]
        [TestCase("Empire-Earth-Launcher-Core/Repair/SetupDownloadPage.cs",
            "public const string PackageRelease = \"https://api.github.com/repos/DritteRippe/Empire-Earth-Community/releases/latest\";",
            Description = "the API of GitHub: a request for the newest version would be a new destination")]
        public void TheRules_FindEveryOtherDestination(string file, string text)
        {
            Assert.That(Offenders(new[] { new SourceLine(file, 1, text) }), Is.Not.Empty);
        }

        [TestCase("Empire-Earth-Launcher-Core/Platform/WindowsNetworkInfo.cs", "Task<IPAddress[]> lookup = Dns.GetHostAddressesAsync(host);")]
        [TestCase("Empire-Earth-WON/NeoApiClient.cs", "using (var tcpClient = new TcpClient())")]
        [TestCase("Empire-Earth-Launcher-Core/Platform/HttpsClient.cs", "var client = new HttpClient(handler, true)")]
        [TestCase("Empire-Earth-Launcher-Core/Repair/UpdateApi.cs", "public const string Url = \"https://api.empireearth.eu/setup/\";")]
        [TestCase("Empire-Earth-Launcher-Core/Repair/SetupDownloadPage.cs", "public const string General = \"https://empireearth.eu/download/\";")]
        [TestCase("Empire-Earth-Launcher-Core/Repair/SetupDownloadPage.cs", "public const string EmpireEarth = \"https://empireearth.eu/download/ee/\";")]
        [TestCase("Empire-Earth-Launcher-Core/Repair/SetupDownloadPage.cs", "public const string NeoEE = \"https://empireearth.eu/download/neo/\";")]
        [TestCase("Empire-Earth-Launcher-Core/Repair/SetupDownloadPage.cs",
            "public const string PackageRelease = \"https://github.com/DritteRippe/Empire-Earth-Community/releases/latest\";")]
        [TestCase("Empire-Earth-Launcher-Core/Diagnostics/NetworkDiagnostics.cs", "int port = 100020;")]
        public void TheRules_AllowTheThreeDestinationsAndTheDownloadPages(string file, string text)
        {
            Assert.That(Offenders(new[] { new SourceLine(file, 1, text) }), Is.Empty);
        }
    }
}

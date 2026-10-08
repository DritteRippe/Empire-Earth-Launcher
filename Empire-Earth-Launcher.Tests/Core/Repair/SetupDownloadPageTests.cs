using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Repair;
using Empire_Earth_Launcher.Tests.Fakes;
using Empire_Earth_Launcher.Tests.TestSupport;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.Repair
{
    /// <summary>
    /// <see cref="SetupDownloadPage"/> (contract 4.3, ADR 0008 amendment of 1.1.0): the download page is chosen by the
    /// installation alone, one page per product and the general page for a foreign installation, an unknown product and
    /// none; the release page of the package for an installation of the suite (ADR 0008 amendment of 1.1.1) is chosen by the
    /// advice (<see cref="SuiteRepairTests"/>).
    /// </summary>
    [TestFixture]
    public class SetupDownloadPageTests
    {
        private static Installation Of(Product product, InstallationKind kind, string root = @"D:\Games\Empire Earth Community")
        {
            return new Installation(product, root, root + @"\Empire Earth", null, kind,
                kind == InstallationKind.Foreign ? InstallMode.Unknown : InstallMode.User,
                new[] { kind == InstallationKind.Foreign ? InstallationSource.InstalledFrom : InstallationSource.RegistryRecord });
        }

        [Test]
        public void Constants_AreTheThreePagesOfContract_4_3()
        {
            Assert.That(SetupDownloadPage.General, Is.EqualTo("https://empireearth.eu/download/"));
            Assert.That(SetupDownloadPage.EmpireEarth, Is.EqualTo("https://empireearth.eu/download/ee/"));
            Assert.That(SetupDownloadPage.NeoEE, Is.EqualTo("https://empireearth.eu/download/neo/"));
        }

        [Test]
        public void PackageRelease_IsTheNewestReleaseOfThePackageOnGitHub()
        {
            var uri = new Uri(SetupDownloadPage.PackageRelease);

            Assert.That(SetupDownloadPage.PackageRelease, Is.EqualTo("https://github.com/DritteRippe/Empire-Earth-Community/releases/latest"));
            Assert.That(uri.Scheme, Is.EqualTo(Uri.UriSchemeHttps));
            Assert.That(uri.Query, Is.Empty, "nothing about the player is sent");
        }

        [TestCase(InstallationKind.Community)]
        [TestCase(InstallationKind.CommunityLegacy)]
        [TestCase(InstallationKind.Foreign)]
        public void For_NeverGivesTheReleasePageOfThePackage_TheSuiteRecordDecides(InstallationKind kind)
        {
            foreach (Product product in Product.All)
                Assert.That(SetupDownloadPage.For(Of(product, kind)), Is.Not.EqualTo(SetupDownloadPage.PackageRelease));
        }

        [TestCase(InstallationKind.Community)]
        [TestCase(InstallationKind.CommunityLegacy)]
        public void ACommunityEE_GetsThePageOfEE(InstallationKind kind)
        {
            Assert.That(SetupDownloadPage.For(Of(Product.EE, kind)), Is.EqualTo(SetupDownloadPage.EmpireEarth));
        }

        [TestCase(InstallationKind.Community)]
        [TestCase(InstallationKind.CommunityLegacy)]
        public void ACommunityNeoEE_GetsThePageOfNeoEE(InstallationKind kind)
        {
            Assert.That(SetupDownloadPage.For(Of(Product.NeoEE, kind)), Is.EqualTo(SetupDownloadPage.NeoEE));
        }

        [Test]
        public void AForeignInstallation_GetsTheGeneralPage_EvenWithNeoEeFiles()
        {
            Assert.That(SetupDownloadPage.For(Of(Product.EE, InstallationKind.Foreign, @"D:\GOG Games\Empire Earth Gold")),
                Is.EqualTo(SetupDownloadPage.General), "a GOG or CD copy");
            Assert.That(SetupDownloadPage.For(Of(Product.NeoEE, InstallationKind.Foreign)), Is.EqualTo(SetupDownloadPage.General),
                "a foreign folder with neoee.dll");
        }

        [Test]
        public void NoInstallation_GetsTheGeneralPage()
        {
            Assert.That(SetupDownloadPage.For(null), Is.EqualTo(SetupDownloadPage.General));
        }

        [TestCase(InstallationKind.Community)]
        [TestCase(InstallationKind.CommunityLegacy)]
        [TestCase(InstallationKind.Foreign)]
        public void EveryPage_IsHttpsOnTheCommunitySite_AndEndsWithASlash(InstallationKind kind)
        {
            foreach (Product product in Product.All)
            {
                string url = SetupDownloadPage.For(Of(product, kind));

                var uri = new Uri(url);
                Assert.That(uri.Scheme, Is.EqualTo(Uri.UriSchemeHttps), url);
                Assert.That(uri.Host, Is.EqualTo("empireearth.eu"), url);
                Assert.That(url, Does.EndWith("/"), "the address of the page itself: no redirect to a page without the slash");
                Assert.That(uri.Query, Is.Empty, "no parameter, no AppId, nothing about the player is sent");
            }
        }

        /// <summary>
        /// The table of <c>docs/CONTRACT.md</c> 4.3 and the constants of the launcher name the same three pages of the community
        /// website. Since revision 7 the contract names the release page of the package for an installation of the suite in a
        /// paragraph of 4.3, not in the table; a row for it is still accepted, at most one.
        /// </summary>
        [Test]
        [Category(TestCategories.SourceTree)]
        public void TheTableOfContract_4_3_NamesTheThreePagesOfTheConstants()
        {
            string contract = File.ReadAllText(RepositoryRoot.GetFullPath("docs/CONTRACT.md")).Replace("\r\n", "\n");
            int start = contract.IndexOf("### 4.3 Where the user gets the setup", StringComparison.Ordinal);
            int end = contract.IndexOf("### 4.4 ", StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), "CONTRACT.md has no section 4.3");
            Assert.That(end, Is.GreaterThan(start));
            string[] rows = contract.Substring(start, end - start).Split('\n')
                .Where(line => line.StartsWith("| ", StringComparison.Ordinal) && !line.StartsWith("| Installation", StringComparison.Ordinal))
                .ToArray();

            string[] package = rows.Where(row => row.Contains("`" + SetupDownloadPage.PackageRelease + "`")).ToArray();
            rows = rows.Except(package).ToArray();
            Assert.That(package, Has.Length.LessThanOrEqualTo(1), "at most one row for an installation of the suite");
            Assert.That(rows, Has.Length.EqualTo(3), "one row per page: EE, NeoEE, foreign or unknown");
            string[] pages = rows.Select(row => Regex.Match(row, @"`(https://[^`]+)`\s*\|\s*$").Groups[1].Value).ToArray();
            Assert.That(pages, Is.EqualTo(new[] { SetupDownloadPage.EmpireEarth, SetupDownloadPage.NeoEE, SetupDownloadPage.General }));
            Assert.That(rows[0], Does.Contain("`EE`").And.Contain("`community`").And.Contain("`community-legacy`"));
            Assert.That(rows[1], Does.Contain("`NeoEE`").And.Contain("`community`").And.Contain("`community-legacy`"));
            Assert.That(rows[2], Does.Contain("`foreign`"));
        }

        [Test]
        public void NameOf_NamesThePageForTheLog()
        {
            Assert.That(SetupDownloadPage.NameOf(SetupDownloadPage.EmpireEarth), Is.EqualTo("EE"));
            Assert.That(SetupDownloadPage.NameOf(SetupDownloadPage.NeoEE), Is.EqualTo("NeoEE"));
            Assert.That(SetupDownloadPage.NameOf(SetupDownloadPage.General), Is.EqualTo("general"));
            Assert.That(SetupDownloadPage.NameOf(SetupDownloadPage.PackageRelease), Is.EqualTo("package release"));
        }

        [TestCase(1155)]
        [TestCase(5)]
        public void Open_AFailure_IsLoggedNotThrown(int error)
        {
            var starter = new FakeProcessStarter { OpenException = new System.ComponentModel.Win32Exception(error) };
            var logger = new RecordingLogger();

            DownloadPageResult result = SetupDownloadPage.Open(SetupDownloadPage.PackageRelease, starter, logger);

            Assert.That(result, Is.EqualTo(DownloadPageResult.Failed));
            Assert.That(logger.Entries.Single().Exception, Is.SameAs(starter.OpenException));
            Assert.That(logger.Entries.Single().Message, Is.EqualTo("The download page " + SetupDownloadPage.PackageRelease + " could not be opened."));
        }

        [Test]
        public void Open_OpensThePageThroughTheShell_AndLogsNothingElse()
        {
            var starter = new FakeProcessStarter();
            var logger = new RecordingLogger();

            Assert.That(SetupDownloadPage.Open(SetupDownloadPage.PackageRelease, starter, logger), Is.EqualTo(DownloadPageResult.Opened));

            Assert.That(starter.OpenedUrls, Is.EqualTo(new[] { SetupDownloadPage.PackageRelease }));
            Assert.That(starter.Started, Is.Empty);
            Assert.That(logger.Messages, Is.Empty, "the caller says why it opens the page");
            Assert.That(() => SetupDownloadPage.Open(null, starter, logger), Throws.ArgumentNullException);
            Assert.That(() => SetupDownloadPage.Open(SetupDownloadPage.General, null, logger), Throws.ArgumentNullException);
            Assert.That(() => SetupDownloadPage.Open(SetupDownloadPage.General, starter, null), Throws.ArgumentNullException);
        }
    }
}

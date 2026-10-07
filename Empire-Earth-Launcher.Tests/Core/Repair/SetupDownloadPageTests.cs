using System;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Repair;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.Repair
{
    /// <summary>
    /// <see cref="SetupDownloadPage"/> (contract 4.3, ADR 0008 amendment of 1.1.0): the download page is chosen by the
    /// installation alone, one page per product and the general page for a foreign installation, an unknown product and
    /// none.
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

        [Test]
        public void NameOf_NamesThePageForTheLog()
        {
            Assert.That(SetupDownloadPage.NameOf(SetupDownloadPage.EmpireEarth), Is.EqualTo("EE"));
            Assert.That(SetupDownloadPage.NameOf(SetupDownloadPage.NeoEE), Is.EqualTo("NeoEE"));
            Assert.That(SetupDownloadPage.NameOf(SetupDownloadPage.General), Is.EqualTo("general"));
        }
    }
}

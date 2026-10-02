using System.Collections.Generic;
using Empire_Earth_Launcher.Core.Repair;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.Repair
{
    /// <summary>
    /// <see cref="UpdateUrlPolicy"/>, the port of the setup's <c>IsAllowedUpdateUrl</c> (contract 4.3 step 2, ADR 0008). The
    /// first table is <c>TestIsAllowedUpdateUrl</c> of the setup's <c>ci/tests/unit_tests.iss</c>, case by case with the same
    /// names, URLs and expectations (a script of the work package compares both); the second holds the launcher's own cases.
    /// </summary>
    [TestFixture]
    public class UpdateUrlPolicyTests
    {
        private static TestCaseData Case(string name, string url, bool allowed)
        {
            return new TestCaseData(url).Returns(allowed).SetName(name);
        }

        /// <summary>The 13 cases of the setup's <c>TestIsAllowedUpdateUrl</c>, in its order.</summary>
        public static IEnumerable<TestCaseData> SetupCases()
        {
            yield return Case("IsAllowedUpdateUrl website", "https://empireearth.eu/download", true);
            yield return Case("IsAllowedUpdateUrl website subdomain", "https://files.empireearth.eu/setup.exe", true);
            yield return Case("IsAllowedUpdateUrl NeoEE", "https://www.neoee.net/download", true);
            yield return Case("IsAllowedUpdateUrl GitHub project", "https://github.com/EE-modders/Empire-Earth-Setup/releases", true);
            yield return Case("IsAllowedUpdateUrl GitHub other", "https://github.com/someone/Empire-Earth-Setup/releases", false);
            yield return Case("IsAllowedUpdateUrl GitHub dot segment", "https://github.com/EE-modders/../someone/x", false);
            yield return Case("IsAllowedUpdateUrl GitHub escape", "https://github.com/EE-modders/%2e%2e/someone/x", false);
            yield return Case("IsAllowedUpdateUrl GitHub root", "https://github.com/", false);
            yield return Case("IsAllowedUpdateUrl mirror", "https://storage.ee.zocker-160.de/setup.exe", false);
            yield return Case("IsAllowedUpdateUrl http", "http://empireearth.eu/download", false);
            yield return Case("IsAllowedUpdateUrl look-alike", "https://empireearth.eu.evil.example/", false);
            yield return Case("IsAllowedUpdateUrl user info", "https://empireearth.eu@evil.example/", false);
            yield return Case("IsAllowedUpdateUrl empty", "", false);
        }

        /// <summary>The launcher's own cases: port, case of the scheme and host, spaces, non-ASCII, % on GitHub, and more.</summary>
        public static IEnumerable<TestCaseData> LauncherCases()
        {
            yield return Case("Launcher port", "https://empireearth.eu:443/download", false);
            yield return Case("Launcher port of GitHub", "https://github.com:8443/EE-modders/x", false);
            yield return Case("Launcher scheme in capitals", "HTTPS://empireearth.eu/download", true);
            yield return Case("Launcher host in capitals", "https://EmpireEarth.EU/download", true);
            yield return Case("Launcher GitHub organization in other case", "https://github.com/ee-MODDERS/Empire-Earth-Setup", true);
            yield return Case("Launcher space in the path", "https://empireearth.eu/down load", false);
            yield return Case("Launcher space at the end", "https://empireearth.eu/download ", false);
            yield return Case("Launcher tab", "https://empireearth.eu/\tdownload", false);
            yield return Case("Launcher non-ASCII in the path", "https://empireearth.eu/téléchargement", false);
            yield return Case("Launcher non-ASCII host", "https://empireearth.eü/download", false);
            yield return Case("Launcher percent escape on GitHub", "https://github.com/EE-modders/Empire%20Earth", false);
            yield return Case("Launcher percent escape on the website", "https://empireearth.eu/a%20b", true);
            yield return Case("Launcher backslash", @"https://empireearth.eu\@evil.example/", false);
            yield return Case("Launcher no path", "https://empireearth.eu", true);
            yield return Case("Launcher query only", "https://neoee.net?setup", true);
            yield return Case("Launcher fragment trick", "https://evil.example#empireearth.eu", false);
            yield return Case("Launcher dot only subdomain", "https://.empireearth.eu/", false);
            yield return Case("Launcher trailing dot host", "https://empireearth.eu./download", false);
            yield return Case("Launcher prefix of the domain", "https://myempireearth.eu/download", false);
            yield return Case("Launcher GitHub subdomain", "https://gist.github.com/EE-modders/x", false);
            yield return Case("Launcher GitHub without the slash", "https://github.com/EE-modders", false);
            yield return Case("Launcher no host", "https:///download", false);
            yield return Case("Launcher null", null, false);
            yield return Case("Launcher only the scheme", "https://", false);
            yield return Case("Launcher ftp", "ftp://empireearth.eu/download", false);
        }

        [TestCaseSource(nameof(SetupCases))]
        public bool TheCasesOfTheSetup(string url)
        {
            return UpdateUrlPolicy.IsAllowed(url);
        }

        [TestCaseSource(nameof(LauncherCases))]
        public bool TheCasesOfTheLauncher(string url)
        {
            return UpdateUrlPolicy.IsAllowed(url);
        }

        [Test]
        public void TheSetupTable_HasItsThirteenCases()
        {
            Assert.That(new List<TestCaseData>(SetupCases()).Count, Is.EqualTo(13));
        }

        [Test]
        public void SplitHttpsUrl_LikeTheSetup()
        {
            Assert.That(UpdateUrlPolicy.TrySplitHttpsUrl("https://Files.EmpireEarth.eu/a?b#c", out string host, out string path), Is.True);
            Assert.That(host, Is.EqualTo("files.empireearth.eu"));
            Assert.That(path, Is.EqualTo("/a?b#c"));
            Assert.That(UpdateUrlPolicy.TrySplitHttpsUrl("https://neoee.net", out host, out path), Is.True);
            Assert.That(path, Is.EqualTo("/"));
        }

        [Test]
        public void IsDomainOrSubdomain_LikeTheSetup()
        {
            Assert.That(UpdateUrlPolicy.IsDomainOrSubdomain("empireearth.eu", "empireearth.eu"), Is.True);
            Assert.That(UpdateUrlPolicy.IsDomainOrSubdomain("a.empireearth.eu", "empireearth.eu"), Is.True);
            Assert.That(UpdateUrlPolicy.IsDomainOrSubdomain("aempireearth.eu", "empireearth.eu"), Is.False);
            Assert.That(UpdateUrlPolicy.IsDomainOrSubdomain(".empireearth.eu", "empireearth.eu"), Is.False);
            Assert.That(UpdateUrlPolicy.IsDomainOrSubdomain("empireearth.eu.evil.example", "empireearth.eu"), Is.False);
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Empire_Earth_Launcher.Tests.TestSupport;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Architecture
{
    /// <summary>
    /// No source of the launcher, its libraries or the mod creator weakens the validation of server certificates (ADR 0008
    /// and its design review amendment, R16): certificate problems such as the wrong certificate of
    /// <c>files.empireearth.eu</c> are never "fixed" by turning validation off. Every name of the broad list of ADR 0008 is
    /// forbidden outside the test project, whether assigned, read or implemented; comment lines are skipped.
    /// </summary>
    [TestFixture]
    public class NoCertificateOverrideTests
    {
        /// <summary>The forbidden names and assignments.</summary>
        private static readonly Regex Forbidden = new Regex(
            @"\b(ServerCertificateValidationCallback|ServerCertificateCustomValidationCallback|RemoteCertificateValidationCallback|" +
            @"DangerousAcceptAnyServerCertificateValidator|ICertificatePolicy|CertificatePolicy)\b|" +
            @"\bCheckCertificateRevocationList\s*=\s*false\b",
            RegexOptions.CultureInvariant);

        private static List<string> Offenders(IEnumerable<SourceLine> lines)
        {
            return lines.Where(line => Forbidden.IsMatch(line.Text)).Select(line => line.ToString()).ToList();
        }

        [Test]
        [Category(TestCategories.SourceTree)]
        public void NoSource_OverridesTheCertificateValidation()
        {
            List<SourceLine> lines = ProductionSources.CodeLines().ToList();
            Assert.That(lines.Select(line => line.File).Distinct().Count(), Is.GreaterThan(50), "the sources were not found");
            Assert.That(lines.Any(line => line.File == "Empire-Earth-Launcher-Core/Platform/HttpsClient.cs"), Is.True,
                "the HTTPS client is one of the files checked");

            Assert.That(Offenders(lines), Is.Empty, "certificate validation is never turned off (ADR 0008)");
        }

        [TestCase("ServicePointManager.ServerCertificateValidationCallback += (s, c, ch, e) => true;")]
        [TestCase("handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;")]
        [TestCase("handler.ServerCertificateCustomValidationCallback = (m, c, ch, e) => true;")]
        [TestCase("var stream = new SslStream(inner, false, new RemoteCertificateValidationCallback(Accept));")]
        [TestCase("ServicePointManager.CertificatePolicy = new TrustAll();")]
        [TestCase("class TrustAll : ICertificatePolicy")]
        [TestCase("handler.CheckCertificateRevocationList = false;")]
        [TestCase("ServicePointManager.CheckCertificateRevocationList=false;")]
        public void TheRule_FindsEveryOverride(string line)
        {
            Assert.That(Offenders(new[] { new SourceLine("Sample.cs", 1, line) }), Has.Count.EqualTo(1));
        }

        [TestCase("return new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false };")]
        [TestCase("handler.CheckCertificateRevocationList = true;")]
        [TestCase("ServerCertificateValidationCallbacks are not used")]
        public void TheRule_AllowsTheClientOfTheLauncher(string line)
        {
            Assert.That(Offenders(new[] { new SourceLine("Sample.cs", 1, line) }), Is.Empty);
        }
    }
}

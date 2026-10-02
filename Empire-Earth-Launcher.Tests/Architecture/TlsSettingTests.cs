using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using Empire_Earth_Launcher.Tests.TestSupport;
using NUnit.Framework;
using LauncherProgram = Empire_Earth_Launcher.Program;

namespace Empire_Earth_Launcher.Tests.Architecture
{
    /// <summary>
    /// The TLS versions of the launcher (ADR 0008 plan review, REV-12): <c>Program</c> sets
    /// <see cref="ServicePointManager.SecurityProtocol"/> exactly once, to exactly <see cref="SecurityProtocolType.Tls12"/> on
    /// Windows 7 (NT 6.1) and not at all elsewhere (<see cref="SecurityProtocolType.SystemDefault"/>). No source names TLS 1.3
    /// (handshakes fail where SChannel lacks it), TLS 1.1, TLS 1.0 or SSL 3, or casts a number to the type.
    /// </summary>
    [TestFixture]
    public class TlsSettingTests
    {
        private const string ProgramFile = "Empire Earth Launcher/Program.cs";

        private static readonly Regex ForbiddenVersion = new Regex(
            @"\bSecurityProtocolType\s*\.\s*(Tls13|Tls11|Tls|Ssl3)\b|\(\s*SecurityProtocolType\s*\)\s*\d", RegexOptions.CultureInvariant);

        /// <summary>An assignment to the property (also a compound one), not a comparison.</summary>
        private static readonly Regex Assignment = new Regex(@"\bSecurityProtocol\s*(\|=|&=|\^=|=(?!=))", RegexOptions.CultureInvariant);

        [TestCase(6, 1, 7601)]
        [TestCase(6, 1, 0)]
        public void Windows7_AsksForExactlyTls12(int major, int minor, int build)
        {
            Assert.That(LauncherProgram.TlsProtocolsFor(new Version(major, minor, build)), Is.EqualTo(SecurityProtocolType.Tls12));
        }

        [TestCase(6, 2)]
        [TestCase(6, 3)]
        [TestCase(10, 0)]
        public void LaterWindows_KeepTheSystemDefault(int major, int minor)
        {
            Assert.That(LauncherProgram.TlsProtocolsFor(new Version(major, minor)), Is.Null);
        }

        [Test]
        [Category(TestCategories.SourceTree)]
        public void NoSource_NamesAnotherTlsVersion()
        {
            List<string> offenders = ProductionSources.CodeLines().Where(line => ForbiddenVersion.IsMatch(line.Text))
                                                     .Select(line => line.ToString()).ToList();

            Assert.That(offenders, Is.Empty, "only Tls12 (Windows 7) or the system default (ADR 0008 plan review)");
        }

        [Test]
        [Category(TestCategories.SourceTree)]
        public void OnlyProgram_SetsTheProtocols_Once()
        {
            List<SourceLine> assignments = ProductionSources.CodeLines().Where(line => Assignment.IsMatch(line.Text)).ToList();

            Assert.That(assignments.Select(line => line.File), Is.EqualTo(new[] { ProgramFile }),
                string.Join(Environment.NewLine, assignments));
            Assert.That(assignments.Single().Text, Does.Contain("ServicePointManager.SecurityProtocol = protocols.Value"));
        }

        [TestCase("ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;")]
        [TestCase("ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls11;")]
        [TestCase("var old = SecurityProtocolType.Tls;")]
        [TestCase("handler.SslProtocols = (SslProtocols)SecurityProtocolType.Ssl3;")]
        [TestCase("ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;")]
        public void TheVersionRule_FindsOtherVersions(string line)
        {
            Assert.That(ForbiddenVersion.IsMatch(line), Is.True);
        }

        [TestCase("return SecurityProtocolType.Tls12;")]
        [TestCase("SecurityProtocolType.SystemDefault")]
        public void TheVersionRule_AllowsTls12AndTheDefault(string line)
        {
            Assert.That(ForbiddenVersion.IsMatch(line), Is.False);
        }

        [TestCase("ServicePointManager.SecurityProtocol = protocols.Value;", true)]
        [TestCase("ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;", true)]
        [TestCase("ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;", true)]
        [TestCase("if (ServicePointManager.SecurityProtocol == SecurityProtocolType.SystemDefault)", false)]
        [TestCase("log.Info(\"TLS: \" + ServicePointManager.SecurityProtocol);", false)]
        public void TheAssignmentRule(string line, bool isAssignment)
        {
            Assert.That(Assignment.IsMatch(line), Is.EqualTo(isAssignment));
        }
    }
}

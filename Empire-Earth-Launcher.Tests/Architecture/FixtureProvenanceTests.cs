using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Empire_Earth_Launcher.Tests.TestSupport;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Architecture
{
    /// <summary>
    /// Test data is synthetic (ADR 0012 plan review, REV-15, briefing D6: game data is never committed): every token of 64
    /// hex digits in the fixture files of the test project (golden files and other data files), in its sources and in
    /// <c>docs/contract-samples/</c> is the SHA-256 of the ASCII text <c>sample-&lt;n&gt;</c> (0 to 9999), which is also the
    /// content of every file the integrity tests create (<see cref="SampleHashes"/>). A hash of a real game file, of an
    /// official installer or of a reconstruction fails the test.
    /// </summary>
    [TestFixture]
    public class FixtureProvenanceTests
    {
        private const string TestProjectFolder = "Empire-Earth-Launcher.Tests";
        private const string ContractSamplesFolder = "docs/contract-samples";

        /// <summary>64 hex digits that are not part of a longer run of hex digits.</summary>
        private static readonly Regex HashToken = new Regex(@"(?<![0-9A-Fa-f])[0-9A-Fa-f]{64}(?![0-9A-Fa-f])",
            RegexOptions.CultureInvariant);

        private static readonly Lazy<HashSet<string>> Synthetic = new Lazy<HashSet<string>>(() =>
            new HashSet<string>(Enumerable.Range(0, SampleHashes.MaxSample + 1).Select(SampleHashes.Of), StringComparer.OrdinalIgnoreCase));

        /// <summary>The text of a file: UTF-16 LE with its BOM (the <c>.reg</c> files), else UTF-8.</summary>
        private static string Text(byte[] bytes)
        {
            return bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE
                ? Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2)
                : new UTF8Encoding(false, false).GetString(bytes);
        }

        /// <summary>Every token of 64 hex digits in <paramref name="text"/> that is no synthetic hash.</summary>
        private static IEnumerable<string> Foreign(string text)
        {
            return HashToken.Matches(text).Cast<Match>().Select(match => match.Value).Where(token => !Synthetic.Value.Contains(token));
        }

        /// <summary>
        /// The files checked, relative to the repository root: everything of the test project except the build folders and
        /// its project and configuration files (golden files, sources, other fixtures), and the contract samples.
        /// </summary>
        private static IReadOnlyList<string> CheckedFiles()
        {
            string[] skipped = { ".csproj", ".config" };
            IEnumerable<string> tests = Directory.EnumerateFiles(RepositoryRoot.GetFullPath(TestProjectFolder), "*", SearchOption.AllDirectories)
                .Select(RepositoryRoot.ToRelativePath)
                .Where(file => !file.Split('/').Any(part => part == "bin" || part == "obj"))
                .Where(file => !skipped.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase));
            IEnumerable<string> samples = Directory.EnumerateFiles(RepositoryRoot.GetFullPath(ContractSamplesFolder))
                .Select(RepositoryRoot.ToRelativePath);
            return tests.Concat(samples).OrderBy(file => file, StringComparer.Ordinal).ToList();
        }

        [Test]
        [Category(TestCategories.SourceTree)]
        public void EveryHashOfTheFixtures_IsSynthetic()
        {
            IReadOnlyList<string> files = CheckedFiles();
            Assert.That(files, Has.Some.EqualTo(ContractSamplesFolder + "/files.sha256"));
            Assert.That(files, Has.Some.StartsWith(TestProjectFolder + "/Core/Backup/Golden/"));

            var offenders = new List<string>();
            int tokens = 0;
            foreach (string file in files)
            {
                string text = Text(File.ReadAllBytes(RepositoryRoot.GetFullPath(file)));
                tokens += HashToken.Matches(text).Count;
                offenders.AddRange(Foreign(text).Select(token => file + ": " + token));
            }

            Assert.That(tokens, Is.GreaterThanOrEqualTo(10), "the hashes of the manifest sample are checked");
            Assert.That(offenders, Is.Empty, "fixtures hold only SHA-256 of sample-<n> (ADR 0012 plan review)");
        }

        [Test]
        public void TheRule_AcceptsTheSamples_InAnyCase()
        {
            Assert.That(Foreign(SampleHashes.Of(0) + "  a.dll\n" + SampleHashes.Of(9999).ToUpperInvariant() + " *b.dll"), Is.Empty);
        }

        [Test]
        public void TheRule_FindsEveryOtherHash()
        {
            string other = SampleHashes.Sha256(Encoding.ASCII.GetBytes("Empire Earth.exe of a real installation"));
            string outOfRange = SampleHashes.Sha256(Encoding.ASCII.GetBytes("sample-10000"));

            Assert.That(Foreign(other + "  Empire Earth/Empire Earth.exe\n"), Is.EqualTo(new[] { other }));
            Assert.That(Foreign("\"" + outOfRange + "\""), Is.EqualTo(new[] { outOfRange }));
            Assert.That(Foreign(Text(Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes("\"x\"=\"" + other + "\"")).ToArray())),
                Is.EqualTo(new[] { other }), "also in UTF-16 files");
        }

        [Test]
        public void TheRule_IgnoresShorterAndLongerRunsOfHexDigits()
        {
            string sample = SampleHashes.Of(1);

            Assert.That(HashToken.IsMatch(sample.Substring(1)), Is.False, "63 digits");
            Assert.That(HashToken.IsMatch(sample + "0"), Is.False, "65 digits");
            Assert.That(HashToken.IsMatch("x" + sample + "x"), Is.True);
        }
    }
}

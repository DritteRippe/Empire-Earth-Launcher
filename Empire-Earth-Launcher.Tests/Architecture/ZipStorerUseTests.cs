using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Empire_Earth_Launcher.Tests.TestSupport;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Architecture
{
    /// <summary>
    /// How the programs use the vendored ZipStorer (third-party code, see THIRD-PARTY-NOTICES.md). Only
    /// <c>ModArchiveReader</c> reads entries (<c>ExtractFile</c>), through the guards this version needs: the size limit of
    /// the data entry, a bounded output stream (ExtractFile does not stop when the input ends early) and the check of the
    /// file paths (EemFormat.IsValidFilePath). <c>RemoveEntries</c> is not used at all: it deletes the archive before it moves
    /// the new copy into its place and turns every error into "false", so a failure can lose the archive. A new place that
    /// extracts entries, such as an installer of mods, needs the same guards, writes files only below their product folder
    /// and is then added here. Checked on the sources of the launcher, its libraries and the mod creator; comment lines are
    /// skipped.
    /// </summary>
    [TestFixture]
    [Category(TestCategories.SourceTree)]
    public class ZipStorerUseTests
    {
        private const string ZipStorerSource = "Empire-Earth-Mod/Empire-Earth-Mod-Lib/ZipStorer.cs";
        private const string ArchiveReader = "Empire-Earth-Mod/Empire-Earth-Mod-Lib/ModArchiveReader.cs";

        private static readonly Regex Extract = new Regex(@"\bExtractFile(?:Async)?\s*\(", RegexOptions.CultureInvariant);
        private static readonly Regex RemoveEntries = new Regex(@"\bRemoveEntries\s*\(", RegexOptions.CultureInvariant);

        /// <summary>The code lines of every source but the vendored ZipStorer itself.</summary>
        private static List<SourceLine> CodeLinesOutsideZipStorer()
        {
            List<SourceLine> lines = ProductionSources.CodeLines().Where(line => line.File != ZipStorerSource).ToList();
            Assert.That(lines.Select(line => line.File).Distinct().Count(), Is.GreaterThan(50), "the sources were not found");
            Assert.That(lines.Any(line => line.File == ArchiveReader), Is.True, "the mod archive reader is one of the files checked");
            return lines;
        }

        private static List<string> Matching(IEnumerable<SourceLine> lines, Regex pattern)
        {
            return lines.Where(line => pattern.IsMatch(line.Text)).Select(line => line.ToString()).ToList();
        }

        [Test]
        public void OnlyTheModArchiveReader_ExtractsEntries()
        {
            List<string> extracting = Matching(CodeLinesOutsideZipStorer(), Extract);

            Assert.That(extracting, Is.Not.Empty, "the check found no extraction at all");
            Assert.That(extracting, Is.All.StartsWith(ArchiveReader + ":"));
        }

        [Test]
        public void NoSource_RemovesEntriesWithZipStorer()
        {
            Assert.That(Matching(CodeLinesOutsideZipStorer(), RemoveEntries), Is.Empty);
        }

        [TestCase("if (!zip.ExtractFile(entry, output) || output.Length == 0)", true)]
        [TestCase("zip.ExtractFile(entry, Path.Combine(gameFolder, entry.FilenameInZip));", true)]
        [TestCase("bool ok = await zip.ExtractFileAsync (entry, stream);", true)]
        [TestCase("private static byte[] ExtractEntry(ZipStorer zip, ZipStorer.ZipFileEntry entry)", false)]
        public void TheCheck_FindsAnExtraction(string line, bool extracts)
        {
            Assert.That(Matching(new[] { new SourceLine("Sample.cs", 1, line) }, Extract), Has.Count.EqualTo(extracts ? 1 : 0));
        }

        [TestCase("ZipStorer.RemoveEntries(ref zip, entries);", true)]
        [TestCase("RemoveEntries( ref zip, new List<ZipStorer.ZipFileEntry> { entry });", true)]
        [TestCase("mod.ModFiles.RemoveAll(modFile => modFile.Variant == variant);", false)]
        public void TheCheck_FindsRemoveEntries(string line, bool removes)
        {
            Assert.That(Matching(new[] { new SourceLine("Sample.cs", 1, line) }, RemoveEntries), Has.Count.EqualTo(removes ? 1 : 0));
        }
    }
}

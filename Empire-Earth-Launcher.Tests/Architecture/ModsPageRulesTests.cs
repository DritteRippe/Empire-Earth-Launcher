using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Empire_Earth_Launcher.Tests.TestSupport;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Architecture
{
    /// <summary>
    /// The rules of the Mods page of launcher 1.1.0 (ADR 0014, contract 2.5), kept in the sources: the page and everything behind
    /// it only read. The launcher neither changes <c>dreXmod.config</c> nor installs, removes or switches a preset (editing the
    /// config comes in 1.2 with an allow-list); the two buttons only open the folder and the file through the shell.
    /// </summary>
    [TestFixture]
    [Category(TestCategories.SourceTree)]
    public class ModsPageRulesTests
    {
        private const string Core = "Empire-Earth-Launcher-Core/";
        private const string Ui = "Empire Earth Launcher/";

        private static readonly string[] Sources =
        {
            Core + "Mods/CreditsParser.cs", Core + "Mods/DreXmodConfigReader.cs", Core + "Mods/DreXmodInfo.cs",
            Core + "Mods/ModFolderScanner.cs", Ui + "ModsModel.cs", Ui + "ModsView.cs", Ui + "ModsUserControl.cs",
        };

        /// <summary>What changes a file, a folder or the registry: the calls of <c>IFileSystem</c>, <c>File</c>, <c>Directory</c> and the registry.</summary>
        private static readonly Regex Writing = new Regex(
            @"\.(WriteAllBytes|WriteAllBytesAtomically|WriteAllText|WriteAllLines|AppendAllText|DeleteFile|CreateDirectory|SetValue|" +
            @"CreateSubKey|DeleteValue|DeleteSubKeyTree)\s*\(|\b(File|Directory)\s*\.\s*(Create|Delete|Move|Copy|Replace|Write\w*|Append\w*)\s*\(|" +
            @"\b(fileSystem|FileSystem)\s*\.\s*(Replace|Move)\s*\(|\bFileStream\b|\bStreamWriter\b|\bIRegistry\b|\bMutationGuard\b|\bFileBackup\b",
            RegexOptions.CultureInvariant);

        private static string Code(string relativePath)
        {
            string text = File.ReadAllText(RepositoryRoot.GetFullPath(relativePath));
            return string.Join("\n", text.Split('\n').Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal)));
        }

        [Test]
        public void TheSourcesOfTheModsPage_ExistAndAreChecked()
        {
            foreach (string source in Sources)
                Assert.That(File.Exists(RepositoryRoot.GetFullPath(source)), Is.True, source);
        }

        [TestCaseSource(nameof(Sources))]
        public void TheModsPage_WritesNoFileAndNoRegistryValue(string source)
        {
            MatchCollection writing = Writing.Matches(Code(source));

            Assert.That(writing.Cast<Match>().Select(match => match.Value), Is.Empty, source + " must only read (ADR 0014)");
        }

        [Test]
        public void TheButtonsOfTheModsPage_OnlyOpenTheFolderAndTheFileThroughTheShell()
        {
            string model = Code(Ui + "ModsModel.cs");

            Assert.That(Regex.Matches(model, @"shell\.(\w+)\(").Cast<Match>().Select(match => match.Groups[1].Value).Distinct(),
                Is.EquivalentTo(new[] { "OpenFolder", "OpenFile" }), "StartProgram would run something; OpenUrl is not needed");
        }

        [Test]
        public void TheModsPage_NamesNoPathOfAnotherFile()
        {
            // dgVoodoo.conf is read by the Graphics page and never by the Mods page; the setup's files stay the setup's.
            foreach (string source in Sources)
                Assert.That(Code(source), Does.Not.Contain("dgVoodoo").And.Not.Contain("authtools").And.Not.Contain("CDKeys"), source);
        }

        [Test]
        public void TheModsPage_IsNotWiredToAnythingThatChangesTheGame()
        {
            string program = File.ReadAllText(RepositoryRoot.GetFullPath(Ui + "Program.cs"));
            Match creation = Regex.Match(program, @"new ModsModel\(([^;]*)\);");

            Assert.That(creation.Success, Is.True);
            Assert.That(creation.Groups[1].Value, Does.Not.Contain("defaults").And.Not.Contain("backups").And.Not.Contain("guard")
                .And.Not.Contain("registry"));
        }
    }
}

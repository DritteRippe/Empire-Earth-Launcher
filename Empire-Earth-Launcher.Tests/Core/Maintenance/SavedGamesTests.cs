using System;
using System.IO;
using System.Linq;
using Empire_Earth_Launcher.Core.Backup;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Maintenance;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Core.Play;
using Empire_Earth_Launcher.Tests.Fakes;
using Empire_Earth_Launcher.Tests.TestSupport;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.Maintenance
{
    /// <summary>
    /// <see cref="SavedGames"/> (R10, forum t=9004 p=44629, t=5747 p=38762, forum report section 8 row 16 and test case 17,
    /// ADR 0016): the folder export of both games from the game folder and the VirtualStore (the copy wins a name conflict, the
    /// other is listed), and the checked import of single files where the game reads them, behind the mutation guard, with a
    /// backup of a replaced file. With the in-memory file system and with real files in a temporary folder.
    /// </summary>
    [TestFixture]
    public class SavedGamesTests
    {
        private const string Root = @"C:\Program Files (x86)\Neo Empire Earth";
        private const string EeSaves = Root + @"\Empire Earth\Data\Saved Games";
        private const string EeScenarios = Root + @"\Empire Earth\Data\Scenarios";
        private const string AocSaves = Root + @"\Empire Earth - The Art of Conquest\Data\Saved Games";
        private const string VirtualStore = @"C:\Users\Player\AppData\Local\VirtualStore";
        private const string EeSavesCopy = VirtualStore + @"\Program Files (x86)\Neo Empire Earth\Empire Earth\Data\Saved Games";
        private const string Downloads = @"C:\Users\Player\Downloads";
        private const string Exports = @"C:\Users\Player\Documents";

        /// <summary>The size limit of the tests in place of 64 MiB.</summary>
        private const long Limit = 1024;

        private static readonly string[] Virtualized =
            { @"C:\Program Files", @"C:\Program Files (x86)", @"C:\ProgramData", @"C:\Windows" };

        private GameSettingsWorld w;

        [SetUp]
        public void SetUp()
        {
            w = new GameSettingsWorld();
            w.World.AddCommunityInstallation(Root, Product.NeoEE);
            w.FileSystem.AddDirectory(Exports);
        }

        private SavedGames Create(IFileSystem fileSystem = null)
        {
            IFileSystem files = fileSystem ?? w.FileSystem;
            return new SavedGames(files, new EffectivePathResolver(files, VirtualStore, Virtualized), w.SystemInfo, w.Guard,
                new FileBackup(files, w.Backups, w.Logger), w.World.Clock, w.Logger, Limit);
        }

        private Installation Installation()
        {
            return InstallationWorld.ByRoot(w.Discover(), Root);
        }

        // --- List and export ---------------------------------------------------------------------------------------------

        [Test]
        public void List_MergesTheGameFolderAndTheVirtualStore_TheCopyWins()
        {
            w.FileSystem.AddFile(EeSaves + @"\Duel.ees", "old");
            w.FileSystem.AddFile(EeSaves + @"\Campaign.ees", "c");
            w.FileSystem.AddFile(EeSaves + @"\notes.txt", "n");
            w.FileSystem.AddFile(EeSavesCopy + @"\Duel.ees", "new");
            w.FileSystem.AddFile(EeScenarios + @"\Island.scn", "s");
            w.FileSystem.AddFile(AocSaves + @"\Mars.ees", "m");

            var files = Create(new WriteForbiddingFileSystem(w.FileSystem)).List(Installation());

            Assert.That(files.Select(file => file.Path), Is.EqualTo(new[]
            {
                EeSaves + @"\Campaign.ees", EeSavesCopy + @"\Duel.ees", EeScenarios + @"\Island.scn", AocSaves + @"\Mars.ees"
            }));
            SavedGameFile duel = files[1];
            Assert.That(duel.IsVirtualStoreCopy, Is.True);
            Assert.That(duel.ShadowedPath, Is.EqualTo(EeSaves + @"\Duel.ees"));
            Assert.That(files.Select(file => file.Kind), Is.EqualTo(new[]
                { SavedGameKind.SavedGame, SavedGameKind.SavedGame, SavedGameKind.Scenario, SavedGameKind.SavedGame }));
            Assert.That(files.Select(file => file.Game.Id), Is.EqualTo(new[] { "EE", "EE", "EE", "AoC" }));
        }

        [Test]
        public void Export_CopiesBothGamesIntoANewFolder_AndListsTheHiddenFile()
        {
            w.FileSystem.AddFile(EeSaves + @"\Duel.ees", "old");
            w.FileSystem.AddFile(EeSavesCopy + @"\Duel.ees", "new");
            w.FileSystem.AddFile(EeScenarios + @"\Island.scn", "s");
            w.FileSystem.AddFile(AocSaves + @"\Mars.ees", "m");

            ExportResult result = Create().Export(Installation(), Exports);

            Assert.That(result.Outcome, Is.EqualTo(ExportOutcome.Done));
            Assert.That(WinPath.GetParent(result.Folder), Is.EqualTo(Exports));
            Assert.That(WinPath.GetFileName(result.Folder), Does.Match(@"^Empire Earth saves \d{4}-\d{2}-\d{2}_\d{6}$"));
            Assert.That(w.FileSystem.GetText(result.Folder + @"\EE\Saved Games\Duel.ees"), Is.EqualTo("new"), "the copy the game uses");
            Assert.That(w.FileSystem.GetText(result.Folder + @"\EE\Scenarios\Island.scn"), Is.EqualTo("s"));
            Assert.That(w.FileSystem.GetText(result.Folder + @"\AoC\Saved Games\Mars.ees"), Is.EqualTo("m"));
            Assert.That(result.Shadowed, Is.EqualTo(new[] { EeSaves + @"\Duel.ees" }));
            Assert.That(w.FileSystem.GetText(EeSaves + @"\Duel.ees"), Is.EqualTo("old"), "the game folders are only read");
        }

        [Test]
        public void Export_TwiceInOneSecond_MakesTwoFolders()
        {
            w.FileSystem.AddFile(EeSaves + @"\Duel.ees", "d");
            SavedGames saves = Create();

            string first = saves.Export(Installation(), Exports).Folder;
            string second = saves.Export(Installation(), Exports).Folder;

            Assert.That(second, Is.EqualTo(first + "_2"));
        }

        [TestCase(Root, ExportOutcome.TargetInsideGameFolder)]
        [TestCase(Root + @"\Empire Earth", ExportOutcome.TargetInsideGameFolder)]
        [TestCase(EeSaves, ExportOutcome.TargetInsideGameFolder)]
        [TestCase(EeSavesCopy, ExportOutcome.TargetInsideGameFolder)]
        [TestCase(@"C:\Missing", ExportOutcome.InvalidTarget)]
        [TestCase("Documents", ExportOutcome.InvalidTarget)]
        public void Export_RefusesAGameFolderAndAMissingFolder(string target, ExportOutcome expected)
        {
            w.FileSystem.AddFile(EeSaves + @"\Duel.ees", "d");
            w.FileSystem.AddDirectory(EeSavesCopy);

            Assert.That(Create().Export(Installation(), target).Outcome, Is.EqualTo(expected));
        }

        [Test]
        public void Export_WithoutFiles_SaysSo_AndAnUnreadableFileIsLeftOut()
        {
            Assert.That(Create().Export(Installation(), Exports).Outcome, Is.EqualTo(ExportOutcome.NothingToExport));

            w.FileSystem.AddFile(EeSaves + @"\a.ees", "a");
            w.FileSystem.AddFile(EeSaves + @"\b.ees", "b");
            w.FileSystem.FailOn(EeSaves + @"\b.ees", FileSystemOperation.Read, FileSystemStatus.AccessDenied);

            ExportResult result = Create().Export(Installation(), Exports);

            Assert.That(result.Outcome, Is.EqualTo(ExportOutcome.Partial));
            Assert.That(result.Exported.Select(file => file.Name), Is.EqualTo(new[] { "a.ees" }));
            Assert.That(result.Skipped.Single().Item2, Is.EqualTo(ExportSkip.Unreadable));
        }

        /// <summary>The export only reads the game folders, so a running game or setup does not block it (ADR 0016).</summary>
        [Test]
        public void Export_IsNotBlocked()
        {
            w.FileSystem.AddFile(EeSaves + @"\Duel.ees", "d");
            w.Mutexes.With("StainlessSteelStudiosPresentsEmpireEarth");

            Assert.That(Create().Export(Installation(), Exports).Outcome, Is.EqualTo(ExportOutcome.Done));
        }

        // --- Import ------------------------------------------------------------------------------------------------------

        [Test]
        public void TheLimitsOfTheChecks()
        {
            Assert.That(SavedGames.MaxFileBytes, Is.EqualTo(64L * 1024 * 1024));
            Assert.That(FileBackup.MaxFileBytes, Is.GreaterThanOrEqualTo(SavedGames.MaxFileBytes),
                "the old file an import replaces always fits into its backup");
            Assert.That(SavedGames.MaxNameLength, Is.EqualTo(200));
            Assert.That(SavedGames.KindOf("a.EES"), Is.EqualTo(SavedGameKind.SavedGame));
            Assert.That(SavedGames.KindOf("a.scn"), Is.EqualTo(SavedGameKind.Scenario));
            Assert.That(SavedGames.KindOf("a.zip"), Is.Null);
            Assert.That(SavedGames.FolderOf(SavedGameKind.Scenario), Is.EqualTo(@"Data\Scenarios"));
        }

        [Test]
        public void Import_WritesIntoTheFolderOfTheGame_KeepingTheName()
        {
            w.FileSystem.AddFile(Downloads + @"\Kampf um Köln.ees", "save");
            w.FileSystem.AddFile(Downloads + @"\Insel.scn", "scenario");
            SavedGames saves = Create();
            ImportPlan plan = saves.PlanImport(Installation(), Game.EmpireEarth,
                new[] { Downloads + @"\Kampf um Köln.ees", Downloads + @"\Insel.scn" });

            Assert.That(plan.NeedsOverwriteConfirmation, Is.False);
            Assert.That(plan.Files[0].NameOutsideAscii, Is.True, "a warning, not a refusal (in the ANSI code page)");
            ImportResult result = saves.Import(plan, false);

            Assert.That(result.ImportedCount, Is.EqualTo(2));
            Assert.That(w.FileSystem.GetText(EeSaves + @"\Kampf um Köln.ees"), Is.EqualTo("save"));
            Assert.That(w.FileSystem.GetText(EeScenarios + @"\Insel.scn"), Is.EqualTo("scenario"));
            Assert.That(result.BackupFolder, Is.Null);
        }

        [TestCase(@"\virus.exe", ImportCheck.WrongExtension)]
        [TestCase(@"\save.ees.exe", ImportCheck.WrongExtension)]
        [TestCase(@"\save", ImportCheck.WrongExtension)]
        [TestCase(@"\CON.ees", ImportCheck.NotAPlainName)]
        [TestCase(@"\.ees", ImportCheck.NotAPlainName)]
        [TestCase(@"\missing.ees", ImportCheck.NotFound)]
        [TestCase(@"\big.ees", ImportCheck.TooLarge)]
        [TestCase(@"\日本.ees", ImportCheck.NameOutsideAnsiCodePage)]
        public void Import_RefusesFilesThatFailTheChecks(string name, ImportCheck expected)
        {
            if (expected != ImportCheck.NotFound)
                w.FileSystem.AddFile(Downloads + name, expected == ImportCheck.TooLarge ? new byte[Limit + 1] : new byte[3]);
            SavedGames saves = Create();

            ImportPlan plan = saves.PlanImport(Installation(), Game.EmpireEarth, new[] { Downloads + name });
            ImportResult result = saves.Import(plan, true);

            Assert.That(plan.Files.Single().Check, Is.EqualTo(expected));
            Assert.That(result.Files.Single().Item2, Is.EqualTo(ImportFileOutcome.Refused));
            Assert.That(w.FileSystem.DirectoryExists(EeSaves), Is.False, "nothing written");
        }

        [Test]
        public void Import_RefusesTwoFilesOfTheSameName_AndAFileAlreadyInPlace()
        {
            w.FileSystem.AddFile(Downloads + @"\a\Duel.ees", "1");
            w.FileSystem.AddFile(Downloads + @"\b\duel.EES", "2");
            w.FileSystem.AddFile(EeSaves + @"\Here.ees", "h");

            ImportPlan plan = Create().PlanImport(Installation(), Game.EmpireEarth,
                new[] { Downloads + @"\a\Duel.ees", Downloads + @"\b\duel.EES", EeSaves + @"\Here.ees" });

            Assert.That(plan.Files.Select(file => file.Check),
                Is.EqualTo(new[] { ImportCheck.Ok, ImportCheck.DuplicateName, ImportCheck.AlreadyInPlace }));
        }

        /// <summary>A file the setup listed is never replaced (contract 2.5).</summary>
        [Test]
        public void Import_NeverReplacesAManifestFile()
        {
            w.FileSystem.AddFile(Root + @"\_setupdata_NeoEE\files.sha256", SampleHashes.Of(1) + "  Empire Earth/Data/Scenarios/Tutorial.scn\n");
            w.FileSystem.AddFile(EeScenarios + @"\Tutorial.scn", SampleHashes.Content(1));
            w.FileSystem.AddFile(Downloads + @"\Tutorial.scn", "mine");

            ImportPlan plan = Create().PlanImport(Installation(), Game.EmpireEarth, new[] { Downloads + @"\Tutorial.scn" });
            Create().Import(plan, true);

            Assert.That(plan.Files.Single().Check, Is.EqualTo(ImportCheck.ManifestFile));
            Assert.That(w.FileSystem.GetText(EeScenarios + @"\Tutorial.scn"), Is.EqualTo(SampleHashes.Content(1)));
        }

        /// <summary>
        /// A manifest that exists but cannot be used (security review): which files the setup installed is unknown, so nothing
        /// is imported, not even a confirmed replacement (contract 2.5, like the WON login reset).
        /// </summary>
        [TestCase("not a manifest line\n")]
        [TestCase(null)]
        public void Import_WithAManifestThatCannotBeUsed_ImportsNothing(string manifest)
        {
            string path = Root + @"\_setupdata_NeoEE\files.sha256";
            w.FileSystem.AddFile(path, (manifest ?? string.Empty) + SampleHashes.Of(1) + "  Empire Earth/Data/Scenarios/Tutorial.scn\n");
            if (manifest == null)
                w.FileSystem.FailOn(path, FileSystemOperation.Read, FileSystemStatus.AccessDenied);
            w.FileSystem.AddFile(EeScenarios + @"\Tutorial.scn", SampleHashes.Content(1));
            w.FileSystem.AddFile(Downloads + @"\Tutorial.scn", "mine");
            w.FileSystem.AddFile(Downloads + @"\Island.scn", "new");

            ImportPlan plan = Create().PlanImport(Installation(), Game.EmpireEarth,
                new[] { Downloads + @"\Tutorial.scn", Downloads + @"\Island.scn" });
            ImportResult result = Create().Import(plan, true);

            Assert.That(plan.Files.Select(file => file.Check), Is.EqualTo(new[] { ImportCheck.ManifestUnusable, ImportCheck.ManifestUnusable }));
            Assert.That(result.ImportedCount, Is.EqualTo(0));
            Assert.That(result.Files.Select(file => file.Item2), Is.All.EqualTo(ImportFileOutcome.Refused));
            Assert.That(w.FileSystem.GetText(EeScenarios + @"\Tutorial.scn"), Is.EqualTo(SampleHashes.Content(1)));
            Assert.That(w.FileSystem.FileExists(EeScenarios + @"\Island.scn"), Is.False);
            Assert.That(w.Logger.MessagesOf(Empire_Earth_Launcher.Core.Logging.LogLevel.Warning),
                Has.Some.StartsWith("Import of saved games: nothing can be imported"));
        }

        [Test]
        public void Import_ReplacesAFileOnlyAfterConfirmation_WithABackup()
        {
            w.FileSystem.AddFile(EeSaves + @"\Duel.ees", "old");
            w.FileSystem.AddFile(Downloads + @"\Duel.ees", "new");
            SavedGames saves = Create();
            ImportPlan plan = saves.PlanImport(Installation(), Game.EmpireEarth, new[] { Downloads + @"\Duel.ees" });
            Assert.That(plan.NeedsOverwriteConfirmation, Is.True);

            ImportResult refused = saves.Import(plan, false);
            Assert.That(refused.Files.Single().Item2, Is.EqualTo(ImportFileOutcome.NotConfirmed));
            Assert.That(w.FileSystem.GetText(EeSaves + @"\Duel.ees"), Is.EqualTo("old"));

            ImportResult result = saves.Import(plan, true);
            Assert.That(result.ImportedCount, Is.EqualTo(1));
            Assert.That(w.FileSystem.GetText(EeSaves + @"\Duel.ees"), Is.EqualTo("new"));
            Assert.That(WinPath.GetFileName(result.BackupFolder), Does.EndWith("_import-saved-games"));
            Assert.That(w.FileSystem.GetText(result.BackupFolder + @"\EE\Saved Games\Duel.ees"), Is.EqualTo("old"));
        }

        /// <summary>A file that appeared after the plan is not replaced without confirmation (the plan is checked again).</summary>
        [Test]
        public void Import_ChecksThePlanAgain()
        {
            w.FileSystem.AddFile(Downloads + @"\Duel.ees", "new");
            SavedGames saves = Create();
            ImportPlan plan = saves.PlanImport(Installation(), Game.EmpireEarth, new[] { Downloads + @"\Duel.ees" });
            w.FileSystem.AddFile(EeSaves + @"\Duel.ees", "saved meanwhile");

            ImportResult result = saves.Import(plan, false);

            Assert.That(result.Files.Single().Item2, Is.EqualTo(ImportFileOutcome.NotConfirmed));
            Assert.That(w.FileSystem.GetText(EeSaves + @"\Duel.ees"), Is.EqualTo("saved meanwhile"));
        }

        /// <summary>ADR 0016: the existing VirtualStore copy is the file the game reads, so the import replaces that one.</summary>
        [Test]
        public void Import_ReplacesTheVirtualStoreCopyTheGameUses()
        {
            w.FileSystem.AddFile(EeSaves + @"\Duel.ees", "original");
            w.FileSystem.AddFile(EeSavesCopy + @"\Duel.ees", "copy");
            w.FileSystem.AddFile(Downloads + @"\Duel.ees", "new");
            SavedGames saves = Create();

            ImportResult result = saves.Import(saves.PlanImport(Installation(), Game.EmpireEarth, new[] { Downloads + @"\Duel.ees" }), true);

            Assert.That(result.Files.Single().Item3, Is.EqualTo(EeSavesCopy + @"\Duel.ees"));
            Assert.That(w.FileSystem.GetText(EeSavesCopy + @"\Duel.ees"), Is.EqualTo("new"));
            Assert.That(w.FileSystem.GetText(EeSaves + @"\Duel.ees"), Is.EqualTo("original"));
        }

        /// <summary>ADR 0016: a game folder the player may not write (a foreign installation below Program Files) gets the file in its VirtualStore folder.</summary>
        [Test]
        public void Import_IntoAGameFolderThatRefuses_GoesToTheVirtualStore()
        {
            w.FileSystem.AddFile(Downloads + @"\Duel.ees", "new");
            w.FileSystem.FailOn(Root + @"\Empire Earth\Data", FileSystemOperation.Write, FileSystemStatus.AccessDenied);
            SavedGames saves = Create();

            ImportResult result = saves.Import(saves.PlanImport(Installation(), Game.EmpireEarth, new[] { Downloads + @"\Duel.ees" }), false);

            Assert.That(result.Files.Single().Item2, Is.EqualTo(ImportFileOutcome.Imported));
            Assert.That(w.FileSystem.GetText(EeSavesCopy + @"\Duel.ees"), Is.EqualTo("new"));
        }

        [Test]
        public void Import_WhereNothingCanBeWritten_ReportsAccessDenied()
        {
            w.World.AddForeignInstallation(@"D:\Games\Empire Earth");
            w.FileSystem.AddFile(Downloads + @"\Duel.ees", "new");
            w.FileSystem.FailOn(@"D:\Games", FileSystemOperation.Write, FileSystemStatus.AccessDenied);
            SavedGames saves = Create();
            Installation foreign = w.Discover(@"D:\Games\Empire Earth").Selected;

            ImportResult result = saves.Import(saves.PlanImport(foreign, Game.EmpireEarth, new[] { Downloads + @"\Duel.ees" }), false);

            Assert.That(result.Files.Single().Item2, Is.EqualTo(ImportFileOutcome.AccessDenied));
        }

        /// <summary>ADR 0016: the import is blocked by a setup and by a game; nothing is written.</summary>
        [Test]
        public void Import_IsBlockedBySetupAndGame(
            [Values("EE_Setup", "NeoEE_Setup", "StainlessSteelStudiosPresentsEmpireEarth", "MadDocSoftwarePresentsEmpireEarthExpansion")]
            string mutex)
        {
            w.FileSystem.AddFile(Downloads + @"\Duel.ees", "new");
            SavedGames saves = Create();
            ImportPlan plan = saves.PlanImport(Installation(), Game.EmpireEarth, new[] { Downloads + @"\Duel.ees" });
            w.Mutexes.With(mutex);

            ImportResult result = saves.Import(plan, true);

            Assert.That(result.IsBlocked, Is.True);
            Assert.That(result.Block.Block, Is.EqualTo(mutex.EndsWith("_Setup", StringComparison.Ordinal)
                ? MutationBlock.SetupRunning : MutationBlock.GameRunning));
            Assert.That(w.FileSystem.FileExists(EeSaves + @"\Duel.ees"), Is.False);
        }

        [Test]
        public void Import_WhenTheBackupOfAReplacedFileFails_ThatFileIsNotWritten()
        {
            w.FileSystem.AddFile(EeSaves + @"\Duel.ees", "old");
            w.FileSystem.AddFile(Downloads + @"\Duel.ees", "new");
            w.FileSystem.AddFile(Downloads + @"\Other.ees", "other");
            w.FileSystem.FailOn(GameSettingsWorld.BackupsFolder, FileSystemOperation.CreateDirectory, FileSystemStatus.IoError);
            SavedGames saves = Create();

            ImportResult result = saves.Import(saves.PlanImport(Installation(), Game.EmpireEarth,
                new[] { Downloads + @"\Duel.ees", Downloads + @"\Other.ees" }), true);

            Assert.That(result.Files.Select(file => file.Item2), Is.EqualTo(new[] { ImportFileOutcome.Failed, ImportFileOutcome.Imported }));
            Assert.That(w.FileSystem.GetText(EeSaves + @"\Duel.ees"), Is.EqualTo("old"));
        }

        /// <summary>Forum test case 17 with real files: a multiplayer saved game with an umlaut in its name out and in again.</summary>
        [Test]
        public void ExportAndImport_RealFiles()
        {
            using (var directory = new TemporaryDirectory())
            {
                var real = new MappedFileSystem("T:", directory.Path);
                string ee = Path.Combine("Games", "Empire Earth");
                directory.CreateFile(Path.Combine(ee, "Empire Earth.exe"), "exe");
                directory.CreateFile(Path.Combine(ee, "Data", "Saved Games", "Mehrspieler Köln.ees"), "mp save");
                Directory.CreateDirectory(directory.Combine("Export"));
                Installation installation = new InstallationDiscovery(new InMemoryRegistry(), real, w.Logger)
                    .Discover(@"T:\Games\Empire Earth", null).Selected;
                var saves = new SavedGames(real, new EffectivePathResolver(real, @"T:\VirtualStore", new[] { @"T:\Program Files" }),
                    w.SystemInfo, w.Guard, new FileBackup(real, new BackupLocations(@"T:\Backups", real, w.World.Clock, w.Logger), w.Logger),
                    w.World.Clock, w.Logger);

                ExportResult exported = saves.Export(installation, @"T:\Export");
                Assert.That(exported.Outcome, Is.EqualTo(ExportOutcome.Done));
                string copy = exported.Folder + @"\EE\Saved Games\Mehrspieler Köln.ees";
                Assert.That(File.ReadAllText(real.ToHost(copy)), Is.EqualTo("mp save"));

                File.Delete(directory.Combine(Path.Combine(ee, "Data", "Saved Games", "Mehrspieler Köln.ees")));
                ImportResult imported = saves.Import(saves.PlanImport(installation, Game.EmpireEarth, new[] { copy }), false);

                Assert.That(imported.ImportedCount, Is.EqualTo(1));
                Assert.That(File.ReadAllText(directory.Combine(Path.Combine(ee, "Data", "Saved Games", "Mehrspieler Köln.ees"))),
                    Is.EqualTo("mp save"));
            }
        }
    }
}

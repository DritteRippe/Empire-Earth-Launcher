using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Empire_Earth_Launcher.Core.Backup;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Logging;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Core.Play;

namespace Empire_Earth_Launcher.Core.Maintenance
{
    /// <summary>A saved game or a scenario (R10).</summary>
    public enum SavedGameKind
    {
        /// <summary><c>.ees</c> in <c>Data\Saved Games</c> (forum t=9004 p=44629).</summary>
        SavedGame,
        /// <summary><c>.scn</c> in <c>Data\Scenarios</c>.</summary>
        Scenario
    }

    /// <summary>A saved game or scenario of a game folder (<see cref="SavedGames.List"/>).</summary>
    public sealed class SavedGameFile
    {
        internal SavedGameFile(Game game, SavedGameKind kind, string path, bool isVirtualStoreCopy, string shadowedPath, long length)
        {
            Game = game;
            Kind = kind;
            Path = path;
            IsVirtualStoreCopy = isVirtualStoreCopy;
            ShadowedPath = shadowedPath;
            Length = length;
        }

        public Game Game { get; }

        public SavedGameKind Kind { get; }

        /// <summary>The file the game uses (the VirtualStore copy wins a name conflict, ADR 0016).</summary>
        public string Path { get; }

        public string Name
        {
            get { return WinPath.GetFileName(Path); }
        }

        public bool IsVirtualStoreCopy { get; }

        /// <summary>The file of the same name in the game folder that the VirtualStore copy hides; null if there is none.</summary>
        public string ShadowedPath { get; }

        public long Length { get; }

        public override string ToString()
        {
            return Path + (ShadowedPath == null ? string.Empty : " (hides " + ShadowedPath + ")");
        }
    }

    /// <summary>Why a file of an export was left out.</summary>
    public enum ExportSkip
    {
        /// <summary>Larger than <see cref="SavedGames.MaxFileBytes"/>.</summary>
        TooLarge,
        /// <summary>It cannot be read.</summary>
        Unreadable,
        /// <summary>The copy could not be written.</summary>
        WriteFailed
    }

    /// <summary>How <see cref="SavedGames.Export"/> ended.</summary>
    public enum ExportOutcome
    {
        /// <summary>Every file was copied.</summary>
        Done,
        /// <summary>Some files were copied, others left out (<see cref="ExportResult.Skipped"/>).</summary>
        Partial,
        /// <summary>The game folders hold no saved game and no scenario.</summary>
        NothingToExport,
        /// <summary>The chosen folder is not a full path of an existing folder.</summary>
        InvalidTarget,
        /// <summary>The chosen folder is the install root, a game folder or inside one (the launcher writes no exports there).</summary>
        TargetInsideGameFolder,
        /// <summary>The export folder could not be created.</summary>
        Failed
    }

    /// <summary>The result of <see cref="SavedGames.Export"/>.</summary>
    public sealed class ExportResult
    {
        internal ExportResult(ExportOutcome outcome, string folder, IEnumerable<SavedGameFile> exported,
            IEnumerable<Tuple<SavedGameFile, ExportSkip>> skipped, string problem)
        {
            Outcome = outcome;
            Folder = folder;
            Exported = new ReadOnlyCollection<SavedGameFile>((exported ?? Enumerable.Empty<SavedGameFile>()).ToList());
            Skipped = new ReadOnlyCollection<Tuple<SavedGameFile, ExportSkip>>(
                (skipped ?? Enumerable.Empty<Tuple<SavedGameFile, ExportSkip>>()).ToList());
            Problem = problem;
        }

        public ExportOutcome Outcome { get; }

        /// <summary>The new folder with the copies; null if none was created.</summary>
        public string Folder { get; }

        public IReadOnlyList<SavedGameFile> Exported { get; }

        public IReadOnlyList<Tuple<SavedGameFile, ExportSkip>> Skipped { get; }

        /// <summary>The files of the game folder that a VirtualStore copy of the same name hid; they were not exported.</summary>
        public IReadOnlyList<string> Shadowed
        {
            get { return Exported.Where(file => file.ShadowedPath != null).Select(file => file.ShadowedPath).ToList(); }
        }

        public string Problem { get; }
    }

    /// <summary>The check of one file the player wants to import.</summary>
    public enum ImportCheck
    {
        /// <summary>It can be imported.</summary>
        Ok,
        /// <summary>It does not exist or is not a file.</summary>
        NotFound,
        /// <summary>Neither <c>.ees</c> nor <c>.scn</c>.</summary>
        WrongExtension,
        /// <summary>Not a plain file name (reserved name, invalid characters, trailing dot or space, too long).</summary>
        NotAPlainName,
        /// <summary>A name the game cannot open: characters outside the ANSI code page of Windows.</summary>
        NameOutsideAnsiCodePage,
        /// <summary>Larger than <see cref="SavedGames.MaxFileBytes"/>.</summary>
        TooLarge,
        /// <summary>The file is already where the game reads it.</summary>
        AlreadyInPlace,
        /// <summary>The target is a file the setup installed (never overwritten, contract 2.5).</summary>
        ManifestFile,
        /// <summary>Two files of the selection have the same name.</summary>
        DuplicateName,
        /// <summary>
        /// The manifest exists but cannot be used: which files the setup installed is unknown, so nothing is imported
        /// (contract 2.5; like the WON login reset).
        /// </summary>
        ManifestUnusable
    }

    /// <summary>One file of an <see cref="ImportPlan"/>.</summary>
    public sealed class ImportCandidate
    {
        internal ImportCandidate(string source, SavedGameKind? kind, ImportCheck check, string target, bool overwrites,
            bool nameOutsideAscii)
        {
            Source = source;
            Kind = kind;
            Check = check;
            Target = target;
            Overwrites = overwrites;
            NameOutsideAscii = nameOutsideAscii;
        }

        /// <summary>The file the player chose.</summary>
        public string Source { get; }

        public string Name
        {
            get { return WinPath.GetFileName(Source); }
        }

        /// <summary>Saved game or scenario; null for a wrong extension.</summary>
        public SavedGameKind? Kind { get; }

        public ImportCheck Check { get; }

        /// <summary>Where the game reads the file (the existing copy, else the game folder); null if the check failed.</summary>
        public string Target { get; }

        /// <summary>True if a file of that name exists where the game reads it: imported only after the player confirmed.</summary>
        public bool Overwrites { get; }

        /// <summary>
        /// True if the name has characters outside printable ASCII: other computers may show it differently, and in multiplayer
        /// both players need a file of exactly the same name (forum t=9004 p=44629, t=3563 p=23879).
        /// </summary>
        public bool NameOutsideAscii { get; }

        public override string ToString()
        {
            return Source + ": " + Check + (Overwrites ? " (overwrites " + Target + ")" : string.Empty);
        }
    }

    /// <summary>The checked files of an import into one game (<see cref="SavedGames.PlanImport"/>).</summary>
    public sealed class ImportPlan
    {
        internal ImportPlan(Installation installation, Game game, IEnumerable<ImportCandidate> files)
        {
            Installation = installation;
            Game = game;
            Files = new ReadOnlyCollection<ImportCandidate>(files.ToList());
        }

        public Installation Installation { get; }

        public Game Game { get; }

        public IReadOnlyList<ImportCandidate> Files { get; }

        /// <summary>The files that passed the checks.</summary>
        public IReadOnlyList<ImportCandidate> Importable
        {
            get { return Files.Where(file => file.Check == ImportCheck.Ok).ToList(); }
        }

        /// <summary>True if an importable file would overwrite one of the same name: the page asks first.</summary>
        public bool NeedsOverwriteConfirmation
        {
            get { return Importable.Any(file => file.Overwrites); }
        }
    }

    /// <summary>What happened to one file of an import.</summary>
    public enum ImportFileOutcome
    {
        /// <summary>Written where the game reads it.</summary>
        Imported,
        /// <summary>Not imported: it would overwrite a file and the player did not confirm.</summary>
        NotConfirmed,
        /// <summary>Not imported: it failed a check of the plan.</summary>
        Refused,
        /// <summary>Not imported: Windows denied writing there.</summary>
        AccessDenied,
        /// <summary>Not imported: reading or writing failed.</summary>
        Failed
    }

    /// <summary>The result of <see cref="SavedGames.Import"/>.</summary>
    public sealed class ImportResult
    {
        internal ImportResult(MutationCheck block, string backupFolder, IEnumerable<Tuple<ImportCandidate, ImportFileOutcome, string>> files)
        {
            Block = block;
            BackupFolder = backupFolder;
            Files = new ReadOnlyCollection<Tuple<ImportCandidate, ImportFileOutcome, string>>(
                (files ?? Enumerable.Empty<Tuple<ImportCandidate, ImportFileOutcome, string>>()).ToList());
        }

        /// <summary>Why the import was blocked (a setup or a game runs), else null; then nothing was written.</summary>
        public MutationCheck Block { get; }

        public bool IsBlocked
        {
            get { return Block != null; }
        }

        /// <summary>The backup of the overwritten files; null if none was overwritten.</summary>
        public string BackupFolder { get; }

        /// <summary>Every file: the candidate, what happened, and the path written (or the problem).</summary>
        public IReadOnlyList<Tuple<ImportCandidate, ImportFileOutcome, string>> Files { get; }

        public int ImportedCount
        {
            get { return Files.Count(file => file.Item2 == ImportFileOutcome.Imported); }
        }
    }

    /// <summary>
    /// Saved games and scenarios of the Tools page (R10, forum t=9004 p=44629, t=5747 p=38762, forum report section 8 row 16
    /// and test case 17): the export copies the <c>.ees</c> files of <c>Data\Saved Games</c> and the <c>.scn</c> files of
    /// <c>Data\Scenarios</c> of both games into a new folder the player chose, from the game folder and its VirtualStore copy
    /// (a VirtualStore copy wins a name conflict, the other file is listed, ADR 0016); the import checks single files and writes
    /// them where the game reads them, behind the mutation guard.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Import checks (ARCHITECTURE 10, "files from outside are untrusted"): only <c>.ees</c> and <c>.scn</c>, a plain file name
    /// (no path, no reserved name, no invalid character, no trailing dot or space, at most 200 characters), a name in the ANSI
    /// code page of Windows (the game is an ANSI program), at most <see cref="MaxFileBytes"/>, never onto a file of the manifest,
    /// and a file of the same name is replaced only after the player confirmed it, with a copy of the old file in
    /// <c>Backups\&lt;time&gt;_import-saved-games</c>. The name is kept exactly: in multiplayer both players need the same name
    /// (p=44629).
    /// </para>
    /// <para>
    /// Where the game reads: the existing VirtualStore copy of the name; else the game folder; if Windows denies writing there and
    /// the folder is virtualized, the VirtualStore folder (ADR 0016). Zip export and zip import are not part of v2 (dropped, see
    /// the CHANGELOG): the folder export covers the forum's way of sending a saved game.
    /// </para>
    /// </remarks>
    public sealed class SavedGames
    {
        /// <summary>The largest saved game or scenario that is exported or imported (64 MiB).</summary>
        public const long MaxFileBytes = 64L * 1024 * 1024;

        /// <summary>The longest file name that is imported.</summary>
        public const int MaxNameLength = 200;

        /// <summary>The name of the backup subfolder of overwritten files: <c>&lt;yyyy-MM-dd_HHmmss&gt;_import-saved-games</c>.</summary>
        public const string BackupWhat = "import-saved-games";

        /// <summary>The prefix of the folder an export creates in the chosen folder.</summary>
        public const string ExportFolderPrefix = "Empire Earth saves ";

        private readonly IFileSystem fileSystem;
        private readonly EffectivePathResolver effectivePaths;
        private readonly ISystemInfo systemInfo;
        private readonly MutationGuard guard;
        private readonly FileBackup backup;
        private readonly IClock clock;
        private readonly ILogger logger;
        private readonly long maxFileBytes;

        public SavedGames(IFileSystem fileSystem, EffectivePathResolver effectivePaths, ISystemInfo systemInfo, MutationGuard guard,
            FileBackup backup, IClock clock, ILogger logger)
            : this(fileSystem, effectivePaths, systemInfo, guard, backup, clock, logger, MaxFileBytes)
        {
        }

        /// <summary>With another size limit than <see cref="MaxFileBytes"/> (the tests, so they need no 64 MiB file).</summary>
        internal SavedGames(IFileSystem fileSystem, EffectivePathResolver effectivePaths, ISystemInfo systemInfo, MutationGuard guard,
            FileBackup backup, IClock clock, ILogger logger, long maxFileBytes)
        {
            if (maxFileBytes <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxFileBytes));
            this.maxFileBytes = maxFileBytes;
            this.fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            this.effectivePaths = effectivePaths ?? throw new ArgumentNullException(nameof(effectivePaths));
            this.systemInfo = systemInfo ?? throw new ArgumentNullException(nameof(systemInfo));
            this.guard = guard ?? throw new ArgumentNullException(nameof(guard));
            this.backup = backup ?? throw new ArgumentNullException(nameof(backup));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary><c>Data\Saved Games</c> or <c>Data\Scenarios</c>.</summary>
        public static string FolderOf(SavedGameKind kind)
        {
            return kind == SavedGameKind.SavedGame ? @"Data\Saved Games" : @"Data\Scenarios";
        }

        /// <summary><c>.ees</c> or <c>.scn</c>.</summary>
        public static string ExtensionOf(SavedGameKind kind)
        {
            return kind == SavedGameKind.SavedGame ? ".ees" : ".scn";
        }

        /// <summary>The kind of a file name by its extension (ignoring case); null for any other extension.</summary>
        public static SavedGameKind? KindOf(string fileName)
        {
            if (fileName == null)
                throw new ArgumentNullException(nameof(fileName));
            string extension = WinPath.GetExtension(fileName);
            foreach (SavedGameKind kind in new[] { SavedGameKind.SavedGame, SavedGameKind.Scenario })
            {
                if (string.Equals(extension, ExtensionOf(kind), StringComparison.OrdinalIgnoreCase))
                    return kind;
            }
            return null;
        }

        /// <summary>True if every character of <paramref name="name"/> is printable ASCII (space to tilde).</summary>
        public static bool IsPrintableAscii(string name)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));
            return name.All(character => character >= ' ' && character <= '~');
        }

        /// <summary>The saved games and scenarios of both games, the file the game uses for each name. Read-only.</summary>
        public IReadOnlyList<SavedGameFile> List(Installation installation)
        {
            if (installation == null)
                throw new ArgumentNullException(nameof(installation));
            var files = new List<SavedGameFile>();
            foreach (Game game in Game.All)
            {
                string gameFolder = installation.GetGameFolder(game);
                if (gameFolder == null || !WinPath.IsFullyQualified(gameFolder))
                    continue;
                foreach (SavedGameKind kind in new[] { SavedGameKind.SavedGame, SavedGameKind.Scenario })
                {
                    string folder = WinPath.Combine(gameFolder, FolderOf(kind));
                    string copy = effectivePaths.GetVirtualStorePath(folder);
                    var names = new SortedDictionary<string, Tuple<string, string>>(StringComparer.OrdinalIgnoreCase);
                    foreach (string path in FilesOf(folder, kind))
                        names[WinPath.GetFileName(path)] = Tuple.Create(path, (string)null);
                    if (copy != null)
                    {
                        foreach (string path in FilesOf(copy, kind))
                        {
                            string name = WinPath.GetFileName(path);
                            names[name] = Tuple.Create(path, names.TryGetValue(name, out var original) ? original.Item1 : null);
                        }
                    }
                    foreach (var entry in names.Values)
                    {
                        bool isCopy = copy != null && WinPath.IsBelow(entry.Item1, copy);
                        FileSystemResult<FileEntry> info = fileSystem.GetFileInfo(entry.Item1);
                        files.Add(new SavedGameFile(game, kind, entry.Item1, isCopy, entry.Item2, info.IsOk ? info.Value.Length : -1));
                    }
                }
            }
            return files;
        }

        /// <summary>
        /// Copies every saved game and scenario into a new folder <c>Empire Earth saves &lt;yyyy-MM-dd_HHmmss&gt;</c> of
        /// <paramref name="targetFolder"/> (subfolders <c>EE\Saved Games</c>, <c>AoC\Scenarios</c>, ...). Read-only for the
        /// game folders, so not guarded (ADR 0016).
        /// </summary>
        public ExportResult Export(Installation installation, string targetFolder)
        {
            if (installation == null)
                throw new ArgumentNullException(nameof(installation));
            if (string.IsNullOrWhiteSpace(targetFolder) || !WinPath.IsFullyQualified(targetFolder) || !fileSystem.DirectoryExists(targetFolder))
                return new ExportResult(ExportOutcome.InvalidTarget, null, null, null, targetFolder);
            if (IsInsideAGameFolder(installation, targetFolder))
            {
                logger.Warning("Saved games: the export into " + targetFolder + " was refused, it is a game folder or inside one.");
                return new ExportResult(ExportOutcome.TargetInsideGameFolder, null, null, null, targetFolder);
            }
            IReadOnlyList<SavedGameFile> files = List(installation);
            if (files.Count == 0)
                return new ExportResult(ExportOutcome.NothingToExport, null, null, null, null);

            string name = ExportFolderPrefix + BackupLocations.TimeStamp(clock.Now);
            string folder = WinPath.Combine(targetFolder, name);
            for (int suffix = 2; fileSystem.DirectoryExists(folder); suffix++)
                folder = WinPath.Combine(targetFolder, name + "_" + suffix.ToString(CultureInfo.InvariantCulture));
            FileSystemResult created = fileSystem.CreateDirectory(folder);
            if (!created.IsOk)
            {
                logger.Error("Saved games: the export folder " + folder + " could not be created: " + created + ".");
                return new ExportResult(ExportOutcome.Failed, null, null, null, created.ToString());
            }

            var exported = new List<SavedGameFile>();
            var skipped = new List<Tuple<SavedGameFile, ExportSkip>>();
            foreach (SavedGameFile file in files)
            {
                FileSystemResult<byte[]> content = fileSystem.ReadAllBytes(file.Path, maxFileBytes);
                if (!content.IsOk)
                {
                    skipped.Add(Tuple.Create(file, content.Status == FileSystemStatus.TooLarge ? ExportSkip.TooLarge : ExportSkip.Unreadable));
                    logger.Warning("Saved games: " + file.Path + " was not exported: " + content + ".");
                    continue;
                }
                string target = WinPath.Combine(folder, file.Game.Id + WinPath.Separator +
                                                        WinPath.GetFileName(FolderOf(file.Kind)) + WinPath.Separator + file.Name);
                FileSystemResult written = fileSystem.CreateDirectory(WinPath.GetParent(target));
                if (written.IsOk)
                    written = fileSystem.WriteAllBytesAtomically(target, content.Value);
                if (!written.IsOk)
                {
                    skipped.Add(Tuple.Create(file, ExportSkip.WriteFailed));
                    logger.Warning("Saved games: " + file.Path + " could not be copied to " + target + ": " + written + ".");
                    continue;
                }
                exported.Add(file);
            }
            foreach (SavedGameFile file in exported.Where(file => file.ShadowedPath != null))
                logger.Info("Saved games: " + file.ShadowedPath + " is hidden by the VirtualStore copy " + file.Path +
                            " (the game uses the copy) and was not exported.");
            logger.Info("Saved games: " + exported.Count.ToString(CultureInfo.InvariantCulture) + " file(s) exported to " + folder +
                        (skipped.Count == 0 ? "." : ", " + skipped.Count.ToString(CultureInfo.InvariantCulture) + " left out."));
            return new ExportResult(skipped.Count == 0 ? ExportOutcome.Done : ExportOutcome.Partial, folder, exported, skipped, null);
        }

        /// <summary>Checks the files the player chose for <paramref name="game"/>. Read-only.</summary>
        public ImportPlan PlanImport(Installation installation, Game game, IEnumerable<string> sourceFiles)
        {
            if (installation == null)
                throw new ArgumentNullException(nameof(installation));
            if (game == null)
                throw new ArgumentNullException(nameof(game));
            if (sourceFiles == null)
                throw new ArgumentNullException(nameof(sourceFiles));
            string gameFolder = installation.GetGameFolder(game);
            if (gameFolder == null)
                throw new ArgumentException("The installation has no folder of " + game.Id + ".", nameof(game));

            ManifestFiles manifest = ManifestFiles.Read(fileSystem, installation, guard);
            if (manifest.Status == ManifestFilesStatus.Unusable)
                logger.Warning("Import of saved games: nothing can be imported, the manifest is needed to keep the files of the setup: " +
                               manifest.Problem + ".");
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var candidates = new List<ImportCandidate>();
            foreach (string source in sourceFiles)
            {
                if (source == null)
                    throw new ArgumentException("The selection contains null.", nameof(sourceFiles));
                candidates.Add(Check(source, gameFolder, manifest, names));
            }
            return new ImportPlan(installation, game, candidates);
        }

        private ImportCandidate Check(string source, string gameFolder, ManifestFiles manifest, HashSet<string> names)
        {
            if (!WinPath.IsFullyQualified(source) || !fileSystem.FileExists(source))
                return Refused(source, null, ImportCheck.NotFound);
            string name = WinPath.GetFileName(source);
            SavedGameKind? kind = KindOf(name);
            if (kind == null)
                return Refused(source, null, ImportCheck.WrongExtension);
            if (name.Length > MaxNameLength || WinPath.CheckManifestPath(name) != ManifestPathError.None ||
                name.Length <= ExtensionOf(kind.Value).Length)
                return Refused(source, kind, ImportCheck.NotAPlainName);
            if (!systemInfo.IsInAnsiCodePage(name))
                return Refused(source, kind, ImportCheck.NameOutsideAnsiCodePage);
            FileSystemResult<FileEntry> info = fileSystem.GetFileInfo(source);
            if (!info.IsOk)
                return Refused(source, kind, ImportCheck.NotFound);
            if (info.Value.Length > maxFileBytes)
                return Refused(source, kind, ImportCheck.TooLarge);
            if (!names.Add(name))
                return Refused(source, kind, ImportCheck.DuplicateName);

            string gamePath = WinPath.Combine(gameFolder, FolderOf(kind.Value) + WinPath.Separator + name);
            if (manifest.Status == ManifestFilesStatus.Unusable)
                return Refused(source, kind, ImportCheck.ManifestUnusable);
            if (manifest.Contains(gamePath))
                return Refused(source, kind, ImportCheck.ManifestFile);
            EffectivePath effective = effectivePaths.Resolve(gamePath);
            if (WinPath.IsSamePath(source, effective.Path) || WinPath.IsSamePath(source, gamePath))
                return Refused(source, kind, ImportCheck.AlreadyInPlace);
            bool overwrites = effective.IsVirtualStoreCopy || fileSystem.FileExists(gamePath);
            return new ImportCandidate(source, kind, ImportCheck.Ok, effective.Path, overwrites, !IsPrintableAscii(name));
        }

        private static ImportCandidate Refused(string source, SavedGameKind? kind, ImportCheck check)
        {
            return new ImportCandidate(source, kind, check, null, false, false);
        }

        /// <summary>
        /// Imports the files of <paramref name="plan"/> that passed the checks, behind the mutation guard (ADR 0016); a file that
        /// would replace one of the same name only if <paramref name="overwriteConfirmed"/>, after a copy of the old file into
        /// the backup folder.
        /// </summary>
        public ImportResult Import(ImportPlan plan, bool overwriteConfirmed)
        {
            if (plan == null)
                throw new ArgumentNullException(nameof(plan));
            MutationCheck check = guard.Check("import saved games");
            if (!check.IsAllowed)
                return new ImportResult(check, null, null);

            // The situation may have changed since the plan: check every file again.
            ImportPlan now = PlanImport(plan.Installation, plan.Game, plan.Files.Select(file => file.Source));
            var results = new List<Tuple<ImportCandidate, ImportFileOutcome, string>>();
            var toWrite = new List<ImportCandidate>();
            foreach (ImportCandidate file in now.Files)
            {
                if (file.Check != ImportCheck.Ok)
                    results.Add(Tuple.Create(file, ImportFileOutcome.Refused, file.Check.ToString()));
                else if (file.Overwrites && !overwriteConfirmed)
                    results.Add(Tuple.Create(file, ImportFileOutcome.NotConfirmed, file.Target));
                else
                    toWrite.Add(file);
            }

            string backupFolder = null;
            List<ImportCandidate> overwritten = toWrite.Where(file => file.Overwrites).ToList();
            if (overwritten.Count > 0)
            {
                FileBackupResult copies = backup.CopyIntoBackup(BackupWhat, overwritten.Select(file => new FileToBackUp(file.Target,
                    plan.Game.Id + WinPath.Separator + WinPath.GetFileName(FolderOf(file.Kind.Value)) + WinPath.Separator + file.Name)).ToList());
                if (copies.Outcome != FileBackupOutcome.Done)
                {
                    // Backup or nothing: the files that would replace others are not written.
                    foreach (ImportCandidate file in overwritten)
                        results.Add(Tuple.Create(file, ImportFileOutcome.Failed, "backup failed: " + copies.Problem));
                    toWrite = toWrite.Where(file => !file.Overwrites).ToList();
                }
                else
                    backupFolder = copies.Folder;
            }

            foreach (ImportCandidate file in toWrite)
                results.Add(Write(plan.Installation, plan.Game, file));
            logger.Info("Saved games: " + results.Count(result => result.Item2 == ImportFileOutcome.Imported).ToString(CultureInfo.InvariantCulture) +
                        " of " + results.Count.ToString(CultureInfo.InvariantCulture) + " file(s) imported into " +
                        plan.Installation.GetGameFolder(plan.Game) + (backupFolder == null ? "." : ", replaced files in " + backupFolder + "."));
            return new ImportResult(null, backupFolder, results.OrderBy(result => now.Files.ToList().IndexOf(result.Item1)));
        }

        /// <summary>Writes one file where the game reads it; falls back to the VirtualStore when the game folder refuses.</summary>
        private Tuple<ImportCandidate, ImportFileOutcome, string> Write(Installation installation, Game game, ImportCandidate file)
        {
            FileSystemResult<byte[]> content = fileSystem.ReadAllBytes(file.Source, maxFileBytes);
            if (!content.IsOk)
            {
                logger.Warning("Saved games: " + file.Source + " cannot be read: " + content + ".");
                return Tuple.Create(file, ImportFileOutcome.Failed, content.ToString());
            }

            FileSystemResult written = WriteTo(file.Target, content.Value);
            string target = file.Target;
            if (!written.IsOk && written.Status == FileSystemStatus.AccessDenied)
            {
                string copy = effectivePaths.GetVirtualStorePath(file.Target);
                if (copy != null)
                {
                    logger.Info("Saved games: writing into " + WinPath.GetParent(file.Target) + " was denied; the file goes to its " +
                                "VirtualStore folder, where the game reads it (ADR 0016).");
                    written = WriteTo(copy, content.Value);
                    target = copy;
                }
            }
            if (!written.IsOk)
            {
                logger.Warning("Saved games: " + file.Source + " could not be written to " + target + ": " + written + ".");
                return Tuple.Create(file, written.Status == FileSystemStatus.AccessDenied ? ImportFileOutcome.AccessDenied
                    : ImportFileOutcome.Failed, written.ToString());
            }
            logger.Info("Saved games: imported " + file.Source + " to " + target + (file.Overwrites ? " (replaced)." : "."));
            return Tuple.Create(file, ImportFileOutcome.Imported, target);
        }

        private FileSystemResult WriteTo(string path, byte[] content)
        {
            FileSystemResult created = fileSystem.CreateDirectory(WinPath.GetParent(path));
            return created.IsOk ? fileSystem.WriteAllBytesAtomically(path, content) : created;
        }

        /// <summary>
        /// True for the install root (unless it is a drive root, e.g. <c>D:\</c> of <c>D:\Empire Earth</c>), a game folder, their
        /// VirtualStore copies and everything below them: the export never writes into the installation.
        /// </summary>
        private bool IsInsideAGameFolder(Installation installation, string folder)
        {
            if (WinPath.GetParent(installation.Root) != null && WinPath.IsSameOrBelow(folder, installation.Root))
                return true;
            foreach (Game game in Game.All)
            {
                string gameFolder = installation.GetGameFolder(game);
                if (gameFolder == null)
                    continue;
                if (WinPath.IsSameOrBelow(folder, gameFolder))
                    return true;
                string copy = effectivePaths.GetVirtualStorePath(gameFolder);
                if (copy != null && WinPath.IsSameOrBelow(folder, copy))
                    return true;
            }
            return false;
        }

        /// <summary>The files of one kind directly in <paramref name="folder"/>; none if it does not exist or cannot be listed.</summary>
        private IEnumerable<string> FilesOf(string folder, SavedGameKind kind)
        {
            if (!fileSystem.DirectoryExists(folder))
                return Enumerable.Empty<string>();
            FileSystemResult<IReadOnlyList<string>> files = fileSystem.GetFiles(folder);
            if (!files.IsOk)
            {
                logger.Warning("Saved games: " + folder + " cannot be listed: " + files + ".");
                return Enumerable.Empty<string>();
            }
            return files.Value.Where(path => KindOf(WinPath.GetFileName(path)) == kind);
        }
    }
}

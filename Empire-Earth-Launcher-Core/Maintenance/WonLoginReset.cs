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
    /// <summary>A WON login file of a game folder or of its VirtualStore copy (<see cref="WonLoginReset.Find"/>).</summary>
    public sealed class WonLoginFile
    {
        internal WonLoginFile(Game game, string path, bool isVirtualStoreCopy, bool isManifestFile)
        {
            Game = game;
            Path = path;
            IsVirtualStoreCopy = isVirtualStoreCopy;
            IsManifestFile = isManifestFile;
        }

        /// <summary>The game whose folder holds the file.</summary>
        public Game Game { get; }

        /// <summary>Full path of the file.</summary>
        public string Path { get; }

        /// <summary>True for the copy below <c>%LOCALAPPDATA%\VirtualStore</c> (ADR 0016).</summary>
        public bool IsVirtualStoreCopy { get; }

        /// <summary>True if the setup listed the file in its manifest: it is never moved (ADR 0007).</summary>
        public bool IsManifestFile { get; }

        public override string ToString()
        {
            return Path + (IsManifestFile ? " (manifest file, kept)" : string.Empty);
        }
    }

    /// <summary>The WON login files of an installation (<see cref="WonLoginReset.Find"/>).</summary>
    public sealed class WonLoginFiles
    {
        internal WonLoginFiles(Installation installation, ManifestFiles manifest, IEnumerable<WonLoginFile> files)
        {
            Installation = installation;
            Manifest = manifest;
            All = new ReadOnlyCollection<WonLoginFile>(files.ToList());
        }

        public Installation Installation { get; }

        /// <summary>The manifest the files were checked against.</summary>
        public ManifestFiles Manifest { get; }

        /// <summary>Every WON login file found, manifest files included.</summary>
        public IReadOnlyList<WonLoginFile> All { get; }

        /// <summary>The files the reset moves: every file found that is not a manifest file.</summary>
        public IReadOnlyList<WonLoginFile> ToMove
        {
            get { return All.Where(file => !file.IsManifestFile).ToList(); }
        }

        /// <summary>The files the reset keeps because the setup listed them.</summary>
        public IReadOnlyList<WonLoginFile> Kept
        {
            get { return All.Where(file => file.IsManifestFile).ToList(); }
        }
    }

    /// <summary>How <see cref="WonLoginReset.Reset"/> ended.</summary>
    public enum WonResetOutcome
    {
        /// <summary>Every file was moved into the backup folder.</summary>
        Done,
        /// <summary>Every file was copied, but Windows denied removing at least one (<see cref="WonResetResult.Files"/>).</summary>
        Partial,
        /// <summary>No WON login file was found (or only manifest files).</summary>
        NothingToReset,
        /// <summary>A setup or a game runs; nothing was changed.</summary>
        Blocked,
        /// <summary>The manifest exists but cannot be read: which files the setup installed is unknown, so nothing is moved.</summary>
        ManifestUnusable,
        /// <summary>The backup could not be written; nothing was moved.</summary>
        BackupFailed
    }

    /// <summary>The result of <see cref="WonLoginReset.Reset"/>.</summary>
    public sealed class WonResetResult
    {
        internal WonResetResult(WonResetOutcome outcome, MutationCheck block, string backupFolder, IEnumerable<MovedFile> files,
            IEnumerable<WonLoginFile> kept, string problem)
        {
            Outcome = outcome;
            Block = block;
            BackupFolder = backupFolder;
            Files = new ReadOnlyCollection<MovedFile>((files ?? Enumerable.Empty<MovedFile>()).ToList());
            Kept = new ReadOnlyCollection<WonLoginFile>((kept ?? Enumerable.Empty<WonLoginFile>()).ToList());
            Problem = problem;
        }

        public WonResetOutcome Outcome { get; }

        /// <summary>Why the reset was blocked, else null.</summary>
        public MutationCheck Block { get; }

        /// <summary>The backup folder (it holds login data); null if none was written.</summary>
        public string BackupFolder { get; }

        /// <summary>Every file the reset tried to move, with what happened.</summary>
        public IReadOnlyList<MovedFile> Files { get; }

        /// <summary>The manifest files that were kept.</summary>
        public IReadOnlyList<WonLoginFile> Kept { get; }

        /// <summary>For the log; null if nothing failed.</summary>
        public string Problem { get; }

        public override string ToString()
        {
            return Outcome + (Block == null ? string.Empty : " " + Block) + ", " +
                   Files.Count(file => file.Outcome == FileMoveOutcome.Moved).ToString(CultureInfo.InvariantCulture) + " moved" +
                   (BackupFolder == null ? string.Empty : ", backup " + BackupFolder) + (Problem == null ? string.Empty : ": " + Problem);
        }
    }

    /// <summary>
    /// "Reset WON login" of the Tools page (R6, forum t=43377 p=83519: "Do you have a file _wonkver.pub or _wonlogin.ks in your
    /// EE directory? Try to delete those"): moves <c>_wonkver.pub</c> and <c>_wonlogin.ks</c> of the EE and AoC folders and of
    /// their VirtualStore copies (ADR 0016) into a dated backup folder, behind the mutation guard.
    /// </summary>
    /// <remarks>
    /// The files are moved, not deleted (ADR 0007); a file the setup listed in its manifest is never moved (the setup installs
    /// <c>_wonkver.pub</c> with <c>deleteafterinstall</c>, contract 2.3, so it never is; the rule protects against a change of
    /// that). If the manifest exists but cannot be read, nothing is moved. The backup folder holds login data: the page says
    /// so, and the diagnostics report never includes it. The contents of the files are never read for anything but the copy
    /// and never logged.
    /// </remarks>
    public sealed class WonLoginReset
    {
        /// <summary>The name of the backup subfolder: <c>&lt;yyyy-MM-dd_HHmmss&gt;_won-login-reset</c>.</summary>
        public const string BackupWhat = "won-login-reset";

        /// <summary>The WON login files of forum p=83519.</summary>
        public static readonly IReadOnlyList<string> FileNames = new ReadOnlyCollection<string>(new[] { "_wonkver.pub", "_wonlogin.ks" });

        private readonly IFileSystem fileSystem;
        private readonly EffectivePathResolver effectivePaths;
        private readonly MutationGuard guard;
        private readonly FileBackup backup;
        private readonly ILogger logger;

        public WonLoginReset(IFileSystem fileSystem, EffectivePathResolver effectivePaths, MutationGuard guard, FileBackup backup,
            ILogger logger)
        {
            this.fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            this.effectivePaths = effectivePaths ?? throw new ArgumentNullException(nameof(effectivePaths));
            this.guard = guard ?? throw new ArgumentNullException(nameof(guard));
            this.backup = backup ?? throw new ArgumentNullException(nameof(backup));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>The WON login files of the EE and AoC folders and their VirtualStore copies. Read-only.</summary>
        public WonLoginFiles Find(Installation installation)
        {
            if (installation == null)
                throw new ArgumentNullException(nameof(installation));
            ManifestFiles manifest = ManifestFiles.Read(fileSystem, installation);
            var files = new List<WonLoginFile>();
            foreach (Game game in Game.All)
            {
                string folder = installation.GetGameFolder(game);
                if (folder == null || !WinPath.IsFullyQualified(folder))
                    continue;
                foreach (string name in FileNames)
                {
                    EffectivePath effective = effectivePaths.Resolve(WinPath.Combine(folder, name));
                    bool listed = manifest.Contains(effective.GamePath);
                    if (fileSystem.FileExists(effective.GamePath))
                        files.Add(new WonLoginFile(game, effective.GamePath, false, listed));
                    if (effective.IsVirtualStoreCopy)
                        files.Add(new WonLoginFile(game, effective.VirtualStorePath, true, listed));
                }
            }
            return new WonLoginFiles(installation, manifest, files);
        }

        /// <summary>
        /// Moves the WON login files of <paramref name="installation"/> into <c>Backups\&lt;time&gt;_won-login-reset\</c>
        /// (subfolders <c>EE</c>, <c>EE-VirtualStore</c>, <c>AoC</c>, <c>AoC-VirtualStore</c>), after the mutation guard.
        /// </summary>
        public WonResetResult Reset(Installation installation)
        {
            if (installation == null)
                throw new ArgumentNullException(nameof(installation));
            MutationCheck check = guard.Check("reset the WON login");
            if (!check.IsAllowed)
                return new WonResetResult(WonResetOutcome.Blocked, check, null, null, null, null);

            WonLoginFiles found = Find(installation);
            if (found.Manifest.Status == ManifestFilesStatus.Unusable)
            {
                logger.Warning("WON login reset: nothing was moved, the manifest is needed to keep the files of the setup: " +
                               found.Manifest.Problem + ".");
                return new WonResetResult(WonResetOutcome.ManifestUnusable, null, null, null, null, found.Manifest.Problem);
            }
            foreach (WonLoginFile kept in found.Kept)
                logger.Info("WON login reset: " + kept.Path + " is listed in the manifest of the setup and is kept.");
            if (found.ToMove.Count == 0)
            {
                logger.Info("WON login reset: no WON login file in " + installation.EeFolder +
                            (installation.AocFolder == null ? string.Empty : " or " + installation.AocFolder) + ".");
                return new WonResetResult(WonResetOutcome.NothingToReset, null, null, null, found.Kept, null);
            }

            List<FileToBackUp> files = found.ToMove.Select(file => new FileToBackUp(file.Path,
                file.Game.Id + (file.IsVirtualStoreCopy ? "-VirtualStore" : string.Empty) + WinPath.Separator +
                WinPath.GetFileName(file.Path))).ToList();
            FileBackupResult moved = backup.MoveIntoBackup(BackupWhat, files);
            WonResetOutcome outcome = moved.Outcome == FileBackupOutcome.Done ? WonResetOutcome.Done
                : moved.Outcome == FileBackupOutcome.Partial ? WonResetOutcome.Partial : WonResetOutcome.BackupFailed;
            logger.Info("WON login reset of " + installation.Root + ": " + outcome + ", " +
                        moved.Files.Count(file => file.Outcome == FileMoveOutcome.Moved).ToString(CultureInfo.InvariantCulture) +
                        " of " + files.Count.ToString(CultureInfo.InvariantCulture) + " file(s) moved" +
                        (moved.Folder == null ? "." : " into " + moved.Folder + " (it contains login data)."));
            return new WonResetResult(outcome, null, moved.Folder, moved.Files, found.Kept, moved.Problem);
        }
    }
}

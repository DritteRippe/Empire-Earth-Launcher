using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using Empire_Earth_Launcher.Core.Logging;
using Empire_Earth_Launcher.Core.Platform;

namespace Empire_Earth_Launcher.Core.Backup
{
    /// <summary>A file to move into a backup folder, and where it goes below that folder.</summary>
    public sealed class FileToBackUp
    {
        /// <param name="source">Full path of the file.</param>
        /// <param name="relativeTarget">Path below the backup folder, plain names separated by <c>\</c> (e.g. <c>EE\_wonlogin.ks</c>).</param>
        public FileToBackUp(string source, string relativeTarget)
        {
            if (source == null || !WinPath.IsFullyQualified(source))
                throw new ArgumentException("A full path of the file is required: " + source, nameof(source));
            if (relativeTarget == null || WinPath.CheckManifestPath(relativeTarget.Replace('\\', '/')) != ManifestPathError.None)
                throw new ArgumentException("The target is a relative path of plain names: " + relativeTarget, nameof(relativeTarget));
            Source = WinPath.Normalize(source);
            RelativeTarget = relativeTarget;
        }

        public string Source { get; }

        public string RelativeTarget { get; }
    }

    /// <summary>What happened to one file of <see cref="FileBackup.MoveIntoBackup"/>.</summary>
    public enum FileMoveOutcome
    {
        /// <summary>Copied into the backup, read back, and removed from its place.</summary>
        Moved,
        /// <summary>Copied into the backup, but Windows denied removing it (rights, a running program): it is still in place.</summary>
        RemoveDenied,
        /// <summary>Copied into the backup, but removing it failed for another reason: it is still in place.</summary>
        RemoveFailed,
        /// <summary>Not touched: the backup failed, so no file was removed.</summary>
        NotMoved
    }

    /// <summary>One file of a <see cref="FileBackupResult"/>.</summary>
    public sealed class MovedFile
    {
        internal MovedFile(string source, string backupPath, FileMoveOutcome outcome, string detail)
        {
            Source = source;
            BackupPath = backupPath;
            Outcome = outcome;
            Detail = detail;
        }

        /// <summary>Where the file was.</summary>
        public string Source { get; }

        /// <summary>Its copy in the backup folder; null if it was not copied.</summary>
        public string BackupPath { get; }

        public FileMoveOutcome Outcome { get; }

        /// <summary>The error, for the log; null if none.</summary>
        public string Detail { get; }

        public override string ToString()
        {
            return Source + ": " + Outcome + (Detail == null ? string.Empty : " (" + Detail + ")");
        }
    }

    /// <summary>How <see cref="FileBackup.MoveIntoBackup"/> ended.</summary>
    public enum FileBackupOutcome
    {
        /// <summary>Every file was copied and removed.</summary>
        Done,
        /// <summary>Every file was copied, but at least one could not be removed (<see cref="FileMoveOutcome.RemoveDenied"/>).</summary>
        Partial,
        /// <summary>The backup could not be written completely: nothing was removed.</summary>
        BackupFailed
    }

    /// <summary>The result of <see cref="FileBackup.MoveIntoBackup"/>.</summary>
    public sealed class FileBackupResult
    {
        internal FileBackupResult(FileBackupOutcome outcome, string folder, IEnumerable<MovedFile> files, string problem)
        {
            Outcome = outcome;
            Folder = folder;
            Files = new ReadOnlyCollection<MovedFile>(files.ToList());
            Problem = problem;
        }

        public FileBackupOutcome Outcome { get; }

        /// <summary>The backup folder; null if it could not be created.</summary>
        public string Folder { get; }

        /// <summary>Every file, in the order given.</summary>
        public IReadOnlyList<MovedFile> Files { get; }

        /// <summary>Why the backup failed, for the log; null otherwise.</summary>
        public string Problem { get; }
    }

    /// <summary>
    /// Moves files into a dated backup folder (ADR 0007: "the files are moved, not deleted"): every file is first copied
    /// into <c>Backups\&lt;yyyy-MM-dd_HHmmss&gt;_&lt;what&gt;\</c> and read back, with a list of the original paths
    /// (<see cref="IndexFileName"/>); only when every copy is on the disk are the originals removed. If a copy fails, no
    /// file is removed. Copying instead of renaming also works across drives and keeps the original where Windows does not
    /// let the launcher remove it.
    /// </summary>
    /// <remarks>The contents of the files are never logged (the WON login files hold login data, ARCHITECTURE 7).</remarks>
    public sealed class FileBackup
    {
        /// <summary>The list of the original paths in every backup folder of this kind (UTF-8 with BOM, CRLF).</summary>
        public const string IndexFileName = "moved-files.txt";

        /// <summary>The largest file that is moved (16 MiB); the WON files are a few hundred bytes.</summary>
        public const long MaxFileBytes = 16 * 1024 * 1024;

        private readonly IFileSystem fileSystem;
        private readonly BackupLocations backups;
        private readonly ILogger logger;

        public FileBackup(IFileSystem fileSystem, BackupLocations backups, ILogger logger)
        {
            this.fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            this.backups = backups ?? throw new ArgumentNullException(nameof(backups));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>Copies <paramref name="files"/> into a new backup folder for <paramref name="what"/>, then removes them.</summary>
        /// <param name="what">The action, e.g. <c>won-login-reset</c> (<see cref="BackupLocations.SubfolderName"/>).</param>
        /// <param name="files">At least one file; the targets must differ.</param>
        public FileBackupResult MoveIntoBackup(string what, IReadOnlyList<FileToBackUp> files)
        {
            if (files == null || files.Count == 0 || files.Any(file => file == null))
                throw new ArgumentException("At least one file is required.", nameof(files));
            if (files.Select(file => file.RelativeTarget).Distinct(StringComparer.OrdinalIgnoreCase).Count() != files.Count)
                throw new ArgumentException("Two files would have the same target.", nameof(files));

            FileSystemResult<BackupFolder> folder = backups.CreateSubfolder(what);
            if (!folder.IsOk)
                return Failed(files, null, "the backup folder could not be created: " + folder);

            var copies = new List<string>();
            foreach (FileToBackUp file in files)
            {
                string target = WinPath.Combine(folder.Value.Path, file.RelativeTarget);
                string problem = Copy(file.Source, target);
                if (problem != null)
                    return Failed(files, folder.Value.Path, problem);
                copies.Add(target);
            }
            string index = WinPath.Combine(folder.Value.Path, IndexFileName);
            string indexProblem = WriteChecked(index, IndexText(files));
            if (indexProblem != null)
                return Failed(files, folder.Value.Path, indexProblem);

            var moved = new List<MovedFile>();
            for (int i = 0; i < files.Count; i++)
            {
                FileSystemResult removed = fileSystem.DeleteFile(files[i].Source);
                FileMoveOutcome outcome = removed.IsOk || removed.Status == FileSystemStatus.NotFound ? FileMoveOutcome.Moved
                    : removed.Status == FileSystemStatus.AccessDenied ? FileMoveOutcome.RemoveDenied : FileMoveOutcome.RemoveFailed;
                if (outcome == FileMoveOutcome.Moved)
                    logger.Info("Backup: moved " + files[i].Source + " to " + copies[i] + ".");
                else
                    logger.Warning("Backup: " + files[i].Source + " was copied to " + copies[i] + " but could not be removed: " +
                                   removed + ".");
                moved.Add(new MovedFile(files[i].Source, copies[i], outcome, removed.IsOk ? null : removed.ToString()));
            }
            FileBackupOutcome result = moved.All(file => file.Outcome == FileMoveOutcome.Moved)
                ? FileBackupOutcome.Done
                : FileBackupOutcome.Partial;
            return new FileBackupResult(result, folder.Value.Path, moved, null);
        }

        /// <summary>Copies one file and reads the copy back; the problem, or null.</summary>
        private string Copy(string source, string target)
        {
            FileSystemResult<byte[]> content = fileSystem.ReadAllBytes(source, MaxFileBytes);
            if (!content.IsOk)
                return source + " could not be read: " + content;
            FileSystemResult created = fileSystem.CreateDirectory(WinPath.GetParent(target));
            if (!created.IsOk)
                return "the folder of " + target + " could not be created: " + created;
            return WriteChecked(target, content.Value);
        }

        /// <summary>Writes a file through a temporary file and reads it back; the problem, or null.</summary>
        private string WriteChecked(string path, byte[] data)
        {
            FileSystemResult written = fileSystem.WriteAllBytesAtomically(path, data);
            if (!written.IsOk)
                return path + " could not be written: " + written;
            FileSystemResult<byte[]> readBack = fileSystem.ReadAllBytes(path, data.Length + 1L);
            if (!readBack.IsOk || !readBack.Value.SequenceEqual(data))
                return path + " cannot be confirmed: " + (readBack.IsOk ? "its content differs" : readBack.ToString());
            return null;
        }

        /// <summary>The index: one line per file, the path in the backup folder and where it came from.</summary>
        private static byte[] IndexText(IEnumerable<FileToBackUp> files)
        {
            var text = new StringBuilder();
            foreach (FileToBackUp file in files)
                text.Append(file.RelativeTarget).Append(" <- ").Append(file.Source).Append("\r\n");
            byte[] bom = Encoding.UTF8.GetPreamble();
            return bom.Concat(new UTF8Encoding(false).GetBytes(text.ToString())).ToArray();
        }

        private FileBackupResult Failed(IReadOnlyList<FileToBackUp> files, string folder, string problem)
        {
            logger.Error("Backup: nothing was moved because the backup failed: " + problem + ".");
            return new FileBackupResult(FileBackupOutcome.BackupFailed, folder, NotMoved(files, problem), problem);
        }

        private static IEnumerable<MovedFile> NotMoved(IEnumerable<FileToBackUp> files, string problem)
        {
            return files.Select(file => new MovedFile(file.Source, null, FileMoveOutcome.NotMoved, problem)).ToList();
        }
    }
}

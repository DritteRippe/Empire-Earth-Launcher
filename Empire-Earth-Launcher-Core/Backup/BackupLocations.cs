using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Logging;
using Empire_Earth_Launcher.Core.Platform;

namespace Empire_Earth_Launcher.Core.Backup
{
    /// <summary>
    /// The backup folder of the launcher, <c>%LOCALAPPDATA%\Empire Earth Launcher\Backups</c> (ARCHITECTURE 8, ADR 0007):
    /// one dated subfolder per action, <c>&lt;yyyy-MM-dd_HHmmss&gt;_&lt;what&gt;</c>, and <c>.reg</c> files named with date,
    /// time, product and game (contract 3.6). The launcher never deletes a backup.
    /// </summary>
    /// <remarks>
    /// Paths are Windows paths (<see cref="WinPath"/>); the folder is given by the composition root. A file is written as
    /// <c>.tmp</c>, then renamed, and read back: a backup counts only if its bytes are on the disk (ADR 0013, "changes
    /// are all-or-nothing").
    /// </remarks>
    public sealed class BackupLocations
    {
        /// <summary>Name of the backup folder below the launcher's per-user folder.</summary>
        public const string FolderName = "Backups";

        private static readonly Regex WhatPattern = new Regex("^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.CultureInvariant);

        private readonly IFileSystem fileSystem;
        private readonly IClock clock;
        private readonly ILogger logger;

        /// <param name="directory">Full path of the backup folder (<see cref="FolderName"/> below the user data folder).</param>
        public BackupLocations(string directory, IFileSystem fileSystem, IClock clock, ILogger logger)
        {
            if (string.IsNullOrWhiteSpace(directory) || !WinPath.IsFullyQualified(directory))
                throw new ArgumentException("A full path of the backup folder is required.", nameof(directory));
            Directory = WinPath.Normalize(directory);
            this.fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>Full path of the backup folder.</summary>
        public string Directory { get; }

        /// <summary><c>2026-10-02_153012</c>: the local time in the names of folders and files.</summary>
        public static string TimeStamp(DateTime time)
        {
            return time.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture);
        }

        /// <summary>Name of a backup subfolder: <c>&lt;yyyy-MM-dd_HHmmss&gt;_&lt;what&gt;</c>.</summary>
        /// <param name="what">The action, lower-case words joined by <c>-</c>, e.g. <c>reset-game-settings</c>.</param>
        public static string SubfolderName(DateTime time, string what)
        {
            if (what == null || !WhatPattern.IsMatch(what))
                throw new ArgumentException("The action is lower-case words joined by '-': " + what, nameof(what));
            return TimeStamp(time) + "_" + what;
        }

        /// <summary>
        /// Name of the <c>.reg</c> backup of the game settings of one game: <c>&lt;yyyy-MM-dd_HHmmss&gt;_&lt;product&gt;_&lt;game&gt;.reg</c>,
        /// e.g. <c>2026-10-02_153012_NeoEE_AoC.reg</c> (contract 3.6: date and time, product and game).
        /// </summary>
        public static string GameSettingsFileName(DateTime time, Product product, Game game)
        {
            if (product == null)
                throw new ArgumentNullException(nameof(product));
            if (game == null)
                throw new ArgumentNullException(nameof(game));
            return TimeStamp(time) + "_" + product.Id + "_" + game.Id + ".reg";
        }

        /// <summary>
        /// Creates a new subfolder for <paramref name="what"/> at the current time; if one of that name exists (two actions
        /// in one second), <c>_2</c>, <c>_3</c>, ... is appended.
        /// </summary>
        /// <returns>The full path of the folder and the time in its name, or the failure.</returns>
        public FileSystemResult<BackupFolder> CreateSubfolder(string what)
        {
            DateTime now = clock.Now;
            string name = SubfolderName(now, what);
            string path = WinPath.Combine(Directory, name);
            for (int suffix = 2; fileSystem.DirectoryExists(path); suffix++)
                path = WinPath.Combine(Directory, name + "_" + suffix.ToString(CultureInfo.InvariantCulture));

            FileSystemResult created = fileSystem.CreateDirectory(path);
            if (!created.IsOk)
            {
                logger.Error("Unable to create the backup folder " + path + ": " + created + ".");
                return FileSystemResult<BackupFolder>.Failure(created.Status, created.Detail);
            }
            return FileSystemResult<BackupFolder>.Success(new BackupFolder(path, now));
        }

        /// <summary>
        /// Writes a <c>.reg</c> file (<see cref="RegFileWriter"/>) through a temporary file and reads it back; the result
        /// is OK only if the file on the disk has exactly these bytes. Logged.
        /// </summary>
        public FileSystemResult WriteRegFile(string path, IEnumerable<RegFileKey> keys)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));
            byte[] bytes = RegFileWriter.ToBytes(keys);
            FileSystemResult written = fileSystem.WriteAllBytesAtomically(path, bytes);
            if (!written.IsOk)
            {
                logger.Error("Unable to write the backup " + path + ": " + written + ".");
                return written;
            }

            FileSystemResult<byte[]> readBack = fileSystem.ReadAllBytes(path, bytes.Length + 1);
            if (!readBack.IsOk || !readBack.Value.SequenceEqual(bytes))
            {
                string problem = readBack.IsOk ? "its content differs from what was written" : readBack.ToString();
                logger.Error("The backup " + path + " cannot be confirmed: " + problem + ".");
                return FileSystemResult.Failure(readBack.IsOk ? FileSystemStatus.IoError : readBack.Status, problem);
            }
            logger.Info("Backup written: " + path + " (" + bytes.Length.ToString(CultureInfo.InvariantCulture) + " bytes).");
            return FileSystemResult.Success;
        }
    }

    /// <summary>A new subfolder of the backup folder and the time in its name.</summary>
    public sealed class BackupFolder
    {
        public BackupFolder(string path, DateTime time)
        {
            Path = path ?? throw new ArgumentNullException(nameof(path));
            Time = time;
        }

        /// <summary>Full path of the folder.</summary>
        public string Path { get; }

        /// <summary>The local time in its name; the files in it use the same time.</summary>
        public DateTime Time { get; }
    }
}

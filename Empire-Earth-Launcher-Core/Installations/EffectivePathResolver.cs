using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Empire_Earth_Launcher.Core.Platform;

namespace Empire_Earth_Launcher.Core.Installations
{
    /// <summary>The file the game really uses for a path in a game folder (<see cref="EffectivePathResolver"/>).</summary>
    public sealed class EffectivePath
    {
        internal EffectivePath(string gamePath, string virtualStorePath, bool isVirtualStoreCopy)
        {
            GamePath = gamePath;
            VirtualStorePath = virtualStorePath;
            IsVirtualStoreCopy = isVirtualStoreCopy;
        }

        /// <summary>The path in the game folder, as asked for (normal form of <see cref="WinPath"/>).</summary>
        public string GamePath { get; }

        /// <summary>
        /// Where Windows keeps the per-user copy of <see cref="GamePath"/> for programs it virtualizes; null if the game
        /// folder is not virtualizable.
        /// </summary>
        public string VirtualStorePath { get; }

        /// <summary>True if the VirtualStore copy exists and is therefore the file the game uses.</summary>
        public bool IsVirtualStoreCopy { get; }

        /// <summary>The effective file: the VirtualStore copy if it exists, else the path in the game folder.</summary>
        public string Path
        {
            get { return IsVirtualStoreCopy ? VirtualStorePath : GamePath; }
        }

        public override string ToString()
        {
            return Path;
        }
    }

    /// <summary>
    /// The only source of game file paths for lobby profiles, saved games, scenarios and the WON login reset (ADR 0016):
    /// the game, a legacy program without a manifest, is virtualized by UAC, the launcher (<c>asInvoker</c>) is not. In a
    /// virtualizable game folder (below <c>Program Files</c>, <c>Program Files (x86)</c>, <c>ProgramData</c> or the
    /// Windows folder) the game reads and writes the copy below <c>%LOCALAPPDATA%\VirtualStore</c> when one exists, so
    /// the launcher must use that copy first. Read-only; it only checks whether files exist.
    /// </summary>
    /// <remarks>
    /// The VirtualStore copy of <c>C:\Program Files (x86)\Empire Earth\Empire Earth\_wonlobbypersistent.dat</c> is
    /// <c>%LOCALAPPDATA%\VirtualStore\Program Files (x86)\Empire Earth\Empire Earth\_wonlobbypersistent.dat</c> (the path
    /// without its drive). Folders outside the virtualized folders (<c>C:\Games\EE</c>, a user installation below
    /// <c>%LOCALAPPDATA%\Programs</c>) are never looked up in the VirtualStore (forum report section 8 row 2, forum 4.12).
    /// </remarks>
    public sealed class EffectivePathResolver
    {
        private readonly IFileSystem fileSystem;
        private readonly string virtualStoreDirectory;
        private readonly ReadOnlyCollection<string> virtualizedDirectories;

        /// <param name="fileSystem">The file system.</param>
        /// <param name="virtualStoreDirectory"><c>%LOCALAPPDATA%\VirtualStore</c> of the current account; null or empty if
        /// it is unknown (then nothing is virtualized).</param>
        /// <param name="virtualizedDirectories">The folders UAC virtualizes (Program Files, Program Files (x86),
        /// ProgramData, Windows); empty or relative entries are ignored.</param>
        public EffectivePathResolver(IFileSystem fileSystem, string virtualStoreDirectory,
            IEnumerable<string> virtualizedDirectories)
        {
            this.fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            if (virtualizedDirectories == null)
                throw new ArgumentNullException(nameof(virtualizedDirectories));
            this.virtualStoreDirectory = !string.IsNullOrWhiteSpace(virtualStoreDirectory) &&
                                         WinPath.IsFullyQualified(virtualStoreDirectory)
                ? WinPath.Normalize(virtualStoreDirectory)
                : null;
            this.virtualizedDirectories = new ReadOnlyCollection<string>(virtualizedDirectories
                .Where(directory => !string.IsNullOrWhiteSpace(directory) && WinPath.GetDrive(directory) != null &&
                                    WinPath.IsFullyQualified(directory))
                .Select(WinPath.Normalize)
                .Distinct(WinPath.Comparer)
                .ToList());
        }

        /// <summary>The folders UAC virtualizes, in normal form, without duplicates.</summary>
        public IReadOnlyList<string> VirtualizedDirectories
        {
            get { return virtualizedDirectories; }
        }

        /// <summary>
        /// True if <paramref name="path"/> is one of the virtualized folders or below one, and the VirtualStore folder is
        /// known.
        /// </summary>
        public bool IsVirtualizable(string path)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));
            return virtualStoreDirectory != null && WinPath.GetDrive(path) != null && WinPath.IsFullyQualified(path) &&
                   virtualizedDirectories.Any(directory => WinPath.IsSameOrBelow(path, directory));
        }

        /// <summary>The VirtualStore path of <paramref name="path"/>; null if it is not virtualizable.</summary>
        public string GetVirtualStorePath(string path)
        {
            if (!IsVirtualizable(path))
                return null;
            string normalized = WinPath.Normalize(path);
            // "C:\Program Files (x86)\..." -> "Program Files (x86)\..."; IsVirtualizable guarantees a drive root.
            return WinPath.Combine(virtualStoreDirectory, normalized.Substring(3));
        }

        /// <summary>
        /// The file the game uses for <paramref name="path"/> (a full path of a file in a game folder): the VirtualStore
        /// copy if the folder is virtualizable and the copy exists, else <paramref name="path"/> itself.
        /// </summary>
        public EffectivePath Resolve(string path)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));
            string gamePath = WinPath.IsFullyQualified(path) ? WinPath.Normalize(path) : path;
            string virtualStorePath = GetVirtualStorePath(gamePath);
            return new EffectivePath(gamePath, virtualStorePath,
                virtualStorePath != null && fileSystem.FileExists(virtualStorePath));
        }
    }
}

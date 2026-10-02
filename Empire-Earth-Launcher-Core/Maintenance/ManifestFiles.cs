using System;
using System.Collections.Generic;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Integrity;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Core.Play;

namespace Empire_Earth_Launcher.Core.Maintenance
{
    /// <summary>Whether the manifest of an installation could be read for the maintenance tools.</summary>
    public enum ManifestFilesStatus
    {
        /// <summary>The manifest was read; <see cref="ManifestFiles.Contains"/> knows its files.</summary>
        Read,
        /// <summary>There is no manifest (older, foreign or portable-only installations): no file is a manifest file.</summary>
        None,
        /// <summary>
        /// The manifest exists but cannot be read or is invalid, or a setup runs and the manifest is not read (contract 4.2):
        /// nothing is known, so writing tools must not act.
        /// </summary>
        Unusable
    }

    /// <summary>
    /// The files the setup listed in its manifest (contract 2.1 to 2.3), as full paths: the maintenance tools never move,
    /// overwrite or delete such a file (contract 2.5, ADR 0007 "files listed in the manifest are never moved"), and the
    /// VirtualStore scanner reports a shadowed one as serious (ADR 0016). Read-only.
    /// </summary>
    public sealed class ManifestFiles
    {
        private readonly HashSet<string> paths;

        private ManifestFiles(ManifestFilesStatus status, HashSet<string> paths, string problem)
        {
            Status = status;
            this.paths = paths;
            Problem = problem;
        }

        public ManifestFilesStatus Status { get; }

        /// <summary>For <see cref="ManifestFilesStatus.Unusable"/>: why, for the log.</summary>
        public string Problem { get; }

        /// <summary>The number of files listed; 0 without a usable manifest.</summary>
        public int Count
        {
            get { return paths.Count; }
        }

        /// <summary>
        /// Reads <c>&lt;root&gt;\_setupdata_&lt;Product&gt;\files.sha256</c> of <paramref name="installation"/>; while a setup runs
        /// (<paramref name="guard"/> finds its mutex) the file is not even opened (contract 4.2) and the result is
        /// <see cref="ManifestFilesStatus.Unusable"/>.
        /// </summary>
        public static ManifestFiles Read(IFileSystem fileSystem, Installation installation, MutationGuard guard)
        {
            if (fileSystem == null)
                throw new ArgumentNullException(nameof(fileSystem));
            if (installation == null)
                throw new ArgumentNullException(nameof(installation));
            if (guard == null)
                throw new ArgumentNullException(nameof(guard));
            var paths = new HashSet<string>(WinPath.Comparer);
            string manifest = WinPath.Combine(installation.Root,
                installation.Product.SetupDataFolderName + WinPath.Separator + ContractNames.ManifestFileName);
            Product setup = guard.FindRunningSetup();
            if (setup != null)
                return new ManifestFiles(ManifestFilesStatus.Unusable, paths, manifest + " is not read while the " + setup.Id +
                                                                              " setup is running (mutex " + setup.SetupMutexName + ")");
            if (!fileSystem.FileExists(manifest))
                return new ManifestFiles(ManifestFilesStatus.None, paths, null);
            FileSystemResult<byte[]> bytes = fileSystem.ReadAllBytes(manifest, ManifestReader.MaxFileBytes);
            if (!bytes.IsOk)
                return new ManifestFiles(ManifestFilesStatus.Unusable, paths, manifest + " cannot be read: " + bytes);
            ManifestParseResult parsed = ManifestReader.Parse(bytes.Value);
            if (!parsed.IsValid)
                return new ManifestFiles(ManifestFilesStatus.Unusable, paths, manifest + " is invalid: " + parsed);
            foreach (ManifestEntry entry in parsed.Entries)
            {
                if (WinPath.TryResolveManifestPath(installation.Root, entry.Path, out string fullPath) == ManifestPathError.None)
                    paths.Add(fullPath);
            }
            return new ManifestFiles(ManifestFilesStatus.Read, paths, null);
        }

        /// <summary>True if <paramref name="path"/> (a full path) is a file of the manifest.</summary>
        public bool Contains(string path)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));
            return WinPath.IsFullyQualified(path) && paths.Contains(WinPath.Normalize(path));
        }
    }
}

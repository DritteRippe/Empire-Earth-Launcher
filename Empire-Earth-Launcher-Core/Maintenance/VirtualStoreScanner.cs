using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Integrity;
using Empire_Earth_Launcher.Core.Logging;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Core.Play;

namespace Empire_Earth_Launcher.Core.Maintenance
{
    /// <summary>Why a VirtualStore copy matters (<see cref="VirtualStoreScanner"/>).</summary>
    public enum VirtualStoreReason
    {
        /// <summary>
        /// Serious: a file the setup installed (listed in its manifest) is shadowed; the game uses the copy, not the file the
        /// setup checked (forum 4.12: a different version with and without administrator rights).
        /// </summary>
        ManifestFile,

        /// <summary>
        /// Serious: a program file (class <c>code</c> of contract 2.4) the game loads from the VirtualStore, e.g. one an updater
        /// replaced without administrator rights.
        /// </summary>
        ProgramFile,

        /// <summary>Information: a file the game writes while it runs (lobby files, logs, saves, configuration).</summary>
        RuntimeFile
    }

    /// <summary>One file below the VirtualStore folder of a game folder.</summary>
    public sealed class VirtualStoreFinding
    {
        internal VirtualStoreFinding(Game game, string gamePath, string virtualStorePath, VirtualStoreReason reason,
            bool originalExists, bool differsFromOriginal = false)
        {
            Game = game;
            GamePath = gamePath;
            VirtualStorePath = virtualStorePath;
            Reason = reason;
            OriginalExists = originalExists;
            DiffersFromOriginal = differsFromOriginal;
        }

        public Game Game { get; }

        /// <summary>The path in the game folder the copy stands for.</summary>
        public string GamePath { get; }

        /// <summary>The copy below <c>%LOCALAPPDATA%\VirtualStore</c>.</summary>
        public string VirtualStorePath { get; }

        public VirtualStoreReason Reason { get; }

        /// <summary>True for a manifest or program file: the game does not use the file the setup installed.</summary>
        public bool IsSerious
        {
            get { return Reason != VirtualStoreReason.RuntimeFile; }
        }

        /// <summary>True if the file also exists in the game folder (the copy shadows it).</summary>
        public bool OriginalExists { get; }

        /// <summary>
        /// True if the copy is the <c>dgVoodoo.conf</c> of the game folder and its content differs from the file in the game
        /// folder: the game reads the copy, so a hand edit of the real file has no effect. Only that file is compared; false
        /// for every other file and for a file that could not be read.
        /// </summary>
        public bool DiffersFromOriginal { get; }

        public override string ToString()
        {
            return VirtualStorePath + " (" + Reason + (OriginalExists ? ", shadows " + GamePath : string.Empty) + ")";
        }
    }

    /// <summary>The result of <see cref="VirtualStoreScanner.Scan"/>.</summary>
    public sealed class VirtualStoreReport
    {
        internal VirtualStoreReport(Installation installation, IEnumerable<string> folders, IEnumerable<VirtualStoreFinding> findings,
            bool truncated, bool manifestUnusable)
        {
            Installation = installation;
            Folders = new ReadOnlyCollection<string>(folders.ToList());
            Findings = new ReadOnlyCollection<VirtualStoreFinding>(findings.ToList());
            Truncated = truncated;
            ManifestUnusable = manifestUnusable;
        }

        public Installation Installation { get; }

        /// <summary>
        /// The VirtualStore folders of the game folders that UAC virtualizes; empty if none is (a folder outside Program Files,
        /// ProgramData and Windows, or a user installation): then the game is never virtualized.
        /// </summary>
        public IReadOnlyList<string> Folders { get; }

        public bool IsVirtualizable
        {
            get { return Folders.Count > 0; }
        }

        /// <summary>The copies, serious ones first, each group by path.</summary>
        public IReadOnlyList<VirtualStoreFinding> Findings { get; }

        public bool HasSerious
        {
            get { return Findings.Any(finding => finding.IsSerious); }
        }

        /// <summary>
        /// The copies of <c>dgVoodoo.conf</c> that differ from the file of the game folder (A5): the game uses them instead of
        /// the file the player edits. The launcher only points to them, it never deletes a copy (contract 2.5).
        /// </summary>
        public IReadOnlyList<VirtualStoreFinding> ShadowingWrapperConfigs
        {
            get { return Findings.Where(finding => finding.DiffersFromOriginal).ToList(); }
        }

        /// <summary>True if there were more files than <see cref="VirtualStoreScanner.MaxFiles"/>; the rest is not listed.</summary>
        public bool Truncated { get; }

        /// <summary>True if the manifest exists but cannot be read: only program files are recognized as serious.</summary>
        public bool ManifestUnusable { get; }
    }

    /// <summary>
    /// The VirtualStore check of the Tools page (R8, ADR 0016, forum report section 8 row 2 and test case 1): for a game folder
    /// below <c>Program Files</c>, <c>Program Files (x86)</c>, <c>ProgramData</c> or the Windows folder, lists the files below
    /// its copy in <c>%LOCALAPPDATA%\VirtualStore</c>. A shadowed manifest file or a program file there is serious (the game uses
    /// the copy), every other file is information (the game writes it at run time). Read-only.
    /// </summary>
    /// <remarks>
    /// The registry VirtualStore copies of the SSSI, Mad Doc and Sierra keys are shown by the registry cleanup
    /// (<see cref="CleanupCandidates"/>). Only the game folders are scanned, never the VirtualStore of the root or of
    /// <c>Program Files</c> as a whole, so files of other programs are never listed.
    /// </remarks>
    public sealed class VirtualStoreScanner
    {
        /// <summary>At most this many files are listed (the game writes a few dozen).</summary>
        public const int MaxFiles = 2000;

        /// <summary>Folders deeper than this below a game folder are not entered.</summary>
        public const int MaxDepth = 16;

        /// <summary>The configuration of the dgVoodoo wrapper in a game folder; its VirtualStore copy is compared with it.</summary>
        public const string WrapperConfigFile = "dgVoodoo.conf";

        /// <summary>A wrapper configuration is a few kilobytes; a larger file is not compared and counts as different.</summary>
        private const long MaxCompareBytes = 1024 * 1024;

        private readonly IFileSystem fileSystem;
        private readonly EffectivePathResolver effectivePaths;
        private readonly MutationGuard guard;
        private readonly ILogger logger;

        /// <param name="fileSystem">Reads the game folders and their VirtualStore copies.</param>
        /// <param name="effectivePaths">The VirtualStore folders of the game folders (ADR 0016).</param>
        /// <param name="guard">Only asked whether a setup runs: the manifest is not read then (contract 4.2).</param>
        /// <param name="logger">Log of the launcher.</param>
        public VirtualStoreScanner(IFileSystem fileSystem, EffectivePathResolver effectivePaths, MutationGuard guard, ILogger logger)
        {
            this.fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            this.effectivePaths = effectivePaths ?? throw new ArgumentNullException(nameof(effectivePaths));
            this.guard = guard ?? throw new ArgumentNullException(nameof(guard));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public VirtualStoreReport Scan(Installation installation)
        {
            if (installation == null)
                throw new ArgumentNullException(nameof(installation));
            ManifestFiles manifest = ManifestFiles.Read(fileSystem, installation, guard);
            var folders = new List<string>();
            var findings = new List<VirtualStoreFinding>();
            bool truncated = false;
            foreach (Game game in Game.All)
            {
                string gameFolder = installation.GetGameFolder(game);
                if (gameFolder == null || !WinPath.IsFullyQualified(gameFolder))
                    continue;
                string copy = effectivePaths.GetVirtualStorePath(gameFolder);
                if (copy == null)
                    continue;
                folders.Add(copy);
                if (!fileSystem.DirectoryExists(copy))
                    continue;
                foreach (string file in Files(copy, 0, ref truncated, findings.Count))
                {
                    string relative = file.Substring(copy.Length).TrimStart(WinPath.Separator);
                    string gamePath = WinPath.Combine(gameFolder, relative);
                    VirtualStoreReason reason = manifest.Contains(gamePath) ? VirtualStoreReason.ManifestFile
                        : FileClassifier.Classify(gamePath) == FileClass.Code ? VirtualStoreReason.ProgramFile
                        : VirtualStoreReason.RuntimeFile;
                    bool originalExists = fileSystem.FileExists(gamePath);
                    bool differs = originalExists && WinPath.IsSamePath(gamePath, WinPath.Combine(gameFolder, WrapperConfigFile)) &&
                                   DiffersFromOriginal(file, gamePath);
                    findings.Add(new VirtualStoreFinding(game, gamePath, file, reason, originalExists, differs));
                    if (findings.Count >= MaxFiles)
                        break;
                }
            }

            List<VirtualStoreFinding> ordered = findings.OrderBy(finding => finding.IsSerious ? 0 : 1)
                                                         .ThenBy(finding => finding.VirtualStorePath, StringComparer.OrdinalIgnoreCase)
                                                         .ToList();
            var report = new VirtualStoreReport(installation, folders, ordered, truncated,
                manifest.Status == ManifestFilesStatus.Unusable);
            if (!report.IsVirtualizable)
                logger.Info("VirtualStore: the game folders of " + installation.Root + " are not virtualized by Windows.");
            else
            {
                logger.Info("VirtualStore: " + ordered.Count.ToString(CultureInfo.InvariantCulture) + " file(s) below " +
                            string.Join(" and ", folders) + ", " +
                            ordered.Count(finding => finding.IsSerious).ToString(CultureInfo.InvariantCulture) + " serious" +
                            (truncated ? " (list cut at " + MaxFiles.ToString(CultureInfo.InvariantCulture) + ")" : string.Empty) + ".");
                foreach (VirtualStoreFinding finding in ordered.Where(finding => finding.IsSerious))
                    logger.Warning("VirtualStore: the game uses " + finding + ".");
                foreach (VirtualStoreFinding finding in report.ShadowingWrapperConfigs)
                    logger.Warning("VirtualStore: " + finding.VirtualStorePath + " differs from " + finding.GamePath +
                                   "; the game reads the copy, so edits of the file in the game folder have no effect.");
            }
            return report;
        }

        /// <summary>
        /// True if the two files differ in length or content; a file that cannot be read is logged and counts as not different
        /// (the hint is shown only for a difference that was seen).
        /// </summary>
        private bool DiffersFromOriginal(string copy, string original)
        {
            FileSystemResult<FileEntry> copyInfo = fileSystem.GetFileInfo(copy);
            FileSystemResult<FileEntry> originalInfo = fileSystem.GetFileInfo(original);
            if (!copyInfo.IsOk || !originalInfo.IsOk)
            {
                logger.Warning("VirtualStore: " + copy + " cannot be compared with " + original + ": " +
                               (copyInfo.IsOk ? originalInfo : copyInfo) + ".");
                return false;
            }
            if (copyInfo.Value.Length != originalInfo.Value.Length)
                return true;
            if (copyInfo.Value.Length > MaxCompareBytes)
                return true;
            byte[] copyBytes = ReadAll(copy);
            byte[] originalBytes = ReadAll(original);
            return copyBytes != null && originalBytes != null && !copyBytes.SequenceEqual(originalBytes);
        }

        private byte[] ReadAll(string path)
        {
            FileSystemResult<Stream> opened = fileSystem.OpenRead(path);
            if (!opened.IsOk)
            {
                logger.Warning("VirtualStore: " + path + " cannot be read: " + opened + ".");
                return null;
            }
            try
            {
                using (Stream stream = opened.Value)
                using (var buffer = new MemoryStream())
                {
                    stream.CopyTo(buffer);
                    return buffer.ToArray();
                }
            }
            catch (IOException ex)
            {
                logger.Warning("VirtualStore: " + path + " cannot be read: " + ex.Message);
                return null;
            }
        }

        /// <summary>The files below <paramref name="folder"/>, depth first; folders that cannot be listed are logged and skipped.</summary>
        private IEnumerable<string> Files(string folder, int depth, ref bool truncated, int alreadyFound)
        {
            var result = new List<string>();
            var pending = new Stack<Tuple<string, int>>();
            pending.Push(Tuple.Create(folder, depth));
            while (pending.Count > 0)
            {
                Tuple<string, int> current = pending.Pop();
                FileSystemResult<IReadOnlyList<string>> files = fileSystem.GetFiles(current.Item1);
                if (!files.IsOk)
                {
                    logger.Warning("VirtualStore: " + current.Item1 + " cannot be listed: " + files + ".");
                    continue;
                }
                foreach (string file in files.Value)
                {
                    if (alreadyFound + result.Count >= MaxFiles)
                    {
                        truncated = true;
                        return result;
                    }
                    result.Add(file);
                }
                if (current.Item2 >= MaxDepth)
                    continue;
                FileSystemResult<IReadOnlyList<string>> folders = fileSystem.GetDirectories(current.Item1);
                if (!folders.IsOk)
                {
                    logger.Warning("VirtualStore: " + current.Item1 + " cannot be listed: " + folders + ".");
                    continue;
                }
                foreach (string subfolder in folders.Value.Reverse())
                    pending.Push(Tuple.Create(subfolder, current.Item2 + 1));
            }
            return result;
        }
    }
}

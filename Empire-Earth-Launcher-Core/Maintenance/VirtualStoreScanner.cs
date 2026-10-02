using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Integrity;
using Empire_Earth_Launcher.Core.Logging;
using Empire_Earth_Launcher.Core.Platform;

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
            bool originalExists)
        {
            Game = game;
            GamePath = gamePath;
            VirtualStorePath = virtualStorePath;
            Reason = reason;
            OriginalExists = originalExists;
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

        private readonly IFileSystem fileSystem;
        private readonly EffectivePathResolver effectivePaths;
        private readonly ILogger logger;

        public VirtualStoreScanner(IFileSystem fileSystem, EffectivePathResolver effectivePaths, ILogger logger)
        {
            this.fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            this.effectivePaths = effectivePaths ?? throw new ArgumentNullException(nameof(effectivePaths));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public VirtualStoreReport Scan(Installation installation)
        {
            if (installation == null)
                throw new ArgumentNullException(nameof(installation));
            ManifestFiles manifest = ManifestFiles.Read(fileSystem, installation);
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
                    findings.Add(new VirtualStoreFinding(game, gamePath, file, reason, fileSystem.FileExists(gamePath)));
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
            }
            return report;
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

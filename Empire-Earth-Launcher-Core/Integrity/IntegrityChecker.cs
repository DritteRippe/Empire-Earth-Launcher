using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Logging;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Core.Play;

namespace Empire_Earth_Launcher.Core.Integrity
{
    /// <summary>The progress of a check: files done of all files of the manifest.</summary>
    public sealed class IntegrityProgress
    {
        public IntegrityProgress(int checkedFiles, int totalFiles)
        {
            CheckedFiles = checkedFiles;
            TotalFiles = totalFiles;
        }

        public int CheckedFiles { get; }

        public int TotalFiles { get; }

        /// <summary>0 to 100.</summary>
        public int Percent
        {
            get { return TotalFiles == 0 ? 100 : (int)(100L * CheckedFiles / TotalFiles); }
        }
    }

    /// <summary>
    /// Checks an installation against its integrity manifest (contract 2): the quick check (every listed file exists, the
    /// <c>code</c> files are hashed) and the full check (also the <c>data</c> files are hashed), the states and the rules of
    /// contract 2.5, without ever disturbing a setup (contract 4.2, ADR 0016 plan review). Read-only: it never writes,
    /// deletes, moves, restores or downloads a file, and never asks for elevation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Cost is counted, not timed (ADR 0012): every file is opened at most once (<c>install.ini</c>, the manifest and each
    /// hashed file); a file that is not hashed is only checked for existence; <c>mutable</c> files are never hashed (a changed
    /// one is not reported); in the quick check no <c>data</c> file is opened.
    /// </para>
    /// <para>
    /// Setups: the check does not start while <c>EE_Setup</c> or <c>NeoEE_Setup</c> exists, and it probes them again before
    /// every file and after every MiB it reads; when one appears it closes the file and returns
    /// <see cref="IntegrityState.Cancelled"/> without findings. The setup watcher of the launcher also cancels it through the
    /// <see cref="CancellationToken"/>. Files are opened with sharing that lets a setup delete and rename them
    /// (<see cref="LocalFileSystem"/>).
    /// </para>
    /// <para>
    /// Errors are results (ADR 0013): a missing or unreadable <c>install.ini</c> or manifest gives Unknown with the reason, an
    /// unreadable game file is a finding of its own. Every finding is logged once with path, class, expected and actual hash.
    /// </para>
    /// </remarks>
    public sealed class IntegrityChecker
    {
        /// <summary>The size of the reads of a hashed file.</summary>
        internal const int BufferSize = 64 * 1024;

        /// <summary>The setup mutexes are probed again after this many bytes of a file.</summary>
        internal const long ProbeIntervalBytes = 1024 * 1024;

        private readonly IFileSystem fileSystem;
        private readonly IRegistry registry;
        private readonly IMutexProbe mutexProbe;
        private readonly ILogger logger;

        public IntegrityChecker(IFileSystem fileSystem, IRegistry registry, IMutexProbe mutexProbe, ILogger logger)
        {
            this.fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
            this.mutexProbe = mutexProbe ?? throw new ArgumentNullException(nameof(mutexProbe));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Runs <see cref="Check"/> on a thread of the pool (ADR 0004): hashing reads the game files, and neither the window
        /// nor a game start waits for it. A cancelled token gives a <see cref="IntegrityState.Cancelled"/> report, not a
        /// cancelled task.
        /// </summary>
        public Task<IntegrityReport> CheckAsync(Installation installation, IntegrityCheckKind kind,
            IProgress<IntegrityProgress> progress = null, CancellationToken cancellationToken = default)
        {
            if (installation == null)
                throw new ArgumentNullException(nameof(installation));
            return Task.Run(() => Check(installation, kind, progress, cancellationToken), CancellationToken.None);
        }

        /// <summary>Checks <paramref name="installation"/> and logs the result and every finding.</summary>
        /// <param name="installation">The installation found by the discovery.</param>
        /// <param name="kind">Quick or full check.</param>
        /// <param name="progress">Gets the files done after each file; null for none.</param>
        /// <param name="cancellationToken">Ends the check between two reads with <see cref="IntegrityState.Cancelled"/>.</param>
        public IntegrityReport Check(Installation installation, IntegrityCheckKind kind,
            IProgress<IntegrityProgress> progress = null, CancellationToken cancellationToken = default)
        {
            if (installation == null)
                throw new ArgumentNullException(nameof(installation));
            var run = new Run(this, installation, kind, progress, cancellationToken);
            IntegrityReport report = run.Execute();
            foreach (IntegrityFinding finding in report.Findings)
            {
                string line = "Integrity finding in " + installation.Root + ": " + finding + ".";
                if (finding.State == IntegrityState.Modified)
                    logger.Info(line);
                else
                    logger.Warning(line);
            }
            if (report.State == IntegrityState.Ok || report.State == IntegrityState.NotChecked ||
                report.State == IntegrityState.Cancelled || report.State == IntegrityState.Modified)
                logger.Info("Integrity: " + report + ".");
            else
                logger.Warning("Integrity: " + report + ".");
            return report;
        }

        /// <summary>The state of one check.</summary>
        private sealed class Run
        {
            private readonly IntegrityChecker owner;
            private readonly Installation installation;
            private readonly IntegrityCheckKind kind;
            private readonly IProgress<IntegrityProgress> progress;
            private readonly CancellationToken cancellationToken;
            private readonly List<IntegrityFinding> findings = new List<IntegrityFinding>();
            private int hashedFiles;

            public Run(IntegrityChecker owner, Installation installation, IntegrityCheckKind kind,
                IProgress<IntegrityProgress> progress, CancellationToken cancellationToken)
            {
                this.owner = owner;
                this.installation = installation;
                this.kind = kind;
                this.progress = progress;
                this.cancellationToken = cancellationToken;
            }

            private IFileSystem FileSystem
            {
                get { return owner.fileSystem; }
            }

            private ILogger Logger
            {
                get { return owner.logger; }
            }

            public IntegrityReport Execute()
            {
                if (installation.Kind == InstallationKind.Foreign || installation.State == InstallationState.FolderMissing)
                {
                    // Contract 2.5: kind foreign, no check and no message.
                    return IntegrityReport.NotChecked(installation, kind);
                }
                if (installation.Kind == InstallationKind.CommunityLegacy)
                    return Unknown(UnknownReason.LegacySetup, "it was installed by a community setup up to 1.7.2, which writes no manifest");

                // Contract 4.2: nothing of the setup data folder is read while a setup runs.
                CancelReason? stop = StopReason();
                if (stop != null)
                    return IntegrityReport.Cancelled(installation, kind, stop.Value);
                if (installation.HasNewerContract)
                    return Unknown(UnknownReason.NewerContract, "the setup implements contract version " +
                        installation.ContractVersion.ToString(CultureInfo.InvariantCulture) + ", the launcher knows " +
                        ContractNames.ContractVersion.ToString(CultureInfo.InvariantCulture));

                string dataFolder = WinPath.Combine(installation.Root, installation.Product.SetupDataFolderName);
                string installInfoPath = WinPath.Combine(dataFolder, ContractNames.InstallInfoFileName);
                if (!FileSystem.FileExists(installInfoPath))
                    return Unknown(UnknownReason.NoInstallInfo, installInfoPath + " does not exist");
                FileSystemResult<byte[]> installInfoBytes = FileSystem.ReadAllBytes(installInfoPath, InstallInfoFile.MaxFileBytes);
                if (!installInfoBytes.IsOk)
                    return Unknown(UnknownReason.NoInstallInfo, installInfoPath + " cannot be read: " + installInfoBytes);
                InstallInfoFile info = InstallInfoFile.Parse(installInfoBytes.Value);
                if (info.ContractVersion > ContractNames.ContractVersion)
                    return Unknown(UnknownReason.NewerContract, installInfoPath + " names contract version " +
                        info.ContractVersion.ToString(CultureInfo.InvariantCulture));
                if (info.ContractVersion < 1)
                    return Unknown(UnknownReason.NoInstallInfo, installInfoPath + " has no valid " + ContractNames.ContractVersionName);

                string manifestPath = WinPath.Combine(dataFolder, ContractNames.ManifestFileName);
                if (!FileSystem.FileExists(manifestPath))
                    return Unknown(UnknownReason.NoManifest, manifestPath + " does not exist");
                if (OlderSetupRanAfter(info))
                    return IntegrityReport.Unknown(installation, kind, UnknownReason.OlderSetupRanAfter);
                FileSystemResult<byte[]> manifestBytes = FileSystem.ReadAllBytes(manifestPath, ManifestReader.MaxFileBytes);
                if (!manifestBytes.IsOk)
                    return Unknown(UnknownReason.ManifestUnreadable, manifestPath + " cannot be read: " + manifestBytes);
                ManifestParseResult manifest = ManifestReader.Parse(manifestBytes.Value);
                if (!manifest.IsValid)
                    return Unknown(UnknownReason.InvalidManifest, manifestPath + " is invalid: " + manifest);
                foreach (string missing in info.MissingAfterInstall)
                {
                    ManifestPathError error = WinPath.CheckManifestPath(missing);
                    if (error != ManifestPathError.None)
                        return Unknown(UnknownReason.InvalidManifest, "[" + ContractNames.MissingAfterInstallSectionName + "] of " +
                            installInfoPath + " names the unsafe path \"" + missing + "\" (" + error + ")");
                }

                return CheckFiles(manifest.Entries, info.MissingAfterInstall);
            }

            private IntegrityReport CheckFiles(IReadOnlyList<ManifestEntry> entries, IReadOnlyList<string> missingAfterInstall)
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    CancelReason? stop = StopReason();
                    if (stop != null)
                        return Cancelled(stop.Value, i, entries.Count);

                    ManifestEntry entry = entries[i];
                    // The paths were checked by the reader; the root check is defence in depth (contract 2.2).
                    ManifestPathError error = WinPath.TryResolveManifestPath(installation.Root, entry.Path, out string fullPath);
                    if (error != ManifestPathError.None)
                        return Unknown(UnknownReason.InvalidManifest, "the path \"" + entry.Path + "\" leaves the install root (" + error + ")");

                    bool hash = entry.Class == FileClass.Code || (kind == IntegrityCheckKind.Full && entry.Class == FileClass.Data);
                    if (hash)
                    {
                        HashResult result = Hash(fullPath);
                        if (result.Stop != null)
                            return Cancelled(result.Stop.Value, i, entries.Count);
                        if (result.Status == FileSystemStatus.NotFound)
                            Add(entry, fullPath, FindingKind.Missing, null, null);
                        else if (result.Status != FileSystemStatus.Ok)
                            Add(entry, fullPath, FindingKind.Unreadable, null, result.Detail);
                        else
                        {
                            hashedFiles++;
                            if (!string.Equals(result.Hash, entry.Hash, StringComparison.Ordinal))
                                Add(entry, fullPath, FindingKind.HashDiffers, result.Hash, null);
                        }
                    }
                    else if (!FileSystem.FileExists(fullPath))
                    {
                        Add(entry, fullPath, FindingKind.Missing, null, null);
                    }
                    progress?.Report(new IntegrityProgress(i + 1, entries.Count));
                }

                foreach (string path in missingAfterInstall)
                {
                    WinPath.TryResolveManifestPath(installation.Root, path, out string fullPath);
                    findings.Add(new IntegrityFinding(path, fullPath, FileClassifier.Classify(path), FindingKind.MissingAfterInstall,
                        null, null, null));
                }
                return IntegrityReport.Finished(installation, kind, findings, entries.Count, hashedFiles);
            }

            private void Add(ManifestEntry entry, string fullPath, FindingKind findingKind, string actualHash, string detail)
            {
                findings.Add(new IntegrityFinding(entry.Path, fullPath, entry.Class, findingKind, entry.Hash, actualHash, detail));
            }

            private IntegrityReport Cancelled(CancelReason reason, int checkedFiles, int totalFiles)
            {
                Logger.Info("Integrity: the " + kind.ToString().ToLowerInvariant() + " check of " + installation.Root +
                            " was cancelled after " + checkedFiles.ToString(CultureInfo.InvariantCulture) + " of " +
                            totalFiles.ToString(CultureInfo.InvariantCulture) + " files (" +
                            (reason == CancelReason.SetupRunning ? "a setup started" : "cancelled") + "); its findings are dropped.");
                return IntegrityReport.Cancelled(installation, kind, reason);
            }

            private IntegrityReport Unknown(UnknownReason reason, string why)
            {
                Logger.Warning("Integrity: the state of " + installation.Root + " is unknown (" + reason + "): " + why + ".");
                return IntegrityReport.Unknown(installation, kind, reason);
            }

            /// <summary>
            /// Contract 2.5, "Later run of an older setup": the uninstall key <c>{&lt;AppId&gt;}_is1</c> of the installation
            /// (AppId and install mode of <c>install.ini</c>; <c>admin</c> HKLM64, <c>user</c> HKCU) exists, names this install
            /// root and lacks <c>Empire Earth Community: ContractVersion</c>. A missing or unreadable key, another root and
            /// portable installations leave the rule out.
            /// </summary>
            private bool OlderSetupRanAfter(InstallInfoFile info)
            {
                if (string.IsNullOrWhiteSpace(info.AppId) ||
                    (info.InstallMode != InstallMode.Admin && info.InstallMode != InstallMode.User))
                    return false;
                string path = ContractNames.UninstallKey + @"\{" + info.AppId.Trim() + "}_is1";
                RegistryLocation key = info.InstallMode == InstallMode.Admin
                    ? RegistryLocation.LocalMachine64(path)
                    : RegistryLocation.CurrentUser(path);
                IRegistry registry = owner.registry;

                RegistryResult probe = registry.ProbeKey(key);
                if (!probe.IsOk)
                {
                    if (probe.Status != RegistryStatus.Missing)
                        Logger.Info("Integrity: the uninstall key " + key + " cannot be read (" + probe + "); the rule of an older setup does not apply.");
                    return false;
                }

                string appPath = RegistryReads.GetStringOrNull(registry, key, ContractNames.UninstallAppPathName);
                string root = RegistryReads.ToRoot(!string.IsNullOrWhiteSpace(appPath)
                    ? appPath
                    : RegistryReads.GetStringOrNull(registry, key, ContractNames.UninstallInstallLocationName));
                if (root == null || !WinPath.IsSamePath(root, installation.Root))
                {
                    Logger.Info("Integrity: the uninstall key " + key + " names another install root (" + (root ?? "none") +
                                "); the rule of an older setup does not apply.");
                    return false;
                }

                RegistryResult<RegistryValue> value = registry.GetValue(key, ContractNames.UninstallContractVersionName);
                if (value.IsOk)
                    return false;
                if (value.Status != RegistryStatus.Missing)
                {
                    Logger.Info("Integrity: " + ContractNames.UninstallContractVersionName + " of " + key + " cannot be read (" +
                                value + "); the rule of an older setup does not apply.");
                    return false;
                }
                Logger.Warning("Integrity: the state of " + installation.Root + " is unknown (" + UnknownReason.OlderSetupRanAfter +
                               "): the uninstall key " + key + " lacks " + ContractNames.UninstallContractVersionName +
                               ", so an older setup ran after the one that wrote the manifest, or the last setup could not replace its records.");
                return true;
            }

            /// <summary>
            /// Why the check must stop now, or null: a cancelled token (a setup that runs is named as the reason) or a setup
            /// mutex.
            /// </summary>
            private CancelReason? StopReason()
            {
                bool setupRunning = RunningGameDetector.FindRunningSetup(owner.mutexProbe) != null;
                if (setupRunning)
                    return CancelReason.SetupRunning;
                return cancellationToken.IsCancellationRequested ? CancelReason.Requested : (CancelReason?)null;
            }

            /// <summary>The SHA-256 of a file, read once; the file is always closed when this returns.</summary>
            private HashResult Hash(string fullPath)
            {
                FileSystemResult<Stream> opened = FileSystem.OpenRead(fullPath);
                if (!opened.IsOk)
                    return new HashResult(opened.Status, null, opened.Detail, null);

                try
                {
                    using (Stream stream = opened.Value)
                    using (SHA256 sha256 = SHA256.Create())
                    {
                        var buffer = new byte[BufferSize];
                        long sinceProbe = 0;
                        int read;
                        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            sha256.TransformBlock(buffer, 0, read, null, 0);
                            sinceProbe += read;
                            if (cancellationToken.IsCancellationRequested || sinceProbe >= ProbeIntervalBytes)
                            {
                                sinceProbe = 0;
                                CancelReason? stop = StopReason();
                                if (stop != null)
                                    return new HashResult(FileSystemStatus.Ok, null, null, stop);
                            }
                        }
                        sha256.TransformFinalBlock(buffer, 0, 0);
                        return new HashResult(FileSystemStatus.Ok, ToHex(sha256.Hash), null, null);
                    }
                }
                catch (IOException ex)
                {
                    return new HashResult(FileSystemStatus.IoError, null, ex.Message, null);
                }
                catch (UnauthorizedAccessException ex)
                {
                    return new HashResult(FileSystemStatus.AccessDenied, null, ex.Message, null);
                }
            }
        }

        /// <summary>The outcome of hashing one file.</summary>
        private sealed class HashResult
        {
            public HashResult(FileSystemStatus status, string hash, string detail, CancelReason? stop)
            {
                Status = status;
                Hash = hash;
                Detail = detail;
                Stop = stop;
            }

            public FileSystemStatus Status { get; }

            /// <summary>64 lowercase hex digits; null unless the file was read to its end.</summary>
            public string Hash { get; }

            public string Detail { get; }

            /// <summary>Set if the check must stop (the file is closed, no hash).</summary>
            public CancelReason? Stop { get; }
        }

        /// <summary>Lowercase hex digits of <paramref name="bytes"/>, as the manifest writes hashes.</summary>
        internal static string ToHex(IEnumerable<byte> bytes)
        {
            var text = new StringBuilder();
            foreach (byte value in bytes)
                text.Append(value.ToString("x2", CultureInfo.InvariantCulture));
            return text.ToString();
        }
    }
}

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
using Microsoft.Win32;

namespace Empire_Earth_Launcher.Core.Maintenance
{
    /// <summary>One key of the cleanup list as <see cref="RegistryCleanup.Scan"/> found it.</summary>
    public sealed class CleanupItem
    {
        internal CleanupItem(CleanupEntry entry, CleanupState state, string folder, DriveKind? driveKind, bool? cdKeysExist)
        {
            Entry = entry ?? throw new ArgumentNullException(nameof(entry));
            State = state;
            Folder = folder;
            DriveKind = driveKind;
            Advice = CleanupAdvice.For(entry, state, folder, cdKeysExist);
        }

        public CleanupEntry Entry { get; }

        public CleanupState State { get; }

        /// <summary>The folder the "Installed From" values of the key name; null if none.</summary>
        public string Folder { get; }

        /// <summary>The kind of the drive of <see cref="Folder"/>; null if it was not needed.</summary>
        public DriveKind? DriveKind { get; }

        public CleanupAdvice Advice { get; }

        /// <summary>True if the launcher offers to delete the key: an HKCU key of the list that is stale.</summary>
        public bool IsOffered
        {
            get { return Entry.Scope == CleanupScope.LauncherDeletes && State == CleanupState.Stale; }
        }

        /// <summary>True if the Tools page lists the key: it exists, and it is offered or shown read-only when kept.</summary>
        public bool IsShown
        {
            get { return State != CleanupState.Missing && (IsOffered || Entry.ShownWhenKept); }
        }

        public override string ToString()
        {
            return Entry.Id + ": " + State + (Folder == null ? string.Empty : " (" + Folder + ")");
        }
    }

    /// <summary>The cleanup list on this computer for one discovery result (<see cref="RegistryCleanup.Scan"/>).</summary>
    public sealed class CleanupScan
    {
        internal CleanupScan(DiscoveryResult discovery, IEnumerable<CleanupItem> items)
        {
            Discovery = discovery ?? throw new ArgumentNullException(nameof(discovery));
            Items = new ReadOnlyCollection<CleanupItem>(items.ToList());
        }

        /// <summary>The installations the scan judged the keys against.</summary>
        public DiscoveryResult Discovery { get; }

        /// <summary>Every entry of the list, in the order of <see cref="CleanupCandidates.All"/>.</summary>
        public IReadOnlyList<CleanupItem> Items { get; }

        /// <summary>The keys the launcher offers to delete.</summary>
        public IReadOnlyList<CleanupItem> Offered
        {
            get { return Items.Where(item => item.IsOffered).ToList(); }
        }

        /// <summary>The keys listed read-only (kept, HKLM with or without advice, <c>Software\Sierra</c>).</summary>
        public IReadOnlyList<CleanupItem> ReadOnly
        {
            get { return Items.Where(item => item.IsShown && !item.IsOffered).ToList(); }
        }

        /// <summary>
        /// False if there is nothing the launcher could delete: the page says "nothing to clean up" and shows the read-only
        /// list without an enabled delete button (ADR 0007 plan review).
        /// </summary>
        public bool HasCandidates
        {
            get { return Items.Any(item => item.IsOffered); }
        }
    }

    /// <summary>How <see cref="RegistryCleanup.Delete"/> ended.</summary>
    public enum CleanupOutcome
    {
        /// <summary>The selected keys were backed up and deleted.</summary>
        Done,
        /// <summary>A setup or a game runs (<see cref="CleanupResult.Block"/>); nothing was changed.</summary>
        Blocked,
        /// <summary>A selected key is not stale any more (an installation appeared, a drive came back); nothing was changed.</summary>
        NoLongerStale,
        /// <summary>The backup could not be written completely; nothing was changed.</summary>
        BackupFailed,
        /// <summary>A key could not be deleted after the backup; the keys before it are deleted, the backup restores them.</summary>
        Failed
    }

    /// <summary>The result of <see cref="RegistryCleanup.Delete"/>.</summary>
    public sealed class CleanupResult
    {
        internal CleanupResult(CleanupOutcome outcome, MutationCheck block, string backupFile, IEnumerable<RegistryLocation> deleted,
            string problem)
        {
            Outcome = outcome;
            Block = block;
            BackupFile = backupFile;
            Deleted = new ReadOnlyCollection<RegistryLocation>((deleted ?? Enumerable.Empty<RegistryLocation>()).ToList());
            Problem = problem;
        }

        public CleanupOutcome Outcome { get; }

        /// <summary>Why the cleanup was blocked (<see cref="CleanupOutcome.Blocked"/>), else null.</summary>
        public MutationCheck Block { get; }

        /// <summary>The <c>.reg</c> backup (full path); null if none was written.</summary>
        public string BackupFile { get; }

        /// <summary>The folder of the backup; null if none was written.</summary>
        public string BackupFolder
        {
            get { return BackupFile == null ? null : WinPath.GetParent(BackupFile); }
        }

        /// <summary>The keys that were deleted, in order.</summary>
        public IReadOnlyList<RegistryLocation> Deleted { get; }

        /// <summary>For the log and the details of an error; null if nothing failed.</summary>
        public string Problem { get; }

        public override string ToString()
        {
            return Outcome + (Block == null ? string.Empty : " " + Block) + ", " +
                   Deleted.Count.ToString(CultureInfo.InvariantCulture) + " deleted" +
                   (BackupFile == null ? string.Empty : ", backup " + BackupFile) + (Problem == null ? string.Empty : ": " + Problem);
        }
    }

    /// <summary>
    /// The registry cleanup of the Tools page (R5, contract 3.8, ADR 0007): scans the keys of <see cref="CleanupCandidates"/>
    /// read-only, and deletes the HKCU keys the player selected after the mutation guard and a <c>.reg</c> backup.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A key is stale only when no installation of its product was found and the folder its "Installed From" values name is
    /// missing on a present, fixed, local drive (ADR 0007 amendment of the design review). HKLM keys are never changed: when
    /// they are stale the page advises to export and delete them with the Registry Editor as an administrator (SSSI and Mad
    /// Doc keys only). <c>Software\Sierra</c> is listed as "do not delete"; whether <c>CDKeys</c> exists below it is the only
    /// thing read of it (O8).
    /// </para>
    /// <para>
    /// Delete: selection -> mutation guard (no setup, no game, ADR 0016) -> the selected keys checked again -> one
    /// <c>.reg</c> file with every selected key and its subkeys, written and read back -> only then the keys deleted, in the
    /// order of the list. If the backup fails, nothing is deleted. The registry passed in is the policy-checked one of the
    /// launcher (<see cref="PolicyCheckedRegistry"/>), so even a wrong entry could not reach a protected key. The existence of
    /// every <c>CDKeys</c> key is logged before and after; a key that vanished would be logged as an error.
    /// </para>
    /// </remarks>
    public sealed class RegistryCleanup
    {
        /// <summary>The name of the backup subfolder and file: <c>&lt;yyyy-MM-dd_HHmmss&gt;_registry-cleanup</c>.</summary>
        public const string BackupWhat = "registry-cleanup";

        private readonly IRegistry registry;
        private readonly IFileSystem fileSystem;
        private readonly MutationGuard guard;
        private readonly BackupLocations backups;
        private readonly ILogger logger;

        /// <param name="registry">The registry; in the launcher the policy-checked one (ADR 0007).</param>
        /// <param name="fileSystem">For the folders and drives the keys name.</param>
        /// <param name="guard">Blocks the deletion while a setup or a game runs (ADR 0016).</param>
        /// <param name="backups">The backup folder.</param>
        /// <param name="logger">Log of the launcher.</param>
        public RegistryCleanup(IRegistry registry, IFileSystem fileSystem, MutationGuard guard, BackupLocations backups,
            ILogger logger)
        {
            this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
            this.fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            this.guard = guard ?? throw new ArgumentNullException(nameof(guard));
            this.backups = backups ?? throw new ArgumentNullException(nameof(backups));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>Reads the state of every key of the list against <paramref name="discovery"/>. Read-only.</summary>
        public CleanupScan Scan(DiscoveryResult discovery)
        {
            if (discovery == null)
                throw new ArgumentNullException(nameof(discovery));
            var items = new List<CleanupItem>();
            foreach (CleanupEntry entry in CleanupCandidates.All)
            {
                CleanupItem item = IsSameKeyAsItsTwin(entry) ? new CleanupItem(entry, CleanupState.Missing, null, null, null)
                    : ScanEntry(entry, discovery);
                items.Add(item);
            }
            var scan = new CleanupScan(discovery, items);
            logger.Info("Registry cleanup: " + scan.Offered.Count.ToString(CultureInfo.InvariantCulture) + " key(s) offered, " +
                        scan.ReadOnly.Count.ToString(CultureInfo.InvariantCulture) + " shown read-only" +
                        (scan.Items.Any(item => item.State != CleanupState.Missing)
                            ? ": " + string.Join("; ", scan.Items.Where(item => item.State != CleanupState.Missing))
                            : string.Empty) + ".");
            return scan;
        }

        /// <summary>
        /// Deletes the keys of <paramref name="selection"/> (offered items of <paramref name="scan"/>): guard, check, backup,
        /// delete. Nothing is changed if the guard blocks, a key is not stale any more or the backup fails.
        /// </summary>
        /// <exception cref="ArgumentException">An item is not an offered item of the scan (the page offers no other).</exception>
        public CleanupResult Delete(CleanupScan scan, IEnumerable<CleanupItem> selection)
        {
            if (scan == null)
                throw new ArgumentNullException(nameof(scan));
            if (selection == null)
                throw new ArgumentNullException(nameof(selection));
            List<CleanupItem> selected = selection.Distinct().ToList();
            if (selected.Count == 0)
                throw new ArgumentException("Nothing is selected.", nameof(selection));
            foreach (CleanupItem item in selected)
            {
                if (item == null || !scan.Items.Contains(item) || !item.IsOffered || !CleanupCandidates.All.Contains(item.Entry))
                    throw new ArgumentException("Only an offered key of the scan can be deleted: " + item, nameof(selection));
            }

            MutationCheck check = guard.Check("delete stale registry keys");
            if (!check.IsAllowed)
                return new CleanupResult(CleanupOutcome.Blocked, check, null, null, null);

            // The scan may be old: a drive may have come back or the key changed meanwhile.
            foreach (CleanupItem item in selected)
            {
                CleanupItem now = ScanEntry(item.Entry, scan.Discovery);
                if (now.State != CleanupState.Stale)
                {
                    logger.Warning("Registry cleanup: " + item.Entry.Key + " is not stale any more (" + now.State + "); nothing was changed.");
                    return new CleanupResult(CleanupOutcome.NoLongerStale, null, null, null, item.Entry.Key + ": " + now.State);
                }
            }

            IReadOnlyList<bool?> cdKeysBefore = CdKeysPresence();
            FileSystemResult<BackupFolder> folder = backups.CreateSubfolder(BackupWhat);
            if (!folder.IsOk)
                return BackupFailed("the backup folder could not be created: " + folder, null);
            var keys = new List<RegFileKey>();
            foreach (CleanupItem item in selected)
            {
                RegistryResult<IReadOnlyList<RegFileKey>> tree = RegistryExport.ReadTree(registry, item.Entry.Key);
                if (!tree.IsOk)
                    return BackupFailed(item.Entry.Key + " could not be read: " + tree, null);
                keys.AddRange(tree.Value);
            }
            string file = WinPath.Combine(folder.Value.Path,
                BackupLocations.TimeStamp(folder.Value.Time) + "_" + BackupWhat + ".reg");
            FileSystemResult written = backups.WriteRegFile(file, keys);
            if (!written.IsOk)
                return BackupFailed("the backup " + file + " could not be written: " + written, null);

            var deleted = new List<RegistryLocation>();
            foreach (CleanupItem item in selected)
            {
                RegistryResult result = registry.DeleteSubKeyTree(item.Entry.Key);
                if (!result.IsOk && result.Status != RegistryStatus.Missing)
                {
                    logger.Error("Registry cleanup: " + item.Entry.Key + " could not be deleted: " + result +
                                 "; the backup " + file + " restores the keys deleted before.");
                    CheckCdKeys(cdKeysBefore);
                    return new CleanupResult(CleanupOutcome.Failed, null, file, deleted, item.Entry.Key + ": " + result);
                }
                logger.Info("Registry cleanup: deleted " + item.Entry.Key + " (" + item.Entry.Id + ", stale: " +
                            (item.Folder ?? "no folder") + " is missing; evidence " + item.Entry.Evidence + ").");
                deleted.Add(item.Entry.Key);
            }
            CheckCdKeys(cdKeysBefore);
            logger.Info("Registry cleanup: " + deleted.Count.ToString(CultureInfo.InvariantCulture) + " key(s) deleted, backup " +
                        file + ".");
            return new CleanupResult(CleanupOutcome.Done, null, file, deleted, null);
        }

        private CleanupResult BackupFailed(string problem, string file)
        {
            logger.Error("Registry cleanup: nothing was deleted because the backup failed: " + problem + ".");
            return new CleanupResult(CleanupOutcome.BackupFailed, null, file, null, problem);
        }

        /// <summary>The state of one key: missing, protected, kept for a reason, or stale.</summary>
        private CleanupItem ScanEntry(CleanupEntry entry, DiscoveryResult discovery)
        {
            RegistryResult probe = registry.ProbeKey(entry.Key);
            if (probe.Status == RegistryStatus.Missing)
                return new CleanupItem(entry, CleanupState.Missing, null, null, null);
            if (!probe.IsOk)
            {
                logger.Warning("Registry cleanup: " + entry.Key + " cannot be read: " + probe + ".");
                return new CleanupItem(entry, CleanupState.Unreadable, null, null, null);
            }

            if (entry.Scope == CleanupScope.Protected)
            {
                RegistryResult cdKeys = registry.ProbeKey(CleanupCandidates.CdKeysBelow(entry.Key));
                bool? exists = cdKeys.IsOk ? true : cdKeys.Status == RegistryStatus.Missing ? false : (bool?)null;
                return new CleanupItem(entry, CleanupState.Protected, null, null, exists);
            }

            RegistryResult<RegistryValue> volume = registry.GetValue(entry.Key, ContractNames.InstalledFromVolumeName);
            RegistryResult<RegistryValue> directory = registry.GetValue(entry.Key, ContractNames.InstalledFromDirectoryName);
            if (IsReadError(volume) || IsReadError(directory))
                return new CleanupItem(entry, CleanupState.Unreadable, null, null, null);
            string folder = InstalledFromReader.CombineInstallLocation(StringOf(volume), StringOf(directory));
            if (folder != null && WinPath.GetDrive(folder) == null && !folder.StartsWith(@"\\", StringComparison.Ordinal))
                folder = null;

            if (discovery.Installations.Any(installation => installation.Product == entry.Product))
                return new CleanupItem(entry, CleanupState.InstallationFound, folder, null, null);
            if (folder == null)
                return new CleanupItem(entry, CleanupState.NoFolderNamed, null, null, null);
            DriveKind drive = fileSystem.GetDriveKind(folder);
            if (drive != DriveKind.Fixed)
                return new CleanupItem(entry, CleanupState.DriveNotFixed, folder, drive, null);
            if (fileSystem.DirectoryExists(folder))
                return new CleanupItem(entry, CleanupState.FolderExists, folder, drive, null);
            if (!IsSurelyMissing(folder))
            {
                logger.Info("Registry cleanup: " + entry.Key + " is kept, whether " + folder + " exists cannot be told.");
                return new CleanupItem(entry, CleanupState.FolderUnknown, folder, drive, null);
            }
            return new CleanupItem(entry, CleanupState.Stale, folder, drive, null);
        }

        /// <summary>
        /// True only if <paramref name="folder"/> is known to be missing (ADR 0007 amendment: "the folder is missing"). Windows
        /// reports a folder it may not look at as missing too (<c>Directory.Exists</c>), so the parent decides: it is missing,
        /// or it can be listed and has no such folder. Access denied or any other error: not known (security review).
        /// A drive root that cannot be listed ends the walk as not known.
        /// </summary>
        private bool IsSurelyMissing(string folder)
        {
            string parent = WinPath.GetParent(folder);
            if (parent == null)
                return false;
            FileSystemResult<IReadOnlyList<string>> folders = fileSystem.GetDirectories(parent);
            if (folders.IsOk)
                return !folders.Value.Any(path => WinPath.IsSamePath(path, folder));
            if (folders.Status == FileSystemStatus.NotFound)
                return IsSurelyMissing(parent); // the same question one level up, ending at a folder that can be listed
            return false;
        }

        private static bool IsReadError(RegistryResult<RegistryValue> value)
        {
            return !value.IsOk && value.Status != RegistryStatus.Missing;
        }

        private static string StringOf(RegistryResult<RegistryValue> value)
        {
            return value.IsOk && value.Value.IsString ? value.Value.StringValue : null;
        }

        /// <summary>
        /// True for an HKLM64 entry whose HKLM32 twin of the list exists with the same values and subkeys: on 32-bit Windows
        /// both views are one key, which is listed once (as HKLM32).
        /// </summary>
        private bool IsSameKeyAsItsTwin(CleanupEntry entry)
        {
            if (entry.Key.Hive != RegistryHive.LocalMachine || entry.Key.View != RegistryView.Registry64)
                return false;
            RegistryLocation twin = RegistryLocation.LocalMachine32(entry.Key.Path);
            if (!CleanupCandidates.All.Any(other => other.Key.Equals(twin)) || !registry.ProbeKey(entry.Key).IsOk ||
                !registry.ProbeKey(twin).IsOk)
                return false;
            RegistryResult<IReadOnlyList<RegFileKey>> mine = RegistryExport.ReadTree(registry, entry.Key);
            RegistryResult<IReadOnlyList<RegFileKey>> its = RegistryExport.ReadTree(registry, twin);
            if (!mine.IsOk || !its.IsOk)
                return false;
            // The content without the key names: the same bytes in a .reg file with the view of the twin.
            byte[] first = RegFileWriter.ToBytes(mine.Value.Select(key => Rebase(key, entry.Key, twin)));
            byte[] second = RegFileWriter.ToBytes(its.Value);
            return first.SequenceEqual(second);
        }

        private static RegFileKey Rebase(RegFileKey key, RegistryLocation from, RegistryLocation to)
        {
            string relative = key.Key.Path.Length == from.Path.Length ? string.Empty : key.Key.Path.Substring(from.Path.Length + 1);
            var rebased = new RegFileKey(relative.Length == 0 ? to : to.Child(relative));
            foreach (KeyValuePair<string, RegistryValue> value in key.Values)
                rebased.Add(value.Key, value.Value);
            return rebased;
        }

        /// <summary>Whether each <c>CDKeys</c> key of the list exists (null: cannot be read), for the log; never its values.</summary>
        private IReadOnlyList<bool?> CdKeysPresence()
        {
            var presence = new List<bool?>();
            foreach (CleanupEntry entry in CleanupCandidates.All.Where(entry => entry.Scope == CleanupScope.Protected))
            {
                RegistryResult probe = registry.ProbeKey(CleanupCandidates.CdKeysBelow(entry.Key));
                presence.Add(probe.IsOk ? true : probe.Status == RegistryStatus.Missing ? false : (bool?)null);
            }
            return presence;
        }

        /// <summary>Logs the CD keys after a cleanup: one that existed before and is gone now would be a severe bug.</summary>
        private void CheckCdKeys(IReadOnlyList<bool?> before)
        {
            IReadOnlyList<bool?> after = CdKeysPresence();
            List<CleanupEntry> protectedEntries = CleanupCandidates.All.Where(entry => entry.Scope == CleanupScope.Protected).ToList();
            for (int i = 0; i < protectedEntries.Count; i++)
            {
                string where = CleanupCandidates.CdKeysBelow(protectedEntries[i].Key).ToString();
                if (before[i] == true && after[i] == false)
                    logger.Error("Registry cleanup: " + where + " existed before the cleanup and is missing now.");
                else if (before[i] == true)
                    logger.Info("Registry cleanup: " + where + " exists, unchanged by the cleanup.");
            }
        }
    }
}

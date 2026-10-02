using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Logging;
using Empire_Earth_Launcher.Core.Maintenance;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Core.Play;

namespace Empire_Earth_Launcher
{
    /// <summary>What the maintenance tools found for one search result (<see cref="MaintenanceModel"/>).</summary>
    internal sealed class MaintenanceScan
    {
        public MaintenanceScan(DiscoveryResult discovery, CleanupScan cleanup, WonLoginFiles wonFiles,
            VirtualStoreReport virtualStore, IReadOnlyList<SavedGameFile> savedGames, NameCheckReport names)
        {
            Discovery = discovery;
            Cleanup = cleanup;
            WonFiles = wonFiles;
            VirtualStore = virtualStore;
            SavedGames = savedGames;
            Names = names;
        }

        public DiscoveryResult Discovery { get; }

        /// <summary>The registry cleanup; it does not depend on the selected installation.</summary>
        public CleanupScan Cleanup { get; }

        /// <summary>The WON login files of the selected installation; null without one.</summary>
        public WonLoginFiles WonFiles { get; }

        /// <summary>The VirtualStore copies of the selected installation; null without one.</summary>
        public VirtualStoreReport VirtualStore { get; }

        /// <summary>The saved games and scenarios of the selected installation; null without one.</summary>
        public IReadOnlyList<SavedGameFile> SavedGames { get; }

        /// <summary>The name check of the selected installation; null without one.</summary>
        public NameCheckReport Names { get; }
    }

    /// <summary>
    /// The maintenance tools of the Tools page (L-WP8): the registry cleanup (R5), the WON login reset (R6), the VirtualStore
    /// check (R8), saved games and scenarios and the name check (R10), and "Open backup folder" (ADR 0007).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Created once by <see cref="Program"/>; use it on the UI thread. After every search of the installations the read-only
    /// scans run on the thread pool (nothing waits for them); the actions run on the thread pool too and scan again when
    /// they are done. <see cref="Changed"/> is raised on the UI thread.
    /// </para>
    /// <para>
    /// Every writing action asks the mutation guard in the core (ADR 0016); while the setup watcher knows that a setup runs
    /// the page does not offer them at all (contract 4.2). Only the latest scan counts.
    /// </para>
    /// </remarks>
    internal sealed class MaintenanceModel
    {
        private readonly RegistryCleanup cleanup;
        private readonly WonLoginReset wonLoginReset;
        private readonly VirtualStoreScanner virtualStoreScanner;
        private readonly SavedGames savedGames;
        private readonly NameChecks nameChecks;
        private readonly InstallationService installations;
        private readonly SetupWatcher setupWatcher;
        private readonly IProcessStarter shell;
        private readonly IFileSystem fileSystem;
        private readonly ILogger logger;

        /// <summary>The search result the latest scan was started for.</summary>
        private DiscoveryResult scannedResult;

        /// <summary>Counts the scans, so that only the result of the latest one is used.</summary>
        private int generation;

        public MaintenanceModel(RegistryCleanup cleanup, WonLoginReset wonLoginReset, VirtualStoreScanner virtualStoreScanner,
            SavedGames savedGames, NameChecks nameChecks, InstallationService installations, SetupWatcher setupWatcher,
            IProcessStarter shell, IFileSystem fileSystem, string backupDirectory, ILogger logger)
        {
            this.cleanup = cleanup ?? throw new ArgumentNullException(nameof(cleanup));
            this.wonLoginReset = wonLoginReset ?? throw new ArgumentNullException(nameof(wonLoginReset));
            this.virtualStoreScanner = virtualStoreScanner ?? throw new ArgumentNullException(nameof(virtualStoreScanner));
            this.savedGames = savedGames ?? throw new ArgumentNullException(nameof(savedGames));
            this.nameChecks = nameChecks ?? throw new ArgumentNullException(nameof(nameChecks));
            this.installations = installations ?? throw new ArgumentNullException(nameof(installations));
            this.setupWatcher = setupWatcher ?? throw new ArgumentNullException(nameof(setupWatcher));
            this.shell = shell ?? throw new ArgumentNullException(nameof(shell));
            this.fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            BackupDirectory = backupDirectory ?? throw new ArgumentNullException(nameof(backupDirectory));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            installations.Changed += (sender, e) => OnInstallationsChanged();
            setupWatcher.SetupStarted += (sender, e) => RaiseChanged();
            setupWatcher.SetupFinished += (sender, e) => RaiseChanged();
        }

        /// <summary>Raised when a scan or an action starts or ends, and when the setup state changed.</summary>
        public event EventHandler Changed;

        /// <summary>The backup folder of the launcher (<c>%LOCALAPPDATA%\Empire Earth Launcher\Backups</c>).</summary>
        public string BackupDirectory { get; }

        /// <summary>The selected installation; null while none is known or none was found.</summary>
        public Installation Selected
        {
            get { return installations.Selected; }
        }

        /// <summary>The latest scan; null before the first one and while the first scan after a new search runs.</summary>
        public MaintenanceScan Scan { get; private set; }

        public bool IsScanning { get; private set; }

        /// <summary>True while an action of the page runs.</summary>
        public bool IsBusy { get; private set; }

        /// <summary>The setup that runs (the page offers no change then, contract 4.2); null if none.</summary>
        public Product RunningSetup
        {
            get { return setupWatcher.RunningSetup; }
        }

        /// <summary>The task of the latest scan (for the tests); null before the first one.</summary>
        public Task LastScan { get; private set; }

        /// <summary>True if the writing actions can be offered now: no setup runs, no search and no action.</summary>
        public bool CanChange
        {
            get { return !setupWatcher.IsSetupRunning && !installations.IsSearching && !installations.IsWaitingForSetup && !IsBusy; }
        }

        /// <summary>
        /// Scans again for the current search result (after an action, or on request). Not while a setup runs (contract 4.2:
        /// the scans read the manifest of the installation); the search that follows the end of the setup scans again.
        /// </summary>
        public Task RefreshAsync()
        {
            DiscoveryResult result = installations.Result;
            if (result == null || installations.IsSearching)
                return Task.CompletedTask;
            if (setupWatcher.IsSetupRunning || installations.IsWaitingForSetup)
            {
                logger.Info("Maintenance tools: no scan while a setup is running; the search after the setup scans again.");
                return Task.CompletedTask;
            }
            scannedResult = result;
            int current = ++generation;
            IsScanning = true;
            RaiseChanged();
            Task scan = RunScanAsync(result, current);
            LastScan = scan;
            scan.ContinueWith(task => logger.Error("The scan of the maintenance tools failed.", task.Exception),
                System.Threading.CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
            return scan;
        }

        private async Task RunScanAsync(DiscoveryResult result, int current)
        {
            MaintenanceScan scan;
            try
            {
                scan = await Task.Run(() => ScanNow(result));
            }
            catch (Exception)
            {
                if (current == generation)
                {
                    IsScanning = false;
                    RaiseChanged();
                }
                throw;
            }
            if (current != generation)
                return;
            Scan = scan;
            IsScanning = false;
            RaiseChanged();
        }

        private MaintenanceScan ScanNow(DiscoveryResult result)
        {
            Installation selected = result.Selected;
            CleanupScan cleanupScan = cleanup.Scan(result);
            if (selected == null)
                return new MaintenanceScan(result, cleanupScan, null, null, null, null);
            return new MaintenanceScan(result, cleanupScan, wonLoginReset.Find(selected), virtualStoreScanner.Scan(selected),
                savedGames.List(selected), nameChecks.Check(selected));
        }

        /// <summary>Deletes the selected keys of the registry cleanup (guard, backup, delete), then scans again.</summary>
        public Task<CleanupResult> DeleteKeysAsync(IReadOnlyList<CleanupItem> selection)
        {
            if (selection == null)
                throw new ArgumentNullException(nameof(selection));
            CleanupScan scan = Scan?.Cleanup ?? throw new InvalidOperationException("Nothing was scanned yet.");
            return RunActionAsync(() => cleanup.Delete(scan, selection));
        }

        /// <summary>Resets the WON login of the selected installation (guard, move into the backup), then scans again.</summary>
        public Task<WonResetResult> ResetWonLoginAsync()
        {
            Installation selected = Selected ?? throw new InvalidOperationException("No installation is selected.");
            return RunActionAsync(() => wonLoginReset.Reset(selected));
        }

        /// <summary>Exports the saved games and scenarios of the selected installation into a new folder of <paramref name="folder"/>.</summary>
        public Task<ExportResult> ExportAsync(string folder)
        {
            Installation selected = Selected ?? throw new InvalidOperationException("No installation is selected.");
            return RunActionAsync(() => savedGames.Export(selected, folder));
        }

        /// <summary>Checks the files the player chose for <paramref name="game"/> (read-only, quick).</summary>
        public ImportPlan PlanImport(Game game, IEnumerable<string> files)
        {
            Installation selected = Selected ?? throw new InvalidOperationException("No installation is selected.");
            return savedGames.PlanImport(selected, game, files);
        }

        /// <summary>Imports the checked files (guard, backup of replaced files, write), then scans again.</summary>
        public Task<ImportResult> ImportAsync(ImportPlan plan, bool overwriteConfirmed)
        {
            if (plan == null)
                throw new ArgumentNullException(nameof(plan));
            return RunActionAsync(() => savedGames.Import(plan, overwriteConfirmed));
        }

        /// <summary>
        /// Opens the backup folder in the Explorer (created first if there is none yet, it is the launcher's own folder);
        /// returns null, or the error to show.
        /// </summary>
        public string OpenBackupFolder()
        {
            FileSystemResult created = fileSystem.CreateDirectory(BackupDirectory);
            if (!created.IsOk)
            {
                logger.Warning("The backup folder " + BackupDirectory + " could not be created: " + created + ".");
                return created.Detail ?? created.Status.ToString();
            }
            try
            {
                shell.OpenFolder(BackupDirectory);
                logger.Info("The backup folder " + BackupDirectory + " was opened.");
                return null;
            }
            catch (Exception ex) when (ex is Win32Exception || ex is FileNotFoundException || ex is InvalidOperationException)
            {
                logger.Warning("The backup folder " + BackupDirectory + " could not be opened: " + ex.Message);
                return ex.Message;
            }
        }

        private async Task<T> RunActionAsync<T>(Func<T> action)
        {
            if (IsBusy)
                throw new InvalidOperationException("Another action of the Tools page is running.");
            IsBusy = true;
            RaiseChanged();
            try
            {
                return await Task.Run(action);
            }
            finally
            {
                IsBusy = false;
                // The scan shows what the action changed; it raises Changed itself. A failing scan is logged by RefreshAsync
                // and must neither hide the result of the action nor replace its exception (build/UI review), so it is
                // awaited through WhenAny, which never throws.
                await Task.WhenAny(RefreshAsync());
                RaiseChanged();
            }
        }

        /// <summary>A search has a new result: the read-only scans run for it.</summary>
        private void OnInstallationsChanged()
        {
            DiscoveryResult result = installations.Result;
            if (installations.IsSearching || result == null || result == scannedResult)
            {
                RaiseChanged();
                return;
            }
            Scan = null;
            RefreshAsync();
        }

        private void RaiseChanged()
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}

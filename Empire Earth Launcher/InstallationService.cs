using System;
using System.Threading;
using System.Threading.Tasks;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Logging;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Core.Play;
using Empire_Earth_Launcher.Core.Settings;

namespace Empire_Earth_Launcher
{
    /// <summary>
    /// The installations of Empire Earth the launcher knows and the one it works with: the result of the core's
    /// <see cref="InstallationDiscovery"/> (contract 1.4) with the folder chosen on the Launcher page as source 1
    /// (<see cref="LauncherSettings.GameDirectory"/> in settings.json). Replaces the GameDirectoryService and
    /// GameDirectoryLocator of the launcher before v2.
    /// </summary>
    /// <remarks>
    /// Created once by <see cref="Program"/>; use it on the UI thread. The discovery runs on the thread pool
    /// (<see cref="InstallationDiscovery.DiscoverAsync"/>), so the window never waits for the registry or a slow drive
    /// (ADR 0004); <see cref="Changed"/> is raised on the thread that started the refresh, the UI thread. Pages start a
    /// refresh through <see cref="UiOperation"/>.
    /// <para>
    /// While a setup runs, no discovery starts (contract 4.2: <c>install.ini</c> is not read while a setup mutex exists):
    /// the refresh waits (<see cref="IsWaitingForSetup"/>), the previous result stays, and when the
    /// <see cref="SetupWatcher"/> sees the setup end, the installations are searched again (L-WP6).
    /// </para>
    /// </remarks>
    internal sealed class InstallationService
    {
        private readonly ILogger logger;
        private readonly SettingsStore settings;
        private readonly InstallationDiscovery discovery;
        private readonly IFileSystem fileSystem;
        private readonly string launcherFolder;
        private readonly SetupWatcher setupWatcher;

        /// <summary>Counts the refreshes, so that only the result of the latest one is used.</summary>
        private int generation;

        private int running;

        /// <summary>True while <see cref="RefreshAsync"/> probes the setup mutexes, so that the end it sees does not start a second refresh.</summary>
        private bool probingSetup;

        /// <param name="logger">Log of the launcher.</param>
        /// <param name="settings">User settings; the chosen folder is saved there.</param>
        /// <param name="discovery">The discovery of the core.</param>
        /// <param name="fileSystem">The file system, to judge a folder the user picks.</param>
        /// <param name="launcherFolder">The folder of the launcher (source 5); null to skip it.</param>
        /// <param name="setupWatcher">Watches the setup mutexes (contract 4.2); null to search regardless of a setup.</param>
        public InstallationService(ILogger logger, SettingsStore settings, InstallationDiscovery discovery,
            IFileSystem fileSystem, string launcherFolder, SetupWatcher setupWatcher = null)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.discovery = discovery ?? throw new ArgumentNullException(nameof(discovery));
            this.fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            this.launcherFolder = launcherFolder;
            this.setupWatcher = setupWatcher;
            if (setupWatcher != null)
                setupWatcher.SetupFinished += OnSetupFinished;
        }

        /// <summary>Raised when a refresh starts or ends, and when <see cref="Result"/> changed.</summary>
        public event EventHandler Changed;

        /// <summary>True while a discovery runs.</summary>
        public bool IsSearching
        {
            get { return running > 0; }
        }

        /// <summary>
        /// True while a refresh waits for the end of a running setup (contract 4.2); the pages say so, and the search starts
        /// by itself when the setup has ended.
        /// </summary>
        public bool IsWaitingForSetup { get; private set; }

        /// <summary>The refresh started by the end of a setup (<see cref="SetupWatcher.SetupFinished"/>); null before the first.</summary>
        public Task RefreshAfterSetup { get; private set; }

        /// <summary>The result of the latest discovery; null until the first one has finished.</summary>
        public DiscoveryResult Result { get; private set; }

        /// <summary>The installation the launcher works with; null if none was found (or none is known yet).</summary>
        public Installation Selected
        {
            get { return Result?.Selected; }
        }

        /// <summary>
        /// Runs the discovery again with the folder chosen in the settings; while a setup runs, it only marks the refresh as
        /// waiting (<see cref="IsWaitingForSetup"/>) and keeps the previous result.
        /// </summary>
        public async Task RefreshAsync(CancellationToken cancellationToken = default)
        {
            int current = ++generation;
            if (setupWatcher != null)
            {
                SetupKind setup;
                probingSetup = true;
                try
                {
                    setup = setupWatcher.ProbeNow();
                }
                finally
                {
                    probingSetup = false;
                }
                if (setup != null)
                {
                    if (!IsWaitingForSetup)
                        logger.Info("The installations are searched when the " + setup.Id +
                                    " setup has ended (install.ini is not read while a setup runs, contract 4.2).");
                    IsWaitingForSetup = true;
                    Changed?.Invoke(this, EventArgs.Empty);
                    return;
                }
            }
            IsWaitingForSetup = false;
            running++;
            Changed?.Invoke(this, EventArgs.Empty);
            try
            {
                DiscoveryResult result = await discovery.DiscoverAsync(settings.Current.GameDirectory, launcherFolder,
                    cancellationToken);
                if (current != generation)
                    return; // a later refresh (another choice) has started; its result counts
                Result = result;
                LogSelection(result);
            }
            finally
            {
                running--;
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>The setup has ended: the installations are searched again (ARCHITECTURE 4.3).</summary>
        private void OnSetupFinished(object sender, SetupStateEventArgs e)
        {
            if (probingSetup)
                return; // RefreshAsync saw the end itself and goes on with the discovery
            Task refresh = RefreshAsync();
            RefreshAfterSetup = refresh;
            // The discovery returns environment problems as results (ADR 0013); a fault here is a programming error, which
            // is logged like an unobserved task exception.
            refresh.ContinueWith(task => logger.Error("The search for the installations after the setup failed.", task.Exception),
                CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
        }

        /// <summary>
        /// Saves <paramref name="folder"/> (an install root, EE folder or AoC folder) as the user's choice, source 1 of the
        /// discovery, and runs the discovery; null or white space switches back to the automatic selection.
        /// </summary>
        public Task ChooseFolderAsync(string folder)
        {
            settings.Current.GameDirectory = string.IsNullOrWhiteSpace(folder) ? string.Empty : folder.Trim();
            // If saving fails (logged by the store), the choice is still used for this session.
            settings.Save();
            return RefreshAsync();
        }

        /// <summary>Chooses an installation of the list: its EE folder is saved as the user's choice.</summary>
        /// <remarks>
        /// The EE folder, not the root: for a foreign installation with another folder name (<c>C:\Games\EE</c>) only the
        /// EE folder says where the game is, also when its "Installed From" values change later.
        /// </remarks>
        public Task SelectAsync(Installation installation)
        {
            if (installation == null)
                throw new ArgumentNullException(nameof(installation));
            return ChooseFolderAsync(installation.EeFolder);
        }

        /// <summary>Removes the user's choice: the first installation found is used.</summary>
        public Task UseAutomaticDetectionAsync()
        {
            return ChooseFolderAsync(null);
        }

        /// <summary>What a folder the user picked is (EE folder, AoC folder, install root or none of them).</summary>
        public GameFolderKind ClassifyFolder(string folder)
        {
            return GameFolders.Classify(fileSystem, folder);
        }

        private void LogSelection(DiscoveryResult result)
        {
            if (result.Selected == null)
                logger.Warning("No Empire Earth installation found. Choose the game folder in the launcher settings.");
            else
                logger.Info("Empire Earth folder: " + result.Selected.EeFolder + " (" +
                            (result.IsSelectedByUser ? "chosen by the user" : "source " + (int)result.Selected.Origin) + ").");
        }
    }
}

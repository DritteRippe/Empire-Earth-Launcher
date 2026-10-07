using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Diagnostics;
using Empire_Earth_Launcher.Core.GameSettings;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Logging;
using Empire_Earth_Launcher.Core.Mods;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Core.Play;
using Empire_Earth_Launcher.Properties;

namespace Empire_Earth_Launcher
{
    /// <summary>The dreXmod presets and the <c>dreXmod.config</c> of one game folder, as read for the page.</summary>
    internal sealed class GameModsLine
    {
        public GameModsLine(Game game, string modsFolder, ModFolderScan scan, DreXmodConfigFile config)
        {
            Game = game;
            ModsFolder = modsFolder;
            Scan = scan;
            Config = config;
        }

        public Game Game { get; }

        /// <summary><c>Data\dxm\mods</c> of the game folder.</summary>
        public string ModsFolder { get; }

        public ModFolderScan Scan { get; }

        public DreXmodConfigFile Config { get; }
    }

    /// <summary>What the Mods page shows for the selected installation (<see cref="ModsModel"/>).</summary>
    internal sealed class ModsSnapshot
    {
        public ModsSnapshot(Installation installation, DreXmodVersion version, IReadOnlyList<GameModsLine> games)
        {
            Installation = installation;
            Version = version;
            Games = games;
        }

        public Installation Installation { get; }

        /// <summary>The dreXmod the setup installed; only <see cref="DreXmodVersion.Version3"/> has presets.</summary>
        public DreXmodVersion Version { get; }

        /// <summary>The presets and the config of each game folder; empty unless <see cref="Version"/> is dreXmod 3.</summary>
        public IReadOnlyList<GameModsLine> Games { get; }
    }

    /// <summary>
    /// The Mods page (launcher 1.1.0): the dreXmod presets of the selected installation, as <c>Data\dxm\mods</c> holds them, with
    /// the name, last edit and author their <c>CREDITS</c> file gives, and which of them <c>dreXmod.config</c> names as the active
    /// mod and as the active lobby theme. The page and its model only read: the launcher neither installs nor switches a
    /// preset and writes no file (contract 2.5, ADR 0014); the two buttons open the folder and the config in the programs
    /// Windows has for them, and the player edits the config there.
    /// </summary>
    /// <remarks>
    /// Created once by <see cref="Program"/>; use it on the UI thread. The state is read on the thread pool after every search of
    /// the installations and when the page is shown (the player may have edited the config or made a preset meanwhile), never
    /// while a setup runs (contract 4.2); <see cref="Changed"/> is raised on the UI thread. Only the latest read counts. The
    /// page, and the navigation button that leads to it, exist for an installation with dreXmod 3 only (<see cref="IsAvailable"/>):
    /// dreXmod 2 has no mod system.
    /// </remarks>
    internal sealed class ModsModel
    {
        private readonly InstallationService installations;
        private readonly SetupWatcher setupWatcher;
        private readonly IFileSystem fileSystem;
        private readonly EffectivePathResolver effectivePaths;
        private readonly IProcessStarter shell;
        private readonly ILogger logger;

        /// <summary>Counts the reads, so that only the result of the latest one is used.</summary>
        private int generation;

        private bool showTemplates;

        /// <param name="installations">The installations; a new search result is read.</param>
        /// <param name="setupWatcher">While a setup runs, nothing is read or opened (contract 4.2).</param>
        /// <param name="fileSystem">Reads the presets and <c>dreXmod.config</c>.</param>
        /// <param name="effectivePaths">Where the game reads <c>dreXmod.config</c> (the VirtualStore copy first, ADR 0016).</param>
        /// <param name="shell">Opens the folder in the Explorer and the config in its program.</param>
        /// <param name="logger">Log of the launcher.</param>
        public ModsModel(InstallationService installations, SetupWatcher setupWatcher, IFileSystem fileSystem,
            EffectivePathResolver effectivePaths, IProcessStarter shell, ILogger logger)
        {
            this.installations = installations ?? throw new ArgumentNullException(nameof(installations));
            this.setupWatcher = setupWatcher ?? throw new ArgumentNullException(nameof(setupWatcher));
            this.fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            this.effectivePaths = effectivePaths ?? throw new ArgumentNullException(nameof(effectivePaths));
            this.shell = shell ?? throw new ArgumentNullException(nameof(shell));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            installations.Changed += (sender, e) => OnInstallationsChanged();
            setupWatcher.SetupStarted += (sender, e) => RaiseChanged();
            setupWatcher.SetupFinished += (sender, e) => RaiseChanged();
        }

        /// <summary>Raised when a read starts or ends, when the template switch changed and when the setup state changed.</summary>
        public event EventHandler Changed;

        /// <summary>The selected installation; null while none is known or none was found.</summary>
        public Installation Selected
        {
            get { return installations.Selected; }
        }

        /// <summary>True once a search of the installations has finished (the page says "searching" before).</summary>
        public bool HasResult
        {
            get { return installations.Result != null; }
        }

        /// <summary>True while the search waits for a setup that runs (contract 4.2).</summary>
        public bool IsWaitingForSetup
        {
            get { return installations.IsWaitingForSetup; }
        }

        /// <summary>The setup that runs (nothing is read or opened then, contract 4.2); null if none.</summary>
        public SetupKind RunningSetup
        {
            get { return setupWatcher.RunningSetup; }
        }

        /// <summary>The latest read; null before the first one, and while no installation is selected.</summary>
        public ModsSnapshot Snapshot { get; private set; }

        public bool IsReading { get; private set; }

        /// <summary>The task of the latest read (for the tests); null before the first one.</summary>
        public Task LastRead { get; private set; }

        /// <summary>
        /// True if the Mods page exists for the selected installation: its latest read found dreXmod 3. False before the first
        /// read, for an installation without dreXmod or with dreXmod 2, and while the selection moved to an installation that
        /// is not read yet.
        /// </summary>
        public bool IsAvailable
        {
            get
            {
                Installation selected = Selected;
                return selected != null && Snapshot != null && Snapshot.Installation == selected &&
                       Snapshot.Version == DreXmodVersion.Version3;
            }
        }

        /// <summary>True if the folder <c>template</c>, the skeleton for authors, is listed with the presets; off by default.</summary>
        public bool ShowTemplates
        {
            get { return showTemplates; }
            set
            {
                if (showTemplates == value)
                    return;
                showTemplates = value;
                RaiseChanged();
            }
        }

        /// <summary>True if the folder and the config can be opened now: a read result of the selected installation and no setup.</summary>
        public bool CanOpen
        {
            get
            {
                return IsAvailable && !setupWatcher.IsSetupRunning && !installations.IsSearching &&
                       !installations.IsWaitingForSetup;
            }
        }

        private void OnInstallationsChanged()
        {
            if (installations.IsSearching || installations.Result == null)
                return;
            Read();
        }

        /// <summary>Starts a read of the selected installation; its end raises <see cref="Changed"/>. Errors are logged.</summary>
        public void Read()
        {
            Task read = RefreshAsync();
            read.ContinueWith(task => logger.Error("Reading the dreXmod presets failed.", task.Exception),
                System.Threading.CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
        }

        /// <summary>
        /// Reads the presets and the configs again. Not while a setup runs (contract 4.2: the search after the setup reads
        /// again), and not before the first search has finished.
        /// </summary>
        public Task RefreshAsync()
        {
            DiscoveryResult result = installations.Result;
            if (result == null || installations.IsSearching)
                return Task.CompletedTask;
            if (setupWatcher.IsSetupRunning || installations.IsWaitingForSetup)
            {
                logger.Info("Mods page: no read while a setup is running; the search after the setup reads again.");
                return Task.CompletedTask;
            }
            Installation selected = result.Selected;
            int current = ++generation;
            if (selected == null)
            {
                Snapshot = null;
                IsReading = false;
                RaiseChanged();
                return Task.CompletedTask;
            }
            IsReading = true;
            RaiseChanged();
            Task read = RunReadAsync(selected, current);
            LastRead = read;
            return read;
        }

        private async Task RunReadAsync(Installation selected, int current)
        {
            ModsSnapshot snapshot;
            try
            {
                snapshot = await Task.Run(() => ReadNow(selected));
            }
            catch (Exception)
            {
                if (current == generation)
                {
                    IsReading = false;
                    RaiseChanged();
                }
                throw;
            }
            if (current != generation)
                return;
            Snapshot = snapshot;
            IsReading = false;
            RaiseChanged();
        }

        private ModsSnapshot ReadNow(Installation installation)
        {
            DreXmodVersion version = DreXmodInfo.Describe(installation, fileSystem);
            var games = new List<GameModsLine>();
            if (version == DreXmodVersion.Version3)
            {
                foreach (Game game in GameDefaultsService.GamesOf(installation))
                {
                    string folder = installation.GetGameFolder(game);
                    if (folder == null || !WinPath.IsFullyQualified(folder))
                        continue;
                    string modsFolder = ModFolderScanner.ModsFolder(folder);
                    ModFolderScan scan = ModFolderScanner.Scan(fileSystem, modsFolder);
                    if (scan.Status == ConfigFileStatus.Unreadable)
                        logger.Warning("The dreXmod presets in " + modsFolder + " could not be read: " + scan.Problem + ".");
                    DreXmodConfigFile config = DreXmodConfigReader.Read(fileSystem, effectivePaths, folder);
                    if (config.Status == ConfigFileStatus.Unreadable)
                        logger.Warning("dreXmod.config " + config.Path + " could not be read: " + config.Problem + ".");
                    games.Add(new GameModsLine(game, modsFolder, scan, config));
                }
            }
            return new ModsSnapshot(installation, version, games);
        }

        /// <summary>
        /// Opens <c>Data\dxm\mods</c> of <paramref name="game"/> in the Explorer, so that the player sees (and may add to) the
        /// presets; returns null, or the problem to show.
        /// </summary>
        public string OpenModsFolder(Game game)
        {
            GameModsLine line = LineOf(game);
            if (line == null)
                return Resources.ModsNothingToOpen;
            if (line.Scan.Status == ConfigFileStatus.Missing)
                return string.Format(CultureInfo.CurrentCulture, Resources.ModsFolderMissingFormat, line.ModsFolder);
            try
            {
                shell.OpenFolder(line.ModsFolder);
                logger.Info("The folder of the dreXmod presets " + line.ModsFolder + " was opened.");
                return null;
            }
            catch (Exception ex) when (ex is Win32Exception || ex is FileNotFoundException || ex is InvalidOperationException)
            {
                logger.Warning("The folder of the dreXmod presets " + line.ModsFolder + " could not be opened: " + ex.Message);
                return ex.Message;
            }
        }

        /// <summary>
        /// Opens the <c>dreXmod.config</c> of <paramref name="game"/> (the file the game reads) in the program Windows has for
        /// it; the launcher shows the file and does not change it. Returns null, or the problem to show.
        /// </summary>
        public string OpenConfig(Game game)
        {
            GameModsLine line = LineOf(game);
            if (line == null)
                return Resources.ModsNothingToOpen;
            if (line.Config.Status == ConfigFileStatus.Missing)
                return string.Format(CultureInfo.CurrentCulture, Resources.ModsConfigMissingFormat, line.Config.Path);
            try
            {
                shell.OpenFile(line.Config.Path);
                logger.Info("dreXmod.config " + line.Config.Path + " was opened.");
                return null;
            }
            catch (Exception ex) when (ex is Win32Exception || ex is FileNotFoundException || ex is InvalidOperationException)
            {
                logger.Warning("dreXmod.config " + line.Config.Path + " could not be opened: " + ex.Message);
                return ex.Message;
            }
        }

        /// <summary>The line of <paramref name="game"/> if the buttons may open something now, else null.</summary>
        private GameModsLine LineOf(Game game)
        {
            if (game == null)
                throw new ArgumentNullException(nameof(game));
            return CanOpen ? Snapshot.Games.FirstOrDefault(line => line.Game == game) : null;
        }

        private void RaiseChanged()
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}

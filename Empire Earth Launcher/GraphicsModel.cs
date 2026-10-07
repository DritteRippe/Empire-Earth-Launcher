using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Diagnostics;
using Empire_Earth_Launcher.Core.GameSettings;
using Empire_Earth_Launcher.Core.Graphics;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Logging;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Core.Play;

namespace Empire_Earth_Launcher
{
    /// <summary>The game window size one game of the selected installation has in the registry.</summary>
    internal sealed class GameWindowLine
    {
        public GameWindowLine(Game game, ScreenSize size)
        {
            Game = game;
            Size = size;
        }

        public Game Game { get; }

        /// <summary><see cref="ScreenSize.Empty"/> if the values are missing or damaged.</summary>
        public ScreenSize Size { get; }
    }

    /// <summary>The <c>dgVoodoo.conf</c> of one game folder, as read for the page.</summary>
    internal sealed class WrapperConfLine
    {
        public WrapperConfLine(Game game, DgVoodooConfFile file)
        {
            Game = game;
            File = file;
        }

        public Game Game { get; }

        public DgVoodooConfFile File { get; }
    }

    /// <summary>What the graphics page shows for the selected installation (<see cref="GraphicsModel"/>).</summary>
    internal sealed class GraphicsSnapshot
    {
        public GraphicsSnapshot(Installation installation, IReadOnlyList<GameWindowLine> windows,
            IReadOnlyList<ResolutionOption> options, ScreenSize screen, int scalingPercent, WrapperInfo wrapper,
            IReadOnlyList<WrapperConfLine> configs)
        {
            Installation = installation;
            Windows = windows;
            Options = options;
            Screen = screen;
            ScalingPercent = scalingPercent;
            Wrapper = wrapper;
            Configs = configs;
        }

        public Installation Installation { get; }

        /// <summary>The window size of each game of the installation.</summary>
        public IReadOnlyList<GameWindowLine> Windows { get; }

        /// <summary>The sizes the page offers (<see cref="ResolutionOptions"/>).</summary>
        public IReadOnlyList<ResolutionOption> Options { get; }

        /// <summary>The primary screen in physical pixels; empty if unknown.</summary>
        public ScreenSize Screen { get; }

        /// <summary>The scaling of the primary screen in percent (ADR 0011).</summary>
        public int ScalingPercent { get; }

        public WrapperInfo Wrapper { get; }

        /// <summary>The <c>dgVoodoo.conf</c> of each game, only for a wrapper that can be dgVoodoo; empty otherwise.</summary>
        public IReadOnlyList<WrapperConfLine> Configs { get; }

        /// <summary>True if <paramref name="size"/> is not the window size of every game of the installation yet.</summary>
        public bool Changes(ScreenSize size)
        {
            return Windows.Any(line => line.Size != size);
        }
    }

    /// <summary>
    /// The graphics page (launcher 1.1.0): the game window size of the selected installation, which the player chooses from the
    /// list of <see cref="ResolutionOptions"/> (the only thing the page writes: <c>Game Window Width</c> and <c>Game Window
    /// Height</c> through <see cref="GameDefaultsService.SetGameWindow"/>, guard and backup included), and the DirectX wrapper the
    /// setup installed with the screen mode keys of its <c>dgVoodoo.conf</c>, which are only read (ADR 0014).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Created once by <see cref="Program"/>; use it on the UI thread. The state is read on the thread pool after every search of
    /// the installations and when the page is shown (the game or a repair may have changed the values meanwhile), never while a
    /// setup runs; <see cref="Changed"/> is raised on the UI thread. Only the latest read counts.
    /// </para>
    /// <para>
    /// The model never writes a file. The wrapper is the setup's, and so is <c>dgVoodoo.conf</c> (contract 2.5, ADR 0014):
    /// editing the screen mode comes with an allow-list in a later version.
    /// </para>
    /// </remarks>
    internal sealed class GraphicsModel
    {
        private readonly GameDefaultsService defaults;
        private readonly GameSettingsModel gameSettings;
        private readonly InstallationService installations;
        private readonly SetupWatcher setupWatcher;
        private readonly ISystemInfo systemInfo;
        private readonly IFileSystem fileSystem;
        private readonly EffectivePathResolver effectivePaths;
        private readonly ILogger logger;

        /// <summary>Counts the reads, so that only the result of the latest one is used.</summary>
        private int generation;

        /// <param name="defaults">Reads and writes the game window size.</param>
        /// <param name="gameSettings">Reads its hints again after the window size changed (the Play page shows them).</param>
        /// <param name="installations">The installations; a new search result is read.</param>
        /// <param name="setupWatcher">While a setup runs, nothing is read or changed (contract 4.2).</param>
        /// <param name="systemInfo">The screen.</param>
        /// <param name="fileSystem">Reads <c>dgVoodoo.conf</c> and looks for wrapper files.</param>
        /// <param name="effectivePaths">Where the game reads <c>dgVoodoo.conf</c> (the VirtualStore copy first, ADR 0016).</param>
        /// <param name="logger">Log of the launcher.</param>
        public GraphicsModel(GameDefaultsService defaults, GameSettingsModel gameSettings, InstallationService installations,
            SetupWatcher setupWatcher, ISystemInfo systemInfo, IFileSystem fileSystem, EffectivePathResolver effectivePaths,
            ILogger logger)
        {
            this.defaults = defaults ?? throw new ArgumentNullException(nameof(defaults));
            this.gameSettings = gameSettings ?? throw new ArgumentNullException(nameof(gameSettings));
            this.installations = installations ?? throw new ArgumentNullException(nameof(installations));
            this.setupWatcher = setupWatcher ?? throw new ArgumentNullException(nameof(setupWatcher));
            this.systemInfo = systemInfo ?? throw new ArgumentNullException(nameof(systemInfo));
            this.fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            this.effectivePaths = effectivePaths ?? throw new ArgumentNullException(nameof(effectivePaths));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            installations.Changed += (sender, e) => OnInstallationsChanged();
            setupWatcher.SetupStarted += (sender, e) => RaiseChanged();
            setupWatcher.SetupFinished += (sender, e) => RaiseChanged();
        }

        /// <summary>Raised when a read or the change of the size starts or ends, and when the setup state changed.</summary>
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

        /// <summary>True while the installations are searched.</summary>
        public bool IsSearching
        {
            get { return installations.IsSearching; }
        }

        /// <summary>True while the search waits for a setup that runs (contract 4.2).</summary>
        public bool IsWaitingForSetup
        {
            get { return installations.IsWaitingForSetup; }
        }

        /// <summary>The setup that runs (nothing can be changed then, contract 4.2); null if none.</summary>
        public SetupKind RunningSetup
        {
            get { return setupWatcher.RunningSetup; }
        }

        /// <summary>The latest read; null before the first one, and while no installation is selected.</summary>
        public GraphicsSnapshot Snapshot { get; private set; }

        public bool IsReading { get; private set; }

        /// <summary>True while the size is written.</summary>
        public bool IsBusy { get; private set; }

        /// <summary>The result of the last change of the window size, else null.</summary>
        public GameSettingsResult LastResult { get; private set; }

        /// <summary>The size <see cref="LastResult"/> is about.</summary>
        public ScreenSize LastSize { get; private set; }

        /// <summary>The task of the latest read (for the tests); null before the first one.</summary>
        public Task LastRead { get; private set; }

        /// <summary>
        /// True if the size can be changed now: an installation that the launcher may write for, and no setup, search, read or
        /// change under way (a game that runs is found by the mutation guard when the player clicks).
        /// </summary>
        public bool CanChange
        {
            get
            {
                Installation selected = Selected;
                return selected != null && selected.State != InstallationState.FolderMissing && !selected.HasNewerContract &&
                       Snapshot != null && Snapshot.Installation == selected && !setupWatcher.IsSetupRunning &&
                       !installations.IsSearching && !installations.IsWaitingForSetup && !IsReading && !IsBusy;
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
            read.ContinueWith(task => logger.Error("Reading the graphics state failed.", task.Exception),
                System.Threading.CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
        }

        /// <summary>
        /// Reads the window size, the wrapper and its <c>dgVoodoo.conf</c> again. Not while a setup runs (contract 4.2: the
        /// search after the setup reads again), and not before the first search has finished.
        /// </summary>
        public Task RefreshAsync()
        {
            DiscoveryResult result = installations.Result;
            if (result == null || installations.IsSearching)
                return Task.CompletedTask;
            if (setupWatcher.IsSetupRunning || installations.IsWaitingForSetup)
            {
                logger.Info("Graphics page: no read while a setup is running; the search after the setup reads again.");
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
            GraphicsSnapshot snapshot;
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

        private GraphicsSnapshot ReadNow(Installation installation)
        {
            IReadOnlyList<Game> games = GameDefaultsService.GamesOf(installation);
            WrapperInfo wrapper = WrapperInfo.Describe(installation, fileSystem);
            // Only a wrapper that can be dgVoodoo has a dgVoodoo.conf worth showing: Native, DDrawCompat and the DirectX 9
            // wrapper are configured elsewhere or not at all.
            bool mayBeDgVoodoo = wrapper.Kind == WrapperKind.DgVoodoo || wrapper.Kind == WrapperKind.Other;
            var configs = new List<WrapperConfLine>();
            if (mayBeDgVoodoo)
            {
                foreach (Game game in games)
                {
                    string folder = installation.GetGameFolder(game);
                    if (folder != null && WinPath.IsFullyQualified(folder))
                        configs.Add(new WrapperConfLine(game, DgVoodooConfReader.Read(fileSystem, effectivePaths, folder)));
                }
                // A wrapper file without a setup record that left no dgVoodoo.conf behind: nothing to show.
                if (wrapper.Kind == WrapperKind.Other && configs.All(line => line.File.Status != ConfigFileStatus.Read))
                    configs.Clear();
            }
            return new GraphicsSnapshot(installation,
                games.Select(game => new GameWindowLine(game, defaults.ReadGameWindow(installation, game))).ToList(),
                ResolutionOptions.For(systemInfo), systemInfo.PrimaryScreen, systemInfo.ScalingPercent(), wrapper, configs);
        }

        /// <summary>
        /// Writes <paramref name="size"/> as the game window size of every game of the selected installation (guard, backup, the
        /// two values and no other), reads again, and lets the Game settings page and the info bar of the Play page read their
        /// hints again (a window that does not fit the screen is one of them).
        /// </summary>
        public async Task ApplyResolutionAsync(ScreenSize size)
        {
            Installation selected = Selected;
            if (selected == null || IsBusy)
                return;
            IsBusy = true;
            RaiseChanged();
            try
            {
                LastResult = await Task.Run(() => defaults.SetGameWindow(selected, size));
                LastSize = size;
            }
            finally
            {
                IsBusy = false;
            }
            await RefreshAsync();
            await gameSettings.RefreshAsync();
        }

        private void RaiseChanged()
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}

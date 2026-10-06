using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.GameSettings;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Logging;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Core.Play;
using Empire_Earth_Launcher.Core.Settings;

namespace Empire_Earth_Launcher
{
    /// <summary>
    /// The state and the actions of the Play page (L-WP6, ADR 0010, ARCHITECTURE 4.2): the game chosen (saved as
    /// <see cref="LauncherSettings.LastGame"/>), the file versions of the programs, whether Play is possible (an
    /// installation, no running setup), and the start and its result. The repair advice of a start opens the download page
    /// through <see cref="UpdateModel"/> (L-WP7).
    /// </summary>
    /// <remarks>
    /// Created once by <see cref="Program"/>; use it on the UI thread. The work runs on the thread pool through the core
    /// (<see cref="GameStarter.StartAsync"/>, <see cref="ProgramVersions"/>), and <see cref="Changed"/> is raised on the
    /// UI thread after it. The page starts the actions through <see cref="UiOperation"/>.
    /// </remarks>
    internal sealed class PlayModel
    {
        private readonly GameStarter starter;
        private readonly ProgramVersions programVersions;
        private readonly SetupWatcher setupWatcher;
        private readonly InstallationService installations;
        private readonly SettingsStore settings;
        private readonly GameSettingsModel gameSettings;
        private readonly ILogger logger;
        private readonly GameWindowActivator windowActivator;

        /// <summary>Cancelled when the launcher closes: ends the hand-over of the foreground to a game (A1).</summary>
        private readonly CancellationTokenSource windowHandOverCancellation = new CancellationTokenSource();

        /// <summary>Counts the reads of the versions, so that only the latest one is shown.</summary>
        private int versionsGeneration;

        /// <param name="starter">Starts the games (contract 3.6, 3.7, 4.2).</param>
        /// <param name="programVersions">Reads the file versions of the programs.</param>
        /// <param name="setupWatcher">Tells whether a setup runs (contract 4.2).</param>
        /// <param name="installations">The selected installation.</param>
        /// <param name="settings">settings.json, for the last game.</param>
        /// <param name="gameSettings">Takes the display question of a first run before Play and shows the new defaults state.</param>
        /// <param name="logger">Log of the launcher.</param>
        /// <param name="windowActivator">Hands the foreground to the window of a started game; null in tests that do not look at it.</param>
        public PlayModel(GameStarter starter, ProgramVersions programVersions, SetupWatcher setupWatcher,
            InstallationService installations, SettingsStore settings, GameSettingsModel gameSettings, ILogger logger,
            GameWindowActivator windowActivator = null)
        {
            this.windowActivator = windowActivator;
            this.starter = starter ?? throw new ArgumentNullException(nameof(starter));
            this.programVersions = programVersions ?? throw new ArgumentNullException(nameof(programVersions));
            this.setupWatcher = setupWatcher ?? throw new ArgumentNullException(nameof(setupWatcher));
            this.installations = installations ?? throw new ArgumentNullException(nameof(installations));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.gameSettings = gameSettings ?? throw new ArgumentNullException(nameof(gameSettings));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            Versions = new ProgramVersion[0];
            installations.Changed += (sender, e) => RaiseChanged();
            setupWatcher.SetupStarted += (sender, e) => RaiseChanged();
            setupWatcher.SetupFinished += (sender, e) => RaiseChanged();
        }

        /// <summary>Raised when the state changed (installation, setup, versions, game, start).</summary>
        public event EventHandler Changed;

        /// <summary>The selected installation; null while none is known or none was found.</summary>
        public Installation Selected
        {
            get { return installations.Selected; }
        }

        /// <summary>True until the first search has a result.</summary>
        public bool IsSearching
        {
            get { return installations.Result == null; }
        }

        /// <summary>The setup that runs (contract 4.2, the suite's too), else null.</summary>
        public SetupKind RunningSetup
        {
            get { return setupWatcher.RunningSetup; }
        }

        /// <summary>The programs of the selected installation with their file versions; empty without one.</summary>
        public IReadOnlyList<ProgramVersion> Versions { get; private set; }

        /// <summary>True while a start runs.</summary>
        public bool IsStarting { get; private set; }

        /// <summary>The result of the last start, else null.</summary>
        public StartResult LastResult { get; private set; }

        /// <summary>The hand-over of the foreground to the game of the last start (it runs in the background); null before one.</summary>
        internal Task<ActivationOutcome> WindowHandOver { get; private set; }

        /// <summary>Ends a running hand-over of the foreground: the launcher is closing.</summary>
        public void CancelWindowHandOver()
        {
            windowHandOverCancellation.Cancel();
        }

        /// <summary>
        /// The game Play starts: the last game of settings.json; The Art of Conquest only if the selected installation has
        /// an AoC folder, else Empire Earth.
        /// </summary>
        public Game SelectedGame
        {
            get
            {
                Game game = Game.FromId(settings.Current.LastGame);
                return game == Game.ArtOfConquest && Selected?.HasArtOfConquest == true ? game : Game.EmpireEarth;
            }
        }

        /// <summary>True if The Art of Conquest can be chosen: the selected installation has an AoC folder.</summary>
        public bool CanChooseArtOfConquest
        {
            get { return Selected?.HasArtOfConquest == true; }
        }

        /// <summary>
        /// True if Play is possible: an installation whose folder exists, no running setup (contract 4.2), no start running.
        /// A missing program does not disable Play: the start then gives the repair advice.
        /// </summary>
        public bool CanPlay
        {
            get
            {
                return Selected != null && Selected.State != InstallationState.FolderMissing && !setupWatcher.IsSetupRunning &&
                       !installations.IsWaitingForSetup && !IsStarting;
            }
        }

        /// <summary>Chooses the game for Play and saves it as the last game in settings.json.</summary>
        public void SelectGame(Game game)
        {
            if (game == null)
                throw new ArgumentNullException(nameof(game));
            if (game == Game.ArtOfConquest && !CanChooseArtOfConquest)
                throw new InvalidOperationException("The selected installation has no folder of The Art of Conquest.");
            if (string.Equals(settings.Current.LastGame, game.Id, StringComparison.Ordinal))
                return;
            settings.Current.LastGame = game.Id;
            // If saving fails (logged by the store), the choice is still used for this session.
            settings.Save();
            RaiseChanged();
        }

        /// <summary>Reads the file versions of the programs of the selected installation again.</summary>
        public async Task RefreshVersionsAsync()
        {
            int generation = ++versionsGeneration;
            Installation selected = Selected;
            IReadOnlyList<ProgramVersion> versions = selected == null
                ? new ProgramVersion[0]
                : await Task.Run(() => programVersions.Read(selected));
            if (generation != versionsGeneration)
                return; // a later read (another installation) has started
            Versions = versions;
            if (selected != null)
                logger.Info("Program versions of " + selected.Root + ": " + string.Join(", ", versions) + ".");
            RaiseChanged();
        }

        /// <summary>
        /// Starts <see cref="SelectedGame"/> of the selected installation (ARCHITECTURE 4.2). A display question of a first
        /// run goes to the info bar of the game settings; it never blocks the start.
        /// </summary>
        /// <param name="startEvenIfOtherGameRuns">The player confirmed that the other game runs.</param>
        public async Task<StartResult> StartAsync(bool startEvenIfOtherGameRuns)
        {
            Installation selected = Selected ?? throw new InvalidOperationException("No installation is selected.");
            Game game = SelectedGame;
            IsStarting = true;
            RaiseChanged();
            StartResult result;
            try
            {
                result = await starter.StartAsync(selected, game, startEvenIfOtherGameRuns);
            }
            finally
            {
                IsStarting = false;
            }

            LastResult = result;
            // The game gets the foreground as soon as it shows its window; the page does not wait for it.
            if (result.IsStarted && windowActivator != null)
                WindowHandOver = windowActivator.ActivateAsync(result.ProcessId, result.Game, windowHandOverCancellation.Token);
            if (result.Question != null)
                gameSettings.AddQuestion(result.Question);
            if (result.Defaults.HasValue && result.Defaults.Value != DefaultsAtStart.None)
                await gameSettings.RefreshAsync();
            if (result.Outcome == StartOutcome.Damaged)
                await RefreshVersionsAsync();
            RaiseChanged();
            return result;
        }

        private void RaiseChanged()
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}

using Krypton.Toolkit;
using System;
using System.Windows.Forms;
using Empire_Earth_Launcher.Core.Lobby;
using Empire_Earth_Launcher.Core.Logging;
using Empire_Earth_Launcher.Core.Play;
using Empire_Earth_Launcher.Core.Settings;
using Empire_Earth_Launcher.Properties;

namespace Empire_Earth_Launcher
{
    public partial class MainForm : KryptonForm
    {
        /// <summary>How often the timer asks the setup watcher; it probes every two seconds (ADR 0010).</summary>
        private const int SetupWatcherTickMilliseconds = 500;

        /// <summary>Buttons of the navigation bar; the Tag of each one is its page (or null).</summary>
        private readonly KryptonCheckButton[] navigationButtons;

        private readonly SetupWatcher setupWatcher;

        /// <summary>Ticks <see cref="setupWatcher"/> on the UI thread while the window is open (contract 4.2).</summary>
        private readonly Timer setupWatcherTimer = new Timer { Interval = SetupWatcherTickMilliseconds };

        /// <param name="logger">Log of the launcher.</param>
        /// <param name="themeService">Theme of the launcher windows.</param>
        /// <param name="settings">User settings of the launcher (settings.json).</param>
        /// <param name="installations">The installations of Empire Earth and the selected one.</param>
        /// <param name="lobbyProfiles">Reads the lobby profiles of the game folder.</param>
        /// <param name="gameSettings">The game settings of the selected installation (L-WP5).</param>
        /// <param name="play">The Play page: game choice, versions, start (L-WP6).</param>
        /// <param name="integrity">The integrity check of the selected installation (L-WP7).</param>
        /// <param name="updates">The update API: version check and the download of the repair advice (L-WP7).</param>
        /// <param name="maintenance">The maintenance tools of the Tools page (L-WP8).</param>
        /// <param name="diagnostics">The network diagnostics and the diagnostics report of the Tools page (L-WP9).</param>
        /// <param name="setupWatcher">Watches the setup mutexes; the window ticks it every half second (contract 4.2).</param>
        /// <param name="uiOperation">Runs the asynchronous work of the pages (ADR 0004).</param>
        /// <param name="playerList">Polls the online player list; null if the server settings are invalid.</param>
        internal MainForm(ILogger logger, IThemeService themeService, SettingsStore settings,
            InstallationService installations, LobbyProfileRepository lobbyProfiles, GameSettingsModel gameSettings,
            PlayModel play, IntegrityModel integrity, UpdateModel updates, MaintenanceModel maintenance, DiagnosticsModel diagnostics,
            SetupWatcher setupWatcher, UiOperation uiOperation, PlayerListPolling playerList)
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer, true);
            InitializeComponent();
            ApplyTexts();
            themeService.Register(launcherKryptonPalette, this);

            // The pages are created by InitializeComponent (designer), which needs parameterless constructors,
            // so they receive their services here.
            generalUserControl.Initialize(logger, themeService, installations, lobbyProfiles, playerList, gameSettings, play,
                integrity, updates, uiOperation);
            settingsUserControl.Initialize(themeService, gameSettings, installations, setupWatcher, uiOperation);
            toolsUserControl.Initialize(themeService, integrity, updates, setupWatcher, maintenance, diagnostics, uiOperation);
            launcherSettingsUserControl.Initialize(themeService, settings, installations, uiOperation);

            // A page cannot be assigned to Tag in the designer, so the navigation is wired up here.
            playKryptonCheckButton.Tag = generalUserControl;
            settingsKryptonCheckButton.Tag = settingsUserControl;
            toolsKryptonCheckButton.Tag = toolsUserControl;
            launcherKryptonCheckButton.Tag = launcherSettingsUserControl;
            navigationButtons = new[]
                { playKryptonCheckButton, settingsKryptonCheckButton, toolsKryptonCheckButton, launcherKryptonCheckButton };
            generalUserControl.GameSettingsRequested += (sender, e) =>
                navigationKryptonCheckButton_Click(settingsKryptonCheckButton, EventArgs.Empty);
            generalUserControl.ToolsRequested += (sender, e) =>
                navigationKryptonCheckButton_Click(toolsKryptonCheckButton, EventArgs.Empty);
            generalUserControl.NetworkCheckRequested += (sender, e) =>
            {
                navigationKryptonCheckButton_Click(toolsKryptonCheckButton, EventArgs.Empty);
                toolsUserControl.ShowNetworkCheck();
            };

            this.setupWatcher = setupWatcher ?? throw new ArgumentNullException(nameof(setupWatcher));
            setupWatcherTimer.Tick += (sender, e) => this.setupWatcher.Tick();
            if (integrity == null)
                throw new ArgumentNullException(nameof(integrity));
            // Closing the window ends a running check and the hand-over of the foreground to a game just started (ADR 0004:
            // closing cancels the operations of the window).
            FormClosed += (sender, e) =>
            {
                setupWatcherTimer.Dispose();
                integrity.CancelCheck();
                play.CancelWindowHandOver();
            };
        }

        /// <summary>
        /// Sets the window title and the navigation texts from the resources in the UI language (ADR 0009). The texts
        /// in MainForm.resx are placeholders for the designer.
        /// </summary>
        private void ApplyTexts()
        {
            Text = Resources.LauncherTitle;
            playKryptonCheckButton.Values.Text = Resources.NavigationPlay;
            settingsKryptonCheckButton.Values.Text = Resources.NavigationSettings;
            toolsKryptonCheckButton.Values.Text = Resources.NavigationTools;
            launcherKryptonCheckButton.Values.Text = Resources.NavigationLauncher;
        }

        /// <summary>
        /// The window can be resized freely but never below the size it opens with (ADR 0017): the pages are laid out for the
        /// width they get and need that much room at least.
        /// </summary>
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            MinimumSize = Size;
        }

        /// <summary>
        /// Starts the discovery of the installations once the window is on the screen: it runs in the background and the
        /// pages show "searching" until it has finished, so nothing delays the window (ADR 0004).
        /// </summary>
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            // The first probe of the setup mutexes runs with the first search (InstallationService), then every 2 s.
            setupWatcherTimer.Start();
            launcherSettingsUserControl.StartDiscovery();
        }

        /// <summary>
        /// Shared Click handler of the navigation buttons, which behave like radio buttons: the clicked button
        /// stays checked (a second click does not uncheck it), all others are unchecked, and only the page in
        /// the clicked button's Tag is visible.
        /// </summary>
        private void navigationKryptonCheckButton_Click(object sender, EventArgs e)
        {
            var selectedButton = (KryptonCheckButton)sender;
            foreach (KryptonCheckButton button in navigationButtons)
            {
                bool selected = button == selectedButton;
                button.Checked = selected;
                if (button.Tag is Control page)
                    page.Visible = selected;
            }
        }
    }
}

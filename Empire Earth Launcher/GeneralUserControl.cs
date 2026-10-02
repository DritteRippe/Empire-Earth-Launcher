using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Forms;
using Empire_Earth_Launcher.Core.GameSettings;
using Empire_Earth_Launcher.Core.Lobby;
using Empire_Earth_Launcher.Core.Logging;
using Empire_Earth_Launcher.Properties;
using Empire_Earth_WON;

namespace Empire_Earth_Launcher
{
    public partial class GeneralUserControl : UserControl
    {
        private ILogger logger;
        private InstallationService installations;
        private GameSettingsModel gameSettings;
        private UiOperation uiOperation;

        /// <summary>The hint the info bar shows; null while it shows the display question or nothing.</summary>
        private ConsistencyFinding shownFinding;

        /// <summary>Polls the online player list (ADR 0004); null if the server settings are invalid.</summary>
        private PlayerListPoller playerList;
        private LobbyProfileRepository lobbyProfiles;

        /// <summary>Profiles shown in the user list, in the same order.</summary>
        private IList<LobbyPersistentData.LobbyGlobalData.PlayerInfoGlobalData> profiles =
            new LobbyPersistentData.LobbyGlobalData.PlayerInfoGlobalData[0];

        public GeneralUserControl()
        {
            InitializeComponent();
            ApplyTexts();

            // No file or network I/O here: the constructor also runs inside the Visual Studio designer and
            // during MainForm.InitializeComponent, where an exception would prevent the launcher from starting.
            // Loading happens in OnLoad.
        }

        /// <summary>
        /// Sets the texts of the page from the resources in the UI language (ADR 0009); the designer texts are
        /// placeholders. The heading of the player list shows its state and is changed when the state changes.
        /// </summary>
        private void ApplyTexts()
        {
            gameSettingsKryptonGroupBox.Values.Heading = Resources.GameChoiceHeading;
            empireEarthKryptonRadioButton.Values.Text = Resources.GameEmpireEarth;
            artOfConquestKryptonRadioButton.Values.Text = Resources.GameArtOfConquest;
            playKryptonButton.Values.Text = Resources.PlayButton;
            neoOnlineKryptonGroupBox.Values.Heading = Resources.OnlinePlayersLoading;
            lobbyUserKryptonLabel.Values.Text = Resources.LobbyProfileLabel;
            usernameColumn.HeaderText = Resources.PlayerListNameColumn;
            stateColumn.HeaderText = Resources.PlayerListStateColumn;
        }

        /// <summary>
        /// Passes the services in. The control is created by the designer, which needs a parameterless
        /// constructor, so its owner calls this right after InitializeComponent, before the control is loaded.
        /// </summary>
        /// <param name="logger">Log of the launcher.</param>
        /// <param name="themeService">Theme of the launcher.</param>
        /// <param name="installations">The installations; the lobby files are read from the EE folder of the selected one,
        /// again when it changes.</param>
        /// <param name="lobbyProfiles">Reads the lobby profiles of the game folder (VirtualStore copy first).</param>
        /// <param name="playerList">Polls the online player list, started when the page loads and ended with it; null
        /// disables the list (invalid server settings).</param>
        /// <param name="gameSettings">The game settings: the display question and the hints of the info bar (L-WP5).</param>
        /// <param name="uiOperation">Runs the answer to the display question (ADR 0004).</param>
        internal void Initialize(ILogger logger, IThemeService themeService, InstallationService installations,
            LobbyProfileRepository lobbyProfiles, PlayerListPoller playerList, GameSettingsModel gameSettings,
            UiOperation uiOperation)
        {
            if (logger == null)
                throw new ArgumentNullException(nameof(logger));
            if (themeService == null)
                throw new ArgumentNullException(nameof(themeService));
            if (installations == null)
                throw new ArgumentNullException(nameof(installations));

            this.logger = logger;
            this.lobbyProfiles = lobbyProfiles ?? throw new ArgumentNullException(nameof(lobbyProfiles));
            this.installations = installations;
            this.playerList = playerList;
            themeService.Register(launcherKryptonPalette, this);

            EventHandler reloadLobbyProfiles = (sender, e) => OnInstallationsChanged();
            installations.Changed += reloadLobbyProfiles;
            Disposed += (sender, e) => installations.Changed -= reloadLobbyProfiles;

            this.gameSettings = gameSettings ?? throw new ArgumentNullException(nameof(gameSettings));
            this.uiOperation = uiOperation ?? throw new ArgumentNullException(nameof(uiOperation));
            EventHandler showHint = (sender, e) => ShowGameSettingsHint();
            gameSettings.Changed += showHint;
            Disposed += (sender, e) => gameSettings.Changed -= showHint;
            ShowGameSettingsHint();
        }

        /// <summary>Raised when the player wants to see the Game settings page (button "Details" of the info bar).</summary>
        internal event EventHandler GameSettingsRequested;

        /// <summary>
        /// The non-modal info bar of the game settings (contract 3.6, ADR 0015): the display question of the first run, else
        /// the first hint the player did not hide, with "Hide" and "Details"; nothing if there is neither.
        /// </summary>
        private void ShowGameSettingsHint()
        {
            shownFinding = null;
            if (gameSettings.Question != null)
            {
                gameSettingsHintKryptonWrapLabel.Text = Texts.DisplayQuestion(gameSettings.Question);
                gameSettingsHintFirstKryptonButton.Values.Text = Resources.DisplayQuestionApply;
                gameSettingsHintSecondKryptonButton.Values.Text = Resources.DisplayQuestionKeep;
                gameSettingsHintKryptonPanel.Visible = true;
                return;
            }

            IReadOnlyList<ConsistencyFinding> visible = gameSettings.VisibleFindings;
            if (visible.Count == 0)
            {
                gameSettingsHintKryptonPanel.Visible = false;
                return;
            }
            shownFinding = visible[0];
            gameSettingsHintKryptonWrapLabel.Text = Texts.Finding(shownFinding) + (visible.Count > 1
                ? Environment.NewLine + string.Format(CultureInfo.CurrentCulture, Resources.HintBarMoreFormat, visible.Count - 1)
                : string.Empty);
            gameSettingsHintFirstKryptonButton.Values.Text = Resources.HintBarHide;
            gameSettingsHintSecondKryptonButton.Values.Text = Resources.HintBarDetails;
            gameSettingsHintKryptonPanel.Visible = true;
        }

        private void gameSettingsHintFirstKryptonButton_Click(object sender, EventArgs e)
        {
            if (shownFinding != null)
                gameSettings.SetHidden(shownFinding, true);
            else if (gameSettings.Question != null)
                uiOperation.Run(gameSettingsHintFirstKryptonButton, () => gameSettings.AnswerQuestionAsync(true));
        }

        private void gameSettingsHintSecondKryptonButton_Click(object sender, EventArgs e)
        {
            if (shownFinding != null)
                GameSettingsRequested?.Invoke(this, EventArgs.Empty);
            else if (gameSettings.Question != null)
                uiOperation.Run(gameSettingsHintSecondKryptonButton, () => gameSettings.AnswerQuestionAsync(false));
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            // Not initialized in the designer.
            if (DesignMode || logger == null)
                return;

            LoadLobbyProfiles();
            StartPlayerListPolling();
        }

        /// <summary>
        /// Starts the polling of the online player list (no request before the page loads); it ends with the page. The poller
        /// logs an outage once and the return of the list once (ADR 0004).
        /// </summary>
        private void StartPlayerListPolling()
        {
            if (playerList == null)
            {
                neoOnlineKryptonGroupBox.Values.Heading = Resources.OnlinePlayersInvalidSettings;
                return;
            }
            neoOnlineKryptonGroupBox.Values.Heading = Resources.OnlinePlayersLoading;
            playerList.Updated += OnPlayerListUpdated;
            Disposed += (sender, e) => playerList.Dispose();
            playerList.Start();
        }

        /// <summary>
        /// Reads the lobby profiles again when a discovery has finished; while one runs, the profiles of the previous
        /// result stay (the first time: "searching").
        /// </summary>
        private void OnInstallationsChanged()
        {
            if (installations.IsSearching && installations.Result != null)
                return;
            LoadLobbyProfiles();
        }

        /// <summary>
        /// Fills the user combo box from the lobby profiles of the game. A missing or unreadable file is not
        /// fatal: <see cref="LobbyProfileRepository"/> logs it, it is shown in the UI, and the rest of the
        /// launcher keeps working.
        /// </summary>
        private void LoadLobbyProfiles()
        {
            usersLobbyKryptonComboBox.Items.Clear();
            usersLobbyKryptonComboBox.Enabled = true;
            neoOnlineKryptonGroupBox.Values.Description = string.Empty;
            profiles = new LobbyPersistentData.LobbyGlobalData.PlayerInfoGlobalData[0];
            if (installations.Result == null)
            {
                ShowLobbyProfilesUnavailable(Resources.InstallationsSearching);
                return;
            }

            // Both lobby files come from the EE folder of the selected installation (the profile list used to be read
            // relative to the current directory and the user files from a hard-coded installation path).
            LobbyProfilesStatus status = lobbyProfiles.LoadProfiles(installations.Selected?.EeFolder, out profiles);
            if (status != LobbyProfilesStatus.Loaded)
            {
                ShowLobbyProfilesUnavailable(Texts.LobbyProfilesProblem(status));
                return;
            }

            foreach (var profile in profiles)
                usersLobbyKryptonComboBox.Items.Add(profile.Username);
            usersLobbyKryptonComboBox.SelectedIndex = 0;
        }

        private void ShowLobbyProfilesUnavailable(string reason)
        {
            usersLobbyKryptonComboBox.Enabled = false;
            neoOnlineKryptonGroupBox.Values.Description = reason;
        }

        /// <summary>A result of the player list, on the UI thread (the poller was started there).</summary>
        private void OnPlayerListUpdated(object sender, PlayerListUpdate update)
        {
            if (IsDisposed)
                return;

            switch (update.Status)
            {
                case PlayerListStatus.Available:
                    ShowOnlinePlayers(update.Message);
                    break;
                case PlayerListStatus.Unavailable:
                    ShowPlayerListUnavailable();
                    break;
                default:
                    // The polling ended by an error of its own (logged by the poller).
                    onlinePlayersKryptonDataGridView.Rows.Clear();
                    neoOnlineKryptonGroupBox.Values.Heading = Resources.OnlinePlayersUnavailableSeeLog;
                    break;
            }
        }

        private void ShowOnlinePlayers(NeoApiClient.ConnectedPlayersMessage message)
        {
            onlinePlayersKryptonDataGridView.Rows.Clear();

            neoOnlineKryptonGroupBox.Values.Heading =
                string.Format(CultureInfo.CurrentCulture, Resources.OnlinePlayersFormat, message.OnlinePlayers);

            foreach (NeoApiClient.ConnectedPlayersMessage.PlayerInfo pInfo in message.PlayersInfo)
            {
                if (!usersLobbyKryptonComboBox.Text.Equals(pInfo.Name, StringComparison.InvariantCultureIgnoreCase))
                    onlinePlayersKryptonDataGridView.Rows.Add(pInfo.Name, Texts.PlayerGameState(pInfo.GameState));
            }
        }

        private void ShowPlayerListUnavailable()
        {
            onlinePlayersKryptonDataGridView.Rows.Clear();
            neoOnlineKryptonGroupBox.Values.Heading = Resources.OnlinePlayersUnavailable;
        }

        private void usersLobbyKryptonComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            int index = usersLobbyKryptonComboBox.SelectedIndex;
            string gameFolder = installations.Selected?.EeFolder;
            if (index < 0 || index >= profiles.Count || gameFolder == null)
                return;

            IDictionary<string, uint> friends;
            LobbyFriendsStatus status = lobbyProfiles.LoadFriends(gameFolder, profiles[index], out friends);
            neoOnlineKryptonGroupBox.Values.Description = Texts.LobbyFriends(status, friends?.Count ?? 0);
        }
    }
}

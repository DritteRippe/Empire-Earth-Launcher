using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Threading;
using System.Windows.Forms;
using Empire_Earth_Launcher.Properties;
using Empire_Earth_WON;

namespace Empire_Earth_Launcher
{
    public partial class GeneralUserControl : UserControl
    {
        /// <summary>Granularity of the poll delay, so that cancelling the worker does not wait for a whole interval.</summary>
        private const int CancellationCheckMilliseconds = 100;

        private BackgroundWorker backgroundWorker;
        private ILogger logger;
        private GameDirectoryService gameDirectory;
        private NeoApiClient neoClient;

        /// <summary>Delay between two requests of the online player list.</summary>
        private int playerListPollIntervalMilliseconds;
        private LobbyProfileRepository lobbyProfiles;

        /// <summary>Profiles shown in the user list, in the same order.</summary>
        private IList<LobbyPersistentData.LobbyGlobalData.PlayerInfoGlobalData> profiles =
            new LobbyPersistentData.LobbyGlobalData.PlayerInfoGlobalData[0];

        /// <summary>
        /// True while the player list cannot be fetched, so that an outage is logged once and not every poll.
        /// </summary>
        private bool playerListUnavailable;

        public GeneralUserControl()
        {
            InitializeComponent();

            // No file or network I/O here: the constructor also runs inside the Visual Studio designer and
            // during MainForm.InitializeComponent, where an exception would prevent the launcher from starting.
            // Loading happens in OnLoad.
            backgroundWorker = new BackgroundWorker()
            {
                WorkerReportsProgress = true,
                WorkerSupportsCancellation = true
            };
            backgroundWorker.DoWork += backgroundWorker_DoWork;
            backgroundWorker.ProgressChanged += backgroundWorker_ProgressChanged;
            backgroundWorker.RunWorkerCompleted += backgroundWorker_RunWorkerCompleted;
            Disposed += (sender, e) => backgroundWorker.CancelAsync();
        }

        /// <summary>
        /// Passes the services in. The control is created by the designer, which needs a parameterless
        /// constructor, so its owner calls this right after InitializeComponent, before the control is loaded.
        /// </summary>
        /// <param name="logger">Log of the launcher.</param>
        /// <param name="themeService">Theme of the launcher.</param>
        /// <param name="gameDirectory">Game folder with the WON lobby files; they are read again when it changes.</param>
        /// <param name="neoClient">Client for the online player list; null disables the list (invalid server
        /// settings).</param>
        /// <param name="playerListPollIntervalMilliseconds">Delay between two requests of the player list.</param>
        internal void Initialize(ILogger logger, IThemeService themeService, GameDirectoryService gameDirectory,
            NeoApiClient neoClient, int playerListPollIntervalMilliseconds)
        {
            if (logger == null)
                throw new ArgumentNullException(nameof(logger));
            if (themeService == null)
                throw new ArgumentNullException(nameof(themeService));
            if (gameDirectory == null)
                throw new ArgumentNullException(nameof(gameDirectory));
            if (neoClient != null && playerListPollIntervalMilliseconds <= 0)
                throw new ArgumentOutOfRangeException(nameof(playerListPollIntervalMilliseconds));

            this.logger = logger;
            lobbyProfiles = new LobbyProfileRepository(logger);
            this.gameDirectory = gameDirectory;
            this.neoClient = neoClient;
            this.playerListPollIntervalMilliseconds = playerListPollIntervalMilliseconds;
            themeService.Register(launcherKryptonPalette, this);

            EventHandler reloadLobbyProfiles = (sender, e) => LoadLobbyProfiles();
            gameDirectory.Changed += reloadLobbyProfiles;
            Disposed += (sender, e) => gameDirectory.Changed -= reloadLobbyProfiles;
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

        private void StartPlayerListPolling()
        {
            if (neoClient == null)
            {
                neoOnlineKryptonGroupBox.Values.Heading = Resources.OnlinePlayersInvalidSettings;
                return;
            }
            neoOnlineKryptonGroupBox.Values.Heading = Resources.OnlinePlayersLoading;
            backgroundWorker.RunWorkerAsync();
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

            // Both lobby files come from the same game folder (the profile list used to be read relative to the
            // current directory and the user files from a hard-coded installation path).
            switch (lobbyProfiles.LoadProfiles(gameDirectory.Location, out profiles))
            {
                case LobbyProfilesStatus.Loaded:
                    foreach (var profile in profiles)
                        usersLobbyKryptonComboBox.Items.Add(profile.Username);
                    usersLobbyKryptonComboBox.SelectedIndex = 0;
                    break;
                case LobbyProfilesStatus.GameDirectoryNotFound:
                    ShowLobbyProfilesUnavailable(Resources.GameDirectoryNotFound);
                    break;
                case LobbyProfilesStatus.Unreadable:
                    ShowLobbyProfilesUnavailable(Resources.LobbyProfilesUnreadable);
                    break;
                default:
                    ShowLobbyProfilesUnavailable(Resources.NoLobbyProfileFound);
                    break;
            }
        }

        private void ShowLobbyProfilesUnavailable(string reason)
        {
            usersLobbyKryptonComboBox.Enabled = false;
            neoOnlineKryptonGroupBox.Values.Description = reason;
        }

        private void backgroundWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            if (IsDisposed)
                return;

            if (e.UserState is NeoApiClient.ConnectedPlayersMessage message)
                ShowOnlinePlayers(message);
            else
                ShowPlayerListUnavailable(e.UserState as Exception);
        }

        private void ShowOnlinePlayers(NeoApiClient.ConnectedPlayersMessage message)
        {
            if (playerListUnavailable)
            {
                logger.Info("The online player list is available again.");
                playerListUnavailable = false;
            }

            onlinePlayersKryptonDataGridView.Rows.Clear();

            neoOnlineKryptonGroupBox.Values.Heading =
                string.Format(CultureInfo.CurrentCulture, Resources.OnlinePlayersFormat, message.OnlinePlayers);

            foreach (NeoApiClient.ConnectedPlayersMessage.PlayerInfo pInfo in message.PlayersInfo)
            {
                if (!usersLobbyKryptonComboBox.Text.Equals(pInfo.Name, StringComparison.InvariantCultureIgnoreCase))
                    onlinePlayersKryptonDataGridView.Rows.Add(pInfo.Name, GetGameStateText(pInfo.GameState));
            }
        }

        /// <summary>
        /// Display text of a player's game state, with a fallback for states the launcher does not know.
        /// </summary>
        private static string GetGameStateText(NeoApiClient.ConnectedPlayersMessage.PlayerInfo.PlayerGameState gameState)
        {
            switch (gameState)
            {
                case NeoApiClient.ConnectedPlayersMessage.PlayerInfo.PlayerGameState.Lobby:
                    return Resources.PlayerStateLobby;
                case NeoApiClient.ConnectedPlayersMessage.PlayerInfo.PlayerGameState.Room:
                    return Resources.PlayerStateRoom;
                case NeoApiClient.ConnectedPlayersMessage.PlayerInfo.PlayerGameState.Playing:
                    return Resources.PlayerStatePlaying;
                default:
                    return string.Format(CultureInfo.CurrentCulture, Resources.PlayerStateUnknownFormat, (int)gameState);
            }
        }

        private void ShowPlayerListUnavailable(Exception error)
        {
            if (!playerListUnavailable)
            {
                logger.Error("The online player list of " + neoClient.Endpoint + " is unavailable, retrying every " +
                             playerListPollIntervalMilliseconds + " ms.", error);
                playerListUnavailable = true;
            }

            onlinePlayersKryptonDataGridView.Rows.Clear();
            neoOnlineKryptonGroupBox.Values.Heading = Resources.OnlinePlayersUnavailable;
        }

        private void backgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker worker = (BackgroundWorker)sender;
            while (!worker.CancellationPending)
            {
                object result = RequestConnectedPlayers();
                // A request can take several seconds; the control may have been disposed meanwhile.
                if (worker.CancellationPending)
                    break;
                worker.ReportProgress(0, result);

                for (int waited = 0;
                     waited < playerListPollIntervalMilliseconds && !worker.CancellationPending;
                     waited += CancellationCheckMilliseconds)
                {
                    Thread.Sleep(CancellationCheckMilliseconds);
                }
            }
            e.Cancel = true;
        }

        /// <summary>
        /// Runs on the worker thread. Returns the message on success, otherwise the error, so that one
        /// failed request never ends the polling.
        /// </summary>
        private object RequestConnectedPlayers()
        {
            try
            {
                NeoApiClient.ConnectedPlayersMessage message;
                Exception error;
                return neoClient.TryGetConnectedPlayers(out message, out error) ? (object)message : error;
            }
            catch (Exception ex)
            {
                // TryGetConnectedPlayers already turns network and protocol errors into a result; anything else is a
                // bug, but it must not end the polling either.
                return ex;
            }
        }

        private void backgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            // DoWork catches the errors of each request, so this only reports bugs in the polling loop itself
            // instead of letting the BackgroundWorker swallow them.
            if (e.Error == null)
                return;

            logger.Error("The online player list polling stopped unexpectedly.", e.Error);
            if (!IsDisposed)
                neoOnlineKryptonGroupBox.Values.Heading = Resources.OnlinePlayersUnavailableSeeLog;
        }

        private void usersLobbyKryptonComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            int index = usersLobbyKryptonComboBox.SelectedIndex;
            if (index < 0 || index >= profiles.Count || gameDirectory.Location == null)
                return;

            IDictionary<string, uint> friends;
            switch (lobbyProfiles.LoadFriends(gameDirectory.Location, profiles[index], out friends))
            {
                case LobbyFriendsStatus.Loaded:
                    neoOnlineKryptonGroupBox.Values.Description =
                        string.Format(CultureInfo.CurrentCulture, Resources.FriendsFormat, friends.Count);
                    break;
                case LobbyFriendsStatus.Unreadable:
                    neoOnlineKryptonGroupBox.Values.Description = Resources.FriendsUnreadable;
                    break;
                default:
                    neoOnlineKryptonGroupBox.Values.Description = string.Empty;
                    break;
            }
        }
    }
}

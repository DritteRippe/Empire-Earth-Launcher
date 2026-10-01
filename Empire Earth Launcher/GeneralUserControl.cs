using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Empire_Earth_Launcher.Properties;
using Empire_Earth_WON;

namespace Empire_Earth_Launcher
{
    public partial class GeneralUserControl : UserControl
    {
        private const string LobbyGlobalDataFile = "./_wonlobbypersistent.dat";

        /// <summary>Granularity of the poll delay, so that cancelling the worker does not wait for a whole interval.</summary>
        private const int CancellationCheckMilliseconds = 100;

        private BackgroundWorker backgroundWorker;
        private NeoApiClient neoClient;

        /// <summary>Delay between two requests of the online player list (from the settings).</summary>
        private int playerListPollIntervalMilliseconds;
        private LobbyPersistentData.LobbyGlobalData lobbyGlobalData;
        private LobbyPersistentData.LobbyUserData lobbyUserData;

        /// <summary>
        /// True while the player list cannot be fetched, so that an outage is logged once and not every poll.
        /// </summary>
        private bool playerListUnavailable;

        public GeneralUserControl()
        {
            InitializeComponent();
            Program.LauncherKryptonTheme.AddPalette(launcherKryptonPalette, this);

            // No file or network I/O here: the constructor also runs inside the Visual Studio designer and
            // during Form1.InitializeComponent, where an exception would prevent the launcher from starting.
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

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            if (DesignMode)
                return;

            LoadLobbyProfiles();
            StartPlayerListPolling();
        }

        /// <summary>
        /// Starts polling the online player list from the Neo server configured in the application settings
        /// (NeoServerHost, NeoServerPort, NeoTimeoutMilliseconds, PlayerListPollIntervalMilliseconds in
        /// "Empire Earth Launcher.exe.config"). Invalid settings are logged and shown instead of crashing.
        /// </summary>
        private void StartPlayerListPolling()
        {
            Settings settings = Settings.Default;
            try
            {
                if (settings.PlayerListPollIntervalMilliseconds <= 0)
                    throw new ArgumentOutOfRangeException(nameof(settings.PlayerListPollIntervalMilliseconds),
                        settings.PlayerListPollIntervalMilliseconds, "The poll interval must be positive.");
                neoClient = new NeoApiClient(new NeoServerEndpoint(settings.NeoServerHost, settings.NeoServerPort,
                    settings.NeoTimeoutMilliseconds));
            }
            catch (ArgumentException ex)
            {
                Program.Logging.Log("The Neo server settings are invalid, the online player list is disabled.", ex);
                neoOnlineKryptonGroupBox.Values.Heading = "Online Players (invalid server settings, see log.txt)";
                return;
            }

            playerListPollIntervalMilliseconds = settings.PlayerListPollIntervalMilliseconds;
            backgroundWorker.RunWorkerAsync();
        }

        /// <summary>
        /// Fills the user combo box from the lobby profiles of the game. A missing or unreadable file is not
        /// fatal: it is logged and shown in the UI, and the rest of the launcher keeps working.
        /// </summary>
        private void LoadLobbyProfiles()
        {
            usersLobbyKryptonComboBox.Items.Clear();
            lobbyGlobalData = null;

            if (!File.Exists(LobbyGlobalDataFile))
            {
                Program.Logging.Log("No lobby profiles found (" + Path.GetFullPath(LobbyGlobalDataFile) + " does not exist).", Logging.LogLevel.Warning);
                ShowLobbyProfilesUnavailable("No lobby profile found");
                return;
            }

            try
            {
                lobbyGlobalData = new LobbyPersistentData.LobbyGlobalData(LobbyGlobalDataFile);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is InvalidDataException)
            {
                Program.Logging.Log("Unable to read the lobby profiles from " + Path.GetFullPath(LobbyGlobalDataFile), ex);
                ShowLobbyProfilesUnavailable("Lobby profiles could not be read (see log.txt)");
                return;
            }

            foreach (var playerInfo in lobbyGlobalData.PlayerInfos.OrderByDescending(d => d.LastUse))
            {
                usersLobbyKryptonComboBox.Items.Add(playerInfo.Username);
            }

            if (usersLobbyKryptonComboBox.Items.Count > 0)
                usersLobbyKryptonComboBox.SelectedIndex = 0;
            else
                ShowLobbyProfilesUnavailable("No lobby profile found");
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
                Program.Logging.Log("The online player list is available again.");
                playerListUnavailable = false;
            }

            onlinePlayersKryptonDataGridView.Rows.Clear();

            neoOnlineKryptonGroupBox.Values.Heading = "Online Players (" + message.OnlinePlayers + ")";

            foreach (NeoApiClient.ConnectedPlayersMessage.PlayerInfo pInfo in message.PlayersInfo)
            {
                if (!usersLobbyKryptonComboBox.Text.Equals(pInfo.Name, StringComparison.InvariantCultureIgnoreCase))
                    onlinePlayersKryptonDataGridView.Rows.Add(pInfo.Name, pInfo.GameStateToString());
            }
        }

        private void ShowPlayerListUnavailable(Exception error)
        {
            if (!playerListUnavailable)
            {
                Program.Logging.Log("The online player list of " + neoClient.Endpoint + " is unavailable, retrying every " +
                                    playerListPollIntervalMilliseconds + " ms.", error);
                playerListUnavailable = true;
            }

            onlinePlayersKryptonDataGridView.Rows.Clear();
            neoOnlineKryptonGroupBox.Values.Heading = "Online Players (unavailable)";
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

            Program.Logging.Log("The online player list polling stopped unexpectedly.", e.Error);
            if (!IsDisposed)
                neoOnlineKryptonGroupBox.Values.Heading = "Online Players (unavailable, see log.txt)";
        }

        private void usersLobbyKryptonComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lobbyGlobalData == null)
                return;

            LobbyPersistentData.LobbyGlobalData.PlayerInfoGlobalData selectedPlayer = lobbyGlobalData.PlayerInfos
                .FirstOrDefault(playerInfo => playerInfo.Username.Equals(usersLobbyKryptonComboBox.Text));

            if (selectedPlayer == null)
                return;

            string tmppath = @"C:\Program Files (x86)\Neo Empire Earth\Empire Earth";
            FileInfo fileInfo = new FileInfo(Path.Combine(tmppath, "_wonuser" + selectedPlayer.FileID + ".dat"));

            if (!fileInfo.Exists)
                return;

            try
            {
                lobbyUserData = new LobbyPersistentData.LobbyUserData(fileInfo.FullName);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is InvalidDataException)
            {
                Program.Logging.Log("Unable to read the lobby user data from " + fileInfo.FullName, ex);
                lobbyUserData = null;
                neoOnlineKryptonGroupBox.Values.Description = "Friends could not be read (see log.txt)";
                return;
            }
            neoOnlineKryptonGroupBox.Values.Description = "Friends (" + lobbyUserData.Friends.Count + ")";
        }
    }
}

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
using Empire_Earth_Launcher.WON;

namespace Empire_Earth_Launcher
{
    public partial class GeneralUserControl : UserControl
    {
        private const string LobbyGlobalDataFile = "./_wonlobbypersistent.dat";

        /// <summary>Delay between two requests of the online player list.</summary>
        private const int PlayerListPollIntervalMilliseconds = 5000;

        /// <summary>Granularity of the poll delay, so that cancelling the worker does not wait for a whole interval.</summary>
        private const int CancellationCheckMilliseconds = 100;

        private BackgroundWorker backgroundWorker;
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

            foreach (var playerInfo in lobbyGlobalData.PlayerInfoGlobalDatas.OrderByDescending(d => d.LastUse))
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

            if (e.UserState is NeoAPI.ConnectedPlayersMessage message)
                ShowOnlinePlayers(message);
            else
                ShowPlayerListUnavailable(e.UserState as Exception);
        }

        private void ShowOnlinePlayers(NeoAPI.ConnectedPlayersMessage message)
        {
            if (playerListUnavailable)
            {
                Program.Logging.Log("The online player list is available again.");
                playerListUnavailable = false;
            }

            onlinePlayersKryptonDataGridView.Rows.Clear();

            neoOnlineKryptonGroupBox.Values.Heading = "Online Players (" + message.OnlinePlayers + ")";

            foreach (NeoAPI.ConnectedPlayersMessage.PlayerInfo pInfo in message.PlayersInfo)
            {
                if (!usersLobbyKryptonComboBox.Text.Equals(pInfo.Name, StringComparison.InvariantCultureIgnoreCase))
                    onlinePlayersKryptonDataGridView.Rows.Add(pInfo.Name, pInfo.GameStateToString(pInfo.GameState));
            }
        }

        private void ShowPlayerListUnavailable(Exception error)
        {
            if (!playerListUnavailable)
            {
                Program.Logging.Log("The online player list is unavailable, retrying every " +
                                    PlayerListPollIntervalMilliseconds / 1000 + " s.", error);
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
                worker.ReportProgress(0, RequestConnectedPlayers());

                for (int waited = 0;
                     waited < PlayerListPollIntervalMilliseconds && !worker.CancellationPending;
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
        private static object RequestConnectedPlayers()
        {
            try
            {
                return new NeoAPI.ConnectedPlayersMessage();
            }
            catch (Exception ex)
            {
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

            LobbyPersistentData.LobbyGlobalData.PlayerInfoGlobalData selectedPlayer = lobbyGlobalData.PlayerInfoGlobalDatas
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

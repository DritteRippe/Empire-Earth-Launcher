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
        private BackgroundWorker backgroundWorker;
        private LobbyPersistentData.LobbyUserData lobbyUserData;

        public GeneralUserControl()
        {
            InitializeComponent();
            Program.LauncherKryptonTheme.AddPalette(launcherKryptonPalette, this);

            backgroundWorker = new BackgroundWorker()
            {
                WorkerReportsProgress = true,
                WorkerSupportsCancellation = true
            };
            backgroundWorker.DoWork += backgroundWorker_DoWork;
            backgroundWorker.ProgressChanged += backgroundWorker_ProgressChanged;
            backgroundWorker.RunWorkerAsync();


            LobbyPersistentData.LobbyGlobalData test = new WON.LobbyPersistentData.LobbyGlobalData("./_wonlobbypersistent.dat");
            foreach (var lobbyGlobalData in test.PlayerInfoGlobalDatas.OrderByDescending(d => d.LastUse))
            {
                usersLobbyKryptonComboBox.Items.Add(lobbyGlobalData.Username);
            }
            usersLobbyKryptonComboBox.SelectedIndex = 0;

            new NeoAPI(NeoAPI.RequestType.GAMES_MESSAGE).SendRequest('\x0B');
            new NeoAPI(NeoAPI.RequestType.CHAT_MESSAGE).SendRequest('\x0B');
        }

        private void backgroundWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            NeoAPI.ConnectedPlayersMessage ConnectedPlayersMessage = ((NeoAPI.ConnectedPlayersMessage)e.UserState);
            onlinePlayersKryptonDataGridView.Rows.Clear();

            neoOnlineKryptonGroupBox.Values.Heading = "Online Players (" + ConnectedPlayersMessage.OnlinePlayers + ")";

            foreach (NeoAPI.ConnectedPlayersMessage.PlayerInfo pInfo in ConnectedPlayersMessage.PlayersInfo)
            {
                if (!usersLobbyKryptonComboBox.Text.Equals(pInfo.Name, StringComparison.InvariantCultureIgnoreCase))
                    onlinePlayersKryptonDataGridView.Rows.Add(pInfo.Name + pInfo.Name, pInfo.GameStateToString(pInfo.GameState));
            }
        }

        private void backgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker worker = (BackgroundWorker)sender;
            while (!worker.CancellationPending)
            {
                worker.ReportProgress(0, new NeoAPI.ConnectedPlayersMessage());
                Thread.Sleep(5000);
            }
        }

        private void usersLobbyKryptonComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            LobbyPersistentData.LobbyGlobalData test = new LobbyPersistentData.LobbyGlobalData("./_wonlobbypersistent.dat");

            int lobbyFileId = -1;
            foreach (LobbyPersistentData.LobbyGlobalData.PlayerInfoGlobalData lobbyGlobalData in test.PlayerInfoGlobalDatas)
            {
                if (lobbyGlobalData.Username.Equals(usersLobbyKryptonComboBox.Text))
                {
                    lobbyFileId = lobbyGlobalData.FileID;
                    break;
                }
            }

            if (lobbyFileId == -1)
                return;

            string tmppath = @"C:\Program Files (x86)\Neo Empire Earth\Empire Earth";
            FileInfo fileInfo = new FileInfo(Path.Combine(tmppath, "_wonuser" + lobbyFileId + ".dat"));

            if (!fileInfo.Exists)
                return;
            lobbyUserData = new LobbyPersistentData.LobbyUserData(fileInfo.FullName);
            neoOnlineKryptonGroupBox.Values.Description = "Friends (" + lobbyUserData.Friends.Count + ")";
        }
    }
}

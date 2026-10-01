using Krypton.Toolkit;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Windows.Forms;
using Empire_Earth_WON;

namespace Empire_Earth_Launcher
{
    public partial class Form1 : KryptonForm
    {
        /// <param name="logger">Log of the launcher.</param>
        /// <param name="themeService">Theme of the launcher windows.</param>
        /// <param name="neoClient">Client for the online player list; null if the server settings are invalid.</param>
        /// <param name="playerListPollIntervalMilliseconds">Delay between two requests of the player list.</param>
        public Form1(ILogger logger, IThemeService themeService, NeoApiClient neoClient,
            int playerListPollIntervalMilliseconds)
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer, true);
            InitializeComponent();
            themeService.Register(launcherKryptonPalette, this);

            // The pages are created by InitializeComponent (designer), which needs parameterless constructors,
            // so they receive their services here.
            generalUserControl.Initialize(logger, themeService, neoClient, playerListPollIntervalMilliseconds);
            settingsUserControl.Initialize(themeService);
            launcherSettingsUserControl.Initialize(themeService);
        }


        // Fake Button as Radio Button

        private void playKryptonCheckButton_Click(object sender, EventArgs e)
        {
            if (!playKryptonCheckButton.Checked)
                playKryptonCheckButton.Checked = true;
            modsKryptonCheckButton.Checked = false;
            settingsKryptonCheckButton.Checked = false;
            launcherKryptonCheckButton.Checked = false;

            generalUserControl.Visible = true;
            settingsUserControl.Visible = false;
            launcherSettingsUserControl.Visible = false;
        }
        private void settingsKryptonCheckButton_Click(object sender, EventArgs e)
        {
            if (!settingsKryptonCheckButton.Checked)
                settingsKryptonCheckButton.Checked = true;
            playKryptonCheckButton.Checked = false;
            modsKryptonCheckButton.Checked = false;
            launcherKryptonCheckButton.Checked = false;

            generalUserControl.Visible = false;
            settingsUserControl.Visible = true;
            launcherSettingsUserControl.Visible = false;
        }

        private void modsKryptonCheckButton_Click(object sender, EventArgs e)
        {
            if (!modsKryptonCheckButton.Checked)
                modsKryptonCheckButton.Checked = true;
            playKryptonCheckButton.Checked = false;
            settingsKryptonCheckButton.Checked = false;
            launcherKryptonCheckButton.Checked = false;


            generalUserControl.Visible = false;
            settingsUserControl.Visible = false;
            launcherSettingsUserControl.Visible = false;
        }

        private void launcherSettingsKryptonCheckButton_Click(object sender, EventArgs e)
        {
            if (!launcherKryptonCheckButton.Checked)
                launcherKryptonCheckButton.Checked = true;
            playKryptonCheckButton.Checked = false;
            modsKryptonCheckButton.Checked = false;
            settingsKryptonCheckButton.Checked = false;

            generalUserControl.Visible = false;
            settingsUserControl.Visible = false;
            launcherSettingsUserControl.Visible = true;
        }
    }
}

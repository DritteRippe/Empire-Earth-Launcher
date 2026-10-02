using Krypton.Toolkit;
using System;
using System.Windows.Forms;
using Empire_Earth_Launcher.Core.Logging;
using Empire_Earth_Launcher.Core.Settings;
using Empire_Earth_Launcher.Properties;
using Empire_Earth_WON;

namespace Empire_Earth_Launcher
{
    public partial class MainForm : KryptonForm
    {
        /// <summary>Buttons of the navigation bar; the Tag of each one is its page (or null).</summary>
        private readonly KryptonCheckButton[] navigationButtons;

        /// <param name="logger">Log of the launcher.</param>
        /// <param name="themeService">Theme of the launcher windows.</param>
        /// <param name="settings">User settings of the launcher (settings.json).</param>
        /// <param name="gameDirectory">The Empire Earth folder.</param>
        /// <param name="neoClient">Client for the online player list; null if the server settings are invalid.</param>
        /// <param name="playerListPollIntervalMilliseconds">Delay between two requests of the player list.</param>
        internal MainForm(ILogger logger, IThemeService themeService, SettingsStore settings,
            GameDirectoryService gameDirectory, NeoApiClient neoClient, int playerListPollIntervalMilliseconds)
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer, true);
            InitializeComponent();
            ApplyTexts();
            themeService.Register(launcherKryptonPalette, this);

            // The pages are created by InitializeComponent (designer), which needs parameterless constructors,
            // so they receive their services here.
            generalUserControl.Initialize(logger, themeService, gameDirectory, neoClient,
                playerListPollIntervalMilliseconds);
            settingsUserControl.Initialize(themeService);
            launcherSettingsUserControl.Initialize(themeService, settings, gameDirectory);

            // A page cannot be assigned to Tag in the designer, so the navigation is wired up here.
            playKryptonCheckButton.Tag = generalUserControl;
            settingsKryptonCheckButton.Tag = settingsUserControl;
            launcherKryptonCheckButton.Tag = launcherSettingsUserControl;
            navigationButtons = new[] { playKryptonCheckButton, settingsKryptonCheckButton, launcherKryptonCheckButton };
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
            launcherKryptonCheckButton.Values.Text = Resources.NavigationLauncher;
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

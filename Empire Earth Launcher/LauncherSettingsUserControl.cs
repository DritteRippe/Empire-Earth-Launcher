using System;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using Empire_Earth_Launcher.Core.Settings;
using Empire_Earth_Launcher.Properties;

namespace Empire_Earth_Launcher
{
    public partial class LauncherSettingsUserControl : UserControl
    {
        /// <summary>
        /// Index of the "Custom" item that the designer puts first into the theme list: it opens a theme file
        /// instead of selecting a theme of the launcher's themes folder.
        /// </summary>
        private const int CustomThemeIndex = 0;

        private IThemeService themeService;
        private SettingsStore settings;
        private GameDirectoryService gameDirectory;

        /// <summary>True while the theme list is changed by code, so that no theme is applied then.</summary>
        private bool updatingThemeSelection;

        public LauncherSettingsUserControl()
        {
            InitializeComponent();
            ApplyTexts();
        }

        /// <summary>
        /// Sets the texts of the page from the resources in the UI language (ADR 0009); the designer texts are
        /// placeholders. Texts that depend on the state (where the game folder comes from) are set where the state
        /// changes.
        /// </summary>
        private void ApplyTexts()
        {
            launcherSettingsKryptonGroupBox.Values.Heading = Resources.LauncherSettingsHeading;
            themeKryptonLabel.Values.Text = Resources.ThemeLabel;
            themeKryptonComboBox.Items[CustomThemeIndex] = Resources.ThemeCustom;
            gameDirectoryKryptonLabel.Values.Text = Resources.GameDirectoryLabel;
            detectGameDirectoryKryptonButton.Values.Text = Resources.DetectGameDirectoryButton;
        }

        /// <summary>
        /// Passes the services in. The control is created by the designer, which needs a parameterless
        /// constructor, so its owner calls this right after InitializeComponent.
        /// </summary>
        /// <param name="themeService">Theme of the launcher.</param>
        /// <param name="settings">User settings; the selected theme is saved there.</param>
        /// <param name="gameDirectory">The Empire Earth folder, which can be chosen on this page.</param>
        internal void Initialize(IThemeService themeService, SettingsStore settings, GameDirectoryService gameDirectory)
        {
            if (themeService == null)
                throw new ArgumentNullException(nameof(themeService));
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));
            if (gameDirectory == null)
                throw new ArgumentNullException(nameof(gameDirectory));
            this.themeService = themeService;
            this.settings = settings;
            this.gameDirectory = gameDirectory;
            themeService.Register(launcherKryptonPalette, this);

            LoadAvailableThemes();
            SelectCurrentTheme();

            EventHandler showGameDirectory = (sender, e) => ShowGameDirectory();
            gameDirectory.Changed += showGameDirectory;
            Disposed += (sender, e) => gameDirectory.Changed -= showGameDirectory;
            ShowGameDirectory();
        }

        private void LoadAvailableThemes()
        {
            foreach (string themeName in themeService.GetAvailableThemeNames())
                themeKryptonComboBox.Items.Add(themeName);
        }

        /// <summary>Shows the applied theme in the list, without applying anything.</summary>
        private void SelectCurrentTheme()
        {
            int index = -1;
            string currentThemeFile = themeService.CurrentThemeFile;
            if (currentThemeFile != null)
            {
                index = IsInThemesDirectory(currentThemeFile)
                    ? themeKryptonComboBox.Items.IndexOf(Path.GetFileNameWithoutExtension(currentThemeFile))
                    : CustomThemeIndex;
            }

            updatingThemeSelection = true;
            try
            {
                themeKryptonComboBox.SelectedIndex = index;
            }
            finally
            {
                updatingThemeSelection = false;
            }
        }

        private static bool IsInThemesDirectory(string themeFile)
        {
            string directory = Path.GetDirectoryName(themeFile);
            return directory != null && string.Equals(Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar),
                Path.GetFullPath(LauncherPaths.ThemesDirectory).TrimEnd(Path.DirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);
        }

        private void themeKryptonComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (updatingThemeSelection || themeService == null || themeKryptonComboBox.SelectedIndex < 0)
                return;

            if (themeKryptonComboBox.SelectedIndex == CustomThemeIndex)
                SelectCustomThemeFile();
            else
                ApplyTheme((string)themeKryptonComboBox.SelectedItem);

            // Cancelled or failed: show the theme that is really applied (this also lets the user pick
            // "Custom" again, which would not raise SelectedIndexChanged while it is still selected).
            SelectCurrentTheme();
        }

        private void ApplyTheme(string themeName)
        {
            if (!themeService.ApplyTheme(themeName))
            {
                ShowThemeNotLoaded();
                return;
            }

            // A failure to save is logged by the store; the theme stays applied for this session.
            settings.Current.ThemeName = themeName;
            settings.Current.CustomThemeFile = string.Empty;
            settings.Save();
        }

        private void SelectCustomThemeFile()
        {
            string themeFile;
            using (var openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = Resources.ThemeFileFilter;
                if (openFileDialog.ShowDialog(this) != DialogResult.OK)
                    return;
                themeFile = openFileDialog.FileName;
            }

            if (!themeService.ApplyThemeFile(themeFile))
            {
                ShowThemeNotLoaded();
                return;
            }

            settings.Current.CustomThemeFile = themeService.CurrentThemeFile;
            settings.Save();
        }

        private void ShowThemeNotLoaded()
        {
            MessageBox.Show(this,
                string.Format(CultureInfo.CurrentCulture, Resources.ThemeNotLoadedFormat, LauncherPaths.LogFile),
                Resources.LauncherTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private void ShowGameDirectory()
        {
            string location = gameDirectory.Location;
            gameDirectoryKryptonTextBox.Text = location ?? string.Empty;
            gameDirectorySourceKryptonLabel.Values.Text = Texts.GameDirectoryOrigin(gameDirectory.Source,
                location != null && Directory.Exists(location));
        }

        private void browseGameDirectoryKryptonButton_Click(object sender, EventArgs e)
        {
            string folder;
            using (var folderBrowserDialog = new FolderBrowserDialog())
            {
                folderBrowserDialog.Description = string.Format(CultureInfo.CurrentCulture,
                    Resources.SelectGameDirectoryFormat, GameDirectoryLocator.GameExecutableName);
                folderBrowserDialog.ShowNewFolderButton = false;
                if (gameDirectory.Location != null && Directory.Exists(gameDirectory.Location))
                    folderBrowserDialog.SelectedPath = gameDirectory.Location;
                if (folderBrowserDialog.ShowDialog(this) != DialogResult.OK)
                    return;
                folder = folderBrowserDialog.SelectedPath;
            }

            if (!GameDirectoryLocator.IsGameDirectory(folder) &&
                MessageBox.Show(this,
                    string.Format(CultureInfo.CurrentCulture, Resources.GameExecutableMissingFormat,
                        GameDirectoryLocator.GameExecutableName, folder),
                    Resources.LauncherTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            {
                return;
            }

            gameDirectory.SetUserDirectory(folder);
        }

        private void detectGameDirectoryKryptonButton_Click(object sender, EventArgs e)
        {
            gameDirectory.SetUserDirectory(null);
        }
    }
}

using System;
using System.IO;
using System.Windows.Forms;
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

        private ILogger logger;
        private IThemeService themeService;
        private Settings settings;

        /// <summary>True while the theme list is changed by code, so that no theme is applied then.</summary>
        private bool updatingThemeSelection;

        public LauncherSettingsUserControl()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Passes the services in. The control is created by the designer, which needs a parameterless
        /// constructor, so its owner calls this right after InitializeComponent.
        /// </summary>
        /// <param name="logger">Log of the launcher.</param>
        /// <param name="themeService">Theme of the launcher.</param>
        /// <param name="settings">User settings; the selected theme is saved there.</param>
        internal void Initialize(ILogger logger, IThemeService themeService, Settings settings)
        {
            if (logger == null)
                throw new ArgumentNullException(nameof(logger));
            if (themeService == null)
                throw new ArgumentNullException(nameof(themeService));
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));
            this.logger = logger;
            this.themeService = themeService;
            this.settings = settings;
            themeService.Register(launcherKryptonPalette, this);

            LoadAvailableThemes();
            SelectCurrentTheme();
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

            settings.ThemeName = themeName;
            settings.CustomThemeFile = string.Empty;
            SaveSettings();
        }

        private void SelectCustomThemeFile()
        {
            string themeFile;
            using (var openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "Theme File (*.xml)|*.xml|All files (*.*)|*.*";
                if (openFileDialog.ShowDialog(this) != DialogResult.OK)
                    return;
                themeFile = openFileDialog.FileName;
            }

            if (!themeService.ApplyThemeFile(themeFile))
            {
                ShowThemeNotLoaded();
                return;
            }

            settings.CustomThemeFile = themeService.CurrentThemeFile;
            SaveSettings();
        }

        private void ShowThemeNotLoaded()
        {
            MessageBox.Show(this, "The theme could not be loaded. Details have been written to " +
                                  LauncherPaths.LogFile + ".", "Empire Earth Launcher",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private void SaveSettings()
        {
            try
            {
                settings.Save();
            }
            catch (Exception ex) when (ex is System.Configuration.ConfigurationException || ex is IOException ||
                                       ex is UnauthorizedAccessException)
            {
                // The theme stays applied for this session.
                logger.Error("Unable to save the launcher settings.", ex);
            }
        }
    }
}

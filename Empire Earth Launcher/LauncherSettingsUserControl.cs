using System;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Installations;
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
        private InstallationService installations;
        private UiOperation uiOperation;

        /// <summary>The result shown in the list of installations, so that the list is rebuilt only when it changes.</summary>
        private DiscoveryResult shownResult;

        /// <summary>True while the list of installations is filled by code, so that no selection is saved then.</summary>
        private bool updatingInstallationList;

        /// <summary>True while the theme list is changed by code, so that no theme is applied then.</summary>
        private bool updatingThemeSelection;

        /// <summary>True while the language list is filled by code, so that nothing is saved then.</summary>
        private bool updatingLanguageSelection;

        /// <summary>
        /// The language setting the launcher started with (<see cref="UiLanguage.Choices"/>); a different choice is
        /// used from the next start on.
        /// </summary>
        private string languageAtStart = UiLanguage.Windows;

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
            uiLanguageKryptonLabel.Values.Text = Resources.UiLanguageLabel;
            uiLanguageHintKryptonLabel.Values.Text = Resources.UiLanguageRestartHint;
            installationsKryptonLabel.Values.Text = Resources.InstallationsLabel;
            installationProductColumn.HeaderText = Resources.InstallationProductColumn;
            installationRootColumn.HeaderText = Resources.InstallationRootColumn;
            installationGameFolderColumn.HeaderText = Resources.InstallationGameFolderColumn;
            installationKindColumn.HeaderText = Resources.InstallationKindColumn;
            installationStateColumn.HeaderText = Resources.InstallationStateColumn;
        }

        /// <summary>
        /// Passes the services in. The control is created by the designer, which needs a parameterless
        /// constructor, so its owner calls this right after InitializeComponent.
        /// </summary>
        /// <param name="themeService">Theme of the launcher.</param>
        /// <param name="settings">User settings; the selected theme is saved there.</param>
        /// <param name="installations">The installations found; the one to use can be chosen on this page.</param>
        /// <param name="uiOperation">Runs the discovery started by this page (ADR 0004).</param>
        internal void Initialize(IThemeService themeService, SettingsStore settings, InstallationService installations,
            UiOperation uiOperation)
        {
            if (themeService == null)
                throw new ArgumentNullException(nameof(themeService));
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));
            if (installations == null)
                throw new ArgumentNullException(nameof(installations));
            if (uiOperation == null)
                throw new ArgumentNullException(nameof(uiOperation));
            this.themeService = themeService;
            this.settings = settings;
            this.installations = installations;
            this.uiOperation = uiOperation;
            themeService.Register(launcherKryptonPalette, this);

            LoadAvailableThemes();
            SelectCurrentTheme();
            LoadUiLanguages();

            EventHandler showInstallations = (sender, e) => ShowInstallations();
            installations.Changed += showInstallations;
            Disposed += (sender, e) => installations.Changed -= showInstallations;
            ShowInstallations();
        }

        /// <summary>
        /// Starts the discovery of the installations in the background (contract 1.4); the Auto-detect button is disabled
        /// until it has finished. Called by the main window once it is shown.
        /// </summary>
        internal void StartDiscovery()
        {
            uiOperation.Run(detectGameDirectoryKryptonButton, () => installations.RefreshAsync());
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

        /// <summary>
        /// Fills the language list (Windows language, English, Deutsch, Français) and selects the setting the launcher
        /// started with (Program applied it before the first window); an unknown value shows as the Windows language.
        /// </summary>
        private void LoadUiLanguages()
        {
            UiLanguage.TryNormalize(settings.Current.UiCulture, out languageAtStart);
            updatingLanguageSelection = true;
            try
            {
                uiLanguageKryptonComboBox.Items.Clear();
                foreach (string language in UiLanguage.Choices)
                    uiLanguageKryptonComboBox.Items.Add(Texts.UiLanguageName(language));
                uiLanguageKryptonComboBox.SelectedIndex = UiLanguage.IndexOf(languageAtStart);
            }
            finally
            {
                updatingLanguageSelection = false;
            }
        }

        /// <summary>
        /// Saves the chosen language in settings.json; it is used from the next start on (the texts of the open windows
        /// stay as they are), which the hint below the list says.
        /// </summary>
        private void uiLanguageKryptonComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            int index = uiLanguageKryptonComboBox.SelectedIndex;
            if (updatingLanguageSelection || settings == null || index < 0)
                return;

            string language = UiLanguage.Choices[index];
            settings.Current.UiCulture = language;
            // A failure to save is logged by the store; the launcher then starts in the previous language.
            settings.Save();
            uiLanguageHintKryptonLabel.Visible = language != languageAtStart;
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

        /// <summary>
        /// Shows the selected installation (its EE folder and where it comes from), the list of every installation found
        /// with product, install folder, EE folder, type and state, and the hints below the list (ADR 0015: several
        /// installations on one game settings key; contract O11 and 5).
        /// </summary>
        private void ShowInstallations()
        {
            DiscoveryResult result = installations.Result;
            if (result == null)
            {
                gameDirectoryKryptonTextBox.Text = string.Empty;
                gameDirectorySourceKryptonLabel.Values.Text = Resources.InstallationsSearching;
                installationsHintKryptonWrapLabel.Text = string.Empty;
                return;
            }

            Installation selected = result.Selected;
            gameDirectoryKryptonTextBox.Text = selected?.EeFolder ?? string.Empty;
            gameDirectorySourceKryptonLabel.Values.Text = Texts.InstallationOrigin(selected, result.IsSelectedByUser);
            installationsHintKryptonWrapLabel.Text = Texts.InstallationHints(result);
            if (result == shownResult)
                return;

            shownResult = result;
            updatingInstallationList = true;
            try
            {
                installationsKryptonDataGridView.Rows.Clear();
                foreach (Installation installation in result.Installations)
                {
                    int index = installationsKryptonDataGridView.Rows.Add(installation.Product.AppName, installation.Root,
                        installation.EeFolder, Texts.InstallationKindName(installation.Kind),
                        Texts.InstallationStateName(installation));
                    DataGridViewRow row = installationsKryptonDataGridView.Rows[index];
                    row.Tag = installation;
                    row.Cells[installationKindColumn.Index].ToolTipText = Texts.InstallationKindHint(installation.Kind);
                    row.Cells[installationStateColumn.Index].ToolTipText = Texts.InstallationStateHint(installation);
                }

                installationsKryptonDataGridView.ClearSelection();
                foreach (DataGridViewRow row in installationsKryptonDataGridView.Rows)
                    row.Selected = row.Tag == selected;
            }
            finally
            {
                updatingInstallationList = false;
            }
        }

        /// <summary>The user picked another installation of the list: it becomes the choice (source 1).</summary>
        private void installationsKryptonDataGridView_SelectionChanged(object sender, EventArgs e)
        {
            if (updatingInstallationList || installations == null || installationsKryptonDataGridView.SelectedRows.Count != 1)
                return;
            // The grid also selects its first row by itself when it is shown; the selected installation is the first row.
            if (!(installationsKryptonDataGridView.SelectedRows[0].Tag is Installation installation) ||
                installation == installations.Selected)
                return;

            uiOperation.Run(installationsKryptonDataGridView, () => installations.SelectAsync(installation));
        }

        private void browseGameDirectoryKryptonButton_Click(object sender, EventArgs e)
        {
            string folder;
            using (var folderBrowserDialog = new FolderBrowserDialog())
            {
                folderBrowserDialog.Description = string.Format(CultureInfo.CurrentCulture,
                    Resources.SelectGameDirectoryFormat, Game.EmpireEarth.ProgramName);
                folderBrowserDialog.ShowNewFolderButton = false;
                string current = installations.Selected?.EeFolder;
                if (current != null && Directory.Exists(current))
                    folderBrowserDialog.SelectedPath = current;
                if (folderBrowserDialog.ShowDialog(this) != DialogResult.OK)
                    return;
                folder = folderBrowserDialog.SelectedPath;
            }

            // An install root, an EE folder or an AoC folder is fine (contract 1.4); anything else only if the user wants it.
            if (installations.ClassifyFolder(folder) == GameFolderKind.None &&
                MessageBox.Show(this,
                    string.Format(CultureInfo.CurrentCulture, Resources.GameExecutableMissingFormat,
                        Game.EmpireEarth.ProgramName, folder),
                    Resources.LauncherTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            {
                return;
            }

            uiOperation.Run(browseGameDirectoryKryptonButton, () => installations.ChooseFolderAsync(folder));
        }

        private void detectGameDirectoryKryptonButton_Click(object sender, EventArgs e)
        {
            uiOperation.Run(detectGameDirectoryKryptonButton, () => installations.UseAutomaticDetectionAsync());
        }
    }
}

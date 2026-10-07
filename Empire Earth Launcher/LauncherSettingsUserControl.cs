using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Core.Settings;
using Empire_Earth_Launcher.Properties;

namespace Empire_Earth_Launcher
{
    /// <summary>
    /// The Launcher page: the theme, the language, the game folder and the list of the installations found. The group of the
    /// settings grows with the window (ADR 0017): its controls are stacked from the heights of their texts by
    /// <see cref="ScrollPageLayout"/> in <see cref="LayoutPage"/>, the text boxes and the list take the width of the group, and
    /// the list takes the height that is left; the page scrolls where the window is too small for the texts.
    /// </summary>
    public partial class LauncherSettingsUserControl : UserControl
    {
        /// <summary>
        /// Index of the "Built-in colors" item that the designer puts first into the theme list: the colors of the
        /// designer, shown while no theme file is applied (before, the list then showed nothing).
        /// </summary>
        internal const int BuiltInThemeIndex = 0;

        /// <summary>
        /// Index of the "Custom" item that the designer puts second into the theme list: it opens a theme file
        /// instead of selecting a theme of the launcher's themes folder.
        /// </summary>
        internal const int CustomThemeIndex = 1;

        /// <summary>Index of the first theme of the themes folder in the theme list.</summary>
        internal const int FirstThemeIndex = 2;

        /// <summary>The space left, right and above the group of the settings.</summary>
        private const int PageMargin = 8;

        /// <summary>The space left and right of the controls in the group (the designer left 13 pixels).</summary>
        private const int GroupMargin = 13;

        /// <summary>The width of the theme list and the language list (from the designer).</summary>
        private readonly int comboWidth;

        /// <summary>The height of the list of the installations (from the designer): it is never lower than that.</summary>
        private readonly int listMinimumHeight;

        /// <summary>Stacks the group of the settings in the panel that scrolls.</summary>
        private readonly ScrollPageLayout page;

        /// <summary>Stacks the controls inside the group; the group is as high as they are, or as the window leaves.</summary>
        private readonly ScrollPageLayout group;

        /// <summary>
        /// The visibility each control should have (Visible reads false while the page is hidden, and the launcher fills the
        /// page before its window is shown). A control that is not in it is always shown.
        /// </summary>
        private readonly Dictionary<Control, bool> shown = new Dictionary<Control, bool>();

        /// <summary>True while <see cref="LayoutPage"/> runs, so that nothing it sets starts it again.</summary>
        private bool layingOut;

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

        /// <summary>The themes of the themes folder in the list, after the two fixed items.</summary>
        private IList<string> themeNames = new List<string>();

        /// <summary>
        /// True after the player chose the built-in colors while a theme file was applied: they are used from the next
        /// start on, the list shows them already.
        /// </summary>
        private bool builtInColorsChosen;

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
            comboWidth = themeKryptonComboBox.Width;
            listMinimumHeight = installationsKryptonDataGridView.Height;
            shown[uiLanguageHintKryptonWrapLabel] = uiLanguageHintKryptonWrapLabel.Visible;
            page = new ScrollPageLayout(launcherScrollPanel, IsShown, PageMargin);
            group = new ScrollPageLayout(launcherSettingsKryptonGroupBox.Panel, IsShown, GroupMargin, false);
            // Krypton sizes the panel of the group when the group is laid out, also when its heading gets another font (a theme).
            launcherSettingsKryptonGroupBox.Panel.SizeChanged += (sender, e) => LayoutPage();
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
            themeKryptonComboBox.Items[BuiltInThemeIndex] = Resources.ThemeBuiltIn;
            themeKryptonComboBox.Items[CustomThemeIndex] = Resources.ThemeCustom;
            gameDirectoryKryptonLabel.Values.Text = Resources.GameDirectoryLabel;
            detectGameDirectoryKryptonButton.Values.Text = Resources.DetectGameDirectoryButton;
            uiLanguageKryptonLabel.Values.Text = Resources.UiLanguageLabel;
            uiLanguageHintKryptonWrapLabel.Text = Resources.UiLanguageRestartHint;
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

        // --- Layout (ADR 0017) -------------------------------------------------------------------------------------------

        private void SetShown(Control control, bool visible)
        {
            shown[control] = visible;
            control.Visible = visible;
        }

        private bool IsShown(Control control)
        {
            // Not control.Visible: it reads false for every control of a page that is hidden, as the launcher fills its pages
            // before its window is shown (see SettingsUserControl).
            return !shown.TryGetValue(control, out bool visible) || visible;
        }

        /// <summary>
        /// The window was resized, or the page is laid out for the first time: the group gets the width of the page. (The
        /// layout of the page itself cannot wait for a change of its size, which the first layout does not need.)
        /// </summary>
        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            LayoutPage();
        }

        /// <summary>
        /// Lays the page out for its size: the group takes the width of the page and the height of what it holds, or the
        /// height the window leaves if that is more (the list of the installations takes the difference). Runs when the page is
        /// laid out and when a text that changes a height changes.
        /// </summary>
        private void LayoutPage()
        {
            // The first layouts run inside InitializeComponent and the Visual Studio designer lays the page out as well.
            if (layingOut || DesignMode || group == null)
                return;
            layingOut = true;
            try
            {
                page.Run(PlaceGroup);
            }
            finally
            {
                layingOut = false;
            }
        }

        private void PlaceGroup()
        {
            Krypton.Toolkit.KryptonGroupBox box = launcherSettingsKryptonGroupBox;
            box.Width = page.ContentWidth;
            box.PerformLayout();
            // What the group takes besides its content (heading, borders) does not depend on the content.
            int chrome = box.Height - box.Panel.Height;

            installationsKryptonDataGridView.Height = listMinimumHeight;
            group.Run(PlaceGroupContent);
            int spare = launcherScrollPanel.ClientSize.Height - 2 * PageMargin - (group.ContentHeight + chrome);
            if (spare > 0)
            {
                installationsKryptonDataGridView.Height = listMinimumHeight + spare;
                group.Run(PlaceGroupContent);
            }
            box.Height = group.ContentHeight + chrome;
            page.Place(box);
        }

        /// <summary>
        /// The rows of the group: a label and its field each (the labels share one column, so that the fields start at the same
        /// place), the folder with its two buttons, the list of the installations and the hints below it.
        /// </summary>
        private void PlaceGroupContent()
        {
            int labelWidth = new Control[] { themeKryptonLabel, uiLanguageKryptonLabel, gameDirectoryKryptonLabel }
                .Max(label => group.NaturalSize(label).Width);
            group.PlaceField(themeKryptonLabel, labelWidth, themeKryptonComboBox, comboWidth);
            group.PlaceField(uiLanguageKryptonLabel, labelWidth, uiLanguageKryptonComboBox, comboWidth);
            group.Place(uiLanguageHintKryptonWrapLabel);
            group.PlaceField(gameDirectoryKryptonLabel, labelWidth, gameDirectoryKryptonTextBox, 0,
                browseGameDirectoryKryptonButton, detectGameDirectoryKryptonButton);
            group.Place(gameDirectorySourceKryptonWrapLabel, labelWidth + ScrollPageLayout.Gap);
            group.Space(ScrollPageLayout.Gap);
            group.Place(installationsKryptonLabel);
            group.Place(installationsKryptonDataGridView);
            group.Place(installationsHintKryptonWrapLabel);
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
            themeNames = themeService.GetAvailableThemeNames();
            foreach (string themeName in themeNames)
                themeKryptonComboBox.Items.Add(themeName);
        }

        /// <summary>Shows the applied theme in the list, without applying anything.</summary>
        private void SelectCurrentTheme()
        {
            int index = ThemeListIndex(themeService.CurrentThemeFile, builtInColorsChosen, LauncherPaths.ThemesDirectory,
                themeNames);

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
            SetShown(uiLanguageHintKryptonWrapLabel, language != languageAtStart);
            LayoutPage();
        }

        /// <summary>
        /// The item of the theme list that shows the colors in use: the built-in colors while no theme file is applied or
        /// after the player chose them for the next start, else the theme of the themes folder, else "Custom file..." (also
        /// for a file of the themes folder that is not in the list). Never -1, so the list never shows nothing.
        /// </summary>
        /// <param name="appliedThemeFile"><see cref="IThemeService.CurrentThemeFile"/>.</param>
        /// <param name="builtInColorsChosen">The player chose the built-in colors while a theme file was applied.</param>
        /// <param name="themesDirectory">The launcher's themes folder.</param>
        /// <param name="themeNames">The themes of the themes folder in the list, from <see cref="FirstThemeIndex"/> on.</param>
        internal static int ThemeListIndex(string appliedThemeFile, bool builtInColorsChosen, string themesDirectory,
            IList<string> themeNames)
        {
            if (appliedThemeFile == null || builtInColorsChosen)
                return BuiltInThemeIndex;
            string folder = WinPath.GetParent(appliedThemeFile);
            if (folder == null || !WinPath.IsSamePath(folder, themesDirectory))
                return CustomThemeIndex;
            string name = Path.GetFileNameWithoutExtension(WinPath.GetFileName(appliedThemeFile));
            for (int i = 0; i < themeNames.Count; i++)
            {
                if (string.Equals(themeNames[i], name, StringComparison.OrdinalIgnoreCase))
                    return FirstThemeIndex + i;
            }
            return CustomThemeIndex;
        }

        /// <summary>
        /// Saves the built-in colors as the theme of the next start (<see cref="LauncherSettings.BuiltInThemeName"/>,
        /// no custom file).
        /// </summary>
        /// <returns>True if a theme file is applied now: its colors stay until the next start, because the colors of the
        /// designer cannot be restored in the open windows.</returns>
        internal static bool ChooseBuiltInColors(LauncherSettings settings, string appliedThemeFile)
        {
            settings.ThemeName = LauncherSettings.BuiltInThemeName;
            settings.CustomThemeFile = string.Empty;
            return appliedThemeFile != null;
        }

        private void themeKryptonComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (updatingThemeSelection || themeService == null || themeKryptonComboBox.SelectedIndex < 0)
                return;

            if (themeKryptonComboBox.SelectedIndex == BuiltInThemeIndex)
                UseBuiltInColors();
            else if (themeKryptonComboBox.SelectedIndex == CustomThemeIndex)
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
            builtInColorsChosen = false;
            settings.Current.ThemeName = themeName;
            settings.Current.CustomThemeFile = string.Empty;
            settings.Save();
        }

        /// <summary>The built-in colors: used from the next start on if a theme file is applied now (the message says so).</summary>
        private void UseBuiltInColors()
        {
            bool nextStart = ChooseBuiltInColors(settings.Current, themeService.CurrentThemeFile);
            // A failure to save is logged by the store.
            settings.Save();
            if (!nextStart)
                return;
            builtInColorsChosen = true;
            MessageBox.Show(this, Resources.ThemeBuiltInNextStart, Resources.LauncherTitle, MessageBoxButtons.OK,
                MessageBoxIcon.Information);
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

            builtInColorsChosen = false;
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
                gameDirectorySourceKryptonWrapLabel.Text = installations.IsWaitingForSetup
                    ? Resources.InstallationsWaitingForSetup
                    : Resources.InstallationsSearching;
                installationsHintKryptonWrapLabel.Text = string.Empty;
                LayoutPage();
                return;
            }

            Installation selected = result.Selected;
            gameDirectoryKryptonTextBox.Text = selected?.EeFolder ?? string.Empty;
            gameDirectorySourceKryptonWrapLabel.Text = Texts.InstallationOrigin(selected, result.IsSelectedByUser);
            installationsHintKryptonWrapLabel.Text = Texts.InstallationHints(result);
            LayoutPage();
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
                {
                    row.Selected = row.Tag == selected;
                    // The current cell too: without one the grid makes the first row current when it is shown, which would
                    // look like a choice of the user (with --product the selection is not always the first row).
                    if (row.Tag == selected && row.Cells.Count > 0 && row.Cells[0].Visible)
                        installationsKryptonDataGridView.CurrentCell = row.Cells[0];
                }
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
            // Only a click or a key of the user chooses (the grid also changes its selection by itself when it is shown, and
            // with --product the selected installation is not always the first row): that needs the focus.
            if (!installationsKryptonDataGridView.ContainsFocus)
                return;
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

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Forms;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Play;
using Empire_Earth_Launcher.Properties;
using Krypton.Toolkit;

namespace Empire_Earth_Launcher
{
    /// <summary>
    /// The Mods page (launcher 1.1.0, ADR 0014): the dreXmod presets of the selected installation (the folders of
    /// <c>Data\dxm\mods</c> with the name, last edit and author of their <c>CREDITS</c> file), which of them
    /// <c>dreXmod.config</c> names as the active mod and as the active lobby theme, and two buttons per game that open the folder
    /// and the config in their programs. The page only shows: the launcher neither installs nor switches a preset and writes no
    /// file; the player edits <c>dreXmod.config</c> by hand, and the page says how.
    /// </summary>
    /// <remarks>
    /// The controls are stacked in <see cref="LayoutPage"/> by <see cref="ScrollPageLayout"/> from the texts they show and the
    /// width of the page, like the Graphics page (ADR 0017); the page scrolls. What the page shows is decided by
    /// <see cref="ModsView"/>; this class only assigns it. The navigation button of the page exists only while
    /// <see cref="ModsModel.IsAvailable"/> (<see cref="MainForm"/>).
    /// </remarks>
    public partial class ModsUserControl : UserControl
    {
        /// <summary>The visibility of the controls that come and go (Visible reads false while the page is hidden).</summary>
        private readonly Dictionary<Control, bool> shown = new Dictionary<Control, bool>();

        /// <summary>Stacks the controls of the page for its width (ADR 0017).</summary>
        private readonly ScrollPageLayout stack;

        /// <summary>The blocks of the two games the page can show: heading, choice, presets and the two buttons.</summary>
        private readonly GameBlock[] blocks;

        private IThemeService themeService;
        private ModsModel model;
        private SetupWatcher setupWatcher;

        public ModsUserControl()
        {
            InitializeComponent();
            blocks = new[]
            {
                new GameBlock(modsGame1HeadingKryptonLabel, modsGame1SelectionKryptonWrapLabel, modsGame1PresetsKryptonWrapLabel,
                    modsGame1OpenFolderKryptonButton, modsGame1OpenConfigKryptonButton),
                new GameBlock(modsGame2HeadingKryptonLabel, modsGame2SelectionKryptonWrapLabel, modsGame2PresetsKryptonWrapLabel,
                    modsGame2OpenFolderKryptonButton, modsGame2OpenConfigKryptonButton),
            };
            ApplyTexts();
            stack = new ScrollPageLayout(modsScrollPanel, IsShown);
            // The window was resized (or the page is shown for the first time after it was): the texts get the new width.
            modsScrollPanel.SizeChanged += (sender, e) =>
            {
                if (stack.NeedsLayout)
                    LayoutPage();
            };
        }

        /// <summary>The controls of one game on the page.</summary>
        private sealed class GameBlock
        {
            public GameBlock(KryptonLabel heading, LauncherWrapLabel selection, LauncherWrapLabel presets, KryptonButton openFolder,
                KryptonButton openConfig)
            {
                Heading = heading;
                Selection = selection;
                Presets = presets;
                OpenFolder = openFolder;
                OpenConfig = openConfig;
            }

            public KryptonLabel Heading { get; }

            public LauncherWrapLabel Selection { get; }

            public LauncherWrapLabel Presets { get; }

            public KryptonButton OpenFolder { get; }

            public KryptonButton OpenConfig { get; }
        }

        /// <summary>
        /// Sets the texts of the page from the resources in the UI language (ADR 0009); the designer texts are
        /// placeholders. Texts that depend on the state are set in <see cref="ShowState"/>.
        /// </summary>
        private void ApplyTexts()
        {
            modsHeadingKryptonLabel.Values.Text = Resources.ModsHeading;
            modsInfoKryptonWrapLabel.Text = Resources.ModsInfo;
            modsShowTemplateKryptonCheckBox.Values.Text = Resources.ModsShowTemplates;
            modsGame1OpenFolderKryptonButton.Values.Text = Resources.ModsOpenFolderButton;
            modsGame1OpenConfigKryptonButton.Values.Text = Resources.ModsOpenConfigButton;
            modsGame2OpenFolderKryptonButton.Values.Text = Resources.ModsOpenFolderButton;
            modsGame2OpenConfigKryptonButton.Values.Text = Resources.ModsOpenConfigButton;
            modsHowToKryptonWrapLabel.Text = Resources.ModsHowTo;
            modsNoteKryptonWrapLabel.Text = Resources.ModsNote;
        }

        /// <summary>
        /// Passes the services in. The control is created by the designer, which needs a parameterless constructor, so its
        /// owner calls this right after InitializeComponent.
        /// </summary>
        /// <param name="themeService">Theme of the launcher.</param>
        /// <param name="model">The state of the page and the two buttons.</param>
        /// <param name="setupWatcher">While a setup runs, nothing is read or opened and the page says so (contract 4.2).</param>
        internal void Initialize(IThemeService themeService, ModsModel model, SetupWatcher setupWatcher)
        {
            this.themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
            this.model = model ?? throw new ArgumentNullException(nameof(model));
            this.setupWatcher = setupWatcher ?? throw new ArgumentNullException(nameof(setupWatcher));
            themeService.Register(launcherKryptonPalette, this);

            EventHandler showState = (sender, e) => ShowState();
            // The player may have edited dreXmod.config or made a preset since the page was last shown.
            EventHandler readAgain = (sender, e) =>
            {
                if (Visible)
                    model.Read();
            };
            model.Changed += showState;
            VisibleChanged += readAgain;
            Disposed += (sender, e) =>
            {
                model.Changed -= showState;
                VisibleChanged -= readAgain;
            };
            ShowState();
        }

        // --- Showing the state ------------------------------------------------------------------------------------

        private void SetShown(Control control, bool visible)
        {
            shown[control] = visible;
            control.Visible = visible;
        }

        private bool IsShown(Control control)
        {
            // Not control.Visible: the page is created hidden and the state is shown before the page is (bug report of
            // 2026-10-06, see SettingsUserControl).
            return !shown.TryGetValue(control, out bool visible) || visible;
        }

        /// <summary>
        /// Shows the state of <see cref="model"/> as <see cref="ModsView"/> describes it: the texts, which controls are shown and
        /// enabled.
        /// </summary>
        private void ShowState()
        {
            if (model == null)
                return;
            ModsView view = ModsView.Of(model, setupWatcher);

            modsInstallationKryptonWrapLabel.Text = view.Installation;
            modsStatusKryptonWrapLabel.Text = view.Status;
            SetShown(modsStatusKryptonWrapLabel, view.Status.Length > 0);

            SetShown(modsShowTemplateKryptonCheckBox, view.ShowTemplateSwitch);
            if (modsShowTemplateKryptonCheckBox.Checked != view.TemplatesShown)
            {
                // The model is the one that holds the choice; assigning it is no choice of the player.
                modsShowTemplateKryptonCheckBox.CheckedChanged -= modsShowTemplateKryptonCheckBox_CheckedChanged;
                modsShowTemplateKryptonCheckBox.Checked = view.TemplatesShown;
                modsShowTemplateKryptonCheckBox.CheckedChanged += modsShowTemplateKryptonCheckBox_CheckedChanged;
            }

            for (int i = 0; i < blocks.Length; i++)
            {
                GameBlock block = blocks[i];
                bool visible = view.ShowGames && i < view.Games.Count;
                ModsGameView game = visible ? view.Games[i] : null;
                block.Heading.Values.Text = game?.Heading ?? string.Empty;
                block.Selection.Text = game?.Selection ?? string.Empty;
                block.Presets.Text = game?.Presets ?? string.Empty;
                foreach (Control control in new Control[] { block.Heading, block.Selection, block.Presets, block.OpenFolder, block.OpenConfig })
                    SetShown(control, visible);
                block.OpenFolder.Enabled = game != null && game.OpenFolderEnabled;
                block.OpenConfig.Enabled = game != null && game.OpenConfigEnabled;
            }
            SetShown(modsHowToKryptonWrapLabel, view.ShowGames);
            SetShown(modsNoteKryptonWrapLabel, view.ShowGames);
            LayoutPage();
        }

        /// <summary>
        /// Stacks the controls that are shown from top to bottom for the width of the page (<see cref="ScrollPageLayout"/>);
        /// hidden controls take no room. Runs again when the width of the page changes.
        /// </summary>
        private void LayoutPage()
        {
            stack.Run(PlaceControls);
        }

        private void PlaceControls()
        {
            stack.Place(modsHeadingKryptonLabel);
            stack.Place(modsInfoKryptonWrapLabel);
            stack.Place(modsInstallationKryptonWrapLabel);
            stack.Place(modsStatusKryptonWrapLabel);
            stack.Place(modsShowTemplateKryptonCheckBox);
            foreach (GameBlock block in blocks)
            {
                stack.Space(ScrollPageLayout.Gap);
                stack.Place(block.Heading);
                stack.Place(block.Selection);
                stack.Place(block.Presets);
                stack.PlaceRow(block.OpenFolder, block.OpenConfig);
            }
            stack.Space(ScrollPageLayout.Gap);
            stack.Place(modsHowToKryptonWrapLabel);
            stack.Place(modsNoteKryptonWrapLabel);
        }

        // --- Actions ------------------------------------------------------------------------------------------------

        private void modsShowTemplateKryptonCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (model != null)
                model.ShowTemplates = modsShowTemplateKryptonCheckBox.Checked;
        }

        private void modsGame1OpenFolderKryptonButton_Click(object sender, EventArgs e)
        {
            Open(0, folder: true);
        }

        private void modsGame1OpenConfigKryptonButton_Click(object sender, EventArgs e)
        {
            Open(0, folder: false);
        }

        private void modsGame2OpenFolderKryptonButton_Click(object sender, EventArgs e)
        {
            Open(1, folder: true);
        }

        private void modsGame2OpenConfigKryptonButton_Click(object sender, EventArgs e)
        {
            Open(1, folder: false);
        }

        /// <summary>
        /// "Open mods folder" or "Open dreXmod.config" of the game at <paramref name="index"/> on the page: the Explorer or the
        /// program Windows has for the file, through the shell; nothing is changed. A problem is shown in a message.
        /// </summary>
        private void Open(int index, bool folder)
        {
            if (model == null || model.Snapshot == null)
                return;
            ModsView view = ModsView.Of(model, setupWatcher);
            if (index >= view.Games.Count)
                return;
            Game game = view.Games[index].Game;
            string problem = folder ? model.OpenModsFolder(game) : model.OpenConfig(game);
            if (problem != null)
                MessageBox.Show(FindForm(), string.Format(CultureInfo.CurrentCulture, Resources.ModsOpenFailedFormat, problem),
                    Resources.LauncherTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}

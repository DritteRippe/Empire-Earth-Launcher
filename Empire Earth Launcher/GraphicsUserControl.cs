using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Empire_Earth_Launcher.Core.GameSettings;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Core.Play;
using Empire_Earth_Launcher.Properties;

namespace Empire_Earth_Launcher
{
    /// <summary>
    /// The Graphics page (launcher 1.1.0, ADR 0014): the game window size of the selected installation, chosen from a list of
    /// sizes up to 1920x1080 (the only thing the page writes: the two registry values of the game window, after a backup), and
    /// the DirectX wrapper the setup installed with the screen mode keys of its <c>dgVoodoo.conf</c>, which are only shown, and
    /// how to change the wrapper in the setup.
    /// </summary>
    /// <remarks>
    /// The controls are stacked in <see cref="LayoutPage"/> by <see cref="ScrollPageLayout"/> from the texts they show and the
    /// width of the page, like the Game settings page and the Tools page (ADR 0017); the page scrolls. The work runs through
    /// <see cref="UiOperation"/> and <see cref="GraphicsModel"/>; this class only shows the state and asks for nothing: the
    /// player's click on "Use this size" is the consent of contract 3.2. While a setup runs the button is disabled and the page
    /// says why (contract 4.2).
    /// </remarks>
    public partial class GraphicsUserControl : UserControl
    {
        /// <summary>The visibility of the controls that come and go (Visible reads false while the page is hidden).</summary>
        private readonly Dictionary<Control, bool> shown = new Dictionary<Control, bool>();

        /// <summary>Stacks the controls of the page for its width (ADR 0017).</summary>
        private readonly ScrollPageLayout stack;

        /// <summary>The width of the list of sizes (the designer width; the longest entries need it in every language).</summary>
        private readonly int comboWidth;

        private IThemeService themeService;
        private GraphicsModel model;
        private SetupWatcher setupWatcher;
        private UiOperation uiOperation;

        /// <summary>The sizes of the list, in its order.</summary>
        private IReadOnlyList<ResolutionOption> offered = new ResolutionOption[0];

        /// <summary>The size the player chose in the list; kept when the list is filled again, but only for the installation it was chosen for.</summary>
        private ScreenSize chosenSize;

        /// <summary>The install root of the installation <see cref="chosenSize"/> was chosen for.</summary>
        private string chosenRoot;

        /// <summary>True while the list is filled by code, so that no choice is taken from it then.</summary>
        private bool updatingList;

        public GraphicsUserControl()
        {
            InitializeComponent();
            ApplyTexts();
            comboWidth = windowSizeKryptonComboBox.Width;
            stack = new ScrollPageLayout(graphicsScrollPanel, IsShown);
            // The window was resized (or the page is shown for the first time after it was): the texts get the new width.
            graphicsScrollPanel.SizeChanged += (sender, e) =>
            {
                if (stack.NeedsLayout)
                    LayoutPage();
            };
        }

        /// <summary>
        /// Sets the texts of the page from the resources in the UI language (ADR 0009); the designer texts are
        /// placeholders. Texts that depend on the state are set in <see cref="ShowState"/>.
        /// </summary>
        private void ApplyTexts()
        {
            windowSizeHeadingKryptonLabel.Values.Text = Resources.GraphicsWindowSizeHeading;
            windowSizeInfoKryptonWrapLabel.Text = Resources.GraphicsWindowSizeInfo;
            windowSizeKryptonLabel.Values.Text = Resources.GraphicsWindowSizeLabel;
            windowSizeApplyKryptonButton.Values.Text = Resources.GraphicsWindowSizeApplyButton;
            wrapperHeadingKryptonLabel.Values.Text = Resources.GraphicsWrapperHeading;
            wrapperNoteKryptonWrapLabel.Text = Resources.GraphicsWrapperNote;
            wrapperChangeKryptonWrapLabel.Text = Resources.GraphicsWrapperChangeInfo;
        }

        /// <summary>
        /// Passes the services in. The control is created by the designer, which needs a parameterless constructor, so its
        /// owner calls this right after InitializeComponent.
        /// </summary>
        /// <param name="themeService">Theme of the launcher.</param>
        /// <param name="model">The state of the page and the change of the window size.</param>
        /// <param name="setupWatcher">While a setup runs, nothing can be changed and the page says so (contract 4.2).</param>
        /// <param name="uiOperation">Runs the work of the page (ADR 0004).</param>
        internal void Initialize(IThemeService themeService, GraphicsModel model, SetupWatcher setupWatcher, UiOperation uiOperation)
        {
            this.themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
            this.model = model ?? throw new ArgumentNullException(nameof(model));
            this.setupWatcher = setupWatcher ?? throw new ArgumentNullException(nameof(setupWatcher));
            this.uiOperation = uiOperation ?? throw new ArgumentNullException(nameof(uiOperation));
            themeService.Register(launcherKryptonPalette, this);

            EventHandler showState = (sender, e) => ShowState();
            // The game (its own resolution option) or a repair may have changed the values since the page was last shown.
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
        /// Shows the state of <see cref="model"/> as <see cref="GraphicsView"/> describes it: the texts, which controls are shown
        /// and enabled, and the list of sizes.
        /// </summary>
        private void ShowState()
        {
            if (model == null)
                return;
            // Another installation (a click on the Play list switches the product) starts from its own sizes.
            chosenSize = GraphicsView.KeepChoice(chosenSize, chosenRoot, model.Snapshot);
            GraphicsView view = GraphicsView.Of(model, setupWatcher, chosenSize);

            windowSizeInstallationKryptonWrapLabel.Text = view.Installation;
            windowSizeCurrentKryptonWrapLabel.Text = view.CurrentSizes;
            SetShown(windowSizeCurrentKryptonWrapLabel, view.CurrentSizes.Length > 0);

            FillList(view);
            SetShown(windowSizeKryptonLabel, view.ShowChooser);
            SetShown(windowSizeKryptonComboBox, view.ShowChooser);
            SetShown(windowSizeApplyKryptonButton, view.ShowChooser);
            windowSizeKryptonComboBox.Enabled = view.ChooserEnabled;
            windowSizeApplyKryptonButton.Enabled = view.ApplyEnabled;

            windowSizeResultKryptonWrapLabel.Text = view.Result;
            SetShown(windowSizeResultKryptonWrapLabel, view.Result.Length > 0);
            windowSizeScalingKryptonWrapLabel.Text = view.Scaling;
            SetShown(windowSizeScalingKryptonWrapLabel, view.Scaling.Length > 0);

            wrapperInstalledKryptonWrapLabel.Text = view.WrapperInstalled;
            SetShown(wrapperInstalledKryptonWrapLabel, view.WrapperInstalled.Length > 0);
            wrapperConfigKryptonWrapLabel.Text = view.WrapperConfig;
            SetShown(wrapperConfigKryptonWrapLabel, view.WrapperConfig.Length > 0);
            LayoutPage();
        }

        /// <summary>
        /// Fills the list of sizes when the sizes or their texts are not the ones in it (a new read, another language of the
        /// state), and selects the size of <paramref name="view"/>.
        /// </summary>
        private void FillList(GraphicsView view)
        {
            updatingList = true;
            try
            {
                offered = view.Options;
                if (!view.OptionTexts.SequenceEqual(windowSizeKryptonComboBox.Items.Cast<object>().Select(item => (string)item)))
                {
                    windowSizeKryptonComboBox.BeginUpdate();
                    windowSizeKryptonComboBox.Items.Clear();
                    foreach (string text in view.OptionTexts)
                        windowSizeKryptonComboBox.Items.Add(text);
                    windowSizeKryptonComboBox.EndUpdate();
                }
                if (windowSizeKryptonComboBox.SelectedIndex != view.SelectedIndex)
                    windowSizeKryptonComboBox.SelectedIndex = view.SelectedIndex;
            }
            finally
            {
                updatingList = false;
            }
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
            stack.Place(windowSizeHeadingKryptonLabel);
            stack.Place(windowSizeInfoKryptonWrapLabel);
            stack.Place(windowSizeInstallationKryptonWrapLabel);
            stack.Place(windowSizeCurrentKryptonWrapLabel);
            stack.PlaceField(windowSizeKryptonLabel, stack.NaturalSize(windowSizeKryptonLabel).Width, windowSizeKryptonComboBox,
                comboWidth);
            stack.Place(windowSizeApplyKryptonButton);
            stack.Place(windowSizeResultKryptonWrapLabel);
            stack.Place(windowSizeScalingKryptonWrapLabel);

            stack.Space(ScrollPageLayout.Gap);
            stack.Place(wrapperHeadingKryptonLabel);
            stack.Place(wrapperInstalledKryptonWrapLabel);
            stack.Place(wrapperConfigKryptonWrapLabel);
            stack.Place(wrapperNoteKryptonWrapLabel);
            stack.Place(wrapperChangeKryptonWrapLabel);
        }

        // --- Actions ------------------------------------------------------------------------------------------------

        /// <summary>A size was chosen in the list: it is kept across the next reads, and the button follows it.</summary>
        private void windowSizeKryptonComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (updatingList || model == null)
                return;
            int index = windowSizeKryptonComboBox.SelectedIndex;
            if (index >= 0 && index < offered.Count)
            {
                chosenSize = offered[index].Size;
                chosenRoot = model.Snapshot?.Installation.Root;
            }
            ShowState();
        }

        /// <summary>
        /// "Use this size": the player's explicit choice, the consent of contract 3.2 to overwrite the two display values. The
        /// mutation guard, the backup and the write are in the core.
        /// </summary>
        private void windowSizeApplyKryptonButton_Click(object sender, EventArgs e)
        {
            int index = windowSizeKryptonComboBox.SelectedIndex;
            if (model == null || index < 0 || index >= offered.Count || !model.CanChange)
                return;
            ScreenSize size = offered[index].Size;
            uiOperation.Run(windowSizeApplyKryptonButton, () => model.ApplyResolutionAsync(size), ShowState);
        }
    }
}

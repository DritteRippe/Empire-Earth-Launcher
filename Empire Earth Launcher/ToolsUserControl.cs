using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Integrity;
using Empire_Earth_Launcher.Core.Play;
using Empire_Earth_Launcher.Core.Repair;
using Empire_Earth_Launcher.Properties;
using Krypton.Toolkit;

namespace Empire_Earth_Launcher
{
    /// <summary>
    /// The Tools page (L-WP7): the integrity check of the selected installation with its explanation and files (contract
    /// 2.5), the full check with progress and cancel, the repair advice (contract 4.4), and the version check of the game
    /// and the setup (contract 4.5). The maintenance tools and the network diagnostics follow in L-WP8 and L-WP9.
    /// </summary>
    /// <remarks>
    /// The controls are stacked in <see cref="LayoutPage"/> from the texts they show, so longer translations push the
    /// following controls down; the page scrolls. The work runs through <see cref="UiOperation"/>,
    /// <see cref="IntegrityModel"/> and <see cref="UpdateModel"/>; this class only shows their state.
    /// </remarks>
    public partial class ToolsUserControl : UserControl
    {
        private const int Gap = 6;

        /// <summary>The visibility of the controls that come and go (Visible reads false while the page is hidden).</summary>
        private readonly Dictionary<Control, bool> shown = new Dictionary<Control, bool>();

        private IThemeService themeService;
        private IntegrityModel integrity;
        private UpdateModel updates;
        private UiOperation uiOperation;

        public ToolsUserControl()
        {
            InitializeComponent();
            ApplyTexts();
        }

        /// <summary>
        /// Sets the texts of the page from the resources in the UI language (ADR 0009); the designer texts are
        /// placeholders. Texts that depend on the state are set in <see cref="ShowState"/>.
        /// </summary>
        private void ApplyTexts()
        {
            filesHeadingKryptonLabel.Values.Text = Resources.ToolsFilesHeading;
            integrityInfoKryptonWrapLabel.Text = Resources.IntegrityCheckInfo;
            fullCheckKryptonButton.Values.Text = Resources.FullCheckButton;
            cancelCheckKryptonButton.Values.Text = Resources.CancelCheckButton;
            repairAdviceKryptonButton.Values.Text = Resources.RepairAdviceButton;
            updatesHeadingKryptonLabel.Values.Text = Resources.ToolsUpdatesHeading;
            versionInfoKryptonWrapLabel.Text = Resources.VersionCheckInfo;
            versionCheckKryptonButton.Values.Text = Resources.VersionCheckToolsButton;
        }

        /// <summary>
        /// Passes the services in. The control is created by the designer, which needs a parameterless constructor, so its
        /// owner calls this right after InitializeComponent.
        /// </summary>
        /// <param name="themeService">Theme of the launcher.</param>
        /// <param name="integrity">The integrity check of the selected installation.</param>
        /// <param name="updates">The update API: version check and the download of the repair advice.</param>
        /// <param name="setupWatcher">While a setup runs, no check starts and the page says so (contract 4.2).</param>
        /// <param name="uiOperation">Runs the work of the page (ADR 0004).</param>
        internal void Initialize(IThemeService themeService, IntegrityModel integrity, UpdateModel updates,
            SetupWatcher setupWatcher, UiOperation uiOperation)
        {
            if (setupWatcher == null)
                throw new ArgumentNullException(nameof(setupWatcher));
            this.themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
            this.integrity = integrity ?? throw new ArgumentNullException(nameof(integrity));
            this.updates = updates ?? throw new ArgumentNullException(nameof(updates));
            this.uiOperation = uiOperation ?? throw new ArgumentNullException(nameof(uiOperation));
            themeService.Register(launcherKryptonPalette, this);

            EventHandler showState = (sender, e) => ShowState();
            EventHandler<SetupStateEventArgs> showSetup = (sender, e) => ShowState();
            integrity.Changed += showState;
            updates.Changed += showState;
            setupWatcher.SetupStarted += showSetup;
            setupWatcher.SetupFinished += showSetup;
            Disposed += (sender, e) =>
            {
                integrity.Changed -= showState;
                updates.Changed -= showState;
                setupWatcher.SetupStarted -= showSetup;
                setupWatcher.SetupFinished -= showSetup;
            };
            ShowState();
        }

        /// <summary>
        /// Shows the state of the models: the installation, the state of the check (or its progress) with the files, which
        /// buttons work, and the result of the version check.
        /// </summary>
        private void ShowState()
        {
            if (integrity == null)
                return;
            Installation selected = integrity.Selected;
            IntegrityReport report = integrity.Report;

            integrityInstallationKryptonLabel.Values.Text = Texts.GameSettingsInstallation(selected);
            integrityStateKryptonWrapLabel.Text = StateText(selected, report);
            bool fullCheck = integrity.RunningCheck == IntegrityCheckKind.Full;
            SetShown(integrityProgressBar, fullCheck);
            integrityProgressBar.Value = fullCheck && integrity.Progress != null ? integrity.Progress.Percent : 0;
            string files = report != null && !integrity.IsChecking ? Texts.IntegrityFiles(report) : string.Empty;
            integrityFilesKryptonTextBox.Text = files;
            SetShown(integrityFilesKryptonTextBox, files.Length > 0);

            fullCheckKryptonButton.Enabled = integrity.CanStartFullCheck;
            cancelCheckKryptonButton.Enabled = fullCheck;
            repairAdviceKryptonButton.Enabled = selected != null;

            versionResultKryptonWrapLabel.Text = updates.IsChecking
                ? Resources.VersionChecking
                : Texts.VersionResults(updates.GameResult, updates.SetupResult);
            versionCheckKryptonButton.Enabled = updates.CanCheck;
            LayoutPage();
        }

        /// <summary>The state line: the setup that runs, the progress, "checking", the explanation of the report, or nothing.</summary>
        private string StateText(Installation selected, IntegrityReport report)
        {
            if (selected == null)
                return string.Empty;
            if (integrity.RunningCheck == IntegrityCheckKind.Full)
                return integrity.Progress == null ? Resources.IntegrityChecking : Texts.IntegrityProgress(integrity.Progress);
            if (integrity.IsChecking || report == null)
                return integrity.IsChecking ? Resources.IntegrityChecking : string.Empty;
            return Texts.IntegrityExplanation(report);
        }

        private void SetShown(Control control, bool visible)
        {
            shown[control] = visible;
            control.Visible = visible;
        }

        private bool IsShown(Control control)
        {
            return !shown.TryGetValue(control, out bool visible) || visible;
        }

        /// <summary>Stacks the controls from their texts; hidden controls take no room.</summary>
        private void LayoutPage()
        {
            toolsScrollPanel.SuspendLayout();
            int scroll = toolsScrollPanel.AutoScrollPosition.Y;
            int y = 8;

            void Place(Control control)
            {
                if (!IsShown(control))
                    return;
                if (control is KryptonWrapLabel label)
                    label.Height = TextHeight(label, label.Width);
                control.Top = y + scroll;
                y += control.Height + Gap;
            }

            void PlaceRow(params Control[] controls)
            {
                int height = 0;
                foreach (Control control in controls)
                {
                    control.Top = y + scroll;
                    height = Math.Max(height, control.Height);
                }
                y += height + Gap;
            }

            Place(filesHeadingKryptonLabel);
            Place(integrityInfoKryptonWrapLabel);
            Place(integrityInstallationKryptonLabel);
            Place(integrityStateKryptonWrapLabel);
            Place(integrityProgressBar);
            Place(integrityFilesKryptonTextBox);
            PlaceRow(fullCheckKryptonButton, cancelCheckKryptonButton, repairAdviceKryptonButton);

            y += Gap;
            Place(updatesHeadingKryptonLabel);
            Place(versionInfoKryptonWrapLabel);
            Place(versionResultKryptonWrapLabel);
            Place(versionCheckKryptonButton);
            toolsScrollPanel.ResumeLayout(true);
        }

        /// <summary>
        /// The height of a wrapping label for its text at <paramref name="width"/>: GDI+ text, as the launcher draws it
        /// (compatible text rendering), measured at 96 DPI like the DPI-unaware launcher, without creating a window handle.
        /// </summary>
        private static int TextHeight(Control label, int width)
        {
            if (string.IsNullOrEmpty(label.Text))
                return 0;
            using (var bitmap = new Bitmap(1, 1))
            using (Graphics graphics = Graphics.FromImage(bitmap))
                return (int)Math.Ceiling(graphics.MeasureString(label.Text, label.Font, width).Height) + 6;
        }

        // --- Actions ------------------------------------------------------------------------------------------------

        /// <summary>The full check on request (contract 2.5); the page shows its progress.</summary>
        private void fullCheckKryptonButton_Click(object sender, EventArgs e)
        {
            if (integrity.CanStartFullCheck)
                uiOperation.Run(fullCheckKryptonButton, () => integrity.StartFullCheckAsync());
        }

        private void cancelCheckKryptonButton_Click(object sender, EventArgs e)
        {
            integrity.CancelCheck();
        }

        /// <summary>
        /// The repair advice of the selected installation (contract 4.4): with the files of the report when it offers the
        /// repair, else the advice on request.
        /// </summary>
        private void repairAdviceKryptonButton_Click(object sender, EventArgs e)
        {
            RepairAdvice advice = integrity.CreateRepairAdvice();
            if (advice == null)
                return;
            RepairAdviceDialog.ShowAdvice(FindForm(), themeService, advice, Texts.RepairReasonText(advice, integrity.Report),
                updates, uiOperation);
        }

        /// <summary>The version check of the game and the setup (contract 4.5); an available update opens the hand-off.</summary>
        private void versionCheckKryptonButton_Click(object sender, EventArgs e)
        {
            uiOperation.Run(versionCheckKryptonButton, () => CheckVersionsAsync(this, true, updates, themeService, uiOperation));
        }

        /// <summary>
        /// Runs the version check and, if a newer version is available, shows the hand-off of contract 4.3 with the version
        /// as its reason (contract 4.5). Shared by the Tools and the Play page.
        /// </summary>
        internal static async Task CheckVersionsAsync(Control page, bool includeSetup, UpdateModel updates,
            IThemeService themeService, UiOperation uiOperation)
        {
            await updates.CheckAsync(includeSetup);
            RepairAdvice advice = updates.UpdateAdvice;
            if (advice != null && !page.IsDisposed)
                RepairAdviceDialog.ShowAdvice(page.FindForm(), themeService, advice, Texts.RepairReasonText(advice, null), updates,
                    uiOperation);
        }
    }
}

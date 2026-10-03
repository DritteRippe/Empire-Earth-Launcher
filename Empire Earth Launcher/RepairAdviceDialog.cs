using System;
using System.Drawing;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Empire_Earth_Launcher.Core.Repair;
using Empire_Earth_Launcher.Properties;
using Krypton.Toolkit;

namespace Empire_Earth_Launcher
{
    /// <summary>
    /// The repair advice of contract 4.4 (R9): why the installation needs the setup (the missing program, the files of the
    /// integrity check with the antivirus remark of contract 2.5, the available version), the numbered steps, and the
    /// download of contract 4.3 with a button that opens it in the browser (through the shell, not elevated) and its address
    /// to copy. The launcher never downloads or starts the setup itself (contract 4.1).
    /// </summary>
    /// <remarks>
    /// <para>
    /// When the window is shown it asks the update API for the download of the current setup (contract 4.3 steps 1 to 3,
    /// L-WP7); "Open download page" waits for the answer, at most the 10 seconds of the HTTPS client. A fixed page used
    /// because the API gave no address is said below the address (no silent fallback, ARCHITECTURE 6). Closing the window
    /// cancels the request.
    /// </para>
    /// <para>
    /// Built in code without a designer file: the texts have very different lengths in English, German and French, so the
    /// labels wrap at a fixed width and the window grows with them (<see cref="Form.AutoSize"/>). Show it with
    /// <see cref="ShowAdvice"/>.
    /// </para>
    /// </remarks>
    internal sealed class RepairAdviceDialog : KryptonForm
    {
        private const int ContentWidth = 480;

        private readonly KryptonPalette launcherKryptonPalette = new KryptonPalette();
        private readonly TableLayoutPanel layoutPanel = new TableLayoutPanel();
        private readonly LauncherWrapLabel adviceKryptonWrapLabel = new LauncherWrapLabel();
        private readonly LauncherWrapLabel pageKryptonWrapLabel = new LauncherWrapLabel();
        private readonly KryptonTextBox pageKryptonTextBox = new KryptonTextBox();
        private readonly LauncherWrapLabel fallbackKryptonWrapLabel = new LauncherWrapLabel();
        private readonly LauncherWrapLabel resultKryptonWrapLabel = new LauncherWrapLabel();
        private readonly FlowLayoutPanel buttonsPanel = new FlowLayoutPanel();
        private readonly KryptonButton openPageKryptonButton = new KryptonButton();
        private readonly KryptonButton closeKryptonButton = new KryptonButton();

        /// <summary>Cancels the request to the update API when the window closes.</summary>
        private readonly CancellationTokenSource closing = new CancellationTokenSource();

        private readonly UpdateModel updates;
        private readonly UiOperation uiOperation;
        private RepairAdvice advice;

        /// <param name="themeService">Theme of the launcher.</param>
        /// <param name="advice">The advice of the core, with the fixed page until the update API answered.</param>
        /// <param name="reason">Why the advice is shown (already in the UI language), e.g. the missing program or the files of
        /// the integrity check; null for none.</param>
        /// <param name="updates">Asks the update API for the download and opens the page (contract 4.3).</param>
        /// <param name="uiOperation">Runs the request (ADR 0004).</param>
        public RepairAdviceDialog(IThemeService themeService, RepairAdvice advice, string reason, UpdateModel updates,
            UiOperation uiOperation)
        {
            if (themeService == null)
                throw new ArgumentNullException(nameof(themeService));
            this.advice = advice ?? throw new ArgumentNullException(nameof(advice));
            this.updates = updates ?? throw new ArgumentNullException(nameof(updates));
            this.uiOperation = uiOperation ?? throw new ArgumentNullException(nameof(uiOperation));
            InitializeComponent();
            ApplyTexts(reason);
            themeService.Register(launcherKryptonPalette, this);
            Shown += (sender, e) => this.uiOperation.Run(openPageKryptonButton, LocateAsync);
            FormClosed += (sender, e) => closing.Cancel();
            Disposed += (sender, e) => closing.Dispose();
        }

        /// <summary>Shows the advice as a modal window of <paramref name="owner"/>.</summary>
        internal static void ShowAdvice(IWin32Window owner, IThemeService themeService, RepairAdvice advice, string reason,
            UpdateModel updates, UiOperation uiOperation)
        {
            using (var dialog = new RepairAdviceDialog(themeService, advice, reason, updates, uiOperation))
                dialog.ShowDialog(owner);
        }

        private void InitializeComponent()
        {
            SuspendLayout();
            Palette = launcherKryptonPalette;
            PaletteMode = PaletteMode.Custom;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.Font;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(12);

            foreach (LauncherWrapLabel label in new[]
                     { adviceKryptonWrapLabel, pageKryptonWrapLabel, fallbackKryptonWrapLabel, resultKryptonWrapLabel })
            {
                label.AutoSize = true;
                label.MaximumSize = new Size(ContentWidth, 0);
                label.MinimumSize = new Size(ContentWidth, 0);
                label.LabelStyle = LabelStyle.NormalControl;
                label.Margin = new Padding(0, 0, 0, 8);
            }
            fallbackKryptonWrapLabel.Visible = false;
            resultKryptonWrapLabel.Visible = false;

            pageKryptonTextBox.ReadOnly = true;
            pageKryptonTextBox.Width = ContentWidth;
            pageKryptonTextBox.Margin = new Padding(0, 0, 0, 12);

            foreach (KryptonButton button in new[] { closeKryptonButton, openPageKryptonButton })
            {
                button.AutoSize = true;
                button.MinimumSize = new Size(150, 30);
                buttonsPanel.Controls.Add(button);
            }
            openPageKryptonButton.Name = nameof(openPageKryptonButton);
            closeKryptonButton.DialogResult = DialogResult.Cancel;
            openPageKryptonButton.Click += openPageKryptonButton_Click;
            AcceptButton = openPageKryptonButton;
            CancelButton = closeKryptonButton;

            buttonsPanel.FlowDirection = FlowDirection.RightToLeft;
            buttonsPanel.AutoSize = true;
            buttonsPanel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            buttonsPanel.Anchor = AnchorStyles.Right;
            buttonsPanel.Margin = Padding.Empty;

            layoutPanel.AutoSize = true;
            layoutPanel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            layoutPanel.ColumnCount = 1;
            layoutPanel.BackColor = Color.Transparent;
            layoutPanel.Location = new Point(12, 12);
            layoutPanel.Controls.Add(adviceKryptonWrapLabel);
            layoutPanel.Controls.Add(pageKryptonWrapLabel);
            layoutPanel.Controls.Add(pageKryptonTextBox);
            layoutPanel.Controls.Add(fallbackKryptonWrapLabel);
            layoutPanel.Controls.Add(resultKryptonWrapLabel);
            layoutPanel.Controls.Add(buttonsPanel);
            Controls.Add(layoutPanel);
            ResumeLayout(true);
        }

        /// <summary>Sets the texts in the UI language (ADR 0009); the steps come from <see cref="Texts.RepairSteps"/>.</summary>
        private void ApplyTexts(string reason)
        {
            Text = Resources.RepairAdviceTitle;
            string steps = Texts.RepairSteps(advice);
            adviceKryptonWrapLabel.Text = string.IsNullOrEmpty(reason)
                ? steps
                : reason + Environment.NewLine + Environment.NewLine + steps;
            pageKryptonWrapLabel.Text = Resources.RepairLocating;
            pageKryptonTextBox.Text = string.Empty;
            openPageKryptonButton.Values.Text = Resources.RepairOpenPageButton;
            closeKryptonButton.Values.Text = Resources.RepairCloseButton;
        }

        /// <summary>
        /// Asks the update API for the download (contract 4.3); the button to open it is disabled meanwhile
        /// (<see cref="UiOperation"/>). Every failure gives the fixed page with the reason, a closed window cancels it.
        /// </summary>
        private async Task LocateAsync()
        {
            RepairAdvice located = await updates.LocateAsync(advice, closing.Token);
            if (IsDisposed)
                return;
            advice = located;
            pageKryptonWrapLabel.Text = Resources.RepairDownloadPageLabel;
            pageKryptonTextBox.Text = advice.DownloadUrl;
            string fallback = Texts.DownloadFallback(advice.Location);
            fallbackKryptonWrapLabel.Text = fallback ?? string.Empty;
            fallbackKryptonWrapLabel.Visible = fallback != null;
        }

        /// <summary>
        /// Opens the download page; the window stays open, so that the steps can be read while the setup downloads. If the
        /// browser cannot be opened, the address is to be copied.
        /// </summary>
        private void openPageKryptonButton_Click(object sender, EventArgs e)
        {
            bool opened = updates.OpenDownloadPage(advice) == DownloadPageResult.Opened;
            resultKryptonWrapLabel.Text = opened
                ? string.Empty
                : string.Format(CultureInfo.CurrentCulture, Resources.RepairPageNotOpenedFormat, advice.DownloadUrl);
            resultKryptonWrapLabel.Visible = !opened;
            if (!opened)
                pageKryptonTextBox.Focus();
        }
    }
}

using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using Empire_Earth_Launcher.Core.Repair;
using Empire_Earth_Launcher.Properties;
using Krypton.Toolkit;

namespace Empire_Earth_Launcher
{
    /// <summary>
    /// The repair advice of contract 4.4 (R9): why the installation needs the setup, the numbered steps, and the download
    /// page of contract 4.3 with a button that opens it in the browser (through the shell, not elevated) and its address to
    /// copy. The launcher never downloads or starts the setup itself (contract 4.1).
    /// </summary>
    /// <remarks>
    /// Built in code without a designer file: the texts have very different lengths in English, German and French, so the
    /// labels wrap at a fixed width and the window grows with them (<see cref="Form.AutoSize"/>). Show it with
    /// <see cref="Form.ShowDialog(IWin32Window)"/> in a using block.
    /// </remarks>
    internal sealed class RepairAdviceDialog : KryptonForm
    {
        private const int ContentWidth = 480;

        private readonly KryptonPalette launcherKryptonPalette = new KryptonPalette();
        private readonly TableLayoutPanel layoutPanel = new TableLayoutPanel();
        private readonly KryptonWrapLabel adviceKryptonWrapLabel = new KryptonWrapLabel();
        private readonly KryptonWrapLabel pageKryptonWrapLabel = new KryptonWrapLabel();
        private readonly KryptonTextBox pageKryptonTextBox = new KryptonTextBox();
        private readonly KryptonWrapLabel resultKryptonWrapLabel = new KryptonWrapLabel();
        private readonly FlowLayoutPanel buttonsPanel = new FlowLayoutPanel();
        private readonly KryptonButton openPageKryptonButton = new KryptonButton();
        private readonly KryptonButton closeKryptonButton = new KryptonButton();

        private readonly RepairAdvice advice;
        private readonly Func<RepairAdvice, DownloadPageResult> openPage;

        /// <param name="themeService">Theme of the launcher.</param>
        /// <param name="advice">The advice of the core.</param>
        /// <param name="reason">Why the advice is shown (already in the UI language), e.g. the missing program; null for none.</param>
        /// <param name="openPage">Opens the download page (<see cref="PlayModel.OpenDownloadPage"/>).</param>
        public RepairAdviceDialog(IThemeService themeService, RepairAdvice advice, string reason,
            Func<RepairAdvice, DownloadPageResult> openPage)
        {
            if (themeService == null)
                throw new ArgumentNullException(nameof(themeService));
            this.advice = advice ?? throw new ArgumentNullException(nameof(advice));
            this.openPage = openPage ?? throw new ArgumentNullException(nameof(openPage));
            InitializeComponent();
            ApplyTexts(reason);
            themeService.Register(launcherKryptonPalette, this);
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

            foreach (KryptonWrapLabel label in new[] { adviceKryptonWrapLabel, pageKryptonWrapLabel, resultKryptonWrapLabel })
            {
                label.AutoSize = true;
                label.MaximumSize = new Size(ContentWidth, 0);
                label.MinimumSize = new Size(ContentWidth, 0);
                label.LabelStyle = LabelStyle.NormalControl;
                label.Margin = new Padding(0, 0, 0, 8);
            }
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
            pageKryptonWrapLabel.Text = Resources.RepairDownloadPageLabel;
            pageKryptonTextBox.Text = advice.DownloadUrl;
            openPageKryptonButton.Values.Text = Resources.RepairOpenPageButton;
            closeKryptonButton.Values.Text = Resources.RepairCloseButton;
        }

        /// <summary>
        /// Opens the download page; the window stays open, so that the steps can be read while the setup downloads. If the
        /// browser cannot be opened, the address is to be copied.
        /// </summary>
        private void openPageKryptonButton_Click(object sender, EventArgs e)
        {
            bool opened = openPage(advice) == DownloadPageResult.Opened;
            resultKryptonWrapLabel.Text = opened
                ? string.Empty
                : string.Format(CultureInfo.CurrentCulture, Resources.RepairPageNotOpenedFormat, advice.DownloadUrl);
            resultKryptonWrapLabel.Visible = !opened;
            if (!opened)
                pageKryptonTextBox.Focus();
        }
    }
}

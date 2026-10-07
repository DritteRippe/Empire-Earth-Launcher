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
    /// The repair advice of contract 4.4 (R9): why the installation needs the setup (the missing program, the files of the
    /// integrity check with the antivirus remark of contract 2.5, the available version), the numbered steps, and the
    /// download of contract 4.3 with a button that opens it in the browser (through the shell, not elevated) and its address
    /// to copy. The launcher never downloads or starts the setup itself (contract 4.1).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The address is the download page of the product (contract 4.3), known from the start: the window shows it at once and
    /// makes no request; the website redirects the browser to the setup.
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
        private readonly LauncherWrapLabel resultKryptonWrapLabel = new LauncherWrapLabel();
        private readonly FlowLayoutPanel buttonsPanel = new FlowLayoutPanel();
        private readonly KryptonButton openPageKryptonButton = new KryptonButton();
        private readonly KryptonButton openSuiteFolderKryptonButton = new KryptonButton();
        private readonly KryptonButton closeKryptonButton = new KryptonButton();

        private readonly UpdateModel updates;
        private readonly RepairAdvice advice;

        /// <param name="themeService">Theme of the launcher.</param>
        /// <param name="advice">The advice of the core, with the download page of the product.</param>
        /// <param name="reason">Why the advice is shown (already in the UI language), e.g. the missing program or the files of
        /// the integrity check; null for none.</param>
        /// <param name="updates">Opens the download page (contract 4.3).</param>
        public RepairAdviceDialog(IThemeService themeService, RepairAdvice advice, string reason, UpdateModel updates)
        {
            if (themeService == null)
                throw new ArgumentNullException(nameof(themeService));
            this.advice = advice ?? throw new ArgumentNullException(nameof(advice));
            this.updates = updates ?? throw new ArgumentNullException(nameof(updates));
            InitializeComponent();
            ApplyTexts(reason);
            themeService.Register(launcherKryptonPalette, this);
        }

        /// <summary>Shows the advice as a modal window of <paramref name="owner"/>.</summary>
        internal static void ShowAdvice(IWin32Window owner, IThemeService themeService, RepairAdvice advice, string reason,
            UpdateModel updates)
        {
            using (var dialog = new RepairAdviceDialog(themeService, advice, reason, updates))
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
                     { adviceKryptonWrapLabel, pageKryptonWrapLabel, resultKryptonWrapLabel })
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

            foreach (KryptonButton button in new[] { closeKryptonButton, openPageKryptonButton, openSuiteFolderKryptonButton })
            {
                button.AutoSize = true;
                button.MinimumSize = new Size(150, 30);
                buttonsPanel.Controls.Add(button);
            }
            openPageKryptonButton.Name = nameof(openPageKryptonButton);
            closeKryptonButton.DialogResult = DialogResult.Cancel;
            openPageKryptonButton.Click += openPageKryptonButton_Click;
            openSuiteFolderKryptonButton.Name = nameof(openSuiteFolderKryptonButton);
            openSuiteFolderKryptonButton.Click += openSuiteFolderKryptonButton_Click;
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
            pageKryptonWrapLabel.Text = advice.SuiteFolder != null
                ? Resources.RepairDownloadPageSuiteLabel
                : Resources.RepairDownloadPageLabel;
            pageKryptonTextBox.Text = advice.DownloadUrl;
            openPageKryptonButton.Values.Text = Resources.RepairOpenPageButton;
            // The suite step (contract 4.4, revision 4): the button opens the folder of the suite setup in the Explorer and
            // starts nothing; the download page below stays the second option.
            openSuiteFolderKryptonButton.Values.Text = Resources.RepairOpenSuiteFolderButton;
            openSuiteFolderKryptonButton.Visible = advice.SuiteFolder != null;
            if (advice.SuiteFolder != null)
                AcceptButton = openSuiteFolderKryptonButton;
            closeKryptonButton.Values.Text = Resources.RepairCloseButton;
        }

        /// <summary>Opens the folder of the suite setup in the Explorer; the window stays open (contract 4.4: never a program).</summary>
        private void openSuiteFolderKryptonButton_Click(object sender, EventArgs e)
        {
            bool opened = updates.OpenSuiteFolder(advice);
            resultKryptonWrapLabel.Text = opened
                ? string.Empty
                : string.Format(CultureInfo.CurrentCulture, Resources.RepairSuiteFolderNotOpenedFormat, advice.SuiteFolder);
            resultKryptonWrapLabel.Visible = !opened;
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

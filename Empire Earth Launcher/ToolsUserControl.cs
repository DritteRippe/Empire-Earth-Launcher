using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Diagnostics;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Integrity;
using Empire_Earth_Launcher.Core.Maintenance;
using Empire_Earth_Launcher.Core.Play;
using Empire_Earth_Launcher.Core.Repair;
using Empire_Earth_Launcher.Properties;
using Krypton.Toolkit;

namespace Empire_Earth_Launcher
{
    /// <summary>
    /// The Tools page: the integrity check of the selected installation with its explanation and files (contract 2.5), the
    /// full check with progress and cancel, the repair advice (contract 4.4), and the version check of the game and the setup
    /// (contract 4.5) (L-WP7); the maintenance tools of L-WP8: the registry cleanup (R5), the WON login reset (R6), the
    /// VirtualStore check (R8), saved games and scenarios and the name check (R10), and "Open backup folder" (ADR 0007); the
    /// network diagnostics with the outage hint (R7) and the diagnostics report, copied or saved, never sent (L-WP9).
    /// </summary>
    /// <remarks>
    /// The controls are stacked in <see cref="LayoutPage"/> from the texts they show, so longer translations push the
    /// following controls down; the page scrolls. The work runs through <see cref="UiOperation"/>,
    /// <see cref="IntegrityModel"/>, <see cref="UpdateModel"/> and <see cref="MaintenanceModel"/>; this class only shows
    /// their state and asks the player (folders, files, confirmations).
    /// </remarks>
    public partial class ToolsUserControl : UserControl
    {
        private const int Gap = 6;

        /// <summary>The visibility of the controls that come and go (Visible reads false while the page is hidden).</summary>
        private readonly Dictionary<Control, bool> shown = new Dictionary<Control, bool>();

        private IThemeService themeService;
        private IntegrityModel integrity;
        private UpdateModel updates;
        private MaintenanceModel maintenance;
        private DiagnosticsModel diagnostics;
        private UiOperation uiOperation;

        /// <summary>The registry cleanup as shown, so that the check boxes map to its keys.</summary>
        private CleanupView cleanupView;

        /// <summary>The scan whose keys the list shows; the list is filled again only for a new scan.</summary>
        private CleanupScan shownCleanupScan;

        /// <summary>The result lines of the last actions; empty until one ran.</summary>
        private string cleanupResult = string.Empty;
        private string wonResult = string.Empty;
        private string savesResult = string.Empty;
        private string reportResult = string.Empty;

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

            cleanupHeadingKryptonLabel.Values.Text = Resources.ToolsCleanupHeading;
            cleanupInfoKryptonWrapLabel.Text = Resources.CleanupInfo;
            cleanupDeleteKryptonButton.Values.Text = Resources.CleanupDeleteButton;
            wonHeadingKryptonLabel.Values.Text = Resources.ToolsWonHeading;
            wonInfoKryptonWrapLabel.Text = Resources.WonInfo;
            wonResetKryptonButton.Values.Text = Resources.WonResetButton;
            virtualStoreHeadingKryptonLabel.Values.Text = Resources.ToolsVirtualStoreHeading;
            virtualStoreInfoKryptonWrapLabel.Text = Resources.VirtualStoreInfo;
            savesHeadingKryptonLabel.Values.Text = Resources.ToolsSavesHeading;
            savesInfoKryptonWrapLabel.Text = Resources.SavesInfo;
            exportSavesKryptonButton.Values.Text = Resources.ExportSavesButton;
            importEeSavesKryptonButton.Values.Text = Resources.ImportEeSavesButton;
            importAocSavesKryptonButton.Values.Text = Resources.ImportAocSavesButton;
            namesHeadingKryptonLabel.Values.Text = Resources.ToolsNamesHeading;
            namesInfoKryptonWrapLabel.Text = Resources.NamesInfo;
            backupsHeadingKryptonLabel.Values.Text = Resources.ToolsBackupsHeading;
            openBackupFolderKryptonButton.Values.Text = Resources.OpenBackupFolderButton;

            networkHeadingKryptonLabel.Values.Text = Resources.ToolsNetworkHeading;
            networkInfoKryptonWrapLabel.Text = Resources.NetworkInfo;
            networkCheckKryptonButton.Values.Text = Resources.NetworkCheckButton;
            reportHeadingKryptonLabel.Values.Text = Resources.ToolsReportHeading;
            reportInfoKryptonWrapLabel.Text = Resources.ReportInfo;
            copyReportKryptonButton.Values.Text = Resources.CopyReportButton;
            saveReportKryptonButton.Values.Text = Resources.SaveReportButton;
        }

        /// <summary>
        /// Passes the services in. The control is created by the designer, which needs a parameterless constructor, so its
        /// owner calls this right after InitializeComponent.
        /// </summary>
        /// <param name="themeService">Theme of the launcher.</param>
        /// <param name="integrity">The integrity check of the selected installation.</param>
        /// <param name="updates">The update API: version check and the download of the repair advice.</param>
        /// <param name="setupWatcher">While a setup runs, no check starts and the page says so (contract 4.2).</param>
        /// <param name="maintenance">The maintenance tools of L-WP8.</param>
        /// <param name="diagnostics">The network diagnostics and the diagnostics report of L-WP9.</param>
        /// <param name="uiOperation">Runs the work of the page (ADR 0004).</param>
        internal void Initialize(IThemeService themeService, IntegrityModel integrity, UpdateModel updates,
            SetupWatcher setupWatcher, MaintenanceModel maintenance, DiagnosticsModel diagnostics, UiOperation uiOperation)
        {
            if (setupWatcher == null)
                throw new ArgumentNullException(nameof(setupWatcher));
            this.themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
            this.integrity = integrity ?? throw new ArgumentNullException(nameof(integrity));
            this.updates = updates ?? throw new ArgumentNullException(nameof(updates));
            this.maintenance = maintenance ?? throw new ArgumentNullException(nameof(maintenance));
            this.diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
            this.uiOperation = uiOperation ?? throw new ArgumentNullException(nameof(uiOperation));
            themeService.Register(launcherKryptonPalette, this);

            EventHandler showState = (sender, e) => ShowState();
            EventHandler<SetupStateEventArgs> showSetup = (sender, e) => ShowState();
            integrity.Changed += showState;
            updates.Changed += showState;
            maintenance.Changed += showState;
            diagnostics.Changed += showState;
            setupWatcher.SetupStarted += showSetup;
            setupWatcher.SetupFinished += showSetup;
            Disposed += (sender, e) =>
            {
                integrity.Changed -= showState;
                updates.Changed -= showState;
                maintenance.Changed -= showState;
                diagnostics.Changed -= showState;
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
            ShowMaintenance();
            ShowDiagnostics();
            LayoutPage();
        }

        /// <summary>
        /// The network diagnostics (the verdict, the hints and the details of the latest check, which runs only on request) and
        /// the result of the last copy or save of the report with the text that left the launcher.
        /// </summary>
        private void ShowDiagnostics()
        {
            NetworkReport network = diagnostics.Network;
            networkCheckKryptonButton.Enabled = !diagnostics.IsChecking;
            networkVerdictKryptonWrapLabel.Text = diagnostics.IsChecking ? Resources.NetworkChecking
                : network == null ? string.Empty : Texts.NetworkVerdict(network);
            string hints = network == null || diagnostics.IsChecking ? string.Empty : Texts.NetworkHints(network);
            networkHintsKryptonWrapLabel.Text = hints;
            SetShown(networkHintsKryptonWrapLabel, hints.Length > 0);
            string details = network == null || diagnostics.IsChecking ? string.Empty : Texts.NetworkDetails(network);
            networkDetailsKryptonTextBox.Text = details;
            SetShown(networkDetailsKryptonTextBox, details.Length > 0);
            reportResultKryptonWrapLabel.Text = reportResult;
            SetShown(reportKryptonTextBox, reportKryptonTextBox.Text.Length > 0);
        }

        /// <summary>The state of the maintenance tools: what the last scan found, the results of the last actions, which buttons work.</summary>
        private void ShowMaintenance()
        {
            MaintenanceScan scan = maintenance.Scan;
            Installation selected = maintenance.Selected;
            bool canChange = maintenance.CanChange;
            string blocked = maintenance.RunningSetup == null ? null : Texts.SetupRunning(maintenance.RunningSetup);

            ShowCleanup(scan?.Cleanup, canChange);
            cleanupResultKryptonWrapLabel.Text = blocked ?? cleanupResult;

            wonStateKryptonWrapLabel.Text = selected == null ? Texts.GameSettingsInstallation(null)
                : scan == null ? Resources.ToolsChecking : Texts.WonFiles(scan.WonFiles);
            wonResetKryptonButton.Enabled = selected != null && canChange;
            wonResultKryptonWrapLabel.Text = blocked ?? wonResult;

            virtualStoreStateKryptonWrapLabel.Text = selected == null ? Texts.GameSettingsInstallation(null)
                : Texts.VirtualStoreState(scan?.VirtualStore);
            string copies = Texts.VirtualStoreFiles(scan?.VirtualStore);
            virtualStoreFilesKryptonTextBox.Text = copies;
            SetShown(virtualStoreFilesKryptonTextBox, copies.Length > 0);

            savesStateKryptonWrapLabel.Text = selected == null ? Texts.GameSettingsInstallation(null)
                : Texts.SavedGamesState(scan?.SavedGames);
            exportSavesKryptonButton.Enabled = selected != null && !maintenance.IsBusy;
            importEeSavesKryptonButton.Enabled = selected != null && canChange;
            importAocSavesKryptonButton.Enabled = selected != null && selected.HasArtOfConquest && canChange;
            savesResultKryptonWrapLabel.Text = blocked ?? savesResult;

            namesStateKryptonWrapLabel.Text = selected == null ? Texts.GameSettingsInstallation(null) : Texts.NameCheck(scan?.Names);
            backupsInfoKryptonWrapLabel.Text = string.Format(CultureInfo.CurrentCulture, Resources.BackupFolderInfoFormat,
                maintenance.BackupDirectory);
        }

        /// <summary>
        /// The registry cleanup (<see cref="CleanupView"/>): the summary, the keys to select (kept selected across scans), the
        /// read-only list, and the delete button only when a key is selected.
        /// </summary>
        private void ShowCleanup(CleanupScan scan, bool canChange)
        {
            if (scan != null && scan == shownCleanupScan)
            {
                UpdateDeleteButton(cleanupKryptonCheckedListBox.CheckedIndices.Count);
                return;
            }
            shownCleanupScan = scan;
            var selected = new HashSet<string>(SelectedKeys().Select(item => item.Entry.Id));
            cleanupView = CleanupView.For(scan, 0, canChange);
            cleanupStateKryptonWrapLabel.Text = cleanupView.Summary;
            cleanupKryptonCheckedListBox.BeginUpdate();
            cleanupKryptonCheckedListBox.Items.Clear();
            for (int i = 0; i < cleanupView.Offered.Count; i++)
            {
                cleanupKryptonCheckedListBox.Items.Add(cleanupView.OfferedTexts[i]);
                if (selected.Contains(cleanupView.Offered[i].Entry.Id))
                    cleanupKryptonCheckedListBox.SetItemChecked(i, true);
            }
            cleanupKryptonCheckedListBox.EndUpdate();
            cleanupKryptonCheckedListBox.Height = Math.Min(5, Math.Max(1, cleanupView.Offered.Count)) * 18 + 6;
            SetShown(cleanupKryptonCheckedListBox, cleanupView.ShowsList);
            cleanupReadOnlyKryptonTextBox.Text = cleanupView.ReadOnlyText;
            SetShown(cleanupReadOnlyKryptonTextBox, cleanupView.ReadOnlyText.Length > 0);
            UpdateDeleteButton(cleanupKryptonCheckedListBox.CheckedIndices.Count);
        }

        private void UpdateDeleteButton(int selectedCount)
        {
            CleanupView view = CleanupView.For(maintenance.Scan?.Cleanup, selectedCount, maintenance.CanChange);
            cleanupDeleteKryptonButton.Enabled = view.DeleteEnabled;
        }

        /// <summary>The offered keys whose check box is set.</summary>
        private List<CleanupItem> SelectedKeys()
        {
            var items = new List<CleanupItem>();
            if (cleanupView == null)
                return items;
            foreach (int index in cleanupKryptonCheckedListBox.CheckedIndices)
            {
                if (index >= 0 && index < cleanupView.Offered.Count)
                    items.Add(cleanupView.Offered[index]);
            }
            return items;
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

            y += Gap;
            Place(cleanupHeadingKryptonLabel);
            Place(cleanupInfoKryptonWrapLabel);
            Place(cleanupStateKryptonWrapLabel);
            Place(cleanupKryptonCheckedListBox);
            Place(cleanupReadOnlyKryptonTextBox);
            Place(cleanupDeleteKryptonButton);
            Place(cleanupResultKryptonWrapLabel);

            y += Gap;
            Place(wonHeadingKryptonLabel);
            Place(wonInfoKryptonWrapLabel);
            Place(wonStateKryptonWrapLabel);
            Place(wonResetKryptonButton);
            Place(wonResultKryptonWrapLabel);

            y += Gap;
            Place(virtualStoreHeadingKryptonLabel);
            Place(virtualStoreInfoKryptonWrapLabel);
            Place(virtualStoreStateKryptonWrapLabel);
            Place(virtualStoreFilesKryptonTextBox);

            y += Gap;
            Place(savesHeadingKryptonLabel);
            Place(savesInfoKryptonWrapLabel);
            Place(savesStateKryptonWrapLabel);
            Place(exportSavesKryptonButton);
            PlaceRow(importEeSavesKryptonButton, importAocSavesKryptonButton);
            Place(savesResultKryptonWrapLabel);

            y += Gap;
            Place(namesHeadingKryptonLabel);
            Place(namesInfoKryptonWrapLabel);
            Place(namesStateKryptonWrapLabel);

            y += Gap;
            Place(backupsHeadingKryptonLabel);
            Place(backupsInfoKryptonWrapLabel);
            Place(openBackupFolderKryptonButton);

            y += Gap;
            Place(networkHeadingKryptonLabel);
            Place(networkInfoKryptonWrapLabel);
            Place(networkCheckKryptonButton);
            Place(networkVerdictKryptonWrapLabel);
            Place(networkHintsKryptonWrapLabel);
            Place(networkDetailsKryptonTextBox);

            y += Gap;
            Place(reportHeadingKryptonLabel);
            Place(reportInfoKryptonWrapLabel);
            PlaceRow(copyReportKryptonButton, saveReportKryptonButton);
            Place(reportResultKryptonWrapLabel);
            Place(reportKryptonTextBox);
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
                uiOperation.Run(fullCheckKryptonButton, () => integrity.StartFullCheckAsync(), ShowState);
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
            uiOperation.Run(versionCheckKryptonButton, () => CheckVersionsAsync(this, true, updates, themeService, uiOperation),
                ShowState);
        }

        // --- Maintenance tools (L-WP8) ---------------------------------------------------------------------------------

        private void cleanupKryptonCheckedListBox_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            // ItemCheck comes before the check box changes: count the new state.
            int selected = cleanupKryptonCheckedListBox.CheckedIndices.Count + (e.NewValue == CheckState.Checked ? 1 : -1);
            UpdateDeleteButton(Math.Max(0, selected));
        }

        /// <summary>The registry cleanup (R5): confirmation with the keys and the backup folder, then guard, backup, delete.</summary>
        private void cleanupDeleteKryptonButton_Click(object sender, EventArgs e)
        {
            List<CleanupItem> selection = SelectedKeys();
            if (selection.Count == 0 || !maintenance.CanChange)
                return;
            if (MessageBox.Show(FindForm(), Texts.CleanupConfirm(selection, maintenance.BackupDirectory), Resources.LauncherTitle,
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                return;
            uiOperation.Run(cleanupDeleteKryptonButton, async () =>
            {
                CleanupResult result = await maintenance.DeleteKeysAsync(selection);
                cleanupResult = Texts.CleanupResult(result);
            }, ShowState);
        }

        /// <summary>The WON login reset (R6, forum p=83519); the result names the backup folder, which contains login data.</summary>
        private void wonResetKryptonButton_Click(object sender, EventArgs e)
        {
            if (maintenance.Selected == null || !maintenance.CanChange)
                return;
            uiOperation.Run(wonResetKryptonButton, async () =>
            {
                WonResetResult result = await maintenance.ResetWonLoginAsync();
                wonResult = Texts.WonResult(result);
            }, ShowState);
        }

        /// <summary>The export of saved games and scenarios (R10) into a new folder of the folder the player chooses.</summary>
        private void exportSavesKryptonButton_Click(object sender, EventArgs e)
        {
            if (maintenance.Selected == null)
                return;
            string folder;
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = Resources.ExportFolderDescription;
                dialog.ShowNewFolderButton = true;
                string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                if (!string.IsNullOrEmpty(documents) && Directory.Exists(documents))
                    dialog.SelectedPath = documents;
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                folder = dialog.SelectedPath;
            }
            uiOperation.Run(exportSavesKryptonButton, async () =>
            {
                ExportResult result = await maintenance.ExportAsync(folder);
                savesResult = Texts.ExportResult(result);
            }, ShowState);
        }

        private void importEeSavesKryptonButton_Click(object sender, EventArgs e)
        {
            Import(Game.EmpireEarth, importEeSavesKryptonButton);
        }

        private void importAocSavesKryptonButton_Click(object sender, EventArgs e)
        {
            Import(Game.ArtOfConquest, importAocSavesKryptonButton);
        }

        /// <summary>
        /// The import of saved games and scenarios (R10): the player chooses files, the core checks them, a file that would
        /// replace one of the same name only after the player confirmed it (its old version goes to the backup folder).
        /// </summary>
        private void Import(Game game, KryptonButton button)
        {
            if (maintenance.Selected == null || !maintenance.CanChange)
                return;
            string[] files;
            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = Resources.ImportDialogTitle;
                dialog.Filter = Resources.ImportFileFilter;
                dialog.Multiselect = true;
                dialog.CheckFileExists = true;
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                files = dialog.FileNames;
            }
            ImportPlan plan = maintenance.PlanImport(game, files);
            bool overwrite = plan.NeedsOverwriteConfirmation &&
                             MessageBox.Show(FindForm(), Texts.ImportConfirm(plan), Resources.LauncherTitle, MessageBoxButtons.YesNo,
                                 MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
            uiOperation.Run(button, async () =>
            {
                ImportResult result = await maintenance.ImportAsync(plan, overwrite);
                savesResult = Texts.ImportResult(result);
            }, ShowState);
        }

        /// <summary>"Open backup folder" (ADR 0007): the Explorer, through the shell.</summary>
        private void openBackupFolderKryptonButton_Click(object sender, EventArgs e)
        {
            string problem = maintenance.OpenBackupFolder();
            if (problem != null)
                MessageBox.Show(FindForm(), string.Format(CultureInfo.CurrentCulture, Resources.OpenBackupFolderFailedFormat, problem),
                    Resources.LauncherTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        // --- Network diagnostics and diagnostics report (L-WP9) --------------------------------------------------------

        /// <summary>The network check on request (R7): DNS, the update API, the status server and the files of the game folders.</summary>
        private void networkCheckKryptonButton_Click(object sender, EventArgs e)
        {
            uiOperation.Run(networkCheckKryptonButton, () => diagnostics.CheckNetworkAsync(), ShowState);
        }

        /// <summary>
        /// The link of an unavailable player list on the Play page: shows the network section and starts the check, which the
        /// player asked for with the click.
        /// </summary>
        internal void ShowNetworkCheck()
        {
            toolsScrollPanel.ScrollControlIntoView(networkHeadingKryptonLabel);
            if (!diagnostics.IsChecking)
                uiOperation.Run(networkCheckKryptonButton, () => diagnostics.CheckNetworkAsync(), ShowState);
        }

        /// <summary>Copies the report to the clipboard and shows the text that was copied (it is never sent).</summary>
        private void copyReportKryptonButton_Click(object sender, EventArgs e)
        {
            string report = diagnostics.BuildReport();
            reportKryptonTextBox.Text = report;
            try
            {
                Clipboard.SetText(report);
                diagnostics.ReportCopied(report);
                reportResult = Resources.ReportCopied;
            }
            catch (ExternalException ex)
            {
                reportResult = string.Format(CultureInfo.CurrentCulture, Resources.ReportCopyFailedFormat, ex.Message);
            }
            ShowState();
            toolsScrollPanel.ScrollControlIntoView(reportKryptonTextBox);
        }

        /// <summary>Saves the report into a file the player chooses (UTF-8, never into the installation).</summary>
        private void saveReportKryptonButton_Click(object sender, EventArgs e)
        {
            string report = diagnostics.BuildReport();
            string path;
            using (var dialog = new SaveFileDialog())
            {
                dialog.Title = Resources.ReportSaveDialogTitle;
                dialog.Filter = Resources.ReportFileFilter;
                dialog.DefaultExt = "txt";
                dialog.AddExtension = true;
                dialog.OverwritePrompt = true;
                dialog.FileName = string.Format(CultureInfo.CurrentCulture, Resources.ReportFileNameFormat,
                    DateTime.Now.ToString("yyyy-MM-dd_HHmm", CultureInfo.InvariantCulture));
                string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                if (!string.IsNullOrEmpty(documents) && Directory.Exists(documents))
                    dialog.InitialDirectory = documents;
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                path = dialog.FileName;
            }
            reportKryptonTextBox.Text = report;
            reportResult = Texts.ReportSaved(diagnostics.SaveReport(path, report));
            ShowState();
            toolsScrollPanel.ScrollControlIntoView(reportKryptonTextBox);
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

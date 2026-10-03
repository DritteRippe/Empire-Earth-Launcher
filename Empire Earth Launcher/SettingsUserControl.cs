using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.GameSettings;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Play;
using Empire_Earth_Launcher.Properties;
using Krypton.Toolkit;

namespace Empire_Earth_Launcher
{
    /// <summary>
    /// The Game settings page (L-WP5, contract 3): the defaults of the selected installation for this Windows account with
    /// the display question, "Apply recommended display" and "Reset game settings" (with the path of the backup), the hints
    /// of the consistency checks (each can be hidden from the Play page), and the compatibility options behind the existing
    /// warning (HKCU switches from Windows 8 on, HKLM read-only, Windows 7 only the old values and RUNASADMIN).
    /// </summary>
    /// <remarks>
    /// The controls are stacked in <see cref="LayoutPage"/> from the texts they show, so longer translations push the
    /// following controls down; the page scrolls. The work runs through <see cref="UiOperation"/> and
    /// <see cref="GameSettingsModel"/>; this class only shows the state.
    /// </remarks>
    public partial class SettingsUserControl : UserControl
    {
        private const int ContentLeft = 12;
        private const int ContentWidth = 505;
        private const int Gap = 6;
        private const int HintCheckBoxWidth = 116;

        private readonly Dictionary<KryptonCheckBox, string> entryOfCheckBox;

        /// <summary>The visibility each control should have (Visible reads false while the page is hidden).</summary>
        private readonly Dictionary<Control, bool> shown = new Dictionary<Control, bool>();

        /// <summary>The check box and text of each hint, created for the findings shown.</summary>
        private readonly List<Tuple<KryptonCheckBox, LauncherWrapLabel>> hintRows = new List<Tuple<KryptonCheckBox, LauncherWrapLabel>>();

        private IThemeService themeService;
        private GameSettingsModel model;
        private InstallationService installations;
        private SetupWatcher setupWatcher;
        private UiOperation uiOperation;

        /// <summary>The discovery result whose defaults were applied last (ADR 0015).</summary>
        private DiscoveryResult appliedResult;

        /// <summary>True after the player confirmed the compatibility warning (this session).</summary>
        private bool compatibilityConfirmed;

        /// <summary>True while the check boxes are set by code, so that no change is written then.</summary>
        private bool updatingCheckBoxes;

        /// <summary>What the confirmation button does; null while no confirmation is shown.</summary>
        private Action pendingConfirmation;

        /// <summary>True if the confirmation belongs to the compatibility options (else to the reset).</summary>
        private bool confirmationBelowCompatibility;

        public SettingsUserControl()
        {
            InitializeComponent();
            ApplyTexts();
            entryOfCheckBox = new Dictionary<KryptonCheckBox, string>
            {
                { dwm8And16BitMitigationKryptonCheckBox, CompatibilityLayers.Dwm8And16BitMitigation },
                { highDpiAwareKryptonCheckBox, CompatibilityLayers.HighDpiAware },
                { heapClearAllocationKryptonCheckBox, CompatibilityLayers.HeapClearAllocation },
                { windows7ModeKryptonCheckBox, CompatibilityLayers.Windows7Mode },
            };
        }

        /// <summary>
        /// Sets the texts of the page from the resources in the UI language (ADR 0009); the designer texts are
        /// placeholders. Texts that depend on the state are set in <see cref="ShowState"/>.
        /// </summary>
        private void ApplyTexts()
        {
            defaultsHeadingKryptonLabel.Values.Text = Resources.GameSettingsDefaultsHeading;
            displayQuestionApplyKryptonButton.Values.Text = Resources.DisplayQuestionApply;
            displayQuestionKeepKryptonButton.Values.Text = Resources.DisplayQuestionKeep;
            applyDisplayKryptonButton.Values.Text = Resources.ApplyDisplayButton;
            resetGameSettingsKryptonButton.Values.Text = Resources.ResetGameSettingsButton;
            confirmNoKryptonButton.Values.Text = Resources.ConfirmCancel;
            hintsHeadingKryptonLabel.Values.Text = Resources.HintsHeading;
            hintsNoneKryptonLabel.Values.Text = Resources.HintsNone;
            compatibilityHeadingKryptonLabel.Values.Text = Resources.CompatibilityHintTitle;
            dwm8And16BitMitigationKryptonCheckBox.Values.Text = Texts.CompatibilityOption(CompatibilityLayers.Dwm8And16BitMitigation);
            highDpiAwareKryptonCheckBox.Values.Text = Texts.CompatibilityOption(CompatibilityLayers.HighDpiAware);
            heapClearAllocationKryptonCheckBox.Values.Text = Texts.CompatibilityOption(CompatibilityLayers.HeapClearAllocation);
            windows7ModeKryptonCheckBox.Values.Text = Texts.CompatibilityOption(CompatibilityLayers.Windows7Mode);
            removeRunAsAdminKryptonButton.Values.Text = Resources.RemoveRunAsAdminButton;
            compatibilityWarningKryptonWrapLabel.Text = Resources.CompatibilityWarningText;
            compatibilityWarningConfirmationKryptonButton.Values.Text = Resources.CompatibilityWarningConfirm;
        }

        /// <summary>
        /// Passes the services in. The control is created by the designer, which needs a parameterless
        /// constructor, so its owner calls this right after InitializeComponent.
        /// </summary>
        /// <param name="themeService">Theme of the launcher.</param>
        /// <param name="model">The state and the actions of the game settings.</param>
        /// <param name="installations">The installations; after each discovery the defaults are applied (ADR 0015).</param>
        /// <param name="setupWatcher">While a setup runs, every change of the page is disabled and the page says why
        /// (contract 4.2, L-WP6).</param>
        /// <param name="uiOperation">Runs the work of the page (ADR 0004).</param>
        internal void Initialize(IThemeService themeService, GameSettingsModel model, InstallationService installations,
            SetupWatcher setupWatcher, UiOperation uiOperation)
        {
            this.themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
            this.model = model ?? throw new ArgumentNullException(nameof(model));
            this.installations = installations ?? throw new ArgumentNullException(nameof(installations));
            this.setupWatcher = setupWatcher ?? throw new ArgumentNullException(nameof(setupWatcher));
            this.uiOperation = uiOperation ?? throw new ArgumentNullException(nameof(uiOperation));
            themeService.Register(launcherKryptonPalette, this);

            EventHandler showState = (sender, e) => ShowState();
            EventHandler applyDefaults = (sender, e) => OnInstallationsChanged();
            EventHandler<SetupStateEventArgs> showSetup = (sender, e) => ShowState();
            model.Changed += showState;
            installations.Changed += applyDefaults;
            setupWatcher.SetupStarted += showSetup;
            setupWatcher.SetupFinished += showSetup;
            Disposed += (sender, e) =>
            {
                model.Changed -= showState;
                installations.Changed -= applyDefaults;
                setupWatcher.SetupStarted -= showSetup;
                setupWatcher.SetupFinished -= showSetup;
            };
            ShowState();
        }

        /// <summary>
        /// After every discovery the defaults are applied for unambiguous installations (contract 3.6, ADR 0015), then the
        /// state is read. A discovery that ends while the previous one is still applied is applied right after it.
        /// </summary>
        private void OnInstallationsChanged()
        {
            if (installations.IsSearching || installations.Result == null || installations.Result == appliedResult)
                return;
            uiOperation.Run(this, async () =>
            {
                while (installations.Result != null && installations.Result != appliedResult)
                {
                    appliedResult = installations.Result;
                    await model.ApplyAfterDiscoveryAsync(appliedResult);
                }
            }, ShowState);
        }

        // --- Showing the state ------------------------------------------------------------------------------------

        private void SetShown(Control control, bool visible)
        {
            shown[control] = visible;
            control.Visible = visible;
        }

        private bool IsShown(Control control)
        {
            return shown.TryGetValue(control, out bool visible) ? visible : control.Visible;
        }

        /// <summary>Shows the state of <see cref="model"/>: texts, enabled buttons, the hints and the options.</summary>
        private void ShowState()
        {
            if (model == null)
                return;
            Installation selected = model.Selected;
            // While a setup runs, every change of this page would be refused by the mutation guard (contract 4.2, ADR 0016):
            // the buttons are disabled and the status says why.
            bool setupRunning = setupWatcher.IsSetupRunning;
            bool usable = selected != null && selected.State != InstallationState.FolderMissing && !selected.HasNewerContract &&
                          !setupRunning;

            installationKryptonLabel.Values.Text = model.Result == null
                ? installations.IsWaitingForSetup ? Resources.InstallationsWaitingForSetup : Resources.InstallationsSearching
                : Texts.GameSettingsInstallation(selected);
            var status = model.Lines.Select(line => Texts.DefaultsStatus(line.Game, line.Status)).ToList();
            string block = Texts.Block(model.StartBlock);
            if (block != null)
                status.Add(block);
            string setup = Texts.SetupRunning(setupWatcher.RunningSetup);
            if (setup != null)
                status.Add(setup);
            defaultsStatusKryptonWrapLabel.Text = string.Join(Environment.NewLine, status);
            SetShown(defaultsStatusKryptonWrapLabel, status.Count > 0);

            bool question = model.Question != null;
            displayQuestionKryptonWrapLabel.Text = question ? Texts.DisplayQuestion(model.Question) : string.Empty;
            SetShown(displayQuestionKryptonWrapLabel, question);
            SetShown(displayQuestionApplyKryptonButton, question);
            SetShown(displayQuestionKeepKryptonButton, question);
            displayQuestionApplyKryptonButton.Enabled = !setupRunning;
            displayQuestionKeepKryptonButton.Enabled = !setupRunning;

            applyDisplayKryptonButton.Enabled = usable;
            resetGameSettingsKryptonButton.Enabled = usable;
            resultKryptonWrapLabel.Text = model.LastResult == null ? string.Empty : Texts.GameSettingsResult(model.LastResult);
            SetShown(resultKryptonWrapLabel, model.LastResult != null);

            ShowHints();
            ShowCompatibility();
            ShowConfirmation();
            LayoutPage();
        }

        private void ShowHints()
        {
            foreach (var row in hintRows)
            {
                gameSettingsScrollPanel.Controls.Remove(row.Item1);
                gameSettingsScrollPanel.Controls.Remove(row.Item2);
                row.Item1.Dispose();
                row.Item2.Dispose();
            }
            hintRows.Clear();

            foreach (ConsistencyFinding finding in model.Findings)
            {
                var checkBox = new KryptonCheckBox
                {
                    Palette = launcherKryptonPalette,
                    PaletteMode = PaletteMode.Custom,
                    Location = new Point(ContentLeft, 0),
                    Size = new Size(HintCheckBoxWidth, 22),
                    Checked = !model.IsHidden(finding)
                };
                checkBox.Values.Text = Resources.HintShowColumn;
                ConsistencyFinding current = finding;
                // Saved after the event: the change rebuilds the rows, this check box included.
                checkBox.CheckedChanged += (sender, e) =>
                {
                    bool hidden = !((KryptonCheckBox)sender).Checked;
                    BeginInvoke(new Action(() => model.SetHidden(current, hidden)));
                };
                var label = new LauncherWrapLabel
                {
                    AutoSize = false,
                    LabelStyle = LabelStyle.NormalControl,
                    Palette = launcherKryptonPalette,
                    PaletteMode = PaletteMode.Custom,
                    Location = new Point(ContentLeft + HintCheckBoxWidth + Gap, 0),
                    Size = new Size(ContentWidth - HintCheckBoxWidth - Gap, 20),
                    Text = Texts.Finding(finding)
                };
                gameSettingsScrollPanel.Controls.Add(checkBox);
                gameSettingsScrollPanel.Controls.Add(label);
                hintRows.Add(Tuple.Create(checkBox, label));
            }
            SetShown(hintsNoneKryptonLabel, model.Selected != null && model.Findings.Count == 0);
            SetShown(hintsHeadingKryptonLabel, model.Selected != null);
        }

        private void ShowCompatibility()
        {
            CompatibilityState state = model.Compatibility;
            bool available = state != null && state.Programs.Count > 0;
            SetShown(compatibilityHeadingKryptonLabel, available);
            SetShown(compatibilityWarningKryptonPanel, available && !compatibilityConfirmed);
            bool options = available && compatibilityConfirmed;

            updatingCheckBoxes = true;
            try
            {
                foreach (var pair in entryOfCheckBox)
                {
                    CompatibilityEntry entry = options && state.SwitchesOffered
                        ? state.Entries.FirstOrDefault(e => e.Name == pair.Value)
                        : null;
                    SetShown(pair.Key, entry != null);
                    if (entry == null)
                        continue;
                    pair.Key.CheckState = entry.State == EntryState.On ? CheckState.Checked
                        : entry.State == EntryState.Mixed ? CheckState.Indeterminate
                        : CheckState.Unchecked;
                    pair.Key.Enabled = (entry.IsOffered || entry.State != EntryState.Off) && !setupWatcher.IsSetupRunning;
                }
            }
            finally
            {
                updatingCheckBoxes = false;
            }

            compatibilityInfoKryptonWrapLabel.Text = options ? Texts.CompatibilityInfo(state) : string.Empty;
            SetShown(compatibilityInfoKryptonWrapLabel, options);
            SetShown(removeRunAsAdminKryptonButton, options && state.RunAsAdminRemovable);
            removeRunAsAdminKryptonButton.Enabled = !setupWatcher.IsSetupRunning;
            compatibilityResultKryptonWrapLabel.Text = model.LastCompatibilityResult == null
                ? string.Empty
                : Texts.CompatibilityResult(model.LastCompatibilityResult);
            SetShown(compatibilityResultKryptonWrapLabel, options && model.LastCompatibilityResult != null);
        }

        private void ShowConfirmation()
        {
            bool confirmation = pendingConfirmation != null;
            SetShown(confirmKryptonWrapLabel, confirmation);
            SetShown(confirmYesKryptonButton, confirmation);
            confirmYesKryptonButton.Enabled = !setupWatcher.IsSetupRunning;
            SetShown(confirmNoKryptonButton, confirmation);
        }

        /// <summary>
        /// Stacks the controls that are shown from top to bottom, each wrapping label as high as its text, so that every
        /// translation fits; the panel scrolls when the page is longer than the window.
        /// </summary>
        private void LayoutPage()
        {
            gameSettingsScrollPanel.SuspendLayout();
            int scroll = gameSettingsScrollPanel.AutoScrollPosition.Y;
            int y = 8;

            void Place(Control control)
            {
                if (!IsShown(control))
                    return;
                if (control is LauncherWrapLabel label)
                    label.Height = TextHeight(label);
                control.Top = y + scroll;
                y += control.Height + Gap;
            }

            void PlaceRow(params Control[] controls)
            {
                Control[] visible = controls.Where(IsShown).ToArray();
                if (visible.Length == 0)
                    return;
                foreach (Control control in visible)
                    control.Top = y + scroll;
                y += visible.Max(control => control.Height) + Gap;
            }

            void PlaceConfirmation()
            {
                Place(confirmKryptonWrapLabel);
                PlaceRow(confirmYesKryptonButton, confirmNoKryptonButton);
            }

            Place(defaultsHeadingKryptonLabel);
            Place(installationKryptonLabel);
            Place(defaultsStatusKryptonWrapLabel);
            Place(displayQuestionKryptonWrapLabel);
            PlaceRow(displayQuestionApplyKryptonButton, displayQuestionKeepKryptonButton);
            PlaceRow(applyDisplayKryptonButton, resetGameSettingsKryptonButton);
            if (!confirmationBelowCompatibility)
                PlaceConfirmation();
            Place(resultKryptonWrapLabel);

            y += Gap;
            Place(hintsHeadingKryptonLabel);
            Place(hintsNoneKryptonLabel);
            foreach (var row in hintRows)
            {
                row.Item2.Height = TextHeight(row.Item2);
                row.Item1.Top = y + scroll;
                row.Item2.Top = y + scroll;
                y += Math.Max(row.Item1.Height, row.Item2.Height) + Gap;
            }

            y += Gap;
            Place(compatibilityHeadingKryptonLabel);
            if (IsShown(compatibilityWarningKryptonPanel))
            {
                compatibilityWarningKryptonPanel.Left = (gameSettingsScrollPanel.ClientSize.Width - compatibilityWarningKryptonPanel.Width) / 2;
                Place(compatibilityWarningKryptonPanel);
            }
            foreach (KryptonCheckBox checkBox in entryOfCheckBox.Keys)
                Place(checkBox);
            if (confirmationBelowCompatibility)
                PlaceConfirmation();
            Place(compatibilityInfoKryptonWrapLabel);
            Place(removeRunAsAdminKryptonButton);
            Place(compatibilityResultKryptonWrapLabel);

            gameSettingsScrollPanel.ResumeLayout(true);
        }

        /// <summary>The height of a wrapping label for its text at its width (<see cref="LauncherWrapLabel.TextHeight"/>), at least 20.</summary>
        private static int TextHeight(LauncherWrapLabel label)
        {
            return Math.Max(20, label.TextHeight(label.Width));
        }

        // --- Actions ------------------------------------------------------------------------------------------------

        private void displayQuestionApplyKryptonButton_Click(object sender, EventArgs e)
        {
            uiOperation.Run(displayQuestionApplyKryptonButton, () => model.AnswerQuestionAsync(true), ShowState);
        }

        private void displayQuestionKeepKryptonButton_Click(object sender, EventArgs e)
        {
            uiOperation.Run(displayQuestionKeepKryptonButton, () => model.AnswerQuestionAsync(false), ShowState);
        }

        private void applyDisplayKryptonButton_Click(object sender, EventArgs e)
        {
            uiOperation.Run(applyDisplayKryptonButton, () => model.ApplyRecommendedDisplayAsync(), ShowState);
        }

        /// <summary>The reset needs a confirmation (contract 3.6), shown on the page below the button.</summary>
        private void resetGameSettingsKryptonButton_Click(object sender, EventArgs e)
        {
            Installation selected = model.Selected;
            if (selected == null)
                return;
            string games = string.Join(", ", GameDefaultsService.GamesOf(selected).Select(Texts.GameName));
            Confirm(string.Format(CultureInfo.CurrentCulture, Resources.ResetConfirmFormat, games, BackupFolderForDisplay()),
                Resources.ResetConfirmButton, false,
                () => uiOperation.Run(resetGameSettingsKryptonButton, () => model.ResetAsync(), ShowState));
        }

        private void confirmYesKryptonButton_Click(object sender, EventArgs e)
        {
            Action action = pendingConfirmation;
            CloseConfirmation();
            action?.Invoke();
        }

        private void confirmNoKryptonButton_Click(object sender, EventArgs e)
        {
            CloseConfirmation();
            ShowState(); // a check box that waited for the confirmation shows its state again
        }

        private void Confirm(string text, string button, bool belowCompatibility, Action action)
        {
            confirmKryptonWrapLabel.Text = text;
            confirmYesKryptonButton.Values.Text = button;
            confirmationBelowCompatibility = belowCompatibility;
            pendingConfirmation = action;
            ShowConfirmation();
            LayoutPage();
            gameSettingsScrollPanel.ScrollControlIntoView(confirmYesKryptonButton);
        }

        private void CloseConfirmation()
        {
            pendingConfirmation = null;
            ShowConfirmation();
            LayoutPage();
        }

        /// <summary>
        /// A check box of a compatibility option changed by the player: switch the entry for the programs of the
        /// installation. Switching HIGHDPIAWARE off on a scaled screen asks first (ADR 0011 plan review).
        /// </summary>
        private void compatibilityKryptonCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (updatingCheckBoxes || model == null)
                return;
            var checkBox = (KryptonCheckBox)sender;
            string entry = entryOfCheckBox[checkBox];
            bool on = checkBox.Checked;
            if (model.NeedsHighDpiHint(entry, on))
            {
                updatingCheckBoxes = true;
                checkBox.Checked = true; // until confirmed
                updatingCheckBoxes = false;
                Confirm(string.Format(CultureInfo.CurrentCulture, Resources.HighDpiOffConfirmFormat, ScalingPercent()),
                    Resources.HighDpiOffConfirmButton, true,
                    () => uiOperation.Run(checkBox, () => model.SetCompatibilityEntryAsync(entry, false), ShowState));
                return;
            }
            uiOperation.Run(checkBox, () => model.SetCompatibilityEntryAsync(entry, on), ShowState);
        }

        private void removeRunAsAdminKryptonButton_Click(object sender, EventArgs e)
        {
            uiOperation.Run(removeRunAsAdminKryptonButton, () => model.RemoveRunAsAdminAsync(), ShowState);
        }

        private void compatibilityWarningConfirmationKryptonButton_Click(object sender, EventArgs e)
        {
            compatibilityConfirmed = true;
            // ShowDialog does not dispose the form; the using block releases it (and its theme registration).
            using (var dialog = new LauncherDialog(themeService, Resources.CompatibilityHintTitle,
                       Resources.CompatibilityHintMessage, MessageBoxButtons.OK))
            {
                dialog.ShowDialog(this);
            }
            ShowState();
        }

        private int ScalingPercent()
        {
            return model.ScalingPercent;
        }

        private string BackupFolderForDisplay()
        {
            return model.BackupFolder;
        }
    }
}

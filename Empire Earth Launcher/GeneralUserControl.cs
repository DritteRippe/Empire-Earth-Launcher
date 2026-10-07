using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Diagnostics;
using Empire_Earth_Launcher.Core.GameSettings;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Integrity;
using Empire_Earth_Launcher.Core.Lobby;
using Empire_Earth_Launcher.Core.Logging;
using Empire_Earth_Launcher.Core.Play;
using Empire_Earth_Launcher.Core.Repair;
using Empire_Earth_Launcher.Properties;
using Empire_Earth_WON;

namespace Empire_Earth_Launcher
{
    /// <summary>
    /// The Play page: the list of the four games (Empire Earth and The Art of Conquest of each product; a game that is not
    /// installed is shown disabled, the choice selects the installation of its product for every page, launcher 1.1.0), the file
    /// versions of the programs with the version check on request (L-WP7, contract 4.5), the integrity state of the installation
    /// (L-WP7, contract 2.5), Play (L-WP6, ADR 0010) with its refusals, the repair advice and the "setup is running" state, the
    /// info bar of the game settings (L-WP5), and the lobby profiles with the online player list, whose "not available" links to
    /// the network check of the Tools page (L-WP9).
    /// </summary>
    /// <remarks>
    /// The page has two columns (ADR 0017): the online players and the Play button have the width of the designer at the right
    /// edge and the height of the page; the rest is the game column, which grows with the window. Its controls are stacked from
    /// the heights of their texts by <see cref="ScrollPageLayout"/> in <see cref="LayoutPage"/>, so that no translation is cut
    /// off: the texts of the game group, the group itself, and the column of the group and the info bar below it, which scrolls
    /// where the window is too small for it. The four games are stacked one below the other; a game whose name is wider than the
    /// group (a large font) takes the two lines of its name (<see cref="PlaceEntry"/>).
    /// </remarks>
    public partial class GeneralUserControl : UserControl
    {
        private ILogger logger;
        private IThemeService themeService;
        private InstallationService installations;
        private GameSettingsModel gameSettings;
        private PlayModel play;
        private IntegrityModel integrity;
        private UpdateModel updates;
        private UiOperation uiOperation;

        /// <summary>True while the game choice is set by code, so that nothing is saved then.</summary>
        private bool updatingGameChoice;

        /// <summary>The radio buttons of the four games, in the order of <see cref="PlayEntry.All"/>.</summary>
        private readonly Krypton.Toolkit.KryptonRadioButton[] entryRadios;

        /// <summary>The name of each game on one line (<see cref="PlaceEntry"/> splits it where the line would be cut off).</summary>
        private readonly Dictionary<Krypton.Toolkit.KryptonRadioButton, string> entryNames =
            new Dictionary<Krypton.Toolkit.KryptonRadioButton, string>();

        /// <summary>The en dash between the product and "The Art of Conquest" in the name of a game; the two-line form splits there.</summary>
        private const string EntrySeparator = " \u2013 ";

        /// <summary>The installation whose file versions were read last (or are being read).</summary>
        private Installation versionsOf;

        /// <summary>The hint the info bar shows; null while it shows the display question or nothing.</summary>
        private ConsistencyFinding shownFinding;

        /// <summary>Polls the online player list (ADR 0004); null if the server settings are invalid.</summary>
        private PlayerListPolling playerList;

        /// <summary>True once the page has loaded: no request goes out before (ADR 0004).</summary>
        private bool loaded;
        private LobbyProfileRepository lobbyProfiles;

        /// <summary>Profiles shown in the user list, in the same order.</summary>
        private IList<LobbyPersistentData.LobbyGlobalData.PlayerInfoGlobalData> profiles =
            new LobbyPersistentData.LobbyGlobalData.PlayerInfoGlobalData[0];

        /// <summary>The most the lobby state line takes from a small player list (about four lines of text).</summary>
        internal const int MaxLobbyStatusHeight = 70;

        /// <summary>The height the player list keeps at least when the state line takes more than <see cref="MaxLobbyStatusHeight"/>.</summary>
        private const int MinimumPlayerListHeight = 60;

        /// <summary>The space between the columns and at the edges of the page.</summary>
        private const int PageMargin = 8;

        /// <summary>The space inside the group of the online players, between its controls and its edges.</summary>
        private const int OnlineGroupMargin = 6;

        /// <summary>The space left and right of the controls in the game group and in the info bar.</summary>
        private const int GroupMargin = 12;

        /// <summary>The height the room of the player list and the state line has without the profile line (set by the layout).</summary>
        private int playerListHeight;

        /// <summary>The text of the lobby state line; the layout shows it again at its new size.</summary>
        private string lobbyStatusText;

        /// <summary>The width of the column of the online players and the Play button (from the designer).</summary>
        private readonly int onlineColumnWidth;

        /// <summary>The size of the Play button (from the designer).</summary>
        private readonly Size playButtonSize;

        /// <summary>The size of the label "Profile:" and of the link below the heading of the online players (from the designer).</summary>
        private readonly Size profileLabelSize;

        private readonly Size networkLinkSize;

        /// <summary>Stacks the group of the game choice and the info bar; the column scrolls (ADR 0017).</summary>
        private readonly ScrollPageLayout column;

        /// <summary>Stacks the controls inside the group of the game choice; the group is as high as they are.</summary>
        private readonly ScrollPageLayout gameStack;

        /// <summary>Stacks the text and the buttons of the info bar; the bar is as high as they are.</summary>
        private readonly ScrollPageLayout hintStack;

        /// <summary>
        /// The visibility each control should have (Visible reads false while the page is hidden, and the launcher fills the
        /// page before its window is shown). A control that is not in it is always shown.
        /// </summary>
        private readonly Dictionary<Control, bool> shown = new Dictionary<Control, bool>();

        /// <summary>True while <see cref="LayoutPage"/> runs, so that nothing it sets starts it again.</summary>
        private bool layingOut;

        public GeneralUserControl()
        {
            InitializeComponent();
            entryRadios = new[]
            {
                empireEarthKryptonRadioButton, empireEarthAocKryptonRadioButton, neoEmpireEarthKryptonRadioButton,
                neoEmpireEarthAocKryptonRadioButton
            };
            ApplyTexts();
            playerListHeight = onlinePlayersKryptonDataGridView.Height;
            onlineColumnWidth = neoOnlineKryptonGroupBox.Width;
            playButtonSize = playKryptonButton.Size;
            profileLabelSize = lobbyUserKryptonLabel.Size;
            networkLinkSize = networkCheckKryptonLinkLabel.Size;
            // What the designer hides stays hidden, and a label without text takes no room, until the state says otherwise.
            shown[gameSettingsHintKryptonPanel] = gameSettingsHintKryptonPanel.Visible;
            shown[integrityKryptonButton] = integrityKryptonButton.Visible;
            foreach (LauncherWrapLabel label in new[]
                {
                    playEntriesHintKryptonWrapLabel, programVersionsKryptonWrapLabel, versionResultKryptonWrapLabel,
                    integrityKryptonWrapLabel, playStatusKryptonWrapLabel
                })
                SetText(label, label.Text);
            column = new ScrollPageLayout(gameColumnPanel, IsShown, PageMargin);
            gameStack = new ScrollPageLayout(gameSettingsKryptonGroupBox.Panel, IsShown, GroupMargin, false);
            hintStack = new ScrollPageLayout(gameSettingsHintKryptonPanel, IsShown, GroupMargin, false);
            // Krypton sizes the panel of a group when the group is laid out, also when its heading gets another font (a theme).
            gameSettingsKryptonGroupBox.Panel.SizeChanged += (sender, e) => LayoutPage();
            neoOnlineKryptonGroupBox.Panel.SizeChanged += (sender, e) => LayoutPage();

            // No file or network I/O here: the constructor also runs inside the Visual Studio designer and
            // during MainForm.InitializeComponent, where an exception would prevent the launcher from starting.
            // Loading happens in OnLoad.
        }

        /// <summary>
        /// Sets the texts of the page from the resources in the UI language (ADR 0009); the designer texts are
        /// placeholders. The heading of the player list shows its state and is changed when the state changes.
        /// </summary>
        private void ApplyTexts()
        {
            gameSettingsKryptonGroupBox.Values.Heading = Resources.GameChoiceHeading;
            empireEarthKryptonRadioButton.Values.Text = RememberName(empireEarthKryptonRadioButton, Resources.PlayEntryEmpireEarth);
            empireEarthAocKryptonRadioButton.Values.Text = RememberName(empireEarthAocKryptonRadioButton, Resources.PlayEntryEmpireEarthAoc);
            neoEmpireEarthKryptonRadioButton.Values.Text = RememberName(neoEmpireEarthKryptonRadioButton, Resources.PlayEntryNeoEE);
            neoEmpireEarthAocKryptonRadioButton.Values.Text = RememberName(neoEmpireEarthAocKryptonRadioButton, Resources.PlayEntryNeoEEAoc);
            playKryptonButton.Values.Text = Resources.PlayButton;
            versionCheckKryptonButton.Values.Text = Resources.VersionCheckPlayButton;
            integrityKryptonButton.Values.Text = Resources.IntegrityDetailsButton;
            neoOnlineKryptonGroupBox.Values.Heading = Resources.OnlinePlayersLoading;
            lobbyUserKryptonLabel.Values.Text = Resources.LobbyProfileLabel;
            networkCheckKryptonLinkLabel.Values.Text = Resources.PlayerListCheckNetworkLink;
            usernameColumn.HeaderText = Resources.PlayerListNameColumn;
            stateColumn.HeaderText = Resources.PlayerListStateColumn;
        }

        /// <summary>Keeps the one-line name of a game for the layout, which may show it in two lines; returns the name.</summary>
        private string RememberName(Krypton.Toolkit.KryptonRadioButton radio, string name)
        {
            entryNames[radio] = name;
            return name;
        }

        /// <summary>
        /// Passes the services in. The control is created by the designer, which needs a parameterless
        /// constructor, so its owner calls this right after InitializeComponent, before the control is loaded.
        /// </summary>
        /// <param name="logger">Log of the launcher.</param>
        /// <param name="themeService">Theme of the launcher.</param>
        /// <param name="installations">The installations; the lobby files are read from the EE folder of the selected one,
        /// again when it changes.</param>
        /// <param name="lobbyProfiles">Reads the lobby profiles of the game folder (VirtualStore copy first).</param>
        /// <param name="playerList">Polls the online player list while the selected installation is NeoEE, started when the
        /// page has loaded and ended with it; null disables the list (invalid server settings).</param>
        /// <param name="gameSettings">The game settings: the display question and the hints of the info bar (L-WP5).</param>
        /// <param name="play">The game choice, the versions and the start (L-WP6).</param>
        /// <param name="integrity">The integrity state of the selected installation (L-WP7).</param>
        /// <param name="updates">The version check and the download of the repair advice (L-WP7).</param>
        /// <param name="uiOperation">Runs the start, the versions and the answer to the display question (ADR 0004).</param>
        internal void Initialize(ILogger logger, IThemeService themeService, InstallationService installations,
            LobbyProfileRepository lobbyProfiles, PlayerListPolling playerList, GameSettingsModel gameSettings, PlayModel play,
            IntegrityModel integrity, UpdateModel updates, UiOperation uiOperation)
        {
            if (logger == null)
                throw new ArgumentNullException(nameof(logger));
            if (themeService == null)
                throw new ArgumentNullException(nameof(themeService));
            if (installations == null)
                throw new ArgumentNullException(nameof(installations));

            this.logger = logger;
            this.themeService = themeService;
            this.lobbyProfiles = lobbyProfiles ?? throw new ArgumentNullException(nameof(lobbyProfiles));
            this.installations = installations;
            this.playerList = playerList;
            themeService.Register(launcherKryptonPalette, this);

            EventHandler reloadLobbyProfiles = (sender, e) => OnInstallationsChanged();
            installations.Changed += reloadLobbyProfiles;
            Disposed += (sender, e) => installations.Changed -= reloadLobbyProfiles;

            this.gameSettings = gameSettings ?? throw new ArgumentNullException(nameof(gameSettings));
            this.uiOperation = uiOperation ?? throw new ArgumentNullException(nameof(uiOperation));
            EventHandler showHint = (sender, e) => ShowGameSettingsHint();
            gameSettings.Changed += showHint;
            Disposed += (sender, e) => gameSettings.Changed -= showHint;

            this.play = play ?? throw new ArgumentNullException(nameof(play));
            EventHandler showPlay = (sender, e) =>
            {
                ShowPlayState();
                ShowGameSettingsHint();
            };
            play.Changed += showPlay;
            Disposed += (sender, e) => play.Changed -= showPlay;

            this.integrity = integrity ?? throw new ArgumentNullException(nameof(integrity));
            this.updates = updates ?? throw new ArgumentNullException(nameof(updates));
            EventHandler showChecks = (sender, e) => ShowChecks();
            integrity.Changed += showChecks;
            updates.Changed += showChecks;
            Disposed += (sender, e) =>
            {
                integrity.Changed -= showChecks;
                updates.Changed -= showChecks;
            };
            ShowGameSettingsHint();
            ShowPlayState();
            ShowChecks();
        }

        // --- Version check and integrity (L-WP7) -------------------------------------------------------------------------

        /// <summary>Raised when the player wants to see the Tools page (button "Details" next to the integrity state).</summary>
        internal event EventHandler ToolsRequested;

        /// <summary>
        /// The result of the version check below the versions, and the integrity state with "Repair..." when the report
        /// offers the repair (contract 2.5), else "Details" (the Tools page). A foreign installation shows no state, a legacy
        /// one only its badge, never the advice (contract 2.5).
        /// </summary>
        private void ShowChecks()
        {
            if (integrity == null || updates == null)
                return;
            SetText(versionResultKryptonWrapLabel, updates.IsChecking
                ? Resources.VersionChecking
                : updates.GameResult == null ? string.Empty : Texts.VersionResult(updates.GameResult));
            versionCheckKryptonButton.Enabled = updates.CanCheck;

            IntegrityReport report = integrity.Report;
            string badge = Texts.IntegrityBadge(report, integrity.IsChecking);
            SetText(integrityKryptonWrapLabel, badge);
            bool offersRepair = !integrity.IsChecking && report != null && report.OffersRepair;
            integrityKryptonButton.Values.Text = offersRepair ? Resources.IntegrityRepairButton : Resources.IntegrityDetailsButton;
            SetShown(integrityKryptonButton, badge.Length > 0);
            LayoutPage();
        }

        /// <summary>The version check of the game on request (contract 4.5, ADR 0008 plan review); an update opens the hand-off.</summary>
        private void versionCheckKryptonButton_Click(object sender, EventArgs e)
        {
            uiOperation.Run(versionCheckKryptonButton,
                () => ToolsUserControl.CheckVersionsAsync(this, false, updates, themeService), ShowChecks);
        }

        /// <summary>"Repair..." opens the repair advice with the files of the report; "Details" the Tools page.</summary>
        private void integrityKryptonButton_Click(object sender, EventArgs e)
        {
            IntegrityReport report = integrity.Report;
            if (integrity.IsChecking || report == null || !report.OffersRepair)
            {
                ToolsRequested?.Invoke(this, EventArgs.Empty);
                return;
            }
            RepairAdvice advice = integrity.CreateRepairAdvice();
            RepairAdviceDialog.ShowAdvice(FindForm(), themeService, advice, Texts.RepairReasonText(advice, report), updates);
        }

        // --- Play (L-WP6) ----------------------------------------------------------------------------------------------

        /// <summary>
        /// Shows the state of <see cref="PlayModel"/>: the four games (the one chosen is checked, a game that is not installed
        /// is disabled, with the hint below the list), the file versions, the state line (searching, setup running, started) and
        /// whether Play is possible.
        /// </summary>
        private void ShowPlayState()
        {
            if (play == null)
                return;
            DiscoveryResult result = installations.Result;
            // The hint is about games that are not installed; a game that cannot be chosen while a start runs is no such game.
            bool notInstalled = result != null && PlayEntry.All.Any(entry => !PlayEntry.IsAvailable(result, entry));
            ShowEntries(play.Entries.Select(play.IsAvailable).ToArray(), play.SelectedEntry, notInstalled);

            SetText(programVersionsKryptonWrapLabel, Texts.ProgramVersions(play.Versions));
            SetText(playStatusKryptonWrapLabel, PlayStatusText());
            playKryptonButton.Enabled = play.CanPlay;
            LayoutPage();
        }

        /// <summary>
        /// Shows the list of the four games: <paramref name="available"/> says for each, in the order of <see cref="PlayEntry.All"/>,
        /// whether it can be chosen (a game that cannot is greyed out), <paramref name="selected"/> is checked, and the hint
        /// "Greyed out games are not installed" is shown if <paramref name="showHint"/>. Nothing is saved.
        /// </summary>
        private void ShowEntries(IReadOnlyList<bool> available, PlayEntry selected, bool showHint)
        {
            updatingGameChoice = true;
            try
            {
                for (int i = 0; i < entryRadios.Length; i++)
                {
                    entryRadios[i].Enabled = available[i];
                    entryRadios[i].Checked = PlayEntry.All[i] == selected;
                }
            }
            finally
            {
                updatingGameChoice = false;
            }
            SetText(playEntriesHintKryptonWrapLabel, showHint ? Resources.PlayEntriesNotInstalledHint : string.Empty);
        }

        /// <summary>The state line below the versions: a running setup first (contract 4.2), else the search, else the last start.</summary>
        private string PlayStatusText()
        {
            string setup = Texts.SetupRunning(play.RunningSetup);
            if (setup != null)
                return setup;
            if (installations.IsWaitingForSetup)
                return Resources.InstallationsWaitingForSetup;
            if (play.IsSearching)
                return Resources.InstallationsSearching;
            if (play.Selected == null)
                return Resources.GameDirectoryNotFound;
            StartResult last = play.LastResult;
            return last != null && last.IsStarted && last.Installation.HasFolder(play.Selected.EeFolder)
                ? Texts.StartMessage(last)
                : string.Empty;
        }

        /// <summary>Reads the file versions again when the discovery selected another installation.</summary>
        private void RefreshVersionsIfSelectionChanged()
        {
            if (installations.IsSearching || play.IsSearching)
                return;
            Installation selected = play.Selected;
            if (selected == versionsOf)
                return;
            versionsOf = selected;
            uiOperation.Run(programVersionsKryptonWrapLabel, () => play.RefreshVersionsAsync());
        }

        /// <summary>
        /// The player chose a game of the list: its product's installation becomes the selected one of every page, and the game
        /// is remembered (<see cref="PlayModel.SelectEntry"/>). A game that cannot be chosen (it is greyed out) is not taken.
        /// </summary>
        private void playEntryKryptonRadioButton_CheckedChanged(object sender, EventArgs e)
        {
            var radio = (Krypton.Toolkit.KryptonRadioButton)sender;
            if (updatingGameChoice || play == null || !radio.Checked)
                return;
            PlayEntry entry = PlayEntry.All[Array.IndexOf(entryRadios, radio)];
            if (!play.IsAvailable(entry))
            {
                ShowPlayState();
                return;
            }
            play.SelectEntry(entry);
        }

        /// <summary>Play: the page is the trigger, so the game choice cannot change while a start runs.</summary>
        private void playKryptonButton_Click(object sender, EventArgs e)
        {
            uiOperation.Run(this, PlayAsync, ShowPlayState);
        }

        /// <summary>
        /// Starts the chosen game (ARCHITECTURE 4.2); asks before starting it while the other game runs, shows the repair
        /// advice for a damaged installation and a message for every other refusal or error.
        /// </summary>
        private async Task PlayAsync()
        {
            StartResult result = await play.StartAsync(false);
            if (result.Outcome == StartOutcome.OtherGameRunning)
            {
                if (MessageBox.Show(FindForm(), Texts.StartMessage(result), Resources.LauncherTitle, MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                    return;
                result = await play.StartAsync(true);
            }
            ShowStartResult(result);
        }

        private void ShowStartResult(StartResult result)
        {
            switch (result.Outcome)
            {
                case StartOutcome.Started:
                    return; // the state line says it
                case StartOutcome.Damaged:
                case StartOutcome.BlockedByAntivirus:
                    RepairAdviceDialog.ShowAdvice(FindForm(), themeService, result.RepairAdvice, Texts.StartMessage(result),
                        updates);
                    return;
                case StartOutcome.ElevationCancelled:
                case StartOutcome.SetupRunning:
                    MessageBox.Show(FindForm(), Texts.StartMessage(result), Resources.LauncherTitle, MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                default:
                    MessageBox.Show(FindForm(), Texts.StartMessage(result), Resources.LauncherTitle, MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
            }
        }

        /// <summary>Raised when the player wants to see the Game settings page (button "Details" of the info bar).</summary>
        internal event EventHandler GameSettingsRequested;

        /// <summary>
        /// The non-modal info bar of the game settings (contract 3.6, ADR 0015): the display question of the first run, else
        /// the first hint the player did not hide, with "Hide" and "Details"; nothing if there is neither.
        /// </summary>
        private void ShowGameSettingsHint()
        {
            shownFinding = null;
            if (gameSettings.Question != null)
            {
                gameSettingsHintKryptonWrapLabel.Text = Texts.DisplayQuestion(gameSettings.Question);
                gameSettingsHintFirstKryptonButton.Values.Text = Resources.DisplayQuestionApply;
                gameSettingsHintSecondKryptonButton.Values.Text = Resources.DisplayQuestionKeep;
                // Both answers write the markers: changes behind the mutation guard, so not while a setup runs.
                bool answerable = play?.RunningSetup == null;
                gameSettingsHintFirstKryptonButton.Enabled = answerable;
                gameSettingsHintSecondKryptonButton.Enabled = answerable;
                SetShown(gameSettingsHintKryptonPanel, true);
                LayoutPage();
                return;
            }

            IReadOnlyList<ConsistencyFinding> visible = gameSettings.VisibleFindings;
            if (visible.Count == 0)
            {
                SetShown(gameSettingsHintKryptonPanel, false);
                LayoutPage();
                return;
            }
            shownFinding = visible[0];
            gameSettingsHintKryptonWrapLabel.Text = Texts.Finding(shownFinding) + (visible.Count > 1
                ? Environment.NewLine + string.Format(CultureInfo.CurrentCulture, Resources.HintBarMoreFormat, visible.Count - 1)
                : string.Empty);
            gameSettingsHintFirstKryptonButton.Values.Text = Resources.HintBarHide;
            gameSettingsHintSecondKryptonButton.Values.Text = Resources.HintBarDetails;
            gameSettingsHintFirstKryptonButton.Enabled = true;
            gameSettingsHintSecondKryptonButton.Enabled = true;
            SetShown(gameSettingsHintKryptonPanel, true);
            LayoutPage();
        }

        private void gameSettingsHintFirstKryptonButton_Click(object sender, EventArgs e)
        {
            if (shownFinding != null)
                gameSettings.SetHidden(shownFinding, true);
            else if (gameSettings.Question != null)
                uiOperation.Run(gameSettingsHintFirstKryptonButton, () => gameSettings.AnswerQuestionAsync(true),
                    ShowGameSettingsHint);
        }

        private void gameSettingsHintSecondKryptonButton_Click(object sender, EventArgs e)
        {
            if (shownFinding != null)
                GameSettingsRequested?.Invoke(this, EventArgs.Empty);
            else if (gameSettings.Question != null)
                uiOperation.Run(gameSettingsHintSecondKryptonButton, () => gameSettings.AnswerQuestionAsync(false),
                    ShowGameSettingsHint);
        }

        // --- Layout (ADR 0017) -------------------------------------------------------------------------------------------

        private void SetShown(Control control, bool visible)
        {
            shown[control] = visible;
            control.Visible = visible;
        }

        private bool IsShown(Control control)
        {
            // Not control.Visible: the page is created hidden and the state is shown before the page is, so Visible reads
            // false for every control then (see SettingsUserControl).
            return !shown.TryGetValue(control, out bool visible) || visible;
        }

        /// <summary>Sets the text of a wrapping label of the game group; a label without text takes no room.</summary>
        private void SetText(LauncherWrapLabel label, string text)
        {
            label.Text = text;
            SetShown(label, !string.IsNullOrEmpty(text));
        }

        /// <summary>
        /// The window was resized, or the page is laid out for the first time: the texts get the width of the page. (The
        /// layout of the page itself cannot wait for a change of its size, which the first layout does not need.)
        /// </summary>
        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            LayoutPage();
        }

        /// <summary>
        /// Lays the page out for its size: the column of the online players and the Play button at the right edge, the game
        /// column in the rest. Runs when the page is laid out and when a text that changes a height changes.
        /// </summary>
        private void LayoutPage()
        {
            // The first layouts run inside InitializeComponent and the Visual Studio designer lays the page out as well.
            if (layingOut || DesignMode || column == null)
                return;
            layingOut = true;
            try
            {
                LayoutGameColumn();
                LayoutOnlineColumn();
            }
            finally
            {
                layingOut = false;
            }
        }

        /// <summary>
        /// The game column takes the width that the online column leaves and the height of the page; the group of the game
        /// choice and the info bar (if shown) are stacked in it, and it scrolls where they are too high.
        /// </summary>
        private void LayoutGameColumn()
        {
            gameColumnPanel.SetBounds(0, 0, Math.Max(0, Width - onlineColumnWidth - 2 * PageMargin), Height);
            column.Run(PlaceColumn);
        }

        private void PlaceColumn()
        {
            LayoutGameGroup(column.ContentWidth);
            column.Place(gameSettingsKryptonGroupBox);
            if (!IsShown(gameSettingsHintKryptonPanel))
                return;
            // The info bar is as high as its text and its buttons need at the width of the column.
            gameSettingsHintKryptonPanel.Width = column.ContentWidth;
            hintStack.Run(PlaceHint);
            gameSettingsHintKryptonPanel.Height = hintStack.ContentHeight;
            column.Place(gameSettingsHintKryptonPanel);
        }

        private void PlaceHint()
        {
            hintStack.Place(gameSettingsHintKryptonWrapLabel);
            hintStack.PlaceRow(gameSettingsHintFirstKryptonButton, gameSettingsHintSecondKryptonButton);
        }

        /// <summary>The game group has the width of the column and the height of what it holds; the texts wrap at its width.</summary>
        private void LayoutGameGroup(int width)
        {
            Krypton.Toolkit.KryptonGroupBox box = gameSettingsKryptonGroupBox;
            box.Width = width;
            box.PerformLayout();
            gameStack.Run(PlaceGameGroup);
            // What the group takes besides its content (heading, borders) does not depend on the content.
            int chrome = box.Height - box.Panel.Height;
            box.Height = gameStack.ContentHeight + chrome;
        }

        private void PlaceGameGroup()
        {
            foreach (Krypton.Toolkit.KryptonRadioButton radio in entryRadios)
                PlaceEntry(radio);
            gameStack.Place(playEntriesHintKryptonWrapLabel);
            gameStack.Place(programVersionsKryptonWrapLabel);
            gameStack.Place(versionResultKryptonWrapLabel);
            gameStack.Place(integrityKryptonWrapLabel);
            gameStack.Place(playStatusKryptonWrapLabel);
            gameStack.PlaceRow(versionCheckKryptonButton, integrityKryptonButton);
        }

        /// <summary>
        /// Places the radio button of a game on its own row of the group. A name that is wider than the group (long names in a
        /// large font in the smallest window) is shown in two lines, split after the en dash ("Neo Empire Earth –" and "The Art
        /// of Conquest"), so that no name is cut off; a Krypton radio button cannot wrap by itself.
        /// </summary>
        private void PlaceEntry(Krypton.Toolkit.KryptonRadioButton radio)
        {
            string name = entryNames.TryGetValue(radio, out string known) ? known : radio.Values.Text;
            radio.Values.Text = name;
            int separator = name.IndexOf(EntrySeparator, StringComparison.Ordinal);
            if (separator > 0 && radio.GetPreferredSize(Size.Empty).Width > gameStack.ContentWidth)
                radio.Values.Text = name.Substring(0, separator + 2) + Environment.NewLine + name.Substring(separator + 3);
            gameStack.Place(radio);
        }

        /// <summary>
        /// The online players at the right edge: the group from the top of the page down to the Play button, which sits at the
        /// bottom with the width of the group.
        /// </summary>
        private void LayoutOnlineColumn()
        {
            int left = Width - PageMargin - onlineColumnWidth;
            playKryptonButton.SetBounds(left, Math.Max(0, Height - PageMargin - playButtonSize.Height), onlineColumnWidth,
                playButtonSize.Height);
            neoOnlineKryptonGroupBox.SetBounds(left, PageMargin, onlineColumnWidth,
                Math.Max(0, playKryptonButton.Top - 2 * PageMargin));
            neoOnlineKryptonGroupBox.PerformLayout();
            LayoutOnlineGroup();
        }

        /// <summary>
        /// Inside the group of the online players: the profile line at the bottom, above it the state line, and the player
        /// list in the room that is left; the link to the network check lies over the top of the list (an empty list).
        /// </summary>
        private void LayoutOnlineGroup()
        {
            Control panel = neoOnlineKryptonGroupBox.Panel;
            int width = panel.Width;
            int innerWidth = Math.Max(0, width - 2 * OnlineGroupMargin);

            int labelWidth = Math.Min(innerWidth / 2,
                Math.Max(profileLabelSize.Width, lobbyUserKryptonLabel.GetPreferredSize(Size.Empty).Width));
            int labelHeight = Math.Max(profileLabelSize.Height, lobbyUserKryptonLabel.GetPreferredSize(Size.Empty).Height);
            int rowHeight = Math.Max(labelHeight, usersLobbyKryptonComboBox.Height);
            int rowTop = Math.Max(0, panel.Height - OnlineGroupMargin - rowHeight);
            lobbyUserKryptonLabel.SetBounds(OnlineGroupMargin, rowTop + (rowHeight - labelHeight) / 2, labelWidth, labelHeight);
            int comboLeft = OnlineGroupMargin + labelWidth + OnlineGroupMargin;
            usersLobbyKryptonComboBox.SetBounds(comboLeft, rowTop + (rowHeight - usersLobbyKryptonComboBox.Height) / 2,
                Math.Max(0, width - OnlineGroupMargin - comboLeft), usersLobbyKryptonComboBox.Height);

            lobbyStatusLauncherWrapLabel.Left = OnlineGroupMargin;
            lobbyStatusLauncherWrapLabel.Width = innerWidth;
            playerListHeight = Math.Max(0, rowTop - OnlineGroupMargin);
            ShowLobbyStatus(lobbyStatusText);

            networkCheckKryptonLinkLabel.SetBounds(OnlineGroupMargin, PageMargin, innerWidth,
                Math.Max(networkLinkSize.Height, networkCheckKryptonLinkLabel.GetPreferredSize(Size.Empty).Height));
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            // Not initialized in the designer.
            if (DesignMode || logger == null)
                return;

            LoadLobbyProfiles();
            loaded = true;
            StartPlayerListPolling();
            LayoutPage();
        }

        /// <summary>
        /// Hooks the online player list (no request before the page loads); it ends with the page. The poller logs an
        /// outage once and the return of the list once (ADR 0004). Whether it polls follows the selected installation
        /// (<see cref="ApplyPlayerListPolling"/>).
        /// </summary>
        private void StartPlayerListPolling()
        {
            if (playerList == null)
            {
                neoOnlineKryptonGroupBox.Values.Heading = Resources.OnlinePlayersInvalidSettings;
                return;
            }
            neoOnlineKryptonGroupBox.Values.Heading = Resources.OnlinePlayersLoading;
            playerList.Updated += OnPlayerListUpdated;
            Disposed += (sender, e) => playerList.Dispose();
            ApplyPlayerListPolling();
        }

        /// <summary>
        /// The player list belongs to the NeoEE lobby (v1.0.0): it is polled while the selected installation is NeoEE, and
        /// not for EE or without an installation. Until the first search has finished the selection is unknown and the list
        /// says "loading". Called when the page has loaded and whenever the installations changed.
        /// </summary>
        private void ApplyPlayerListPolling()
        {
            if (playerList == null || !loaded || installations.Result == null)
                return;
            bool wasPolling = playerList.IsPolling;
            if (playerList.Apply(installations.Selected))
            {
                if (!wasPolling)
                    neoOnlineKryptonGroupBox.Values.Heading = Resources.OnlinePlayersLoading;
                return;
            }
            onlinePlayersKryptonDataGridView.Rows.Clear();
            networkCheckKryptonLinkLabel.Visible = false;
            neoOnlineKryptonGroupBox.Values.Heading = Resources.OnlinePlayersNeoOnly;
        }

        /// <summary>
        /// Reads the lobby profiles again when a discovery has finished; while one runs, the profiles of the previous
        /// result stay (the first time: "searching").
        /// </summary>
        private void OnInstallationsChanged()
        {
            RefreshVersionsIfSelectionChanged();
            if (installations.IsSearching && installations.Result != null)
                return;
            LoadLobbyProfiles();
            ApplyPlayerListPolling();
        }

        /// <summary>
        /// Fills the user combo box from the lobby profiles of the game. A missing or unreadable file is not
        /// fatal: <see cref="LobbyProfileRepository"/> logs it, it is shown in the UI, and the rest of the
        /// launcher keeps working.
        /// </summary>
        private void LoadLobbyProfiles()
        {
            usersLobbyKryptonComboBox.Items.Clear();
            usersLobbyKryptonComboBox.Enabled = true;
            ShowLobbyStatus(null);
            profiles = new LobbyPersistentData.LobbyGlobalData.PlayerInfoGlobalData[0];
            if (installations.Result == null)
            {
                ShowLobbyProfilesUnavailable(installations.IsWaitingForSetup
                    ? Resources.InstallationsWaitingForSetup
                    : Resources.InstallationsSearching);
                return;
            }

            // Both lobby files come from the EE folder of the selected installation (the profile list used to be read
            // relative to the current directory and the user files from a hard-coded installation path).
            LobbyProfilesStatus status = lobbyProfiles.LoadProfiles(installations.Selected?.EeFolder, out profiles);
            if (status != LobbyProfilesStatus.Loaded)
            {
                ShowLobbyProfilesUnavailable(Texts.LobbyProfilesProblem(status));
                return;
            }

            foreach (var profile in profiles)
                usersLobbyKryptonComboBox.Items.Add(profile.Username);
            usersLobbyKryptonComboBox.SelectedIndex = 0;
        }

        private void ShowLobbyProfilesUnavailable(string reason)
        {
            usersLobbyKryptonComboBox.Enabled = false;
            ShowLobbyStatus(reason);
        }

        /// <summary>
        /// Shows <paramref name="text"/> (why the lobby profiles are missing, or the friends of the profile) in the line above
        /// "Profile:", as high as the text needs (at most <see cref="MaxLobbyStatusHeight"/>); the player list gets shorter by
        /// as much. Null or empty hides the line. The text used to be the description in the heading of the group, where the
        /// 210 pixels of the group left only its first letters after "Online Players (unavailable)" (bug report of
        /// 2026-10-03: "Kei").
        /// </summary>
        internal void ShowLobbyStatus(string text)
        {
            lobbyStatusText = text;
            // A taller column (a larger window, a larger font) gives the line more room, but the list always keeps some.
            ShowLobbyStatus(lobbyStatusLauncherWrapLabel, onlinePlayersKryptonDataGridView, playerListHeight, text,
                Math.Max(MaxLobbyStatusHeight, playerListHeight - MinimumPlayerListHeight));
        }

        /// <summary>
        /// Puts <paramref name="text"/> into <paramref name="status"/> at the bottom of the room of <paramref name="playerList"/>
        /// (<paramref name="playerListHeight"/> from its top at 0) and shortens the list by the height of the text.
        /// </summary>
        internal static void ShowLobbyStatus(LauncherWrapLabel status, Control playerList, int playerListHeight, string text)
        {
            ShowLobbyStatus(status, playerList, playerListHeight, text, MaxLobbyStatusHeight);
        }

        /// <summary>As above, with the line taking <paramref name="maxHeight"/> at most.</summary>
        internal static void ShowLobbyStatus(LauncherWrapLabel status, Control playerList, int playerListHeight, string text,
            int maxHeight)
        {
            status.Text = text ?? string.Empty;
            int height = Math.Min(status.TextHeight(status.Width), maxHeight);
            playerList.Height = playerListHeight - height;
            status.SetBounds(status.Left, playerListHeight - height, status.Width, Math.Max(height, 1));
            status.Visible = height > 0;
        }

        /// <summary>
        /// Raised when the player clicks the link of an unavailable player list: the Tools page shows the network check and
        /// runs it (L-WP9, the outage hint of forum report section 8 row 9).
        /// </summary>
        internal event EventHandler NetworkCheckRequested;

        private void networkCheckKryptonLinkLabel_LinkClicked(object sender, EventArgs e)
        {
            NetworkCheckRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>A result of the player list, on the UI thread (the poller was started there).</summary>
        private void OnPlayerListUpdated(object sender, PlayerListUpdate update)
        {
            if (IsDisposed)
                return;

            // "Not available" links to the network check, which tells a server outage from a problem of this computer.
            bool link = OutageHint.LinksToNetworkCheck(update.Status);
            networkCheckKryptonLinkLabel.Visible = link;
            if (link)
                networkCheckKryptonLinkLabel.BringToFront();

            switch (update.Status)
            {
                case PlayerListStatus.Available:
                    ShowOnlinePlayers(update.Message);
                    break;
                case PlayerListStatus.Unavailable:
                    ShowPlayerListUnavailable();
                    break;
                default:
                    // The polling ended by an error of its own (logged by the poller).
                    onlinePlayersKryptonDataGridView.Rows.Clear();
                    neoOnlineKryptonGroupBox.Values.Heading = Resources.OnlinePlayersUnavailableSeeLog;
                    break;
            }
        }

        private void ShowOnlinePlayers(NeoApiClient.ConnectedPlayersMessage message)
        {
            onlinePlayersKryptonDataGridView.Rows.Clear();

            neoOnlineKryptonGroupBox.Values.Heading =
                string.Format(CultureInfo.CurrentCulture, Resources.OnlinePlayersFormat, message.OnlinePlayers);

            foreach (NeoApiClient.ConnectedPlayersMessage.PlayerInfo pInfo in message.PlayersInfo)
            {
                if (!usersLobbyKryptonComboBox.Text.Equals(pInfo.Name, StringComparison.InvariantCultureIgnoreCase))
                    onlinePlayersKryptonDataGridView.Rows.Add(pInfo.Name, Texts.PlayerGameState(pInfo.GameState));
            }
        }

        private void ShowPlayerListUnavailable()
        {
            onlinePlayersKryptonDataGridView.Rows.Clear();
            neoOnlineKryptonGroupBox.Values.Heading = Resources.OnlinePlayersUnavailable;
        }

        private void usersLobbyKryptonComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            int index = usersLobbyKryptonComboBox.SelectedIndex;
            string gameFolder = installations.Selected?.EeFolder;
            if (index < 0 || index >= profiles.Count || gameFolder == null)
                return;

            IDictionary<string, uint> friends;
            LobbyFriendsStatus status = lobbyProfiles.LoadFriends(gameFolder, profiles[index], out friends);
            ShowLobbyStatus(Texts.LobbyFriends(status, friends?.Count ?? 0));
        }
    }
}

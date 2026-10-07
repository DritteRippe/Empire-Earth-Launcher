using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.GameSettings;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Logging;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Core.Play;
using Empire_Earth_Launcher.Core.Settings;

namespace Empire_Earth_Launcher
{
    /// <summary>The defaults state of one game for the Game settings page.</summary>
    internal sealed class GameDefaultsLine
    {
        public GameDefaultsLine(Game game, DefaultsStatus status)
        {
            Game = game;
            Status = status;
        }

        public Game Game { get; }

        public DefaultsStatus Status { get; }
    }

    /// <summary>
    /// The state of the game settings for the pages (contract 3, L-WP5): the defaults of the selected installation, the
    /// display question of the first run, the hints of the consistency checks and the compatibility options, with the
    /// actions of the Game settings page and the info bar of the Play page.
    /// </summary>
    /// <remarks>
    /// Created once by <see cref="Program"/>; use it on the UI thread. The core work runs on the thread pool
    /// (<see cref="Task.Run(Action)"/>), and <see cref="Changed"/> is raised on the thread that started it, the UI thread,
    /// after it. The pages start the actions through <see cref="UiOperation"/>.
    /// </remarks>
    internal sealed class GameSettingsModel
    {
        private readonly GameDefaultsService defaults;
        private readonly ConsistencyChecker checker;
        private readonly CompatibilityOptions compatibility;
        private readonly SettingsStore settings;
        private readonly ISystemInfo systemInfo;
        private readonly ILogger logger;

        /// <param name="defaults">The defaults, display settings and reset.</param>
        /// <param name="checker">The consistency checks.</param>
        /// <param name="compatibility">The compatibility options.</param>
        /// <param name="settings">settings.json, for the hidden hints.</param>
        /// <param name="systemInfo">For the scaling of the screen.</param>
        /// <param name="backupFolder">The backup folder, named in the confirmation of the reset.</param>
        /// <param name="logger">Log of the launcher.</param>
        public GameSettingsModel(GameDefaultsService defaults, ConsistencyChecker checker, CompatibilityOptions compatibility,
            SettingsStore settings, ISystemInfo systemInfo, string backupFolder, ILogger logger)
        {
            this.defaults = defaults ?? throw new ArgumentNullException(nameof(defaults));
            this.checker = checker ?? throw new ArgumentNullException(nameof(checker));
            this.compatibility = compatibility ?? throw new ArgumentNullException(nameof(compatibility));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.systemInfo = systemInfo ?? throw new ArgumentNullException(nameof(systemInfo));
            BackupFolder = backupFolder ?? throw new ArgumentNullException(nameof(backupFolder));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            Lines = new GameDefaultsLine[0];
            Findings = new ConsistencyFinding[0];
        }

        /// <summary>Raised when the state changed.</summary>
        public event EventHandler Changed;

        /// <summary>The discovery result the state belongs to; null before the first one.</summary>
        public DiscoveryResult Result { get; private set; }

        /// <summary>The selected installation; null if none was found.</summary>
        public Installation Selected
        {
            get { return Result?.Selected; }
        }

        /// <summary>Why the start wrote nothing (a setup or a game ran), else null.</summary>
        public MutationCheck StartBlock { get; private set; }

        /// <summary>The display question of the first run, until it is answered; null if there is none.</summary>
        public DisplayQuestion Question { get; private set; }

        /// <summary>The defaults state of each game of the selected installation.</summary>
        public IReadOnlyList<GameDefaultsLine> Lines { get; private set; }

        /// <summary>Every finding of the consistency checks of the selected installation (the page lists all of them).</summary>
        public IReadOnlyList<ConsistencyFinding> Findings { get; private set; }

        /// <summary>The findings the player did not hide (the info bar of the Play page).</summary>
        public IReadOnlyList<ConsistencyFinding> VisibleFindings
        {
            get { return HintVisibility.Visible(settings.Current, Findings); }
        }

        /// <summary>The compatibility options of the selected installation; null if there is none.</summary>
        public CompatibilityState Compatibility { get; private set; }

        /// <summary>The result of the last action of the page (reset, display settings, answer), else null.</summary>
        public GameSettingsResult LastResult { get; private set; }

        /// <summary>The result of the last change of a compatibility option, else null.</summary>
        public CompatibilityResult LastCompatibilityResult { get; private set; }

        /// <summary>The backup folder of the launcher (<c>%LOCALAPPDATA%\Empire Earth Launcher\Backups</c>).</summary>
        public string BackupFolder { get; }

        /// <summary>The scaling of the primary screen in percent (ADR 0011).</summary>
        public int ScalingPercent
        {
            get { return systemInfo.ScalingPercent(); }
        }

        /// <summary>True while the selected installation is unambiguous for its settings key (ADR 0015).</summary>
        public bool IsUnambiguous
        {
            get { return Selected != null && Result.IsUnambiguous(Selected); }
        }

        /// <summary>True if switching <paramref name="entry"/> off needs the hint first (ADR 0011 plan review).</summary>
        public bool NeedsHighDpiHint(string entry, bool on)
        {
            return compatibility.NeedsHighDpiHint(entry, on);
        }

        /// <summary>
        /// After a discovery: the defaults at the start (contract 3.6, only for unambiguous installations, ADR 0015), then the
        /// state of the selected installation. Runs after every discovery, so a folder the player chooses gets its
        /// defaults at once.
        /// </summary>
        public async Task ApplyAfterDiscoveryAsync(DiscoveryResult result)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));
            Result = result;
            DefaultsStartup startup = await Task.Run(() => defaults.ApplyAtLauncherStart(result));
            StartBlock = startup.Block;
            Question = startup.Question;
            foreach (GameDefaultsAtStart game in startup.Games)
                logger.Info("Game defaults at the start: " + game + ".");
            await RefreshAsync();
        }

        /// <summary>Reads the state of the selected installation again (statuses, findings, compatibility options).</summary>
        public async Task RefreshAsync()
        {
            Installation selected = Selected;
            if (selected == null)
            {
                Lines = new GameDefaultsLine[0];
                Findings = new ConsistencyFinding[0];
                Compatibility = null;
            }
            else
            {
                bool unambiguous = IsUnambiguous;
                var state = await Task.Run(() => Tuple.Create(
                    GameDefaultsService.GamesOf(selected)
                                       .Select(game => new GameDefaultsLine(game, defaults.GetStatus(selected, game, unambiguous)))
                                       .ToList(),
                    checker.Check(selected),
                    compatibility.Read(selected)));
                Lines = state.Item1;
                Findings = state.Item2;
                Compatibility = state.Item3;
            }
            Changed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Answers the display question (contract 3.6 step 3).</summary>
        public async Task AnswerQuestionAsync(bool apply)
        {
            DisplayQuestion question = Question;
            if (question == null)
                return;
            LastResult = await Task.Run(() => defaults.AnswerDisplayQuestion(question, apply));
            if (LastResult.IsDone)
                Question = null;
            await RefreshAsync();
        }

        /// <summary>
        /// Adds the display question of a first run before Play (contract 3.6 step 3, L-WP6) to the open question; the info
        /// bar of the Play page shows it.
        /// </summary>
        public void AddQuestion(DisplayQuestion question)
        {
            if (question == null)
                throw new ArgumentNullException(nameof(question));
            Question = DisplayQuestion.Combine(Question, question);
            Changed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>"Apply recommended display" for the selected installation.</summary>
        public async Task ApplyRecommendedDisplayAsync()
        {
            Installation selected = Selected;
            if (selected == null)
                return;
            LastResult = await Task.Run(() => defaults.ApplyRecommendedDisplay(selected));
            await RefreshAsync();
        }

        /// <summary>The reset of the selected installation (after the player confirmed it).</summary>
        public async Task ResetAsync()
        {
            Installation selected = Selected;
            if (selected == null)
                return;
            LastResult = await Task.Run(() => defaults.Reset(selected));
            if (LastResult.IsDone && Question != null &&
                Question.Items.All(item => item.Installation == selected))
                Question = null; // the reset wrote every display value and the markers
            await RefreshAsync();
        }

        /// <summary>Switches a compatibility entry for the programs of the selected installation.</summary>
        public async Task SetCompatibilityEntryAsync(string entry, bool on)
        {
            Installation selected = Selected;
            if (selected == null)
                return;
            LastCompatibilityResult = await Task.Run(() => compatibility.SetEntry(selected, entry, on));
            await RefreshAsync();
        }

        /// <summary>Removes the HKCU values that are exactly <c>~ RUNASADMIN</c> (with a backup).</summary>
        public async Task RemoveRunAsAdminAsync()
        {
            Installation selected = Selected;
            if (selected == null)
                return;
            LastCompatibilityResult = await Task.Run(() => compatibility.RemoveRunAsAdmin(selected));
            await RefreshAsync();
        }

        /// <summary>True if the player hid the hint (for its current values).</summary>
        public bool IsHidden(ConsistencyFinding finding)
        {
            return HintVisibility.IsHidden(settings.Current, finding);
        }

        /// <summary>Hides a hint from the Play page or shows it again, and saves settings.json.</summary>
        public void SetHidden(ConsistencyFinding finding, bool hidden)
        {
            if (finding == null)
                throw new ArgumentNullException(nameof(finding));
            if (hidden)
                HintVisibility.Hide(settings.Current, finding);
            else
                HintVisibility.Show(settings.Current, finding);
            settings.Save();
            logger.Info("The hint " + finding.HintKey + " is " + (hidden ? "hidden" : "shown again") + " on the Play page.");
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}

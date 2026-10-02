using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Core.Play;

namespace Empire_Earth_Launcher.Core.GameSettings
{
    /// <summary>The state of the defaults marker of one game for this account (contract 3.5).</summary>
    public enum MarkerState
    {
        /// <summary>No marker (or one that is not a REG_DWORD): the defaults were never applied for this account.</summary>
        Missing,

        /// <summary>An older contract version: the values added since then are created if missing, then the marker is raised.</summary>
        Lower,

        /// <summary>The launcher's contract version: only class S is kept in sync.</summary>
        Current,

        /// <summary>A newer contract version: nothing is overwritten.</summary>
        Higher
    }

    /// <summary>The defaults marker <c>GameDefaults\&lt;Product&gt;</c>, value <c>EE</c> or <c>AoC</c> (contract 3.5).</summary>
    public sealed class DefaultsMarker
    {
        internal DefaultsMarker(MarkerState state, int version)
        {
            State = state;
            Version = version;
        }

        public MarkerState State { get; }

        /// <summary>The contract version in the marker; 0 if it is missing.</summary>
        public int Version { get; }

        /// <summary>The state of a marker value as read (null if missing).</summary>
        public static DefaultsMarker From(RegistryValue value)
        {
            if (value == null || value.Type != RegistryValueType.DWord)
                return new DefaultsMarker(MarkerState.Missing, 0);
            int version = value.DWordValue;
            MarkerState state = version < ContractNames.ContractVersion ? MarkerState.Lower
                : version == ContractNames.ContractVersion ? MarkerState.Current
                : MarkerState.Higher;
            return new DefaultsMarker(state, version);
        }

        public override string ToString()
        {
            return State == MarkerState.Missing ? "missing" : State + " (" + Version.ToString(CultureInfo.InvariantCulture) + ")";
        }
    }

    /// <summary>What the defaults of a game look like for the Game settings page.</summary>
    public enum DefaultsStatus
    {
        /// <summary>Applied for this account by this contract version (marker current).</summary>
        Applied,

        /// <summary>Applied by a newer launcher or setup (marker higher): left as they are.</summary>
        AppliedByNewerVersion,

        /// <summary>Not applied yet; the launcher applies them at its start (the installation is unambiguous).</summary>
        Pending,

        /// <summary>Not applied yet; several installations share the settings key, so the first Play of the game applies them (ADR 0015).</summary>
        WaitingForPlay,

        /// <summary>The installation was made by a newer setup than the launcher knows: no defaults and no reset (contract 5).</summary>
        NewerContract
    }

    /// <summary>How a change of the game settings ended.</summary>
    public enum GameSettingsOutcome
    {
        /// <summary>Done; <see cref="GameSettingsResult.Changes"/> lists what changed (possibly nothing).</summary>
        Done,

        /// <summary>A setup or a game runs (ADR 0016): nothing was changed.</summary>
        Blocked,

        /// <summary>The installation was made by a newer setup than the launcher knows (contract 5): nothing was changed.</summary>
        NewerContract,

        /// <summary>The <c>.reg</c> backup could not be written completely: nothing was changed (contract 3.6).</summary>
        BackupFailed,

        /// <summary>A value could not be read or written (logged); the backup, if any, restores the previous state.</summary>
        Failed,

        /// <summary>The game folder is not on a drive letter (a network path): the class S values cannot be written (contract 3.3).</summary>
        NotOnADrive
    }

    /// <summary>One value the launcher wrote or deleted, with the value before (for the log and the result).</summary>
    public sealed class ValueChange
    {
        internal ValueChange(RegistryLocation key, string valueName, RegistryValue oldValue, RegistryValue newValue)
        {
            Key = key;
            ValueName = valueName;
            OldValue = oldValue;
            NewValue = newValue;
        }

        public RegistryLocation Key { get; }

        public string ValueName { get; }

        /// <summary>The value before; null if it did not exist.</summary>
        public RegistryValue OldValue { get; }

        /// <summary>The value after; null if it was deleted.</summary>
        public RegistryValue NewValue { get; }

        public override string ToString()
        {
            return Key + " @\"" + ValueName + "\": " + (OldValue?.ToString() ?? "missing") + " -> " + (NewValue?.ToString() ?? "deleted");
        }
    }

    /// <summary>The result of a change of the game settings: outcome, the block or problem, the backups and the changes.</summary>
    public sealed class GameSettingsResult
    {
        internal GameSettingsResult(GameSettingsOutcome outcome, MutationCheck block, IEnumerable<string> backupFiles,
            IEnumerable<ValueChange> changes, string problem)
        {
            Outcome = outcome;
            Block = block;
            BackupFiles = new ReadOnlyCollection<string>((backupFiles ?? Enumerable.Empty<string>()).ToList());
            Changes = new ReadOnlyCollection<ValueChange>((changes ?? Enumerable.Empty<ValueChange>()).ToList());
            Problem = problem;
        }

        public GameSettingsOutcome Outcome { get; }

        /// <summary>Why the change was blocked (<see cref="GameSettingsOutcome.Blocked"/>), else null.</summary>
        public MutationCheck Block { get; }

        /// <summary>The <c>.reg</c> files written before the change (full paths); empty if none.</summary>
        public IReadOnlyList<string> BackupFiles { get; }

        /// <summary>The folder of the backups, or null.</summary>
        public string BackupFolder
        {
            get { return BackupFiles.Count == 0 ? null : WinPath.GetParent(BackupFiles[0]); }
        }

        public IReadOnlyList<ValueChange> Changes { get; }

        /// <summary>For the log and the details of an error: what failed; null if nothing did.</summary>
        public string Problem { get; }

        public bool IsDone
        {
            get { return Outcome == GameSettingsOutcome.Done; }
        }

        internal static GameSettingsResult Blocked(MutationCheck block)
        {
            return new GameSettingsResult(GameSettingsOutcome.Blocked, block, null, null, null);
        }

        public override string ToString()
        {
            return Outcome + (Block == null ? string.Empty : " " + Block) +
                   ", " + Changes.Count.ToString(CultureInfo.InvariantCulture) + " changes" +
                   (BackupFiles.Count == 0 ? string.Empty : ", backup " + BackupFolder) +
                   (Problem == null ? string.Empty : ": " + Problem);
        }
    }

    /// <summary>A display value (class D) that differs from its recommended data.</summary>
    public sealed class ValueDifference
    {
        internal ValueDifference(GameSetting setting, RegistryValue current, RegistryValue recommended)
        {
            Setting = setting;
            Current = current;
            Recommended = recommended;
        }

        public GameSetting Setting { get; }

        public RegistryValue Current { get; }

        public RegistryValue Recommended { get; }
    }

    /// <summary>The display values of one game that differ from the recommended ones at its first run.</summary>
    public sealed class DisplayQuestionItem
    {
        internal DisplayQuestionItem(Installation installation, Game game, IEnumerable<ValueDifference> differences)
        {
            Installation = installation;
            Game = game;
            Differences = new ReadOnlyCollection<ValueDifference>(differences.ToList());
        }

        public Installation Installation { get; }

        public Game Game { get; }

        public IReadOnlyList<ValueDifference> Differences { get; }
    }

    /// <summary>
    /// The one non-blocking question of the first run (contract 3.6 step 3): "apply the recommended display settings?" for
    /// every game whose existing display values differ. Answered with
    /// <see cref="GameDefaultsService.AnswerDisplayQuestion"/>; the marker is written with either answer.
    /// </summary>
    public sealed class DisplayQuestion
    {
        internal DisplayQuestion(IEnumerable<DisplayQuestionItem> items)
        {
            Items = new ReadOnlyCollection<DisplayQuestionItem>(items.ToList());
        }

        public IReadOnlyList<DisplayQuestionItem> Items { get; }

        /// <summary>
        /// Both questions as one, e.g. the open question of the launcher start and the one of a first Play (L-WP6): the items
        /// of <paramref name="first"/>, then those of <paramref name="second"/> about another game of another installation.
        /// </summary>
        /// <returns>The combined question; the other one if one of them is null; null if both are.</returns>
        public static DisplayQuestion Combine(DisplayQuestion first, DisplayQuestion second)
        {
            if (first == null)
                return second;
            if (second == null)
                return first;
            var items = first.Items.ToList();
            items.AddRange(second.Items.Where(item => !items.Any(known => known.Game == item.Game &&
                known.Installation.Product == item.Installation.Product &&
                WinPath.IsSamePath(known.Installation.Root, item.Installation.Root))));
            return new DisplayQuestion(items);
        }
    }

    /// <summary>What the launcher did with the class S values of a game at its start (ADR 0015 plan review).</summary>
    public enum InstalledFromAtStart
    {
        /// <summary>Not looked at: the installation is ambiguous, blocked, or its folder is missing.</summary>
        NotChecked,

        /// <summary>Both values were missing and have been created.</summary>
        Created,

        /// <summary>Both values exist (equal or not): left to the sync before Play.</summary>
        Present,

        /// <summary>Only one value exists: nothing written at start (logged), Play syncs it.</summary>
        Incomplete,

        /// <summary>The game folder is not on a drive letter: no values, a warning (contract 3.3).</summary>
        NotOnADrive,

        /// <summary>Reading or writing failed (logged).</summary>
        Failed
    }

    /// <summary>What the launcher did with the defaults of a game at its start (contract 3.6, ADR 0015).</summary>
    public enum DefaultsAtStart
    {
        /// <summary>Nothing to do: the marker is current or higher.</summary>
        None,

        /// <summary>The first run created the missing values and wrote the marker.</summary>
        FirstRun,

        /// <summary>The first run created the missing values; the marker waits for the answer to the display question.</summary>
        FirstRunWithQuestion,

        /// <summary>The marker was lower: the values added since then were created and the marker raised.</summary>
        Updated,

        /// <summary>Not applied: several installations share the settings key (ADR 0015).</summary>
        Ambiguous,

        /// <summary>Not applied: the installation was made by a newer setup (contract 5).</summary>
        NewerContract,

        /// <summary>Not applied: the chosen folder does not exist.</summary>
        FolderMissing,

        /// <summary>Not applied: a setup or a game runs (ADR 0016).</summary>
        Blocked,

        /// <summary>A value could not be read or written (logged); the marker was not written, so the next start repeats it.</summary>
        Failed
    }

    /// <summary>The start of the defaults for one game of one installation.</summary>
    public sealed class GameDefaultsAtStart
    {
        internal GameDefaultsAtStart(Installation installation, Game game, InstalledFromAtStart installedFrom, DefaultsAtStart defaults)
        {
            Installation = installation;
            Game = game;
            InstalledFrom = installedFrom;
            Defaults = defaults;
        }

        public Installation Installation { get; }

        public Game Game { get; }

        public InstalledFromAtStart InstalledFrom { get; }

        public DefaultsAtStart Defaults { get; }

        public override string ToString()
        {
            return Installation.Root + " " + Game.Id + ": Installed From " + InstalledFrom + ", defaults " + Defaults;
        }
    }

    /// <summary>The result of <see cref="GameDefaultsService.ApplyAtLauncherStart"/>.</summary>
    public sealed class DefaultsStartup
    {
        internal DefaultsStartup(MutationCheck block, IEnumerable<GameDefaultsAtStart> games, DisplayQuestion question)
        {
            Block = block;
            Games = new ReadOnlyCollection<GameDefaultsAtStart>((games ?? Enumerable.Empty<GameDefaultsAtStart>()).ToList());
            Question = question;
        }

        /// <summary>Why nothing was written (a setup or a game runs), else null.</summary>
        public MutationCheck Block { get; }

        /// <summary>One entry per game of every installation found.</summary>
        public IReadOnlyList<GameDefaultsAtStart> Games { get; }

        /// <summary>The display question of the first run, or null if there is none.</summary>
        public DisplayQuestion Question { get; }
    }
}

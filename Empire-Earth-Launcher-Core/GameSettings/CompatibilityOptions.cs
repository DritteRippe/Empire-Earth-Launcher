using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Empire_Earth_Launcher.Core.Backup;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Logging;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Core.Play;

namespace Empire_Earth_Launcher.Core.GameSettings
{
    /// <summary>Whether a compatibility entry is in the HKCU values of the programs of an installation.</summary>
    public enum EntryState
    {
        /// <summary>In no HKCU value.</summary>
        Off,

        /// <summary>In the HKCU value of every program.</summary>
        On,

        /// <summary>In the HKCU value of some programs only.</summary>
        Mixed
    }

    /// <summary>Why an entry is not offered as a switch.</summary>
    public enum EntryUnavailable
    {
        /// <summary>It is offered.</summary>
        None,

        /// <summary>Windows 7 or Wine: no switches at all (contract 3.7, ADR 0007 plan review).</summary>
        WindowsVersion,

        /// <summary>
        /// The HKLM value (written by an admin setup for all users) or the HKCU value already has a Windows version mode:
        /// no second one is offered (ADR 0015).
        /// </summary>
        VersionModeSet
    }

    /// <summary>The compatibility values of one program: HKCU (switchable) and HKLM (read-only).</summary>
    public sealed class ProgramLayers
    {
        internal ProgramLayers(Game game, string programPath, string currentUser, string localMachine)
        {
            Game = game;
            ProgramPath = programPath;
            CurrentUser = currentUser;
            LocalMachine = localMachine;
        }

        public Game Game { get; }

        /// <summary>The full path of the program, the value name.</summary>
        public string ProgramPath { get; }

        /// <summary>The HKCU value, or null.</summary>
        public string CurrentUser { get; }

        /// <summary>The HKLM value (64-bit view, else 32-bit view), or null; read-only for the launcher (contract 3.7).</summary>
        public string LocalMachine { get; }
    }

    /// <summary>One switchable entry and its state.</summary>
    public sealed class CompatibilityEntry
    {
        internal CompatibilityEntry(string name, EntryState state, EntryUnavailable unavailable, bool inLocalMachine)
        {
            Name = name;
            State = state;
            Unavailable = unavailable;
            InLocalMachine = inLocalMachine;
        }

        /// <summary>One of <see cref="CompatibilityLayers.LauncherEntries"/>.</summary>
        public string Name { get; }

        public EntryState State { get; }

        public EntryUnavailable Unavailable { get; }

        /// <summary>True if the read-only HKLM value of a program has the entry (it applies whatever HKCU says).</summary>
        public bool InLocalMachine { get; }

        public bool IsOffered
        {
            get { return Unavailable == EntryUnavailable.None; }
        }
    }

    /// <summary>A value of the old list of contract 3.7 (Windows Vista and 7), shown read-only with the advice to run the setup.</summary>
    public sealed class LegacyLayerValue
    {
        internal LegacyLayerValue(RegistryLocation key, string programPath, string value)
        {
            Key = key;
            ProgramPath = programPath;
            Value = value;
        }

        public RegistryLocation Key { get; }

        public string ProgramPath { get; }

        public string Value { get; }
    }

    /// <summary>What the Game settings page shows of the compatibility options of an installation.</summary>
    public sealed class CompatibilityState
    {
        internal CompatibilityState(bool switchesOffered, IEnumerable<ProgramLayers> programs, IEnumerable<CompatibilityEntry> entries,
            bool runAsAdminRemovable, IEnumerable<LegacyLayerValue> legacyValues)
        {
            SwitchesOffered = switchesOffered;
            Programs = new ReadOnlyCollection<ProgramLayers>(programs.ToList());
            Entries = new ReadOnlyCollection<CompatibilityEntry>(entries.ToList());
            RunAsAdminRemovable = runAsAdminRemovable;
            LegacyValues = new ReadOnlyCollection<LegacyLayerValue>(legacyValues.ToList());
        }

        /// <summary>True from Windows 8 on and outside Wine: the entries can be switched (contract 3.7).</summary>
        public bool SwitchesOffered { get; }

        /// <summary>The programs of the installation with their HKCU and HKLM values.</summary>
        public IReadOnlyList<ProgramLayers> Programs { get; }

        /// <summary>The switchable entries; empty when <see cref="SwitchesOffered"/> is false.</summary>
        public IReadOnlyList<CompatibilityEntry> Entries { get; }

        /// <summary>True if the HKCU value of a program is exactly <c>~ RUNASADMIN</c> (contract 3.7: MAY be removed).</summary>
        public bool RunAsAdminRemovable { get; }

        /// <summary>
        /// On Windows 7 and under Wine: old values of earlier setups in HKCU or HKLM (<c>IsLegacyVistaCompatValue</c>),
        /// shown read-only with the advice to run the current setup, which removes them (ADR 0007 plan review).
        /// </summary>
        public IReadOnlyList<LegacyLayerValue> LegacyValues { get; }
    }

    /// <summary>The outcome of a change of the compatibility values.</summary>
    public sealed class CompatibilityResult
    {
        internal CompatibilityResult(GameSettingsOutcome outcome, MutationCheck block, IEnumerable<ValueChange> changes,
            string backupFile, string problem)
        {
            Outcome = outcome;
            Block = block;
            Changes = new ReadOnlyCollection<ValueChange>((changes ?? Enumerable.Empty<ValueChange>()).ToList());
            BackupFile = backupFile;
            Problem = problem;
        }

        public GameSettingsOutcome Outcome { get; }

        public MutationCheck Block { get; }

        public IReadOnlyList<ValueChange> Changes { get; }

        /// <summary>The <c>.reg</c> backup (removing <c>~ RUNASADMIN</c>), else null.</summary>
        public string BackupFile { get; }

        public string Problem { get; }

        public override string ToString()
        {
            return Outcome + (Block == null ? string.Empty : " " + Block) + ", " +
                   Changes.Count.ToString(CultureInfo.InvariantCulture) + " changes" + (Problem == null ? string.Empty : ": " + Problem);
        }
    }

    /// <summary>
    /// The compatibility options of the Game settings page (contract 3.7, ADR 0007 plan review, ADR 0015): the entries of the
    /// rows <c>compatibility</c> and <c>compatibility_windows</c> as switches in HKCU for the programs of an installation, from
    /// Windows 8 on and outside Wine; HKLM shown read-only; <c>~ RUNASADMIN</c> removable on every Windows; on Windows 7 the old
    /// values only shown.
    /// </summary>
    /// <remarks>
    /// A switch adds or removes one entry in the HKCU value of every program of the installation and keeps every other entry
    /// (the write policy checks the content). Its undo is the switch itself, so it makes no backup; removing
    /// <c>~ RUNASADMIN</c>, which the launcher cannot add back, writes a <c>.reg</c> backup first (ADR 0007). Every change asks the
    /// mutation guard.
    /// </remarks>
    public sealed class CompatibilityOptions
    {
        /// <summary>The keys of the compatibility values: HKCU, then HKLM in the 64-bit and the 32-bit view.</summary>
        public static readonly IReadOnlyList<RegistryLocation> LayerKeys = new ReadOnlyCollection<RegistryLocation>(new[]
        {
            RegistryLocation.CurrentUser(ContractNames.CompatibilityLayersKey),
            RegistryLocation.LocalMachine64(ContractNames.CompatibilityLayersKey),
            RegistryLocation.LocalMachine32(ContractNames.CompatibilityLayersKey)
        });

        private static readonly RegistryLocation CurrentUserLayers = LayerKeys[0];

        private readonly IRegistry registry;
        private readonly ISystemInfo systemInfo;
        private readonly MutationGuard guard;
        private readonly BackupLocations backups;
        private readonly ILogger logger;

        public CompatibilityOptions(IRegistry registry, ISystemInfo systemInfo, MutationGuard guard, BackupLocations backups,
            ILogger logger)
        {
            this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
            this.systemInfo = systemInfo ?? throw new ArgumentNullException(nameof(systemInfo));
            this.guard = guard ?? throw new ArgumentNullException(nameof(guard));
            this.backups = backups ?? throw new ArgumentNullException(nameof(backups));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>True if switches are offered on this computer (Windows 8 and later, not Wine).</summary>
        public bool SwitchesOffered
        {
            get { return LauncherWritePolicy.AreLayerEntriesAllowed(systemInfo); }
        }

        /// <summary>
        /// True if switching <c>HIGHDPIAWARE</c> off needs the hint first (ADR 0011 plan review): the screen is scaled above
        /// 100 %, so the game then sees the smaller logical screen.
        /// </summary>
        public bool NeedsHighDpiHint(string entry, bool on)
        {
            return !on && string.Equals(entry, CompatibilityLayers.HighDpiAware, StringComparison.OrdinalIgnoreCase) &&
                   systemInfo.ScalingPercent() > 100;
        }

        /// <summary>Reads the values of the programs of the installation.</summary>
        public CompatibilityState Read(Installation installation)
        {
            if (installation == null)
                throw new ArgumentNullException(nameof(installation));
            var programs = GameDefaultsService.GamesOf(installation)
                .Select(game => GameDefaultsService.ProgramPath(installation, game) is string path
                    ? new ProgramLayers(game, path, ReadString(CurrentUserLayers, path), ReadLocalMachine(path))
                    : null)
                .Where(program => program != null && RegistryWriteRule.IsProgramPath(program.ProgramPath))
                .ToList();

            bool offered = SwitchesOffered;
            var entries = new List<CompatibilityEntry>();
            if (offered)
            {
                foreach (string entry in CompatibilityLayers.LauncherEntries)
                {
                    int count = programs.Count(program => CompatibilityLayerValue.Parse(program.CurrentUser).Contains(entry));
                    EntryState state = count == 0 ? EntryState.Off : count == programs.Count ? EntryState.On : EntryState.Mixed;
                    bool inLocalMachine = programs.Any(program => CompatibilityLayerValue.Parse(program.LocalMachine).Contains(entry));
                    // ADR 0015: with a Windows version mode in HKLM (or another one in HKCU) no version mode is switched on;
                    // one that is on in HKCU can still be switched off.
                    EntryUnavailable unavailable = EntryUnavailable.None;
                    if (state == EntryState.Off && CompatibilityLayers.IsWindowsVersionMode(entry) && programs.Any(program =>
                            HasOtherVersionMode(program.LocalMachine, null) || HasOtherVersionMode(program.CurrentUser, entry)))
                        unavailable = EntryUnavailable.VersionModeSet;
                    entries.Add(new CompatibilityEntry(entry, state, unavailable, inLocalMachine));
                }
            }

            bool runAsAdmin = programs.Any(program =>
                string.Equals(program.CurrentUser, CompatibilityLayers.LegacyRunAsAdminValue, StringComparison.Ordinal));
            var legacy = new List<LegacyLayerValue>();
            if (!offered)
            {
                foreach (ProgramLayers program in programs)
                {
                    foreach (RegistryLocation key in LayerKeys)
                    {
                        string value = ReadString(key, program.ProgramPath);
                        if (CompatibilityLayers.IsLegacyVistaValue(value) &&
                            !legacy.Any(known => known.ProgramPath == program.ProgramPath && known.Value == value &&
                                                 known.Key.Hive == key.Hive))
                            legacy.Add(new LegacyLayerValue(key, program.ProgramPath, value));
                    }
                }
            }
            return new CompatibilityState(offered, programs, entries, runAsAdmin, legacy);
        }

        /// <summary>
        /// Switches <paramref name="entry"/> on or off in the HKCU value of every program of the installation; every other
        /// entry stays. A value left without entries is deleted.
        /// </summary>
        public CompatibilityResult SetEntry(Installation installation, string entry, bool on)
        {
            if (installation == null)
                throw new ArgumentNullException(nameof(installation));
            if (!CompatibilityLayers.IsLauncherEntry(entry))
                throw new ArgumentException("The launcher only switches the entries of contract 3.7: " + entry, nameof(entry));

            MutationCheck check = guard.Check("switch the compatibility option " + entry + (on ? " on" : " off"));
            if (!check.IsAllowed)
                return new CompatibilityResult(GameSettingsOutcome.Blocked, check, null, null, null);
            CompatibilityState state = Read(installation);
            CompatibilityEntry offered = state.Entries.FirstOrDefault(e => string.Equals(e.Name, entry, StringComparison.OrdinalIgnoreCase));
            if (offered == null || (on && !offered.IsOffered))
            {
                logger.Warning("Compatibility: " + entry + " is not offered on this computer for " + installation.Root + "; nothing changed.");
                return new CompatibilityResult(GameSettingsOutcome.Failed, null, null, null, entry + " is not offered");
            }

            var changes = new List<ValueChange>();
            foreach (ProgramLayers program in state.Programs)
            {
                CompatibilityLayerValue current = CompatibilityLayerValue.Parse(program.CurrentUser);
                if (current.Contains(entry) == on)
                    continue;
                CompatibilityLayerValue next = on ? current.With(entry) : current.Without(entry);
                if (!Write(program, next, changes))
                    return new CompatibilityResult(GameSettingsOutcome.Failed, null, changes, null,
                        "the compatibility value of " + program.ProgramPath + " could not be written (see the log)");
            }
            return new CompatibilityResult(GameSettingsOutcome.Done, null, changes, null, null);
        }

        /// <summary>
        /// Removes the HKCU values that are exactly <c>~ RUNASADMIN</c> (contract 3.7, like the setup's
        /// <c>RemoveLegacyRunAsAdmin</c>), on every Windows, after a <c>.reg</c> backup of them.
        /// </summary>
        public CompatibilityResult RemoveRunAsAdmin(Installation installation)
        {
            if (installation == null)
                throw new ArgumentNullException(nameof(installation));
            MutationCheck check = guard.Check("remove ~ RUNASADMIN");
            if (!check.IsAllowed)
                return new CompatibilityResult(GameSettingsOutcome.Blocked, check, null, null, null);

            List<ProgramLayers> programs = Read(installation).Programs
                .Where(program => string.Equals(program.CurrentUser, CompatibilityLayers.LegacyRunAsAdminValue, StringComparison.Ordinal))
                .ToList();
            if (programs.Count == 0)
                return new CompatibilityResult(GameSettingsOutcome.Done, null, null, null, null);

            var backup = new RegFileKey(CurrentUserLayers);
            foreach (ProgramLayers program in programs)
                backup.Add(program.ProgramPath, RegistryValue.FromString(program.CurrentUser));
            FileSystemResult<BackupFolder> folder = backups.CreateSubfolder("remove-runasadmin");
            string file = folder.IsOk ? WinPath.Combine(folder.Value.Path, BackupLocations.TimeStamp(folder.Value.Time) + "_Layers.reg") : null;
            if (!folder.IsOk || !backups.WriteRegFile(file, new[] { backup }).IsOk)
            {
                logger.Error("Compatibility: nothing was changed because the backup of the compatibility values failed.");
                return new CompatibilityResult(GameSettingsOutcome.BackupFailed, null, null, null, "the backup failed");
            }

            var changes = new List<ValueChange>();
            foreach (ProgramLayers program in programs)
            {
                if (!Write(program, CompatibilityLayerValue.Parse(string.Empty), changes))
                    return new CompatibilityResult(GameSettingsOutcome.Failed, null, changes, file, "see the log");
            }
            return new CompatibilityResult(GameSettingsOutcome.Done, null, changes, file, null);
        }

        /// <summary>Writes <paramref name="next"/> into HKCU, or deletes the value when it has no entries.</summary>
        private bool Write(ProgramLayers program, CompatibilityLayerValue next, List<ValueChange> changes)
        {
            RegistryValue old = program.CurrentUser == null ? null : RegistryValue.FromString(program.CurrentUser);
            RegistryResult result;
            RegistryValue written = null;
            if (next.IsEmpty)
            {
                if (old == null)
                    return true;
                result = registry.DeleteValue(CurrentUserLayers, program.ProgramPath);
            }
            else
            {
                if (registry.ProbeKey(CurrentUserLayers).Status == RegistryStatus.Missing)
                {
                    RegistryResult created = registry.CreateSubKey(CurrentUserLayers);
                    if (!created.IsOk)
                    {
                        logger.Error("Compatibility: unable to create " + CurrentUserLayers + ": " + created + ".");
                        return false;
                    }
                }
                written = RegistryValue.FromString(next.ToString());
                result = registry.SetValue(CurrentUserLayers, program.ProgramPath, written);
            }
            if (!result.IsOk)
            {
                logger.Error("Compatibility: unable to change " + CurrentUserLayers + " @\"" + program.ProgramPath + "\": " + result + ".");
                return false;
            }
            var change = new ValueChange(CurrentUserLayers, program.ProgramPath, old, written);
            changes.Add(change);
            logger.Info("Compatibility: changed " + change + ".");
            return true;
        }

        private string ReadLocalMachine(string programPath)
        {
            return ReadString(LayerKeys[1], programPath) ?? ReadString(LayerKeys[2], programPath);
        }

        private string ReadString(RegistryLocation key, string valueName)
        {
            RegistryResult<RegistryValue> value = registry.GetValue(key, valueName);
            return value.IsOk && value.Value.IsString ? value.Value.StringValue : null;
        }

        /// <summary>True if <paramref name="value"/> has a Windows version mode other than <paramref name="except"/>.</summary>
        private static bool HasOtherVersionMode(string value, string except)
        {
            return CompatibilityLayerValue.Parse(value).Entries.Any(entry => CompatibilityLayers.IsWindowsVersionMode(entry) &&
                !string.Equals(entry, except, StringComparison.OrdinalIgnoreCase));
        }
    }
}

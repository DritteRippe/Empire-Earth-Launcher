using System;
using System.Collections.Generic;
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
    /// <summary>
    /// Applies the per-user game defaults of contract 3 for the account that runs the launcher (R1, R4): the class S values
    /// at the start and before a game starts, the first run, the display question, the recommended display settings and
    /// the reset with its <c>.reg</c> backup.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>When</b> (contract 3.6, ADR 0015 with its amendments): at the launcher start only for an installation that is
    /// unambiguous for its game settings key (<see cref="DiscoveryResult.IsUnambiguous"/>) and only if the mutation guard
    /// allows it; class S is then only created when both values are missing, never changed. Before a game starts, class S
    /// is synchronized (<see cref="SynchronizeInstalledFrom"/>, by <see cref="GameStarter"/>). The reset and the display settings run on the
    /// player's request. No defaults and no reset for an installation of a newer contract (contract 5).
    /// </para>
    /// <para>
    /// <b>What</b>: only the values of the contract table (<see cref="GameSettingsTable"/>), the GPU preference of the
    /// programs of the installation (Windows 10 and later, task <c>compatibility_windows</c>, contract 3.4) and the defaults
    /// marker (contract 3.5); every other value is left alone. "Create if missing" tests only whether a value exists;
    /// "overwrite" deletes a value of another type first. Every value written or deleted is logged with old and new value.
    /// </para>
    /// <para>
    /// <b>Backup or nothing</b>: the display settings and the reset first write a <c>.reg</c> file per game with the
    /// game settings key and its subkeys, a delete line for every value they create, and the GPU preference and the marker;
    /// if a backup cannot be written completely, nothing is changed. The marker is written last.
    /// </para>
    /// <para>
    /// Synchronous: the registry work takes milliseconds; the UI runs it through <c>UiOperation</c> on the thread pool.
    /// The writing methods run one at a time (one lock): the defaults at the launcher start and the first Play run on two
    /// threads of the pool and would otherwise both see a missing marker and both write a backup (build/UI review).
    /// </para>
    /// </remarks>
    public sealed class GameDefaultsService : IGameStartPreparation
    {
        private readonly IRegistry registry;
        private readonly IFileSystem fileSystem;
        private readonly ISystemInfo systemInfo;
        private readonly MutationGuard guard;
        private readonly BackupLocations backups;
        private readonly ILogger logger;

        /// <summary>Serializes the writing methods (the launcher start, Play, the page and the reset run on the thread pool).</summary>
        private readonly object writes = new object();

        /// <param name="registry">The registry, wrapped in the launcher's write policy (<see cref="LauncherWritePolicy"/>).</param>
        /// <param name="fileSystem">For the wrapper files of the rasterizer rule.</param>
        /// <param name="systemInfo">Windows version, Wine and screen.</param>
        /// <param name="guard">Blocks every change while a setup or a game runs (ADR 0016).</param>
        /// <param name="backups">Where the <c>.reg</c> backups go.</param>
        /// <param name="logger">Log of the launcher.</param>
        public GameDefaultsService(IRegistry registry, IFileSystem fileSystem, ISystemInfo systemInfo, MutationGuard guard,
            BackupLocations backups, ILogger logger)
        {
            this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
            this.fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            this.systemInfo = systemInfo ?? throw new ArgumentNullException(nameof(systemInfo));
            this.guard = guard ?? throw new ArgumentNullException(nameof(guard));
            this.backups = backups ?? throw new ArgumentNullException(nameof(backups));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>The games of an installation: Empire Earth, and The Art of Conquest if it is installed (contract 3.1).</summary>
        public static IReadOnlyList<Game> GamesOf(Installation installation)
        {
            if (installation == null)
                throw new ArgumentNullException(nameof(installation));
            return installation.HasArtOfConquest ? Game.All : new[] { Game.EmpireEarth };
        }

        /// <summary>The HKCU game settings key of a game of an installation (contract 3.1).</summary>
        public static RegistryLocation SettingsKey(Installation installation, Game game)
        {
            return RegistryLocation.CurrentUser(installation.GetGameSettingsKey(game));
        }

        /// <summary>The value of the defaults marker of a game (contract 3.5).</summary>
        public static RegistryLocation MarkerKey(Product product)
        {
            if (product == null)
                throw new ArgumentNullException(nameof(product));
            return RegistryLocation.CurrentUser(product.DefaultsMarkerKey);
        }

        /// <summary>
        /// True if the GPU preference applies to the installation (contract 3.4): Windows 10 or later and the task
        /// <c>compatibility_windows</c> in the tasks of <c>install.ini</c> or of the uninstall key.
        /// </summary>
        public bool AppliesGpuPreference(Installation installation)
        {
            if (installation == null)
                throw new ArgumentNullException(nameof(installation));
            return systemInfo.IsWindows10OrLater() && installation.Tasks != null &&
                   installation.Tasks.Contains(ContractNames.CompatibilityWindowsTask);
        }

        /// <summary>The full path of the program of a game of an installation: the value name of 3.4 and 3.7.</summary>
        public static string ProgramPath(Installation installation, Game game)
        {
            string folder = installation.GetGameFolder(game);
            return folder == null ? null : WinPath.Combine(folder, game.ProgramName);
        }

        /// <summary>The state of the defaults of a game for the Game settings page.</summary>
        /// <param name="installation">The installation.</param>
        /// <param name="game">A game of the installation.</param>
        /// <param name="unambiguous"><see cref="DiscoveryResult.IsUnambiguous"/> of the installation.</param>
        public DefaultsStatus GetStatus(Installation installation, Game game, bool unambiguous)
        {
            if (installation == null)
                throw new ArgumentNullException(nameof(installation));
            if (game == null)
                throw new ArgumentNullException(nameof(game));
            if (installation.HasNewerContract)
                return DefaultsStatus.NewerContract;
            DefaultsMarker marker = ReadMarker(installation.Product, game, out _);
            switch (marker.State)
            {
                case MarkerState.Current:
                    return DefaultsStatus.Applied;
                case MarkerState.Higher:
                    return DefaultsStatus.AppliedByNewerVersion;
                default:
                    return unambiguous ? DefaultsStatus.Pending : DefaultsStatus.WaitingForPlay;
            }
        }

        /// <summary>The defaults marker of a game; a value that cannot be read counts as missing (logged).</summary>
        public DefaultsMarker ReadMarker(Product product, Game game, out bool readable)
        {
            RegistryResult<RegistryValue> value = registry.GetValue(MarkerKey(product), game.Id);
            readable = value.IsOk || value.Status == RegistryStatus.Missing;
            if (!readable)
                logger.Warning("Game defaults: the marker " + MarkerKey(product) + " @\"" + game.Id + "\" cannot be read: " + value + ".");
            else if (value.IsOk && value.Value.Type != RegistryValueType.DWord)
                logger.Warning("Game defaults: the marker " + MarkerKey(product) + " @\"" + game.Id + "\" is " + value.Value +
                               ", not a REG_DWORD; it counts as missing.");
            return DefaultsMarker.From(value.IsOk ? value.Value : null);
        }

        // --- Launcher start ----------------------------------------------------------------------------------------

        /// <summary>
        /// The defaults at the launcher start (contract 3.6, ADR 0015): for every installation that is unambiguous for its
        /// game settings key and every game of it, class S created if both values are missing, then the first run if the
        /// marker is missing (P and GPU preference created, D created, differing D values collected for one question, the
        /// marker written when nothing differs) or the values added since a lower marker. Nothing at all while the mutation
        /// guard blocks.
        /// </summary>
        public DefaultsStartup ApplyAtLauncherStart(DiscoveryResult discovery)
        {
            lock (writes)
                return ApplyAtLauncherStartNow(discovery);
        }

        private DefaultsStartup ApplyAtLauncherStartNow(DiscoveryResult discovery)
        {
            if (discovery == null)
                throw new ArgumentNullException(nameof(discovery));
            var games = new List<GameDefaultsAtStart>();
            if (discovery.Installations.Count == 0)
                return new DefaultsStartup(null, games, null);

            MutationCheck check = guard.Check("apply the game defaults at the launcher start");
            if (!check.IsAllowed)
                return new DefaultsStartup(check, games, null);

            var questions = new List<DisplayQuestionItem>();
            foreach (Installation installation in discovery.Installations)
            {
                bool unambiguous = discovery.IsUnambiguous(installation);
                if (!unambiguous)
                    logger.Info("Game defaults: nothing written at the start for " + installation.Root + ": " +
                                discovery.SharingSettingsWith(installation).Count.ToString(CultureInfo.InvariantCulture) +
                                " other installation(s) share its game settings; the first Play from the launcher applies them.");
                else if (installation.State == InstallationState.FolderMissing)
                    logger.Info("Game defaults: nothing written for " + installation.Root + ": the chosen folder does not exist.");

                foreach (Game game in GamesOf(installation))
                {
                    if (!unambiguous)
                    {
                        games.Add(new GameDefaultsAtStart(installation, game, InstalledFromAtStart.NotChecked, DefaultsAtStart.Ambiguous));
                        continue;
                    }
                    if (installation.State == InstallationState.FolderMissing)
                    {
                        games.Add(new GameDefaultsAtStart(installation, game, InstalledFromAtStart.NotChecked, DefaultsAtStart.FolderMissing));
                        continue;
                    }

                    InstalledFromAtStart installedFrom = CreateInstalledFromIfMissing(installation, game);
                    DefaultsAtStart defaults;
                    if (installation.HasNewerContract)
                    {
                        logger.Info("Game defaults: " + installation.Root + " was installed by a setup of contract " +
                                    installation.ContractVersion.ToString(CultureInfo.InvariantCulture) +
                                    "; this launcher applies no defaults to it (contract 5).");
                        defaults = DefaultsAtStart.NewerContract;
                    }
                    else
                    {
                        defaults = ApplyDefaults(installation, game, questions);
                    }
                    games.Add(new GameDefaultsAtStart(installation, game, installedFrom, defaults));
                }
            }
            return new DefaultsStartup(null, games, questions.Count == 0 ? null : new DisplayQuestion(questions));
        }

        /// <summary>
        /// The first run of one game if its marker is missing, or the values added since a lower marker (contract 3.5,
        /// 3.6), after the mutation guard: for the first Play of a game (<see cref="GameStarter"/>). Returns the display question in
        /// <paramref name="question"/> if existing display values differ.
        /// </summary>
        public DefaultsAtStart ApplyDefaultsIfNeeded(Installation installation, Game game, out DisplayQuestion question)
        {
            lock (writes)
                return ApplyDefaultsIfNeededNow(installation, game, out question);
        }

        private DefaultsAtStart ApplyDefaultsIfNeededNow(Installation installation, Game game, out DisplayQuestion question)
        {
            if (installation == null)
                throw new ArgumentNullException(nameof(installation));
            if (game == null)
                throw new ArgumentNullException(nameof(game));
            question = null;
            if (installation.HasNewerContract)
                return DefaultsAtStart.NewerContract;
            if (!guard.Check("apply the game defaults of " + game.Id).IsAllowed)
                return DefaultsAtStart.Blocked;
            var questions = new List<DisplayQuestionItem>();
            DefaultsAtStart result = ApplyDefaults(installation, game, questions);
            if (questions.Count > 0)
                question = new DisplayQuestion(questions);
            return result;
        }

        /// <summary>
        /// Class S at the launcher start (ADR 0015 plan review): both values missing -> created from the real game folder;
        /// one missing -> nothing (logged, the sync before Play writes it); both present -> untouched.
        /// </summary>
        private InstalledFromAtStart CreateInstalledFromIfMissing(Installation installation, Game game)
        {
            RegistryLocation key = SettingsKey(installation, game);
            if (!InstalledFromValues.TryCompute(installation.GetGameFolder(game), out InstalledFromValues values))
            {
                WarnNotOnADrive(installation, game);
                return InstalledFromAtStart.NotOnADrive;
            }

            RegistryResult<RegistryValue> volume = registry.GetValue(key, ContractNames.InstalledFromVolumeName);
            RegistryResult<RegistryValue> directory = registry.GetValue(key, ContractNames.InstalledFromDirectoryName);
            if ((!volume.IsOk && volume.Status != RegistryStatus.Missing) || (!directory.IsOk && directory.Status != RegistryStatus.Missing))
            {
                logger.Error("Game defaults: the \"Installed From\" values of " + key + " cannot be read: " + volume + ", " + directory + ".");
                return InstalledFromAtStart.Failed;
            }
            if (volume.IsOk && directory.IsOk)
                return InstalledFromAtStart.Present;
            if (volume.IsOk || directory.IsOk)
            {
                logger.Info("Game defaults: only one of the \"Installed From\" values of " + key +
                            " exists; nothing is written at the start, the start of " + game.Id + " from the launcher completes them.");
                return InstalledFromAtStart.Incomplete;
            }

            var changes = new List<ValueChange>();
            bool ok = Overwrite(key, ContractNames.InstalledFromVolumeName, RegistryValue.FromString(values.Volume), changes) &&
                      Overwrite(key, ContractNames.InstalledFromDirectoryName, RegistryValue.FromString(values.Directory), changes);
            return ok ? InstalledFromAtStart.Created : InstalledFromAtStart.Failed;
        }

        /// <summary>The marker logic of contract 3.5 and the first run of 3.6 (steps 2 to 4) for one game.</summary>
        private DefaultsAtStart ApplyDefaults(Installation installation, Game game, List<DisplayQuestionItem> questions)
        {
            DefaultsMarker marker = ReadMarker(installation.Product, game, out bool readable);
            if (!readable)
                return DefaultsAtStart.Failed;
            if (marker.State == MarkerState.Current || marker.State == MarkerState.Higher)
                return DefaultsAtStart.None;

            RecommendedValues values = RecommendedValues.For(installation, game, systemInfo, fileSystem);
            RegistryLocation key = SettingsKey(installation, game);
            var changes = new List<ValueChange>();
            int since = marker.State == MarkerState.Lower ? marker.Version : 0;
            logger.Info("Game defaults: " + (marker.State == MarkerState.Missing ? "first run" : "marker " + marker) + " of " +
                        installation.Product.Id + " " + game.Id + " for " + installation.Root + "; rasterizer " + values.Rasterizer +
                        ", window " + values.GameWindow + ".");

            // Contract 3.6 step 2: P and the GPU preference, created if missing.
            foreach (GameSetting setting in GameSettingsTable.OfClass(SettingClass.P).Where(s => s.AddedInContractVersion > since))
            {
                if (!CreateIfMissing(setting.KeyIn(key), setting.ValueName, values.ValueOf(setting), changes, out _))
                    return DefaultsAtStart.Failed;
            }
            if (AppliesGpuPreference(installation) &&
                !CreateIfMissing(RegistryLocation.CurrentUser(ContractNames.GpuPreferencesKey), ProgramPath(installation, game),
                    RegistryValue.FromString(ContractNames.GpuPreferenceData), changes, out _))
                return DefaultsAtStart.Failed;

            // Step 3: D created if missing; existing values that differ are only collected for the question.
            var differences = new List<ValueDifference>();
            foreach (GameSetting setting in GameSettingsTable.OfClass(SettingClass.D).Where(s => s.AddedInContractVersion > since))
            {
                RegistryValue recommended = values.ValueOf(setting);
                if (!CreateIfMissing(setting.KeyIn(key), setting.ValueName, recommended, changes, out RegistryValue existing))
                    return DefaultsAtStart.Failed;
                if (existing != null && marker.State == MarkerState.Missing && Differs(existing, recommended))
                    differences.Add(new ValueDifference(setting, existing, recommended));
            }

            if (differences.Count > 0)
            {
                logger.Info("Game defaults: " + differences.Count.ToString(CultureInfo.InvariantCulture) + " display value(s) of " + key +
                            " differ from the recommended ones (" + string.Join(", ", differences.Select(d => d.Setting.Name)) +
                            "); the player is asked once, the marker follows the answer.");
                questions.Add(new DisplayQuestionItem(installation, game, differences));
                return DefaultsAtStart.FirstRunWithQuestion;
            }

            // Step 4: the marker.
            if (!WriteMarker(installation.Product, game, changes))
                return DefaultsAtStart.Failed;
            return marker.State == MarkerState.Missing ? DefaultsAtStart.FirstRun : DefaultsAtStart.Updated;
        }

        // --- Before a game starts (IGameStartPreparation, used by GameStarter) ----------------------------------------

        /// <summary>
        /// Class S before a game starts (contract 3.6): the "Installed From" values of <paramref name="game"/> from its real
        /// folder, written only if they differ after normalization (a foreign installation whose values already name its own
        /// folder stays untouched); old and new values are logged. Asks the mutation guard first. Also for installations of a
        /// newer contract (contract 5 keeps class S).
        /// </summary>
        public GameSettingsResult SynchronizeInstalledFrom(Installation installation, Game game)
        {
            lock (writes)
                return SynchronizeInstalledFromNow(installation, game);
        }

        private GameSettingsResult SynchronizeInstalledFromNow(Installation installation, Game game)
        {
            if (installation == null)
                throw new ArgumentNullException(nameof(installation));
            if (game == null)
                throw new ArgumentNullException(nameof(game));
            string folder = installation.GetGameFolder(game);
            if (folder == null)
                throw new ArgumentException(installation.Root + " has no folder of " + game.Id + ".", nameof(game));

            MutationCheck check = guard.Check("synchronize the \"Installed From\" values of " + game.Id);
            if (!check.IsAllowed)
                return GameSettingsResult.Blocked(check);
            if (!InstalledFromValues.TryCompute(folder, out InstalledFromValues values))
            {
                WarnNotOnADrive(installation, game);
                return new GameSettingsResult(GameSettingsOutcome.NotOnADrive, null, null, null, folder + " is not on a drive letter");
            }

            RegistryLocation key = SettingsKey(installation, game);
            var changes = new List<ValueChange>();
            RegistryResult<RegistryValue> volume = registry.GetValue(key, ContractNames.InstalledFromVolumeName);
            RegistryResult<RegistryValue> directory = registry.GetValue(key, ContractNames.InstalledFromDirectoryName);
            bool volumeSame = volume.IsOk && volume.Value.IsString && values.IsSameVolume(volume.Value.StringValue);
            bool directorySame = directory.IsOk && directory.Value.IsString && values.IsSameDirectory(directory.Value.StringValue);
            if (volumeSame && directorySame)
            {
                logger.Info("Game defaults: the \"Installed From\" values of " + key + " already name " + folder + "; unchanged.");
                return new GameSettingsResult(GameSettingsOutcome.Done, null, null, changes, null);
            }

            bool ok = (volumeSame || Overwrite(key, ContractNames.InstalledFromVolumeName, RegistryValue.FromString(values.Volume), changes)) &&
                      (directorySame || Overwrite(key, ContractNames.InstalledFromDirectoryName, RegistryValue.FromString(values.Directory), changes));
            return new GameSettingsResult(ok ? GameSettingsOutcome.Done : GameSettingsOutcome.Failed, null, null, changes,
                ok ? null : "the \"Installed From\" values of " + key + " could not be written (see the log)");
        }

        // --- Display question, display settings, reset ----------------------------------------------------------------

        /// <summary>
        /// The answer to the display question of the first run (contract 3.6 step 3): with <paramref name="apply"/> a
        /// <c>.reg</c> backup, then the display values overwritten; with either answer the markers of the games written.
        /// If the backup or a write fails, the markers are not written, so the question comes again.
        /// </summary>
        public GameSettingsResult AnswerDisplayQuestion(DisplayQuestion question, bool apply)
        {
            lock (writes)
                return AnswerDisplayQuestionNow(question, apply);
        }

        private GameSettingsResult AnswerDisplayQuestionNow(DisplayQuestion question, bool apply)
        {
            if (question == null)
                throw new ArgumentNullException(nameof(question));
            MutationCheck check = guard.Check("answer the question about the recommended display settings");
            if (!check.IsAllowed)
                return GameSettingsResult.Blocked(check);
            logger.Info("Game defaults: the player answered the question about the recommended display settings with " +
                        (apply ? "yes" : "no") + ".");

            var games = question.Items.Select(item => Tuple.Create(item.Installation, item.Game)).ToList();
            GameSettingsResult display = apply ? ApplyDisplay(games) : null;
            if (display != null && !display.IsDone)
                return display;

            var changes = display?.Changes.ToList() ?? new List<ValueChange>();
            foreach (DisplayQuestionItem item in question.Items)
            {
                if (!WriteMarker(item.Installation.Product, item.Game, changes))
                    return new GameSettingsResult(GameSettingsOutcome.Failed, null, display?.BackupFiles, changes,
                        "the defaults marker could not be written (see the log)");
            }
            return new GameSettingsResult(GameSettingsOutcome.Done, null, display?.BackupFiles, changes, null);
        }

        /// <summary>
        /// "Apply the recommended display settings" (contract 3.6): a <c>.reg</c> backup of the game settings of every game
        /// of the installation, then the display values (class D) overwritten. Nothing else changes.
        /// </summary>
        public GameSettingsResult ApplyRecommendedDisplay(Installation installation)
        {
            lock (writes)
                return ApplyRecommendedDisplayNow(installation);
        }

        private GameSettingsResult ApplyRecommendedDisplayNow(Installation installation)
        {
            if (installation == null)
                throw new ArgumentNullException(nameof(installation));
            if (installation.HasNewerContract)
                return NewerContract(installation);
            MutationCheck check = guard.Check("apply the recommended display settings");
            if (!check.IsAllowed)
                return GameSettingsResult.Blocked(check);
            return ApplyDisplay(GamesOf(installation).Select(game => Tuple.Create(installation, game)).ToList());
        }

        private GameSettingsResult ApplyDisplay(IReadOnlyList<Tuple<Installation, Game>> games)
        {
            var targets = new List<Tuple<Installation, Game, List<Target>>>();
            foreach (var game in games)
            {
                RecommendedValues values = RecommendedValues.For(game.Item1, game.Item2, systemInfo, fileSystem);
                RegistryLocation key = SettingsKey(game.Item1, game.Item2);
                targets.Add(Tuple.Create(game.Item1, game.Item2, GameSettingsTable.OfClass(SettingClass.D)
                    .Select(setting => new Target(setting.KeyIn(key), setting.ValueName, values.ValueOf(setting)))
                    .ToList()));
            }
            return BackupThenOverwrite("display-settings", targets, new List<Target>(), null);
        }

        /// <summary>
        /// The reset of the game settings of every game of the installation (contract 3.6, R4), after the player confirmed
        /// it: a <c>.reg</c> backup per game (game settings key with subkeys, a delete line for every value the reset creates,
        /// the GPU preference and the marker) -> if a backup fails, nothing is changed -> S, D, P and the GPU preference
        /// overwritten (a value of another type deleted first) -> the markers last. Values outside the table stay as they are.
        /// Refused for an installation of a newer contract (contract 5).
        /// </summary>
        public GameSettingsResult Reset(Installation installation)
        {
            lock (writes)
                return ResetNow(installation);
        }

        private GameSettingsResult ResetNow(Installation installation)
        {
            if (installation == null)
                throw new ArgumentNullException(nameof(installation));
            if (installation.HasNewerContract)
                return NewerContract(installation);
            MutationCheck check = guard.Check("reset the game settings");
            if (!check.IsAllowed)
                return GameSettingsResult.Blocked(check);

            var targets = new List<Tuple<Installation, Game, List<Target>>>();
            var markers = new List<Target>();
            string note = null;
            foreach (Game game in GamesOf(installation))
            {
                RecommendedValues values = RecommendedValues.For(installation, game, systemInfo, fileSystem);
                RegistryLocation key = SettingsKey(installation, game);
                var gameTargets = new List<Target>();
                foreach (GameSetting setting in GameSettingsTable.All)
                {
                    RegistryValue value = values.ValueOf(setting);
                    if (value != null)
                        gameTargets.Add(new Target(setting.KeyIn(key), setting.ValueName, value));
                }
                if (values.InstalledFrom == null)
                {
                    WarnNotOnADrive(installation, game);
                    note = installation.GetGameFolder(game) + " is not on a drive letter: the \"Installed From\" values were not written";
                }
                if (AppliesGpuPreference(installation))
                    gameTargets.Add(new Target(RegistryLocation.CurrentUser(ContractNames.GpuPreferencesKey),
                        ProgramPath(installation, game), RegistryValue.FromString(ContractNames.GpuPreferenceData)));
                var marker = new Target(MarkerKey(installation.Product), game.Id, RegistryValue.FromDWord(ContractNames.ContractVersion));
                gameTargets.Add(marker); // backed up with the game; written last
                markers.Add(marker);
                targets.Add(Tuple.Create(installation, game, gameTargets));
            }
            return BackupThenOverwrite("reset-game-settings", targets, markers, note);
        }

        /// <summary>
        /// Writes one <c>.reg</c> backup per game, and only if all of them are on the disk overwrites the targets (the
        /// <paramref name="last"/> ones, the markers, after all others).
        /// </summary>
        private GameSettingsResult BackupThenOverwrite(string what, List<Tuple<Installation, Game, List<Target>>> targets,
            List<Target> last, string note)
        {
            FileSystemResult<BackupFolder> folder = backups.CreateSubfolder(what);
            if (!folder.IsOk)
                return BackupFailed("the backup folder could not be created: " + folder);

            var files = new List<string>();
            foreach (var game in targets)
            {
                if (!TryBuildBackup(game.Item1, game.Item2, game.Item3, out List<RegFileKey> keys, out string problem))
                    return BackupFailed(problem, files);
                string file = WinPath.Combine(folder.Value.Path,
                    BackupLocations.GameSettingsFileName(folder.Value.Time, game.Item1.Product, game.Item2));
                FileSystemResult written = backups.WriteRegFile(file, keys);
                if (!written.IsOk)
                    return BackupFailed("the backup " + file + " could not be written: " + written, files);
                files.Add(file);
            }

            var changes = new List<ValueChange>();
            foreach (Target target in targets.SelectMany(game => game.Item3).Where(target => !last.Contains(target)).Concat(last))
            {
                if (!Overwrite(target.Key, target.ValueName, target.Value, changes))
                    return new GameSettingsResult(GameSettingsOutcome.Failed, null, files, changes,
                        target.Key + " @\"" + target.ValueName + "\" could not be written (see the log); the backup restores the previous values");
            }
            logger.Info("Game defaults: " + what + " done, " + changes.Count.ToString(CultureInfo.InvariantCulture) +
                        " value(s) changed, backup in " + folder.Value.Path + ".");
            return new GameSettingsResult(GameSettingsOutcome.Done, null, files, changes, note);
        }

        private GameSettingsResult BackupFailed(string problem, IEnumerable<string> writtenFiles = null)
        {
            logger.Error("Game defaults: nothing was changed because the backup failed: " + problem + ".");
            return new GameSettingsResult(GameSettingsOutcome.BackupFailed, null, writtenFiles, null, problem);
        }

        private GameSettingsResult NewerContract(Installation installation)
        {
            logger.Warning("Game defaults: " + installation.Root + " was installed by a setup of contract " +
                           installation.ContractVersion.ToString(CultureInfo.InvariantCulture) +
                           ", which this launcher does not know; no reset and no defaults (contract 5). Update the launcher.");
            return new GameSettingsResult(GameSettingsOutcome.NewerContract, null, null, null, "newer contract");
        }

        /// <summary>
        /// The backup of one game: the game settings key with its subkeys, a delete line for every target value that does not
        /// exist now (so that importing the file removes what the change creates), and the target values outside that key
        /// (GPU preference, marker) as they are now or as delete lines (ADR 0007 amendment).
        /// </summary>
        private bool TryBuildBackup(Installation installation, Game game, IEnumerable<Target> targets, out List<RegFileKey> keys,
            out string problem)
        {
            keys = null;
            problem = null;
            RegistryLocation settingsKey = SettingsKey(installation, game);
            RegistryResult<IReadOnlyList<RegFileKey>> tree = RegistryExport.ReadTree(registry, settingsKey);
            if (!tree.IsOk)
            {
                problem = "the game settings could not be read: " + tree;
                return false;
            }
            keys = tree.Value.ToList();
            foreach (Target target in targets)
            {
                RegFileKey key = keys.FirstOrDefault(candidate => candidate.Key.Equals(target.Key));
                RegistryResult<RegistryValue> current = registry.GetValue(target.Key, target.ValueName);
                if (!current.IsOk && current.Status != RegistryStatus.Missing)
                {
                    problem = target.Key + " @\"" + target.ValueName + "\" could not be read: " + current;
                    return false;
                }
                if (key == null)
                {
                    key = new RegFileKey(target.Key);
                    keys.Add(key);
                }
                if (key.Contains(target.ValueName))
                    continue;
                if (current.IsOk)
                    key.Add(target.ValueName, current.Value);
                else
                    key.Delete(target.ValueName);
            }
            return true;
        }

        // --- Registry helpers ------------------------------------------------------------------------------------------

        private bool WriteMarker(Product product, Game game, List<ValueChange> changes)
        {
            return Overwrite(MarkerKey(product), game.Id, RegistryValue.FromDWord(ContractNames.ContractVersion), changes);
        }

        /// <summary>
        /// "Create if missing" (contract 3.2): writes the value only if it does not exist (of any type).
        /// <paramref name="existing"/> is the value found, or null.
        /// </summary>
        private bool CreateIfMissing(RegistryLocation key, string valueName, RegistryValue value, List<ValueChange> changes,
            out RegistryValue existing)
        {
            existing = null;
            RegistryResult<RegistryValue> current = registry.GetValue(key, valueName);
            if (current.IsOk)
            {
                existing = current.Value;
                return true;
            }
            if (current.Status != RegistryStatus.Missing)
            {
                logger.Error("Game defaults: " + key + " @\"" + valueName + "\" cannot be read: " + current + ".");
                return false;
            }
            return Set(key, valueName, value, null, changes);
        }

        /// <summary>
        /// "Overwrite" (contract 3.2): writes the value unless it is already exactly this value; a value of another type is
        /// deleted first, like <c>deletevalue</c> of the setup.
        /// </summary>
        private bool Overwrite(RegistryLocation key, string valueName, RegistryValue value, List<ValueChange> changes)
        {
            RegistryResult<RegistryValue> current = registry.GetValue(key, valueName);
            if (current.IsOk && current.Value.Equals(value))
                return true;
            if (!current.IsOk && current.Status != RegistryStatus.Missing)
            {
                logger.Error("Game defaults: " + key + " @\"" + valueName + "\" cannot be read: " + current + ".");
                return false;
            }
            RegistryValue old = current.IsOk ? current.Value : null;
            if (old != null && old.Type != value.Type)
            {
                RegistryResult deleted = registry.DeleteValue(key, valueName);
                if (!deleted.IsOk)
                {
                    logger.Error("Game defaults: unable to delete " + key + " @\"" + valueName + "\" (" + old + "): " + deleted + ".");
                    return false;
                }
                logger.Info("Game defaults: deleted " + key + " @\"" + valueName + "\" (" + old + ") to write it as another type.");
            }
            return Set(key, valueName, value, old, changes);
        }

        private bool Set(RegistryLocation key, string valueName, RegistryValue value, RegistryValue old, List<ValueChange> changes)
        {
            if (registry.ProbeKey(key).Status == RegistryStatus.Missing)
            {
                RegistryResult created = registry.CreateSubKey(key);
                if (!created.IsOk)
                {
                    logger.Error("Game defaults: unable to create " + key + ": " + created + ".");
                    return false;
                }
            }
            RegistryResult result = registry.SetValue(key, valueName, value);
            if (!result.IsOk)
            {
                logger.Error("Game defaults: unable to write " + key + " @\"" + valueName + "\" = " + value + ": " + result + ".");
                return false;
            }
            var change = new ValueChange(key, valueName, old, value);
            changes.Add(change);
            logger.Info("Game defaults: " + (old == null ? "created " : "changed ") + change + ".");
            return true;
        }

        /// <summary>Whether an existing display value differs from the recommended one (strings ignoring case).</summary>
        private static bool Differs(RegistryValue existing, RegistryValue recommended)
        {
            if (existing.Type != recommended.Type)
                return true;
            return existing.IsString
                ? !string.Equals(existing.StringValue, recommended.StringValue, StringComparison.OrdinalIgnoreCase)
                : !existing.Equals(recommended);
        }

        private void WarnNotOnADrive(Installation installation, Game game)
        {
            logger.Warning("Game defaults: the folder " + installation.GetGameFolder(game) + " of " + game.Id +
                           " is not on a drive letter; the \"Installed From\" values cannot name it and are not written (contract 3.3).");
        }

        /// <summary>A value the launcher writes: key, name and data.</summary>
        private sealed class Target
        {
            public Target(RegistryLocation key, string valueName, RegistryValue value)
            {
                Key = key;
                ValueName = valueName;
                Value = value;
            }

            public RegistryLocation Key { get; }

            public string ValueName { get; }

            public RegistryValue Value { get; }
        }
    }
}

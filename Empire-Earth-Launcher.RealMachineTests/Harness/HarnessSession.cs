using System;
using System.Collections.Generic;
using System.Linq;
using Empire_Earth_Launcher.Core.Backup;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.GameSettings;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Integrity;
using Empire_Earth_Launcher.Core.Logging;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Core.Play;
using Empire_Earth_Launcher.RealMachineTests.Checks;
using Empire_Earth_Launcher.RealMachineTests.Expectations;

namespace Empire_Earth_Launcher.RealMachineTests.Harness
{
    /// <summary>
    /// The launcher core composed as the launcher's <c>Program</c> composes it, over the adapters it is given (the real ones in
    /// <c>RealMachine/</c>, in-memory fakes in the self-tests), with every adapter wrapped so that the harness can only change
    /// what a step allows, and the checks of one step of a CI scenario (<see cref="Expectation"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Reading code (the discovery, the integrity check, the consistency check, the status of the defaults, the snapshots) gets
    /// <see cref="ReadOnlyRegistry"/> and <see cref="ReadOnlyFileSystem"/>. The defaults of the launcher start and the reset get
    /// the launcher's write policy (<c>PolicyCheckedRegistry</c> with <c>LauncherWritePolicy.For</c>) over
    /// <see cref="RecordingRegistry"/>, and their backups go below the work folder only (<see cref="WorkFolderFileSystem"/>).
    /// </para>
    /// <para>
    /// Every method returns problem lines (empty when everything matches) or a result; the NUnit fixtures assert, so the same
    /// code runs on the runner and in the self-tests. No line holds a hash (<see cref="SafeText"/>).
    /// </para>
    /// </remarks>
    internal sealed class HarnessSession
    {
        private readonly ISystemInfo systemInfo;
        private readonly IMutexProbe mutexProbe;
        private readonly ILogger logger;

        public HarnessSession(Expectation expectation, string workFolder, IRegistry registry, IFileSystem fileSystem,
            ISystemInfo systemInfo, IMutexProbe mutexProbe, IClock clock, ILogger logger)
        {
            Expectation = expectation ?? throw new ArgumentNullException(nameof(expectation));
            if (registry == null)
                throw new ArgumentNullException(nameof(registry));
            if (fileSystem == null)
                throw new ArgumentNullException(nameof(fileSystem));
            this.systemInfo = systemInfo ?? throw new ArgumentNullException(nameof(systemInfo));
            this.mutexProbe = mutexProbe ?? throw new ArgumentNullException(nameof(mutexProbe));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            if (clock == null)
                throw new ArgumentNullException(nameof(clock));

            Violations = new HarnessViolations();
            ReadOnlyRegistry = new ReadOnlyRegistry(registry, Violations);
            ReadOnlyFileSystem = new ReadOnlyFileSystem(fileSystem, Violations);
            Writes = new RecordingRegistry(registry, Violations);
            WorkFileSystem = new WorkFolderFileSystem(fileSystem, workFolder, Violations);

            var guard = new MutationGuard(mutexProbe, logger);
            var backups = new BackupLocations(WinPath.Combine(WorkFileSystem.WorkFolder, BackupLocations.FolderName), WorkFileSystem, clock,
                logger);
            BackupFolder = backups.Directory;
            Defaults = new GameDefaultsService(new PolicyCheckedRegistry(Writes, LauncherWritePolicy.For(systemInfo)), ReadOnlyFileSystem,
                systemInfo, guard, backups, logger);
            DefaultsReader = new GameDefaultsService(ReadOnlyRegistry, ReadOnlyFileSystem, systemInfo, guard, backups, logger);

            logger.Info("RealMachine: scenario " + (expectation.Scenario ?? "-") + ", step " + (expectation.Step ?? "-") + "; " +
                        systemInfo.Describe() + ".");
            Before = MachineSnapshot.Take(ReadOnlyRegistry, ReadOnlyFileSystem, expectation.Roots);
            logger.Info("RealMachine: snapshot of " + Before.Count + " entries before the checks.");
        }

        public Expectation Expectation { get; }

        public HarnessViolations Violations { get; }

        public ReadOnlyRegistry ReadOnlyRegistry { get; }

        public ReadOnlyFileSystem ReadOnlyFileSystem { get; }

        /// <summary>The record of every change of the defaults and the reset.</summary>
        public RecordingRegistry Writes { get; }

        public WorkFolderFileSystem WorkFileSystem { get; }

        /// <summary>Where the reset writes its <c>.reg</c> backups.</summary>
        public string BackupFolder { get; }

        /// <summary>The defaults service of the launcher, writing through the write policy.</summary>
        public GameDefaultsService Defaults { get; }

        /// <summary>The same service over the read-only registry: for <c>GetStatus</c>, which must not write.</summary>
        public GameDefaultsService DefaultsReader { get; }

        /// <summary>The snapshot taken when the session started, before any check.</summary>
        public MachineSnapshot Before { get; }

        /// <summary>The discovery of the launcher (sources 1 to 4; the harness is not in a game folder), read-only.</summary>
        public DiscoveryResult Discover()
        {
            DiscoveryResult result = new InstallationDiscovery(ReadOnlyRegistry, ReadOnlyFileSystem, logger).Discover(Expectation.UserChoice, null);
            logger.Info("RealMachine: discovery found " + DiscoveryCheck.Describe(result) + ".");
            return result;
        }

        // --- Discovery and integrity (read-only) ------------------------------------------------------------------------

        public IReadOnlyList<string> CheckDiscovery(DiscoveryResult result)
        {
            return SafeText.Redact(DiscoveryCheck.Compare(Expectation, result));
        }

        /// <summary>
        /// The integrity check of every expected installation that has an expectation for <paramref name="kind"/>; the number of
        /// installations checked is returned in <paramref name="checkedInstallations"/>.
        /// </summary>
        public IReadOnlyList<string> CheckIntegrity(DiscoveryResult result, IntegrityCheckKind kind, out int checkedInstallations,
            IList<string> report = null)
        {
            var problems = new List<string>();
            checkedInstallations = 0;
            var checker = new IntegrityChecker(ReadOnlyFileSystem, ReadOnlyRegistry, mutexProbe, logger);
            foreach (InstallationExpectation expected in Expectation.Installations)
            {
                CheckExpectation check = kind == IntegrityCheckKind.Quick ? expected.Quick : expected.Full;
                if (check == null)
                    continue;
                checkedInstallations++;
                Installation installation = DiscoveryCheck.Find(result, expected.Root);
                if (installation == null)
                {
                    problems.Add(expected + ": not found, so not checked");
                    continue;
                }
                IntegrityReport integrity = checker.Check(installation, kind);
                report?.Add(expected + " " + kind.ToString().ToLowerInvariant() + " check: " + IntegrityCheck.Describe(integrity));
                problems.AddRange(IntegrityCheck.Compare(expected + " " + kind.ToString().ToLowerInvariant() + " check", check, integrity));
            }
            return SafeText.Redact(problems);
        }

        // --- Game settings before any change (read-only) ----------------------------------------------------------------

        /// <summary>The status of the defaults and the consistency findings of every expected installation that names them.</summary>
        public IReadOnlyList<string> CheckGameSettingsState(DiscoveryResult result, out int checkedInstallations)
        {
            var problems = new List<string>();
            checkedInstallations = 0;
            var consistency = new ConsistencyChecker(ReadOnlyRegistry, ReadOnlyFileSystem, systemInfo);
            foreach (InstallationExpectation expected in Expectation.Installations.Where(e => e.DefaultsStatus != null || e.Consistency != null))
            {
                checkedInstallations++;
                Installation installation = DiscoveryCheck.Find(result, expected.Root);
                if (installation == null)
                {
                    problems.Add(expected + ": not found, so its game settings were not checked");
                    continue;
                }
                problems.AddRange(GameSettingsCheck.CompareStatus(expected, installation, result, DefaultsReader));
                if (expected.Consistency != null)
                    problems.AddRange(GameSettingsCheck.CompareConsistency(expected, consistency.Check(installation)));
            }
            return SafeText.Redact(problems);
        }

        // --- Defaults of the launcher start and reset (write HKCU) -------------------------------------------------------

        /// <summary>Saves the values the setup wrote (before the start) to <see cref="DefaultsExpectation.RecordSetupValuesTo"/>.</summary>
        public IReadOnlyList<string> RecordSetupValues(DiscoveryResult result)
        {
            string file = Expectation.Defaults?.RecordSetupValuesTo;
            if (file == null)
                return new string[0];
            if (!WorkFileSystem.IsInWorkFolder(file))
                return new[] { "recordSetupValuesTo must be below the work folder " + WorkFileSystem.WorkFolder + ": " + file };
            var values = new List<SettingValue>();
            var problems = new List<string>();
            foreach (InstallationExpectation expected in Expectation.Installations)
            {
                Installation installation = DiscoveryCheck.Find(result, expected.Root);
                if (installation == null)
                    problems.Add(expected + ": not found, so its values were not saved");
                else
                    values.AddRange(SettingValues.Read(ReadOnlyRegistry, installation));
            }
            string folder = WinPath.GetParent(file);
            FileSystemResult written = WorkFileSystem.CreateDirectory(folder);
            if (written.IsOk)
                written = WorkFileSystem.WriteAllBytesAtomically(file, SettingValues.ToJson(values));
            if (!written.IsOk)
                problems.Add("the setup values could not be saved to " + file + ": " + written);
            else
                logger.Info("RealMachine: " + values.Count + " values of the setup saved to " + file + ".");
            return problems;
        }

        /// <summary>
        /// The defaults of the launcher start (<see cref="GameDefaultsService.ApplyAtLauncherStart"/>) and their checks: what they
        /// did per game, which values they wrote, the recommended values, the values saved by an earlier step.
        /// </summary>
        public IReadOnlyList<string> ApplyDefaultsAtStart(DiscoveryResult result, IList<string> report = null)
        {
            DefaultsExpectation expected = Expectation.Defaults ?? throw new InvalidOperationException("No defaults in this step.");
            DefaultsStartup startup = Defaults.ApplyAtLauncherStart(result);
            foreach (GameDefaultsAtStart game in startup.Games)
                report?.Add("defaults at the start: " + game);
            var problems = new List<string>(DefaultsCheck.CompareStart(Expectation, startup));
            IReadOnlyList<RegistryWrite> writes = Writes.Writes;
            report?.Add("defaults at the start: " + writes.Count + " registry change(s)");
            problems.AddRange(DefaultsCheck.UnexpectedWrites(writes, result.Installations));
            if (expected.ExpectNoWrites && writes.Count > 0)
                problems.Add("the start wrote " + writes.Count + " value(s), expected none: " + string.Join("; ", writes.Take(10)));

            List<Installation> installations = Expected(result, problems);
            if (expected.ExpectRecommendedValues)
            {
                foreach (Installation installation in installations)
                    problems.AddRange(DefaultsCheck.CompareWithRecommended(ReadOnlyRegistry, installation, DefaultsReader, systemInfo,
                        ReadOnlyFileSystem));
            }
            if (expected.CompareWithSetupValuesFrom != null)
            {
                FileSystemResult<byte[]> saved = ReadOnlyFileSystem.ReadAllBytes(expected.CompareWithSetupValuesFrom, 4 * 1024 * 1024);
                if (!saved.IsOk)
                    problems.Add("the saved setup values " + expected.CompareWithSetupValuesFrom + " cannot be read: " + saved);
                else
                {
                    try
                    {
                        IReadOnlyList<SettingValue> setup = SettingValues.FromJson(saved.Value);
                        IReadOnlyList<SettingValue> now = installations.SelectMany(i => SettingValues.Read(ReadOnlyRegistry, i)).ToList();
                        problems.AddRange(SettingValues.Compare(setup, now, expected.AllowedDifferences, "the setup wrote"));
                    }
                    catch (FormatException ex)
                    {
                        problems.Add(expected.CompareWithSetupValuesFrom + ": " + ex.Message);
                    }
                }
            }
            return SafeText.Redact(problems);
        }

        /// <summary>A second start: every game unchanged (<c>None</c>, or <c>Ambiguous</c> as before) and no further change.</summary>
        public IReadOnlyList<string> CheckSecondStart(DiscoveryResult result)
        {
            int before = Writes.Writes.Count;
            DefaultsStartup startup = Defaults.ApplyAtLauncherStart(result);
            var problems = new List<string>();
            foreach (GameDefaultsAtStart game in startup.Games.Where(g => g.Defaults != DefaultsAtStart.None && g.Defaults != DefaultsAtStart.Ambiguous))
                problems.Add("second start: " + game);
            IReadOnlyList<RegistryWrite> writes = Writes.Writes;
            if (writes.Count != before)
                problems.Add("second start: " + (writes.Count - before) + " further change(s): " + string.Join("; ", writes.Skip(before).Take(10)));
            return SafeText.Redact(problems);
        }

        /// <summary>
        /// The reset of every expected installation (contract 3.6, R4): done, one <c>.reg</c> backup per game below the work
        /// folder, the recommended values afterwards, and no change the defaults may not make.
        /// </summary>
        public IReadOnlyList<string> CheckReset(DiscoveryResult result)
        {
            var problems = new List<string>();
            foreach (Installation installation in Expected(result, problems))
            {
                GameSettingsResult reset = Defaults.Reset(installation);
                string name = installation.Product.Id + " in " + installation.Root;
                if (!reset.IsDone)
                {
                    problems.Add(name + ": the reset ended with " + reset);
                    continue;
                }
                int games = GameDefaultsService.GamesOf(installation).Count;
                if (reset.BackupFiles.Count != games)
                    problems.Add(name + ": the reset wrote " + reset.BackupFiles.Count + " backup(s), expected one per game (" + games + ")");
                foreach (string file in reset.BackupFiles.Where(file => !WinPath.IsBelow(file, BackupFolder) || !ReadOnlyFileSystem.FileExists(file)))
                    problems.Add(name + ": the backup " + file + " is missing or not below " + BackupFolder);
                problems.AddRange(DefaultsCheck.CompareWithRecommended(ReadOnlyRegistry, installation, DefaultsReader, systemInfo,
                    ReadOnlyFileSystem));
            }
            problems.AddRange(DefaultsCheck.UnexpectedWrites(Writes.Writes, result.Installations));
            return SafeText.Redact(problems);
        }

        // --- At the end -------------------------------------------------------------------------------------------------

        /// <summary>
        /// What must hold after every step: no refused change, no change of the defaults unless the step applies them, and the
        /// snapshot unchanged (CD keys, records, uninstall keys, compatibility layers, HKLM settings, the files of the roots).
        /// </summary>
        public IReadOnlyList<string> CheckMachineState()
        {
            var problems = new List<string>(Violations.Items.Select(item => "refused: " + item));
            if (Expectation.Defaults == null && Writes.Writes.Count > 0)
                problems.Add(Writes.Writes.Count + " registry change(s) in a step without defaults: " + string.Join("; ", Writes.Writes.Take(10)));
            MachineSnapshot after = MachineSnapshot.Take(ReadOnlyRegistry, ReadOnlyFileSystem, Expectation.Roots);
            problems.AddRange(Before.DifferencesTo(after).Select(line => "changed during the checks: " + line));
            foreach (Game game in Game.All.Where(game => mutexProbe.Exists(game.MutexName)))
                problems.Add(game.ProgramName + " runs (mutex " + game.MutexName + "); nothing of the harness starts a game");
            return SafeText.Redact(problems);
        }

        /// <summary>The installations of the expectation as the discovery found them; a missing one is a problem.</summary>
        private List<Installation> Expected(DiscoveryResult result, List<string> problems)
        {
            var installations = new List<Installation>();
            foreach (InstallationExpectation expected in Expectation.Installations)
            {
                Installation installation = DiscoveryCheck.Find(result, expected.Root);
                if (installation == null)
                    problems.Add(expected + ": not found");
                else
                    installations.Add(installation);
            }
            return installations;
        }
    }
}

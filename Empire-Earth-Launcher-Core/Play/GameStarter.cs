using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.GameSettings;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Logging;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Core.Repair;

namespace Empire_Earth_Launcher.Core.Play
{
    /// <summary>How a click on Play ended (ADR 0010, ARCHITECTURE 4.2).</summary>
    public enum StartOutcome
    {
        /// <summary>The game was started; <see cref="StartResult.ProcessId"/> is its process id, or null ("pid unknown").</summary>
        Started,

        /// <summary>Refused: a setup runs (contract 4.2); <see cref="StartResult.RunningSetup"/> names it.</summary>
        SetupRunning,

        /// <summary>
        /// Refused: the same game runs (its mutex exists, forum report section 8 row 14);
        /// <see cref="StartResult.ProcessFound"/> tells whether a process of the program exists (it may hang).
        /// </summary>
        AlreadyRunning,

        /// <summary>
        /// Not started yet: the other game runs (<see cref="StartResult.OtherGame"/>); the player may start anyway, which
        /// starts again with <c>startEvenIfOtherGameRuns</c>.
        /// </summary>
        OtherGameRunning,

        /// <summary>Refused: the folder the player chose does not exist (any more).</summary>
        FolderMissing,

        /// <summary>
        /// Refused: the program is missing, before or at the start, so the installation is damaged (contract 1.4
        /// "Validity"); <see cref="StartResult.RepairAdvice"/> says what to do (contract 4.4).
        /// </summary>
        Damaged,

        /// <summary>Windows refused the program as a threat of an antivirus (errors 225, 226); with the repair advice.</summary>
        BlockedByAntivirus,

        /// <summary>The player cancelled the elevation prompt of a compatibility layer (error 1223).</summary>
        ElevationCancelled,

        /// <summary>Windows denied the start (error 5, or 1260: blocked by a policy).</summary>
        AccessDenied,

        /// <summary>Another error of the start; <see cref="StartResult.ErrorCode"/> and <see cref="StartResult.ErrorMessage"/>.</summary>
        Failed
    }

    /// <summary>The result of <see cref="GameStarter.Start"/>, with the data the UI needs for its message.</summary>
    public sealed class StartResult
    {
        internal StartResult(StartOutcome outcome, Installation installation, Game game, string programPath)
        {
            Outcome = outcome;
            Installation = installation;
            Game = game;
            ProgramPath = programPath;
        }

        public StartOutcome Outcome { get; }

        public Installation Installation { get; }

        public Game Game { get; }

        /// <summary>Full path of the program (<c>Empire Earth.exe</c> or <c>EE-AOC.exe</c> in the real game folder).</summary>
        public string ProgramPath { get; }

        /// <summary>The process id of the started game; null if it is not known or the game was not started.</summary>
        public int? ProcessId { get; internal set; }

        /// <summary>The setup that runs (<see cref="StartOutcome.SetupRunning"/>), else null.</summary>
        public SetupKind RunningSetup { get; internal set; }

        /// <summary>With <see cref="StartOutcome.AlreadyRunning"/>: a process of the program exists.</summary>
        public bool ProcessFound { get; internal set; }

        /// <summary>The other game that runs (<see cref="StartOutcome.OtherGameRunning"/>, or started anyway), else null.</summary>
        public Game OtherGame { get; internal set; }

        /// <summary>What to do (<see cref="StartOutcome.Damaged"/>, <see cref="StartOutcome.BlockedByAntivirus"/>), else null.</summary>
        public RepairAdvice RepairAdvice { get; internal set; }

        /// <summary>The Windows error code of a failed start; 0 otherwise.</summary>
        public int ErrorCode { get; internal set; }

        /// <summary>The message of the exception of a failed start (for the details and the log); null otherwise.</summary>
        public string ErrorMessage { get; internal set; }

        /// <summary>The synchronization of class S before the start; null if the start was refused before it.</summary>
        public GameSettingsResult InstalledFrom { get; internal set; }

        /// <summary>What the first run did before the start; null if the start was refused before it.</summary>
        public DefaultsAtStart? Defaults { get; internal set; }

        /// <summary>The display question of a first run before the start (asked without blocking it), else null.</summary>
        public DisplayQuestion Question { get; internal set; }

        public bool IsStarted
        {
            get { return Outcome == StartOutcome.Started; }
        }

        public override string ToString()
        {
            return Outcome + " " + Game?.Id + " " + ProgramPath +
                   (ProcessId.HasValue ? ", pid " + ProcessId.Value.ToString(CultureInfo.InvariantCulture) : string.Empty);
        }
    }

    /// <summary>
    /// Starts Empire Earth or The Art of Conquest of an installation (R3, ADR 0010, ARCHITECTURE 4.2) in a fixed order:
    /// setup mutex -> game mutex (the same game refused, the other one a warning) -> folder and program -> class S
    /// synchronized (contract 3.6) -> first run if the marker is missing -> start through the shell in the real game
    /// folder (contract 3.7) -> log with installation, game, program and process id.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every environment problem is a <see cref="StartResult"/>, never an exception (ADR 0013): a running setup or game,
    /// a missing program (with the <see cref="Repair.RepairAdvice"/>), a cancelled elevation prompt, a denied start. Only
    /// programming errors throw (no installation, a game the installation does not have).
    /// </para>
    /// <para>
    /// The game settings work never stops a start: if the mutation guard blocks it (the other game runs and the player
    /// starts anyway) or a value cannot be written, it is logged and the game starts; the findings of the integrity check
    /// never block either (contract 2.5). The launcher never ends a process.
    /// </para>
    /// </remarks>
    public sealed class GameStarter
    {
        private const int ErrorFileNotFound = 2;
        private const int ErrorPathNotFound = 3;
        private const int ErrorAccessDenied = 5;
        private const int ErrorVirusInfected = 225;
        private const int ErrorVirusDeleted = 226;
        private const int ErrorCancelled = 1223;
        private const int ErrorAccessDisabledByPolicy = 1260;

        private readonly RunningGameDetector detector;
        private readonly IFileSystem fileSystem;
        private readonly IGameStartPreparation preparation;
        private readonly IProcessStarter starter;
        private readonly ILogger logger;

        /// <param name="detector">The running setups, games and processes.</param>
        /// <param name="fileSystem">To check that the program exists.</param>
        /// <param name="preparation">Class S and the first run (<see cref="GameDefaultsService"/>).</param>
        /// <param name="starter">Starts the program through the shell.</param>
        /// <param name="logger">Log of the launcher.</param>
        public GameStarter(RunningGameDetector detector, IFileSystem fileSystem, IGameStartPreparation preparation,
            IProcessStarter starter, ILogger logger)
        {
            this.detector = detector ?? throw new ArgumentNullException(nameof(detector));
            this.fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            this.preparation = preparation ?? throw new ArgumentNullException(nameof(preparation));
            this.starter = starter ?? throw new ArgumentNullException(nameof(starter));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// <see cref="Start"/> on the thread pool: the registry work and the shell (which waits while Windows shows the
        /// elevation prompt of a <c>RUNASADMIN</c> layer) never block the window (ADR 0004).
        /// </summary>
        public Task<StartResult> StartAsync(Installation installation, Game game, bool startEvenIfOtherGameRuns,
            CancellationToken cancellationToken = default)
        {
            CheckArguments(installation, game);
            return Task.Run(() => Start(installation, game, startEvenIfOtherGameRuns), cancellationToken);
        }

        /// <summary>Starts <paramref name="game"/> of <paramref name="installation"/> in the order of the class description.</summary>
        /// <param name="installation">The selected installation.</param>
        /// <param name="game">A game of the installation (The Art of Conquest only if it has an AoC folder).</param>
        /// <param name="startEvenIfOtherGameRuns">The player confirmed the warning that the other game runs.</param>
        public StartResult Start(Installation installation, Game game, bool startEvenIfOtherGameRuns)
        {
            CheckArguments(installation, game);
            string folder = installation.GetGameFolder(game);
            string program = WinPath.Combine(folder, game.ProgramName);
            StartResult Result(StartOutcome outcome) => new StartResult(outcome, installation, game, program);

            // 1. A running setup (contract 4.2): it may replace the program right now.
            SetupKind setup = detector.FindRunningSetup();
            if (setup != null)
            {
                logger.Info("Game start refused: the " + setup.Id + " setup is running (mutex " + setup.MutexName + ").");
                StartResult refused = Result(StartOutcome.SetupRunning);
                refused.RunningSetup = setup;
                return refused;
            }

            // 2. The same game runs: no second start; the other one: a warning first.
            if (detector.IsRunning(game))
            {
                bool processFound = detector.HasProcess(game);
                logger.Info("Game start refused: " + game.ProgramName + " is already running (mutex " + game.MutexName + ")" +
                            (processFound ? "; a process " + game.ProgramName + " exists, it may hang." : "; no process of that name is visible."));
                StartResult refused = Result(StartOutcome.AlreadyRunning);
                refused.ProcessFound = processFound;
                return refused;
            }
            Game other = RunningGameDetector.Other(game);
            bool otherRuns = detector.IsRunning(other);
            if (otherRuns && !startEvenIfOtherGameRuns)
            {
                logger.Info("Game start of " + game.ProgramName + " waits for the player: " + other.ProgramName + " is running.");
                StartResult warning = Result(StartOutcome.OtherGameRunning);
                warning.OtherGame = other;
                return warning;
            }

            // 3. The folder and the program.
            if (installation.State == InstallationState.FolderMissing)
            {
                logger.Warning("Game start refused: the chosen folder " + folder + " does not exist.");
                return Result(StartOutcome.FolderMissing);
            }
            if (!fileSystem.FileExists(program))
                return Damaged(Result(StartOutcome.Damaged), "it does not exist");

            // 4. Class S, then the first run (contract 3.6); neither stops the start.
            StartResult result = Result(StartOutcome.Started);
            result.OtherGame = otherRuns ? other : null;
            result.InstalledFrom = preparation.SynchronizeInstalledFrom(installation, game);
            if (!result.InstalledFrom.IsDone)
                logger.Warning("Game start of " + game.ProgramName + " without synchronized \"Installed From\" values: " +
                               result.InstalledFrom + ".");
            result.Defaults = preparation.ApplyDefaultsIfNeeded(installation, game, out DisplayQuestion question);
            result.Question = question;

            // 5. The start through the shell (contract 3.7), 6. the log.
            try
            {
                result.ProcessId = starter.StartProgram(program, folder);
            }
            catch (Win32Exception ex)
            {
                return StartFailed(result, ex.NativeErrorCode, ex);
            }
            catch (FileNotFoundException ex)
            {
                return StartFailed(result, ErrorFileNotFound, ex);
            }
            catch (InvalidOperationException ex)
            {
                return StartFailed(result, 0, ex);
            }

            logger.Info("Game started: " + installation.Product.Id + " " + installation.Root + ", game " + game.Id +
                        ", program " + program + ", " + (result.ProcessId.HasValue
                            ? "pid " + result.ProcessId.Value.ToString(CultureInfo.InvariantCulture)
                            : "pid unknown") +
                        (otherRuns ? " (" + other.ProgramName + " is running, the player started anyway)" : string.Empty) + ".");
            return result;
        }

        private StartResult Damaged(StartResult result, string why)
        {
            result.RepairAdvice = RepairAdvice.For(result.Installation, RepairReason.ProgramMissing, new[] { result.Game });
            logger.Warning("Game start refused: the program " + result.ProgramPath + " is missing (" + why + "); repair advice: " +
                           result.RepairAdvice + ".");
            return result;
        }

        /// <summary>A start that Windows refused, as a result with the outcome of its error code.</summary>
        private StartResult StartFailed(StartResult started, int errorCode, Exception error)
        {
            StartOutcome outcome;
            switch (errorCode)
            {
                case ErrorFileNotFound:
                case ErrorPathNotFound:
                    outcome = StartOutcome.Damaged;
                    break;
                case ErrorVirusInfected:
                case ErrorVirusDeleted:
                    outcome = StartOutcome.BlockedByAntivirus;
                    break;
                case ErrorCancelled:
                    outcome = StartOutcome.ElevationCancelled;
                    break;
                case ErrorAccessDenied:
                case ErrorAccessDisabledByPolicy:
                    outcome = StartOutcome.AccessDenied;
                    break;
                default:
                    outcome = StartOutcome.Failed;
                    break;
            }

            var result = new StartResult(outcome, started.Installation, started.Game, started.ProgramPath)
            {
                ErrorCode = errorCode,
                ErrorMessage = error.Message,
                OtherGame = started.OtherGame,
                InstalledFrom = started.InstalledFrom,
                Defaults = started.Defaults,
                Question = started.Question
            };
            if (outcome == StartOutcome.Damaged)
                return Damaged(result, "Windows error " + errorCode.ToString(CultureInfo.InvariantCulture) + " at the start");
            if (outcome == StartOutcome.BlockedByAntivirus)
                result.RepairAdvice = RepairAdvice.For(result.Installation, RepairReason.ProgramMissing, new[] { result.Game });

            if (outcome == StartOutcome.ElevationCancelled)
                logger.Info("Game start of " + result.ProgramPath + " cancelled: the elevation prompt was not confirmed (error 1223).");
            else
                logger.Error("Game start of " + result.ProgramPath + " failed (" + outcome + ", Windows error " +
                             errorCode.ToString(CultureInfo.InvariantCulture) + ").", error);
            return result;
        }

        private static void CheckArguments(Installation installation, Game game)
        {
            if (installation == null)
                throw new ArgumentNullException(nameof(installation));
            if (game == null)
                throw new ArgumentNullException(nameof(game));
            if (installation.GetGameFolder(game) == null)
                throw new ArgumentException(installation.Root + " has no folder of " + game.Id + ".", nameof(game));
        }
    }
}

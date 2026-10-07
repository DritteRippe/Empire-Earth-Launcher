using System;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Logging;
using Empire_Earth_Launcher.Core.Platform;

namespace Empire_Earth_Launcher.Core.Play
{
    /// <summary>Why <see cref="MutationGuard"/> refused a change; <see cref="None"/> if it allowed it.</summary>
    public enum MutationBlock
    {
        /// <summary>No setup and no game runs: the change may go ahead.</summary>
        None,
        /// <summary>
        /// A setup runs (<c>EE_Setup</c>, <c>NeoEE_Setup</c> or the suite's <c>EmpireEarthCommunity_Suite</c>); it writes the
        /// same values and files.
        /// </summary>
        SetupRunning,
        /// <summary>A game runs; it writes its settings on exit and holds its files.</summary>
        GameRunning
    }

    /// <summary>The answer of <see cref="MutationGuard.Check"/>: allowed, or blocked with the reason.</summary>
    public sealed class MutationCheck
    {
        private MutationCheck(MutationBlock block, SetupKind setup, Game game, string mutexName)
        {
            Block = block;
            Setup = setup;
            Game = game;
            MutexName = mutexName;
        }

        /// <summary>No setup and no game runs.</summary>
        public static MutationCheck Allowed { get; } = new MutationCheck(MutationBlock.None, null, null, null);

        public MutationBlock Block { get; }

        public bool IsAllowed
        {
            get { return Block == MutationBlock.None; }
        }

        /// <summary>The setup that runs (<see cref="MutationBlock.SetupRunning"/>), else null.</summary>
        public SetupKind Setup { get; }

        /// <summary>The game that runs (<see cref="MutationBlock.GameRunning"/>), else null.</summary>
        public Game Game { get; }

        /// <summary>The mutex that was found, else null.</summary>
        public string MutexName { get; }

        internal static MutationCheck SetupRunning(SetupKind setup)
        {
            return new MutationCheck(MutationBlock.SetupRunning, setup, null, setup.MutexName);
        }

        internal static MutationCheck GameRunning(Game game)
        {
            return new MutationCheck(MutationBlock.GameRunning, null, game, game.MutexName);
        }

        public override string ToString()
        {
            return IsAllowed ? "Allowed" : "Blocked(" + Block + ", " + MutexName + ")";
        }
    }

    /// <summary>
    /// Asked by every action that changes game settings, the defaults marker, compatibility values or files in the
    /// game folders (first run, reset, display defaults, compatibility options, WON login reset, imports, registry
    /// cleanup) before it changes anything (ADR 0016). While a setup or a game runs the action is blocked and
    /// changes nothing; read-only actions (checks, export, diagnostics) do not ask.
    /// </summary>
    /// <remarks>
    /// The mutex names are fixed per product and game, not per installation (contract 0), so a running game of any
    /// installation blocks: it may write the shared settings keys or compatibility values on exit. Every setup blocks,
    /// because the product setups write the GPU preference and the compatibility values of the programs they install, and
    /// the suite (revision 4) holds its mutex around both of them, also between the two runs. The setups
    /// are checked first, so a setup that starts a game is reported as the setup.
    /// </remarks>
    public sealed class MutationGuard
    {
        private readonly IMutexProbe probe;
        private readonly ILogger logger;

        public MutationGuard(IMutexProbe probe, ILogger logger)
        {
            this.probe = probe ?? throw new ArgumentNullException(nameof(probe));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// The setup that runs now (its <c>SetupMutex</c> exists), or null; not logged. For readers that must not read the
        /// files of an installation while a setup writes them (contract 4.2), e.g. <see cref="Maintenance.ManifestFiles"/>.
        /// </summary>
        public SetupKind FindRunningSetup()
        {
            return RunningGameDetector.FindRunningSetup(probe);
        }

        /// <summary>Whether a change may go ahead now; a block is logged.</summary>
        /// <param name="action">What the caller wants to do, for the log (English), e.g. "reset the game settings".</param>
        public MutationCheck Check(string action)
        {
            if (string.IsNullOrEmpty(action))
                throw new ArgumentException("The action is required for the log.", nameof(action));

            foreach (SetupKind setup in SetupKind.All)
            {
                if (probe.Exists(setup.MutexName))
                {
                    logger.Info("Not allowed to " + action + " now: the " + setup.Id + " setup is running (mutex " +
                                setup.MutexName + ").");
                    return MutationCheck.SetupRunning(setup);
                }
            }

            foreach (Game game in Game.All)
            {
                if (probe.Exists(game.MutexName))
                {
                    logger.Info("Not allowed to " + action + " now: " + game.ProgramName + " is running (mutex " +
                                game.MutexName + ").");
                    return MutationCheck.GameRunning(game);
                }
            }

            return MutationCheck.Allowed;
        }
    }
}

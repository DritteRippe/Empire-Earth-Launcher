using System;
using System.Linq;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Platform;

namespace Empire_Earth_Launcher.Core.Play
{
    /// <summary>
    /// What runs right now (ADR 0010): a setup (its <c>SetupMutex</c>, contract 4.2; the suite's too, revision 4), a game (its mutex, contract 0) and,
    /// as extra information for the hint about a hanging game, a process of a game's program.
    /// </summary>
    /// <remarks>
    /// The mutexes decide; they are what the setup itself uses (<c>AppMutex</c>). The process list only adds the hint
    /// that a game whose window is gone may hang and can be ended in the Task Manager (forum report section 8 row 14,
    /// t=2815, t=5859). The launcher never ends a process.
    /// </remarks>
    public sealed class RunningGameDetector
    {
        private readonly IMutexProbe probe;
        private readonly IProcessList processes;

        public RunningGameDetector(IMutexProbe probe, IProcessList processes)
        {
            this.probe = probe ?? throw new ArgumentNullException(nameof(probe));
            this.processes = processes ?? throw new ArgumentNullException(nameof(processes));
        }

        /// <summary>
        /// The setup that runs (<c>NeoEE_Setup</c>, <c>EE_Setup</c>, then <c>EmpireEarthCommunity_Suite</c>, as the mutation
        /// guard); null if none.
        /// </summary>
        public SetupKind FindRunningSetup()
        {
            return FindRunningSetup(probe);
        }

        /// <summary>The setup that runs, by <paramref name="mutexProbe"/>; null if none.</summary>
        public static SetupKind FindRunningSetup(IMutexProbe mutexProbe)
        {
            if (mutexProbe == null)
                throw new ArgumentNullException(nameof(mutexProbe));
            return SetupKind.All.FirstOrDefault(setup => mutexProbe.Exists(setup.MutexName));
        }

        /// <summary>True if the mutex of <paramref name="game"/> exists: the game runs (or hangs while it holds the mutex).</summary>
        public bool IsRunning(Game game)
        {
            if (game == null)
                throw new ArgumentNullException(nameof(game));
            return probe.Exists(game.MutexName);
        }

        /// <summary>True if a process of the program of <paramref name="game"/> exists (in any session).</summary>
        public bool HasProcess(Game game)
        {
            if (game == null)
                throw new ArgumentNullException(nameof(game));
            return processes.IsRunning(game.ProgramName);
        }

        /// <summary>The other one of the two games.</summary>
        public static Game Other(Game game)
        {
            if (game == null)
                throw new ArgumentNullException(nameof(game));
            return game == Game.EmpireEarth ? Game.ArtOfConquest : Game.EmpireEarth;
        }
    }
}

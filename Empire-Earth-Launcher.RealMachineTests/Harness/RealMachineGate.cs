using System;

namespace Empire_Earth_Launcher.RealMachineTests.Harness
{
    /// <summary>Whether the checks against the real computer may run.</summary>
    internal enum GateState
    {
        /// <summary>Switched off (the normal case: CI of the launcher, the verify script, a developer's computer): ignored.</summary>
        Disabled,

        /// <summary>Switched on where it must not run: the tests fail with the reason.</summary>
        Refused,

        /// <summary>Switched on on a GitHub-hosted Windows runner.</summary>
        Enabled
    }

    /// <summary>The decision of <see cref="RealMachineGate.Evaluate"/> and why.</summary>
    internal sealed class GateDecision
    {
        public GateDecision(GateState state, string reason)
        {
            State = state;
            Reason = reason ?? throw new ArgumentNullException(nameof(reason));
        }

        public GateState State { get; }

        public string Reason { get; }
    }

    /// <summary>
    /// The second lock of the category <c>RealMachine</c> after <c>[Explicit]</c>: the checks run only when
    /// <see cref="SwitchVariable"/> is <c>1</c>, on Windows, on a runner that GitHub hosts and throws away after the job. The
    /// checks of the game defaults write the game settings of the current Windows account (as the launcher does at its start),
    /// which must never happen on a player's computer by accident.
    /// </summary>
    internal static class RealMachineGate
    {
        /// <summary>The switch; only the value <c>1</c> turns the checks on.</summary>
        public const string SwitchVariable = "EE_LAUNCHER_REAL_MACHINE_TESTS";

        /// <summary>Set by GitHub Actions on every runner: <see cref="GitHubHostedRunner"/> or <c>self-hosted</c>.</summary>
        public const string RunnerEnvironmentVariable = "RUNNER_ENVIRONMENT";

        /// <summary>The value of <see cref="RunnerEnvironmentVariable"/> on a runner of GitHub.</summary>
        public const string GitHubHostedRunner = "github-hosted";

        /// <summary>The decision for the environment variables <paramref name="environment"/> returns.</summary>
        /// <param name="environment">The value of an environment variable, or null if it is not set.</param>
        /// <param name="isWindows">True on Windows (not under Mono on Linux).</param>
        public static GateDecision Evaluate(Func<string, string> environment, bool isWindows)
        {
            if (environment == null)
                throw new ArgumentNullException(nameof(environment));
            string value = environment(SwitchVariable);
            if (value != "1")
                return new GateDecision(GateState.Disabled,
                    "The checks against the real computer are switched off (" + SwitchVariable + " is " +
                    (value == null ? "not set" : "\"" + value + "\"") + "; only 1 switches them on, on a GitHub-hosted Windows runner).");
            if (!isWindows)
                return new GateDecision(GateState.Refused,
                    SwitchVariable + " is 1, but the checks against the real computer need Windows: they read the real registry.");
            string runner = environment(RunnerEnvironmentVariable);
            if (runner != GitHubHostedRunner)
                return new GateDecision(GateState.Refused,
                    SwitchVariable + " is 1, but " + RunnerEnvironmentVariable + " is " + (runner == null ? "not set" : "\"" + runner + "\"") +
                    ": the checks run only on a GitHub-hosted runner, because the defaults checks write the game settings of the " +
                    "current Windows account.");
            return new GateDecision(GateState.Enabled, "Switched on, on a GitHub-hosted Windows runner.");
        }
    }
}

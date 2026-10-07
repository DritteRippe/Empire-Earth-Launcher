using System;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.RealMachineTests.Expectations;
using Empire_Earth_Launcher.RealMachineTests.Harness;
using NUnit.Framework;

namespace Empire_Earth_Launcher.RealMachineTests.RealMachine
{
    /// <summary>
    /// The one session of a run against the real computer: every fixture of this folder opens it in its one-time set-up. The
    /// gate decides first (<see cref="RealMachineGate"/>: ignored when switched off, failed when switched on where it must not
    /// run); then the parameters, the expectation, the real adapters and the snapshot before the checks, once per run.
    /// </summary>
    internal static class RealMachineRun
    {
        /// <summary>The reason of <c>[Explicit]</c> on every fixture of the category.</summary>
        public const string ExplicitReason = "Checks against the real computer: only with --where \"cat == RealMachine\" and " +
                                             RealMachineGate.SwitchVariable + "=1 on a GitHub-hosted Windows runner";

        /// <summary>The NUnit parameter with the full path of the expectation file.</summary>
        public const string ExpectationParameter = "expect";

        /// <summary>The NUnit parameter with the full path of the work folder of the step (created if needed, never uploaded).</summary>
        public const string WorkFolderParameter = "work";

        private static readonly object Sync = new object();
        private static HarnessSession session;
        private static string startFailure;

        /// <summary>The session of this run; ignores or fails the calling fixture as the gate decides.</summary>
        public static HarnessSession Open()
        {
            GateDecision gate = RealMachineGate.Evaluate(Environment.GetEnvironmentVariable,
                Environment.OSVersion.Platform == PlatformID.Win32NT);
            if (gate.State == GateState.Disabled)
                Assert.Ignore(gate.Reason);
            if (gate.State == GateState.Refused)
                Assert.Fail(gate.Reason);

            lock (Sync)
            {
                if (session == null && startFailure == null)
                {
                    try
                    {
                        session = Start();
                    }
                    catch (Exception ex) when (ex is FormatException || ex is ArgumentException || ex is InvalidOperationException)
                    {
                        startFailure = SafeText.Redact(ex.Message);
                    }
                }
                if (startFailure != null)
                    Assert.Fail("The real-machine session could not start: " + startFailure);
                return session;
            }
        }

        private static HarnessSession Start()
        {
            string expectationFile = TestContext.Parameters.Get(ExpectationParameter);
            string workFolder = TestContext.Parameters.Get(WorkFolderParameter);
            if (string.IsNullOrWhiteSpace(expectationFile) || string.IsNullOrWhiteSpace(workFolder) ||
                !WinPath.IsFullyQualified(expectationFile) || !WinPath.IsFullyQualified(workFolder))
                throw new ArgumentException("The checks need full paths in --params \"" + ExpectationParameter + "=<file>;" +
                                            WorkFolderParameter + "=<folder>\" (no ';' in the paths).");
            RealAdapters adapters = RealAdapters.Create(workFolder);
            Expectation expectation = ExpectationFile.Load(new ReadOnlyFileSystem(adapters.FileSystem, new HarnessViolations()), expectationFile);
            TestContext.Progress.WriteLine("RealMachine: scenario " + (expectation.Scenario ?? "-") + ", step " + (expectation.Step ?? "-") +
                                           ", " + expectation.Installations.Count + " installation(s) expected");
            return new HarnessSession(expectation, workFolder, adapters.Registry, adapters.FileSystem, adapters.SystemInfo, adapters.MutexProbe,
                SystemClock.Instance, adapters.Logger);
        }
    }
}

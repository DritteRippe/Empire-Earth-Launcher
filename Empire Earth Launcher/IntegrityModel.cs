using System;
using System.Threading;
using System.Threading.Tasks;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Integrity;
using Empire_Earth_Launcher.Core.Logging;
using Empire_Earth_Launcher.Core.Play;
using Empire_Earth_Launcher.Core.Repair;

namespace Empire_Earth_Launcher
{
    /// <summary>
    /// The integrity check of the selected installation for the Play and Tools pages (contract 2.5, L-WP7, ARCHITECTURE
    /// 4.1 step 5 and 4.3): the quick check in the background after every search of the installations (at start and after a
    /// setup has ended), the full check on request with progress and cancel, the latest report, and the repair advice
    /// that goes with it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Created once by <see cref="Program"/>; use it on the UI thread. The check runs on the thread pool
    /// (<see cref="IntegrityChecker.CheckAsync"/>) and only reads; nothing waits for it, Play least of all (contract 2.5:
    /// a finding never blocks a start). <see cref="Changed"/> is raised on the UI thread.
    /// </para>
    /// <para>
    /// Setups (contract 4.2, ADR 0016 plan review): the search of the installations, and with it the quick check, waits
    /// while a setup runs; the checker itself does not start while a setup mutex exists and stops when one appears; the
    /// <see cref="SetupWatcher.SetupStarted"/> hook cancels a running check as well. When the setup has ended the search
    /// runs again and the quick check after it.
    /// </para>
    /// <para>
    /// One check at a time: a new check (another installation, the full check) cancels the running one, and only the
    /// result of the latest check counts.
    /// </para>
    /// </remarks>
    internal sealed class IntegrityModel
    {
        private readonly IntegrityChecker checker;
        private readonly InstallationService installations;
        private readonly SetupWatcher setupWatcher;
        private readonly ILogger logger;

        /// <summary>The search result whose selected installation the last quick check was started for.</summary>
        private DiscoveryResult checkedResult;

        /// <summary>Cancels the running check; null while none runs.</summary>
        private CancellationTokenSource running;

        /// <summary>Counts the checks, so that only the result of the latest one is used.</summary>
        private int generation;

        /// <param name="checker">The integrity check of the core (contract 2).</param>
        /// <param name="installations">The selected installation; every new search result starts a quick check.</param>
        /// <param name="setupWatcher">A setup that starts cancels the running check (contract 4.2).</param>
        /// <param name="logger">Log of the launcher.</param>
        public IntegrityModel(IntegrityChecker checker, InstallationService installations, SetupWatcher setupWatcher,
            ILogger logger)
        {
            this.checker = checker ?? throw new ArgumentNullException(nameof(checker));
            this.installations = installations ?? throw new ArgumentNullException(nameof(installations));
            this.setupWatcher = setupWatcher ?? throw new ArgumentNullException(nameof(setupWatcher));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            installations.Changed += (sender, e) => OnInstallationsChanged();
            setupWatcher.SetupStarted += (sender, e) => OnSetupStarted(e.Setup);
            setupWatcher.SetupFinished += (sender, e) => RaiseChanged();
        }

        /// <summary>Raised when a check starts, progresses or ends, and when the installation or the setup state changed.</summary>
        public event EventHandler Changed;

        /// <summary>The selected installation; null while none is known or none was found.</summary>
        public Installation Selected
        {
            get { return installations.Selected; }
        }

        /// <summary>
        /// The result of the latest finished check of the selected installation; null before the first one and while the
        /// first check after a new search runs.
        /// </summary>
        public IntegrityReport Report { get; private set; }

        /// <summary>The kind of the running check; null while none runs.</summary>
        public IntegrityCheckKind? RunningCheck { get; private set; }

        /// <summary>True while a check runs.</summary>
        public bool IsChecking
        {
            get { return RunningCheck != null; }
        }

        /// <summary>The progress of the running full check; null before its first file and for the quick check.</summary>
        public IntegrityProgress Progress { get; private set; }

        /// <summary>The task of the latest check (for the tests); null before the first one.</summary>
        public Task<IntegrityReport> LastCheck { get; private set; }

        /// <summary>
        /// True if the selected installation can be checked now: it exists, it comes from the community setup (contract 2.5:
        /// no check of foreign installations), and neither a search nor a setup runs (contract 4.2).
        /// </summary>
        public bool CanCheck
        {
            get
            {
                Installation selected = Selected;
                return selected != null && selected.Kind != InstallationKind.Foreign &&
                       selected.State != InstallationState.FolderMissing && !installations.IsSearching &&
                       !installations.IsWaitingForSetup && !setupWatcher.IsSetupRunning;
            }
        }

        /// <summary>True if the full check can be started: <see cref="CanCheck"/>, and no full check runs.</summary>
        public bool CanStartFullCheck
        {
            get { return CanCheck && RunningCheck != IntegrityCheckKind.Full; }
        }

        /// <summary>
        /// The repair advice for the selected installation (contract 4.4): with the files and the antivirus exception
        /// first when the latest report offers the repair (Damaged, Incomplete, Unknown of a community installation); for a
        /// missing program the advice of a damaged installation; otherwise the advice on request. Null without an
        /// installation. The download location is the fixed page until <see cref="UpdateModel.LocateAsync"/> asked the
        /// update API.
        /// </summary>
        public RepairAdvice CreateRepairAdvice()
        {
            Installation selected = Selected;
            if (selected == null)
                return null;
            IntegrityReport report = Report;
            if (report != null && report.Installation == selected && report.OffersRepair)
                return RepairAdvice.ForIntegrity(report);
            return RepairAdvice.For(selected,
                selected.State == InstallationState.Damaged ? RepairReason.ProgramMissing : RepairReason.Requested);
        }

        /// <summary>Starts the full check of the selected installation (contract 2.5: on request of the user).</summary>
        /// <returns>The report; null if nothing could be checked (no installation).</returns>
        public Task<IntegrityReport> StartFullCheckAsync()
        {
            if (!CanStartFullCheck)
                throw new InvalidOperationException("The full check cannot be started now.");
            logger.Info("Integrity: the full check of " + Selected.Root + " was started by the user.");
            return Start(IntegrityCheckKind.Full);
        }

        /// <summary>Cancels the running check (button, closing the window); its findings are dropped.</summary>
        public void CancelCheck()
        {
            if (running == null)
                return;
            logger.Info("Integrity: the " + RunningCheck.ToString().ToLowerInvariant() + " check is cancelled.");
            running.Cancel();
        }

        /// <summary>
        /// A search has a new result: the quick check of its selected installation starts (contract 2.5 "at every start",
        /// ARCHITECTURE 4.3 "after a setup"). While a search runs, the previous report stays.
        /// </summary>
        private void OnInstallationsChanged()
        {
            DiscoveryResult result = installations.Result;
            if (installations.IsSearching || result == null || result == checkedResult)
            {
                RaiseChanged();
                return;
            }
            checkedResult = result;
            Report = null;
            if (result.Selected == null)
            {
                running?.Cancel();
                RaiseChanged();
                return;
            }
            Start(IntegrityCheckKind.Quick);
        }

        /// <summary>A setup started: the running check is cancelled (contract 4.2) and gets the reason from the checker.</summary>
        private void OnSetupStarted(Product setup)
        {
            if (running != null)
            {
                logger.Info("Integrity: the " + setup.Id + " setup started; the running " +
                            RunningCheck.ToString().ToLowerInvariant() + " check is cancelled (contract 4.2).");
                running.Cancel();
            }
            RaiseChanged();
        }

        private Task<IntegrityReport> Start(IntegrityCheckKind kind)
        {
            Installation installation = Selected;
            running?.Cancel();
            // Not disposed: no timer is attached, and a late Cancel on a disposed source would throw.
            var cancellation = new CancellationTokenSource();
            running = cancellation;
            int current = ++generation;
            RunningCheck = kind;
            Progress = null;
            RaiseChanged();
            Task<IntegrityReport> check = RunAsync(installation, kind, cancellation, current);
            LastCheck = check;
            // The checker returns environment problems as results (ADR 0013); a fault is a programming error, logged like an
            // unobserved task exception.
            check.ContinueWith(task => logger.Error("The integrity check of " + installation.Root + " failed.", task.Exception),
                CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
            return check;
        }

        private async Task<IntegrityReport> RunAsync(Installation installation, IntegrityCheckKind kind,
            CancellationTokenSource cancellation, int current)
        {
            // Progress<T> reports on the thread that created it, the UI thread.
            IProgress<IntegrityProgress> progress = kind == IntegrityCheckKind.Full
                ? new Progress<IntegrityProgress>(value => OnProgress(value, current))
                : null;
            IntegrityReport report;
            try
            {
                report = await checker.CheckAsync(installation, kind, progress, cancellation.Token);
            }
            catch (Exception)
            {
                // A bug of the check (environment problems are results): the pages must not say "checking" for ever. The
                // fault itself is logged by the continuation of Start.
                if (current == generation)
                {
                    running = null;
                    RunningCheck = null;
                    Progress = null;
                    RaiseChanged();
                }
                throw;
            }
            if (current == generation)
            {
                running = null;
                RunningCheck = null;
                Progress = null;
            }
            if (current != generation)
                return report; // a later check (another installation, the full check) has started; its result counts
            Report = report;
            RaiseChanged();
            return report;
        }

        private void OnProgress(IntegrityProgress value, int current)
        {
            if (current != generation || RunningCheck == null)
                return;
            Progress = value;
            RaiseChanged();
        }

        private void RaiseChanged()
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}

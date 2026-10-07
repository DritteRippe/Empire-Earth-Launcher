using System;
using System.Globalization;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Logging;
using Empire_Earth_Launcher.Core.Platform;

namespace Empire_Earth_Launcher.Core.Play
{
    /// <summary>A setup that started or ended, for the hooks of <see cref="SetupWatcher"/>.</summary>
    public sealed class SetupStateEventArgs : EventArgs
    {
        internal SetupStateEventArgs(SetupKind setup, TimeSpan? duration)
        {
            Setup = setup;
            Duration = duration;
        }

        /// <summary>The setup that started or ended.</summary>
        public SetupKind Setup { get; }

        /// <summary>When a setup ended: how long the launcher saw it running; null when a setup started.</summary>
        public TimeSpan? Duration { get; }
    }

    /// <summary>
    /// Watches the setup mutexes <c>EE_Setup</c>, <c>NeoEE_Setup</c> and, since contract revision 4, the suite's
    /// <c>EmpireEarthCommunity_Suite</c> while the launcher runs (contract 4.2, ADR 0010, ARCHITECTURE 4.3). While one exists, Play and every guarded change are blocked and the pages say so; when it is
    /// gone, the installations are searched again.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The UI calls <see cref="Tick"/> from a WinForms timer on the UI thread; the watcher probes when
    /// <see cref="Interval"/> has passed by its <see cref="IClock"/> (two seconds; the probe takes microseconds), so the
    /// tests move a fake clock and tick by hand. The events are raised on the thread that probes.
    /// </para>
    /// <para>
    /// <see cref="SetupStarted"/> and <see cref="SetupFinished"/> are the hooks other parts register: the discovery runs
    /// again when a setup has finished (<c>InstallationService</c>), and the integrity check of L-WP7 cancels a running
    /// check when a setup starts and runs the quick check again when it has finished (ADR 0016 plan review).
    /// </para>
    /// </remarks>
    public sealed class SetupWatcher
    {
        /// <summary>How often the mutexes are probed while the launcher runs (ADR 0010).</summary>
        public static readonly TimeSpan Interval = TimeSpan.FromSeconds(2);

        private readonly IMutexProbe probe;
        private readonly IClock clock;
        private readonly ILogger logger;

        private DateTime? lastProbeUtc;
        private DateTime setupSeenUtc;

        public SetupWatcher(IMutexProbe probe, IClock clock, ILogger logger)
        {
            this.probe = probe ?? throw new ArgumentNullException(nameof(probe));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Hook: a setup mutex appeared while none existed, or another setup replaced the one seen. The pages block Play
        /// and the guarded changes; L-WP7 cancels a running integrity check.
        /// </summary>
        public event EventHandler<SetupStateEventArgs> SetupStarted;

        /// <summary>
        /// Hook: no setup mutex exists any more. The installations are searched again; L-WP7 runs the quick check again.
        /// </summary>
        public event EventHandler<SetupStateEventArgs> SetupFinished;

        /// <summary>The setup that was seen running at the last probe; null if none (or not probed yet).</summary>
        public SetupKind RunningSetup { get; private set; }

        /// <summary>True if a setup was seen running at the last probe.</summary>
        public bool IsSetupRunning
        {
            get { return RunningSetup != null; }
        }

        /// <summary>
        /// Called by the timer: probes if <see cref="Interval"/> has passed since the last probe (or the clock went back),
        /// and raises the hooks on a change.
        /// </summary>
        /// <returns>True if it probed.</returns>
        public bool Tick()
        {
            DateTime now = clock.UtcNow;
            if (lastProbeUtc.HasValue && now >= lastProbeUtc.Value && now - lastProbeUtc.Value < Interval)
                return false;
            ProbeNow();
            return true;
        }

        /// <summary>Probes now (at the start, before a refresh of the installations) and raises the hooks on a change.</summary>
        /// <returns>The setup that runs, or null.</returns>
        public SetupKind ProbeNow()
        {
            DateTime now = clock.UtcNow;
            lastProbeUtc = now;
            SetupKind before = RunningSetup;
            SetupKind running = RunningGameDetector.FindRunningSetup(probe);
            if (running == before)
                return running;

            RunningSetup = running;
            if (running != null)
            {
                setupSeenUtc = now;
                logger.Info("The " + running.Id + " setup is running (mutex " + running.MutexName +
                            "): games are not started and nothing is changed until it has ended.");
                SetupStarted?.Invoke(this, new SetupStateEventArgs(running, null));
            }
            else
            {
                TimeSpan duration = now >= setupSeenUtc ? now - setupSeenUtc : TimeSpan.Zero;
                logger.Info("The " + before.Id + " setup has ended (seen for " +
                            ((int)duration.TotalSeconds).ToString(CultureInfo.InvariantCulture) +
                            " s); the installations are searched again.");
                SetupFinished?.Invoke(this, new SetupStateEventArgs(before, duration));
            }
            return running;
        }
    }
}

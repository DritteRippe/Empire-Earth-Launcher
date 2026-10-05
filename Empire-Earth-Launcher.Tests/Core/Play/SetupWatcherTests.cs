using System;
using System.Collections.Generic;
using System.Linq;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Play;
using Empire_Earth_Launcher.Tests.Fakes;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.Play
{
    /// <summary>
    /// <see cref="SetupWatcher"/> with a fake clock and manual ticks (contract 4.2, ADR 0010): a probe every two seconds,
    /// the hooks "setup started" and "setup finished".
    /// </summary>
    [TestFixture]
    public class SetupWatcherTests
    {
        private FakeMutexProbe mutexes;
        private FakeClock clock;
        private RecordingLogger logger;
        private SetupWatcher watcher;
        private List<string> hooks;

        [SetUp]
        public void SetUp()
        {
            mutexes = new FakeMutexProbe();
            clock = new FakeClock();
            logger = new RecordingLogger();
            watcher = new SetupWatcher(mutexes, clock, logger);
            hooks = new List<string>();
            watcher.SetupStarted += (sender, e) => hooks.Add("started " + e.Setup.Id);
            watcher.SetupFinished += (sender, e) => hooks.Add("finished " + e.Setup.Id + " after " + e.Duration.Value.TotalSeconds + " s");
        }

        private int Probes
        {
            get { return mutexes.Probed.Count(name => name == "NeoEE_Setup"); }
        }

        [Test]
        public void TheFirstTick_Probes_AndNoSetupMeansNoHook()
        {
            Assert.That(watcher.Tick(), Is.True);

            Assert.That(watcher.IsSetupRunning, Is.False);
            Assert.That(watcher.RunningSetup, Is.Null);
            Assert.That(mutexes.Probed, Is.EqualTo(new[] { "NeoEE_Setup", "EE_Setup", "EmpireEarthCommunity_Suite" }));
            Assert.That(hooks, Is.Empty);
            Assert.That(logger.Entries, Is.Empty);
        }

        [Test]
        public void Ticks_ProbeEveryTwoSecondsOfTheClock()
        {
            watcher.Tick();
            clock.Advance(TimeSpan.FromMilliseconds(500));
            Assert.That(watcher.Tick(), Is.False);
            clock.Advance(TimeSpan.FromMilliseconds(1499));
            Assert.That(watcher.Tick(), Is.False);
            Assert.That(Probes, Is.EqualTo(1));

            clock.Advance(TimeSpan.FromMilliseconds(1));
            Assert.That(watcher.Tick(), Is.True);
            Assert.That(Probes, Is.EqualTo(2));
            Assert.That(SetupWatcher.Interval, Is.EqualTo(TimeSpan.FromSeconds(2)));
        }

        [Test]
        public void AClockThatGoesBack_ProbesAtOnce()
        {
            watcher.Tick();
            clock.Advance(TimeSpan.FromHours(-1));

            Assert.That(watcher.Tick(), Is.True);
        }

        [Test]
        public void ProbeNow_AlwaysProbes()
        {
            watcher.Tick();
            mutexes.With("EE_Setup");

            Assert.That(watcher.ProbeNow(), Is.SameAs(SetupKind.EE));
            Assert.That(Probes, Is.EqualTo(2));
            Assert.That(hooks, Is.EqualTo(new[] { "started EE" }));
        }

        [Test]
        public void Contract_4_2_ASetupThatStartsAndEnds_RaisesEachHookOnce()
        {
            watcher.Tick();
            mutexes.With("NeoEE_Setup");
            clock.Advance(SetupWatcher.Interval);
            watcher.Tick();
            Assert.That(watcher.RunningSetup, Is.SameAs(SetupKind.NeoEE));
            for (int i = 0; i < 5; i++)
            {
                clock.Advance(SetupWatcher.Interval);
                watcher.Tick();
            }
            Assert.That(watcher.IsSetupRunning, Is.True);

            mutexes.Remove("NeoEE_Setup");
            clock.Advance(SetupWatcher.Interval);
            watcher.Tick();

            Assert.That(watcher.IsSetupRunning, Is.False);
            Assert.That(hooks, Is.EqualTo(new[] { "started NeoEE", "finished NeoEE after 12 s" }));
            Assert.That(logger.Messages, Is.EqualTo(new[]
            {
                "Info: The NeoEE setup is running (mutex NeoEE_Setup): games are not started and nothing is changed until it has ended.",
                "Info: The NeoEE setup has ended (seen for 12 s); the installations are searched again."
            }));
        }

        [Test]
        public void ASetupRunningAtTheStart_RaisesStarted()
        {
            mutexes.With("EE_Setup");

            watcher.Tick();

            Assert.That(hooks, Is.EqualTo(new[] { "started EE" }));
        }

        [Test]
        public void AnotherSetupReplacingTheFirst_IsAStartNotAnEnd()
        {
            mutexes.With("NeoEE_Setup");
            watcher.Tick();
            mutexes.Remove("NeoEE_Setup");
            mutexes.With("EE_Setup");
            clock.Advance(SetupWatcher.Interval);

            watcher.Tick();

            Assert.That(watcher.RunningSetup, Is.SameAs(SetupKind.EE));
            Assert.That(hooks, Is.EqualTo(new[] { "started NeoEE", "started EE" }), "no discovery while a setup runs");
        }

        [Test]
        public void EveryRegisteredHook_IsCalled()
        {
            var second = new List<SetupKind>();
            watcher.SetupFinished += (sender, e) => second.Add(e.Setup);
            mutexes.With("EE_Setup");
            watcher.Tick();
            mutexes.Remove("EE_Setup");

            watcher.ProbeNow();

            Assert.That(hooks.Last(), Does.StartWith("finished EE"));
            Assert.That(second, Is.EqualTo(new[] { SetupKind.EE }));
        }

        [Test]
        public void TheSuite_IsASetupLikeTheProductSetups()
        {
            mutexes.With("EmpireEarthCommunity_Suite");

            watcher.Tick();

            Assert.That(watcher.IsSetupRunning, Is.True);
            Assert.That(watcher.RunningSetup, Is.SameAs(SetupKind.Suite));
            Assert.That(hooks, Is.EqualTo(new[] { "started Suite" }));
            Assert.That(logger.Messages.Single(), Does.Contain("The Suite setup is running (mutex EmpireEarthCommunity_Suite)"));
        }

        [Test]
        public void BetweenTwoProductSetups_TheSuiteMutexKeepsTheSetupRunning()
        {
            // The suite holds its mutex for its whole run, also in the moment where neither product mutex exists
            // (contract 4.2): the launcher never sees "no setup" there, so the search does not start in the gap.
            mutexes.With("EmpireEarthCommunity_Suite", "EE_Setup");
            watcher.Tick();
            mutexes.Remove("EE_Setup");
            clock.Advance(SetupWatcher.Interval);
            watcher.Tick();
            mutexes.With("NeoEE_Setup");
            clock.Advance(SetupWatcher.Interval);
            watcher.Tick();
            mutexes.Remove("NeoEE_Setup");
            clock.Advance(SetupWatcher.Interval);
            watcher.Tick();

            Assert.That(watcher.RunningSetup, Is.SameAs(SetupKind.Suite));
            Assert.That(hooks.Any(hook => hook.StartsWith("finished", StringComparison.Ordinal)), Is.False, "no end before the suite ends");

            mutexes.Remove("EmpireEarthCommunity_Suite");
            clock.Advance(SetupWatcher.Interval);
            watcher.Tick();

            Assert.That(watcher.IsSetupRunning, Is.False);
            Assert.That(hooks.Last(), Does.StartWith("finished Suite"));
            Assert.That(hooks.Count(hook => hook.StartsWith("finished", StringComparison.Ordinal)), Is.EqualTo(1), "one end, for the whole run");
        }

        [TestCase("StainlessSteelStudiosPresentsEmpireEarth")]
        [TestCase("EmpireEarthCommunityLauncher")]
        public void AGameOrTheLauncher_IsNoSetup(string mutex)
        {
            mutexes.With(mutex);

            watcher.Tick();

            Assert.That(watcher.IsSetupRunning, Is.False);
            Assert.That(hooks, Is.Empty);
        }
    }
}

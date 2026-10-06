using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Logging;
using Empire_Earth_Launcher.Core.Play;
using Empire_Earth_Launcher.Tests.Fakes;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.Play
{
    /// <summary>
    /// <see cref="GameWindowActivator"/> with a fake window system and a delay that only moves a fake clock (ADR 0010 amendment of
    /// 1.1.0): the window is found after some looks, the foreground is handed over only while the launcher (or the game) owns it,
    /// never stolen from another program, re-checked after two seconds, handed over again at most three times, and given up
    /// after 60 seconds without a window.
    /// </summary>
    [TestFixture]
    public class GameWindowActivatorTests
    {
        private const int LauncherPid = 100;
        private const int GamePid = 4242;
        private const int OtherPid = 777;

        private FakeClock clock;
        private FakeWindowSystem windows;
        private RecordingLogger logger;
        private List<TimeSpan> waits;
        private Action<TimeSpan> onWait;
        private GameWindowActivator activator;

        [SetUp]
        public void SetUp()
        {
            clock = new FakeClock();
            windows = new FakeWindowSystem(GamePid) { Foreground = LauncherPid };
            logger = new RecordingLogger();
            waits = new List<TimeSpan>();
            onWait = null;
            activator = new GameWindowActivator(windows, clock, logger, LauncherPid, Wait);
        }

        /// <summary>The delay of the activator: no real waiting, the fake clock moves, and the test may change the world.</summary>
        private Task Wait(TimeSpan time, CancellationToken cancellationToken)
        {
            waits.Add(time);
            clock.Advance(time);
            onWait?.Invoke(time);
            return Task.CompletedTask;
        }

        [Test]
        public async Task TheWindowAppearsAfterSomePolls_TheForegroundIsHandedOverOnce()
        {
            windows.LooksWithoutWindow = 7;

            ActivationOutcome outcome = await activator.ActivateAsync(GamePid, Game.EmpireEarth);

            Assert.That(outcome, Is.EqualTo(ActivationOutcome.GameInForeground));
            Assert.That(windows.SetForegroundCalls, Is.EqualTo(new[] { windows.Window }), "once, on the window that was found");
            Assert.That(waits.Take(7), Is.All.EqualTo(GameWindowActivator.PollInterval));
            Assert.That(waits.Last(), Is.EqualTo(GameWindowActivator.RecheckInterval), "one look again after two seconds");
            Assert.That(windows.AllowCalls, Is.Empty, "the right is granted by the start, not here");
            Assert.That(logger.Messages, Has.Some.EqualTo("Info: Game window 0x1234 of Empire Earth.exe (pid 4242) brought to the foreground " +
                                                          "after 700 ms (the foreground was pid 100 (the launcher))."));
        }

        [Test]
        public async Task TheGameOwnsTheForegroundAlready_ItStaysWithTheGame()
        {
            windows.Foreground = GamePid;

            ActivationOutcome outcome = await activator.ActivateAsync(GamePid, Game.ArtOfConquest);

            Assert.That(outcome, Is.EqualTo(ActivationOutcome.GameInForeground));
            Assert.That(windows.SetForegroundCalls, Has.Count.EqualTo(1));
            Assert.That(logger.Messages, Has.Some.Contains("Game window 0x1234 of EE-AOC.exe (pid 4242) brought to the foreground after 0 ms"));
        }

        [Test]
        public async Task AnotherProgramOwnsTheForeground_NothingIsStolen()
        {
            windows.Foreground = OtherPid;
            windows.LooksWithoutWindow = 3;

            ActivationOutcome outcome = await activator.ActivateAsync(GamePid, Game.EmpireEarth);

            Assert.That(outcome, Is.EqualTo(ActivationOutcome.UserSwitched));
            Assert.That(windows.SetForegroundCalls, Is.Empty);
            Assert.That(logger.Messages, Has.Some.Contains("found after 300 ms, not brought to the foreground: skipped, user switched to pid 777."));
        }

        [Test]
        public async Task NobodyOwnsTheForeground_NothingIsChanged()
        {
            windows.Foreground = 0;

            ActivationOutcome outcome = await activator.ActivateAsync(GamePid, Game.EmpireEarth);

            Assert.That(outcome, Is.EqualTo(ActivationOutcome.UserSwitched));
            Assert.That(windows.SetForegroundCalls, Is.Empty);
            Assert.That(logger.Messages, Has.Some.Contains("user switched to no process"));
        }

        [Test]
        public async Task ThePlayerSwitchesAfterTheHandOver_TheLookAfterTwoSecondsLeavesItAlone()
        {
            onWait = time =>
            {
                if (time == GameWindowActivator.RecheckInterval)
                    windows.Foreground = OtherPid;
            };

            ActivationOutcome outcome = await activator.ActivateAsync(GamePid, Game.EmpireEarth);

            Assert.That(outcome, Is.EqualTo(ActivationOutcome.UserSwitched));
            Assert.That(windows.SetForegroundCalls, Has.Count.EqualTo(1), "no second hand-over");
            Assert.That(logger.Messages, Has.Some.Contains("The foreground belongs to pid 777 now: skipped, user switched"));
        }

        [Test]
        public async Task TheLauncherTakesTheForegroundBack_TheWindowGetsItAgain()
        {
            int rechecks = 0;
            onWait = time =>
            {
                if (time == GameWindowActivator.RecheckInterval && ++rechecks == 1)
                    windows.Foreground = LauncherPid;
            };

            ActivationOutcome outcome = await activator.ActivateAsync(GamePid, Game.EmpireEarth);

            Assert.That(outcome, Is.EqualTo(ActivationOutcome.GameInForeground));
            Assert.That(windows.SetForegroundCalls, Has.Count.EqualTo(2));
            Assert.That(logger.Messages, Has.Some.Contains("brought to the foreground again (1 of 3)"));
        }

        [Test]
        public async Task TheLauncherKeepsTakingItBack_TheHandOverIsLimitedToThreeTimes()
        {
            onWait = time =>
            {
                if (time == GameWindowActivator.RecheckInterval)
                    windows.Foreground = LauncherPid;
            };

            ActivationOutcome outcome = await activator.ActivateAsync(GamePid, Game.EmpireEarth);

            Assert.That(outcome, Is.EqualTo(ActivationOutcome.GaveUp));
            Assert.That(windows.SetForegroundCalls, Has.Count.EqualTo(1 + GameWindowActivator.MaxReactivations));
            Assert.That(logger.Entries.Last().Level, Is.EqualTo(LogLevel.Warning));
            Assert.That(logger.Entries.Last().Message, Does.Contain("after 3 hand-overs").And.Contain("giving up"));
        }

        [Test]
        public async Task WindowsRefusesTheForeground_ItIsLoggedAndTriedAgainThreeTimes()
        {
            windows.SetForegroundResult = false;

            ActivationOutcome outcome = await activator.ActivateAsync(GamePid, Game.EmpireEarth);

            Assert.That(outcome, Is.EqualTo(ActivationOutcome.GaveUp));
            Assert.That(windows.SetForegroundCalls, Has.Count.EqualTo(1 + GameWindowActivator.MaxReactivations));
            Assert.That(logger.Messages, Has.Some.Contains("SetForegroundWindow was refused (the foreground is pid 100 (the launcher))"));
            Assert.That(logger.Messages, Has.Some.Contains("SetForegroundWindow was refused again (3 of 3)"));
        }

        [Test]
        public async Task NoWindowWithinTheCap_TheLauncherGivesUpAndChangesNothing()
        {
            windows.LooksWithoutWindow = int.MaxValue;

            ActivationOutcome outcome = await activator.ActivateAsync(GamePid, Game.EmpireEarth);

            Assert.That(outcome, Is.EqualTo(ActivationOutcome.NoWindow));
            Assert.That(windows.SetForegroundCalls, Is.Empty);
            Assert.That(waits, Is.All.EqualTo(GameWindowActivator.PollInterval));
            Assert.That(waits.Count, Is.EqualTo(600), "60 s at one look per 100 ms");
            Assert.That(logger.Messages, Has.Some.EqualTo("Info: No window of Empire Earth.exe (pid 4242) within 60 s; the foreground was left alone."));
        }

        [Test]
        public async Task TheWindowIsGoneAtTheLook_NothingIsHandedOver()
        {
            onWait = time =>
            {
                if (time == GameWindowActivator.RecheckInterval)
                    windows.Window = IntPtr.Zero;
            };

            ActivationOutcome outcome = await activator.ActivateAsync(GamePid, Game.EmpireEarth);

            Assert.That(outcome, Is.EqualTo(ActivationOutcome.NoWindow));
            Assert.That(windows.SetForegroundCalls, Has.Count.EqualTo(1));
            Assert.That(logger.Messages, Has.Some.Contains("is gone; the hand-over of the foreground ends"));
        }

        [Test]
        public async Task WithoutAProcessId_NothingIsPolled()
        {
            ActivationOutcome outcome = await activator.ActivateAsync(null, Game.EmpireEarth);

            Assert.That(outcome, Is.EqualTo(ActivationOutcome.ProcessIdUnknown));
            Assert.That(windows.FindCalls, Is.EqualTo(0));
            Assert.That(waits, Is.Empty);
            Assert.That(logger.Messages, Has.Some.Contains("its process id is not known"));
        }

        [Test]
        public async Task ItRunsOnABackgroundThread()
        {
            // The thread of the test before the first await: afterwards the test may go on on any thread of the pool.
            int callingThread = Environment.CurrentManagedThreadId;

            await activator.ActivateAsync(GamePid, Game.EmpireEarth);

            Assert.That(windows.LastFindThreadId, Is.Not.EqualTo(callingThread));
        }

        [Test]
        public async Task ClosingTheLauncher_EndsThePolling()
        {
            using (var cancellation = new CancellationTokenSource())
            {
                windows.LooksWithoutWindow = int.MaxValue;
                // With the real delay: the cancelled token ends the wait of 100 ms at once.
                var real = new GameWindowActivator(windows, clock, logger, LauncherPid);
                Task<ActivationOutcome> running = real.ActivateAsync(GamePid, Game.EmpireEarth, cancellation.Token);

                cancellation.Cancel();

                Assert.That(await running, Is.EqualTo(ActivationOutcome.Cancelled));
                Assert.That(windows.SetForegroundCalls, Is.Empty);
            }
        }

        [Test]
        public async Task ACancelledTokenBetweenTwoLooks_EndsTheHandOver()
        {
            using (var cancellation = new CancellationTokenSource())
            {
                windows.LooksWithoutWindow = int.MaxValue;
                onWait = time => cancellation.Cancel();

                ActivationOutcome outcome = await activator.ActivateAsync(GamePid, Game.EmpireEarth, cancellation.Token);

                Assert.That(outcome, Is.EqualTo(ActivationOutcome.Cancelled));
                Assert.That(waits, Has.Count.EqualTo(1));
            }
        }

        [Test]
        public async Task AFailingWindowSystem_IsLoggedAndNeverThrown()
        {
            windows.Failure = new InvalidOperationException("boom");

            ActivationOutcome outcome = await activator.ActivateAsync(GamePid, Game.EmpireEarth);

            Assert.That(outcome, Is.EqualTo(ActivationOutcome.GaveUp));
            Assert.That(logger.Entries.Single().Level, Is.EqualTo(LogLevel.Warning));
            Assert.That(logger.Entries.Single().Exception, Is.SameAs(windows.Failure));
        }

        [Test]
        public void TheArguments_AreChecked()
        {
            Assert.That(() => new GameWindowActivator(null, clock, logger, LauncherPid), Throws.ArgumentNullException);
            Assert.That(() => new GameWindowActivator(windows, null, logger, LauncherPid), Throws.ArgumentNullException);
            Assert.That(() => new GameWindowActivator(windows, clock, null, LauncherPid), Throws.ArgumentNullException);
            Assert.That(() => activator.ActivateAsync(GamePid, null), Throws.ArgumentNullException);
        }
    }
}

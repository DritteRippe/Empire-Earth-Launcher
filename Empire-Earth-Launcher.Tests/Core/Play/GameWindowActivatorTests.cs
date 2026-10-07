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
    /// 1.1.0): the main window (not the splash) is found after some looks, the foreground is handed over only while the launcher
    /// (or nobody) owns it, never stolen from another program, not touched if the game owns it already, re-checked after two
    /// seconds, handed over again at most three times, given up after 60 seconds without a window, and then watched for 60
    /// seconds without any change (A1 of 1.1.0; the log lines come from the fake clock).
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
            Assert.That(waits[7], Is.EqualTo(GameWindowActivator.RecheckInterval), "one look again after two seconds");
            Assert.That(windows.AllowCalls, Is.Empty, "the right is granted by the start, not here");
            Assert.That(logger.Messages, Has.Some.EqualTo("Info: Game window 0x1234 of Empire Earth.exe (pid 4242) brought to the foreground " +
                                                          "after 700 ms (the foreground was pid 100 (the launcher))."));
        }

        /// <summary>A1 (b): SetForegroundWindow on a window that is in front wakes the wrapper for nothing; it is not called.</summary>
        [Test]
        public async Task TheGameOwnsTheForegroundAlready_NothingIsDone_AndItIsLogged()
        {
            windows.Foreground = GamePid;

            ActivationOutcome outcome = await activator.ActivateAsync(GamePid, Game.ArtOfConquest);

            Assert.That(outcome, Is.EqualTo(ActivationOutcome.GameInForeground));
            Assert.That(windows.SetForegroundCalls, Is.Empty, "no call: the game is in front");
            Assert.That(logger.Messages, Has.Some.EqualTo("Info: Game window 0x1234 of EE-AOC.exe (pid 4242) found after 0 ms, already in the foreground, nothing to do."));
            Assert.That(logger.Messages, Has.None.Contains("brought to the foreground"));
        }

        /// <summary>A1 (a): the splash of Empire Earth ('Loading Game Window', a tool window) is no main window.</summary>
        [Test]
        public async Task TheSplashOfTheGame_IsSkipped_TheMainWindowGetsTheForeground()
        {
            windows.HasSplash = true;
            windows.LooksWithoutWindow = 4;

            ActivationOutcome outcome = await activator.ActivateAsync(GamePid, Game.EmpireEarth);

            Assert.That(outcome, Is.EqualTo(ActivationOutcome.GameInForeground));
            Assert.That(windows.SetForegroundCalls, Is.EqualTo(new[] { windows.Window }), "the main window, never the splash");
            Assert.That(waits.Take(4), Is.All.EqualTo(GameWindowActivator.PollInterval), "the splash does not end the wait for the window");
            Assert.That(logger.Messages, Has.Some.Contains("Game window 0x1234 of Empire Earth.exe (pid 4242) brought to the foreground after 400 ms"));
            Assert.That(logger.Messages, Has.None.Contains("0x5555"), "the splash is in no log line");
        }

        [Test]
        public async Task OnlyTheSplashExists_NoWindowIsFound_AndTheForegroundIsLeftAlone()
        {
            windows.HasSplash = true;
            windows.LooksWithoutWindow = int.MaxValue;

            ActivationOutcome outcome = await activator.ActivateAsync(GamePid, Game.EmpireEarth);

            Assert.That(outcome, Is.EqualTo(ActivationOutcome.NoWindow));
            Assert.That(windows.SetForegroundCalls, Is.Empty);
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

        /// <summary>
        /// Windows has no foreground window for a moment while a window is created or the display mode switches: that is nobody's
        /// foreground, no player who switched. After three looks, 100 ms apart, the launcher hands the window over.
        /// </summary>
        [Test]
        public async Task NobodyOwnsTheForeground_AfterThreeLooks_TheWindowIsHandedTheForeground()
        {
            windows.Foreground = 0;

            ActivationOutcome outcome = await activator.ActivateAsync(GamePid, Game.EmpireEarth);

            Assert.That(outcome, Is.EqualTo(ActivationOutcome.GameInForeground));
            Assert.That(waits.Take(GameWindowActivator.NullForegroundPolls), Is.All.EqualTo(GameWindowActivator.PollInterval));
            Assert.That(windows.SetForegroundCalls, Is.EqualTo(new[] { windows.Window }));
            Assert.That(logger.Messages, Has.Some.Contains("brought to the foreground after 0 ms (the foreground was no process)"),
                "the time is that of the window; the looks for a foreground window come after it");
            Assert.That(logger.Messages, Has.None.Contains("user switched"));
        }

        [Test]
        public async Task TheForegroundIsMissingForAMoment_ThenAnotherProgramHasIt_NothingIsStolen()
        {
            windows.Foreground = 0;
            onWait = time =>
            {
                if (waits.Count == 2)
                    windows.Foreground = OtherPid;
            };

            ActivationOutcome outcome = await activator.ActivateAsync(GamePid, Game.EmpireEarth);

            Assert.That(outcome, Is.EqualTo(ActivationOutcome.UserSwitched));
            Assert.That(windows.SetForegroundCalls, Is.Empty);
            Assert.That(logger.Messages, Has.Some.Contains("found after 0 ms, not brought to the foreground: skipped, user switched to pid 777."));
        }

        [Test]
        public async Task TheForegroundIsMissingForAMoment_ThenTheLauncherHasIt_TheWindowGetsIt()
        {
            windows.Foreground = 0;
            onWait = time =>
            {
                if (waits.Count == 1)
                    windows.Foreground = LauncherPid;
            };

            ActivationOutcome outcome = await activator.ActivateAsync(GamePid, Game.EmpireEarth);

            Assert.That(outcome, Is.EqualTo(ActivationOutcome.GameInForeground));
            Assert.That(windows.SetForegroundCalls, Has.Count.EqualTo(1));
            Assert.That(logger.Messages, Has.Some.Contains("(the foreground was pid 100 (the launcher))"));
        }

        [Test]
        public async Task TheForegroundIsMissingAtTheLook_NobodyIsTakenForTheLauncher_TheWindowGetsItAgain()
        {
            bool taken = false;
            onWait = time =>
            {
                if (time == GameWindowActivator.RecheckInterval && !taken)
                {
                    taken = true;
                    windows.Foreground = 0;
                }
            };

            ActivationOutcome outcome = await activator.ActivateAsync(GamePid, Game.EmpireEarth);

            Assert.That(outcome, Is.EqualTo(ActivationOutcome.GameInForeground));
            Assert.That(windows.SetForegroundCalls, Has.Count.EqualTo(2));
            Assert.That(logger.Messages, Has.Some.EqualTo("Info: Nobody had the foreground: window 0x1234 of Empire Earth.exe (pid 4242) " +
                                                          "brought to the foreground again (1 of 3)."));
            Assert.That(logger.Messages, Has.None.Contains("user switched"));
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
            var warning = logger.Entries.Single(entry => entry.Level == LogLevel.Warning);
            Assert.That(warning.Message, Does.Contain("after 3 hand-overs").And.Contain("giving up"));
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
        public async Task TheWindowIsGoneAtTheLook_NothingIsHandedOver_AndNothingIsWatched()
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
            Assert.That(logger.Messages, Has.None.Contains("Watch"), "no window, no watch");
        }

        // --- The watch after the hand-over (A1 (c) of 1.1.0): read-only, with the time since the start ---------------------

        private static string WatchLine(string time, string text)
        {
            return "Info: Watch t+" + time + " s: " + text;
        }

        private const string GameWindowText = "window 0x1234 (pid 4242, class 'SSSI Empire Earth')";
        private const string GameRectangleText = "rectangle 0,0,1920,1080 (1920x1080), style 0x16CF0000, exstyle 0x00040008.";

        [Test]
        public async Task AfterTheHandOver_TheWindowIsWatchedFor60Seconds_AndANoiselessWatchLogsTheStartOnly()
        {
            ActivationOutcome outcome = await activator.ActivateAsync(GamePid, Game.EmpireEarth);

            Assert.That(outcome, Is.EqualTo(ActivationOutcome.GameInForeground), "the outcome is that of the hand-over");
            // The hand-over ends with the look after two seconds; the watch starts there and ends 60 seconds later.
            Assert.That(clock.UtcNow - new FakeClock().UtcNow, Is.EqualTo(GameWindowActivator.RecheckInterval + GameWindowActivator.WatchDuration));
            Assert.That(waits.Count(wait => wait == GameWindowActivator.WatchInterval), Is.EqualTo(240), "four looks per second for 60 s");
            Assert.That(logger.Messages, Has.Some.EqualTo("Info: Watching Empire Earth.exe (pid 4242) for 60 s (read-only): changes of the foreground " +
                                                          "window and of the rectangle and the styles of its main window are logged with the time since the start."));
            Assert.That(logger.Messages, Has.Some.EqualTo(WatchLine("2.0", "foreground window is " + GameWindowText + " (the game).")));
            Assert.That(logger.Messages, Has.Some.EqualTo(WatchLine("2.0", "main window 0x1234 (class 'SSSI Empire Earth'): " + GameRectangleText)));
            Assert.That(logger.Messages.Last(), Is.EqualTo("Info: The watch of Empire Earth.exe (pid 4242) ends after 60 s."));
            Assert.That(logger.Messages.Count(message => message.StartsWith("Info: Watch t+", StringComparison.Ordinal)), Is.EqualTo(2),
                "nothing changed: the first state of the foreground and of the window, nothing more");
        }

        [Test]
        public async Task TheWatch_LogsEveryChangeOfTheForegroundWindow_WithTheTimeSinceTheStart()
        {
            DateTime start = clock.UtcNow;
            bool switched = false;
            bool back = false;
            onWait = time =>
            {
                TimeSpan elapsed = clock.UtcNow - start;
                if (!switched && elapsed >= TimeSpan.FromSeconds(10))
                {
                    switched = true;
                    windows.Foreground = OtherPid;
                }
                else if (switched && !back && elapsed >= TimeSpan.FromSeconds(30))
                {
                    back = true;
                    windows.Foreground = GamePid;
                }
            };

            await activator.ActivateAsync(GamePid, Game.EmpireEarth);

            Assert.That(logger.Messages, Has.Some.EqualTo(WatchLine("10.0", "foreground window changed from " + GameWindowText +
                " (the game) to window 0x9309 (pid 777, class 'OtherClass').")));
            Assert.That(logger.Messages, Has.Some.EqualTo(WatchLine("30.0", "foreground window changed from window 0x9309 (pid 777, class " +
                "'OtherClass') to " + GameWindowText + " (the game).")));
        }

        [Test]
        public async Task TheWatch_NamesTheLauncherAndNoWindowInFront()
        {
            DateTime start = clock.UtcNow;
            int step = 0;
            onWait = time =>
            {
                TimeSpan elapsed = clock.UtcNow - start;
                if (step == 0 && elapsed >= TimeSpan.FromSeconds(5))
                {
                    step = 1;
                    windows.Foreground = LauncherPid;
                }
                else if (step == 1 && elapsed >= TimeSpan.FromSeconds(6))
                {
                    step = 2;
                    windows.Foreground = 0;
                }
            };

            await activator.ActivateAsync(GamePid, Game.EmpireEarth);

            Assert.That(logger.Messages, Has.Some.Contains("to window 0x9064 (pid 100, class 'OtherClass') (the launcher)."));
            Assert.That(logger.Messages, Has.Some.Contains("(the launcher) to none."));
        }

        [Test]
        public async Task TheWatch_LogsAChangeOfTheRectangleAndOfTheStyles()
        {
            DateTime start = clock.UtcNow;
            int step = 0;
            onWait = time =>
            {
                TimeSpan elapsed = clock.UtcNow - start;
                if (step == 0 && elapsed >= TimeSpan.FromSeconds(8))
                {
                    step = 1;
                    windows.GameRight = 1280;
                    windows.GameBottom = 720;
                }
                else if (step == 1 && elapsed >= TimeSpan.FromSeconds(9))
                {
                    step = 2;
                    windows.GameStyle = 0x94000000;
                    windows.GameExStyle = 0x00040000;
                }
            };

            await activator.ActivateAsync(GamePid, Game.EmpireEarth);

            Assert.That(logger.Messages, Has.Some.EqualTo(WatchLine("8.0", "main window 0x1234 rectangle changed from 0,0,1920,1080 (1920x1080) " +
                                                                    "to 0,0,1280,720 (1280x720).")));
            Assert.That(logger.Messages, Has.Some.EqualTo(WatchLine("9.0", "main window 0x1234 styles changed from style 0x16CF0000, exstyle 0x00040008 " +
                                                                    "to style 0x94000000, exstyle 0x00040000.")));
            Assert.That(logger.Messages.Count(message => message.Contains("changed from")), Is.EqualTo(2), "one line for each change");
        }

        [Test]
        public async Task TheWatch_ChangesNothing_NoForegroundIsTakenWhileTheLauncherHasItAgain()
        {
            DateTime start = clock.UtcNow;
            onWait = time =>
            {
                // From the watch on the launcher has the foreground again: a hand-over would be due, but the watch only reads.
                if (clock.UtcNow - start >= TimeSpan.FromSeconds(3))
                    windows.Foreground = LauncherPid;
            };

            await activator.ActivateAsync(GamePid, Game.EmpireEarth);

            Assert.That(windows.SetForegroundCalls, Has.Count.EqualTo(1), "only the hand-over itself, before the watch");
            Assert.That(windows.AllowCalls, Is.Empty);
            Assert.That(logger.Messages, Has.Some.Contains("(the launcher)."), "the watch saw the launcher in front");
        }

        [Test]
        public async Task TheWatch_TellsThatTheMainWindowIsGone_Once()
        {
            DateTime start = clock.UtcNow;
            onWait = time =>
            {
                if (clock.UtcNow - start >= TimeSpan.FromSeconds(20))
                    windows.Window = IntPtr.Zero;
            };

            await activator.ActivateAsync(GamePid, Game.EmpireEarth);

            Assert.That(logger.Messages.Count(message => message.Contains("the main window of Empire Earth.exe (pid 4242) is not there.")), Is.EqualTo(1));
            Assert.That(logger.Messages, Has.Some.Contains("Watch t+20.0 s: foreground window changed from " + GameWindowText),
                "the foreground window is gone with it");
        }

        [Test]
        public async Task TheWatchOfASplash_LogsTheMainWindowOnly()
        {
            windows.HasSplash = true;

            await activator.ActivateAsync(GamePid, Game.EmpireEarth);

            Assert.That(logger.Messages, Has.None.Contains("Loading Game Window"));
            Assert.That(logger.Messages, Has.Some.Contains("main window 0x1234 (class 'SSSI Empire Earth')"));
        }

        [Test]
        public async Task TheWatchAfterAGiveUp_StillMeasures_AndTheOutcomeStaysTheHandOvers()
        {
            onWait = time =>
            {
                if (time == GameWindowActivator.RecheckInterval)
                    windows.Foreground = LauncherPid;
            };

            ActivationOutcome outcome = await activator.ActivateAsync(GamePid, Game.EmpireEarth);

            Assert.That(outcome, Is.EqualTo(ActivationOutcome.GaveUp));
            Assert.That(logger.Messages.Last(), Does.StartWith("Info: The watch of Empire Earth.exe (pid 4242) ends"));
        }

        [Test]
        public async Task ClosingTheLauncher_DuringTheWatch_EndsIt()
        {
            using (var cancellation = new CancellationTokenSource())
            {
                onWait = time =>
                {
                    if (time == GameWindowActivator.WatchInterval)
                        cancellation.Cancel();
                };

                ActivationOutcome outcome = await activator.ActivateAsync(GamePid, Game.EmpireEarth, cancellation.Token);

                Assert.That(outcome, Is.EqualTo(ActivationOutcome.Cancelled));
                Assert.That(waits.Count(wait => wait == GameWindowActivator.WatchInterval), Is.EqualTo(1));
            }
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

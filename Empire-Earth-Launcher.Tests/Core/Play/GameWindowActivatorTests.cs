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

        /// <summary>The most waits a start needs (the longest, the 180 s deadline of the signal, takes about 720): more means a deadline is missing.</summary>
        private const int MaxWaits = 1000;

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

        [TearDown]
        public void TearDown()
        {
            Assert.That(waits.Count, Is.LessThan(MaxWaits), "the safety cap of the test delay was reached: a deadline is missing");
        }

        /// <summary>The delay of the activator: no real waiting, the fake clock moves, and the test may change the world.</summary>
        private Task Wait(TimeSpan time, CancellationToken cancellationToken)
        {
            // Without a deadline a watch loops for ever: fail fast instead of hanging the test run.
            if (waits.Count >= MaxWaits)
                throw new InvalidOperationException("The activator waited " + MaxWaits + " times: a deadline is missing.");
            waits.Add(time);
            clock.Advance(time);
            onWait?.Invoke(time);
            return Task.CompletedTask;
        }

        [Test]
        public async Task TheWindowAppearsAfterSomePolls_TheForegroundIsHandedOverOnce()
        {
            windows.LooksWithoutWindow = 7;

            ActivationOutcome outcome = (await activator.ActivateAsync(GamePid, Game.EmpireEarth)).HandOver;

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

            ActivationOutcome outcome = (await activator.ActivateAsync(GamePid, Game.ArtOfConquest)).HandOver;

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

            ActivationOutcome outcome = (await activator.ActivateAsync(GamePid, Game.EmpireEarth)).HandOver;

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

            ActivationOutcome outcome = (await activator.ActivateAsync(GamePid, Game.EmpireEarth)).HandOver;

            Assert.That(outcome, Is.EqualTo(ActivationOutcome.NoWindow));
            Assert.That(windows.SetForegroundCalls, Is.Empty);
        }

        [Test]
        public async Task AnotherProgramOwnsTheForeground_NothingIsStolen()
        {
            windows.Foreground = OtherPid;
            windows.LooksWithoutWindow = 3;

            ActivationOutcome outcome = (await activator.ActivateAsync(GamePid, Game.EmpireEarth)).HandOver;

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

            ActivationOutcome outcome = (await activator.ActivateAsync(GamePid, Game.EmpireEarth)).HandOver;

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

            ActivationOutcome outcome = (await activator.ActivateAsync(GamePid, Game.EmpireEarth)).HandOver;

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

            ActivationOutcome outcome = (await activator.ActivateAsync(GamePid, Game.EmpireEarth)).HandOver;

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

            ActivationOutcome outcome = (await activator.ActivateAsync(GamePid, Game.EmpireEarth)).HandOver;

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

            ActivationOutcome outcome = (await activator.ActivateAsync(GamePid, Game.EmpireEarth)).HandOver;

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

            ActivationOutcome outcome = (await activator.ActivateAsync(GamePid, Game.EmpireEarth)).HandOver;

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

            ActivationOutcome outcome = (await activator.ActivateAsync(GamePid, Game.EmpireEarth)).HandOver;

            Assert.That(outcome, Is.EqualTo(ActivationOutcome.GaveUp));
            Assert.That(windows.SetForegroundCalls, Has.Count.EqualTo(1 + GameWindowActivator.MaxReactivations));
            var warning = logger.Entries.Single(entry => entry.Level == LogLevel.Warning);
            Assert.That(warning.Message, Does.Contain("after 3 hand-overs").And.Contain("giving up"));
        }

        [Test]
        public async Task WindowsRefusesTheForeground_ItIsLoggedAndTriedAgainThreeTimes()
        {
            windows.SetForegroundResult = false;

            ActivationOutcome outcome = (await activator.ActivateAsync(GamePid, Game.EmpireEarth)).HandOver;

            Assert.That(outcome, Is.EqualTo(ActivationOutcome.GaveUp));
            Assert.That(windows.SetForegroundCalls, Has.Count.EqualTo(1 + GameWindowActivator.MaxReactivations));
            Assert.That(logger.Messages, Has.Some.Contains("SetForegroundWindow was refused (the foreground is pid 100 (the launcher))"));
            Assert.That(logger.Messages, Has.Some.Contains("SetForegroundWindow was refused again (3 of 3)"));
        }

        [Test]
        public async Task NoWindowWithinTheCap_TheLauncherGivesUpAndChangesNothing()
        {
            windows.LooksWithoutWindow = int.MaxValue;

            ActivationOutcome outcome = (await activator.ActivateAsync(GamePid, Game.EmpireEarth)).HandOver;

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

            ActivationOutcome outcome = (await activator.ActivateAsync(GamePid, Game.EmpireEarth)).HandOver;

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
        public async Task AfterTheHandOver_TheWindowIsWatchedFor60Seconds_AndANoiselessWatchLogsTheStartTheSignalAndTheEndOnly()
        {
            ActivationOutcome outcome = (await activator.ActivateAsync(GamePid, Game.EmpireEarth)).HandOver;

            Assert.That(outcome, Is.EqualTo(ActivationOutcome.GameInForeground), "the outcome is that of the hand-over");
            // The hand-over ends with the look after two seconds; the watch starts there and ends 60 seconds later.
            Assert.That(clock.UtcNow - new FakeClock().UtcNow, Is.EqualTo(GameWindowActivator.RecheckInterval + GameWindowActivator.WatchDuration));
            Assert.That(waits.Count(wait => wait == GameWindowActivator.WatchInterval), Is.EqualTo(240), "four looks per second for 60 s");
            Assert.That(logger.Messages, Has.Some.EqualTo("Info: Watching Empire Earth.exe (pid 4242) for 60 s (read-only): changes of the foreground " +
                                                          "window and of the rectangle and the styles of its main window are logged with the time since the start."));
            Assert.That(logger.Messages, Has.Some.EqualTo(WatchLine("2.0", "foreground window is " + GameWindowText + " (the game).")));
            Assert.That(logger.Messages, Has.Some.EqualTo(WatchLine("2.0", "main window 0x1234 (class 'SSSI Empire Earth'): " + GameRectangleText)));
            Assert.That(logger.Messages.Last(), Is.EqualTo("Info: The watch of Empire Earth.exe (pid 4242) ends after 60 s."));
            Assert.That(logger.Messages.Where(message => message.StartsWith("Info: Watch t+", StringComparison.Ordinal)).ToList(), Is.EqualTo(new[]
            {
                WatchLine("2.0", "foreground window is " + GameWindowText + " (the game)."),
                WatchLine("2.0", "main window 0x1234 (class 'SSSI Empire Earth'): " + GameRectangleText),
                WatchLine("2.0", "activation signal waits: the main window 0x1234 has rectangle 0,0,1920,1080 (1920x1080), style 0x16CF0000, " +
                                 "exstyle 0x00040008; 5 s without a change are needed."),
                WatchLine("7.0", "activation signal sent: WM_ACTIVATE (WA_ACTIVE) posted to main window 0x1234 (class 'SSSI Empire Earth'), " +
                                 "which was the foreground window with rectangle 0,0,1920,1080 (1920x1080) for 5.0 s."),
            }), "nothing changed: the first state of the foreground and of the window, the wait of the signal and the one post");
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
            Assert.That(windows.PostActivateCalls, Is.Empty, "the launcher was in front: no activation message");
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

            ActivationOutcome outcome = (await activator.ActivateAsync(GamePid, Game.EmpireEarth)).HandOver;

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

                ActivationResult result = await activator.ActivateAsync(GamePid, Game.EmpireEarth, cancellation.Token);

                Assert.That(result.HandOver, Is.EqualTo(ActivationOutcome.Cancelled));
                Assert.That(result.Signal, Is.EqualTo(ActivationSignalOutcome.Cancelled), "the signal was still waiting");
                Assert.That(waits.Count(wait => wait == GameWindowActivator.WatchInterval), Is.EqualTo(1));
                Assert.That(logger.Messages.Last(), Is.EqualTo("Info: The activation signal for Empire Earth.exe (pid 4242) was not sent: the launcher is closing."));
            }
        }

        // --- The activation signal (A1b of 1.1.0): one WM_ACTIVATE to the settled main window in front -----------------------

        private const string SignalPrefix = "Info: Activation signal for Empire Earth.exe (pid 4242): ";

        /// <summary>Remembers when (seconds since the start) the activation message was posted.</summary>
        private List<double> TrackPosts(DateTime start)
        {
            var posted = new List<double>();
            windows.OnCall = call =>
            {
                if (call.StartsWith("post activate", StringComparison.Ordinal))
                    posted.Add((clock.UtcNow - start).TotalSeconds);
            };
            return posted;
        }

        [Test]
        public async Task TheGameInFront_GetsOneActivationSignal_FiveSecondsAfterItsWindowSettled()
        {
            DateTime start = clock.UtcNow;
            List<double> posted = TrackPosts(start);

            ActivationResult result = await activator.ActivateAsync(GamePid, Game.EmpireEarth);

            Assert.That(result.HandOver, Is.EqualTo(ActivationOutcome.GameInForeground));
            Assert.That(result.Signal, Is.EqualTo(ActivationSignalOutcome.Sent));
            Assert.That(windows.PostActivateCalls, Is.EqualTo(new[] { windows.Window }), "one message, to the main window");
            Assert.That(posted, Is.EqualTo(new[] { 7.0 }), "the hand-over ended at 2 s, the window was quiet from then on: 5 s later");
            Assert.That(windows.SetForegroundCalls, Has.Count.EqualTo(1), "nothing is brought to the foreground after the hand-over");
            Assert.That(logger.Messages, Has.Some.EqualTo(SignalPrefix + "armed. One WM_ACTIVATE (WA_ACTIVE) goes to its main window (class 'SSSI Empire Earth') " +
                "once that window has been the foreground window with the same rectangle and styles for 5 s, at the latest 180 s after the start (A1b)."));
            Assert.That(logger.Messages.Count(message => message.Contains("activation signal sent")), Is.EqualTo(1));
        }

        [Test]
        public async Task TheSignal_WaitsForTheLastChangeOfTheWindow()
        {
            DateTime start = clock.UtcNow;
            List<double> posted = TrackPosts(start);
            bool changed = false;
            onWait = time =>
            {
                if (!changed && clock.UtcNow - start >= TimeSpan.FromSeconds(6))
                {
                    changed = true;
                    windows.GameRight = 1280;
                    windows.GameBottom = 720;
                }
            };

            ActivationResult result = await activator.ActivateAsync(GamePid, Game.EmpireEarth);

            Assert.That(result.Signal, Is.EqualTo(ActivationSignalOutcome.Sent));
            Assert.That(posted, Has.Count.EqualTo(1));
            Assert.That(posted[0], Is.GreaterThanOrEqualTo(11.0), "5 s after the change at 6 s, not 5 s after the first look");
            Assert.That(logger.Messages, Has.Some.Contains("activation signal waits: the main window 0x1234 has rectangle 0,0,1280,720 (1280x720)"),
                "a change that restarts the quiet time is logged");
            Assert.That(logger.Messages, Has.Some.Contains("which was the foreground window with rectangle 0,0,1280,720 (1280x720) for 5.0 s."));
        }

        [Test]
        public async Task WhileTheLobbyPopupIsInFront_NoSignal_ThenOneAfterItCloses()
        {
            DateTime start = clock.UtcNow;
            List<double> posted = TrackPosts(start);
            onWait = time =>
            {
                TimeSpan elapsed = clock.UtcNow - start;
                if (elapsed >= TimeSpan.FromSeconds(3) && elapsed < TimeSpan.FromSeconds(20))
                    windows.GameForegroundWindow = FakeWindowSystem.LobbyWindow;
                else
                    windows.GameForegroundWindow = null;
            };

            ActivationResult result = await activator.ActivateAsync(GamePid, Game.EmpireEarth);

            Assert.That(result.Signal, Is.EqualTo(ActivationSignalOutcome.Sent));
            Assert.That(posted, Has.Count.EqualTo(1));
            Assert.That(posted[0], Is.GreaterThanOrEqualTo(25.0), "5 s after the lobby closed at 20 s");
            Assert.That(logger.Messages, Has.Some.Contains("activation signal waits: the foreground window is window 0x7777 (pid 4242, class " +
                "'WONLobbyPopup'), another window of the game."));
            Assert.That(windows.PostActivateCalls, Is.EqualTo(new[] { windows.Window }), "to the main window, never to the lobby");
        }

        [Test]
        public async Task AnotherProgramInFront_NoSignalUntilItIsGone_AndTheWaitIsLoggedOnce()
        {
            DateTime start = clock.UtcNow;
            List<double> posted = TrackPosts(start);
            onWait = time =>
            {
                TimeSpan elapsed = clock.UtcNow - start;
                windows.Foreground = elapsed >= TimeSpan.FromSeconds(3) && elapsed < TimeSpan.FromSeconds(100) ? OtherPid : GamePid;
            };

            ActivationResult result = await activator.ActivateAsync(GamePid, Game.EmpireEarth);

            Assert.That(result.Signal, Is.EqualTo(ActivationSignalOutcome.Sent));
            Assert.That(posted[0], Is.GreaterThanOrEqualTo(105.0));
            Assert.That(logger.Messages.Count(message => message.Contains("another program.")), Is.EqualTo(1), "logged when the reason changes only");
            Assert.That(logger.Messages, Has.Some.Contains("activation signal waits: the foreground window is window 0x9309 (pid 777, class 'OtherClass'), another program."));
            // The watch lasts until the signal is decided: 60 s are long over.
            Assert.That(logger.Messages.Last(), Does.Match(@"The watch of Empire Earth\.exe \(pid 4242\) ends after 10[3-9] s\."));
        }

        [Test]
        public async Task AMinimizedMainWindow_GetsNoSignal_UntilItIsBack()
        {
            DateTime start = clock.UtcNow;
            List<double> posted = TrackPosts(start);
            onWait = time =>
            {
                TimeSpan elapsed = clock.UtcNow - start;
                windows.GameMinimized = elapsed >= TimeSpan.FromSeconds(3) && elapsed < TimeSpan.FromSeconds(30);
            };

            await activator.ActivateAsync(GamePid, Game.EmpireEarth);

            Assert.That(posted, Has.Count.EqualTo(1));
            Assert.That(posted[0], Is.GreaterThanOrEqualTo(35.0));
            Assert.That(logger.Messages, Has.Some.Contains("activation signal waits: the main window is minimized or hidden."));
        }

        [Test]
        public async Task AfterAUserSwitch_TheSignalIsNotArmed()
        {
            onWait = time =>
            {
                if (time == GameWindowActivator.RecheckInterval)
                    windows.Foreground = OtherPid;
            };
            windows.Foreground = LauncherPid;

            ActivationResult result = await activator.ActivateAsync(GamePid, Game.EmpireEarth);

            Assert.That(result.HandOver, Is.EqualTo(ActivationOutcome.UserSwitched));
            Assert.That(result.Signal, Is.EqualTo(ActivationSignalOutcome.NotArmed));
            Assert.That(windows.PostActivateCalls, Is.Empty);
            Assert.That(logger.Messages, Has.Some.EqualTo(SignalPrefix + "not armed, the hand-over ended with UserSwitched (the game is not in front; " +
                "Windows activates it when the player returns to it)."));
            Assert.That(logger.Messages, Has.None.Contains("activation signal waits"));
        }

        [Test]
        public async Task AfterAGiveUp_TheSignalIsNotArmed()
        {
            windows.SetForegroundResult = false;

            ActivationResult result = await activator.ActivateAsync(GamePid, Game.EmpireEarth);

            Assert.That(result.HandOver, Is.EqualTo(ActivationOutcome.GaveUp));
            Assert.That(result.Signal, Is.EqualTo(ActivationSignalOutcome.NotArmed));
            Assert.That(windows.PostActivateCalls, Is.Empty);
            Assert.That(logger.Messages, Has.Some.EqualTo(SignalPrefix + "not armed, the hand-over ended with GaveUp (the launcher kept the " +
                "foreground; a click on the game activates it)."));
        }

        [Test]
        public async Task WithoutAWindow_ThereIsNothingToArm()
        {
            windows.LooksWithoutWindow = int.MaxValue;

            ActivationResult result = await activator.ActivateAsync(GamePid, Game.EmpireEarth);

            Assert.That(result.HandOver, Is.EqualTo(ActivationOutcome.NoWindow));
            Assert.That(result.Signal, Is.EqualTo(ActivationSignalOutcome.NotArmed));
            Assert.That(logger.Messages, Has.None.Contains("Activation signal"));
        }

        [Test]
        public async Task PostMessageRefused_IsAWarningWithTheAltTabHint_AndNothingElseHappens()
        {
            windows.PostActivateError = 5;

            ActivationResult result = await activator.ActivateAsync(GamePid, Game.EmpireEarth);

            Assert.That(result.Signal, Is.EqualTo(ActivationSignalOutcome.SendFailed));
            Assert.That(windows.PostActivateCalls, Has.Count.EqualTo(1), "no retry");
            Assert.That(windows.SetForegroundCalls, Has.Count.EqualTo(1), "nothing else is done to a window");
            var warning = logger.Entries.Single(entry => entry.Level == LogLevel.Warning);
            Assert.That(warning.Message, Does.StartWith("Watch t+7.0 s: activation signal failed: PostMessage(WM_ACTIVATE) to main window 0x1234 returned error 5 ("));
            Assert.That(warning.Message, Does.Contain("The game probably runs as administrator").And.Contain("switch to another window and back once (Alt+Tab)."));
            Assert.That(logger.Messages.Last(), Is.EqualTo("Info: The watch of Empire Earth.exe (pid 4242) ends after 60 s."), "the watch goes on to its end");
            Assert.That(logger.Messages, Has.None.Contains("activation signal sent"));
        }

        [Test]
        public async Task PostMessageRefusedForAnotherReason_HasNoWordAboutAdministratorRights()
        {
            windows.PostActivateError = 8;

            ActivationResult result = await activator.ActivateAsync(GamePid, Game.EmpireEarth);

            Assert.That(result.Signal, Is.EqualTo(ActivationSignalOutcome.SendFailed));
            var warning = logger.Entries.Single(entry => entry.Level == LogLevel.Warning);
            Assert.That(warning.Message, Does.Contain("returned error 8 (").And.Contain("(Alt+Tab)."));
            Assert.That(warning.Message, Does.Not.Contain("administrator"));
        }

        /// <summary>The adapter reports 1400 without calling PostMessage when the window is gone: the log must not say that PostMessage returned it.</summary>
        [Test]
        public async Task AMainWindowThatIsGoneBeforeThePost_IsSaidSo_NotAPostMessageError()
        {
            windows.PostActivateError = 1400;

            ActivationResult result = await activator.ActivateAsync(GamePid, Game.EmpireEarth);

            Assert.That(result.Signal, Is.EqualTo(ActivationSignalOutcome.WindowGone));
            Assert.That(windows.PostActivateCalls, Has.Count.EqualTo(1), "no retry");
            Assert.That(logger.Entries.Where(entry => entry.Level == LogLevel.Warning), Is.Empty, "the game has ended; nothing went wrong");
            Assert.That(logger.Messages, Has.Some.EqualTo(WatchLine("7.0", "activation signal not sent: the main window 0x1234 was gone before the message could be posted.")));
            Assert.That(logger.Messages, Has.None.Contains("PostMessage"));
            Assert.That(logger.Messages, Has.None.Contains("1400"));
        }

        [Test]
        public async Task WithoutAQuietWindow_TheWatchEndsAt180sAfterTheStart_WithTheNotSentLine()
        {
            DateTime start = clock.UtcNow;
            int flips = 0;
            onWait = time =>
            {
                // The rectangle changes every second: the window never rests for 5 s.
                if (time == GameWindowActivator.WatchInterval && (clock.UtcNow - start).TotalSeconds % 1.0 == 0)
                    windows.GameRight = 1900 + (++flips % 2);
            };

            ActivationResult result = await activator.ActivateAsync(GamePid, Game.EmpireEarth);

            Assert.That(result.Signal, Is.EqualTo(ActivationSignalOutcome.NotSettled));
            Assert.That(windows.PostActivateCalls, Is.Empty);
            Assert.That(clock.UtcNow - start, Is.EqualTo(ActivationSignal.Deadline), "the look at 180 s ends it");
            Assert.That(logger.Messages, Has.Some.EqualTo("Info: Watch t+180.0 s: activation signal not sent: the main window of Empire Earth.exe (pid 4242) was not " +
                "the foreground window with the same rectangle and styles for 5 s within 180 s of the start (last: the main window was still changing)."));
        }

        [Test]
        public async Task ClosingTheLauncher_WhileTheSignalWaits_SendsNothing()
        {
            using (var cancellation = new CancellationTokenSource())
            {
                DateTime start = clock.UtcNow;
                onWait = time =>
                {
                    if (clock.UtcNow - start >= TimeSpan.FromSeconds(4))
                        cancellation.Cancel();
                };

                ActivationResult result = await activator.ActivateAsync(GamePid, Game.EmpireEarth, cancellation.Token);

                Assert.That(result.Signal, Is.EqualTo(ActivationSignalOutcome.Cancelled));
                Assert.That(windows.PostActivateCalls, Is.Empty);
                Assert.That(logger.Messages, Has.Some.EqualTo("Info: The activation signal for Empire Earth.exe (pid 4242) was not sent: the launcher is closing."));
            }
        }

        [Test]
        public async Task ClosingTheLauncher_AfterTheSignalWasSent_KeepsItsOutcome()
        {
            using (var cancellation = new CancellationTokenSource())
            {
                DateTime start = clock.UtcNow;
                onWait = time =>
                {
                    if (clock.UtcNow - start >= TimeSpan.FromSeconds(20))
                        cancellation.Cancel();
                };

                ActivationResult result = await activator.ActivateAsync(GamePid, Game.EmpireEarth, cancellation.Token);

                Assert.That(result.HandOver, Is.EqualTo(ActivationOutcome.Cancelled));
                Assert.That(result.Signal, Is.EqualTo(ActivationSignalOutcome.Sent));
                Assert.That(logger.Messages, Has.None.Contains("launcher is closing"), "nothing was pending");
            }
        }

        [Test]
        public async Task TheWindowGone_EndsTheSignalAfter10s()
        {
            DateTime start = clock.UtcNow;
            onWait = time =>
            {
                if (clock.UtcNow - start >= TimeSpan.FromSeconds(3))
                    windows.Window = IntPtr.Zero;
            };

            ActivationResult result = await activator.ActivateAsync(GamePid, Game.EmpireEarth);

            Assert.That(result.Signal, Is.EqualTo(ActivationSignalOutcome.WindowGone));
            Assert.That(windows.PostActivateCalls, Is.Empty);
            Assert.That(logger.Messages, Has.Some.Contains("activation signal not sent: the main window of Empire Earth.exe (pid 4242) has not been there for 10 s."));
        }

        [Test]
        public async Task AWindowThatComesBackWithin10s_IsNotGone()
        {
            DateTime start = clock.UtcNow;
            IntPtr main = windows.Window;
            onWait = time =>
            {
                TimeSpan elapsed = clock.UtcNow - start;
                windows.Window = elapsed >= TimeSpan.FromSeconds(3) && elapsed < TimeSpan.FromSeconds(11) ? IntPtr.Zero : main;
            };

            ActivationResult result = await activator.ActivateAsync(GamePid, Game.EmpireEarth);

            Assert.That(result.Signal, Is.EqualTo(ActivationSignalOutcome.Sent));
            Assert.That(windows.PostActivateCalls, Is.EqualTo(new[] { main }));
        }

        [Test]
        public async Task TheWatch_LastsAtLeast60s_AlsoWhenTheSignalWasSentEarly()
        {
            DateTime start = clock.UtcNow;
            List<double> posted = TrackPosts(start);

            await activator.ActivateAsync(GamePid, Game.EmpireEarth);

            Assert.That(posted, Is.EqualTo(new[] { 7.0 }));
            Assert.That(clock.UtcNow - start, Is.EqualTo(GameWindowActivator.RecheckInterval + GameWindowActivator.WatchDuration));
            Assert.That(logger.Messages.Last(), Is.EqualTo("Info: The watch of Empire Earth.exe (pid 4242) ends after 60 s."));
        }

        [Test]
        public async Task TheSignal_IsSentAtMostOncePerStart_EvenWhenTheWindowChangesAfterwards()
        {
            DateTime start = clock.UtcNow;
            List<double> posted = TrackPosts(start);
            onWait = time =>
            {
                if (clock.UtcNow - start >= TimeSpan.FromSeconds(10))
                    windows.GameRight = 1000 + (int)(clock.UtcNow - start).TotalSeconds;
            };

            await activator.ActivateAsync(GamePid, Game.EmpireEarth);

            Assert.That(posted, Is.EqualTo(new[] { 7.0 }));
            Assert.That(windows.PostActivateCalls, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task TheSignal_NeverChangesAWindow_OnlyTheOneMessageIsPosted()
        {
            await activator.ActivateAsync(GamePid, Game.EmpireEarth);

            Assert.That(windows.SetForegroundCalls, Has.Count.EqualTo(1), "the hand-over, nothing after it");
            Assert.That(windows.AllowCalls, Is.Empty);
            Assert.That(windows.PostActivateCalls, Has.Count.EqualTo(1));
        }

        // --- The check right before the post, the identity of the main window, failures and the clock -------------------------

        /// <summary>Calls <paramref name="action"/> once, at the first call of the window system named <paramref name="call"/> from <paramref name="after"/> seconds on.</summary>
        private void OnceAtCall(string call, double after, DateTime start, Action action)
        {
            bool done = false;
            windows.OnCall = name =>
            {
                if (!done && name.StartsWith(call, StringComparison.Ordinal) && (clock.UtcNow - start).TotalSeconds >= after)
                {
                    done = true;
                    action();
                }
            };
        }

        [Test]
        public async Task TheForegroundChangesBetweenTheLookAndThePost_NothingIsPosted_UntilTheMainWindowHasBeenQuietAgain()
        {
            DateTime start = clock.UtcNow;
            List<double> posted = new List<double>();
            bool switched = false;
            windows.OnCall = call =>
            {
                TimeSpan elapsed = clock.UtcNow - start;
                // The look at 7.0 s has read the foreground (the game) and decided to send; the player switches before the post.
                if (!switched && call.StartsWith("responding", StringComparison.Ordinal) && elapsed >= TimeSpan.FromSeconds(7))
                {
                    switched = true;
                    windows.Foreground = OtherPid;
                }
                if (call.StartsWith("post activate", StringComparison.Ordinal))
                    posted.Add(elapsed.TotalSeconds);
            };
            onWait = time =>
            {
                if (clock.UtcNow - start >= TimeSpan.FromSeconds(20))
                    windows.Foreground = GamePid;
            };

            ActivationResult result = await activator.ActivateAsync(GamePid, Game.EmpireEarth);

            Assert.That(result.Signal, Is.EqualTo(ActivationSignalOutcome.Sent));
            Assert.That(posted, Has.Count.EqualTo(1));
            Assert.That(posted[0], Is.GreaterThanOrEqualTo(25.0), "5 s after the game was back in front at 20 s");
            Assert.That(logger.Messages, Has.Some.Contains(WatchLine("7.0", "activation signal held back right before the post: the foreground window is window 0x9309 " +
                "(pid 777, class 'OtherClass'), another program; the quiet time starts again.")));
        }

        [Test]
        public async Task AWindowOfTheGameInFrontBeforeThePost_HoldsTheSignalBack_Too()
        {
            DateTime start = clock.UtcNow;
            OnceAtCall("responding", 7, start, () => windows.GameForegroundWindow = FakeWindowSystem.LobbyWindow);
            onWait = time =>
            {
                if (clock.UtcNow - start >= TimeSpan.FromSeconds(20))
                    windows.GameForegroundWindow = null;
            };

            ActivationResult result = await activator.ActivateAsync(GamePid, Game.EmpireEarth);

            Assert.That(result.Signal, Is.EqualTo(ActivationSignalOutcome.Sent));
            Assert.That(windows.PostActivateCalls, Is.EqualTo(new[] { windows.Window }));
            Assert.That(logger.Messages, Has.Some.Contains("activation signal held back right before the post: the foreground window is window 0x7777 " +
                "(pid 4242, class 'WONLobbyPopup'), another window of the game"));
        }

        [Test]
        public async Task NoForegroundWindowBeforeThePost_HoldsTheSignalBack()
        {
            DateTime start = clock.UtcNow;
            OnceAtCall("responding", 7, start, () => windows.Foreground = 0);
            onWait = time =>
            {
                if (clock.UtcNow - start >= TimeSpan.FromSeconds(12))
                    windows.Foreground = GamePid;
            };

            ActivationResult result = await activator.ActivateAsync(GamePid, Game.EmpireEarth);

            Assert.That(result.Signal, Is.EqualTo(ActivationSignalOutcome.Sent));
            Assert.That(logger.Messages, Has.Some.Contains("activation signal held back right before the post: no window is in the foreground"));
        }

        [Test]
        public async Task TheLauncherClosesBetweenTheLookAndThePost_NothingIsPosted()
        {
            using (var cancellation = new CancellationTokenSource())
            {
                DateTime start = clock.UtcNow;
                // The look at 7.0 s decides to send; the launcher is closing before the post.
                OnceAtCall("responding", 7, start, cancellation.Cancel);

                ActivationResult result = await activator.ActivateAsync(GamePid, Game.EmpireEarth, cancellation.Token);

                Assert.That(result.Signal, Is.EqualTo(ActivationSignalOutcome.Cancelled));
                Assert.That(windows.PostActivateCalls, Is.Empty);
                Assert.That(logger.Messages.Last(), Is.EqualTo("Info: The activation signal for Empire Earth.exe (pid 4242) was not sent: the launcher is closing."));
            }
        }

        [Test]
        public async Task AMainWindowThatDoesNotRespond_GetsNoSignal_UntilItResponds()
        {
            DateTime start = clock.UtcNow;
            List<double> posted = TrackPosts(start);
            onWait = time =>
            {
                TimeSpan elapsed = clock.UtcNow - start;
                windows.GameHung = elapsed >= TimeSpan.FromSeconds(3) && elapsed < TimeSpan.FromSeconds(30);
            };

            ActivationResult result = await activator.ActivateAsync(GamePid, Game.EmpireEarth);

            Assert.That(result.Signal, Is.EqualTo(ActivationSignalOutcome.Sent));
            Assert.That(posted, Has.Count.EqualTo(1));
            Assert.That(posted[0], Is.GreaterThanOrEqualTo(35.0), "5 s after it responded again at 30 s");
            Assert.That(logger.Messages, Has.Some.Contains("activation signal waits: the main window does not respond."));
        }

        [Test]
        public async Task AMainWindowThatStopsRespondingBeforeThePost_HoldsTheSignalBack()
        {
            DateTime start = clock.UtcNow;
            int responding = 0;
            windows.OnCall = call =>
            {
                // The first call at 7 s is the look, the second the check right before the post.
                if (call.StartsWith("responding", StringComparison.Ordinal) && clock.UtcNow - start >= TimeSpan.FromSeconds(7) && ++responding == 2)
                    windows.GameHung = true;
            };
            onWait = time =>
            {
                if (clock.UtcNow - start >= TimeSpan.FromSeconds(20))
                    windows.GameHung = false;
            };

            ActivationResult result = await activator.ActivateAsync(GamePid, Game.EmpireEarth);

            Assert.That(result.Signal, Is.EqualTo(ActivationSignalOutcome.Sent));
            Assert.That(windows.PostActivateCalls, Has.Count.EqualTo(1));
            Assert.That(logger.Messages, Has.Some.Contains("activation signal held back right before the post: the main window does not respond; the quiet time starts again."));
        }

        [Test]
        public async Task AMainWindowOfAnotherClassAfterTheArming_IsNotTheMainWindow_NoSignalGoesToIt()
        {
            DateTime start = clock.UtcNow;
            onWait = time =>
            {
                if (clock.UtcNow - start >= TimeSpan.FromSeconds(3))
                    windows.GameClass = "SomeOtherClass";
            };

            ActivationResult result = await activator.ActivateAsync(GamePid, Game.EmpireEarth);

            Assert.That(result.Signal, Is.EqualTo(ActivationSignalOutcome.WindowGone));
            Assert.That(windows.PostActivateCalls, Is.Empty);
            Assert.That(logger.Messages, Has.Some.Contains("activation signal not sent: the main window of Empire Earth.exe (pid 4242) has not been there for 10 s."));
        }

        [Test]
        public async Task AMainWindowWhoseClassCannotBeRead_ArmsNothing()
        {
            windows.GameClass = string.Empty;

            ActivationResult result = await activator.ActivateAsync(GamePid, Game.EmpireEarth);

            Assert.That(result.HandOver, Is.EqualTo(ActivationOutcome.GameInForeground));
            Assert.That(result.Signal, Is.EqualTo(ActivationSignalOutcome.NotArmed));
            Assert.That(windows.PostActivateCalls, Is.Empty);
            Assert.That(logger.Messages, Has.Some.StartWith(SignalPrefix + "not armed, the class of the main window 0x1234 could not be read"));
        }

        [Test]
        public async Task AWatchThatFailsAfterTheHandOver_KeepsTheOutcomeOfTheHandOver_AndSaysTheSignalWasNotSent()
        {
            DateTime start = clock.UtcNow;
            onWait = time =>
            {
                if (clock.UtcNow - start >= TimeSpan.FromSeconds(4))
                    windows.Failure = new InvalidOperationException("boom");
            };

            ActivationResult result = await activator.ActivateAsync(GamePid, Game.EmpireEarth);

            Assert.That(result.HandOver, Is.EqualTo(ActivationOutcome.GameInForeground), "the hand-over had succeeded");
            Assert.That(result.Signal, Is.EqualTo(ActivationSignalOutcome.WatchFailed));
            Assert.That(windows.PostActivateCalls, Is.Empty);
            var warning = logger.Entries.Single(entry => entry.Level == LogLevel.Warning);
            Assert.That(warning.Message, Is.EqualTo("The watch of Empire Earth.exe (pid 4242) failed after the hand-over (GameInForeground)."));
            Assert.That(warning.Exception, Is.SameAs(windows.Failure));
            Assert.That(logger.Messages, Has.None.Contains("could not be handed"));
            Assert.That(logger.Messages.Last(), Is.EqualTo("Info: The activation signal for Empire Earth.exe (pid 4242) was not sent: the watch failed."));
        }

        [Test]
        public async Task AWatchThatFailsAfterTheSignalWasSent_KeepsTheOutcomeSent()
        {
            DateTime start = clock.UtcNow;
            onWait = time =>
            {
                if (clock.UtcNow - start >= TimeSpan.FromSeconds(10))
                    windows.Failure = new InvalidOperationException("boom");
            };

            ActivationResult result = await activator.ActivateAsync(GamePid, Game.EmpireEarth);

            Assert.That(result.HandOver, Is.EqualTo(ActivationOutcome.GameInForeground));
            Assert.That(result.Signal, Is.EqualTo(ActivationSignalOutcome.Sent));
            Assert.That(logger.Messages, Has.None.Contains("watch failed."));
        }

        [Test]
        public async Task AChangeOfTheWallClock_DoesNotMoveTheQuietTimeOrTheDeadline()
        {
            DateTime start = clock.UtcNow;
            TimeSpan startElapsed = clock.Elapsed;
            var posted = new List<double>();
            windows.OnCall = call =>
            {
                if (call.StartsWith("post activate", StringComparison.Ordinal))
                    posted.Add((clock.Elapsed - startElapsed).TotalSeconds);
            };
            bool set = false;
            onWait = time =>
            {
                // At 3 s the computer's clock jumps forward by an hour (time server, daylight saving).
                if (!set && clock.Elapsed - startElapsed >= TimeSpan.FromSeconds(3))
                {
                    set = true;
                    clock.SetWallClock(TimeSpan.FromHours(1));
                }
            };

            ActivationResult result = await activator.ActivateAsync(GamePid, Game.EmpireEarth);

            Assert.That(set);
            Assert.That(result.Signal, Is.EqualTo(ActivationSignalOutcome.Sent));
            Assert.That(posted, Is.EqualTo(new[] { 7.0 }), "5 s of quiet time have to pass, however the clock was set");
        }

        [Test]
        public async Task AChangeOfTheWallClock_DoesNotEndTheWaitBefore180Seconds()
        {
            TimeSpan startElapsed = clock.Elapsed;
            int flips = 0;
            bool set = false;
            onWait = time =>
            {
                TimeSpan elapsed = clock.Elapsed - startElapsed;
                if (time == GameWindowActivator.WatchInterval && elapsed.TotalSeconds % 1.0 == 0)
                    windows.GameRight = 1900 + (++flips % 2);
                if (!set && elapsed >= TimeSpan.FromSeconds(50))
                {
                    set = true;
                    clock.SetWallClock(TimeSpan.FromHours(1));
                }
            };

            ActivationResult result = await activator.ActivateAsync(GamePid, Game.EmpireEarth);

            Assert.That(set);
            Assert.That(result.Signal, Is.EqualTo(ActivationSignalOutcome.NotSettled));
            Assert.That(clock.Elapsed - startElapsed, Is.EqualTo(ActivationSignal.Deadline), "180 s of elapsed time, not of the wall clock");
            Assert.That(logger.Messages, Has.Some.StartWith("Info: Watch t+180.0 s: activation signal not sent"));
        }

        [Test]
        public async Task AChangeOfTheWallClock_DoesNotShortenTheSearchForTheWindow()
        {
            windows.LooksWithoutWindow = int.MaxValue;
            onWait = time =>
            {
                if (waits.Count == 10)
                    clock.SetWallClock(TimeSpan.FromHours(1));
            };

            ActivationOutcome outcome = (await activator.ActivateAsync(GamePid, Game.EmpireEarth)).HandOver;

            Assert.That(outcome, Is.EqualTo(ActivationOutcome.NoWindow));
            Assert.That(waits.Count, Is.EqualTo(600), "60 s of elapsed time at one look per 100 ms");
        }

        /// <summary>The safety cap of the test delay itself: a loop without end fails the test instead of hanging the run.</summary>
        [Test]
        public void TheTestDelay_StopsALoopWithoutEnd()
        {
            var thrown = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                for (int i = 0; i <= MaxWaits; i++)
                    await Wait(TimeSpan.FromMilliseconds(1), CancellationToken.None);
            });

            Assert.That(thrown.Message, Does.Contain("a deadline is missing"));
            waits.Clear();
        }

        [Test]
        public async Task WithoutAProcessId_NothingIsPolled()
        {
            ActivationOutcome outcome = (await activator.ActivateAsync(null, Game.EmpireEarth)).HandOver;

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
                Task<ActivationResult> running = real.ActivateAsync(GamePid, Game.EmpireEarth, cancellation.Token);

                cancellation.Cancel();

                Assert.That((await running).HandOver, Is.EqualTo(ActivationOutcome.Cancelled));
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

                ActivationOutcome outcome = (await activator.ActivateAsync(GamePid, Game.EmpireEarth, cancellation.Token)).HandOver;

                Assert.That(outcome, Is.EqualTo(ActivationOutcome.Cancelled));
                Assert.That(waits, Has.Count.EqualTo(1));
            }
        }

        [Test]
        public async Task AFailingWindowSystem_IsLoggedAndNeverThrown()
        {
            windows.Failure = new InvalidOperationException("boom");

            ActivationOutcome outcome = (await activator.ActivateAsync(GamePid, Game.EmpireEarth)).HandOver;

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

using System;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Core.Play;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.Play
{
    /// <summary>
    /// <see cref="ActivationSignal"/>, the decision of A1b (ADR 0010 amendment of 1.1.0): the one activation message goes to the
    /// main window of a started game after it has been the foreground window, with the same rectangle and styles and not
    /// minimized and responding, for five seconds, never while another window is in front, never once the player has switched
    /// to another program, never 180 seconds or later after the start, and never twice. The class decides from the windows it
    /// is shown and a time since the start; no window is touched here.
    /// </summary>
    [TestFixture]
    public class ActivationSignalTests
    {
        private const int GamePid = 4242;
        private const int LauncherPid = 100;
        private const long VisibleStyle = 0x16CF0000;
        private const string MainClass = "SSSI Empire Earth";

        private ActivationSignal signal;

        [SetUp]
        public void SetUp()
        {
            signal = new ActivationSignal(GamePid, LauncherPid, MainClass);
        }

        private static TimeSpan At(double seconds)
        {
            return TimeSpan.FromSeconds(seconds);
        }

        private static WindowState Main(int right = 1920, int bottom = 1200, long style = VisibleStyle, long exStyle = 0x00040008, int handle = 0x1234,
            int processId = GamePid, string className = MainClass)
        {
            return new WindowState(new IntPtr(handle), processId, className, 0, 0, right, bottom, style, exStyle);
        }

        private static WindowState Lobby()
        {
            return new WindowState(new IntPtr(0x7777), GamePid, "WONLobbyPopup", 200, 100, 1000, 700, 0x94CA0000, 0x100);
        }

        private static WindowState Splash()
        {
            return new WindowState(new IntPtr(0x5555), GamePid, "Loading Game Window", 660, 440, 1260, 640, 0x90000000, 0x88);
        }

        private static WindowState Other(int processId, string className = "Notepad")
        {
            return new WindowState(new IntPtr(0x9000 + processId), processId, className, 100, 100, 900, 700, 0x14CF0000, 0x100);
        }

        /// <summary>Looks every 250 ms from <paramref name="from"/> up to and including <paramref name="to"/> seconds; returns the first step that is not Wait.</summary>
        private SignalStep LookUntil(double from, double to, WindowState foreground, WindowState main, out double at, bool responding = true)
        {
            for (double t = from; t <= to + 0.0001; t += 0.25)
            {
                SignalStep step = signal.Observe(At(t), foreground, main, responding);
                if (step != SignalStep.Wait)
                {
                    at = t;
                    return step;
                }
            }
            at = double.NaN;
            return SignalStep.Wait;
        }

        [Test]
        public void ASettledMainWindowInFront_GetsTheSignalAfterFiveSeconds_AndNotBefore()
        {
            WindowState main = Main();

            SignalStep before = LookUntil(10.0, 14.75, main, main, out double none);
            Assert.That(before, Is.EqualTo(SignalStep.Wait), "not before 5 s");
            Assert.That(double.IsNaN(none));
            Assert.That(signal.Wait, Is.EqualTo(SignalWait.Settling));
            Assert.That(signal.QuietState, Is.SameAs(main));
            Assert.That(signal.QuietFor, Is.EqualTo(At(4.75)));

            SignalStep step = signal.Observe(At(15.0), main, main);

            Assert.That(step, Is.EqualTo(SignalStep.Send));
            Assert.That(signal.QuietFor, Is.EqualTo(ActivationSignal.SettleTime));
            Assert.That(signal.Wait, Is.EqualTo(SignalWait.None));
            Assert.That(signal.IsPending, Is.True, "the caller completes it after the post");
            Assert.That(signal.Outcome, Is.EqualTo(ActivationSignalOutcome.NotArmed));
        }

        [Test]
        public void ALookThatSends_CannotSendAgain_EvenWithoutComplete()
        {
            WindowState main = Main();
            LookUntil(0, 5.0, main, main, out double at);
            Assert.That(at, Is.EqualTo(5.0));

            Assert.That(signal.Observe(At(5.25), main, main), Is.EqualTo(SignalStep.Wait));
            Assert.That(signal.Observe(At(60), main, main), Is.EqualTo(SignalStep.Wait));
        }

        [Test]
        public void AChangeOfTheRectangle_StartsTheQuietTimeAgain()
        {
            LookUntil(0, 3.0, Main(), Main(), out double none);
            WindowState changed = Main(right: 1280, bottom: 720);

            Assert.That(signal.Observe(At(3.25), changed, changed), Is.EqualTo(SignalStep.Wait));
            Assert.That(signal.QuietState, Is.SameAs(changed));
            Assert.That(signal.QuietFor, Is.EqualTo(TimeSpan.Zero));

            Assert.That(LookUntil(3.5, 8.0, changed, changed, out none), Is.EqualTo(SignalStep.Wait), "5 s after the change, not after the first look");
            Assert.That(signal.Observe(At(8.25), changed, changed), Is.EqualTo(SignalStep.Send));
        }

        [Test]
        public void AChangeOfTheStyles_StartsTheQuietTimeAgain()
        {
            LookUntil(0, 4.0, Main(), Main(), out double none);
            WindowState restyled = Main(style: 0x94000000 | 0x10000000, exStyle: 0x00040000);

            Assert.That(signal.Observe(At(4.25), restyled, restyled), Is.EqualTo(SignalStep.Wait));
            Assert.That(LookUntil(4.5, 9.0, restyled, restyled, out none), Is.EqualTo(SignalStep.Wait));
            Assert.That(signal.Observe(At(9.25), restyled, restyled), Is.EqualTo(SignalStep.Send));
        }

        [Test]
        public void ANewMainWindowHandle_StartsTheQuietTimeAgain()
        {
            LookUntil(0, 4.0, Main(), Main(), out double none);
            WindowState another = Main(handle: 0x4321);

            Assert.That(signal.Observe(At(4.25), another, another), Is.EqualTo(SignalStep.Wait));
            Assert.That(LookUntil(4.5, 9.0, another, another, out none), Is.EqualTo(SignalStep.Wait));
            Assert.That(signal.Observe(At(9.25), another, another), Is.EqualTo(SignalStep.Send));
        }

        [Test]
        public void TheLobbyPopupInFront_NeverSends_AndTheQuietTimeStartsWhenTheMainWindowIsBack()
        {
            WindowState main = Main();

            SignalStep inLobby = LookUntil(0, 20.0, Lobby(), main, out double none);

            Assert.That(inLobby, Is.EqualTo(SignalStep.Wait), "20 s with the lobby in front");
            Assert.That(signal.Wait, Is.EqualTo(SignalWait.OtherWindowOfTheGameInFront));
            Assert.That(signal.QuietState, Is.Null);
            Assert.That(LookUntil(20.25, 25.0, main, main, out none), Is.EqualTo(SignalStep.Wait), "the quiet time started when the lobby closed");
            Assert.That(signal.Observe(At(25.25), main, main), Is.EqualTo(SignalStep.Send));
        }

        [Test]
        public void ALobbyThatOpensInTheLastSecond_StopsTheQuietTime()
        {
            WindowState main = Main();
            LookUntil(0, 4.75, main, main, out double none);

            Assert.That(signal.Observe(At(5.0), Lobby(), main), Is.EqualTo(SignalStep.Wait), "the lobby is in front at 5.0 s");
            Assert.That(signal.Wait, Is.EqualTo(SignalWait.OtherWindowOfTheGameInFront));
        }

        [Test]
        public void TheSplashInFront_Waits()
        {
            Assert.That(LookUntil(0, 30.0, Splash(), Main(), out double none), Is.EqualTo(SignalStep.Wait));
            Assert.That(signal.Wait, Is.EqualTo(SignalWait.OtherWindowOfTheGameInFront));
        }

        /// <summary>The launcher may hold the foreground for a moment while the game takes it: no switch, the signal keeps waiting.</summary>
        [Test]
        public void TheLauncherInFront_Waits_ItIsNoSwitchToAnotherProgram()
        {
            Assert.That(LookUntil(0, 30.0, Other(LauncherPid, "WindowsForms10.Window.8.app"), Main(), out double none), Is.EqualTo(SignalStep.Wait));
            Assert.That(signal.Wait, Is.EqualTo(SignalWait.LauncherInFront));
            Assert.That(signal.IsPending, Is.True);
        }

        /// <summary>The launcher in front, then the main window for 5 s: the signal still goes out (the launcher is no switch).</summary>
        [Test]
        public void TheLauncherInFrontAndThenTheMainWindow_StillGetsTheSignal()
        {
            WindowState main = Main();
            Assert.That(LookUntil(0, 4.0, Other(LauncherPid), main, out double none), Is.EqualTo(SignalStep.Wait));

            Assert.That(LookUntil(4.25, 9.0, main, main, out none), Is.EqualTo(SignalStep.Wait), "the quiet time starts when the main window is in front");
            Assert.That(signal.Observe(At(9.25), main, main), Is.EqualTo(SignalStep.Send));
        }

        /// <summary>The player left for another program: the signal ends at once, also if the main window is not there or minimized.</summary>
        [Test]
        public void AnotherProgramInFront_EndsTheSignal_AsPlayerSwitched()
        {
            Assert.That(signal.Observe(At(10.0), Other(777), Main()), Is.EqualTo(SignalStep.PlayerSwitched));
            Assert.That(signal.Wait, Is.EqualTo(SignalWait.None), "decided, not waiting");
        }

        [Test]
        public void AnotherProgramInFront_WithoutAMainWindow_EndsTheSignal()
        {
            Assert.That(signal.Observe(At(10.0), Other(777), null), Is.EqualTo(SignalStep.PlayerSwitched));
        }

        [Test]
        public void AnotherProgramInFront_WithAMinimizedMainWindow_EndsTheSignal()
        {
            Assert.That(signal.Observe(At(10.0), Other(777), Main(style: VisibleStyle | 0x20000000L)), Is.EqualTo(SignalStep.PlayerSwitched));
        }

        /// <summary>
        /// The finding of the review: after the signal is armed the player switches to another program and comes back within
        /// the 180 s. The return is a real activation by Windows, so the main window in front for 5 s afterwards gets no signal.
        /// </summary>
        [Test]
        public void SwitchedToAnotherProgramAndBackToTheMainWindow_NeverSends()
        {
            WindowState main = Main();
            Assert.That(LookUntil(10.0, 12.0, main, main, out double none), Is.EqualTo(SignalStep.Wait), "settling");
            Assert.That(signal.Observe(At(12.25), Other(777), main), Is.EqualTo(SignalStep.PlayerSwitched));

            // the caller completes the signal; even without that no later look decides again
            Assert.That(LookUntil(12.5, 30.0, main, main, out none), Is.EqualTo(SignalStep.Wait), "5 s and more with the main window back in front");
            Assert.That(double.IsNaN(none));
            signal.Complete(ActivationSignalOutcome.PlayerSwitched);
            Assert.That(signal.Outcome, Is.EqualTo(ActivationSignalOutcome.PlayerSwitched));
            Assert.That(signal.IsPending, Is.False);
            Assert.That(LookUntil(30.25, 60.0, main, main, out none), Is.EqualTo(SignalStep.Wait));
        }

        /// <summary>A switch to another program for one look is enough: it is not undone by the next look.</summary>
        [Test]
        public void ASwitchOfOneLook_IsEnough()
        {
            WindowState main = Main();
            Assert.That(signal.Observe(At(20.0), Other(777), main), Is.EqualTo(SignalStep.PlayerSwitched));

            Assert.That(signal.Observe(At(20.25), main, main), Is.EqualTo(SignalStep.Wait));
            Assert.That(signal.Observe(At(25.25), main, main), Is.EqualTo(SignalStep.Wait), "no Send 5 s later");
        }

        [Test]
        public void TheLobbyAndTheSplash_AreNoSwitch_TheyAreWindowsOfTheGame()
        {
            WindowState main = Main();
            Assert.That(LookUntil(0, 5.0, Lobby(), main, out double none), Is.EqualTo(SignalStep.Wait));
            Assert.That(LookUntil(5.25, 10.0, Splash(), main, out none), Is.EqualTo(SignalStep.Wait));
            Assert.That(LookUntil(10.25, 15.0, main, main, out none), Is.EqualTo(SignalStep.Wait));
            Assert.That(signal.Observe(At(15.25), main, main), Is.EqualTo(SignalStep.Send));
        }

        /// <summary>A window whose process cannot be read (id 0, the window is gone) is as good as none.</summary>
        [Test]
        public void AForegroundWindowOfNoProcess_IsNoSwitch()
        {
            WindowState gone = Other(0);

            Assert.That(LookUntil(0, 10.0, gone, Main(), out double none), Is.EqualTo(SignalStep.Wait));
            Assert.That(signal.IsOtherProgram(gone), Is.False);
        }

        [Test]
        public void IsOtherProgram_TheGameTheLauncherAndNobodyAreNot_AnythingElseIs()
        {
            Assert.That(signal.IsOtherProgram(null), Is.False);
            Assert.That(signal.IsOtherProgram(Main()), Is.False, "the game");
            Assert.That(signal.IsOtherProgram(Other(LauncherPid)), Is.False, "the launcher");
            Assert.That(signal.IsOtherProgram(Other(777)), Is.True);
        }

        [Test]
        public void NoForegroundWindow_Waits()
        {
            Assert.That(LookUntil(0, 30.0, null, Main(), out double none), Is.EqualTo(SignalStep.Wait));
            Assert.That(signal.Wait, Is.EqualTo(SignalWait.NoForeground));
        }

        [TestCase(VisibleStyle | 0x20000000L, "minimized")]
        [TestCase(0x06CF0000L, "hidden: WS_VISIBLE is missing")]
        public void AMinimizedOrHiddenMainWindow_Waits(long style, string why)
        {
            WindowState main = Main(style: style);

            Assert.That(LookUntil(0, 30.0, main, main, out double none), Is.EqualTo(SignalStep.Wait), why);
            Assert.That(signal.Wait, Is.EqualTo(SignalWait.Minimized), why);
        }

        [Test]
        public void AMainWindowThatDoesNotRespond_Waits_AndTheQuietTimeStartsWhenItResponds()
        {
            WindowState main = Main();

            Assert.That(LookUntil(0, 30.0, main, main, out double none, responding: false), Is.EqualTo(SignalStep.Wait),
                "30 s of a window that does not process messages: a message would wait in its queue");
            Assert.That(signal.Wait, Is.EqualTo(SignalWait.NotResponding));
            Assert.That(signal.QuietState, Is.Null);
            Assert.That(signal.QuietFor, Is.EqualTo(TimeSpan.Zero));

            Assert.That(LookUntil(30.25, 35.0, main, main, out none), Is.EqualTo(SignalStep.Wait), "the quiet time started when it responded again");
            Assert.That(signal.Observe(At(35.25), main, main), Is.EqualTo(SignalStep.Send));
        }

        [Test]
        public void AWindowThatStopsRespondingInTheLastSecond_StopsTheQuietTime()
        {
            WindowState main = Main();
            LookUntil(0, 4.75, main, main, out double none);

            Assert.That(signal.Observe(At(5.0), main, main, mainResponding: false), Is.EqualTo(SignalStep.Wait));
            Assert.That(signal.Wait, Is.EqualTo(SignalWait.NotResponding));
        }

        [Test]
        public void AMainWindowOfAnotherClass_CountsAsMissing_AndNeverGetsTheSignal()
        {
            WindowState second = Main(className: "SomeOtherClass");

            Assert.That(LookUntil(0, 9.75, second, second, out double none), Is.EqualTo(SignalStep.Wait));
            Assert.That(signal.Wait, Is.EqualTo(SignalWait.MainWindowMissing));
            Assert.That(signal.QuietState, Is.Null);
            Assert.That(signal.Observe(At(10.0), second, second), Is.EqualTo(SignalStep.WindowGone), "the same rule as for a window that is not there");
        }

        [Test]
        public void TheClassIsComparedExactly()
        {
            WindowState lower = Main(className: "sssi empire earth");

            Assert.That(LookUntil(0, 9.75, lower, lower, out double none), Is.EqualTo(SignalStep.Wait));
            Assert.That(signal.Wait, Is.EqualTo(SignalWait.MainWindowMissing));
        }

        [Test]
        public void AMainWindowOfAnotherProcess_CountsAsMissing_WhenTheProcessIdWasReused()
        {
            WindowState reused = Main(processId: 5151);

            Assert.That(LookUntil(0, 9.75, Splash(), reused, out double none), Is.EqualTo(SignalStep.Wait));
            Assert.That(signal.Wait, Is.EqualTo(SignalWait.MainWindowMissing));
            Assert.That(signal.Observe(At(10.0), Splash(), reused), Is.EqualTo(SignalStep.WindowGone));
        }

        [Test]
        public void TheMainWindowOfTheRightClassAndProcess_StillGetsTheSignal_AfterAWrongOneWasSeen()
        {
            WindowState wrong = Main(className: "SomeOtherClass", handle: 0x4321);
            WindowState main = Main();
            LookUntil(0, 3.0, wrong, wrong, out double none);

            Assert.That(signal.Observe(At(3.25), main, main), Is.EqualTo(SignalStep.Wait));
            Assert.That(LookUntil(3.5, 8.0, main, main, out none), Is.EqualTo(SignalStep.Wait));
            Assert.That(signal.Observe(At(8.25), main, main), Is.EqualTo(SignalStep.Send));
        }

        [Test]
        public void ASignalNeedsTheClassOfTheMainWindow([Values(null, "")] string className)
        {
            Assert.That(() => new ActivationSignal(GamePid, LauncherPid, className), Throws.ArgumentException);
        }

        [Test]
        public void Withdraw_TakesTheDecisionBack_TheQuietTimeStartsAgain_AndTheSignalCanSendLater()
        {
            WindowState main = Main();
            LookUntil(0, 5.0, main, main, out double none);

            signal.Withdraw(SignalWait.LauncherInFront);

            Assert.That(signal.IsPending, Is.True);
            Assert.That(signal.Wait, Is.EqualTo(SignalWait.LauncherInFront));
            Assert.That(signal.QuietState, Is.Null);
            Assert.That(signal.QuietFor, Is.EqualTo(TimeSpan.Zero));
            Assert.That(signal.Observe(At(5.25), main, main), Is.EqualTo(SignalStep.Wait), "5 s from now, not from the first look");
            Assert.That(LookUntil(5.5, 10.0, main, main, out none), Is.EqualTo(SignalStep.Wait));
            Assert.That(signal.Observe(At(10.25), main, main), Is.EqualTo(SignalStep.Send));
        }

        [Test]
        public void Withdraw_AfterTheDeadline_StillExpires()
        {
            WindowState main = Main();
            LookUntil(0, 5.0, main, main, out double none);
            signal.Withdraw(SignalWait.NoForeground);

            Assert.That(signal.Observe(At(180.0), main, main), Is.EqualTo(SignalStep.Expire));
        }

        [Test]
        public void Withdraw_WithoutADecisionToSend_Throws()
        {
            Assert.That(() => signal.Withdraw(SignalWait.NoForeground), Throws.InvalidOperationException, "no look yet");
            WindowState main = Main();
            LookUntil(0, 3.0, main, main, out double none);
            Assert.That(() => signal.Withdraw(SignalWait.NoForeground), Throws.InvalidOperationException, "the quiet time is not over");
        }

        [Test]
        public void Withdraw_AfterExpireOrComplete_Throws()
        {
            WindowState main = Main();
            Assert.That(signal.Observe(At(180.0), main, main), Is.EqualTo(SignalStep.Expire));
            Assert.That(() => signal.Withdraw(SignalWait.NoForeground), Throws.InvalidOperationException, "the decision was Expire");

            var sent = new ActivationSignal(GamePid, LauncherPid, MainClass);
            for (double t = 0; sent.Observe(At(t), main, main) != SignalStep.Send; t += 0.25)
            {
            }
            sent.Complete(ActivationSignalOutcome.Sent);
            Assert.That(() => sent.Withdraw(SignalWait.NoForeground), Throws.InvalidOperationException, "the message went out");
        }

        [Test]
        public void QuietFor5sReachedAtTheDeadline_Expires_AndNeverSends()
        {
            WindowState main = Main();
            Assert.That(LookUntil(175.0, 179.75, main, main, out double none), Is.EqualTo(SignalStep.Wait));

            Assert.That(signal.Observe(At(180.0), main, main), Is.EqualTo(SignalStep.Expire), "the deadline comes first");
        }

        [Test]
        public void AtTheDeadline_ExpiresOnce()
        {
            WindowState main = Main();
            Assert.That(signal.Observe(At(179.75), Other(LauncherPid), main), Is.EqualTo(SignalStep.Wait));

            Assert.That(signal.Observe(At(180.0), Other(LauncherPid), main), Is.EqualTo(SignalStep.Expire));
            Assert.That(signal.Observe(At(180.25), main, main), Is.EqualTo(SignalStep.Wait), "decided: no second Expire, and no Send");
            signal.Complete(ActivationSignalOutcome.NotSettled);
            Assert.That(signal.Observe(At(181.0), main, main), Is.EqualTo(SignalStep.Wait));
            Assert.That(signal.Outcome, Is.EqualTo(ActivationSignalOutcome.NotSettled));
            Assert.That(signal.IsPending, Is.False);
        }

        [Test]
        public void AMissingMainWindowFor10s_EndsWithWindowGone()
        {
            Assert.That(LookUntil(100.0, 109.75, null, null, out double none), Is.EqualTo(SignalStep.Wait));
            Assert.That(signal.Wait, Is.EqualTo(SignalWait.MainWindowMissing));

            Assert.That(signal.Observe(At(110.0), null, null), Is.EqualTo(SignalStep.WindowGone));
        }

        [Test]
        public void AMissingMainWindowFor9s_StillWaits_AndItsReturnStartsTheCountAgain()
        {
            Assert.That(LookUntil(100.0, 109.0, null, null, out double none), Is.EqualTo(SignalStep.Wait));
            WindowState main = Main();
            Assert.That(signal.Observe(At(109.25), main, main), Is.EqualTo(SignalStep.Wait), "the window is back");
            Assert.That(signal.Wait, Is.EqualTo(SignalWait.Settling));

            Assert.That(LookUntil(109.5, 130.0, null, null, out double endedAt), Is.EqualTo(SignalStep.WindowGone));
            Assert.That(endedAt, Is.EqualTo(119.5), "10 s after the second disappearance, not after the first");
        }

        [Test]
        public void AfterComplete_NoLookSendsAgain()
        {
            WindowState main = Main();
            LookUntil(0, 5.0, main, main, out double none);
            signal.Complete(ActivationSignalOutcome.Sent);

            Assert.That(signal.IsPending, Is.False);
            Assert.That(signal.Outcome, Is.EqualTo(ActivationSignalOutcome.Sent));
            Assert.That(LookUntil(5.25, 60.0, main, main, out none), Is.EqualTo(SignalStep.Wait));
        }

        [Test]
        public void CompleteTwice_Throws()
        {
            signal.Complete(ActivationSignalOutcome.Cancelled);

            Assert.That(() => signal.Complete(ActivationSignalOutcome.Sent), Throws.InvalidOperationException);
            Assert.That(signal.Outcome, Is.EqualTo(ActivationSignalOutcome.Cancelled), "the first outcome stays");
        }

        [Test]
        public void ANewSignal_IsPendingAndNotArmedYet()
        {
            Assert.That(signal.IsPending, Is.True);
            Assert.That(signal.Outcome, Is.EqualTo(ActivationSignalOutcome.NotArmed));
            Assert.That(signal.Wait, Is.EqualTo(SignalWait.None));
            Assert.That(signal.QuietState, Is.Null);
        }

        [Test]
        public void TheConstants_AreTheDesign()
        {
            Assert.That(ActivationSignal.SettleTime, Is.EqualTo(TimeSpan.FromSeconds(5)));
            Assert.That(ActivationSignal.Deadline, Is.EqualTo(TimeSpan.FromSeconds(180)));
            Assert.That(ActivationSignal.MissingWindowLimit, Is.EqualTo(TimeSpan.FromSeconds(10)));
            Assert.That(ActivationSignal.MinimizedStyle, Is.EqualTo(0x20000000L), "WS_MINIMIZE");
            Assert.That(ActivationSignal.VisibleStyle, Is.EqualTo(0x10000000L), "WS_VISIBLE");
        }
    }
}

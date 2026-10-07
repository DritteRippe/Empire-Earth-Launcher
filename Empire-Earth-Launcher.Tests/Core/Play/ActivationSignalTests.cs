using System;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Core.Play;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.Play
{
    /// <summary>
    /// <see cref="ActivationSignal"/>, the decision of A1b (ADR 0010 amendment of 1.1.0): the one activation message goes to the
    /// main window of a started game after it has been the foreground window, with the same rectangle and styles and not
    /// minimized, for five seconds, never while another window is in front, never 180 seconds or later after the start, and
    /// never twice. The class decides from the windows it is shown and a time since the start; no window is touched here.
    /// </summary>
    [TestFixture]
    public class ActivationSignalTests
    {
        private const int GamePid = 4242;
        private const int LauncherPid = 100;
        private const long VisibleStyle = 0x16CF0000;

        private ActivationSignal signal;

        [SetUp]
        public void SetUp()
        {
            signal = new ActivationSignal(GamePid);
        }

        private static TimeSpan At(double seconds)
        {
            return TimeSpan.FromSeconds(seconds);
        }

        private static WindowState Main(int right = 1920, int bottom = 1200, long style = VisibleStyle, long exStyle = 0x00040008, int handle = 0x1234)
        {
            return new WindowState(new IntPtr(handle), GamePid, "SSSI Empire Earth", 0, 0, right, bottom, style, exStyle);
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
        private SignalStep LookUntil(double from, double to, WindowState foreground, WindowState main, out double at)
        {
            for (double t = from; t <= to + 0.0001; t += 0.25)
            {
                SignalStep step = signal.Observe(At(t), foreground, main);
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

        [Test]
        public void AnotherProgramInFront_Waits()
        {
            Assert.That(LookUntil(0, 30.0, Other(777), Main(), out double none), Is.EqualTo(SignalStep.Wait));
            Assert.That(signal.Wait, Is.EqualTo(SignalWait.OtherProgramInFront));
        }

        [Test]
        public void TheLauncherInFront_Waits_ItCountsAsAnotherProgram()
        {
            Assert.That(LookUntil(0, 30.0, Other(LauncherPid, "WindowsForms10.Window.8.app"), Main(), out double none), Is.EqualTo(SignalStep.Wait));
            Assert.That(signal.Wait, Is.EqualTo(SignalWait.OtherProgramInFront));
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
            Assert.That(signal.Observe(At(179.75), Other(777), main), Is.EqualTo(SignalStep.Wait));

            Assert.That(signal.Observe(At(180.0), Other(777), main), Is.EqualTo(SignalStep.Expire));
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

using System;
using Empire_Earth_Launcher.Core.Platform;

namespace Empire_Earth_Launcher.Core.Play
{
    /// <summary>How the activation signal of a start ended (<see cref="ActivationSignal"/>, A1b).</summary>
    public enum ActivationSignalOutcome
    {
        /// <summary>Not armed (the hand-over did not end with the game in front) or not decided yet.</summary>
        NotArmed,

        /// <summary>The one <c>WM_ACTIVATE</c> was posted to the main window.</summary>
        Sent,

        /// <summary>Windows refused the message (for example error 5, a game that runs as administrator); no retry.</summary>
        SendFailed,

        /// <summary>The main window was not in front, unchanged, for <see cref="ActivationSignal.SettleTime"/> within <see cref="ActivationSignal.Deadline"/>.</summary>
        NotSettled,

        /// <summary>The main window was missing for <see cref="ActivationSignal.MissingWindowLimit"/>: the game has ended.</summary>
        WindowGone,

        /// <summary>The launcher closed before the signal was decided.</summary>
        Cancelled,

        /// <summary>The watch failed (an exception of the window system) before the signal was decided; nothing was sent.</summary>
        WatchFailed,

        /// <summary>
        /// A window of another program (neither the game nor the launcher) was in front after the game had been: the player
        /// switched away, and Windows activates the game when the player returns to it. Nothing is sent.
        /// </summary>
        PlayerSwitched
    }

    /// <summary>What one look of the watch tells the caller to do.</summary>
    public enum SignalStep
    {
        /// <summary>Nothing yet; look again.</summary>
        Wait,

        /// <summary>Post the activation message to the main window and call <see cref="ActivationSignal.Complete"/>.</summary>
        Send,

        /// <summary>The deadline is reached: complete with <see cref="ActivationSignalOutcome.NotSettled"/>.</summary>
        Expire,

        /// <summary>The main window has been missing too long: complete with <see cref="ActivationSignalOutcome.WindowGone"/>.</summary>
        WindowGone,

        /// <summary>A window of another program is in front: complete with <see cref="ActivationSignalOutcome.PlayerSwitched"/>.</summary>
        PlayerSwitched
    }

    /// <summary>Why the signal waits (logged when it changes).</summary>
    public enum SignalWait
    {
        /// <summary>No look yet, or the signal decided to send.</summary>
        None,

        MainWindowMissing,

        NoForeground,

        /// <summary>A window of the game other than the main window is in front: the lobby popup, the splash, a dialog.</summary>
        OtherWindowOfTheGameInFront,

        /// <summary>A window of the launcher is in front (it started the game and may hold the foreground for a moment): the signal keeps waiting.</summary>
        LauncherInFront,

        Minimized,

        /// <summary>The main window does not respond (<c>IsHungAppWindow</c>): a message would wait in its queue, maybe until the player has switched away.</summary>
        NotResponding,

        /// <summary>The main window is in front but has not been unchanged for <see cref="ActivationSignal.SettleTime"/> yet.</summary>
        Settling
    }

    /// <summary>
    /// A1b of 1.1.0 (ADR 0010 amendment): decides when the launcher posts the one <c>WM_ACTIVATE</c> (<c>WA_ACTIVE</c>) to the
    /// main window of a game it started: once the window has been the foreground window with the same rectangle and styles, not
    /// minimized, responding, for <see cref="SettleTime"/>, and never <see cref="Deadline"/> or later after the start. Empire Earth acquires
    /// its DirectInput mouse and keyboard on activation only; without a display mode change at the start no activation reaches
    /// it after it created them, and the mouse stays dead until Alt+Tab. The class only decides; it calls nothing and has no
    /// clock of its own (the caller passes the time since the start).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Inside the wait one look decides in this order: the deadline; a foreground window of another program, neither the game
    /// nor the launcher (the player switched away: the signal ends for good, <see cref="SignalStep.PlayerSwitched"/>); no main
    /// window (reset the quiet time; the game has ended after <see cref="MissingWindowLimit"/> in a row); no foreground window;
    /// a foreground window that is not the main window (reset; "another window of the game" if it belongs to the same process,
    /// else the launcher); a minimized or hidden main window (reset); a main window that does not respond (reset); a main
    /// window whose handle, rectangle or styles differ from the last quiet state (the quiet time starts now); and finally the
    /// quiet time itself.
    /// </para>
    /// <para>
    /// Why a switch to another program ends the signal: it is armed only while the game is in front (the hand-over ended with
    /// <see cref="ActivationOutcome.GameInForeground"/>). If the player goes to another program and comes back, Windows gives
    /// the game a real activation on the return, which does what the message would do; a synthetic one five seconds later
    /// would be a second activation, in the worst case in the middle of a match. It is the rule of
    /// <see cref="ActivationOutcome.UserSwitched"/>, which arms nothing. Not a switch: the launcher, the windows of the game
    /// itself (lobby popup, splash, a dialog) and no window at all (Windows has none for a moment while a window is created).
    /// </para>
    /// <para>
    /// Which window is the main window: the window the caller passes, but only if it is a window of the process the launcher
    /// started and of the class the hand-over found at arming (<c>SSSI Empire Earth</c>). Windows reuses process ids; if the game
    /// has ended and another program got the id, or the game has a second window without an owner, the signal must not go to a
    /// window that is not the main window of the game. A window of another process or class counts as missing.
    /// </para>
    /// <para>
    /// The caller checks once more right before the post (the foreground may have changed since the look, the launcher may be
    /// closing); if that fails it calls <see cref="Withdraw"/>, which takes the decision back and starts the quiet time again.
    /// </para>
    /// <para>
    /// Why <see cref="SettleTime"/> is 5 s. dgVoodoo does its window work in one burst: in real fullscreen inside the game's
    /// SetCooperativeLevel/SetDisplayMode, in fake fullscreen at the first presented frame; a display mode switch with its monitor
    /// resync takes up to about 2 to 3 s on laptop panels and over HDMI. 5 s without any change of rectangle or styles puts the
    /// signal after that burst with margin, so it is not spent before the last change. The game creates its DirectInput devices
    /// right after its graphics initialization, so 5 s after the window has settled is after that, also from a slow disk. And it
    /// is short enough for the intro: the logos last 9 s (Sierra) and 8 s (SSSI), so with the window settling within a few
    /// seconds of its creation the signal arrives during a logo and a click skips the 98 s movie at the latest. 3 s would win two
    /// seconds but leave no margin on slow machines (a signal before the last change is wasted: at most once).
    /// </para>
    /// <para>
    /// Why <see cref="Deadline"/> is 180 s: the longest normal way to the main menu is the window search (at most 60 s) plus the
    /// intro (115 s); later the player is in the menu, the lobby or a match, where a synthetic activation (the game centres its
    /// cursor once) would only disturb. Why <see cref="MissingWindowLimit"/> is 10 s: a window is briefly invisible while a
    /// wrapper restyles it; 10 s without it means the game has ended.
    /// </para>
    /// </remarks>
    public sealed class ActivationSignal
    {
        /// <summary>How long the main window must be the foreground window, unchanged, before the signal goes out.</summary>
        public static readonly TimeSpan SettleTime = TimeSpan.FromSeconds(5);

        /// <summary>The signal is never sent this long after the start or later.</summary>
        public static readonly TimeSpan Deadline = TimeSpan.FromSeconds(180);

        /// <summary>How long the main window may be missing, in a row, before the game counts as ended.</summary>
        public static readonly TimeSpan MissingWindowLimit = TimeSpan.FromSeconds(10);

        /// <summary><c>WS_MINIMIZE</c>.</summary>
        public const long MinimizedStyle = 0x20000000;

        /// <summary><c>WS_VISIBLE</c>.</summary>
        public const long VisibleStyle = 0x10000000;

        private readonly int gameProcessId;
        private readonly int launcherProcessId;
        private readonly string mainWindowClass;
        private bool completed;
        private bool decided;
        private bool sendDecided;
        private TimeSpan quietSince;
        private TimeSpan? missingSince;

        /// <param name="gameProcessId">The process the launcher started; a foreground window of it that is not the main window is "another window of the game".</param>
        /// <param name="launcherProcessId">The process of the launcher: its windows in front are no switch to another program.</param>
        /// <param name="mainWindowClass">The class of the main window that the hand-over found at arming; a main window of another class counts as missing.</param>
        public ActivationSignal(int gameProcessId, int launcherProcessId, string mainWindowClass)
        {
            if (string.IsNullOrEmpty(mainWindowClass))
                throw new ArgumentException("The class of the main window is needed.", nameof(mainWindowClass));
            this.gameProcessId = gameProcessId;
            this.launcherProcessId = launcherProcessId;
            this.mainWindowClass = mainWindowClass;
        }

        /// <summary>True until <see cref="Complete"/> was called: the signal is still waiting.</summary>
        public bool IsPending
        {
            get { return !completed; }
        }

        /// <summary><see cref="ActivationSignalOutcome.NotArmed"/> until <see cref="Complete"/>.</summary>
        public ActivationSignalOutcome Outcome { get; private set; }

        /// <summary>The reason of the last look to wait.</summary>
        public SignalWait Wait { get; private set; }

        /// <summary>The state of the main window that the quiet time measures; null if there is none.</summary>
        public WindowState QuietState { get; private set; }

        /// <summary>How long the main window has been quiet at the last look.</summary>
        public TimeSpan QuietFor { get; private set; }

        /// <summary>One look of the watch.</summary>
        /// <param name="elapsed">Time since the start of the game.</param>
        /// <param name="foreground">The foreground window (null: none).</param>
        /// <param name="main">The main window of the game (null: not there). A window of another process or class counts as not there.</param>
        /// <param name="mainResponding">False if Windows counts the main window as not responding.</param>
        public SignalStep Observe(TimeSpan elapsed, WindowState foreground, WindowState main, bool mainResponding = true)
        {
            // Once a look has decided, or the signal is complete, no look can decide again: the message goes out at most once.
            if (completed || decided)
                return SignalStep.Wait;

            if (elapsed >= Deadline)
            {
                decided = true;
                return SignalStep.Expire;
            }

            // The player went to another program (process 0: a window that is gone, as good as none).
            if (IsOtherProgram(foreground))
            {
                decided = true;
                Wait = SignalWait.None;
                return SignalStep.PlayerSwitched;
            }

            // Not the main window of this game (the process id was reused, a second window without an owner): as if it was not there.
            if (main != null && (main.ProcessId != gameProcessId || !string.Equals(main.ClassName, mainWindowClass, StringComparison.Ordinal)))
                main = null;

            if (main == null)
            {
                ResetQuiet();
                if (missingSince == null)
                    missingSince = elapsed;
                Wait = SignalWait.MainWindowMissing;
                if (elapsed - missingSince.Value >= MissingWindowLimit)
                {
                    decided = true;
                    return SignalStep.WindowGone;
                }
                return SignalStep.Wait;
            }
            missingSince = null;

            if (foreground == null)
                return WaitFor(SignalWait.NoForeground);
            if (foreground.Handle != main.Handle)
            {
                return WaitFor(foreground.ProcessId == gameProcessId
                    ? SignalWait.OtherWindowOfTheGameInFront
                    : SignalWait.LauncherInFront);
            }
            if ((main.Style & MinimizedStyle) != 0 || (main.Style & VisibleStyle) == 0)
                return WaitFor(SignalWait.Minimized);
            if (!mainResponding)
                return WaitFor(SignalWait.NotResponding);

            if (QuietState == null || QuietState.Handle != main.Handle || !QuietState.HasSameRectangle(main) || !QuietState.HasSameStyles(main))
            {
                QuietState = main;
                quietSince = elapsed;
                QuietFor = TimeSpan.Zero;
                Wait = SignalWait.Settling;
                return SignalStep.Wait;
            }

            QuietFor = elapsed - quietSince;
            if (QuietFor >= SettleTime)
            {
                decided = true;
                sendDecided = true;
                Wait = SignalWait.None;
                return SignalStep.Send;
            }
            Wait = SignalWait.Settling;
            return SignalStep.Wait;
        }

        /// <summary>
        /// Takes back the decision to send that the last <see cref="Observe"/> made, because the check right before the post
        /// failed: the signal waits again for <paramref name="reason"/>, and the quiet time starts again. Throws if the last look
        /// did not decide to send or the signal is complete.
        /// </summary>
        public void Withdraw(SignalWait reason)
        {
            if (completed || !sendDecided)
                throw new InvalidOperationException("There is no decision to send to withdraw.");
            decided = false;
            sendDecided = false;
            ResetQuiet();
            Wait = reason;
        }

        /// <summary>Ends the wait with <paramref name="outcome"/>; a second call throws.</summary>
        public void Complete(ActivationSignalOutcome outcome)
        {
            if (completed)
                throw new InvalidOperationException("The activation signal is complete already (" + Outcome + ").");
            completed = true;
            Outcome = outcome;
        }

        /// <summary>
        /// True if <paramref name="window"/> belongs to a program that is neither the game nor the launcher: the player is
        /// there. The caller uses it right before the post too.
        /// </summary>
        public bool IsOtherProgram(WindowState window)
        {
            return window != null && window.ProcessId != 0 && window.ProcessId != gameProcessId && window.ProcessId != launcherProcessId;
        }

        private SignalStep WaitFor(SignalWait reason)
        {
            ResetQuiet();
            Wait = reason;
            return SignalStep.Wait;
        }

        private void ResetQuiet()
        {
            QuietState = null;
            QuietFor = TimeSpan.Zero;
        }
    }
}

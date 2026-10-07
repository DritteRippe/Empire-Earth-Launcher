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
        Cancelled
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
        WindowGone
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

        /// <summary>A window of another program is in front; the launcher counts as another program.</summary>
        OtherProgramInFront,

        Minimized,

        /// <summary>The main window is in front but has not been unchanged for <see cref="ActivationSignal.SettleTime"/> yet.</summary>
        Settling
    }

    /// <summary>
    /// A1b of 1.1.0 (ADR 0010 amendment): decides when the launcher posts the one <c>WM_ACTIVATE</c> (<c>WA_ACTIVE</c>) to the
    /// main window of a game it started: once the window has been the foreground window with the same rectangle and styles, not
    /// minimized, for <see cref="SettleTime"/>, and never <see cref="Deadline"/> or later after the start. Empire Earth acquires
    /// its DirectInput mouse and keyboard on activation only; without a display mode change at the start no activation reaches
    /// it after it created them, and the mouse stays dead until Alt+Tab. The class only decides; it calls nothing and has no
    /// clock of its own (the caller passes the time since the start).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Inside the wait one look decides in this order: the deadline; no main window (reset the quiet time; the game has ended
    /// after <see cref="MissingWindowLimit"/> in a row); no foreground window; a foreground window that is not the main window
    /// (reset; "another window of the game" if it belongs to the same process, else "another program"); a minimized or hidden
    /// main window (reset); a main window whose handle, rectangle or styles differ from the last quiet state (the quiet time
    /// starts now); and finally the quiet time itself.
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
        private bool completed;
        private bool decided;
        private TimeSpan quietSince;
        private TimeSpan? missingSince;

        /// <param name="gameProcessId">The process the launcher started; a foreground window of it that is not the main window is "another window of the game".</param>
        public ActivationSignal(int gameProcessId)
        {
            this.gameProcessId = gameProcessId;
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
        /// <param name="main">The main window of the game (null: not there).</param>
        public SignalStep Observe(TimeSpan elapsed, WindowState foreground, WindowState main)
        {
            // Once a look has decided, or the signal is complete, no look can decide again: the message goes out at most once.
            if (completed || decided)
                return SignalStep.Wait;

            if (elapsed >= Deadline)
            {
                decided = true;
                return SignalStep.Expire;
            }

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
                    : SignalWait.OtherProgramInFront);
            }
            if ((main.Style & MinimizedStyle) != 0 || (main.Style & VisibleStyle) == 0)
                return WaitFor(SignalWait.Minimized);

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
                Wait = SignalWait.None;
                return SignalStep.Send;
            }
            Wait = SignalWait.Settling;
            return SignalStep.Wait;
        }

        /// <summary>Ends the wait with <paramref name="outcome"/>; a second call throws.</summary>
        public void Complete(ActivationSignalOutcome outcome)
        {
            if (completed)
                throw new InvalidOperationException("The activation signal is complete already (" + Outcome + ").");
            completed = true;
            Outcome = outcome;
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

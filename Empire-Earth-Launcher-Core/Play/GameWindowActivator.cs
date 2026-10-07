using System;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Logging;
using Empire_Earth_Launcher.Core.Platform;

namespace Empire_Earth_Launcher.Core.Play
{
    /// <summary>How the hand-over of the foreground to a started game ended (<see cref="GameWindowActivator"/>).</summary>
    public enum ActivationOutcome
    {
        /// <summary>The game owns the foreground: the launcher handed it over, or the game had it already.</summary>
        GameInForeground,

        /// <summary>Not tried: the process id of the game is not known (ADR 0010 amendment, "pid unknown").</summary>
        ProcessIdUnknown,

        /// <summary>The game showed no window within <see cref="GameWindowActivator.FindTimeout"/>; nothing was changed.</summary>
        NoWindow,

        /// <summary>Another program owns the foreground (the player switched): the launcher never steals it.</summary>
        UserSwitched,

        /// <summary>The launcher kept the foreground after <see cref="GameWindowActivator.MaxReactivations"/> hand-overs (or Windows refused).</summary>
        GaveUp,

        /// <summary>The launcher is closing: the hand-over was stopped.</summary>
        Cancelled
    }

    /// <summary>What <see cref="GameWindowActivator.ActivateAsync"/> did for one start: the hand-over of the foreground and the activation signal (A1b).</summary>
    public sealed class ActivationResult
    {
        public ActivationResult(ActivationOutcome handOver, ActivationSignalOutcome signal)
        {
            HandOver = handOver;
            Signal = signal;
        }

        /// <summary>How the hand-over of the foreground ended; <see cref="ActivationOutcome.Cancelled"/> if the launcher closed at any time before the end of the watch.</summary>
        public ActivationOutcome HandOver { get; }

        /// <summary><see cref="ActivationSignalOutcome.NotArmed"/> if the signal was not armed (or the watch failed before it was decided).</summary>
        public ActivationSignalOutcome Signal { get; }
    }

    /// <summary>
    /// Hands the foreground to the window of a game that the launcher has just started (ADR 0010 amendment of 1.1.0). The
    /// game acquires its DirectInput devices with the foreground cooperative level; if the launcher keeps the foreground while
    /// the game creates its window, the mouse stays dead until the player minimizes and restores the window (report 1).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Runs on the thread pool: after the start it polls every <see cref="PollInterval"/> for up to
    /// <see cref="FindTimeout"/> for the main window of the game process (the first visible window without an owner that is no
    /// tool window: not the splash 'Loading Game Window'). If the launcher owns the foreground, or nobody does, then it calls
    /// <c>SetForegroundWindow</c> on it; if the game owns it already it logs that and calls nothing. A foreground window that
    /// is missing is looked at again <see cref="NullForegroundPolls"/> times first (Windows has none for a moment while a window
    /// is created or the display mode switches); no window in front is nobody's, so acting then takes nothing from anyone.
    /// About <see cref="RecheckInterval"/> later it looks again (a wrapper such as dgVoodoo may switch the display mode, and
    /// Windows may give the foreground back to the launcher); it hands the window over again at most
    /// <see cref="MaxReactivations"/> times while the launcher (or nobody) owns the foreground.
    /// </para>
    /// <para>
    /// Afterwards it watches, for at least <see cref="WatchDuration"/>: every change of the foreground window and of the rectangle
    /// and the styles of the main window is logged with the time since the start. That is a measurement, not an action.
    /// </para>
    /// <para>
    /// Once per start, and only for a game it started and that owns the foreground after the hand-over, the launcher posts one
    /// <c>WM_ACTIVATE</c> (<c>WA_ACTIVE</c>) to the main window (A1b, <see cref="ActivationSignal"/>): Empire Earth acquires its
    /// DirectInput devices on activation only, and with dgVoodoo no activation reaches it after it created them, unless the
    /// start changed the display mode. The message goes out when the main window has been the foreground window, with the same
    /// rectangle and styles, for <see cref="ActivationSignal.SettleTime"/>, at the latest <see cref="ActivationSignal.Deadline"/>
    /// after the start; it never goes out while another window (the lobby popup, the splash, another program) is in front. If
    /// the post fails (the game runs as administrator) it is logged and nothing else happens. The watch lasts until the signal
    /// is decided. Right before the post it reads the foreground window again and whether the main window responds, and it
    /// stops if the launcher is closing: the look that decided may be some milliseconds old, and a message to a window that does
    /// not process messages (the game is loading) would wait in its queue, perhaps until the player has switched away. The
    /// main window must be of the class that the hand-over found (<c>SSSI Empire Earth</c>) and of the started process. All
    /// durations come from <see cref="IClock.Elapsed"/>, which a change of the clock of the computer does not move.
    /// </para>
    /// <para>
    /// The launcher never steals the foreground: as soon as another process owns it, the hand-over ends. It does not minimize,
    /// hide, move, resize or close any window, does not wait for the game to end and never ends a process. One line says when the
    /// window was found, what was done and who owned the foreground, and every decision of the signal is logged. The start itself
    /// stays a shell start (ADR 0010).
    /// </para>
    /// </remarks>
    public sealed class GameWindowActivator
    {
        /// <summary>How often the window of the game is looked for.</summary>
        public static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);

        /// <summary>How long the launcher waits for the window of the game before it gives up (loading from a slow disk, a wrapper).</summary>
        public static readonly TimeSpan FindTimeout = TimeSpan.FromSeconds(60);

        /// <summary>The time after a hand-over until the foreground is looked at again.</summary>
        public static readonly TimeSpan RecheckInterval = TimeSpan.FromSeconds(2);

        /// <summary>How many times the window is handed the foreground again after the first time.</summary>
        public const int MaxReactivations = 3;

        /// <summary>How often a missing foreground window is looked at again (every <see cref="PollInterval"/>) before it counts as nobody's.</summary>
        public const int NullForegroundPolls = 3;

        /// <summary>How long the foreground and the window of the game are watched, at least, after the hand-over (longer while the activation signal waits).</summary>
        public static readonly TimeSpan WatchDuration = TimeSpan.FromSeconds(60);

        /// <summary>How often the watch reads the foreground window and the window of the game.</summary>
        public static readonly TimeSpan WatchInterval = TimeSpan.FromMilliseconds(250);

        /// <summary><c>ERROR_ACCESS_DENIED</c>: what <c>PostMessage</c> reports to a window of a process with higher rights (UIPI).</summary>
        private const int ErrorAccessDenied = 5;

        /// <summary><c>ERROR_INVALID_WINDOW_HANDLE</c>: the main window was gone before the message could be posted.</summary>
        private const int ErrorInvalidWindowHandle = 1400;

        private readonly IWindowSystem windows;
        private readonly IClock clock;
        private readonly ILogger logger;
        private readonly int launcherProcessId;
        private readonly Func<TimeSpan, CancellationToken, Task> delay;

        /// <param name="windows">The foreground and the windows of the computer.</param>
        /// <param name="clock">Measures how long the window took to appear and how long it has been quiet (<see cref="IClock.Elapsed"/>).</param>
        /// <param name="logger">Log of the launcher.</param>
        /// <param name="launcherProcessId">The process id of the launcher: while it owns the foreground, the game may take it.</param>
        /// <param name="delay">Waits between two looks; <see cref="Task.Delay(TimeSpan, CancellationToken)"/> if null (tests pass
        /// a delay that moves their clock).</param>
        public GameWindowActivator(IWindowSystem windows, IClock clock, ILogger logger, int launcherProcessId,
            Func<TimeSpan, CancellationToken, Task> delay = null)
        {
            this.windows = windows ?? throw new ArgumentNullException(nameof(windows));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.launcherProcessId = launcherProcessId;
            this.delay = delay ?? Task.Delay;
        }

        /// <summary>
        /// Hands the foreground to the window of the game started with <paramref name="processId"/>, on the thread pool, and then
        /// watches it for <see cref="WatchDuration"/> and while the activation signal waits. The task never throws: a failure is
        /// logged and a closing launcher (<paramref name="cancellationToken"/>) ends it with <see cref="ActivationOutcome.Cancelled"/>.
        /// It ends after the watch; the result tells the outcome of the hand-over and of the signal.
        /// </summary>
        /// <param name="processId">The process id of the started game; null if it is not known (logged, nothing else happens).</param>
        /// <param name="game">The game that was started (for the log).</param>
        /// <param name="cancellationToken">Cancelled when the launcher closes.</param>
        public Task<ActivationResult> ActivateAsync(int? processId, Game game, CancellationToken cancellationToken = default)
        {
            if (game == null)
                throw new ArgumentNullException(nameof(game));
            if (!processId.HasValue)
            {
                logger.Info("The foreground is not handed to " + game.ProgramName + " and no activation signal is sent: its process id is not known.");
                return Task.FromResult(new ActivationResult(ActivationOutcome.ProcessIdUnknown, ActivationSignalOutcome.NotArmed));
            }
            return Task.Run(() => ActivateCoreAsync(processId.Value, game, cancellationToken));
        }

        private async Task<ActivationResult> ActivateCoreAsync(int processId, Game game, CancellationToken cancellationToken)
        {
            ActivationSignal signal = null;
            ActivationOutcome? handOver = null;
            string name = game.ProgramName + " (pid " + processId.ToString(CultureInfo.InvariantCulture) + ")";
            try
            {
                TimeSpan started = clock.Elapsed;
                var mainWindow = new StrongBox<IntPtr>();
                ActivationOutcome outcome = await HandOverAsync(processId, name, started, mainWindow, cancellationToken).ConfigureAwait(false);
                handOver = outcome;
                // The window may be gone (the game ended) or never came: nothing to watch then.
                if (outcome == ActivationOutcome.NoWindow)
                    return new ActivationResult(outcome, ActivationSignalOutcome.NotArmed);
                signal = Arm(outcome, processId, name, mainWindow.Value);
                await WatchAsync(processId, name, started, signal, cancellationToken).ConfigureAwait(false);
                return new ActivationResult(outcome, signal?.Outcome ?? ActivationSignalOutcome.NotArmed);
            }
            catch (OperationCanceledException)
            {
                if (signal != null && signal.IsPending)
                {
                    signal.Complete(ActivationSignalOutcome.Cancelled);
                    logger.Info("The activation signal for " + name + " was not sent: the launcher is closing.");
                }
                return new ActivationResult(ActivationOutcome.Cancelled, signal?.Outcome ?? ActivationSignalOutcome.NotArmed);
            }
            catch (Exception ex)
            {
                // A background task of a convenience: it must never end the launcher (ADR 0013). A signal that was still waiting
                // is dropped: nothing was sent. The result keeps the outcome of the hand-over if it had ended already.
                if (!handOver.HasValue)
                {
                    logger.Warning("The foreground could not be handed to " + game.ProgramName + ".", ex);
                    return new ActivationResult(ActivationOutcome.GaveUp, ActivationSignalOutcome.NotArmed);
                }
                logger.Warning("The watch of " + name + " failed after the hand-over (" + handOver.Value + ").", ex);
                if (signal != null && signal.IsPending)
                {
                    signal.Complete(ActivationSignalOutcome.WatchFailed);
                    logger.Info("The activation signal for " + name + " was not sent: the watch failed.");
                }
                return new ActivationResult(handOver.Value, signal?.Outcome ?? ActivationSignalOutcome.NotArmed);
            }
        }

        /// <summary>
        /// The activation signal of A1b for a start whose hand-over ended with <paramref name="outcome"/>: armed only if the game
        /// owns the foreground (after a user switch Windows activates the game when the player returns to it; after a give-up the
        /// player's click does); null otherwise. Logs which.
        /// </summary>
        private ActivationSignal Arm(ActivationOutcome outcome, int processId, string name, IntPtr mainWindow)
        {
            string start = "Activation signal for " + name + ": ";
            switch (outcome)
            {
                case ActivationOutcome.GameInForeground:
                    // The class of the main window that the hand-over found: a window of another class is not the main window later
                    // (the process id may be reused, the game may have a second window without an owner).
                    WindowState found = windows.ReadWindow(mainWindow);
                    if (found == null || string.IsNullOrEmpty(found.ClassName))
                    {
                        logger.Info(start + "not armed, the class of the main window " + FormatWindow(mainWindow) +
                                    " could not be read (the signal goes to a window of the class that the hand-over found only).");
                        return null;
                    }
                    logger.Info(start + "armed. One WM_ACTIVATE (WA_ACTIVE) goes to its main window (class '" + found.ClassName +
                                "') once that window has been the foreground window with the same rectangle and styles for " +
                                FormatSeconds(ActivationSignal.SettleTime) + ", at the latest " + FormatSeconds(ActivationSignal.Deadline) +
                                " after the start (A1b).");
                    return new ActivationSignal(processId, found.ClassName);
                case ActivationOutcome.UserSwitched:
                    logger.Info(start + "not armed, the hand-over ended with UserSwitched (the game is not in front; Windows activates it " +
                                "when the player returns to it).");
                    return null;
                default:
                    logger.Info(start + "not armed, the hand-over ended with " + outcome + " (the launcher kept the foreground; a click on " +
                                "the game activates it).");
                    return null;
            }
        }

        /// <summary>The window found last is left in <paramref name="mainWindow"/>.</summary>
        private async Task<ActivationOutcome> HandOverAsync(int processId, string name, TimeSpan started, StrongBox<IntPtr> mainWindow,
            CancellationToken cancellationToken)
        {
            // 1. The window: the game needs a moment (and, with a wrapper, a second window) before it shows one.
            IntPtr window;
            while ((window = windows.FindVisibleTopLevelWindow(processId)) == IntPtr.Zero)
            {
                if (clock.Elapsed - started >= FindTimeout)
                {
                    logger.Info("No window of " + name + " within " + FormatSeconds(FindTimeout) + "; the foreground was left alone.");
                    return ActivationOutcome.NoWindow;
                }
                await delay(PollInterval, cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
            }
            mainWindow.Value = window;
            string seen = "Game window " + FormatWindow(window) + " of " + name;
            string after = FormatMilliseconds(clock.Elapsed - started);

            // 2. Only the launcher (or nobody) may be in front: if another program is, the player switched. If the game is in front
            // already there is nothing to do (SetForegroundWindow would only wake the wrapper for nothing).
            int foreground = await ReadForegroundAsync(cancellationToken).ConfigureAwait(false);
            if (foreground == processId)
            {
                logger.Info(seen + " found after " + after + ", already in the foreground, nothing to do.");
            }
            else if (foreground != 0 && foreground != launcherProcessId)
            {
                logger.Info(seen + " found after " + after + ", not brought to the foreground: skipped, user switched to " +
                            Describe(foreground) + ".");
                return ActivationOutcome.UserSwitched;
            }
            else
            {
                bool done = windows.SetForegroundWindow(window);
                logger.Info(done
                    ? seen + " brought to the foreground after " + after + " (the foreground was " + Describe(foreground) + ")."
                    : seen + " found after " + after + ", SetForegroundWindow was refused (the foreground is " +
                      Describe(foreground) + ").");
            }

            // 3. A wrapper switches the display mode a moment later and Windows may give the foreground back: look again.
            int reactivations = 0;
            while (true)
            {
                await delay(RecheckInterval, cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                window = windows.FindVisibleTopLevelWindow(processId);
                if (window == IntPtr.Zero)
                {
                    logger.Info("The window of " + name + " is gone; the hand-over of the foreground ends.");
                    return ActivationOutcome.NoWindow;
                }
                mainWindow.Value = window;
                foreground = await ReadForegroundAsync(cancellationToken).ConfigureAwait(false);
                if (foreground == processId)
                    return ActivationOutcome.GameInForeground;
                if (foreground != 0 && foreground != launcherProcessId)
                {
                    logger.Info("The foreground belongs to " + Describe(foreground) + " now: skipped, user switched; the window of " +
                                name + " is left alone.");
                    return ActivationOutcome.UserSwitched;
                }
                if (reactivations == MaxReactivations)
                {
                    logger.Warning("The launcher still owns the foreground after " + MaxReactivations.ToString(CultureInfo.InvariantCulture) +
                                   " hand-overs to " + name + "; giving up.");
                    return ActivationOutcome.GaveUp;
                }
                reactivations++;
                bool done = windows.SetForegroundWindow(window);
                logger.Info((foreground == 0 ? "Nobody had the foreground: window " : "The launcher had the foreground again: window ") +
                            FormatWindow(window) + " of " + name +
                            (done ? " brought to the foreground again" : ", SetForegroundWindow was refused again") + " (" +
                            reactivations.ToString(CultureInfo.InvariantCulture) + " of " +
                            MaxReactivations.ToString(CultureInfo.InvariantCulture) + ").");
            }
        }

        /// <summary>
        /// The process that owns the foreground. No foreground window (0) is looked at again <see cref="NullForegroundPolls"/>
        /// times, <see cref="PollInterval"/> apart: Windows has none for a moment while a window is created or the display mode
        /// switches, and that is no player who switched to another program. 0 after that: nobody owns it.
        /// </summary>
        private async Task<int> ReadForegroundAsync(CancellationToken cancellationToken)
        {
            int foreground = windows.GetForegroundProcessId();
            for (int polls = 0; foreground == 0 && polls < NullForegroundPolls; polls++)
            {
                await delay(PollInterval, cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                foreground = windows.GetForegroundProcessId();
            }
            return foreground;
        }

        /// <summary>
        /// Reads, for <see cref="WatchDuration"/> and while <paramref name="signal"/> (null: not armed) waits, which window is in
        /// front and where the main window of the game is, and logs each change with the time since the start (A1 of 1.1.0: the
        /// measurement of when a wrapper switches the display mode and whether the game loses the foreground). The only thing it
        /// ever does besides reading is the one activation message of the signal (A1b, <see cref="SendSignal"/>).
        /// </summary>
        private async Task WatchAsync(int processId, string name, TimeSpan started, ActivationSignal signal, CancellationToken cancellationToken)
        {
            logger.Info("Watching " + name + " for " + FormatSeconds(WatchDuration) + " (read-only): changes of the foreground window and of " +
                        "the rectangle and the styles of its main window are logged with the time since the start.");
            TimeSpan begin = clock.Elapsed;
            bool first = true;
            IntPtr foreground = IntPtr.Zero;
            WindowState foregroundState = null;
            WindowState gameState = null;
            bool gameWasThere = false;
            SignalWait loggedWait = SignalWait.None;
            WindowState loggedQuietState = null;
            while (true)
            {
                string at = "Watch t+" + FormatElapsed(clock.Elapsed - started) + ": ";

                // The foreground window: the handle alone tells a change (a window keeps its process and its class).
                IntPtr now = windows.GetForegroundWindow();
                if (first || now != foreground)
                {
                    WindowState state = windows.ReadWindow(now);
                    logger.Info(at + (first ? "foreground window is " : "foreground window changed from " +
                                      DescribeForeground(foregroundState, processId) + " to ") + DescribeForeground(state, processId) + ".");
                    foreground = now;
                    foregroundState = state;
                }

                // The main window of the game: its rectangle and styles.
                IntPtr main = windows.FindVisibleTopLevelWindow(processId);
                WindowState game = main == IntPtr.Zero ? null : windows.ReadWindow(main);
                if (game == null)
                {
                    if (gameWasThere || first)
                        logger.Info(at + "the main window of " + name + " is not there.");
                }
                else if (gameState == null || gameState.Handle != game.Handle)
                {
                    logger.Info(at + "main window " + WindowState.FormatHandle(game.Handle) + " (class '" + game.ClassName + "'): rectangle " +
                                game.FormatRectangle() + ", " + game.FormatStyles() + ".");
                }
                else
                {
                    if (!game.HasSameRectangle(gameState))
                        logger.Info(at + "main window " + WindowState.FormatHandle(game.Handle) + " rectangle changed from " +
                                    gameState.FormatRectangle() + " to " + game.FormatRectangle() + ".");
                    if (!game.HasSameStyles(gameState))
                        logger.Info(at + "main window " + WindowState.FormatHandle(game.Handle) + " styles changed from " +
                                    gameState.FormatStyles() + " to " + game.FormatStyles() + ".");
                }
                gameState = game;
                gameWasThere = game != null;
                first = false;

                if (signal != null && signal.IsPending)
                {
                    // The foreground window as it is now (read again if the watch could not read it when it came to the front).
                    WindowState inFront = foregroundState ?? (now == IntPtr.Zero ? null : windows.ReadWindow(now));
                    bool responding = game == null || windows.IsWindowResponding(game.Handle);
                    SignalStep step = signal.Observe(clock.Elapsed - started, inFront, game, responding);
                    if (signal.Wait != SignalWait.None &&
                        (signal.Wait != loggedWait || (signal.Wait == SignalWait.Settling && !ReferenceEquals(signal.QuietState, loggedQuietState))))
                    {
                        logger.Info(at + "activation signal waits: " + DescribeWait(signal, inFront) + ".");
                        loggedWait = signal.Wait;
                        loggedQuietState = signal.QuietState;
                    }
                    switch (step)
                    {
                        case SignalStep.Send:
                            SendSignal(signal, game, at, cancellationToken);
                            break;
                        case SignalStep.Expire:
                            signal.Complete(ActivationSignalOutcome.NotSettled);
                            logger.Info(at + "activation signal not sent: the main window of " + name + " was not the foreground window with the " +
                                        "same rectangle and styles for " + FormatSeconds(ActivationSignal.SettleTime) + " within " +
                                        FormatSeconds(ActivationSignal.Deadline) + " of the start (last: " + DescribeLastWait(signal.Wait) + ").");
                            break;
                        case SignalStep.WindowGone:
                            signal.Complete(ActivationSignalOutcome.WindowGone);
                            logger.Info(at + "activation signal not sent: the main window of " + name + " has not been there for " +
                                        FormatSeconds(ActivationSignal.MissingWindowLimit) + ".");
                            break;
                    }
                }

                if (clock.Elapsed - begin >= WatchDuration && (signal == null || !signal.IsPending))
                    break;
                await delay(WatchInterval, cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
            }
            logger.Info("The watch of " + name + " ends after " + FormatSeconds(clock.Elapsed - begin) + ".");
        }

        /// <summary>
        /// Posts the one <c>WM_ACTIVATE</c> to <paramref name="main"/> (A1b) and completes <paramref name="signal"/>. First the
        /// check right before the post: if the launcher is closing nothing is posted (the caller completes the signal as
        /// cancelled); if the foreground window is not the main window any more, or the main window does not respond, the
        /// decision is withdrawn and the signal waits again. Never retried after the post: a refusal (error 5, a game that runs as
        /// administrator: User Interface Privilege Isolation) is logged as a warning with the way out (Alt+Tab) and nothing else
        /// happens; a main window that is gone (error 1400) ends the signal as <see cref="ActivationSignalOutcome.WindowGone"/>.
        /// </summary>
        private void SendSignal(ActivationSignal signal, WindowState main, string at, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WindowState inFront;
            SignalWait? held = CheckBeforePost(main, out inFront);
            if (held.HasValue)
            {
                signal.Withdraw(held.Value);
                logger.Info(at + "activation signal held back right before the post: " + DescribeWait(signal, inFront) + "; the quiet time starts again.");
                return;
            }

            WindowState quiet = signal.QuietState;
            string target = "main window " + WindowState.FormatHandle(main.Handle);
            if (windows.PostActivateMessage(main.Handle, out int error))
            {
                signal.Complete(ActivationSignalOutcome.Sent);
                logger.Info(at + "activation signal sent: WM_ACTIVATE (WA_ACTIVE) posted to " + target + " (class '" + main.ClassName +
                            "'), which was the foreground window with rectangle " + quiet.FormatRectangle() + " for " +
                            FormatElapsed(signal.QuietFor) + ".");
                return;
            }
            if (error == ErrorInvalidWindowHandle)
            {
                signal.Complete(ActivationSignalOutcome.WindowGone);
                logger.Info(at + "activation signal not sent: the " + target + " was gone before the message could be posted.");
                return;
            }
            signal.Complete(ActivationSignalOutcome.SendFailed);
            logger.Warning(at + "activation signal failed: PostMessage(WM_ACTIVATE) to " + target + " returned error " +
                           error.ToString(CultureInfo.InvariantCulture) + " (" + new Win32Exception(error).Message + "). " +
                           (error == ErrorAccessDenied
                               ? "The game probably runs as administrator, and Windows does not let a program without those rights send it messages. "
                               : string.Empty) +
                           "If the mouse does not react, switch to another window and back once (Alt+Tab).");
        }

        /// <summary>
        /// The last look before the post: null if the main window is still the foreground window and responds; else the reason
        /// to wait again (and, for a foreground window that is not the main window, the window in front in <paramref name="inFront"/>).
        /// </summary>
        private SignalWait? CheckBeforePost(WindowState main, out WindowState inFront)
        {
            inFront = null;
            IntPtr front = windows.GetForegroundWindow();
            if (front == IntPtr.Zero)
                return SignalWait.NoForeground;
            if (front != main.Handle)
            {
                inFront = windows.ReadWindow(front);
                return inFront != null && inFront.ProcessId == main.ProcessId
                    ? SignalWait.OtherWindowOfTheGameInFront
                    : SignalWait.OtherProgramInFront;
            }
            return windows.IsWindowResponding(main.Handle) ? (SignalWait?)null : SignalWait.NotResponding;
        }

        /// <summary>The reason the signal waits, for the log line "activation signal waits: ...".</summary>
        private static string DescribeWait(ActivationSignal signal, WindowState inFront)
        {
            switch (signal.Wait)
            {
                case SignalWait.MainWindowMissing:
                    return "the main window is not there";
                case SignalWait.NoForeground:
                    return "no window is in the foreground";
                case SignalWait.OtherWindowOfTheGameInFront:
                    return "the foreground window is " + (inFront == null ? "a window that is gone" : inFront.Describe()) + ", another window of the game";
                case SignalWait.OtherProgramInFront:
                    return "the foreground window is " + (inFront == null ? "a window that is gone" : inFront.Describe()) + ", another program";
                case SignalWait.Minimized:
                    return "the main window is minimized or hidden";
                case SignalWait.NotResponding:
                    return "the main window does not respond";
                default:
                    WindowState quiet = signal.QuietState;
                    return "the main window " + WindowState.FormatHandle(quiet.Handle) + " has rectangle " + quiet.FormatRectangle() + ", " +
                           quiet.FormatStyles() + "; " + FormatSeconds(ActivationSignal.SettleTime) + " without a change are needed";
            }
        }

        /// <summary>The last reason the signal waited, for the line that says it was not sent in time.</summary>
        private static string DescribeLastWait(SignalWait wait)
        {
            switch (wait)
            {
                case SignalWait.MainWindowMissing:
                    return "the main window was not there";
                case SignalWait.NoForeground:
                    return "no window was in the foreground";
                case SignalWait.OtherWindowOfTheGameInFront:
                    return "another window of the game was in front";
                case SignalWait.OtherProgramInFront:
                    return "another program was in front";
                case SignalWait.Minimized:
                    return "the main window was minimized or hidden";
                case SignalWait.NotResponding:
                    return "the main window did not respond";
                case SignalWait.Settling:
                    return "the main window was still changing";
                default:
                    return "the signal never looked";
            }
        }

        private string DescribeForeground(WindowState state, int gameProcessId)
        {
            if (state == null)
                return "none";
            string who = state.ProcessId == gameProcessId ? " (the game)" : state.ProcessId == launcherProcessId ? " (the launcher)" : string.Empty;
            return state.Describe() + who;
        }

        private string Describe(int processId)
        {
            if (processId == 0)
                return "no process";
            string id = "pid " + processId.ToString(CultureInfo.InvariantCulture);
            return processId == launcherProcessId ? id + " (the launcher)" : id;
        }

        private static string FormatWindow(IntPtr window)
        {
            return WindowState.FormatHandle(window);
        }

        private static string FormatMilliseconds(TimeSpan time)
        {
            return ((long)time.TotalMilliseconds).ToString(CultureInfo.InvariantCulture) + " ms";
        }

        private static string FormatElapsed(TimeSpan time)
        {
            return time.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture) + " s";
        }

        private static string FormatSeconds(TimeSpan time)
        {
            return ((long)time.TotalSeconds).ToString(CultureInfo.InvariantCulture) + " s";
        }
    }
}

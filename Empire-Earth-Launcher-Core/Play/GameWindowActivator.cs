using System;
using System.Globalization;
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
    /// Afterwards it only watches, for <see cref="WatchDuration"/>: every change of the foreground window and of the rectangle
    /// and the styles of the main window is logged with the time since the start. That is a measurement, not an action; A1 does
    /// not fix the dgVoodoo case (ADR 0010 amendment).
    /// </para>
    /// <para>
    /// The launcher never steals the foreground: as soon as another process owns it, the hand-over ends. It does not minimize,
    /// hide or move any window, does not wait for the game to end and never ends a process. One line says when the window was
    /// found, what was done and who owned the foreground. The start itself stays a shell start (ADR 0010).
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

        /// <summary>How long the foreground and the window of the game are watched, read-only, after the hand-over.</summary>
        public static readonly TimeSpan WatchDuration = TimeSpan.FromSeconds(60);

        /// <summary>How often the watch reads the foreground window and the window of the game.</summary>
        public static readonly TimeSpan WatchInterval = TimeSpan.FromMilliseconds(250);

        private readonly IWindowSystem windows;
        private readonly IClock clock;
        private readonly ILogger logger;
        private readonly int launcherProcessId;
        private readonly Func<TimeSpan, CancellationToken, Task> delay;

        /// <param name="windows">The foreground and the windows of the computer.</param>
        /// <param name="clock">Measures how long the window took to appear.</param>
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
        /// watches it for <see cref="WatchDuration"/>. The task never throws: a failure is logged and a closing launcher
        /// (<paramref name="cancellationToken"/>) ends it with <see cref="ActivationOutcome.Cancelled"/>. It ends after the
        /// watch; the outcome is that of the hand-over.
        /// </summary>
        /// <param name="processId">The process id of the started game; null if it is not known (logged, nothing else happens).</param>
        /// <param name="game">The game that was started (for the log).</param>
        /// <param name="cancellationToken">Cancelled when the launcher closes.</param>
        public Task<ActivationOutcome> ActivateAsync(int? processId, Game game, CancellationToken cancellationToken = default)
        {
            if (game == null)
                throw new ArgumentNullException(nameof(game));
            if (!processId.HasValue)
            {
                logger.Info("The foreground is not handed to " + game.ProgramName + ": its process id is not known.");
                return Task.FromResult(ActivationOutcome.ProcessIdUnknown);
            }
            return Task.Run(() => ActivateCoreAsync(processId.Value, game, cancellationToken));
        }

        private async Task<ActivationOutcome> ActivateCoreAsync(int processId, Game game, CancellationToken cancellationToken)
        {
            try
            {
                string name = game.ProgramName + " (pid " + processId.ToString(CultureInfo.InvariantCulture) + ")";
                DateTime started = clock.UtcNow;
                ActivationOutcome outcome = await HandOverAsync(processId, name, started, cancellationToken).ConfigureAwait(false);
                // The window may be gone (the game ended) or never came: nothing to watch then.
                if (outcome != ActivationOutcome.NoWindow)
                    await WatchAsync(processId, name, started, cancellationToken).ConfigureAwait(false);
                return outcome;
            }
            catch (OperationCanceledException)
            {
                return ActivationOutcome.Cancelled;
            }
            catch (Exception ex)
            {
                // A background task of a convenience: it must never end the launcher (ADR 0013).
                logger.Warning("The foreground could not be handed to " + game.ProgramName + ".", ex);
                return ActivationOutcome.GaveUp;
            }
        }

        private async Task<ActivationOutcome> HandOverAsync(int processId, string name, DateTime started, CancellationToken cancellationToken)
        {
            // 1. The window: the game needs a moment (and, with a wrapper, a second window) before it shows one.
            IntPtr window;
            while ((window = windows.FindVisibleTopLevelWindow(processId)) == IntPtr.Zero)
            {
                if (clock.UtcNow - started >= FindTimeout)
                {
                    logger.Info("No window of " + name + " within " + FormatSeconds(FindTimeout) + "; the foreground was left alone.");
                    return ActivationOutcome.NoWindow;
                }
                await delay(PollInterval, cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
            }
            string seen = "Game window " + FormatWindow(window) + " of " + name;
            string after = FormatMilliseconds(clock.UtcNow - started);

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
        /// Reads, for <see cref="WatchDuration"/>, which window is in front and where the main window of the game is, and logs
        /// each change with the time since the start. Changes nothing (A1 of 1.1.0: the measurement of when a wrapper switches
        /// the display mode and whether the game loses the foreground).
        /// </summary>
        private async Task WatchAsync(int processId, string name, DateTime started, CancellationToken cancellationToken)
        {
            logger.Info("Watching " + name + " for " + FormatSeconds(WatchDuration) + " (read-only): changes of the foreground window and of " +
                        "the rectangle and the styles of its main window are logged with the time since the start.");
            DateTime begin = clock.UtcNow;
            bool first = true;
            IntPtr foreground = IntPtr.Zero;
            WindowState foregroundState = null;
            WindowState gameState = null;
            bool gameWasThere = false;
            while (true)
            {
                string at = "Watch t+" + FormatElapsed(clock.UtcNow - started) + ": ";

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

                if (clock.UtcNow - begin >= WatchDuration)
                    break;
                await delay(WatchInterval, cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
            }
            logger.Info("The watch of " + name + " ends after " + FormatSeconds(WatchDuration) + ".");
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

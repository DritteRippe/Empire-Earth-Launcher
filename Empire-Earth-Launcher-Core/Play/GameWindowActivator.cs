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
    /// <see cref="FindTimeout"/> for the first visible top-level window of the game process. If the launcher (or the game)
    /// owns the foreground then it calls <c>SetForegroundWindow</c> on it. About <see cref="RecheckInterval"/> later it looks
    /// again (a wrapper such as dgVoodoo may switch the display mode, and Windows may give the foreground back to the
    /// launcher); it hands the window over again at most <see cref="MaxReactivations"/> times while the launcher owns the
    /// foreground.
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
        /// Hands the foreground to the window of the game started with <paramref name="processId"/>, on the thread pool. The
        /// task never throws: a failure is logged and a closing launcher (<paramref name="cancellationToken"/>) ends it with
        /// <see cref="ActivationOutcome.Cancelled"/>.
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

                // 2. Only the launcher (or the game itself) may be in front: if another program is, the player switched.
                int foreground = windows.GetForegroundProcessId();
                if (foreground != processId && foreground != launcherProcessId)
                {
                    logger.Info(seen + " found after " + after + ", not brought to the foreground: skipped, user switched to " +
                                Describe(foreground) + ".");
                    return ActivationOutcome.UserSwitched;
                }
                bool done = windows.SetForegroundWindow(window);
                logger.Info(done
                    ? seen + " brought to the foreground after " + after + " (the foreground was " + Describe(foreground) + ")."
                    : seen + " found after " + after + ", SetForegroundWindow was refused (the foreground is " +
                      Describe(foreground) + ").");

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
                    foreground = windows.GetForegroundProcessId();
                    if (foreground == processId)
                        return ActivationOutcome.GameInForeground;
                    if (foreground != launcherProcessId)
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
                    done = windows.SetForegroundWindow(window);
                    logger.Info("The launcher had the foreground again: window " + FormatWindow(window) + " of " + name +
                                (done ? " brought to the foreground again" : ", SetForegroundWindow was refused again") + " (" +
                                reactivations.ToString(CultureInfo.InvariantCulture) + " of " +
                                MaxReactivations.ToString(CultureInfo.InvariantCulture) + ").");
                }
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

        private string Describe(int processId)
        {
            if (processId == 0)
                return "no process";
            string id = "pid " + processId.ToString(CultureInfo.InvariantCulture);
            return processId == launcherProcessId ? id + " (the launcher)" : id;
        }

        private static string FormatWindow(IntPtr window)
        {
            return "0x" + window.ToInt64().ToString("X", CultureInfo.InvariantCulture);
        }

        private static string FormatMilliseconds(TimeSpan time)
        {
            return ((long)time.TotalMilliseconds).ToString(CultureInfo.InvariantCulture) + " ms";
        }

        private static string FormatSeconds(TimeSpan time)
        {
            return ((long)time.TotalSeconds).ToString(CultureInfo.InvariantCulture) + " s";
        }
    }
}

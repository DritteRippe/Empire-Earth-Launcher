using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Empire_Earth_Launcher.Core.Logging;
using Empire_Earth_WON;

namespace Empire_Earth_Launcher.Core.Lobby
{
    /// <summary>One request of the online player list (the NeoEE status server, <see cref="NeoApiClient"/>).</summary>
    public interface IPlayerListSource
    {
        /// <summary>The server, for the log (<c>host:port</c>).</summary>
        string Endpoint { get; }

        /// <summary>Requests the connected players. Never throws for network or protocol errors.</summary>
        /// <returns>true and the message, or false and the error.</returns>
        bool TryGetConnectedPlayers(out NeoApiClient.ConnectedPlayersMessage message, out Exception error);
    }

    /// <summary><see cref="IPlayerListSource"/> of the configured NeoEE status server.</summary>
    public sealed class NeoPlayerListSource : IPlayerListSource
    {
        private readonly NeoApiClient client;

        public NeoPlayerListSource(NeoApiClient client)
        {
            this.client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public string Endpoint
        {
            get { return client.Endpoint.ToString(); }
        }

        public bool TryGetConnectedPlayers(out NeoApiClient.ConnectedPlayersMessage message, out Exception error)
        {
            return client.TryGetConnectedPlayers(out message, out error);
        }
    }

    /// <summary>What the last request of the player list gave.</summary>
    public enum PlayerListStatus
    {
        /// <summary>The list arrived (<see cref="PlayerListUpdate.Message"/>).</summary>
        Available,

        /// <summary>The server did not answer or answered nonsense (<see cref="PlayerListUpdate.Error"/>); the next request follows.</summary>
        Unavailable,

        /// <summary>The polling itself failed (a programming error, logged) and has ended.</summary>
        Stopped
    }

    /// <summary>One result of the polling, for the Play page.</summary>
    public sealed class PlayerListUpdate : EventArgs
    {
        private PlayerListUpdate(PlayerListStatus status, NeoApiClient.ConnectedPlayersMessage message, Exception error)
        {
            Status = status;
            Message = message;
            Error = error;
        }

        public PlayerListStatus Status { get; }

        /// <summary>The players (<see cref="PlayerListStatus.Available"/>), else null.</summary>
        public NeoApiClient.ConnectedPlayersMessage Message { get; }

        /// <summary>Why the list is missing (<see cref="PlayerListStatus.Unavailable"/>, <see cref="PlayerListStatus.Stopped"/>); may be null.</summary>
        public Exception Error { get; }

        internal static PlayerListUpdate Available(NeoApiClient.ConnectedPlayersMessage message)
        {
            return new PlayerListUpdate(PlayerListStatus.Available, message, null);
        }

        internal static PlayerListUpdate Unavailable(Exception error)
        {
            return new PlayerListUpdate(PlayerListStatus.Unavailable, null, error);
        }

        internal static PlayerListUpdate Stopped(Exception error)
        {
            return new PlayerListUpdate(PlayerListStatus.Stopped, null, error);
        }
    }

    /// <summary>The player list became available or unavailable (for the outage hint of L-WP9, forum report section 8 row 9).</summary>
    public sealed class PlayerListAvailabilityEventArgs : EventArgs
    {
        internal PlayerListAvailabilityEventArgs(bool isAvailable, Exception error)
        {
            IsAvailable = isAvailable;
            Error = error;
        }

        /// <summary>True when the list arrives (again), false at the start of an outage.</summary>
        public bool IsAvailable { get; }

        /// <summary>The error that started the outage; null when available.</summary>
        public Exception Error { get; }
    }

    /// <summary>
    /// Polls the online player list of the NeoEE status server while the Play page exists (ADR 0004): one request, then
    /// <c>Task.Delay</c> of the interval, until it is cancelled or disposed. Replaces the worker thread loop of
    /// the Play page and keeps every fix of the quality review (korr-S3/S5, wart-S5/S6).
    /// </summary>
    /// <remarks>
    /// <para>
    /// No I/O before <see cref="Start"/> (the page creates nothing that connects in its constructor). Each request runs on
    /// the thread pool (<see cref="NeoApiClient"/> is synchronous and limits the connect and the whole exchange). A request
    /// that fails, also by an exception, is reported as <see cref="PlayerListStatus.Unavailable"/> and the polling goes on;
    /// an outage is logged once when it starts and once when the list is back, not at every request. After a cancel or
    /// <see cref="Dispose"/> no result is reported any more, also not of a request that was still running.
    /// </para>
    /// <para>
    /// <see cref="Updated"/> and <see cref="AvailabilityChanged"/> are raised through the <see cref="SynchronizationContext"/>
    /// of the thread that called <see cref="Start"/> (the UI thread), or on the polling thread when there is none (tests).
    /// </para>
    /// </remarks>
    public sealed class PlayerListPoller : IDisposable
    {
        private readonly IPlayerListSource source;
        private readonly TimeSpan interval;
        private readonly ILogger logger;
        private readonly Func<TimeSpan, CancellationToken, Task> delay;
        private readonly CancellationTokenSource stop = new CancellationTokenSource();

        private SynchronizationContext context;
        private bool? available;

        /// <param name="source">The server to ask.</param>
        /// <param name="interval">The delay between two requests.</param>
        /// <param name="logger">Log of the launcher.</param>
        public PlayerListPoller(IPlayerListSource source, TimeSpan interval, ILogger logger)
            : this(source, interval, logger, Task.Delay)
        {
        }

        /// <summary>For tests: with another delay than <see cref="Task.Delay(TimeSpan, CancellationToken)"/>.</summary>
        internal PlayerListPoller(IPlayerListSource source, TimeSpan interval, ILogger logger,
            Func<TimeSpan, CancellationToken, Task> delay)
        {
            if (interval <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(interval), interval, "The poll interval must be positive.");
            this.source = source ?? throw new ArgumentNullException(nameof(source));
            this.interval = interval;
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.delay = delay ?? throw new ArgumentNullException(nameof(delay));
        }

        /// <summary>A result of a request (or the end of the polling by an error).</summary>
        public event EventHandler<PlayerListUpdate> Updated;

        /// <summary>
        /// The first result, the start of an outage ("not available") and the return of the list; the hook for the outage
        /// hint of L-WP9.
        /// </summary>
        public event EventHandler<PlayerListAvailabilityEventArgs> AvailabilityChanged;

        /// <summary>The polling loop; null before <see cref="Start"/>. It ends without an exception after a cancel.</summary>
        public Task Completion { get; private set; }

        /// <summary>Starts the polling; the first request is sent at once.</summary>
        /// <param name="cancellationToken">Ends the polling, like <see cref="Dispose"/>.</param>
        public void Start(CancellationToken cancellationToken = default)
        {
            if (Completion != null)
                throw new InvalidOperationException("The player list polling has already been started.");
            if (stop.IsCancellationRequested)
                throw new ObjectDisposedException(nameof(PlayerListPoller));
            context = SynchronizationContext.Current;
            CancellationToken token = cancellationToken.CanBeCanceled
                ? CancellationTokenSource.CreateLinkedTokenSource(stop.Token, cancellationToken).Token
                : stop.Token;
            Completion = Task.Run(() => RunAsync(token));
        }

        /// <summary>Ends the polling; no result is reported afterwards.</summary>
        public void Dispose()
        {
            stop.Cancel();
        }

        private async Task RunAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    PlayerListUpdate update = await RequestAsync().ConfigureAwait(false);
                    // A request can take seconds; the page may have been closed meanwhile.
                    if (token.IsCancellationRequested)
                        break;
                    Report(update, token);
                    await delay(interval, token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                // Cancelled while waiting: the normal end.
            }
            catch (Exception ex)
            {
                // A loop that must survive every request (ADR 0013) ends only by a programming error of its own; it is
                // logged and shown instead of ending silently (review finding korr-S3).
                logger.Error("The online player list polling stopped unexpectedly.", ex);
                Raise(Updated, PlayerListUpdate.Stopped(ex), token, ignoreCancel: true);
            }
        }

        private async Task<PlayerListUpdate> RequestAsync()
        {
            try
            {
                return await Task.Run(() => source.TryGetConnectedPlayers(out NeoApiClient.ConnectedPlayersMessage message,
                    out Exception error)
                    ? PlayerListUpdate.Available(message)
                    : PlayerListUpdate.Unavailable(error)).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // TryGetConnectedPlayers turns network and protocol errors into a result; anything else is a bug, but it
                // must not end the polling either (review findings korr-S3, wart-S6).
                return PlayerListUpdate.Unavailable(ex);
            }
        }

        /// <summary>Logs a change of the availability once, then reports the result.</summary>
        private void Report(PlayerListUpdate update, CancellationToken token)
        {
            bool nowAvailable = update.Status == PlayerListStatus.Available;
            if (available != nowAvailable)
            {
                if (!nowAvailable)
                    logger.Error("The online player list of " + source.Endpoint + " is unavailable, retrying every " +
                                 ((long)interval.TotalMilliseconds).ToString(CultureInfo.InvariantCulture) + " ms.", update.Error);
                else if (available == false)
                    logger.Info("The online player list is available again.");
                available = nowAvailable;
                Raise(AvailabilityChanged, new PlayerListAvailabilityEventArgs(nowAvailable, update.Error), token, false);
            }
            Raise(Updated, update, token, false);
        }

        /// <summary>
        /// Raises an event through the context of <see cref="Start"/>; each handler on its own, so that a failing handler
        /// neither ends the polling nor keeps the other handlers from running (it is logged).
        /// </summary>
        private void Raise<T>(EventHandler<T> handlers, T args, CancellationToken token, bool ignoreCancel) where T : EventArgs
        {
            if (handlers == null)
                return;
            void Invoke()
            {
                if (token.IsCancellationRequested && !ignoreCancel)
                    return;
                foreach (EventHandler<T> handler in handlers.GetInvocationList())
                {
                    try
                    {
                        handler(this, args);
                    }
                    catch (Exception ex)
                    {
                        logger.Error("A handler of the online player list failed.", ex);
                    }
                }
            }

            if (context == null)
                Invoke();
            else
                context.Post(state => Invoke(), null);
        }
    }
}

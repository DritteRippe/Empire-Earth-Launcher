using System;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Installations;

namespace Empire_Earth_Launcher.Core.Lobby
{
    /// <summary>
    /// Runs the polling of the online player list only while the selected installation is NeoEE (v1.0.0, architecture item 7):
    /// the list belongs to the NeoEE lobby, so with EE (or no installation) the launcher sends no request to the status server.
    /// A <see cref="PlayerListPoller"/> lives once, so this creates a new one each time the selection becomes NeoEE and
    /// disposes it when the selection is another.
    /// </summary>
    /// <remarks>
    /// Use it on the UI thread: the poller reports through the <see cref="System.Threading.SynchronizationContext"/> of the
    /// thread that started it. No request goes out before <see cref="Apply"/> found NeoEE.
    /// </remarks>
    public sealed class PlayerListPolling : IDisposable
    {
        private readonly Func<PlayerListPoller> createPoller;
        private PlayerListPoller poller;
        private bool disposed;

        /// <param name="createPoller">Creates a poller, not started; called each time the polling starts.</param>
        public PlayerListPolling(Func<PlayerListPoller> createPoller)
        {
            this.createPoller = createPoller ?? throw new ArgumentNullException(nameof(createPoller));
        }

        /// <summary>A result of the poller that runs now (see <see cref="PlayerListPoller.Updated"/>).</summary>
        public event EventHandler<PlayerListUpdate> Updated;

        /// <summary>True while the polling runs.</summary>
        public bool IsPolling
        {
            get { return poller != null; }
        }

        /// <summary>True if the player list is polled for <paramref name="installation"/>: it is a NeoEE installation.</summary>
        public static bool ShouldPoll(Installation installation)
        {
            return installation != null && installation.Product == Product.NeoEE;
        }

        /// <summary>
        /// Starts the polling if <paramref name="selected"/> is NeoEE and it does not run yet; ends it if the selection is
        /// another product or none. Calling it again with the same selection changes nothing.
        /// </summary>
        /// <returns>True if the polling runs afterwards.</returns>
        public bool Apply(Installation selected)
        {
            if (disposed)
                return false;
            bool shouldPoll = ShouldPoll(selected);
            if (shouldPoll && poller == null)
            {
                poller = createPoller();
                poller.Updated += OnUpdated;
                poller.Start();
            }
            else if (!shouldPoll)
            {
                Stop();
            }
            return poller != null;
        }

        /// <summary>Ends the polling for good.</summary>
        public void Dispose()
        {
            disposed = true;
            Stop();
        }

        private void Stop()
        {
            PlayerListPoller ending = poller;
            if (ending == null)
                return;
            poller = null;
            ending.Updated -= OnUpdated;
            ending.Dispose();
        }

        private void OnUpdated(object sender, PlayerListUpdate update)
        {
            // A result of a poller that was ended meanwhile is not reported (the poller checks its token); one that
            // belongs to the current poller is passed on.
            if (sender == poller)
                Updated?.Invoke(this, update);
        }
    }
}

using System;
using Empire_Earth_Launcher.Core.Logging;
using Empire_Earth_Launcher.Core.Platform;

namespace Empire_Earth_Launcher.Core.Play
{
    /// <summary>
    /// One launcher per Windows session (ADR 0010): the first launcher holds the mutex
    /// <see cref="MutexName"/>; a second one shows a message and ends.
    /// </summary>
    /// <remarks>
    /// The name is reserved for a future <c>AppMutex</c> of the setup (contract O10: a setup that installs the launcher
    /// closes it before a repair). It is no setup or game mutex, so it never blocks Play or a change.
    /// </remarks>
    public static class SingleInstance
    {
        /// <summary>The mutex of a running launcher, in the session namespace.</summary>
        public const string MutexName = "EmpireEarthCommunityLauncher";

        /// <summary>
        /// Claims the single-instance mutex for this launcher; keep the handle until the launcher ends.
        /// </summary>
        /// <returns>The handle; null if another launcher runs (logged).</returns>
        public static IDisposable TryClaim(IMutexOwner owner, ILogger logger)
        {
            if (owner == null)
                throw new ArgumentNullException(nameof(owner));
            if (logger == null)
                throw new ArgumentNullException(nameof(logger));
            IDisposable handle = owner.TryCreate(MutexName);
            if (handle == null)
                logger.Info("Another Empire Earth Launcher is already running (mutex " + MutexName + "); this one ends.");
            return handle;
        }
    }
}

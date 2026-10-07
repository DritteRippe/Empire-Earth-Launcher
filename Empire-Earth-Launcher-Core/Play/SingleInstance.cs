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
    /// The suite "Empire Earth Community" (contract revision 4, O10) names it in its <c>AppMutex</c>, so that the suite and
    /// its uninstaller do not run while a launcher runs. It is no setup or game mutex, so it never blocks Play or a change.
    /// A second launcher that was started with <c>--product=</c> first hands the product to this one
    /// (<see cref="InstanceForwarder"/>) and says nothing.
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

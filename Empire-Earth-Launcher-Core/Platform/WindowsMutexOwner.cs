using System;
using System.IO;
using System.Threading;
using Empire_Earth_Launcher.Core.Logging;

namespace Empire_Earth_Launcher.Core.Platform
{
    /// <summary>
    /// <see cref="IMutexOwner"/> with <c>new Mutex(false, name, out createdNew)</c>: the mutex is created but never acquired,
    /// because only its existence matters (another launcher, a future <c>AppMutex</c> of the setup, contract O10).
    /// </summary>
    /// <remarks>
    /// <see cref="UnauthorizedAccessException"/> means the mutex exists with an access list that excludes this process (an
    /// elevated launcher of the same session created it): another instance. Any other error cannot tell whether another
    /// instance runs; the launcher then starts anyway (logged), because refusing the start would be worse than two windows.
    /// Checked on real Windows by the test plan.
    /// </remarks>
    public sealed class WindowsMutexOwner : IMutexOwner
    {
        private readonly ILogger logger;

        public WindowsMutexOwner(ILogger logger)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public IDisposable TryCreate(string name)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("A mutex name is required.", nameof(name));
            try
            {
                var mutex = new Mutex(false, name, out bool createdNew);
                if (createdNew)
                    return mutex;
                mutex.Dispose();
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
            catch (Exception ex) when (ex is IOException || ex is WaitHandleCannotBeOpenedException)
            {
                logger.Warning("Unable to create the mutex " + name + "; the launcher starts without the single-instance check.", ex);
                return new NoMutex();
            }
        }

        /// <summary>The handle when no mutex could be created: nothing to release.</summary>
        private sealed class NoMutex : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}

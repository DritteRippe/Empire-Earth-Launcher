using System;
using System.IO;
using System.Security.AccessControl;
using System.Threading;
using Empire_Earth_Launcher.Core.Logging;

namespace Empire_Earth_Launcher.Core.Platform
{
    /// <summary>
    /// <see cref="IMutexProbe"/> with <see cref="Mutex.TryOpenExisting(string, MutexRights, out Mutex)"/> (ADR 0010):
    /// opened = exists; <see cref="UnauthorizedAccessException"/> = exists too (another account or integrity level
    /// created it without access for us, e.g. an elevated setup); not found = does not exist. The opened handle is
    /// closed at once; the mutex is never acquired.
    /// </summary>
    /// <remarks>Checked on real Windows by the test plan (setup running, game running).</remarks>
    public sealed class WindowsMutexProbe : IMutexProbe
    {
        /// <summary>Prefix of the global namespace, which a future setup might use.</summary>
        public const string GlobalPrefix = @"Global\";

        private readonly ILogger logger;

        public WindowsMutexProbe(ILogger logger)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public bool Exists(string name)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("A mutex name is required.", nameof(name));
            return ExistsExactly(name) || ExistsExactly(GlobalPrefix + name);
        }

        private bool ExistsExactly(string fullName)
        {
            try
            {
                if (!Mutex.TryOpenExisting(fullName, MutexRights.Synchronize, out Mutex mutex))
                    return false;
                mutex.Dispose();
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                return true;
            }
            catch (IOException ex)
            {
                // A Win32 error other than "not found": answer "exists", the safe side for every caller (no game
                // start, no change while a setup might run).
                logger.Warning("Unable to check whether the mutex " + fullName + " exists; it is treated as existing.", ex);
                return true;
            }
        }
    }
}

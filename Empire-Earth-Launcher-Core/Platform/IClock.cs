using System;

namespace Empire_Earth_Launcher.Core.Platform
{
    /// <summary>The current time (ADR 0006), so that time stamps in logs and backup names are testable.</summary>
    public interface IClock
    {
        /// <summary>Local time of the computer (log lines, names of backup folders).</summary>
        DateTime Now { get; }

        /// <summary>Coordinated universal time (durations, comparisons with file times).</summary>
        DateTime UtcNow { get; }
    }

    /// <summary>The clock of the computer.</summary>
    public sealed class SystemClock : IClock
    {
        /// <summary>The only instance; the class has no state.</summary>
        public static readonly SystemClock Instance = new SystemClock();

        private SystemClock()
        {
        }

        public DateTime Now
        {
            get { return DateTime.Now; }
        }

        public DateTime UtcNow
        {
            get { return DateTime.UtcNow; }
        }
    }
}

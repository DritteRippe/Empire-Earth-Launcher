using System;
using System.Globalization;
using System.IO;
using System.Text;
using Empire_Earth_Launcher.Core.Logging;

namespace Empire_Earth_Launcher.RealMachineTests.Harness
{
    /// <summary>
    /// The log of the core during the checks, into one file of the work folder and nowhere else: unlike the launcher's
    /// <c>TraceFileLogger</c> it never writes to the console, because the integrity check logs every finding with its expected
    /// and actual SHA-256 (contract 2.5) and the console output of the harness becomes the CI report. The file stays on the
    /// runner and is never uploaded.
    /// </summary>
    internal sealed class FileLogger : ILogger
    {
        private readonly string path;
        private readonly object sync = new object();

        public FileLogger(string path)
        {
            this.path = path ?? throw new ArgumentNullException(nameof(path));
        }

        public void Log(LogLevel level, string message, Exception exception = null)
        {
            string line = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture) + " " + level + " " +
                          message + (exception == null ? string.Empty : " " + exception) + Environment.NewLine;
            lock (sync)
                File.AppendAllText(path, line, new UTF8Encoding(false));
        }
    }
}

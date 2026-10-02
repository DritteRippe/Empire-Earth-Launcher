using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Empire_Earth_Launcher.Core.Logging
{
    /// <summary>
    /// <see cref="ILogger"/> that writes timestamped lines to a log file (and the console) through
    /// <see cref="Trace"/>.
    /// </summary>
    public sealed class TraceFileLogger : ILogger
    {
        /// <summary>
        /// Size above which the log file is trimmed when the launcher starts.
        /// </summary>
        internal const long MaxLogFileBytes = 1024 * 1024; // 1 MiB

        /// <summary>
        /// Number of most recent lines that are kept when the log file is trimmed.
        /// </summary>
        internal const int LinesKeptAfterTrim = 500;

        /// <summary>
        /// Sends all trace output of the process to <paramref name="logFile"/> and the console. Create only
        /// one instance: a second one replaces the listeners of the first.
        /// </summary>
        /// <remarks>
        /// Side effect: all trace listeners of the process (including the default debugger listener) are
        /// removed and replaced by the log file and the console, so every Trace output ends up in the log.
        /// This constructor never throws because of the log file: logging must not prevent the launcher
        /// from starting.
        /// </remarks>
        /// <param name="logFile">Full path of the log file; its folder is created if needed.</param>
        public TraceFileLogger(string logFile)
        {
            Trace.Listeners.Clear();

            CreateLogDirectory(logFile);
            TrimLogFile(logFile);

            var fileListener = new TextWriterTraceListener(logFile);
            fileListener.Name = "Empire Earth Launcher Logger";

            var consoleListener = new ConsoleTraceListener(false);
            consoleListener.TraceOutputOptions = TraceOptions.DateTime;

            Trace.Listeners.Add(fileListener);
            Trace.Listeners.Add(consoleListener);
            Trace.AutoFlush = true;

            Trace.WriteLine("");
        }

        private static void CreateLogDirectory(string logFile)
        {
            try
            {
                string directory = Path.GetDirectoryName(Path.GetFullPath(logFile));
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException ||
                                       ex is NotSupportedException || ex is ArgumentException)
            {
                // The trace listeners are not set up yet, so the console is the only place to report this.
                Console.Error.WriteLine("Unable to create the folder of the log file " + logFile + ": " + ex.Message);
            }
        }

        /// <summary>
        /// When the log is larger than <see cref="MaxLogFileBytes"/>, keep only its last
        /// <see cref="LinesKeptAfterTrim"/> lines.
        /// </summary>
        /// <remarks>
        /// The trimmed log is written to a temporary file first and only then swapped in, and the complete
        /// previous log is kept as "&lt;log_file&gt;.old", so a failure in the middle never loses the log.
        /// Any I/O problem leaves the log untouched instead of aborting the start of the launcher.
        /// Internal for the unit tests, which call it directly: the constructor would also replace the
        /// process-wide trace listeners.
        /// </remarks>
        internal static void TrimLogFile(string logFile)
        {
            string trimmedFile = logFile + ".tmp";
            try
            {
                FileInfo info = new FileInfo(logFile);
                if (!info.Exists || info.Length <= MaxLogFileBytes)
                    return;

                string[] lines = File.ReadAllLines(logFile);
                File.WriteAllLines(trimmedFile, lines.Skip(Math.Max(0, lines.Length - LinesKeptAfterTrim)).ToArray());
                File.Replace(trimmedFile, logFile, logFile + ".old");
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException ||
                                       ex is NotSupportedException || ex is ArgumentException)
            {
                // The trace listeners are not set up yet, so the console is the only place to report this.
                Console.Error.WriteLine("Unable to trim the log file " + logFile + ": " + ex.Message);
                try
                {
                    if (File.Exists(trimmedFile))
                        File.Delete(trimmedFile);
                }
                catch (Exception cleanupEx) when (cleanupEx is IOException || cleanupEx is UnauthorizedAccessException)
                {
                    // Nothing else to do, a stale temporary file is harmless.
                }
            }
        }

        public void Log(LogLevel level, string message, Exception exception = null)
        {
            // ISO 8601 timestamp: independent of the user's culture and sortable.
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            // Exception.ToString() includes the type, the message, inner exceptions and the stack trace.
            string details = exception != null ? Environment.NewLine + exception : string.Empty;
            Trace.WriteLine("[" + timestamp + "] " + level + " : " + message + details);
        }
    }
}

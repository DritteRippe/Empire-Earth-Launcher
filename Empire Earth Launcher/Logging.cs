using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Empire_Earth_Launcher
{
    class Logging
    {
        /// <summary>
        /// Size above which the log file is trimmed when the launcher starts.
        /// </summary>
        private const long MaxLogFileBytes = 1024 * 1024; // 1 MiB

        /// <summary>
        /// Number of most recent lines that are kept when the log file is trimmed.
        /// </summary>
        private const int LinesKeptAfterTrim = 500;

        public enum LogLevel
        {
            Info, Warning, Error
        }

        /// <summary>
        /// Launcher logging, this will redirect the console to log file
        /// <br>Don't call it multiple time or previous the one will not work !</br>
        /// </summary>
        /// <remarks>
        /// Side effect: all trace listeners of the process (including the default debugger listener) are
        /// removed and replaced by the log file and the console, so every Trace output ends up in the log.
        /// This constructor never throws because of the log file: logging must not prevent the launcher
        /// from starting.
        /// </remarks>
        public Logging(string log_file)
        {
            Trace.Listeners.Clear();

            TrimLogFile(log_file);

            TextWriterTraceListener twtl = new TextWriterTraceListener(log_file);
            twtl.Name = "Empire Earth Launcher Logger";

            ConsoleTraceListener ctl = new ConsoleTraceListener(false);
            ctl.TraceOutputOptions = TraceOptions.DateTime;

            Trace.Listeners.Add(twtl);
            Trace.Listeners.Add(ctl);
            Trace.AutoFlush = true;

            Trace.WriteLine("");
        }

        /// <summary>
        /// When the log is larger than <see cref="MaxLogFileBytes"/>, keep only its last
        /// <see cref="LinesKeptAfterTrim"/> lines.
        /// </summary>
        /// <remarks>
        /// The trimmed log is written to a temporary file first and only then swapped in, and the complete
        /// previous log is kept as "&lt;log_file&gt;.old", so a failure in the middle never loses the log.
        /// Any I/O problem leaves the log untouched instead of aborting the start of the launcher.
        /// </remarks>
        private static void TrimLogFile(string logFile)
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

        public void Log(object log, LogLevel level)
        {
            // ISO 8601 timestamp: independent of the user's culture and sortable.
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            Trace.WriteLine("[" + timestamp + "] " + level + " : " + log);
        }

        public void Log(object log)
        {
            Log(log, LogLevel.Info);
        }

        public void Log(object log, Exception ex)
        {
            // Exception.ToString() includes the type, the message, inner exceptions and the stack trace.
            Log(log + Environment.NewLine + (ex != null ? ex.ToString() : "(no exception details)"), LogLevel.Error);
        }
    }
}

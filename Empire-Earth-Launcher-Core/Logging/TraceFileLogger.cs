using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Text;
using Empire_Earth_Launcher.Core.Platform;

namespace Empire_Earth_Launcher.Core.Logging
{
    /// <summary>
    /// <see cref="ILogger"/> that writes timestamped lines to a log file (and the console) through
    /// <see cref="Trace"/>. The log file is shared: every launcher of the user appends to the same file (ADR 0013
    /// amendment of 1.1.1).
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
        /// The characters the writer of the log file buffers: an entry up to this length (an exception with its stack trace
        /// fits easily) reaches the file in one write, so the line of another launcher never lands in the middle of it.
        /// </summary>
        internal const int EntryBufferChars = 16 * 1024;

        private const string FileListenerName = "Empire Earth Launcher Logger";

        private readonly IClock clock;

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
        /// <param name="clock">Source of the time stamps; null for the computer's clock.</param>
        public TraceFileLogger(string logFile, IClock clock = null)
        {
            this.clock = clock ?? SystemClock.Instance;
            Trace.Listeners.Clear();

            CreateLogDirectory(logFile);
            TrimLogFile(logFile);

            TraceListener fileListener = CreateFileListener(logFile);

            var consoleListener = new ConsoleTraceListener(false);
            consoleListener.TraceOutputOptions = TraceOptions.DateTime;

            if (fileListener != null)
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
        /// The listener that appends to <paramref name="logFile"/>, or null when the file cannot be opened (reported on the
        /// console): logging must not prevent the launcher from starting.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Several launchers write into the same file: a second start logs its hand-over to the running launcher and ends
        /// (ADR 0010) while the first one keeps the file open. <c>TextWriterTraceListener(path)</c> opened the file shared for
        /// reading only, so the second launcher could not open it and wrote into a new file "&lt;GUID&gt;log.txt" next to it.
        /// The file is therefore opened here, shared for reading, writing and deleting, and with the right to append only
        /// (<c>FILE_APPEND_DATA</c>): Windows writes every block at the end of the file as it is at that moment, so the lines
        /// of two launchers follow each other instead of overwriting each other.
        /// </para>
        /// <para>
        /// The file stream has no buffer of its own (size 1) and the writer flushes after every entry, so each entry up to
        /// <see cref="EntryBufferChars"/> characters is one write. The text is UTF-8 without a byte order mark, as before.
        /// Internal for the unit tests, which open two listeners on one file: the constructor would replace the trace
        /// listeners of the whole process.
        /// </para>
        /// </remarks>
        internal static TraceListener CreateFileListener(string logFile)
        {
            FileStream stream = null;
            try
            {
                stream = new FileStream(logFile, FileMode.Append, FileSystemRights.AppendData,
                    FileShare.ReadWrite | FileShare.Delete, 1, FileOptions.None);
                var writer = new StreamWriter(stream, new UTF8Encoding(false), EntryBufferChars) { AutoFlush = true };
                return new TextWriterTraceListener(writer, FileListenerName);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException ||
                                       ex is NotSupportedException || ex is ArgumentException)
            {
                stream?.Dispose();
                // The trace listeners are not set up yet, so the console is the only place to report this.
                Console.Error.WriteLine("Unable to open the log file " + logFile + ": " + ex.Message);
                return null;
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
            Trace.WriteLine(FormatLine(clock.Now, level, message, exception));
        }

        /// <summary>One entry of the log: "[yyyy-MM-dd HH:mm:ss] Level : message", then the exception.</summary>
        internal static string FormatLine(DateTime timestamp, LogLevel level, string message, Exception exception)
        {
            // ISO 8601 timestamp: independent of the user's culture (and its calendar) and sortable.
            string time = timestamp.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            // Exception.ToString() includes the type, the message, inner exceptions and the stack trace.
            string details = exception != null ? Environment.NewLine + exception : string.Empty;
            return "[" + time + "] " + level + " : " + message + details;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Empire_Earth_Launcher.Core.Logging;

namespace Empire_Earth_Launcher.Tests.Fakes
{
    /// <summary><see cref="ILogger"/> that keeps every message for the assertions of a test.</summary>
    internal sealed class RecordingLogger : ILogger
    {
        /// <summary>One logged message.</summary>
        public sealed class Entry
        {
            public Entry(LogLevel level, string message, Exception exception)
            {
                Level = level;
                Message = message;
                Exception = exception;
            }

            public LogLevel Level { get; }

            public string Message { get; }

            public Exception Exception { get; }

            public override string ToString()
            {
                return Level + ": " + Message;
            }
        }

        private readonly List<Entry> entries = new List<Entry>();
        private readonly object sync = new object();

        /// <summary>Every message, oldest first.</summary>
        public IReadOnlyList<Entry> Entries
        {
            get
            {
                lock (sync)
                    return entries.ToList();
            }
        }

        /// <summary>"Level: message" of every entry.</summary>
        public IReadOnlyList<string> Messages
        {
            get { return Entries.Select(entry => entry.ToString()).ToList(); }
        }

        /// <summary>The messages of one level.</summary>
        public IReadOnlyList<string> MessagesOf(LogLevel level)
        {
            return Entries.Where(entry => entry.Level == level).Select(entry => entry.Message).ToList();
        }

        public void Log(LogLevel level, string message, Exception exception = null)
        {
            lock (sync)
                entries.Add(new Entry(level, message, exception));
        }
    }
}

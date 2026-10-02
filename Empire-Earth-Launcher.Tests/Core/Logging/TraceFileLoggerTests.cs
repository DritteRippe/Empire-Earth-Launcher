using System;
using Empire_Earth_Launcher.Core.Logging;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.Logging
{
    /// <summary>
    /// The format of a log entry (<see cref="TraceFileLogger.FormatLine"/>). The logger itself is not created: it
    /// would replace the trace listeners of the whole test process.
    /// </summary>
    [TestFixture]
    public class TraceFileLoggerTests
    {
        private static readonly DateTime Time = new DateTime(2026, 10, 2, 8, 5, 9);

        [Test]
        public void FormatLine_HasAnIsoTimestampAndTheLevel()
        {
            Assert.That(TraceFileLogger.FormatLine(Time, LogLevel.Warning, "Something happened", null),
                Is.EqualTo("[2026-10-02 08:05:09] Warning : Something happened"));
        }

        [TestCase("ar-SA")]
        [TestCase("th-TH")]
        [TestCase("de-DE")]
        public void FormatLine_IsIndependentOfTheCultureAndItsCalendar(string culture)
        {
            // ar-SA uses the Hijri calendar and th-TH the Buddhist era by default; the log stays Gregorian.
            using (new CultureScope(culture))
            {
                Assert.That(TraceFileLogger.FormatLine(Time, LogLevel.Info, "x", null), Does.StartWith("[2026-10-02 08:05:09]"));
            }
        }

        [Test]
        public void FormatLine_AppendsTheException()
        {
            var exception = new InvalidOperationException("broken");

            string line = TraceFileLogger.FormatLine(Time, LogLevel.Error, "Failed", exception);

            Assert.That(line, Does.StartWith("[2026-10-02 08:05:09] Error : Failed" + Environment.NewLine));
            Assert.That(line, Does.Contain("System.InvalidOperationException: broken"));
        }

        /// <summary>Sets the current culture of the thread and restores it on dispose.</summary>
        private sealed class CultureScope : IDisposable
        {
            private readonly System.Globalization.CultureInfo previous;

            public CultureScope(string name)
            {
                previous = System.Threading.Thread.CurrentThread.CurrentCulture;
                System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo(name);
            }

            public void Dispose()
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = previous;
            }
        }
    }
}

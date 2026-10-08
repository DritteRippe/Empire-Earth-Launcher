using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Empire_Earth_Launcher.Core.Logging;
using Empire_Earth_Launcher.Tests.TestSupport;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.Logging
{
    /// <summary>
    /// The log file of <see cref="TraceFileLogger"/> is shared (<see cref="TraceFileLogger.CreateFileListener"/>, ADR 0013
    /// amendment of 1.1.1): a second start hands its command line over and ends while the first launcher keeps the file open
    /// (ADR 0010), and its lines belong in the same <c>log.txt</c>, not in a new file "&lt;GUID&gt;log.txt" next to it. The
    /// listeners are used directly: the logger itself would replace the trace listeners of the whole test process.
    /// </summary>
    [TestFixture]
    public class LogFileSharingTests
    {
        /// <summary>
        /// Why the tests of two open listeners do not run under Mono, which ignores the right to append only: two streams write
        /// at their own positions there and overwrite each other. The launcher runs on the .NET Framework of Windows.
        /// </summary>
        private const string MonoIgnoresTheRightToAppend = "Mono ignores FileSystemRights.AppendData";

        private static readonly Encoding Utf8WithoutByteOrderMark = new UTF8Encoding(false);

        private TemporaryDirectory directory;
        private string logFile;

        [SetUp]
        public void SetUp()
        {
            directory = new TemporaryDirectory();
            logFile = directory.Combine("log.txt");
        }

        [TearDown]
        public void TearDown()
        {
            directory.Dispose();
        }

        private static TraceListener Open(string file)
        {
            TraceListener listener = TraceFileLogger.CreateFileListener(file);
            Assert.That(listener, Is.Not.Null, "the log file could not be opened");
            return listener;
        }

        [Test]
        public void AnExistingLog_IsContinuedInUtf8WithoutAByteOrderMark()
        {
            const string earlier = "[2026-10-07 21:00:00] Info : Starting Empire Earth Launcher v1.1.0";
            const string later = "[2026-10-08 09:00:00] Info : Grüße aus München";
            File.WriteAllText(logFile, earlier + Environment.NewLine, Utf8WithoutByteOrderMark);

            using (TraceListener listener = Open(logFile))
                listener.WriteLine(later);

            Assert.That(File.ReadAllBytes(logFile),
                Is.EqualTo(Utf8WithoutByteOrderMark.GetBytes(earlier + Environment.NewLine + later + Environment.NewLine)));
        }

        [Test]
        [Platform(Exclude = "Mono", Reason = MonoIgnoresTheRightToAppend)]
        public void ASecondLauncher_AppendsToTheFileTheFirstOneHoldsOpen_AndCreatesNoOtherFile()
        {
            using (TraceListener first = Open(logFile))
            {
                first.WriteLine("first : Starting Empire Earth Launcher");
                using (TraceListener second = Open(logFile))
                {
                    second.WriteLine("second : Starting Empire Earth Launcher");
                    first.WriteLine("first : A second launcher asked the launcher to come to the front; the selection stays.");
                    second.WriteLine("second : The running launcher took the request to come to the front; this one ends.");
                }
                first.WriteLine("first : still running");
            }

            Assert.That(File.ReadAllLines(logFile), Is.EqualTo(new[]
            {
                "first : Starting Empire Earth Launcher",
                "second : Starting Empire Earth Launcher",
                "first : A second launcher asked the launcher to come to the front; the selection stays.",
                "second : The running launcher took the request to come to the front; this one ends.",
                "first : still running"
            }), "every line at the end of the file, none overwritten");
            Assert.That(Directory.GetFiles(directory.Path), Is.EqualTo(new[] { logFile }), "no \"<GUID>log.txt\" next to it");
        }

        [Test]
        [Platform(Exclude = "Mono", Reason = MonoIgnoresTheRightToAppend)]
        public void ASecondLauncher_DoesNotTrimTheFileTheFirstOneWrites()
        {
            // Larger than the limit: a launcher that starts alone trims it (LogTrimmingTests). While the first launcher writes,
            // the second one cannot read it and leaves it alone, so the file is never renamed to log.txt.old under the first.
            string[] lines = Enumerable.Range(0, 5000).Select(i => i.ToString("D4").PadRight(300, 'x')).ToArray();
            File.WriteAllLines(logFile, lines);
            Assert.That(new FileInfo(logFile).Length, Is.GreaterThan(TraceFileLogger.MaxLogFileBytes));

            using (TraceListener first = Open(logFile))
            {
                Assert.That(() => TraceFileLogger.TrimLogFile(logFile), Throws.Nothing);
                first.WriteLine("first : still writing into log.txt");
            }

            Assert.That(File.ReadAllLines(logFile), Is.EqualTo(lines.Concat(new[] { "first : still writing into log.txt" })));
            Assert.That(Directory.GetFiles(directory.Path), Is.EqualTo(new[] { logFile }), "no log.txt.old, no log.txt.tmp");
        }

        [Test]
        public void ALogThatCannotBeOpened_GivesNoListener_AndNothingIsThrown()
        {
            // A folder in place of the log file: the launcher starts without a log file.
            Directory.CreateDirectory(logFile);
            TraceListener listener = null;

            Assert.That(() => { listener = TraceFileLogger.CreateFileListener(logFile); }, Throws.Nothing);

            Assert.That(listener, Is.Null);
        }
    }
}

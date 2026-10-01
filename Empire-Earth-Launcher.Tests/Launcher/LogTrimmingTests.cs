using System.IO;
using System.Linq;
using Empire_Earth_Launcher.Tests.TestSupport;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Launcher
{
    /// <summary>
    /// Trimming of the launcher log at start-up (<see cref="TraceFileLogger.TrimLogFile"/>). The logger itself
    /// is not created: it would replace the trace listeners of the whole test process.
    /// </summary>
    [TestFixture]
    public class LogTrimmingTests
    {
        private const long MaxBytes = TraceFileLogger.MaxLogFileBytes;
        private const int KeptLines = TraceFileLogger.LinesKeptAfterTrim;

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

        private string TemporaryFile
        {
            get { return logFile + ".tmp"; }
        }

        private string BackupFile
        {
            get { return logFile + ".old"; }
        }

        /// <summary>Lines "0000 xxx...", each <paramref name="lineLength"/> characters long.</summary>
        private static string[] CreateLines(int count, int lineLength)
        {
            return Enumerable.Range(0, count)
                .Select(i => i.ToString("D4").PadRight(lineLength, 'x'))
                .ToArray();
        }

        private string[] WriteLog(int lineCount, int lineLength)
        {
            string[] lines = CreateLines(lineCount, lineLength);
            File.WriteAllLines(logFile, lines);
            return lines;
        }

        [Test]
        public void MissingLog_NothingIsCreated()
        {
            TraceFileLogger.TrimLogFile(logFile);

            Assert.That(Directory.GetFileSystemEntries(directory.Path), Is.Empty);
        }

        [Test]
        public void EmptyLog_IsKept()
        {
            File.WriteAllText(logFile, string.Empty);

            TraceFileLogger.TrimLogFile(logFile);

            Assert.That(new FileInfo(logFile).Length, Is.EqualTo(0));
            Assert.That(Directory.GetFiles(directory.Path), Is.EqualTo(new[] { logFile }));
        }

        [Test]
        public void LogBelowTheLimit_IsKeptEvenWithManyLines()
        {
            string[] lines = WriteLog(2 * KeptLines, 20);
            Assert.That(new FileInfo(logFile).Length, Is.LessThan(MaxBytes));

            TraceFileLogger.TrimLogFile(logFile);

            Assert.That(File.ReadAllLines(logFile), Is.EqualTo(lines));
            Assert.That(BackupFile, Does.Not.Exist);
        }

        [Test]
        public void LogExactlyAtTheLimit_IsKept()
        {
            byte[] content = Enumerable.Repeat((byte)'a', (int)MaxBytes).ToArray();
            File.WriteAllBytes(logFile, content);

            TraceFileLogger.TrimLogFile(logFile);

            Assert.That(File.ReadAllBytes(logFile), Is.EqualTo(content));
            Assert.That(BackupFile, Does.Not.Exist);
        }

        [Test]
        public void LargeLogWithFewerLinesThanKept_KeepsAllLines()
        {
            // Fewer lines than kept but larger than the limit: the old code started at a negative line index
            // and crashed the launcher start (korr-S18, wart-S15).
            int lineCount = KeptLines / 2;
            string[] lines = WriteLog(lineCount, (int)(MaxBytes / lineCount) + 100);
            byte[] original = File.ReadAllBytes(logFile);
            Assert.That(original.Length, Is.GreaterThan(MaxBytes));

            Assert.That(() => TraceFileLogger.TrimLogFile(logFile), Throws.Nothing);

            Assert.That(File.ReadAllLines(logFile), Is.EqualTo(lines));
            Assert.That(File.ReadAllBytes(BackupFile), Is.EqualTo(original));
            Assert.That(TemporaryFile, Does.Not.Exist);
        }

        [Test]
        public void SingleHugeLine_IsKept()
        {
            string[] lines = WriteLog(1, (int)MaxBytes + 10);

            TraceFileLogger.TrimLogFile(logFile);

            Assert.That(File.ReadAllLines(logFile), Is.EqualTo(lines));
            Assert.That(TemporaryFile, Does.Not.Exist);
        }

        [Test]
        public void LargeLog_KeepsTheLastLinesAndThePreviousLogAsBackup()
        {
            string[] lines = WriteLog(10 * KeptLines, 300);
            byte[] original = File.ReadAllBytes(logFile);
            Assert.That(original.Length, Is.GreaterThan(MaxBytes));

            TraceFileLogger.TrimLogFile(logFile);

            Assert.That(File.ReadAllLines(logFile), Is.EqualTo(lines.Skip(lines.Length - KeptLines)));
            Assert.That(File.ReadAllBytes(BackupFile), Is.EqualTo(original));
            Assert.That(TemporaryFile, Does.Not.Exist);
        }

        [Test]
        public void LargeLog_ReplacesAnOlderBackupAndAStaleTemporaryFile()
        {
            File.WriteAllText(BackupFile, "backup of an earlier start");
            File.WriteAllText(TemporaryFile, "left over by an interrupted start");
            string[] lines = WriteLog(10 * KeptLines, 300);
            byte[] original = File.ReadAllBytes(logFile);

            TraceFileLogger.TrimLogFile(logFile);

            Assert.That(File.ReadAllLines(logFile), Is.EqualTo(lines.Skip(lines.Length - KeptLines)));
            Assert.That(File.ReadAllBytes(BackupFile), Is.EqualTo(original));
            Assert.That(TemporaryFile, Does.Not.Exist);
        }

        [Test]
        public void TrimmingFails_LogIsLeftUntouchedAndNothingIsThrown()
        {
            // A folder in place of the temporary file makes writing the trimmed log fail.
            Directory.CreateDirectory(TemporaryFile);
            WriteLog(10 * KeptLines, 300);
            byte[] original = File.ReadAllBytes(logFile);

            Assert.That(() => TraceFileLogger.TrimLogFile(logFile), Throws.Nothing);

            Assert.That(File.ReadAllBytes(logFile), Is.EqualTo(original));
            Assert.That(BackupFile, Does.Not.Exist);
        }
    }
}

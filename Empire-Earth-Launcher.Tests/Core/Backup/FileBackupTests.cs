using System;
using System.IO;
using System.Linq;
using System.Text;
using Empire_Earth_Launcher.Core.Backup;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Tests.Fakes;
using Empire_Earth_Launcher.Tests.TestSupport;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.Backup
{
    /// <summary>
    /// <see cref="FileBackup"/> (ADR 0007: files are moved into a dated backup folder, not deleted): every file is copied and
    /// read back before the first original is removed; a failed copy removes nothing; a file Windows does not let the launcher
    /// remove stays in place and is reported. With the in-memory file system, and once with real files in a temporary folder.
    /// </summary>
    [TestFixture]
    public class FileBackupTests
    {
        private const string Backups = @"C:\Users\Player\AppData\Local\Empire Earth Launcher\Backups";
        private const string Login = @"D:\Games\Empire Earth\_wonlogin.ks";
        private const string Key = @"D:\Games\Empire Earth\_wonkver.pub";

        private FakeClock clock;
        private InMemoryFileSystem fileSystem;
        private RecordingLogger logger;
        private FileBackup backup;

        [SetUp]
        public void SetUp()
        {
            clock = new FakeClock();
            fileSystem = new InMemoryFileSystem(clock);
            logger = new RecordingLogger();
            backup = new FileBackup(fileSystem, new BackupLocations(Backups, fileSystem, clock, logger), logger);
            fileSystem.AddFile(Login, SampleHashes.Content(1));
            fileSystem.AddFile(Key, SampleHashes.Content(2));
        }

        private static FileToBackUp[] Files()
        {
            return new[] { new FileToBackUp(Login, @"EE\_wonlogin.ks"), new FileToBackUp(Key, @"EE\_wonkver.pub") };
        }

        [Test]
        public void MovesTheFiles_IntoADatedFolder_WithAListOfTheOriginalPaths()
        {
            FileBackupResult result = backup.MoveIntoBackup("won-login-reset", Files());

            Assert.That(result.Outcome, Is.EqualTo(FileBackupOutcome.Done));
            Assert.That(WinPath.GetParent(result.Folder), Is.EqualTo(Backups));
            Assert.That(WinPath.GetFileName(result.Folder), Does.Match(@"^\d{4}-\d{2}-\d{2}_\d{6}_won-login-reset$"));
            Assert.That(fileSystem.FileExists(Login), Is.False);
            Assert.That(fileSystem.FileExists(Key), Is.False);
            Assert.That(fileSystem.GetText(result.Folder + @"\EE\_wonlogin.ks"), Is.EqualTo(SampleHashes.Content(1)));
            Assert.That(fileSystem.GetText(result.Folder + @"\EE\_wonkver.pub"), Is.EqualTo(SampleHashes.Content(2)));
            Assert.That(result.Files.Select(file => file.Outcome), Is.All.EqualTo(FileMoveOutcome.Moved));
            Assert.That(result.Files.Select(file => file.BackupPath),
                Is.EqualTo(new[] { result.Folder + @"\EE\_wonlogin.ks", result.Folder + @"\EE\_wonkver.pub" }));

            byte[] index = fileSystem.GetContent(result.Folder + @"\" + FileBackup.IndexFileName);
            Assert.That(index.Take(3), Is.EqualTo(Encoding.UTF8.GetPreamble()));
            Assert.That(Encoding.UTF8.GetString(index, 3, index.Length - 3), Is.EqualTo(
                @"EE\_wonlogin.ks <- " + Login + "\r\n" + @"EE\_wonkver.pub <- " + Key + "\r\n"));
            Assert.That(fileSystem.AllFiles.Where(path => path.EndsWith(FileSystemExtensions.TemporaryFileSuffix, StringComparison.Ordinal)),
                Is.Empty);
        }

        [Test]
        public void ACopyThatFails_RemovesNothing()
        {
            fileSystem.FailOn(Key, FileSystemOperation.Read, FileSystemStatus.AccessDenied);

            FileBackupResult result = backup.MoveIntoBackup("won-login-reset", Files());

            Assert.That(result.Outcome, Is.EqualTo(FileBackupOutcome.BackupFailed));
            Assert.That(fileSystem.FileExists(Login), Is.True, "the first file was copied, but not removed");
            Assert.That(fileSystem.FileExists(Key), Is.True);
            Assert.That(result.Files.Select(file => file.Outcome), Is.All.EqualTo(FileMoveOutcome.NotMoved));
            Assert.That(result.Problem, Does.Contain(Key));
        }

        [TestCase("CreateDirectory")]
        [TestCase("Write")]
        [TestCase("Move")]
        public void ABackupThatCannotBeWritten_RemovesNothing(string operation)
        {
            fileSystem.FailOn(Backups, (FileSystemOperation)Enum.Parse(typeof(FileSystemOperation), operation), FileSystemStatus.IoError);

            FileBackupResult result = backup.MoveIntoBackup("won-login-reset", Files());

            Assert.That(result.Outcome, Is.EqualTo(FileBackupOutcome.BackupFailed));
            Assert.That(fileSystem.FileExists(Login), Is.True);
            Assert.That(fileSystem.FileExists(Key), Is.True);
        }

        [Test]
        public void AFileThatCannotBeRemoved_StaysAndIsReported()
        {
            fileSystem.SetReadOnly(Key, true);

            FileBackupResult result = backup.MoveIntoBackup("won-login-reset", Files());

            Assert.That(result.Outcome, Is.EqualTo(FileBackupOutcome.Partial));
            Assert.That(result.Files.Select(file => file.Outcome), Is.EqualTo(new[] { FileMoveOutcome.Moved, FileMoveOutcome.RemoveDenied }));
            Assert.That(fileSystem.FileExists(Key), Is.True);
            Assert.That(fileSystem.FileExists(result.Files[1].BackupPath), Is.True, "the copy is in the backup");
        }

        [Test]
        public void TheContents_AreNeverLogged()
        {
            fileSystem.AddFile(Login, "SECRET-LOGIN-0000");

            backup.MoveIntoBackup("won-login-reset", Files());

            Assert.That(logger.Messages.Any(message => message.Contains("SECRET-LOGIN")), Is.False);
        }

        [Test]
        public void TheTargets_ArePlainRelativePaths_AndDiffer()
        {
            Assert.That(() => new FileToBackUp(Login, @"..\x"), Throws.ArgumentException);
            Assert.That(() => new FileToBackUp(Login, @"C:\x"), Throws.ArgumentException);
            Assert.That(() => new FileToBackUp(Login, @"EE\\x"), Throws.ArgumentException);
            Assert.That(() => new FileToBackUp("relative", "x"), Throws.ArgumentException);
            Assert.That(() => backup.MoveIntoBackup("won-login-reset", new[] { new FileToBackUp(Login, "x"), new FileToBackUp(Key, "X") }),
                Throws.ArgumentException);
            Assert.That(() => backup.MoveIntoBackup("won-login-reset", new FileToBackUp[0]), Throws.ArgumentException);
        }

        /// <summary>The same move with real files in a temporary folder (through a mapped drive letter).</summary>
        [Test]
        public void MovesRealFiles()
        {
            using (var directory = new TemporaryDirectory())
            {
                var real = new MappedFileSystem("T:", directory.Path);
                directory.CreateFile(Path.Combine("Games", "Empire Earth", "_wonlogin.ks"), "login");
                var realBackup = new FileBackup(real, new BackupLocations(@"T:\Backups", real, clock, logger), logger);

                FileBackupResult result = realBackup.MoveIntoBackup("won-login-reset",
                    new[] { new FileToBackUp(@"T:\Games\Empire Earth\_wonlogin.ks", @"EE\_wonlogin.ks") });

                Assert.That(result.Outcome, Is.EqualTo(FileBackupOutcome.Done), result.Problem);
                Assert.That(File.Exists(directory.Combine(Path.Combine("Games", "Empire Earth", "_wonlogin.ks"))), Is.False);
                Assert.That(File.ReadAllText(real.ToHost(result.Folder + @"\EE\_wonlogin.ks")), Is.EqualTo("login"));
                Assert.That(File.Exists(real.ToHost(result.Folder + @"\" + FileBackup.IndexFileName)), Is.True);
            }
        }
    }
}

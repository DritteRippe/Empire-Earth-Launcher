using System;
using System.Linq;
using Empire_Earth_Launcher.Core.Backup;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Tests.Fakes;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.Backup
{
    /// <summary>
    /// <see cref="BackupLocations"/>: dated folders below <c>Backups</c>, file names with date, time, product and game
    /// (contract 3.6), and a backup that counts only once its bytes are on the disk.
    /// </summary>
    [TestFixture]
    public class BackupLocationsTests
    {
        private const string Directory = @"C:\Users\Player\AppData\Local\Empire Earth Launcher\Backups";

        private FakeClock clock;
        private InMemoryFileSystem fileSystem;
        private RecordingLogger logger;
        private BackupLocations backups;

        [SetUp]
        public void SetUp()
        {
            clock = new FakeClock(new DateTime(2026, 10, 2, 13, 30, 12, DateTimeKind.Utc)); // 15:30:12 local
            fileSystem = new InMemoryFileSystem(clock);
            logger = new RecordingLogger();
            backups = new BackupLocations(Directory, fileSystem, clock, logger);
        }

        [Test]
        public void Names_HaveDateTimeProductAndGame()
        {
            DateTime time = new DateTime(2026, 1, 5, 7, 8, 9);

            Assert.That(BackupLocations.SubfolderName(time, "reset-game-settings"), Is.EqualTo("2026-01-05_070809_reset-game-settings"));
            Assert.That(BackupLocations.GameSettingsFileName(time, Product.NeoEE, Game.ArtOfConquest), Is.EqualTo("2026-01-05_070809_NeoEE_AoC.reg"));
            Assert.That(BackupLocations.GameSettingsFileName(time, Product.EE, Game.EmpireEarth), Is.EqualTo("2026-01-05_070809_EE_EE.reg"));
            Assert.That(() => BackupLocations.SubfolderName(time, @"..\x"), Throws.ArgumentException);
        }

        [Test]
        [SetCulture("ar-SA")]
        public void Names_UseTheGregorianCalendarWhateverTheCulture()
        {
            Assert.That(BackupLocations.TimeStamp(new DateTime(2026, 10, 2, 15, 30, 12)), Is.EqualTo("2026-10-02_153012"));
        }

        [Test]
        public void CreateSubfolder_AddsANumberIfTheFolderExists()
        {
            FileSystemResult<BackupFolder> first = backups.CreateSubfolder("reset-game-settings");
            FileSystemResult<BackupFolder> second = backups.CreateSubfolder("reset-game-settings");

            Assert.That(first.Value.Path, Is.EqualTo(Directory + @"\2026-10-02_153012_reset-game-settings"));
            Assert.That(second.Value.Path, Is.EqualTo(Directory + @"\2026-10-02_153012_reset-game-settings_2"));
            Assert.That(fileSystem.DirectoryExists(second.Value.Path), Is.True);
            Assert.That(first.Value.Time, Is.EqualTo(clock.Now));
        }

        [Test]
        public void CreateSubfolder_ReportsAFailure()
        {
            fileSystem.FailOn(Directory, FileSystemOperation.CreateDirectory, FileSystemStatus.AccessDenied);

            FileSystemResult<BackupFolder> result = backups.CreateSubfolder("reset-game-settings");

            Assert.That(result.Status, Is.EqualTo(FileSystemStatus.AccessDenied));
            Assert.That(logger.MessagesOf(Empire_Earth_Launcher.Core.Logging.LogLevel.Error), Has.Count.EqualTo(1));
        }

        [Test]
        public void WriteRegFile_WritesTheBytesOfTheWriter()
        {
            string folder = backups.CreateSubfolder("reset-game-settings").Value.Path;
            string path = folder + @"\2026-10-02_153012_NeoEE_EE.reg";
            var keys = new[] { new RegFileKey(RegistryLocation.CurrentUser(@"Software\Neo\Empire Earth")).Delete("Wait for VSync") };

            FileSystemResult result = backups.WriteRegFile(path, keys);

            Assert.That(result.IsOk, Is.True, result.ToString());
            Assert.That(fileSystem.GetContent(path), Is.EqualTo(RegFileWriter.ToBytes(keys)));
            Assert.That(fileSystem.AllFiles.Any(file => file.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)), Is.False);
            Assert.That(logger.Messages, Has.Some.Contains("Backup written: " + path));
        }

        [TestCase("Write")]
        [TestCase("Move")]
        [TestCase("Read")]
        public void WriteRegFile_FailsIfTheFileIsNotOnTheDisk(string operationName)
        {
            var operation = (FileSystemOperation)Enum.Parse(typeof(FileSystemOperation), operationName);
            string folder = backups.CreateSubfolder("reset-game-settings").Value.Path;
            fileSystem.FailOn(folder, operation, FileSystemStatus.IoError);

            FileSystemResult result = backups.WriteRegFile(folder + @"\x.reg",
                new[] { new RegFileKey(RegistryLocation.CurrentUser(@"Software\Neo\Empire Earth")).Delete("A") });

            Assert.That(result.IsOk, Is.False);
            Assert.That(logger.MessagesOf(Empire_Earth_Launcher.Core.Logging.LogLevel.Error), Is.Not.Empty);
        }
    }
}

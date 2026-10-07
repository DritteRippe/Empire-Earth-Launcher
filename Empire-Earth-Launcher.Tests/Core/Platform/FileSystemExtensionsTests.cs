using System.Text;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Tests.Fakes;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.Platform
{
    /// <summary>
    /// Reading with a size limit and writing through a temporary file (<see cref="FileSystemExtensions"/>): a
    /// failure in the middle never leaves a half written or missing file (ADR 0005, ADR 0013).
    /// </summary>
    [TestFixture]
    public class FileSystemExtensionsTests
    {
        private const string Folder = @"C:\Users\Player\AppData\Local\Empire Earth Launcher";
        private const string File = Folder + @"\settings.json";
        private const string TemporaryFile = File + ".tmp";

        private InMemoryFileSystem fileSystem;

        [SetUp]
        public void SetUp()
        {
            fileSystem = new InMemoryFileSystem();
            fileSystem.AddDirectory(Folder);
        }

        private static byte[] Bytes(string text)
        {
            return Encoding.UTF8.GetBytes(text);
        }

        [Test]
        public void AtomicWrite_CreatesANewFile()
        {
            Assert.That(fileSystem.WriteAllBytesAtomically(File, Bytes("new")).IsOk, Is.True);

            Assert.That(fileSystem.GetText(File), Is.EqualTo("new"));
            Assert.That(fileSystem.FileExists(TemporaryFile), Is.False);
        }

        [Test]
        public void AtomicWrite_ReplacesAnExistingFile()
        {
            fileSystem.AddFile(File, "old");

            Assert.That(fileSystem.WriteAllBytesAtomically(File, Bytes("new")).IsOk, Is.True);

            Assert.That(fileSystem.GetText(File), Is.EqualTo("new"));
            Assert.That(fileSystem.AllFiles, Is.EqualTo(new[] { File }));
        }

        [Test]
        public void AtomicWrite_WriteOfTheTemporaryFileFails_TheOldFileStays()
        {
            fileSystem.AddFile(File, "old");
            fileSystem.FailOn(TemporaryFile, FileSystemOperation.Write, FileSystemStatus.IoError);

            FileSystemResult result = fileSystem.WriteAllBytesAtomically(File, Bytes("new"));

            Assert.That(result.Status, Is.EqualTo(FileSystemStatus.IoError));
            Assert.That(fileSystem.GetText(File), Is.EqualTo("old"));
            Assert.That(fileSystem.FileExists(TemporaryFile), Is.False);
        }

        [Test]
        public void AtomicWrite_ReplaceFails_TheOldFileStaysAndTheTemporaryFileIsRemoved()
        {
            fileSystem.AddFile(File, "old");
            fileSystem.FailOn(File, FileSystemOperation.Replace, FileSystemStatus.AccessDenied);

            FileSystemResult result = fileSystem.WriteAllBytesAtomically(File, Bytes("new"));

            Assert.That(result.Status, Is.EqualTo(FileSystemStatus.AccessDenied));
            Assert.That(fileSystem.GetText(File), Is.EqualTo("old"));
            Assert.That(fileSystem.FileExists(TemporaryFile), Is.False);
        }

        [Test]
        public void AtomicWrite_ReplacesAStaleTemporaryFile()
        {
            fileSystem.AddFile(TemporaryFile, "left over by a crash");

            Assert.That(fileSystem.WriteAllBytesAtomically(File, Bytes("new")).IsOk, Is.True);

            Assert.That(fileSystem.GetText(File), Is.EqualTo("new"));
            Assert.That(fileSystem.FileExists(TemporaryFile), Is.False);
        }

        [Test]
        public void ReadAllBytes_RespectsTheLimit()
        {
            fileSystem.AddFile(File, "12345");

            Assert.That(fileSystem.ReadAllBytes(File, 5).Value, Is.EqualTo(Bytes("12345")));
            Assert.That(fileSystem.ReadAllBytes(File, 4).Status, Is.EqualTo(FileSystemStatus.TooLarge));
            Assert.That(fileSystem.ReadAllBytes(File + ".missing", 5).Status, Is.EqualTo(FileSystemStatus.NotFound));
        }
    }
}

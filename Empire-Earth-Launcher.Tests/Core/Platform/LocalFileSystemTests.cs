using System.IO;
using System.Linq;
using System.Text;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Tests.TestSupport;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.Platform
{
    /// <summary>
    /// The real file system adapter (<see cref="LocalFileSystem"/>) in a temporary folder: every operation and the
    /// translation of the exceptions into <see cref="FileSystemStatus"/> values. Paths are built with
    /// <see cref="Path"/>, so the tests run on Windows and under Mono.
    /// </summary>
    [TestFixture]
    public class LocalFileSystemTests
    {
        private TemporaryDirectory directory;
        private LocalFileSystem fileSystem;

        [SetUp]
        public void SetUp()
        {
            directory = new TemporaryDirectory();
            fileSystem = new LocalFileSystem();
        }

        [TearDown]
        public void TearDown()
        {
            directory.Dispose();
        }

        [Test]
        public void ExistenceAndInfo()
        {
            string file = directory.CreateFile("a.txt", "abc");

            Assert.That(fileSystem.FileExists(file), Is.True);
            Assert.That(fileSystem.FileExists(directory.Path), Is.False, "a folder is not a file");
            Assert.That(fileSystem.DirectoryExists(directory.Path), Is.True);
            Assert.That(fileSystem.DirectoryExists(file), Is.False);

            FileSystemResult<FileEntry> info = fileSystem.GetFileInfo(file);
            Assert.That(info.IsOk, Is.True);
            Assert.That(info.Value.Length, Is.EqualTo(3));
            Assert.That(info.Value.IsReadOnly, Is.False);
            Assert.That(fileSystem.GetFileInfo(directory.Combine("missing")).Status, Is.EqualTo(FileSystemStatus.NotFound));
        }

        [Test]
        public void OpenRead_ReadsTheFile_AndReportsMissingFiles()
        {
            string file = directory.CreateFile("a.txt", "abc");

            FileSystemResult<Stream> opened = fileSystem.OpenRead(file);
            Assert.That(opened.IsOk, Is.True);
            using (var reader = new StreamReader(opened.Value))
                Assert.That(reader.ReadToEnd(), Is.EqualTo("abc"));

            FileSystemResult<Stream> missing = fileSystem.OpenRead(directory.Combine("missing.txt"));
            Assert.That(missing.Status, Is.EqualTo(FileSystemStatus.NotFound));
            Assert.That(missing.Detail, Is.Not.Empty);
            Assert.That(fileSystem.OpenRead(directory.Combine(Path.Combine("no folder", "x"))).Status,
                Is.EqualTo(FileSystemStatus.NotFound));
        }

        [Test]
        public void GetFilesAndDirectories_AreSortedIgnoringCase()
        {
            directory.CreateFile("b.txt");
            directory.CreateFile("A.txt");
            Directory.CreateDirectory(directory.Combine("sub"));

            Assert.That(fileSystem.GetFiles(directory.Path).Value.Select(Path.GetFileName), Is.EqualTo(new[] { "A.txt", "b.txt" }));
            Assert.That(fileSystem.GetDirectories(directory.Path).Value.Select(Path.GetFileName), Is.EqualTo(new[] { "sub" }));
            Assert.That(fileSystem.GetFiles(directory.Combine("missing")).Status, Is.EqualTo(FileSystemStatus.NotFound));
        }

        [Test]
        public void WriteReplaceMoveDelete()
        {
            string target = directory.Combine("settings.json");
            string other = directory.Combine("other.json");

            Assert.That(fileSystem.CreateDirectory(directory.Combine(Path.Combine("x", "y"))).IsOk, Is.True);
            Assert.That(fileSystem.WriteAllBytes(target, Encoding.UTF8.GetBytes("old")).IsOk, Is.True);
            Assert.That(fileSystem.WriteAllBytes(other, Encoding.UTF8.GetBytes("new")).IsOk, Is.True);

            Assert.That(fileSystem.Replace(other, target).IsOk, Is.True);
            Assert.That(File.ReadAllText(target), Is.EqualTo("new"));
            Assert.That(File.Exists(other), Is.False);

            Assert.That(fileSystem.Move(target, other).IsOk, Is.True);
            Assert.That(File.ReadAllText(other), Is.EqualTo("new"));
            Assert.That(fileSystem.DeleteFile(other).IsOk, Is.True);
            Assert.That(File.Exists(other), Is.False);
        }

        [Test]
        public void Failures_AreStatusValues()
        {
            string existing = directory.CreateFile("a.txt", "a");
            string second = directory.CreateFile("b.txt", "b");

            Assert.That(fileSystem.Move(existing, second).Status, Is.EqualTo(FileSystemStatus.IoError), "target exists");
            Assert.That(fileSystem.DeleteFile(directory.Combine("missing")).Status, Is.EqualTo(FileSystemStatus.NotFound));
            Assert.That(fileSystem.Replace(directory.Combine("missing"), existing).IsOk, Is.False);
            Assert.That(fileSystem.WriteAllBytes(directory.Combine(Path.Combine("no folder", "x")), new byte[1]).Status,
                Is.EqualTo(FileSystemStatus.NotFound));
            Assert.That(fileSystem.WriteAllBytes(directory.Path, new byte[1]).IsOk, Is.False, "a folder cannot be written");
            Assert.That(File.ReadAllText(existing), Is.EqualTo("a"));
            Assert.That(File.ReadAllText(second), Is.EqualTo("b"));
        }

        [Test]
        public void ReadAllBytesAndAtomicWrite_OnTheRealFileSystem()
        {
            string file = directory.Combine("settings.json");

            Assert.That(fileSystem.WriteAllBytesAtomically(file, Encoding.UTF8.GetBytes("first")).IsOk, Is.True);
            Assert.That(fileSystem.WriteAllBytesAtomically(file, Encoding.UTF8.GetBytes("second")).IsOk, Is.True);

            Assert.That(Encoding.UTF8.GetString(fileSystem.ReadAllBytes(file, 100).Value), Is.EqualTo("second"));
            Assert.That(fileSystem.ReadAllBytes(file, 3).Status, Is.EqualTo(FileSystemStatus.TooLarge));
            Assert.That(Directory.GetFiles(directory.Path), Is.EqualTo(new[] { file }), "no temporary file is left");
        }
    }
}

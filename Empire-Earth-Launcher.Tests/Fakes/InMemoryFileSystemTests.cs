using System.IO;
using Empire_Earth_Launcher.Core.Platform;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Fakes
{
    /// <summary>
    /// The in-memory file system behaves like the Windows file system where the core relies on it; later tests
    /// build on that, so the fake has its own tests.
    /// </summary>
    [TestFixture]
    public class InMemoryFileSystemTests
    {
        private InMemoryFileSystem fileSystem;

        [SetUp]
        public void SetUp()
        {
            fileSystem = new InMemoryFileSystem();
        }

        [Test]
        public void Paths_AreCaseInsensitive_AndAcceptBothSeparators()
        {
            fileSystem.AddFile(@"C:\Games\Empire Earth\Data\Game.cfg", "x");

            Assert.That(fileSystem.FileExists(@"c:\games\empire earth\data\GAME.CFG"), Is.True);
            Assert.That(fileSystem.FileExists("C:/Games//Empire Earth/Data/Game.cfg"), Is.True);
            Assert.That(fileSystem.DirectoryExists(@"C:\GAMES\Empire Earth\"), Is.True);
            Assert.That(fileSystem.FileExists(@"C:\Games\Empire Earth"), Is.False, "a folder is not a file");
            Assert.That(fileSystem.AllFiles, Is.EqualTo(new[] { @"C:\Games\Empire Earth\Data\Game.cfg" }), "original spelling kept");
        }

        [Test]
        public void InvalidPaths_AreReported()
        {
            Assert.That(fileSystem.OpenRead(@"Games\x").Status, Is.EqualTo(FileSystemStatus.InvalidPath));
            Assert.That(fileSystem.OpenRead(@"C:\Games\x?.cfg").Status, Is.EqualTo(FileSystemStatus.InvalidPath));
            Assert.That(fileSystem.OpenRead(@"C:\Games\x.cfg:stream").Status, Is.EqualTo(FileSystemStatus.InvalidPath));
            Assert.That(fileSystem.FileExists("relative"), Is.False);
        }

        [Test]
        public void OpenRead_CountsOpens()
        {
            fileSystem.AddFile(@"C:\a.txt", "abc");

            using (Stream stream = fileSystem.OpenRead(@"C:\A.TXT").Value)
                Assert.That(new StreamReader(stream).ReadToEnd(), Is.EqualTo("abc"));
            fileSystem.OpenRead(@"C:\a.txt").Value.Dispose();
            fileSystem.OpenRead(@"C:\missing.txt");

            Assert.That(fileSystem.OpenCount(@"C:\a.txt"), Is.EqualTo(2));
            Assert.That(fileSystem.TotalOpenCount, Is.EqualTo(2), "failed opens are not counted");
        }

        [Test]
        public void InjectedFaults_ApplyToThePathAndBelow()
        {
            fileSystem.AddFile(@"C:\Games\EE\a.cfg", "a");
            fileSystem.AddFile(@"C:\Games\Other\b.cfg", "b");
            fileSystem.FailOn(@"C:\Games\EE", FileSystemOperation.Read, FileSystemStatus.AccessDenied);

            Assert.That(fileSystem.OpenRead(@"C:\Games\EE\a.cfg").Status, Is.EqualTo(FileSystemStatus.AccessDenied));
            Assert.That(fileSystem.OpenRead(@"C:\Games\Other\b.cfg").IsOk, Is.True);
            Assert.That(fileSystem.GetFileInfo(@"C:\Games\EE\a.cfg").IsOk, Is.True, "only the injected operation fails");

            fileSystem.ClearFaults();
            Assert.That(fileSystem.OpenRead(@"C:\Games\EE\a.cfg").IsOk, Is.True);
        }

        [Test]
        public void Write_NeedsAnExistingFolder_AndRespectsReadOnly()
        {
            Assert.That(fileSystem.WriteAllBytes(@"C:\Missing\a.txt", new byte[1]).Status, Is.EqualTo(FileSystemStatus.NotFound));
            Assert.That(fileSystem.CreateDirectory(@"C:\Missing\Deeper").IsOk, Is.True);
            Assert.That(fileSystem.WriteAllBytes(@"C:\Missing\a.txt", new byte[1]).IsOk, Is.True);

            fileSystem.AddFile(@"C:\ro.txt", "x", readOnly: true);
            Assert.That(fileSystem.WriteAllBytes(@"C:\ro.txt", new byte[1]).Status, Is.EqualTo(FileSystemStatus.AccessDenied));
            Assert.That(fileSystem.DeleteFile(@"C:\ro.txt").Status, Is.EqualTo(FileSystemStatus.AccessDenied));
            Assert.That(fileSystem.GetFileInfo(@"C:\ro.txt").Value.IsReadOnly, Is.True);
            Assert.That(fileSystem.WriteAllBytes(@"C:\Missing", new byte[1]).Status, Is.EqualTo(FileSystemStatus.AccessDenied), "a folder");
            Assert.That(fileSystem.CreateDirectory(@"D:\x").Status, Is.EqualTo(FileSystemStatus.NotFound), "no drive D:");
        }

        [Test]
        public void MoveAndReplace_BehaveLikeNtfs()
        {
            fileSystem.AddFile(@"C:\a.txt", "a");
            fileSystem.AddFile(@"C:\b.txt", "b");

            Assert.That(fileSystem.Move(@"C:\a.txt", @"C:\b.txt").Status, Is.EqualTo(FileSystemStatus.IoError), "target exists");
            Assert.That(fileSystem.Replace(@"C:\a.txt", @"C:\c.txt").Status, Is.EqualTo(FileSystemStatus.NotFound), "no target");
            Assert.That(fileSystem.Replace(@"C:\a.txt", @"C:\b.txt").IsOk, Is.True);
            Assert.That(fileSystem.GetText(@"C:\b.txt"), Is.EqualTo("a"));
            Assert.That(fileSystem.FileExists(@"C:\a.txt"), Is.False);
            Assert.That(fileSystem.Move(@"C:\b.txt", @"C:\c.txt").IsOk, Is.True);
            Assert.That(fileSystem.AllFiles, Is.EqualTo(new[] { @"C:\c.txt" }));
        }

        [Test]
        public void Enumerate_ListsDirectChildrenOnly()
        {
            fileSystem.AddFile(@"C:\Games\EE\a.txt", "a");
            fileSystem.AddFile(@"C:\Games\EE\Data\b.txt", "b");
            fileSystem.AddFile(@"C:\Games\EE2\c.txt", "c");

            Assert.That(fileSystem.GetFiles(@"C:\games\ee").Value, Is.EqualTo(new[] { @"C:\Games\EE\a.txt" }));
            Assert.That(fileSystem.GetDirectories(@"C:\Games").Value, Is.EqualTo(new[] { @"C:\Games\EE", @"C:\Games\EE2" }));
            Assert.That(fileSystem.GetFiles(@"C:\Nothing").Status, Is.EqualTo(FileSystemStatus.NotFound));
        }

        [Test]
        public void FileTimes_ComeFromTheClock()
        {
            var clock = new FakeClock();
            var timed = new InMemoryFileSystem(clock);
            timed.AddDirectory(@"C:\x");
            clock.Advance(System.TimeSpan.FromMinutes(5));

            timed.WriteAllBytes(@"C:\x\a.txt", new byte[3]);

            Assert.That(timed.GetFileInfo(@"C:\x\a.txt").Value.LastWriteTimeUtc, Is.EqualTo(clock.UtcNow));
        }
    }
}

using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Tests.Fakes;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.Installations
{
    /// <summary>How the discovery and the folder dialog recognize EE folders, AoC folders and install roots.</summary>
    [TestFixture]
    public class GameFoldersTests
    {
        private InMemoryFileSystem fileSystem;

        [SetUp]
        public void SetUp()
        {
            fileSystem = new InMemoryFileSystem();
            fileSystem.AddFile(@"C:\Root\Empire Earth\Empire Earth.exe", "exe");
            fileSystem.AddFile(@"C:\Root\Empire Earth - The Art of Conquest\EE-AOC.exe", "exe");
            fileSystem.AddDirectory(@"C:\Portable\_setupdata_NeoEE");
            fileSystem.AddDirectory(@"C:\Empty");
        }

        [TestCase(@"C:\Root\Empire Earth", GameFolderKind.EmpireEarthFolder)]
        [TestCase(@"c:\root\empire earth\", GameFolderKind.EmpireEarthFolder)]
        [TestCase(@"C:\Root\Empire Earth - The Art of Conquest", GameFolderKind.ArtOfConquestFolder)]
        [TestCase(@"C:\Root", GameFolderKind.InstallRoot)]
        [TestCase(@"C:\Portable", GameFolderKind.InstallRoot)]
        [TestCase(@"C:\Empty", GameFolderKind.None)]
        [TestCase(@"C:\Missing", GameFolderKind.None)]
        [TestCase(@"Root\Empire Earth", GameFolderKind.None)]
        [TestCase("", GameFolderKind.None)]
        [TestCase(null, GameFolderKind.None)]
        public void Classify(string folder, GameFolderKind expected)
        {
            Assert.That(GameFolders.Classify(fileSystem, folder), Is.EqualTo(expected));
        }

        [Test]
        public void ContainsProgram_ChecksTheProgramOfTheGame()
        {
            Assert.That(GameFolders.ContainsProgram(fileSystem, @"C:\Root\Empire Earth", Game.EmpireEarth), Is.True);
            Assert.That(GameFolders.ContainsProgram(fileSystem, @"C:\Root\Empire Earth", Game.ArtOfConquest), Is.False);
            Assert.That(GameFolders.ContainsProgram(fileSystem, @"C:\Root\Empire Earth - The Art of Conquest", Game.ArtOfConquest),
                Is.True);
            Assert.That(() => GameFolders.ContainsProgram(null, @"C:\Root", Game.EmpireEarth), Throws.ArgumentNullException);
            Assert.That(() => GameFolders.ContainsProgram(fileSystem, @"C:\Root", null), Throws.ArgumentNullException);
        }
    }
}

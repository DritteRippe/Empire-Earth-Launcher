using System.Linq;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Tests.TestSupport;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.Installations
{
    /// <summary>
    /// The cases of the old <c>GameDirectoryLocatorTests</c> (launcher, until L-WP4), ported one by one to the discovery of
    /// the core with the same names, so that none is lost. The old locator returned one folder (the EE folder) and its
    /// source; the discovery returns every installation and the selected one, whose <see cref="Installation.EeFolder"/> is
    /// that folder. With the in-memory registry and file system the four cases that ran on Windows only
    /// (<c>[Platform("Win")]</c>) now run everywhere.
    /// </summary>
    [TestFixture]
    public class GameDirectoryLocatorPortTests
    {
        private const string GameDirectory = @"C:\Users\Player\Games\Empire Earth";

        private InstallationWorld world;

        [SetUp]
        public void SetUp()
        {
            world = new InstallationWorld();
            world.FileSystem.AddDirectory(GameDirectory);
        }

        /// <summary>A registered installation the user choice must win against.</summary>
        private void RegisterOtherInstallation()
        {
            world.AddForeignInstallation(@"C:\GAMES\Empire Earth");
        }

        [Test]
        public void Locate_UserDirectory_WinsAndIsTrimmed()
        {
            RegisterOtherInstallation();

            DiscoveryResult result = world.Discover("  " + GameDirectory + "  ");

            Assert.That(result.Selected.EeFolder, Is.EqualTo(GameDirectory));
            Assert.That(result.IsSelectedByUser, Is.True);
            Assert.That(result.UserChoice, Is.EqualTo(GameDirectory));
            Assert.That(result.Selected.Origin, Is.EqualTo(InstallationSource.UserChoice));
            // New: the registered installation is still listed (contract 1.4: the launcher shows every installation).
            Assert.That(result.Installations, Has.Count.EqualTo(2));
            Assert.That(result.Installations[0], Is.SameAs(result.Selected));
        }

        [Test]
        public void Locate_UserDirectoryThatNoLongerExists_IsStillReported()
        {
            string missing = WinPath.Combine(GameDirectory, "removed");

            DiscoveryResult result = world.Discover(missing);

            Assert.That(result.Selected.EeFolder, Is.EqualTo(missing));
            Assert.That(result.IsSelectedByUser, Is.True);
            Assert.That(result.Selected.State, Is.EqualTo(InstallationState.FolderMissing));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void Locate_NothingConfiguredOrRegistered_ReturnsNull(string userDirectory)
        {
            // The launcher folder does not contain Empire Earth.exe either.
            DiscoveryResult result = world.Discover(userDirectory, @"C:\Tools\Launcher");

            Assert.That(result.Selected, Is.Null);
            Assert.That(result.Installations, Is.Empty);
            Assert.That(result.IsSelectedByUser, Is.False);
            Assert.That(result.UserChoice, Is.Null);
        }

        [Test]
        public void Locate_RegisteredFolderThatDoesNotExist_IsIgnored()
        {
            world.SetInstalledFrom(InstallationWorld.Hkcu(@"Software\Neo\Empire Earth"), @"C:\Empire Earth 1d0c\Empire Earth");

            DiscoveryResult result = world.Discover();

            Assert.That(result.Selected, Is.Null);
            Assert.That(world.LogLinesAbout(@"HKCU\Software\Neo\Empire Earth"), Has.Length.EqualTo(1));
        }

        [Test]
        public void Locate_RegisteredFolder_IsJoinedFromVolumeAndDirectory()
        {
            world.AddEmpireEarth(GameDirectory);
            world.SetInstalledFrom(InstallationWorld.Hkcu(@"Software\SSSI\Empire Earth"), GameDirectory);

            DiscoveryResult result = world.Discover();

            Assert.That(result.Selected.EeFolder, Is.EqualTo(GameDirectory).IgnoreCase);
            Assert.That(result.Selected.Origin, Is.EqualTo(InstallationSource.InstalledFrom));
            Assert.That(result.IsSelectedByUser, Is.False);
        }

        [TestCase(null, @"\GAMES\Empire Earth\")]
        [TestCase("C:", null)]
        [TestCase("  ", @"\GAMES\Empire Earth\")]
        [TestCase("C:", "")]
        public void CombineInstallLocation_MissingValue_ReturnsNull(string volume, string directory)
        {
            Assert.That(InstalledFromReader.CombineInstallLocation(volume, directory), Is.Null);
        }

        [TestCase("C:", @"\GAMES\Empire Earth\", @"C:\GAMES\Empire Earth")]
        [TestCase(@"C:\", @"GAMES\Empire Earth", @"C:\GAMES\Empire Earth")]
        [TestCase(" D: ", @" \PROGRAM FILES (X86)\NEO EMPIRE EARTH\Empire Earth\ ", @"D:\PROGRAM FILES (X86)\NEO EMPIRE EARTH\Empire Earth")]
        public void CombineInstallLocation_JoinsDriveAndFolder(string volume, string directory, string expected)
        {
            // Path.Combine("C:", @"\GAMES") would give "\GAMES" without the drive; WinPath is the same on every platform.
            Assert.That(InstalledFromReader.CombineInstallLocation(volume, directory), Is.EqualTo(expected));
        }

        [Test]
        public void IsGameDirectory_NeedsTheGameExecutable()
        {
            Assert.That(GameFolders.IsEmpireEarthFolder(world.FileSystem, GameDirectory), Is.False);

            world.FileSystem.AddFile(WinPath.Combine(GameDirectory, Game.EmpireEarth.ProgramName), "exe");

            Assert.That(GameFolders.IsEmpireEarthFolder(world.FileSystem, GameDirectory), Is.True);
            Assert.That(GameFolders.IsEmpireEarthFolder(world.FileSystem, null), Is.False);
            Assert.That(GameFolders.IsEmpireEarthFolder(world.FileSystem, string.Empty), Is.False);
        }

        [Test]
        public void ThePortKeepsEveryCaseOfTheOldTests()
        {
            // The names of the old fixture (git show 4e4f817:Empire-Earth-Launcher.Tests/Launcher/GameDirectoryLocatorTests.cs).
            string[] oldNames =
            {
                "Locate_UserDirectory_WinsAndIsTrimmed", "Locate_UserDirectoryThatNoLongerExists_IsStillReported",
                "Locate_NothingConfiguredOrRegistered_ReturnsNull", "Locate_RegisteredFolderThatDoesNotExist_IsIgnored",
                "Locate_RegisteredFolder_IsJoinedFromVolumeAndDirectory", "CombineInstallLocation_MissingValue_ReturnsNull",
                "CombineInstallLocation_JoinsDriveAndFolder", "IsGameDirectory_NeedsTheGameExecutable"
            };
            string[] names = typeof(GameDirectoryLocatorPortTests).GetMethods().Select(method => method.Name).ToArray();

            Assert.That(names, Is.SupersetOf(oldNames));
        }
    }
}

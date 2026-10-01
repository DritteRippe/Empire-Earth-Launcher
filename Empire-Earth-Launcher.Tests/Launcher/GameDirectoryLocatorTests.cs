using System.IO;
using Empire_Earth_Launcher.Tests.TestSupport;
using Microsoft.Win32;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Launcher
{
    /// <summary>
    /// Detection of the Empire Earth folder, with a fake registry: the real registry is never read.
    /// </summary>
    [TestFixture]
    public class GameDirectoryLocatorTests
    {
        /// <summary>Returns the given install location for every hive, view and key.</summary>
        private sealed class FakeRegistryLocator : GameDirectoryLocator
        {
            private readonly string volume;
            private readonly string directory;

            public FakeRegistryLocator(string volume = null, string directory = null)
            {
                this.volume = volume;
                this.directory = directory;
            }

            public int Reads { get; private set; }

            protected override string ReadRegistryValue(RegistryHive hive, RegistryView view, string keyName, string valueName)
            {
                Reads++;
                if (valueName == VolumeValueName)
                    return volume;
                return valueName == DirectoryValueName ? directory : null;
            }
        }

        private TemporaryDirectory gameDirectory;

        [SetUp]
        public void SetUp()
        {
            gameDirectory = new TemporaryDirectory();
        }

        [TearDown]
        public void TearDown()
        {
            gameDirectory.Dispose();
        }

        [Test]
        public void Locate_UserDirectory_WinsAndIsTrimmed()
        {
            var locator = new FakeRegistryLocator("C:", @"\GAMES\Empire Earth\");
            GameDirectorySource source;

            string location = locator.Locate("  " + gameDirectory.Path + "  ", out source);

            Assert.That(location, Is.EqualTo(gameDirectory.Path));
            Assert.That(source, Is.EqualTo(GameDirectorySource.UserSetting));
            Assert.That(locator.Reads, Is.EqualTo(0));
        }

        [Test]
        public void Locate_UserDirectoryThatNoLongerExists_IsStillReported()
        {
            string missing = gameDirectory.Combine("removed");
            GameDirectorySource source;

            string location = new FakeRegistryLocator().Locate(missing, out source);

            Assert.That(location, Is.EqualTo(missing));
            Assert.That(source, Is.EqualTo(GameDirectorySource.UserSetting));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void Locate_NothingConfiguredOrRegistered_ReturnsNull(string userDirectory)
        {
            // The test program's folder does not contain Empire Earth.exe either.
            var locator = new FakeRegistryLocator();
            GameDirectorySource source;

            string location = locator.Locate(userDirectory, out source);

            Assert.That(location, Is.Null);
            Assert.That(source, Is.EqualTo(GameDirectorySource.NotFound));
            Assert.That(locator.Reads, Is.GreaterThan(0));
        }

        [Test]
        public void Locate_RegisteredFolderThatDoesNotExist_IsIgnored()
        {
            GameDirectorySource source;

            string location = new FakeRegistryLocator("C:", @"\Empire Earth " + System.Guid.NewGuid().ToString("N") + @"\")
                .Locate(null, out source);

            Assert.That(location, Is.Null);
            Assert.That(source, Is.EqualTo(GameDirectorySource.NotFound));
        }

        [Test]
        [Platform(Include = "Win", Reason = "The registry values use Windows drive letters and backslashes.")]
        public void Locate_RegisteredFolder_IsJoinedFromVolumeAndDirectory()
        {
            string root = Path.GetPathRoot(gameDirectory.Path); // e.g. "C:\"
            string volume = root.TrimEnd('\\');
            string directory = gameDirectory.Path.Substring(volume.Length) + @"\";
            GameDirectorySource source;

            string location = new FakeRegistryLocator(volume, directory).Locate(null, out source);

            Assert.That(location, Is.EqualTo(gameDirectory.Path).IgnoreCase);
            Assert.That(source, Is.EqualTo(GameDirectorySource.Registry));
        }

        [TestCase(null, @"\GAMES\Empire Earth\")]
        [TestCase("C:", null)]
        [TestCase("  ", @"\GAMES\Empire Earth\")]
        [TestCase("C:", "")]
        public void CombineInstallLocation_MissingValue_ReturnsNull(string volume, string directory)
        {
            Assert.That(GameDirectoryLocator.CombineInstallLocation(volume, directory), Is.Null);
        }

        [TestCase("C:", @"\GAMES\Empire Earth\", @"C:\GAMES\Empire Earth")]
        [TestCase(@"C:\", @"GAMES\Empire Earth", @"C:\GAMES\Empire Earth")]
        [TestCase(" D: ", @" \PROGRAM FILES (X86)\NEO EMPIRE EARTH\Empire Earth\ ", @"D:\PROGRAM FILES (X86)\NEO EMPIRE EARTH\Empire Earth")]
        [Platform(Include = "Win", Reason = "The registry values use Windows drive letters and backslashes.")]
        public void CombineInstallLocation_JoinsDriveAndFolder(string volume, string directory, string expected)
        {
            // Path.Combine("C:", @"\GAMES") would give "\GAMES" without the drive.
            Assert.That(GameDirectoryLocator.CombineInstallLocation(volume, directory), Is.EqualTo(expected));
        }

        [Test]
        public void IsGameDirectory_NeedsTheGameExecutable()
        {
            Assert.That(GameDirectoryLocator.IsGameDirectory(gameDirectory.Path), Is.False);

            gameDirectory.CreateFile(GameDirectoryLocator.GameExecutableName);

            Assert.That(GameDirectoryLocator.IsGameDirectory(gameDirectory.Path), Is.True);
            Assert.That(GameDirectoryLocator.IsGameDirectory(null), Is.False);
            Assert.That(GameDirectoryLocator.IsGameDirectory(string.Empty), Is.False);
        }
    }
}

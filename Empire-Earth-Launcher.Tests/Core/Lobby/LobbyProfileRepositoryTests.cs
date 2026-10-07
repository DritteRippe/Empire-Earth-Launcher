using System;
using System.Collections.Generic;
using System.Linq;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Lobby;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Tests.Fakes;
using Empire_Earth_Launcher.Tests.Won;
using Empire_Earth_WON;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.Lobby
{
    /// <summary>
    /// Lobby profiles and friends as the launcher's start page loads them (<see cref="LobbyProfileRepository"/>).
    /// A missing, empty or damaged lobby file must never stop the launcher (korr-S1): it is reported as a status.
    /// In a game folder below Program Files the files of the VirtualStore come first (ADR 0016).
    /// </summary>
    [TestFixture]
    public class LobbyProfileRepositoryTests
    {
        private const string VirtualStore = @"C:\Users\Player\AppData\Local\VirtualStore";

        /// <summary>A game folder UAC does not virtualize.</summary>
        private string gameDirectory = @"C:\Games\Empire Earth";

        private InMemoryFileSystem fileSystem;
        private RecordingLogger logger;
        private LobbyProfileRepository repository;

        [SetUp]
        public void SetUp()
        {
            gameDirectory = @"C:\Games\Empire Earth";
            fileSystem = new InMemoryFileSystem();
            fileSystem.AddDirectory(gameDirectory);
            logger = new RecordingLogger();
            repository = new LobbyProfileRepository(logger, new WriteForbiddingFileSystem(fileSystem),
                new EffectivePathResolver(fileSystem, VirtualStore, new[] { @"C:\Program Files", @"C:\Program Files (x86)" }));
        }

        private void WriteGameFile(string fileName, byte[] content)
        {
            fileSystem.AddFile(WinPath.Combine(gameDirectory, fileName), content);
        }

        private IList<LobbyPersistentData.LobbyGlobalData.PlayerInfoGlobalData> LoadProfiles(LobbyProfilesStatus expected)
        {
            IList<LobbyPersistentData.LobbyGlobalData.PlayerInfoGlobalData> profiles;
            Assert.That(repository.LoadProfiles(gameDirectory, out profiles), Is.EqualTo(expected));
            Assert.That(profiles, Is.Not.Null);
            return profiles;
        }

        [Test]
        public void Profiles_AreSortedByLastUse()
        {
            // GlobalFile gives each later profile a later LastUse.
            WriteGameFile(LobbyPersistentData.GlobalDataFileName, LobbyFileBuilder.GlobalFile(true, true, 1, "Old", "Newer", "Newest"));

            var profiles = LoadProfiles(LobbyProfilesStatus.Loaded);

            Assert.That(profiles.Select(profile => profile.Username), Is.EqualTo(new[] { "Newest", "Newer", "Old" }));
            Assert.That(logger.Messages, Is.Empty);
        }

        [Test]
        public void NoGameDirectory_IsReported()
        {
            IList<LobbyPersistentData.LobbyGlobalData.PlayerInfoGlobalData> profiles;

            Assert.That(repository.LoadProfiles(null, out profiles), Is.EqualTo(LobbyProfilesStatus.GameDirectoryNotFound));
            Assert.That(profiles, Is.Empty);
        }

        [Test]
        public void MissingProfileFile_IsReportedAndLogged()
        {
            Assert.That(LoadProfiles(LobbyProfilesStatus.NoProfileFile), Is.Empty);
            Assert.That(logger.Messages.Single(), Does.StartWith("Warning: No lobby profiles found"));
        }

        [Test]
        public void EmptyProfileFile_IsReportedAsUnreadable()
        {
            WriteGameFile(LobbyPersistentData.GlobalDataFileName, new byte[0]);

            Assert.That(LoadProfiles(LobbyProfilesStatus.Unreadable), Is.Empty);
            Assert.That(logger.Messages.Single(), Does.StartWith("Error: Unable to read the lobby profiles"));
        }

        [Test]
        public void DamagedProfileFile_IsReportedAsUnreadable()
        {
            byte[] file = LobbyFileBuilder.GlobalFile(true, true, 1, "Alice", "Bob");
            WriteGameFile(LobbyPersistentData.GlobalDataFileName, file.Take(file.Length / 2).ToArray());

            Assert.That(LoadProfiles(LobbyProfilesStatus.Unreadable), Is.Empty);
            Assert.That(logger.Messages.Single(), Does.StartWith("Error:"));
        }

        [Test]
        public void ProfileFileWithoutProfiles_IsReported()
        {
            WriteGameFile(LobbyPersistentData.GlobalDataFileName, LobbyFileBuilder.GlobalFile(true, true, 1));

            Assert.That(LoadProfiles(LobbyProfilesStatus.NoProfiles), Is.Empty);
        }

        [Test]
        public void Friends_AreReadFromTheUserFileOfTheProfile()
        {
            WriteGameFile(LobbyPersistentData.GlobalDataFileName, LobbyFileBuilder.GlobalFile(true, true, 1, "Alice", "Bob"));
            WriteGameFile(LobbyPersistentData.GetUserDataFileName(1), LobbyFileBuilder.UserFile(new string[0],
                new KeyValuePair<string, uint>("Carol", 3), new KeyValuePair<string, uint>("Dave", 4)));
            var bob = LoadProfiles(LobbyProfilesStatus.Loaded).Single(profile => profile.Username == "Bob");
            IDictionary<string, uint> friends;

            Assert.That(repository.LoadFriends(gameDirectory, bob, out friends), Is.EqualTo(LobbyFriendsStatus.Loaded));

            Assert.That(friends.Keys, Is.EquivalentTo(new[] { "Carol", "Dave" }));
        }

        [Test]
        public void Friends_MissingUserFile_IsNotAnError()
        {
            WriteGameFile(LobbyPersistentData.GlobalDataFileName, LobbyFileBuilder.GlobalFile(true, true, 1, "Alice"));
            var alice = LoadProfiles(LobbyProfilesStatus.Loaded).Single();
            IDictionary<string, uint> friends;

            Assert.That(repository.LoadFriends(gameDirectory, alice, out friends), Is.EqualTo(LobbyFriendsStatus.NoUserFile));

            Assert.That(friends, Is.Empty);
            Assert.That(logger.Messages, Is.Empty);
        }

        [Test]
        public void Friends_DamagedUserFile_IsReportedAndLogged()
        {
            WriteGameFile(LobbyPersistentData.GlobalDataFileName, LobbyFileBuilder.GlobalFile(true, true, 1, "Alice"));
            WriteGameFile(LobbyPersistentData.GetUserDataFileName(0), new byte[] { 1, 2, 3 });
            var alice = LoadProfiles(LobbyProfilesStatus.Loaded).Single();
            IDictionary<string, uint> friends;

            Assert.That(repository.LoadFriends(gameDirectory, alice, out friends), Is.EqualTo(LobbyFriendsStatus.Unreadable));

            Assert.That(friends, Is.Empty);
            Assert.That(logger.Messages.Single(), Does.StartWith("Error: Unable to read the lobby user data"));
        }

        [Test]
        public void UnopenableProfileFile_IsReportedAsUnreadable()
        {
            WriteGameFile(LobbyPersistentData.GlobalDataFileName, LobbyFileBuilder.GlobalFile(true, true, 1, "Alice"));
            fileSystem.FailOn(WinPath.Combine(gameDirectory, LobbyPersistentData.GlobalDataFileName), FileSystemOperation.Read,
                FileSystemStatus.AccessDenied);

            Assert.That(LoadProfiles(LobbyProfilesStatus.Unreadable), Is.Empty);
            Assert.That(logger.Messages.Single(), Does.StartWith("Error: Unable to read the lobby profiles").And.Contain("AccessDenied"));
        }

        [Test]
        public void GameDirectoryThatIsNoFullPath_HasNoProfileFile()
        {
            IList<LobbyPersistentData.LobbyGlobalData.PlayerInfoGlobalData> profiles;

            Assert.That(repository.LoadProfiles(@"Games\Empire Earth", out profiles), Is.EqualTo(LobbyProfilesStatus.NoProfileFile));
        }

        // --- VirtualStore (ADR 0016) --------------------------------------------------------------------------------

        [Test]
        public void VirtualizedGameFolder_ReadsTheVirtualStoreCopyFirst()
        {
            gameDirectory = @"C:\Program Files (x86)\Sierra\Empire Earth";
            WriteGameFile(LobbyPersistentData.GlobalDataFileName, LobbyFileBuilder.GlobalFile(true, true, 1, "Original"));
            fileSystem.AddFile(VirtualStore + @"\Program Files (x86)\Sierra\Empire Earth\" + LobbyPersistentData.GlobalDataFileName,
                LobbyFileBuilder.GlobalFile(true, true, 1, "Virtual"));

            var profiles = LoadProfiles(LobbyProfilesStatus.Loaded);

            Assert.That(profiles.Select(profile => profile.Username), Is.EqualTo(new[] { "Virtual" }));
            Assert.That(logger.Messages.Single(), Does.StartWith("Info: The game uses the VirtualStore copy"));
        }

        [Test]
        public void VirtualizedGameFolder_WithoutACopy_ReadsTheGameFolder()
        {
            gameDirectory = @"C:\Program Files (x86)\Sierra\Empire Earth";
            WriteGameFile(LobbyPersistentData.GlobalDataFileName, LobbyFileBuilder.GlobalFile(true, true, 1, "Original"));

            Assert.That(LoadProfiles(LobbyProfilesStatus.Loaded).Single().Username, Is.EqualTo("Original"));
            Assert.That(logger.Messages, Is.Empty);
        }

        [Test]
        public void VirtualizedGameFolder_FriendsComeFromTheVirtualStoreCopyToo()
        {
            gameDirectory = @"C:\Program Files\Empire Earth";
            string copies = VirtualStore + @"\Program Files\Empire Earth\";
            fileSystem.AddFile(copies + LobbyPersistentData.GlobalDataFileName, LobbyFileBuilder.GlobalFile(true, true, 1, "Alice"));
            fileSystem.AddFile(copies + LobbyPersistentData.GetUserDataFileName(0), LobbyFileBuilder.UserFile(new string[0],
                new KeyValuePair<string, uint>("Carol", 3)));
            var alice = LoadProfiles(LobbyProfilesStatus.Loaded).Single();
            IDictionary<string, uint> friends;

            Assert.That(repository.LoadFriends(gameDirectory, alice, out friends), Is.EqualTo(LobbyFriendsStatus.Loaded));
            Assert.That(friends.Keys, Is.EqualTo(new[] { "Carol" }));
        }

        [Test]
        public void GameFolderOutsideTheVirtualizedFolders_IgnoresAStaleVirtualStoreCopy()
        {
            fileSystem.AddFile(VirtualStore + @"\Games\Empire Earth\" + LobbyPersistentData.GlobalDataFileName,
                LobbyFileBuilder.GlobalFile(true, true, 1, "Stale"));

            Assert.That(LoadProfiles(LobbyProfilesStatus.NoProfileFile), Is.Empty);
        }

        [Test]
        public void Arguments_AreChecked()
        {
            var resolver = new EffectivePathResolver(fileSystem, VirtualStore, new string[0]);
            Assert.That(() => new LobbyProfileRepository(null, fileSystem, resolver), Throws.ArgumentNullException);
            Assert.That(() => new LobbyProfileRepository(logger, null, resolver), Throws.ArgumentNullException);
            Assert.That(() => new LobbyProfileRepository(logger, fileSystem, null), Throws.ArgumentNullException);
        }
    }
}

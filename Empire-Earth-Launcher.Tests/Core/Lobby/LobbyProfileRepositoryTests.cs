using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Empire_Earth_Launcher.Core.Lobby;
using Empire_Earth_Launcher.Core.Logging;
using Empire_Earth_Launcher.Tests.TestSupport;
using Empire_Earth_Launcher.Tests.Won;
using Empire_Earth_WON;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.Lobby
{
    /// <summary>
    /// Lobby profiles and friends as the launcher's start page loads them (<see cref="LobbyProfileRepository"/>).
    /// A missing, empty or damaged lobby file must never stop the launcher (korr-S1): it is reported as a status.
    /// </summary>
    [TestFixture]
    public class LobbyProfileRepositoryTests
    {
        private TemporaryDirectory gameDirectory;
        private RecordingLogger logger;
        private LobbyProfileRepository repository;

        [SetUp]
        public void SetUp()
        {
            gameDirectory = new TemporaryDirectory();
            logger = new RecordingLogger();
            repository = new LobbyProfileRepository(logger);
        }

        [TearDown]
        public void TearDown()
        {
            gameDirectory.Dispose();
        }

        private void WriteGameFile(string fileName, byte[] content)
        {
            File.WriteAllBytes(gameDirectory.Combine(fileName), content);
        }

        private IList<LobbyPersistentData.LobbyGlobalData.PlayerInfoGlobalData> LoadProfiles(LobbyProfilesStatus expected)
        {
            IList<LobbyPersistentData.LobbyGlobalData.PlayerInfoGlobalData> profiles;
            Assert.That(repository.LoadProfiles(gameDirectory.Path, out profiles), Is.EqualTo(expected));
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

            Assert.That(repository.LoadFriends(gameDirectory.Path, bob, out friends), Is.EqualTo(LobbyFriendsStatus.Loaded));

            Assert.That(friends.Keys, Is.EquivalentTo(new[] { "Carol", "Dave" }));
        }

        [Test]
        public void Friends_MissingUserFile_IsNotAnError()
        {
            WriteGameFile(LobbyPersistentData.GlobalDataFileName, LobbyFileBuilder.GlobalFile(true, true, 1, "Alice"));
            var alice = LoadProfiles(LobbyProfilesStatus.Loaded).Single();
            IDictionary<string, uint> friends;

            Assert.That(repository.LoadFriends(gameDirectory.Path, alice, out friends), Is.EqualTo(LobbyFriendsStatus.NoUserFile));

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

            Assert.That(repository.LoadFriends(gameDirectory.Path, alice, out friends), Is.EqualTo(LobbyFriendsStatus.Unreadable));

            Assert.That(friends, Is.Empty);
            Assert.That(logger.Messages.Single(), Does.StartWith("Error: Unable to read the lobby user data"));
        }

        private sealed class RecordingLogger : ILogger
        {
            public readonly List<string> Messages = new List<string>();

            public void Log(LogLevel level, string message, Exception exception = null)
            {
                Messages.Add(level + ": " + message);
            }
        }
    }
}

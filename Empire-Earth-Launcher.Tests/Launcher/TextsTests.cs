using System;
using Empire_Earth_Launcher.Core.Lobby;
using Empire_Earth_Launcher.Core.Settings;
using NUnit.Framework;
using PlayerGameState = Empire_Earth_WON.NeoApiClient.ConnectedPlayersMessage.PlayerInfo.PlayerGameState;

namespace Empire_Earth_Launcher.Tests.Launcher
{
    /// <summary>
    /// <see cref="Texts"/>: which text the UI shows for a result (ADR 0009). Checked in English, the neutral language;
    /// that German and French have the same texts is checked by <c>ResourceParityTests</c>.
    /// </summary>
    [TestFixture]
    [SetUICulture("en-US")]
    public class TextsTests
    {
        [TestCase(PlayerGameState.Lobby, "Lobby")]
        [TestCase(PlayerGameState.Room, "Room")]
        [TestCase(PlayerGameState.Playing, "Playing")]
        [TestCase((PlayerGameState)7, "Unknown (7)")]
        public void PlayerGameState_HasATextForEveryState(PlayerGameState state, string expected)
        {
            Assert.That(Texts.PlayerGameState(state), Is.EqualTo(expected));
        }

        [TestCase(LobbyProfilesStatus.GameDirectoryNotFound, "Empire Earth installation not found")]
        [TestCase(LobbyProfilesStatus.NoProfileFile, "No lobby profile found")]
        [TestCase(LobbyProfilesStatus.NoProfiles, "No lobby profile found")]
        [TestCase(LobbyProfilesStatus.Unreadable, "Lobby profiles could not be read (see the log)")]
        public void LobbyProfilesProblem_ExplainsWhyNoProfileIsShown(LobbyProfilesStatus status, string expected)
        {
            Assert.That(Texts.LobbyProfilesProblem(status), Is.EqualTo(expected));
        }

        [Test]
        public void LobbyProfilesProblem_IsNullWhenTheProfilesWereLoaded()
        {
            Assert.That(Texts.LobbyProfilesProblem(LobbyProfilesStatus.Loaded), Is.Null);
        }

        [TestCase(LobbyFriendsStatus.Loaded, 3, "Friends (3)")]
        [TestCase(LobbyFriendsStatus.Loaded, 0, "Friends (0)")]
        [TestCase(LobbyFriendsStatus.Unreadable, 0, "Friends could not be read (see the log)")]
        [TestCase(LobbyFriendsStatus.NoUserFile, 0, "")]
        public void LobbyFriends_ShowsTheNumberOrTheProblem(LobbyFriendsStatus status, int count, string expected)
        {
            Assert.That(Texts.LobbyFriends(status, count), Is.EqualTo(expected));
        }

        [TestCase("", "Windows language")]
        [TestCase("en", "English")]
        [TestCase("de", "Deutsch")]
        [TestCase("fr", "Français")]
        public void UiLanguageName_NamesEachLanguageInItsOwnLanguage(string language, string expected)
        {
            Assert.That(Texts.UiLanguageName(language), Is.EqualTo(expected));
        }

        [Test]
        public void UiLanguageName_HasANameForEveryChoice()
        {
            foreach (string language in UiLanguage.Choices)
                Assert.That(Texts.UiLanguageName(language), Is.Not.Empty, language);
            Assert.That(() => Texts.UiLanguageName("es"), Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        // GameDirectorySource is internal, so the cases pass its name (a public test method cannot take it).
        [TestCase("UserSetting", true, "Chosen manually")]
        [TestCase("UserSetting", false, "Chosen manually, but the folder does not exist")]
        [TestCase("Registry", true, "Detected from the game installation")]
        [TestCase("LauncherFolder", true, "Detected in the launcher folder")]
        [TestCase("NotFound", false, "Not found, please choose the folder of Empire Earth.exe")]
        public void GameDirectoryOrigin_NamesWhereTheFolderComesFrom(string source, bool exists, string expected)
        {
            var origin = (GameDirectorySource)Enum.Parse(typeof(GameDirectorySource), source);

            Assert.That(Texts.GameDirectoryOrigin(origin, exists), Is.EqualTo(expected));
        }
    }
}

using System;
using System.Linq;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Lobby;
using Empire_Earth_Launcher.Core.Settings;
using Empire_Earth_Launcher.Tests.TestSupport;
using Microsoft.Win32;
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

        // --- Installations (L-WP4) --------------------------------------------------------------------------------

        private const string NeoRoot = @"C:\Program Files (x86)\Neo Empire Earth";

        private static DiscoveryResult Discover(Action<InstallationWorld> arrange, string userChoice = null,
            string launcherFolder = null)
        {
            var world = new InstallationWorld();
            arrange(world);
            return world.Discover(userChoice, launcherFolder);
        }

        [Test]
        public void InstallationOrigin_NamesWhereTheInstallationComesFrom()
        {
            // The texts of the old GameDirectoryOrigin, now for the installation the discovery selected.
            DiscoveryResult registry = Discover(w => w.AddCommunityInstallation(NeoRoot, Product.NeoEE));
            DiscoveryResult chosen = Discover(w => w.AddCommunityInstallation(NeoRoot, Product.NeoEE), NeoRoot);
            DiscoveryResult missing = Discover(w => { }, @"D:\Removed");
            DiscoveryResult launcher = Discover(w => w.AddEmpireEarth(@"D:\EE"), launcherFolder: @"D:\EE");

            Assert.That(Texts.InstallationOrigin(chosen.Selected, true), Is.EqualTo("Chosen manually"));
            Assert.That(Texts.InstallationOrigin(missing.Selected, true), Is.EqualTo("Chosen manually, but the folder does not exist"));
            Assert.That(Texts.InstallationOrigin(registry.Selected, false), Is.EqualTo("Detected from the game installation"));
            Assert.That(Texts.InstallationOrigin(launcher.Selected, false), Is.EqualTo("Detected in the launcher folder"));
            Assert.That(Texts.InstallationOrigin(null, false), Is.EqualTo("Not found, please choose the folder of Empire Earth.exe"));
        }

        [TestCase(InstallationKind.Community, "Community setup")]
        [TestCase(InstallationKind.CommunityLegacy, "Older setup")]
        [TestCase(InstallationKind.Foreign, "Other")]
        public void InstallationKindName_HasATextForEveryKind(InstallationKind kind, string expected)
        {
            Assert.That(Texts.InstallationKindName(kind), Is.EqualTo(expected));
            Assert.That(Texts.InstallationKindHint(kind), Is.Not.Empty);
        }

        [Test]
        public void InstallationState_OkDamagedAndMissing()
        {
            DiscoveryResult ok = Discover(w => w.AddCommunityInstallation(NeoRoot, Product.NeoEE));
            DiscoveryResult damaged = Discover(w =>
            {
                w.AddCommunityInstallation(NeoRoot, Product.NeoEE);
                w.FileSystem.DeleteFile(NeoRoot + @"\Empire Earth\Empire Earth.exe");
                w.FileSystem.DeleteFile(NeoRoot + @"\Empire Earth - The Art of Conquest\EE-AOC.exe");
            });
            DiscoveryResult missing = Discover(w => { }, @"D:\Removed");

            Assert.That(Texts.InstallationStateName(ok.Selected), Is.EqualTo("OK"));
            Assert.That(Texts.InstallationStateHint(ok.Selected), Is.Empty);
            Assert.That(Texts.InstallationStateName(damaged.Selected), Is.EqualTo("Damaged"));
            Assert.That(Texts.InstallationStateHint(damaged.Selected), Is.EqualTo("Missing: Empire Earth.exe, EE-AOC.exe"));
            Assert.That(Texts.InstallationStateName(missing.Selected), Is.EqualTo("Not found"));
            Assert.That(Texts.InstallationStateHint(missing.Selected), Is.EqualTo("Chosen manually, but the folder does not exist"));
        }

        [Test]
        public void InstallationHints_AreEmptyForOneHealthyInstallation()
        {
            Assert.That(Texts.InstallationHints(Discover(w => w.AddCommunityInstallation(NeoRoot, Product.NeoEE))), Is.Empty);
        }

        [Test]
        public void InstallationHints_NameASharedGameSettingsKey()
        {
            DiscoveryResult result = Discover(w =>
            {
                w.AddLegacyInstallation(@"C:\Program Files (x86)\Empire Earth", Product.EE);
                w.AddForeignInstallation(@"C:\Retail\Empire Earth", RegistryHive.LocalMachine, RegistryView.Registry32);
            });

            Assert.That(Texts.InstallationHints(result), Is.EqualTo(
                @"2 installations of Empire Earth use the same game settings (HKCU\Software\SSSI\Empire Earth): the settings, " +
                "including the game folder stored there, are the same for all of them."));
        }

        [Test]
        public void InstallationHints_NameTwoProductsInOneFolder_ANewerSetup_AndADamagedSelection()
        {
            DiscoveryResult result = Discover(w =>
            {
                w.AddCommunityInstallation(@"C:\Games\Shared", Product.NeoEE);
                w.Clock.Advance(TimeSpan.FromDays(1));
                w.AddInstallInfo(@"C:\Games\Shared", Product.EE, ContractNames.ContractVersion + 1);
                w.FileSystem.DeleteFile(@"C:\Games\Shared\Empire Earth\Empire Earth.exe");
            });

            string[] lines = Texts.InstallationHints(result).Split(new[] { Environment.NewLine }, StringSplitOptions.None);

            Assert.That(lines, Has.Length.EqualTo(3));
            Assert.That(lines[0], Is.EqualTo(@"Empire Earth and NeoEE are installed in the same folder (C:\Games\Shared). The " +
                                             "launcher uses Empire Earth, which was installed last; uninstalling one of them also " +
                                             "removes files of the other."));
            Assert.That(lines[1], Is.EqualTo(@"C:\Games\Shared was installed by a newer setup than this launcher knows. " +
                                             "Please update the launcher."));
            Assert.That(lines[2], Does.StartWith(@"Empire Earth.exe is missing in C:\Games\Shared\Empire Earth. Antivirus"));
        }

        [Test]
        public void ArgumentsAreChecked()
        {
            Assert.That(() => Texts.InstallationHints(null), Throws.ArgumentNullException);
            Assert.That(() => Texts.InstallationStateName(null), Throws.ArgumentNullException);
            Assert.That(() => Texts.InstallationStateHint(null), Throws.ArgumentNullException);
            Assert.That(Enum.GetValues(typeof(InstallationKind)).Cast<InstallationKind>().Select(Texts.InstallationKindName),
                Is.Unique);
        }
    }
}

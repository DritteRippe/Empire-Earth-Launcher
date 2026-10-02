using System;
using System.Collections.Generic;
using System.Linq;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.GameSettings;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Lobby;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Core.Play;
using Empire_Earth_Launcher.Core.Settings;
using Empire_Earth_Launcher.Tests.Fakes;
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

        // --- Game settings (L-WP5) --------------------------------------------------------------------------------

        private static GameSettingsWorld GameSettings(FakeSystemInfo systemInfo = null)
        {
            var world = new GameSettingsWorld(systemInfo);
            world.AddAdminInstallationOfAnotherAccount(NeoRoot, Product.NeoEE);
            return world;
        }

        private static IReadOnlyList<ConsistencyFinding> Findings(GameSettingsWorld world)
        {
            return new ConsistencyChecker(world.Registry, world.FileSystem, world.SystemInfo).Check(world.Discover().Selected);
        }

        [TestCase(DefaultsStatus.Applied, "Empire Earth: the recommended settings are set up for your Windows account.")]
        [TestCase(DefaultsStatus.AppliedByNewerVersion, "Empire Earth: set up by a newer launcher or setup; the launcher leaves them as they are.")]
        [TestCase(DefaultsStatus.Pending, "Empire Earth: the recommended settings are not set up for your Windows account yet.")]
        [TestCase(DefaultsStatus.WaitingForPlay, "Empire Earth: not set up yet, because several installations share these settings. \"Reset game settings\" sets them up for this installation.")]
        [TestCase(DefaultsStatus.NewerContract, "Empire Earth: installed by a newer setup than this launcher knows. The launcher changes no settings; please update it.")]
        public void DefaultsStatus_HasATextForEveryState(DefaultsStatus status, string expected)
        {
            Assert.That(Texts.DefaultsStatus(Game.EmpireEarth, status), Is.EqualTo(expected));
            Assert.That(Texts.DefaultsStatus(Game.ArtOfConquest, status), Does.StartWith("The Art of Conquest: "));
        }

        [Test]
        public void Block_NamesTheSetupOrTheProgram()
        {
            var probe = new FakeMutexProbe();
            var guard = new MutationGuard(probe, new RecordingLogger());

            Assert.That(Texts.Block(guard.Check("test")), Is.Null);
            probe.With("MadDocSoftwarePresentsEmpireEarthExpansion");
            Assert.That(Texts.Block(guard.Check("test")), Is.EqualTo("Not possible while EE-AOC.exe is running."));
            probe.With("NeoEE_Setup");
            Assert.That(Texts.Block(guard.Check("test")), Is.EqualTo("Not possible while the NeoEE setup is running."));
        }

        [Test]
        public void DisplayQuestion_ListsEveryDifferingValue()
        {
            GameSettingsWorld world = GameSettings();
            world.RawRegistry.Seed(GameSettingsWorld.Settings(Product.NeoEE, Game.EmpireEarth), "Game Bit Depth", RegistryValue.FromDWord(16));
            world.RawRegistry.Seed(GameSettingsWorld.Settings(Product.NeoEE, Game.ArtOfConquest), "Rasterizer Name", RegistryValue.FromString("Direct3D"));
            DisplayQuestion question = world.CreateDefaultsService().ApplyAtLauncherStart(world.Discover()).Question;

            Assert.That(Texts.DisplayQuestion(question), Is.EqualTo(
                "Your display settings differ from the recommended ones: Empire Earth Game Bit Depth 16 instead of 32; " +
                "The Art of Conquest Rasterizer Name Direct3D instead of Direct3D Hardware TnL. Apply the recommended display " +
                "settings? Your current values are saved as a .reg file first."));
        }

        [Test]
        public void Finding_HasATextForEveryCode()
        {
            GameSettingsWorld world = GameSettings(new FakeSystemInfo().WithScreen(1366, 700, 150));
            RegistryLocation ee = GameSettingsWorld.Settings(Product.NeoEE, Game.EmpireEarth);
            world.RawRegistry.Seed(ee, "Game Bit Depth", RegistryValue.FromDWord(16));
            world.RawRegistry.Seed(ee, "Texture Bit Depth", RegistryValue.FromDWord(32));
            world.RawRegistry.Seed(ee, "Rasterizer Name", RegistryValue.FromString("Direct3D"));
            world.RawRegistry.Seed(ee, "Game Window Width", RegistryValue.FromDWord(1024));
            world.RawRegistry.Seed(ee, "Game Window Height", RegistryValue.FromDWord(768));

            string[] texts = Findings(world).Select(Texts.Finding).ToArray();

            Assert.That(texts, Is.EqualTo(new[]
            {
                "Empire Earth: Game Bit Depth (16) differs from Texture Bit Depth (32); the main menu can turn white and unreadable. \"Apply recommended display\" sets both to 32.",
                "Empire Earth: 16-bit colors often freeze the game on Windows 8 and later. \"Apply recommended display\" sets 32 bit; hide the hint if 16 bit works for you.",
                "Empire Earth: the renderer is \"Direct3D\"; recommended for this installation is \"Direct3D Hardware TnL\". Hide the hint if you chose it on purpose.",
                "Empire Earth: the game window (1024x768) is larger than the screen as the game sees it (910x466). \"Apply recommended display\" sets 1366x768.",
                "The screen is only 700 pixels high; the menus of the game need at least 768. Some menus may not fit.",
            }));
        }

        [Test]
        public void Finding_WindowFitsOnlyWithHighDpiAware_AdvisesTheOptionOr100Percent()
        {
            GameSettingsWorld world = GameSettings(new FakeSystemInfo().WithScreen(1920, 1080, 150));
            world.RawRegistry.Seed(GameSettingsWorld.Settings(Product.NeoEE, Game.ArtOfConquest), "Game Window Width", RegistryValue.FromDWord(1920));
            world.RawRegistry.Seed(GameSettingsWorld.Settings(Product.NeoEE, Game.ArtOfConquest), "Game Window Height", RegistryValue.FromDWord(1080));

            Assert.That(Texts.Finding(Findings(world).Single()), Is.EqualTo(
                "The Art of Conquest: the game window (1920x1080) fits the screen only with the compatibility option HIGHDPIAWARE; " +
                "without it, the game sees 1280x720 at this scaling. Switch the option on below or set the Windows scaling to 100 %."));
        }

        [Test]
        public void Finding_NetworkFolder()
        {
            var world = new GameSettingsWorld();
            world.World.AddEmpireEarth(@"\\server\games\EE");
            Installation installation = world.Discover(@"\\server\games\EE").Selected;

            ConsistencyFinding finding = new ConsistencyChecker(world.Registry, world.FileSystem, world.SystemInfo).Check(installation).Single();

            Assert.That(Texts.Finding(finding), Is.EqualTo(
                "Empire Earth: the folder \\\\server\\games\\EE is not on a drive letter, so the \"Installed From\" values cannot point to it. " +
                "Connect the network folder as a drive, e.g. Z:."));
        }

        [Test]
        public void Finding_FolderOutsideTheAnsiCodePage()
        {
            var world = new GameSettingsWorld();
            world.World.AddEmpireEarth(@"C:\Παιχνίδια\Empire Earth");
            Installation installation = world.Discover(@"C:\Παιχνίδια\Empire Earth").Selected;

            ConsistencyFinding finding = new ConsistencyChecker(world.Registry, world.FileSystem, world.SystemInfo).Check(installation).Single();

            Assert.That(Texts.Finding(finding), Is.EqualTo(
                "Empire Earth: the folder C:\\Παιχνίδια\\Empire Earth contains characters that Windows does not have for non-Unicode " +
                "programs such as the game, so the game may not find its files. If it does not start, copy it to a folder whose name " +
                "has only the letters A to Z and digits."));
        }

        [Test]
        public void GameSettingsResult_NamesTheBackupFolderOrTheProblem()
        {
            GameSettingsWorld world = GameSettings();
            GameSettingsResult done = world.CreateDefaultsService().Reset(world.Discover().Selected);
            world.FileSystem.FailOn(GameSettingsWorld.BackupsFolder, FileSystemOperation.Write, FileSystemStatus.IoError);
            GameSettingsResult failed = world.CreateDefaultsService().Reset(world.Discover().Selected);
            world.Mutexes.With("EE_Setup");
            GameSettingsResult blocked = world.CreateDefaultsService().Reset(world.Discover().Selected);

            Assert.That(Texts.GameSettingsResult(done), Is.EqualTo("Done. Your previous settings are saved in: " + done.BackupFolder));
            Assert.That(Texts.GameSettingsResult(failed), Is.EqualTo("Nothing was changed: the backup could not be written (details in the log)."));
            Assert.That(Texts.GameSettingsResult(blocked), Is.EqualTo("Not possible while the Empire Earth setup is running."));
        }

        [Test]
        public void CompatibilityOption_NamesEveryEntryAndNoOther()
        {
            foreach (string entry in CompatibilityLayers.LauncherEntries)
                Assert.That(Texts.CompatibilityOption(entry), Does.EndWith("(" + entry + ")"));
            Assert.That(() => Texts.CompatibilityOption(CompatibilityLayers.RunAsAdmin), Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void CompatibilityInfo_Windows10_ProgramsHklmAndVersionMode()
        {
            GameSettingsWorld world = GameSettings();
            world.RawRegistry.Seed(RegistryLocation.LocalMachine64(ContractNames.CompatibilityLayersKey),
                NeoRoot + @"\Empire Earth - The Art of Conquest\EE-AOC.exe", RegistryValue.FromString("~ WIN7RTM"));
            CompatibilityState state = new CompatibilityOptions(world.Registry, world.SystemInfo, world.Guard, world.Backups, world.Logger)
                .Read(world.Discover().Selected);

            Assert.That(Texts.CompatibilityInfo(state).Split(new[] { Environment.NewLine }, StringSplitOptions.None), Is.EqualTo(new[]
            {
                "The options apply to Empire Earth.exe, EE-AOC.exe for your Windows account.",
                "Set by the setup for all users (only the setup changes it): EE-AOC.exe: ~ WIN7RTM",
                "A Windows compatibility mode is already set, so no second one is offered. If the setup set it for all users, a custom run of the setup without \"Enable earlier Windows compatibility mode\" removes it.",
            }));
        }

        [Test]
        public void CompatibilityInfo_Windows7_OldValuesAndRunAsAdmin()
        {
            GameSettingsWorld world = GameSettings(FakeSystemInfo.Windows7());
            world.RawRegistry.Seed(GameSettingsWorld.Layers, NeoRoot + @"\Empire Earth\Empire Earth.exe", RegistryValue.FromString("~ RUNASADMIN"));
            world.RawRegistry.Seed(GameSettingsWorld.Layers, NeoRoot + @"\Empire Earth - The Art of Conquest\EE-AOC.exe",
                RegistryValue.FromString("~ RUNASADMIN WINXPSP3"));
            CompatibilityState state = new CompatibilityOptions(world.Registry, world.SystemInfo, world.Guard, world.Backups, world.Logger)
                .Read(world.Discover().Selected);

            Assert.That(Texts.CompatibilityInfo(state).Split(new[] { Environment.NewLine }, StringSplitOptions.None), Is.EqualTo(new[]
            {
                "On Windows 7 and under Wine the launcher offers no compatibility options. On Windows 7 the current setup can set them if you choose them during the installation.",
                "Compatibility values of an older setup (HKCU, EE-AOC.exe: ~ RUNASADMIN WINXPSP3), which can cause black screens on Windows 7. Run the current community setup: it removes them.",
                "An older setup set \"Run as administrator\" for your account (~ RUNASADMIN). The online lobby should not run as administrator.",
            }));
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

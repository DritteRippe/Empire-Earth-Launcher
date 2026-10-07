using System;
using System.Collections.Generic;
using System.Linq;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.GameSettings;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Integrity;
using Empire_Earth_Launcher.Core.Lobby;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Core.Play;
using Empire_Earth_Launcher.Core.Repair;
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

        // --- Play (L-WP6) -------------------------------------------------------------------------------------------------

        private const string PlayRoot = @"C:\Program Files (x86)\Neo Empire Earth";

        private static Installation PlayInstallation(InstallMode mode = InstallMode.Admin, InstallationKind kind = InstallationKind.Community)
        {
            return new Installation(Product.NeoEE, PlayRoot, PlayRoot + @"\Empire Earth", PlayRoot + @"\Empire Earth - The Art of Conquest",
                kind, mode, new[] { InstallationSource.RegistryRecord });
        }

        private static StartResult Result(StartOutcome outcome, Game game = null)
        {
            Installation installation = PlayInstallation();
            game = game ?? Game.EmpireEarth;
            return new StartResult(outcome, installation, game,
                WinPath.Combine(installation.GetGameFolder(game), game.ProgramName));
        }

        [Test]
        public void ProgramVersions_OneLinePerProgram()
        {
            var fileSystem = new InMemoryFileSystem();
            fileSystem.AddFile(PlayRoot + @"\Empire Earth\Empire Earth.exe", "program");
            fileSystem.AddFile(PlayRoot + @"\Empire Earth - The Art of Conquest\EE-AOC.exe", "program");
            var reader = new FakeFileVersionReader().With(PlayRoot + @"\Empire Earth\Empire Earth.exe", "2.0.0.2949");

            Assert.That(Texts.ProgramVersions(new ProgramVersions(fileSystem, reader).Read(PlayInstallation())), Is.EqualTo(
                "Empire Earth.exe: version 2.0.0.2949" + Environment.NewLine + "EE-AOC.exe: no version information"));
            Assert.That(Texts.ProgramVersions(new ProgramVersions(new InMemoryFileSystem(), reader).Read(PlayInstallation())),
                Is.EqualTo("Empire Earth.exe: missing" + Environment.NewLine + "EE-AOC.exe: missing"));
        }

        [Test]
        public void SetupRunning_NamesTheProduct()
        {
            Assert.That(Texts.SetupRunning(null), Is.Null);
            Assert.That(Texts.SetupRunning(SetupKind.NeoEE), Is.EqualTo(
                "The NeoEE setup is running. Until it has ended, the launcher starts no game and changes no game settings."));
        }

        [Test]
        public void SetupRunning_TheSuite_NamesEmpireEarthCommunity()
        {
            Assert.That(Texts.SetupRunning(SetupKind.Suite), Is.EqualTo(
                "The Empire Earth Community setup is running. Until it has ended, the launcher starts no game and changes no game settings."));
            Assert.That(Texts.SetupRunning(SetupKind.EE), Does.StartWith("The Empire Earth setup is running."));
        }

        [Test]
        public void Block_TheSuite_NamesEmpireEarthCommunity()
        {
            Assert.That(Texts.Block(MutationCheck.SetupRunning(SetupKind.Suite)),
                Is.EqualTo("Not possible while the Empire Earth Community setup is running."));
        }

        [Test]
        public void RepairSteps_Contract_4_4_WithTheSuiteFolder_TheSuiteStepComesFirst()
        {
            const string Folder = @"C:\Users\Anna\Downloads\Empire Earth Community";
            RepairAdvice advice = RepairAdvice.For(PlayInstallation(), RepairReason.ProgramMissing, new[] { Game.EmpireEarth }, Folder);

            Assert.That(Texts.RepairSteps(advice).Split(new[] { Environment.NewLine }, StringSplitOptions.None), Is.EqualTo(new[]
            {
                "1. First add an exception for the folder " + PlayRoot + " in your antivirus program; otherwise it removes the files again.",
                "2. Close the game. Run \"Empire Earth Community Setup\" again from the folder you unpacked it to (" + Folder +
                "): it repairs or updates the games it installed.",
                "3. Keep the folder " + PlayRoot + " and choose \"Install for all users\" again.",
                "4. Keep the task \"Register NeoEE CDKeys\" selected: it also repairs the CD keys."
            }));
            Assert.That(Texts.RepairSteps(advice), Does.Not.Contain("Download the current community setup"),
                "the download is the second option below the steps, not a step");
        }

        [Test]
        public void StartMessage_HasATextForEveryOutcome()
        {
            foreach (StartOutcome outcome in Enum.GetValues(typeof(StartOutcome)))
            {
                StartResult result = Result(outcome);
                result.RunningSetup = SetupKind.EE;
                result.OtherGame = Game.ArtOfConquest;
                result.ErrorCode = 5;
                result.ErrorMessage = "Access is denied";
                Assert.That(Texts.StartMessage(result), Is.Not.Empty, outcome.ToString());
            }
        }

        [Test]
        public void StartMessage_Texts()
        {
            string program = PlayRoot + @"\Empire Earth\Empire Earth.exe";
            Assert.That(Texts.StartMessage(Result(StartOutcome.Started, Game.ArtOfConquest)), Is.EqualTo("The Art of Conquest was started."));
            StartResult other = Result(StartOutcome.OtherGameRunning, Game.ArtOfConquest);
            other.OtherGame = Game.EmpireEarth;
            Assert.That(Texts.StartMessage(other), Is.EqualTo(
                "Empire Earth.exe is running. Start The Art of Conquest anyway? Both games at the same time can become unstable."));
            Assert.That(Texts.StartMessage(Result(StartOutcome.FolderMissing)), Is.EqualTo(
                "The folder " + PlayRoot + @"\Empire Earth does not exist. Choose the game folder on the Launcher page."));
            Assert.That(Texts.StartMessage(Result(StartOutcome.Damaged)), Does.StartWith(program + " is missing. The installation is damaged"));
            StartResult failed = Result(StartOutcome.Failed);
            failed.ErrorCode = 740;
            failed.ErrorMessage = "The requested operation requires elevation";
            Assert.That(Texts.StartMessage(failed), Is.EqualTo(
                program + " could not be started (Windows error 740): The requested operation requires elevation"));
            Assert.That(Texts.StartMessage(Result(StartOutcome.ElevationCancelled)), Does.Contain("\"Run as administrator\""));
        }

        [Test]
        public void StartMessage_Forum18_TheHangingHintOnlyWithAProcess()
        {
            StartResult running = Result(StartOutcome.AlreadyRunning);
            Assert.That(Texts.StartMessage(running), Is.EqualTo(
                "Empire Earth.exe is already running. The launcher does not start it a second time."));

            running.ProcessFound = true;
            Assert.That(Texts.StartMessage(running), Does.StartWith(
                "Empire Earth.exe is already running. The launcher does not start it a second time." + Environment.NewLine +
                Environment.NewLine + "If you cannot see its window, it may hang: open the Task Manager (Ctrl+Shift+Esc), select " +
                "Empire Earth.exe on the Details tab").And.EndWith("The launcher never ends a program itself."));
        }

        [Test]
        public void RepairSteps_Contract_4_4_NeoEEForAllUsers()
        {
            Installation installation = PlayInstallation();
            RepairAdvice advice = RepairAdvice.For(installation, RepairReason.ProgramMissing, new[] { Game.EmpireEarth });

            Assert.That(Texts.RepairSteps(advice).Split(new[] { Environment.NewLine }, StringSplitOptions.None), Is.EqualTo(new[]
            {
                "1. First add an exception for the folder " + PlayRoot + " in your antivirus program; otherwise it removes the files again.",
                "2. Close the game. Download the current community setup and run it: it finds the installation and offers to update or repair it.",
                "3. Keep the folder " + PlayRoot + " and choose \"Install for all users\" again.",
                "4. Keep the task \"Register NeoEE CDKeys\" selected: it also repairs the CD keys."
            }));
        }

        [TestCase(InstallMode.User, "choose \"Install for me only\" again.")]
        [TestCase(InstallMode.Portable, "Run the portable setup again from the same place")]
        [TestCase(InstallMode.Unknown, "Keep the folder " + PlayRoot + ".")]
        public void RepairSteps_Contract_4_4_TheModeOfTheInstallation(InstallMode mode, string expected)
        {
            RepairAdvice advice = RepairAdvice.For(PlayInstallation(mode), RepairReason.Requested);

            Assert.That(Texts.RepairSteps(advice), Does.Contain(expected));
        }

        [Test]
        public void RepairSteps_Contract_4_4_AForeignInstallationIsNotRepaired()
        {
            var foreign = new Installation(Product.EE, @"D:\", @"D:\Empire Earth", null, InstallationKind.Foreign, InstallMode.Unknown,
                new[] { InstallationSource.InstalledFrom });
            RepairAdvice advice = RepairAdvice.For(foreign, RepairReason.ProgramMissing, new[] { Game.EmpireEarth });

            Assert.That(Texts.RepairSteps(advice), Is.EqualTo(
                @"1. The installation in D:\Empire Earth was not made by the community setup (for example a CD or GOG installation). " +
                "The setup does not repair it; it installs its own copy of the game in a new folder." + Environment.NewLine +
                @"2. First add an exception for the folder D:\Empire Earth in your antivirus program; otherwise it removes the files again."));
        }

        // --- Integrity and update API (L-WP7, contract 2.5, 2.6, 4.3, 4.5) ----------------------------------------------

        private static IntegrityFinding FileFinding(string path, FileClass fileClass, FindingKind kind)
        {
            return new IntegrityFinding(path, WinPath.Combine(PlayRoot, path), fileClass, kind, null, null, null);
        }

        private static IntegrityReport Finished(Installation installation, params IntegrityFinding[] findings)
        {
            return IntegrityReport.Finished(installation, IntegrityCheckKind.Quick, findings, 1500, 40);
        }

        [Test]
        public void IntegrityBadge_HasATextForEveryState_AndNoneForAForeignInstallation()
        {
            Installation community = PlayInstallation();
            Installation foreign = PlayInstallation(InstallMode.Unknown, InstallationKind.Foreign);
            IntegrityFinding missingData = FileFinding("Empire Earth/Data/file0001.dat", FileClass.Data, FindingKind.Missing);
            IntegrityFinding changedData = FileFinding("Empire Earth/Data/file0002.dat", FileClass.Data, FindingKind.HashDiffers);
            IntegrityFinding missingProgram = FileFinding("Empire Earth/Empire Earth.exe", FileClass.Code, FindingKind.Missing);

            Assert.That(Texts.IntegrityBadge(null, false), Is.Empty);
            Assert.That(Texts.IntegrityBadge(null, true), Is.EqualTo("Files: checking..."));
            Assert.That(Texts.IntegrityBadge(IntegrityReport.NotChecked(foreign, IntegrityCheckKind.Quick), false), Is.Empty,
                "contract 2.5: foreign, no check and no message");
            Assert.That(Texts.IntegrityBadge(Finished(community), false), Is.EqualTo("Files: OK"));
            Assert.That(Texts.IntegrityBadge(Finished(community, changedData), false), Is.EqualTo("Files: OK, game data changed"));
            Assert.That(Texts.IntegrityBadge(Finished(community, missingData), false), Is.EqualTo("Files: incomplete"));
            Assert.That(Texts.IntegrityBadge(Finished(community, missingData, missingProgram), false), Is.EqualTo("Files: damaged"));
            Assert.That(Texts.IntegrityBadge(IntegrityReport.Unknown(community, IntegrityCheckKind.Quick, UnknownReason.LegacySetup),
                false), Is.EqualTo("Files: no check (older setup)"));
            Assert.That(Texts.IntegrityBadge(IntegrityReport.Unknown(community, IntegrityCheckKind.Quick, UnknownReason.NoManifest),
                false), Is.EqualTo("Files: cannot be checked"));
            Assert.That(Texts.IntegrityBadge(IntegrityReport.Cancelled(community, IntegrityCheckKind.Full, CancelReason.SetupRunning),
                false), Is.EqualTo("Files: check cancelled"));
            Assert.That(Texts.IntegrityBadge(Finished(community), true), Is.EqualTo("Files: checking..."), "a running check first");
        }

        [Test]
        public void IntegrityBadge_O11_TwoProductsInOneFolder_AreUnreliable()
        {
            Installation installation = PlayInstallation();
            installation.OtherProductInRoot = Product.EE;

            Assert.That(Texts.IntegrityBadge(Finished(installation), false), Is.EqualTo("Files: OK (unreliable)"));
            Assert.That(Texts.IntegrityExplanation(Finished(installation)).Split(new[] { Environment.NewLine }, StringSplitOptions.None)[1],
                Is.EqualTo("Empire Earth and NeoEE are installed in the same folder: the setup of Empire Earth may have replaced " +
                           "files of NeoEE, so this check is unreliable."));
        }

        [Test]
        public void Contract_2_6_ChangedNeoEEProgramFiles_AreWordedNeutrally()
        {
            IntegrityFinding changedProgram = FileFinding("Empire Earth/neoee.dll", FileClass.Code, FindingKind.HashDiffers);
            IntegrityReport neoee = Finished(PlayInstallation(), changedProgram);
            var ee = new Installation(Product.EE, PlayRoot, PlayRoot + @"\Empire Earth", null, InstallationKind.Community,
                InstallMode.Admin, new[] { InstallationSource.RegistryRecord });
            IntegrityReport eeReport = Finished(ee, changedProgram);

            Assert.That(Texts.IntegrityBadge(neoee, false), Is.EqualTo("Files: changed since the installation"));
            Assert.That(Texts.IntegrityExplanation(neoee), Does.StartWith("Program files have changed since the installation."));
            Assert.That(Texts.IntegrityFiles(neoee), Is.EqualTo("Empire Earth/neoee.dll: changed since the installation"));
            Assert.That(Texts.IntegrityBadge(eeReport, false), Is.EqualTo("Files: damaged"));
            Assert.That(Texts.IntegrityFiles(eeReport), Is.EqualTo("Empire Earth/neoee.dll: damaged or replaced"));

            IntegrityReport missingToo = Finished(PlayInstallation(), changedProgram,
                FileFinding("Empire Earth/Empire Earth.exe", FileClass.Code, FindingKind.Missing));
            Assert.That(Texts.IntegrityBadge(missingToo, false), Is.EqualTo("Files: damaged"), "a missing file is no update");
            Assert.That(Texts.IntegrityFiles(missingToo), Does.Contain("Empire Earth/neoee.dll: changed since the installation"));
        }

        [Test]
        public void IntegrityExplanation_Contract_2_5_TheTextsForCommunityLegacyAndForeign()
        {
            Installation community = PlayInstallation();

            Assert.That(Texts.IntegrityExplanation(IntegrityReport.Unknown(community, IntegrityCheckKind.Quick, UnknownReason.LegacySetup)),
                Is.EqualTo("Installed by the community setup 1.7.2 or older, which writes no list of files. Run the current setup to enable the check."));
            Assert.That(Texts.IntegrityExplanation(IntegrityReport.Unknown(community, IntegrityCheckKind.Quick, UnknownReason.OlderSetupRanAfter)),
                Is.EqualTo("An older setup ran after the current one, or the last setup could not replace its records. Run the current setup."));
            foreach (UnknownReason reason in new[] { UnknownReason.NoInstallInfo, UnknownReason.NoManifest,
                         UnknownReason.ManifestUnreadable, UnknownReason.InvalidManifest })
                Assert.That(Texts.IntegrityExplanation(IntegrityReport.Unknown(community, IntegrityCheckKind.Quick, reason)),
                    Does.StartWith("The last run of the setup did not finish"), reason.ToString());
            Assert.That(Texts.IntegrityExplanation(IntegrityReport.Unknown(community, IntegrityCheckKind.Quick, UnknownReason.NewerContract)),
                Is.EqualTo("Installed by a newer setup than this launcher knows. Please update the launcher."));
            Assert.That(Texts.IntegrityExplanation(IntegrityReport.Unknown(community, IntegrityCheckKind.Quick, UnknownReason.FilesUnreadable)),
                Does.StartWith("Some files could not be read"));
            Assert.That(Texts.IntegrityExplanation(IntegrityReport.NotChecked(PlayInstallation(InstallMode.Unknown, InstallationKind.Foreign),
                IntegrityCheckKind.Quick)), Does.StartWith("Not checked: this installation was not made by the community setup"));
            Assert.That(Texts.IntegrityExplanation(IntegrityReport.Cancelled(community, IntegrityCheckKind.Full, CancelReason.SetupRunning)),
                Is.EqualTo("The check was cancelled because a setup started. It runs again when the setup has ended."));
            Assert.That(Texts.IntegrityExplanation(IntegrityReport.Cancelled(community, IntegrityCheckKind.Full, CancelReason.Requested)),
                Is.EqualTo("The check was cancelled."));
            Assert.That(Texts.IntegrityExplanation(Finished(community)),
                Is.EqualTo("All 1500 files of the setup's list are there, and the 40 program files are unchanged (quick check)."));
            Assert.That(Texts.IntegrityExplanation(IntegrityReport.Finished(community, IntegrityCheckKind.Full, new IntegrityFinding[0], 1500, 1480)),
                Does.EndWith("the 1480 files compared with their checksum are unchanged (full check)."));
        }

        [Test]
        public void RepairReason_Contract_2_5_NamesTheFiles_AtMostTen_DamagedFirst()
        {
            var findings = new List<IntegrityFinding> { FileFinding("Empire Earth/Data/file0000.dat", FileClass.Data, FindingKind.HashDiffers) };
            for (int i = 1; i <= 11; i++)
                findings.Add(FileFinding("Empire Earth/Data/file" + i.ToString("0000", System.Globalization.CultureInfo.InvariantCulture) + ".dat",
                    FileClass.Data, FindingKind.Missing));
            findings.Add(FileFinding("Empire Earth/Empire Earth.exe", FileClass.Code, FindingKind.MissingAfterInstall));
            IntegrityReport report = Finished(PlayInstallation(), findings.ToArray());
            RepairAdvice advice = RepairAdvice.ForIntegrity(report);

            string[] lines = Texts.RepairReasonText(advice, report).Split(new[] { Environment.NewLine }, StringSplitOptions.None);

            Assert.That(lines[0], Is.EqualTo("Files of the installation are missing or damaged. Antivirus programs often delete game files " +
                                             "or move them to quarantine."));
            Assert.That(lines[1], Is.Empty);
            Assert.That(lines[2], Is.EqualTo("Empire Earth/Empire Earth.exe: missing since the installation"), "damaged first");
            Assert.That(lines[3], Is.EqualTo("Empire Earth/Data/file0001.dat: missing"));
            Assert.That(lines.Length, Is.EqualTo(13), "explanation, empty line, ten files, the rest");
            Assert.That(lines[12], Is.EqualTo("and 3 more files"), "two missing files and the changed one");
            Assert.That(Texts.IntegrityFiles(report).Split(new[] { Environment.NewLine }, StringSplitOptions.None),
                Has.Length.EqualTo(13).And.Contains("Empire Earth/Data/file0000.dat: modified"), "the Tools page lists every file");
            Assert.That(Texts.RepairSteps(advice), Does.StartWith("1. First add an exception for the folder " + PlayRoot));
        }

        [Test]
        public void RepairReason_OfTheOtherAdvice()
        {
            Installation installation = PlayInstallation();
            IntegrityReport unknown = IntegrityReport.Unknown(installation, IntegrityCheckKind.Quick, UnknownReason.NoManifest);
            var update = new VersionCheckResult(installation, VersionKind.Game, "2.0.0.5", VersionCheckOutcome.UpdateAvailable, "2.0.1",
                UpdateApiFailure.None);

            Assert.That(Texts.RepairReasonText(RepairAdvice.ForIntegrity(unknown), unknown), Does.StartWith("The last run of the setup"));
            Assert.That(Texts.RepairReasonText(RepairAdvice.ForUpdate(update), null), Is.EqualTo("Game version 2.0.0.5: version 2.0.1 is available."));
            Assert.That(Texts.RepairReasonText(RepairAdvice.For(installation, RepairReason.ProgramMissing, new[] { Game.ArtOfConquest }), null),
                Does.StartWith("EE-AOC.exe is missing in " + PlayRoot + @"\Empire Earth - The Art of Conquest. Antivirus programs"));
            Assert.That(Texts.RepairReasonText(RepairAdvice.For(installation, RepairReason.Requested), null), Is.Null);
        }

        [Test]
        public void IntegrityProgress_CountsTheFiles()
        {
            Assert.That(Texts.IntegrityProgress(new IntegrityProgress(250, 1500)), Is.EqualTo("Checking: 250 of 1500 files"));
        }

        [Test]
        public void VersionResult_Contract_4_5_EveryOutcome()
        {
            Installation installation = PlayInstallation();
            VersionCheckResult Result(VersionKind kind, VersionCheckOutcome outcome, string latest = null,
                UpdateApiFailure failure = UpdateApiFailure.None)
            {
                return new VersionCheckResult(installation, kind, kind == VersionKind.Game ? "2.0.0.5" : "2.0.0", outcome, latest, failure);
            }

            Assert.That(Texts.VersionResult(Result(VersionKind.Game, VersionCheckOutcome.UpToDate)), Is.EqualTo("Game version 2.0.0.5: up to date."));
            Assert.That(Texts.VersionResult(Result(VersionKind.Game, VersionCheckOutcome.UpdateAvailable, "?")),
                Is.EqualTo("Game version 2.0.0.5: version ? is available."));
            Assert.That(Texts.VersionResult(Result(VersionKind.Setup, VersionCheckOutcome.UpToDate)), Is.EqualTo("Setup version 2.0.0: up to date."));
            Assert.That(Texts.VersionResult(Result(VersionKind.Setup, VersionCheckOutcome.UpdateAvailable, "2.1.0")),
                Is.EqualTo("Setup version 2.0.0: version 2.1.0 is available."));
            Assert.That(Texts.VersionResult(Result(VersionKind.Game, VersionCheckOutcome.NotPossible)), Does.StartWith("No version check:"));
            Assert.That(Texts.VersionResult(Result(VersionKind.Game, VersionCheckOutcome.Failed, null, UpdateApiFailure.TlsError)),
                Is.EqualTo("The update server could not be asked (the secure connection failed); details in the log."));
            Assert.That(Texts.VersionResults(Result(VersionKind.Game, VersionCheckOutcome.NotPossible),
                Result(VersionKind.Setup, VersionCheckOutcome.NotPossible)), Does.Not.Contain(Environment.NewLine), "said once");
            Assert.That(Texts.VersionResults(null, null), Is.Empty);
        }

        [TestCase(UpdateApiFailure.Timeout, "no answer within 10 seconds")]
        [TestCase(UpdateApiFailure.TlsError, "the secure connection failed")]
        [TestCase(UpdateApiFailure.NetworkError, "no connection")]
        [TestCase(UpdateApiFailure.StatusNotOk, "unexpected answer of the server")]
        public void Failure_Contract_4_5_SaysWhyTheUpdateApiGaveNoAnswer(UpdateApiFailure reason, string expected)
        {
            Assert.That(Texts.Failure(reason), Is.EqualTo(expected));
        }
    }
}

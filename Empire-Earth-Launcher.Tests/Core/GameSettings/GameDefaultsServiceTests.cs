using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.GameSettings;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Logging;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Core.Play;
using Empire_Earth_Launcher.Tests.Fakes;
using Empire_Earth_Launcher.Tests.TestSupport;
using Microsoft.Win32;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.GameSettings
{
    /// <summary>
    /// <see cref="GameDefaultsService"/> (contract 3.2, 3.5, 3.6, ADR 0015 with the plan review, ADR 0016): the start of the
    /// launcher, the first run with its question, the markers, the class S sync before Play, the recommended display
    /// settings and the reset with its backup. Every change passes the launcher's write policy.
    /// </summary>
    [TestFixture]
    public class GameDefaultsServiceTests
    {
        private const string NeoRoot = GameSettingsWorld.NeoRoot;
        private const string NeoEeFolder = NeoRoot + @"\Empire Earth";
        private const string NeoAocFolder = NeoRoot + @"\Empire Earth - The Art of Conquest";

        private static readonly RegistryLocation NeoEE = GameSettingsWorld.Settings(Product.NeoEE, Game.EmpireEarth);
        private static readonly RegistryLocation NeoAoC = GameSettingsWorld.Settings(Product.NeoEE, Game.ArtOfConquest);
        private static readonly RegistryLocation NeoMarker = GameSettingsWorld.Marker(Product.NeoEE);
        private static readonly RegistryLocation Gpu = GameSettingsWorld.GpuPreferences;

        private GameSettingsWorld w;

        [SetUp]
        public void SetUp()
        {
            w = new GameSettingsWorld();
        }

        private static RegistryValue Sz(string text)
        {
            return RegistryValue.FromString(text);
        }

        private static RegistryValue Dw(int number)
        {
            return RegistryValue.FromDWord(number);
        }

        private DefaultsStartup Start(string userChoice = null)
        {
            return w.CreateDefaultsService().ApplyAtLauncherStart(w.Discover(userChoice));
        }

        private Installation Installation(string userChoice = null)
        {
            return w.Discover(userChoice).Selected;
        }

        // --- Second account, launcher start (R1, forum report section 8 test cases 1 and 7) ---------------------------

        [Test]
        public void Start_SecondAccount_CreatesClassSDefaultsGpuPreferenceAndMarkers()
        {
            w.AddAdminInstallationOfAnotherAccount(NeoRoot, Product.NeoEE);

            DefaultsStartup startup = Start();

            Assert.That(startup.Block, Is.Null);
            Assert.That(startup.Question, Is.Null);
            Assert.That(startup.Games.Select(g => g.Game.Id + ":" + g.InstalledFrom + "," + g.Defaults),
                Is.EqualTo(new[] { "EE:Created,FirstRun", "AoC:Created,FirstRun" }));
            // Class S, byte-identical with contract 3.3: AoC starts without EE having been started (t=2825 p=19423).
            Assert.That(w.Get(NeoEE, "Installed From Volume"), Is.EqualTo(Sz("C:")));
            Assert.That(w.Get(NeoEE, "Installed From Directory"), Is.EqualTo(Sz(@"\PROGRAM FILES (X86)\NEO EMPIRE EARTH\Empire Earth\")));
            Assert.That(w.Get(NeoAoC, "Installed From Directory"),
                Is.EqualTo(Sz(@"\PROGRAM FILES (X86)\NEO EMPIRE EARTH\Empire Earth - The Art of Conquest\")));
            // D, P, GPU preference and the markers.
            Assert.That(w.Get(NeoEE, "Rasterizer Name"), Is.EqualTo(Sz("Direct3D Hardware TnL")));
            Assert.That(w.Get(NeoEE, "Wait for VSync"), Is.EqualTo(Dw(0)));
            Assert.That(w.Get(NeoEE, "Game Window Width"), Is.EqualTo(Dw(1920)));
            Assert.That(w.Get(NeoAoC, "Texture Bit Depth"), Is.EqualTo(Dw(32)));
            Assert.That(w.Get(NeoEE, "AutoSave In Milliseconds"), Is.EqualTo(Dw(1200000)));
            Assert.That(w.Get(NeoEE.Child("Game Options"), "Ending Epoch"), Is.EqualTo(Dw(13)));
            Assert.That(w.Get(NeoAoC.Child("Game Options"), "Ending Epoch"), Is.EqualTo(Dw(14)));
            Assert.That(w.Get(Gpu, NeoEeFolder + @"\Empire Earth.exe"), Is.EqualTo(Sz("GpuPreference=2;")));
            Assert.That(w.Get(Gpu, NeoAocFolder + @"\EE-AOC.exe"), Is.EqualTo(Sz("GpuPreference=2;")));
            Assert.That(w.Get(NeoMarker, "EE"), Is.EqualTo(Dw(1)));
            Assert.That(w.Get(NeoMarker, "AoC"), Is.EqualTo(Dw(1)));
            Assert.That(w.ValuesBelow(NeoEE), Has.Count.EqualTo(GameSettingsTable.All.Count));
        }

        /// <summary>
        /// Build/UI review: the defaults at the launcher start and a quick first Play run on two threads of the pool. The
        /// writing methods run one at a time, so Play waits and then finds the marker: one first run, not two.
        /// </summary>
        [Test]
        public void Start_AndAFirstPlayAtTheSameTime_RunOneAfterTheOther()
        {
            w.AddAdminInstallationOfAnotherAccount(NeoRoot, Product.NeoEE);
            DiscoveryResult discovery = w.Discover();
            using (var blocking = new BlockingRegistry(w.Registry))
            {
                var service = new GameDefaultsService(blocking, w.FileSystem, w.SystemInfo, w.Guard, w.Backups, w.Logger);

                Task<DefaultsStartup> start = Task.Run(() => service.ApplyAtLauncherStart(discovery));
                Assert.That(blocking.Entered.WaitOne(TimeSpan.FromSeconds(10)), Is.True, "the start reads the registry");
                Task<DefaultsAtStart> play = Task.Run(() => service.ApplyDefaultsIfNeeded(discovery.Selected, Game.EmpireEarth, out _));
                Thread.Sleep(200);
                Assert.That(blocking.Waiting, Is.EqualTo(1), "Play waits for the start, it does not read the registry meanwhile");
                Assert.That(play.IsCompleted, Is.False);

                blocking.Release();
                Assert.That(Task.WaitAll(new Task[] { start, play }, TimeSpan.FromSeconds(10)), Is.True);

                Assert.That(start.Result.Games.First(g => g.Game == Game.EmpireEarth).Defaults, Is.EqualTo(DefaultsAtStart.FirstRun));
                Assert.That(play.Result, Is.EqualTo(DefaultsAtStart.None), "Play finds the marker of the start");
                Assert.That(w.Logger.MessagesOf(LogLevel.Info).Count(m => m.StartsWith("Game defaults: first run of NeoEE EE ", StringComparison.Ordinal)),
                    Is.EqualTo(1));
            }
        }

        [Test]
        public void Start_ClassS_ExistingValuesAreNeverChanged_EvenWhenTheyDiffer()
        {
            w.AddAdminInstallationOfAnotherAccount(NeoRoot, Product.NeoEE);
            w.RawRegistry.Seed(NeoEE, "Installed From Volume", Sz("D:"));
            w.RawRegistry.Seed(NeoEE, "Installed From Directory", Sz(@"\COPY\Empire Earth\"));

            DefaultsStartup startup = Start();

            Assert.That(startup.Games[0].InstalledFrom, Is.EqualTo(InstalledFromAtStart.Present));
            Assert.That(w.Get(NeoEE, "Installed From Volume"), Is.EqualTo(Sz("D:")));
            Assert.That(w.Get(NeoEE, "Installed From Directory"), Is.EqualTo(Sz(@"\COPY\Empire Earth\")));
            Assert.That(w.Changes.Where(c => c.Contains("Installed From") && c.Contains(@"Neo\Empire Earth @")), Is.Empty);
        }

        [Test]
        public void Start_ClassS_OnlyOneValueMissing_NothingWrittenAndLogged()
        {
            w.AddAdminInstallationOfAnotherAccount(NeoRoot, Product.NeoEE);
            w.RawRegistry.Seed(NeoEE, "Installed From Volume", Sz("C:"));

            DefaultsStartup startup = Start();

            Assert.That(startup.Games[0].InstalledFrom, Is.EqualTo(InstalledFromAtStart.Incomplete));
            Assert.That(w.Get(NeoEE, "Installed From Directory"), Is.Null);
            Assert.That(w.Logger.MessagesOf(LogLevel.Info), Has.Some.Contains("only one of the \"Installed From\" values of " + NeoEE));
        }

        /// <summary>Several installations share the settings key: the start writes nothing at all (ADR 0015).</summary>
        [Test]
        public void Start_Ambiguous_WritesNothing()
        {
            w.World.AddLegacyInstallation(GameSettingsWorld.EERoot, Product.EE);
            w.World.AddForeignInstallation(@"C:\Games\EE");
            Assert.That(w.Discover().Installations, Has.Count.EqualTo(2));

            DefaultsStartup startup = Start();

            Assert.That(startup.Games.All(g => g.Defaults == DefaultsAtStart.Ambiguous && g.InstalledFrom == InstalledFromAtStart.NotChecked));
            Assert.That(w.Changes, Is.Empty);
            Assert.That(w.Logger.MessagesOf(LogLevel.Info), Has.Some.Contains("share its game settings"));
        }

        /// <summary>
        /// Laptop test TP-93: the uninstall key of the suite has the publisher of EE and was taken for a second EE
        /// installation, which made the EE settings ambiguous. Marked, or found by the record of an older suite, it is none.
        /// </summary>
        [TestCase(true)]
        [TestCase(false)]
        public void Start_TheSuiteUninstallKey_LeavesTheEeSettingsUnambiguous(bool marker)
        {
            w.AddAdminInstallationOfAnotherAccount(GameSettingsWorld.EERoot, Product.EE);
            w.FileSystem.AddFile(InstallationWorld.SuiteRoot + @"\Empire Earth Launcher.exe", "exe");
            w.World.AddSuiteUninstallKey(marker: marker);
            if (!marker)
                w.World.AddSuiteRecord();

            DefaultsStartup startup = Start();

            Assert.That(startup.Games, Is.Not.Empty);
            Assert.That(startup.Games.Select(g => g.Defaults), Is.All.EqualTo(DefaultsAtStart.FirstRun));
            Assert.That(startup.Games.Select(g => g.InstalledFrom), Is.All.EqualTo(InstalledFromAtStart.Created));
            Assert.That(w.Logger.MessagesOf(LogLevel.Info), Has.None.Contains("share its game settings"));
        }

        /// <summary>The user's choice makes its installation unambiguous; the other one stays untouched.</summary>
        [Test]
        public void Start_UserChoice_IsUnambiguous()
        {
            w.World.AddLegacyInstallation(GameSettingsWorld.EERoot, Product.EE, artOfConquest: false);
            w.World.AddForeignInstallation(@"C:\Games\EE");

            DefaultsStartup startup = Start(@"C:\Games\EE");

            GameDefaultsAtStart chosen = startup.Games.Single(g => g.Installation.EeFolder.Equals(@"C:\GAMES\EE", StringComparison.OrdinalIgnoreCase));
            Assert.That(chosen.Defaults, Is.EqualTo(DefaultsAtStart.FirstRun));
            Assert.That(startup.Games.Where(g => g != chosen).Select(g => g.Defaults), Is.All.EqualTo(DefaultsAtStart.Ambiguous));
            Assert.That(w.Get(GameSettingsWorld.Marker(Product.EE), "EE"), Is.EqualTo(Dw(1)));
        }

        [TestCase("NeoEE_Setup", MutationBlock.SetupRunning)]
        [TestCase("EE_Setup", MutationBlock.SetupRunning)]
        [TestCase("StainlessSteelStudiosPresentsEmpireEarth", MutationBlock.GameRunning)]
        [TestCase("MadDocSoftwarePresentsEmpireEarthExpansion", MutationBlock.GameRunning)]
        public void Start_BlockedByTheMutationGuard_WritesNothing(string mutex, MutationBlock block)
        {
            w.AddAdminInstallationOfAnotherAccount(NeoRoot, Product.NeoEE);
            w.Mutexes.With(mutex);

            DefaultsStartup startup = Start();

            Assert.That(startup.Block.Block, Is.EqualTo(block));
            Assert.That(startup.Games, Is.Empty);
            Assert.That(w.Changes, Is.Empty);
        }

        [Test]
        public void Start_ChosenFolderMissing_WritesNothing()
        {
            w.World.AddEmpireEarth(@"C:\Games\EE");

            DefaultsStartup startup = Start(@"C:\Games\Gone");

            Assert.That(startup.Games.Single().Defaults, Is.EqualTo(DefaultsAtStart.FolderMissing));
            Assert.That(w.Changes, Is.Empty);
        }

        // --- First run with differing display values: one question --------------------------------------------------

        private DefaultsStartup StartWithDifferingDisplayValues()
        {
            w.AddAdminInstallationOfAnotherAccount(NeoRoot, Product.NeoEE, components: "game");
            w.RawRegistry.Seed(NeoEE, "Game Bit Depth", Dw(16));
            w.RawRegistry.Seed(NeoEE, "Rasterizer Name", Sz("direct3d hardware tnl"));
            return Start();
        }

        [Test]
        public void FirstRun_DifferingDisplayValues_OneQuestion_NoMarkerYet()
        {
            DefaultsStartup startup = StartWithDifferingDisplayValues();

            Assert.That(startup.Games.Single().Defaults, Is.EqualTo(DefaultsAtStart.FirstRunWithQuestion));
            DisplayQuestionItem item = startup.Question.Items.Single();
            Assert.That(item.Differences.Select(d => d.Setting.Name), Is.EqualTo(new[] { "Game Bit Depth" }),
                "the rasterizer differs only in case");
            Assert.That(item.Differences[0].Current, Is.EqualTo(Dw(16)));
            Assert.That(item.Differences[0].Recommended, Is.EqualTo(Dw(32)));
            Assert.That(w.Get(NeoEE, "Game Bit Depth"), Is.EqualTo(Dw(16)), "never overwritten without consent");
            Assert.That(w.Get(NeoEE, "Wait for VSync"), Is.EqualTo(Dw(0)), "missing D values are created");
            Assert.That(w.Get(NeoEE, "Music Volume"), Is.EqualTo(Dw(44)), "P values are created");
            Assert.That(w.Get(NeoMarker, "EE"), Is.Null, "the marker follows the answer");
        }

        [Test]
        public void FirstRun_AnswerYes_BackupThenDisplayValuesThenMarker()
        {
            DefaultsStartup startup = StartWithDifferingDisplayValues();

            GameSettingsResult result = w.CreateDefaultsService().AnswerDisplayQuestion(startup.Question, true);

            Assert.That(result.Outcome, Is.EqualTo(GameSettingsOutcome.Done), result.ToString());
            Assert.That(result.BackupFiles, Has.Count.EqualTo(1));
            Assert.That(result.BackupFiles[0], Does.StartWith(GameSettingsWorld.BackupsFolder + @"\").And.Contain("_display-settings")
                .And.EndWith("_NeoEE_EE.reg"));
            string backup = Encoding.Unicode.GetString(w.FileSystem.GetContent(result.BackupFiles[0]));
            Assert.That(backup, Does.Contain("\"Game Bit Depth\"=dword:00000010"));
            Assert.That(w.Get(NeoEE, "Game Bit Depth"), Is.EqualTo(Dw(32)));
            Assert.That(w.Get(NeoEE, "Rasterizer Name"), Is.EqualTo(Sz("Direct3D Hardware TnL")));
            Assert.That(w.Get(NeoMarker, "EE"), Is.EqualTo(Dw(1)));
        }

        [Test]
        public void FirstRun_AnswerNo_KeepsTheValues_WritesTheMarker()
        {
            DefaultsStartup startup = StartWithDifferingDisplayValues();

            GameSettingsResult result = w.CreateDefaultsService().AnswerDisplayQuestion(startup.Question, false);

            Assert.That(result.Outcome, Is.EqualTo(GameSettingsOutcome.Done));
            Assert.That(result.BackupFiles, Is.Empty);
            Assert.That(w.Get(NeoEE, "Game Bit Depth"), Is.EqualTo(Dw(16)));
            Assert.That(w.Get(NeoMarker, "EE"), Is.EqualTo(Dw(1)));
            Assert.That(Start().Question, Is.Null, "asked once");
        }

        [Test]
        public void FirstRun_AnswerYes_BackupFails_NothingChanged_QuestionStays()
        {
            DefaultsStartup startup = StartWithDifferingDisplayValues();
            int changesBefore = w.Changes.Count;
            w.FileSystem.FailOn(GameSettingsWorld.BackupsFolder, FileSystemOperation.Write, FileSystemStatus.IoError);

            GameSettingsResult result = w.CreateDefaultsService().AnswerDisplayQuestion(startup.Question, true);

            Assert.That(result.Outcome, Is.EqualTo(GameSettingsOutcome.BackupFailed));
            Assert.That(w.Changes, Has.Count.EqualTo(changesBefore));
            Assert.That(w.Get(NeoMarker, "EE"), Is.Null);
        }

        // --- Markers (contract 3.5) ----------------------------------------------------------------------------------

        [Test]
        public void Marker_Lower_CreatesOnlyMissingValues_NoQuestion_RaisesTheMarker()
        {
            w.AddAdminInstallationOfAnotherAccount(NeoRoot, Product.NeoEE, components: "game");
            w.RawRegistry.Seed(NeoMarker, "EE", Dw(0));
            w.RawRegistry.Seed(NeoEE, "Music Volume", Dw(10));
            w.RawRegistry.Seed(NeoEE, "Game Bit Depth", Dw(16));

            DefaultsStartup startup = Start();

            Assert.That(startup.Games.Single().Defaults, Is.EqualTo(DefaultsAtStart.Updated));
            Assert.That(startup.Question, Is.Null);
            Assert.That(w.Get(NeoEE, "Music Volume"), Is.EqualTo(Dw(10)));
            Assert.That(w.Get(NeoEE, "Game Bit Depth"), Is.EqualTo(Dw(16)));
            Assert.That(w.Get(NeoEE, "Sound Volume"), Is.EqualTo(Dw(60)));
            Assert.That(w.Get(NeoMarker, "EE"), Is.EqualTo(Dw(1)));
        }

        [TestCase(1, DefaultsStatus.Applied)]
        [TestCase(2, DefaultsStatus.AppliedByNewerVersion)]
        public void Marker_CurrentOrHigher_NothingIsOverwritten(int marker, DefaultsStatus status)
        {
            w.AddAdminInstallationOfAnotherAccount(NeoRoot, Product.NeoEE, components: "game");
            w.RawRegistry.Seed(NeoMarker, "EE", Dw(marker));
            w.RawRegistry.Seed(NeoEE, "Installed From Volume", Sz("C:"));
            w.RawRegistry.Seed(NeoEE, "Installed From Directory", Sz(@"\PROGRAM FILES (X86)\NEO EMPIRE EARTH\Empire Earth\"));

            DefaultsStartup startup = Start();

            Assert.That(startup.Games.Single().Defaults, Is.EqualTo(DefaultsAtStart.None));
            Assert.That(w.Changes, Is.Empty);
            Assert.That(w.Get(NeoMarker, "EE"), Is.EqualTo(Dw(marker)));
            Assert.That(w.CreateDefaultsService().GetStatus(Installation(), Game.EmpireEarth, true), Is.EqualTo(status));
        }

        [Test]
        public void Marker_OfAnotherType_CountsAsMissing()
        {
            w.AddAdminInstallationOfAnotherAccount(NeoRoot, Product.NeoEE, components: "game");
            w.RawRegistry.Seed(NeoMarker, "EE", Sz("1"));

            Assert.That(Start().Games.Single().Defaults, Is.EqualTo(DefaultsAtStart.FirstRun));
            Assert.That(w.Get(NeoMarker, "EE"), Is.EqualTo(Dw(1)));
        }

        [Test]
        public void Status_MissingMarker_PendingOrWaitingForPlay()
        {
            w.AddAdminInstallationOfAnotherAccount(NeoRoot, Product.NeoEE, components: "game");
            GameDefaultsService service = w.CreateDefaultsService();

            Assert.That(service.GetStatus(Installation(), Game.EmpireEarth, true), Is.EqualTo(DefaultsStatus.Pending));
            Assert.That(service.GetStatus(Installation(), Game.EmpireEarth, false), Is.EqualTo(DefaultsStatus.WaitingForPlay));
        }

        // --- Newer contract (contract 5) -----------------------------------------------------------------------------

        [Test]
        public void NewerContract_NoDefaultsNoResetNoDisplay_ButClassSAtStart()
        {
            w.AddAdminInstallationOfAnotherAccount(NeoRoot, Product.NeoEE, components: "game", contractVersion: 2);
            Installation installation = Installation();
            Assert.That(installation.HasNewerContract, Is.True);
            GameDefaultsService service = w.CreateDefaultsService();

            DefaultsStartup startup = Start();
            int changesAfterStart = w.Changes.Count;
            GameSettingsResult reset = service.Reset(installation);
            GameSettingsResult display = service.ApplyRecommendedDisplay(installation);

            Assert.That(startup.Games.Single().Defaults, Is.EqualTo(DefaultsAtStart.NewerContract));
            Assert.That(startup.Games.Single().InstalledFrom, Is.EqualTo(InstalledFromAtStart.Created));
            Assert.That(w.Get(NeoEE, "Music Volume"), Is.Null);
            Assert.That(w.Get(NeoMarker, "EE"), Is.Null);
            Assert.That(reset.Outcome, Is.EqualTo(GameSettingsOutcome.NewerContract));
            Assert.That(display.Outcome, Is.EqualTo(GameSettingsOutcome.NewerContract));
            Assert.That(w.Changes, Has.Count.EqualTo(changesAfterStart));
            Assert.That(service.GetStatus(installation, Game.EmpireEarth, true), Is.EqualTo(DefaultsStatus.NewerContract));
        }

        // --- Class S before Play (public API for L-WP6) --------------------------------------------------------------

        [Test]
        public void Sync_ForeignInstallationWhoseValuesNameItsOwnFolder_StaysUntouched()
        {
            w.World.AddForeignInstallation(@"C:\Games\EE");
            w.RawRegistry.Seed(GameSettingsWorld.Settings(Product.EE, Game.EmpireEarth), "Installed From Volume", Sz("c:"));
            w.RawRegistry.Seed(GameSettingsWorld.Settings(Product.EE, Game.EmpireEarth), "Installed From Directory", Sz(@"\games\\EE\\"));

            GameSettingsResult result = w.CreateDefaultsService().SynchronizeInstalledFrom(Installation(), Game.EmpireEarth);

            Assert.That(result.Outcome, Is.EqualTo(GameSettingsOutcome.Done));
            Assert.That(result.Changes, Is.Empty);
            Assert.That(w.Changes, Is.Empty);
        }

        [Test]
        public void Sync_WritesOnlyWhatDiffers_AndLogsOldAndNew()
        {
            w.World.AddLegacyInstallation(NeoRoot, Product.NeoEE);
            w.RawRegistry.Seed(NeoEE, "Installed From Directory", Sz(@"\COPY\Empire Earth\"));

            GameSettingsResult result = w.CreateDefaultsService().SynchronizeInstalledFrom(Installation(), Game.EmpireEarth);

            Assert.That(result.Changes.Select(c => c.ValueName), Is.EqualTo(new[] { "Installed From Directory" }));
            Assert.That(w.Get(NeoEE, "Installed From Directory"), Is.EqualTo(Sz(@"\PROGRAM FILES (X86)\NEO EMPIRE EARTH\Empire Earth\")));
            Assert.That(w.Logger.MessagesOf(LogLevel.Info), Has.Some.Contains(@"REG_SZ ""\COPY\Empire Earth\"" -> REG_SZ ""\PROGRAM FILES (X86)\NEO EMPIRE EARTH\Empire Earth\"""));
        }

        [Test]
        public void Sync_AValueOfAnotherTypeIsDeletedFirst()
        {
            w.World.AddLegacyInstallation(NeoRoot, Product.NeoEE);
            w.RawRegistry.Seed(NeoAoC, "Installed From Volume", Dw(3));

            w.CreateDefaultsService().SynchronizeInstalledFrom(Installation(), Game.ArtOfConquest);

            Assert.That(w.Changes, Has.Some.EqualTo("DeleteValue " + NeoAoC + " @\"Installed From Volume\""));
            Assert.That(w.Get(NeoAoC, "Installed From Volume"), Is.EqualTo(Sz("C:")));
            Assert.That(w.Get(NeoAoC, "Installed From Directory"),
                Is.EqualTo(Sz(@"\PROGRAM FILES (X86)\NEO EMPIRE EARTH\Empire Earth - The Art of Conquest\")));
        }

        [Test]
        public void Sync_ForeignFoldersKeepTheirRealNames()
        {
            w.World.AddForeignInstallation(@"D:\Empire Earth");
            Installation installation = Installation(); // found through its "Installed From" values, then they change
            RegistryLocation sssi = GameSettingsWorld.Settings(Product.EE, Game.EmpireEarth);
            w.RawRegistry.Seed(sssi, "Installed From Volume", Sz("C:"));
            w.RawRegistry.DeleteValue(sssi, "Installed From Directory");

            w.CreateDefaultsService().SynchronizeInstalledFrom(installation, Game.EmpireEarth);

            Assert.That(w.Get(sssi, "Installed From Volume"), Is.EqualTo(Sz("D:")));
            Assert.That(w.Get(sssi, "Installed From Directory"), Is.EqualTo(Sz(@"\Empire Earth\")));
        }

        [Test]
        public void Sync_NetworkPath_NoValuesAndAWarning()
        {
            w.World.AddEmpireEarth(@"\\server\games\Empire Earth");

            GameSettingsResult result = w.CreateDefaultsService().SynchronizeInstalledFrom(Installation(@"\\server\games\Empire Earth"),
                Game.EmpireEarth);

            Assert.That(result.Outcome, Is.EqualTo(GameSettingsOutcome.NotOnADrive));
            Assert.That(w.Changes, Is.Empty);
            Assert.That(w.Logger.MessagesOf(LogLevel.Warning), Has.Some.Contains("is not on a drive letter"));
        }

        [Test]
        [SetCulture("tr-TR")]
        public void Sync_UnderTurkishCulture_TheSetupSpellingIsTheSameFolder()
        {
            w.World.AddForeignInstallation(@"C:\Oyunlar\Bilgisayar\Empire Earth");

            GameSettingsResult result = w.CreateDefaultsService().SynchronizeInstalledFrom(Installation(), Game.EmpireEarth);

            Assert.That(result.Changes, Is.Empty, "the values the setup helper wrote (ASCII upper case) already name the folder");
        }

        // --- Recommended display settings ------------------------------------------------------------------------------

        [Test]
        public void ApplyRecommendedDisplay_BacksUpThenOverwritesOnlyTheDisplayValues()
        {
            w.World.AddLegacyInstallation(NeoRoot, Product.NeoEE, artOfConquest: false);
            w.RawRegistry.Seed(NeoEE, "Game Window Width", Dw(800));
            w.RawRegistry.Seed(NeoEE, "Music Volume", Dw(10));

            GameSettingsResult result = w.CreateDefaultsService().ApplyRecommendedDisplay(Installation());

            Assert.That(result.Outcome, Is.EqualTo(GameSettingsOutcome.Done));
            Assert.That(result.BackupFolder, Does.Contain("_display-settings"));
            Assert.That(w.Get(NeoEE, "Game Window Width"), Is.EqualTo(Dw(1920)));
            Assert.That(w.Get(NeoEE, "Music Volume"), Is.EqualTo(Dw(10)));
            Assert.That(result.Changes.Select(c => GameSettingsTable.Find(c.ValueName).Class), Is.All.EqualTo(SettingClass.D));
        }

        // --- Game window size of the graphics page (launcher 1.1.0, contract 3.2) ---------------------------------------

        private Installation SeedForGameWindow(bool artOfConquest = true)
        {
            w.World.AddLegacyInstallation(NeoRoot, Product.NeoEE, artOfConquest: artOfConquest);
            w.RawRegistry.Seed(NeoEE, "Game Window Width", Dw(1920));
            w.RawRegistry.Seed(NeoEE, "Game Window Height", Dw(1080));
            w.RawRegistry.Seed(NeoEE, "Music Volume", Dw(10));
            w.RawRegistry.Seed(NeoEE, "Game Bit Depth", Dw(16));
            if (artOfConquest)
            {
                w.RawRegistry.Seed(NeoAoC, "Game Window Width", Dw(1920));
                w.RawRegistry.Seed(NeoAoC, "Game Window Height", Dw(1080));
            }
            return Installation();
        }

        [Test]
        public void SetGameWindow_BacksUpThenWritesOnlyTheTwoWindowValuesOfEveryGame()
        {
            Installation installation = SeedForGameWindow();

            GameSettingsResult result = w.CreateDefaultsService().SetGameWindow(installation, new ScreenSize(1600, 900));

            Assert.That(result.Outcome, Is.EqualTo(GameSettingsOutcome.Done), result.ToString());
            Assert.That(result.BackupFiles, Has.Count.EqualTo(2));
            Assert.That(result.BackupFolder, Does.Contain("_game-window"));
            string backup = Encoding.Unicode.GetString(w.FileSystem.GetContent(result.BackupFiles[0]));
            Assert.That(backup, Does.Contain("\"Game Window Width\"=dword:00000780"), "the old width 1920 is in the backup");
            Assert.That(w.Get(NeoEE, "Game Window Width"), Is.EqualTo(Dw(1600)));
            Assert.That(w.Get(NeoEE, "Game Window Height"), Is.EqualTo(Dw(900)));
            Assert.That(w.Get(NeoAoC, "Game Window Width"), Is.EqualTo(Dw(1600)));
            Assert.That(w.Get(NeoAoC, "Game Window Height"), Is.EqualTo(Dw(900)));
            Assert.That(w.Get(NeoEE, "Music Volume"), Is.EqualTo(Dw(10)), "no other value changes");
            Assert.That(w.Get(NeoEE, "Game Bit Depth"), Is.EqualTo(Dw(16)), "not even a display value that looks wrong");
            Assert.That(result.Changes.Select(change => change.ValueName),
                Is.EquivalentTo(new[] { "Game Window Width", "Game Window Height", "Game Window Width", "Game Window Height" }));
        }

        [Test]
        public void SetGameWindow_OnlyTheGamesThatAreInstalled()
        {
            Installation installation = SeedForGameWindow(artOfConquest: false);

            GameSettingsResult result = w.CreateDefaultsService().SetGameWindow(installation, new ScreenSize(1280, 960));

            Assert.That(result.Outcome, Is.EqualTo(GameSettingsOutcome.Done));
            Assert.That(result.BackupFiles, Has.Count.EqualTo(1));
            Assert.That(w.Get(NeoAoC, "Game Window Width"), Is.Null, "no key is created for a game that is not installed");
        }

        [Test]
        public void SetGameWindow_CreatesTheValuesWhenTheyAreMissing()
        {
            w.World.AddLegacyInstallation(NeoRoot, Product.NeoEE, artOfConquest: false);

            GameSettingsResult result = w.CreateDefaultsService().SetGameWindow(Installation(), new ScreenSize(1024, 768));

            Assert.That(result.IsDone, Is.True, result.ToString());
            Assert.That(w.Get(NeoEE, "Game Window Width"), Is.EqualTo(Dw(1024)));
            Assert.That(w.Get(NeoEE, "Game Window Height"), Is.EqualTo(Dw(768)));
        }

        [Test]
        public void SetGameWindow_TheSameSize_ChangesNothing()
        {
            Installation installation = SeedForGameWindow();

            GameSettingsResult result = w.CreateDefaultsService().SetGameWindow(installation, new ScreenSize(1920, 1080));

            Assert.That(result.IsDone, Is.True);
            Assert.That(result.Changes, Is.Empty);
        }

        [Test]
        public void SetGameWindow_1920x1200_IsWithinTheLimitsOfRevision6()
        {
            Installation installation = SeedForGameWindow();

            GameSettingsResult result = w.CreateDefaultsService().SetGameWindow(installation, new ScreenSize(1920, 1200));

            Assert.That(result.IsDone, Is.True, result.ToString());
            Assert.That(w.Get(NeoEE, "Game Window Width"), Is.EqualTo(Dw(1920)));
            Assert.That(w.Get(NeoEE, "Game Window Height"), Is.EqualTo(Dw(1200)));
            Assert.That(w.Get(NeoAoC, "Game Window Height"), Is.EqualTo(Dw(1200)));
        }

        [Test]
        public void SetGameWindow_DoesNotTouchTheMarkerOrTheOtherClasses()
        {
            Installation installation = SeedForGameWindow();

            w.CreateDefaultsService().SetGameWindow(installation, new ScreenSize(1280, 960));

            Assert.That(w.Get(NeoMarker, "EE"), Is.Null, "the choice is no answer to the display question");
            Assert.That(w.Get(NeoEE, "Rasterizer Name"), Is.Null);
            Assert.That(w.Get(NeoEE, "Installed From Volume"), Is.EqualTo(Sz("C:")), "the value of the setup, not written again");
        }

        [Test]
        public void SetGameWindow_ALogLineNamesTheChoice()
        {
            Installation installation = SeedForGameWindow();

            w.CreateDefaultsService().SetGameWindow(installation, new ScreenSize(1600, 900));

            Assert.That(w.Logger.MessagesOf(LogLevel.Info), Has.Some.Contains("the player chose the game window size 1600x900"));
        }

        [TestCase(1920, 1201)]
        [TestCase(1921, 1080)]
        [TestCase(2560, 1440)]
        [TestCase(800, 600)]
        [TestCase(1024, 767)]
        [TestCase(0, 0)]
        public void SetGameWindow_ASizeOutsideTheLimitsOfContract33_IsRefused(int width, int height)
        {
            Installation installation = SeedForGameWindow();
            int changes = w.Changes.Count;

            Assert.That(() => w.CreateDefaultsService().SetGameWindow(installation, new ScreenSize(width, height)),
                Throws.TypeOf<ArgumentOutOfRangeException>());

            Assert.That(w.Changes, Has.Count.EqualTo(changes));
            Assert.That(w.FileSystem.DirectoryExists(GameSettingsWorld.BackupsFolder), Is.False);
        }

        [Test]
        public void SetGameWindow_BackupFails_NothingChanged()
        {
            Installation installation = SeedForGameWindow();
            int changes = w.Changes.Count;
            w.FileSystem.FailOn(GameSettingsWorld.BackupsFolder, FileSystemOperation.Write, FileSystemStatus.IoError);

            GameSettingsResult result = w.CreateDefaultsService().SetGameWindow(installation, new ScreenSize(1600, 900));

            Assert.That(result.Outcome, Is.EqualTo(GameSettingsOutcome.BackupFailed));
            Assert.That(w.Changes, Has.Count.EqualTo(changes));
            Assert.That(w.Get(NeoEE, "Game Window Width"), Is.EqualTo(Dw(1920)));
        }

        [Test]
        public void SetGameWindow_NewerContract_IsRefused()
        {
            w.AddAdminInstallationOfAnotherAccount(NeoRoot, Product.NeoEE, components: "game", contractVersion: 2);
            Installation installation = Installation();
            int changes = w.Changes.Count;

            GameSettingsResult result = w.CreateDefaultsService().SetGameWindow(installation, new ScreenSize(1600, 900));

            Assert.That(result.Outcome, Is.EqualTo(GameSettingsOutcome.NewerContract));
            Assert.That(w.Changes, Has.Count.EqualTo(changes));
        }

        [Test]
        public void SetGameWindow_PassesTheWritePolicy_OnlyTheTwoValueNamesOfTheTable()
        {
            Installation installation = SeedForGameWindow();

            w.CreateDefaultsService().SetGameWindow(installation, new ScreenSize(1600, 900));

            // The registry of the world is wrapped in LauncherWritePolicy (allow-list by value name): an unknown name would throw.
            Assert.That(w.Changes.Where(change => change.StartsWith("SetValue", StringComparison.Ordinal))
                              .Select(change => change.Split('@')[1].Split(new[] { " = " }, StringSplitOptions.None)[0]),
                Is.All.Matches<string>(name => name == "\"Game Window Width\"" || name == "\"Game Window Height\""));
        }

        [Test]
        public void ReadGameWindow_ReadsBothValues()
        {
            Installation installation = SeedForGameWindow();
            w.RawRegistry.Seed(NeoEE, "Game Window Width", Dw(1366));
            w.RawRegistry.Seed(NeoEE, "Game Window Height", Dw(768));

            Assert.That(w.CreateDefaultsService().ReadGameWindow(installation, Game.EmpireEarth), Is.EqualTo(new ScreenSize(1366, 768)));
            Assert.That(w.CreateDefaultsService().ReadGameWindow(installation, Game.ArtOfConquest), Is.EqualTo(new ScreenSize(1920, 1080)));
        }

        [Test]
        public void ReadGameWindow_AMissingOrDamagedValue_IsEmpty()
        {
            w.World.AddLegacyInstallation(NeoRoot, Product.NeoEE, artOfConquest: false);
            Installation installation = Installation();
            GameDefaultsService service = w.CreateDefaultsService();
            Assert.That(service.ReadGameWindow(installation, Game.EmpireEarth), Is.EqualTo(ScreenSize.Empty), "missing");

            w.RawRegistry.Seed(NeoEE, "Game Window Width", Dw(1600));
            Assert.That(service.ReadGameWindow(installation, Game.EmpireEarth), Is.EqualTo(ScreenSize.Empty), "height missing");

            w.RawRegistry.Seed(NeoEE, "Game Window Height", Sz("900"));
            Assert.That(service.ReadGameWindow(installation, Game.EmpireEarth), Is.EqualTo(ScreenSize.Empty), "not a REG_DWORD");

            w.RawRegistry.Seed(NeoEE, "Game Window Height", Dw(-1));
            Assert.That(service.ReadGameWindow(installation, Game.EmpireEarth), Is.EqualTo(ScreenSize.Empty), "not positive");
        }

        [Test]
        public void ReadGameWindow_WritesNothing()
        {
            Installation installation = SeedForGameWindow();
            int changes = w.Changes.Count;

            w.CreateDefaultsService().ReadGameWindow(installation, Game.EmpireEarth);

            Assert.That(w.Changes, Has.Count.EqualTo(changes));
        }

        // --- Reset (contract 3.6, R4) --------------------------------------------------------------------------------

        private Installation SeedForReset()
        {
            w.AddAdminInstallationOfAnotherAccount(NeoRoot, Product.NeoEE);
            w.RawRegistry.Seed(NeoEE, "Player Name", Sz("not in the table"));
            w.RawRegistry.Seed(NeoEE.Child("Game Options"), "Last Map", Sz("not in the table"));
            w.RawRegistry.Seed(NeoEE, "Game Bit Depth", Sz("16"));
            w.RawRegistry.Seed(NeoEE, "Music Volume", Dw(10));
            w.RawRegistry.Seed(NeoEE, "Installed From Volume", Sz("D:"));
            w.RawRegistry.Seed(NeoEE, "Installed From Directory", Sz(@"\COPY\Empire Earth\"));
            w.RawRegistry.Seed(NeoAoC, "Rasterizer Name", Sz("Direct3D"));
            w.RawRegistry.Seed(Gpu, @"C:\Other\Game.exe", Sz("GpuPreference=1;"));
            return Installation();
        }

        [Test]
        public void Reset_OverwritesTheTableValues_LeavesEverythingElse_MarkerLast()
        {
            Installation installation = SeedForReset();

            GameSettingsResult result = w.CreateDefaultsService().Reset(installation);

            Assert.That(result.Outcome, Is.EqualTo(GameSettingsOutcome.Done), result.ToString());
            Assert.That(w.Get(NeoEE, "Game Bit Depth"), Is.EqualTo(Dw(32)));
            Assert.That(w.Get(NeoEE, "Music Volume"), Is.EqualTo(Dw(44)));
            Assert.That(w.Get(NeoEE, "Installed From Volume"), Is.EqualTo(Sz("C:")));
            Assert.That(w.Get(NeoAoC, "Rasterizer Name"), Is.EqualTo(Sz("Direct3D Hardware TnL")));
            Assert.That(w.Get(Gpu, NeoEeFolder + @"\Empire Earth.exe"), Is.EqualTo(Sz("GpuPreference=2;")));
            Assert.That(w.Get(NeoMarker, "AoC"), Is.EqualTo(Dw(1)));
            // Values outside the table stay as they are (contract 3.2).
            Assert.That(w.Get(NeoEE, "Player Name"), Is.EqualTo(Sz("not in the table")));
            Assert.That(w.Get(NeoEE.Child("Game Options"), "Last Map"), Is.EqualTo(Sz("not in the table")));
            Assert.That(w.Get(Gpu, @"C:\Other\Game.exe"), Is.EqualTo(Sz("GpuPreference=1;")));
            // A value of another type is deleted first.
            List<string> changes = w.Changes.ToList();
            int delete = changes.IndexOf("DeleteValue " + NeoEE + " @\"Game Bit Depth\"");
            Assert.That(delete, Is.GreaterThanOrEqualTo(0));
            Assert.That(changes.FindIndex(c => c.StartsWith("SetValue " + NeoEE + " @\"Game Bit Depth\"", StringComparison.Ordinal)),
                Is.GreaterThan(delete));
            // The markers come last.
            Assert.That(changes.Skip(changes.Count - 2), Is.All.StartsWith("SetValue " + NeoMarker));
        }

        [Test]
        public void Reset_BackupHasEveryValueAndADeleteLineForEveryValueItCreates()
        {
            Installation installation = SeedForReset();

            GameSettingsResult result = w.CreateDefaultsService().Reset(installation);

            Assert.That(result.BackupFiles.Select(file => WinPath.GetFileName(file)),
                Is.EqualTo(new[] { "2026-10-02_200431_NeoEE_EE.reg", "2026-10-02_200431_NeoEE_AoC.reg" }));
            Assert.That(result.BackupFolder, Is.EqualTo(GameSettingsWorld.BackupsFolder + @"\2026-10-02_200431_reset-game-settings"));
            string ee = Encoding.Unicode.GetString(w.FileSystem.GetContent(result.BackupFiles[0]));
            Assert.That(ee, Does.Contain("\r\n\"Game Bit Depth\"=\"16\"\r\n"));
            Assert.That(ee, Does.Contain("\r\n\"Player Name\"=\"not in the table\"\r\n"));
            Assert.That(ee, Does.Contain("\r\n\"Sound Volume\"=-\r\n"));
            Assert.That(ee, Does.Contain("[HKEY_CURRENT_USER\\Software\\Neo\\Empire Earth\\Game Options]\r\n\"Last Map\"=\"not in the table\"\r\n\"Map Type\"=-\r\n"));
            Assert.That(ee, Does.Contain("[HKEY_CURRENT_USER\\Software\\Microsoft\\DirectX\\UserGpuPreferences]\r\n\"" +
                                         NeoEeFolder.Replace(@"\", @"\\") + "\\\\Empire Earth.exe\"=-\r\n"));
            Assert.That(ee, Does.Contain("[HKEY_CURRENT_USER\\Software\\Empire Earth Community\\GameDefaults\\NeoEE]\r\n\"EE\"=-\r\n"));
            Assert.That(ee, Does.Not.Contain("Other\\\\Game.exe"), "only the values the reset changes");
        }

        /// <summary>Importing the backups (double-click) restores the previous values exactly (ADR 0007 amendment).</summary>
        [Test]
        public void Reset_ImportingTheBackupsRestoresThePreviousValues()
        {
            Installation installation = SeedForReset();
            RegistryLocation[] keys = { NeoEE, NeoAoC, Gpu, NeoMarker };
            IReadOnlyList<string> before = w.ValuesBelow(keys);

            GameSettingsResult result = w.CreateDefaultsService().Reset(installation);
            Assert.That(w.ValuesBelow(keys), Is.Not.EqualTo(before));
            foreach (string file in result.BackupFiles)
                RegFileImporter.Import(w.FileSystem.GetContent(file), w.RawRegistry);

            Assert.That(w.ValuesBelow(keys), Is.EqualTo(before));
        }

        [TestCase("Write")]
        [TestCase("CreateDirectory")]
        public void Reset_BackupFails_NothingChanged(string operation)
        {
            Installation installation = SeedForReset();
            IReadOnlyList<string> before = w.ValuesBelow(NeoEE, NeoAoC, Gpu, NeoMarker);
            w.FileSystem.FailOn(GameSettingsWorld.BackupsFolder, (FileSystemOperation)Enum.Parse(typeof(FileSystemOperation), operation),
                FileSystemStatus.AccessDenied);

            GameSettingsResult result = w.CreateDefaultsService().Reset(installation);

            Assert.That(result.Outcome, Is.EqualTo(GameSettingsOutcome.BackupFailed));
            Assert.That(w.Changes, Is.Empty);
            Assert.That(w.ValuesBelow(NeoEE, NeoAoC, Gpu, NeoMarker), Is.EqualTo(before));
            Assert.That(w.Logger.MessagesOf(LogLevel.Error), Has.Some.Contains("nothing was changed"));
        }

        [Test]
        public void Reset_GameSettingsUnreadable_NothingChanged()
        {
            Installation installation = SeedForReset();
            w.RawRegistry.SetFault(NeoAoC.Child("Game Options"), RegistryStatus.AccessDenied);

            GameSettingsResult result = w.CreateDefaultsService().Reset(installation);

            Assert.That(result.Outcome, Is.EqualTo(GameSettingsOutcome.BackupFailed));
            Assert.That(w.Changes, Is.Empty);
        }

        // --- GPU preference (contract 3.4) ---------------------------------------------------------------------------

        [Test]
        public void GpuPreference_OnlyFromWindows10()
        {
            w = new GameSettingsWorld(FakeSystemInfo.Windows81());
            w.AddAdminInstallationOfAnotherAccount(NeoRoot, Product.NeoEE);

            Start();
            w.CreateDefaultsService().Reset(Installation());

            Assert.That(w.RawRegistry.ProbeKey(Gpu).Status, Is.EqualTo(RegistryStatus.Missing));
        }

        [Test]
        public void GpuPreference_OnlyWithTheTaskCompatibilityWindows()
        {
            w.AddAdminInstallationOfAnotherAccount(NeoRoot, Product.NeoEE, tasks: "compatibility");

            Start();

            Assert.That(w.RawRegistry.ProbeKey(Gpu).Status, Is.EqualTo(RegistryStatus.Missing));
        }

        [Test]
        public void GpuPreference_OnlyForTheProgramsOfTheInstallationsFound()
        {
            w.AddAdminInstallationOfAnotherAccount(NeoRoot, Product.NeoEE);
            w.World.AddLegacyInstallation(GameSettingsWorld.EERoot, Product.EE);

            Start();

            Assert.That(w.RawRegistry.GetValueNames(Gpu).Value, Is.EquivalentTo(new[]
            {
                NeoEeFolder + @"\Empire Earth.exe", NeoAocFolder + @"\EE-AOC.exe"
            }), "the legacy EE installation has no compatibility_windows task");
        }

        // --- Mutation guard for every writing action (ADR 0016) ---------------------------------------------------------

        public enum Action
        {
            Start,
            AnswerYes,
            AnswerNo,
            Sync,
            Display,
            GameWindow,
            Reset,
            DefaultsIfNeeded
        }

        [Test]
        public void EveryWritingAction_IsBlockedBySetupAndGame(
            [Values] Action action,
            [Values("NeoEE_Setup", "MadDocSoftwarePresentsEmpireEarthExpansion")] string mutex)
        {
            DefaultsStartup startup = StartWithDifferingDisplayValues();
            Installation installation = Installation();
            int changes = w.Changes.Count;
            w.Mutexes.With(mutex);
            GameDefaultsService service = w.CreateDefaultsService();
            MutationBlock expected = mutex.EndsWith("_Setup", StringComparison.Ordinal) ? MutationBlock.SetupRunning : MutationBlock.GameRunning;

            MutationCheck block;
            switch (action)
            {
                case Action.Start:
                    block = service.ApplyAtLauncherStart(w.Discover()).Block;
                    break;
                case Action.AnswerYes:
                    block = service.AnswerDisplayQuestion(startup.Question, true).Block;
                    break;
                case Action.AnswerNo:
                    block = service.AnswerDisplayQuestion(startup.Question, false).Block;
                    break;
                case Action.Sync:
                    block = service.SynchronizeInstalledFrom(installation, Game.EmpireEarth).Block;
                    break;
                case Action.Display:
                    block = service.ApplyRecommendedDisplay(installation).Block;
                    break;
                case Action.GameWindow:
                    block = service.SetGameWindow(installation, new ScreenSize(1600, 900)).Block;
                    break;
                case Action.Reset:
                    block = service.Reset(installation).Block;
                    break;
                default:
                    Assert.That(service.ApplyDefaultsIfNeeded(installation, Game.EmpireEarth, out _), Is.EqualTo(DefaultsAtStart.Blocked));
                    block = w.Guard.Check("probe");
                    break;
            }

            Assert.That(block.Block, Is.EqualTo(expected));
            Assert.That(w.Changes, Has.Count.EqualTo(changes), "nothing changed");
            Assert.That(w.FileSystem.DirectoryExists(GameSettingsWorld.BackupsFolder), Is.False, "no backup either");
        }
    }
}

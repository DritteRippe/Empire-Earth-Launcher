using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.GameSettings;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Core.Play;
using Empire_Earth_Launcher.Core.Repair;
using Empire_Earth_Launcher.Core.Settings;
using Empire_Earth_Launcher.Tests.Core.Settings;
using Empire_Earth_Launcher.Tests.Fakes;
using Empire_Earth_Launcher.Tests.TestSupport;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Launcher
{
    /// <summary>
    /// <see cref="PlayModel"/>, the state of the Play page (L-WP6): the game choice in settings.json, AoC only with an AoC
    /// folder, the versions, Play blocked while a setup runs, the start and its display question (the download page of the
    /// repair advice is opened by <see cref="UpdateModel"/>, <c>UpdateModelTests</c>).
    /// With the fake registry, file system, mutexes and shell.
    /// </summary>
    [TestFixture]
    public class PlayModelTests
    {
        private const string SettingsFile = @"C:\Users\Player\AppData\Local\Empire Earth Launcher\settings.json";
        private const string EeFolder = GameSettingsWorld.NeoRoot + @"\Empire Earth";
        private const string AocFolder = GameSettingsWorld.NeoRoot + @"\Empire Earth - The Art of Conquest";
        private const int LauncherPid = 100;

        private GameSettingsWorld w;
        private CountingFileSystem counting;
        private SettingsStore settings;
        private SetupWatcher watcher;
        private InstallationService installations;
        private GameSettingsModel gameSettings;
        private FakeProcessStarter shell;
        private FakeWindowSystem windows;
        private Func<TimeSpan, CancellationToken, Task> handOverDelay;
        private PlayModel model;
        private int changed;

        private void Create(Action<GameSettingsWorld> addInstallations)
        {
            w = new GameSettingsWorld();
            addInstallations(w);
            counting = new CountingFileSystem(w.FileSystem);
            settings = new SettingsStore(counting, SettingsFile, w.Logger);
            settings.Load();
            watcher = new SetupWatcher(w.Mutexes, w.World.Clock, w.Logger);
            installations = new InstallationService(w.Logger, settings, w.World.CreateDiscovery(), w.FileSystem, null, watcher);
            gameSettings = new GameSettingsModel(w.CreateDefaultsService(), new ConsistencyChecker(w.Registry, w.FileSystem, w.SystemInfo),
                new CompatibilityOptions(w.Registry, w.SystemInfo, w.Guard, w.Backups, w.Logger), settings, w.SystemInfo,
                GameSettingsWorld.BackupsFolder, w.Logger);
            shell = new FakeProcessStarter();
            windows = new FakeWindowSystem(shell.ProcessId.Value) { Foreground = LauncherPid };
            var starter = new GameStarter(new RunningGameDetector(w.Mutexes, new FakeProcessList()), w.FileSystem,
                w.CreateDefaultsService(), shell, w.Logger, null, windows);
            var activator = new GameWindowActivator(windows, w.World.Clock, w.Logger, LauncherPid,
                (time, token) => handOverDelay(time, token));
            var versions = new ProgramVersions(w.FileSystem, new FakeFileVersionReader().With(EeFolder + @"\Empire Earth.exe", "2.0.0.2949"));
            model = new PlayModel(starter, versions, watcher, installations, settings, gameSettings, w.Logger, activator);
            model.Changed += (sender, e) => changed++;
        }

        [SetUp]
        public void SetUp()
        {
            // The hand-over of the foreground does not wait for real: the fake clock moves instead.
            handOverDelay = (time, token) =>
            {
                w.World.Clock.Advance(time);
                return Task.CompletedTask;
            };
            Create(world => world.AddAdminInstallationOfAnotherAccount(GameSettingsWorld.NeoRoot, Product.NeoEE));
        }

        [Test]
        public void BeforeTheFirstSearch_NothingCanBePlayed()
        {
            Assert.That(model.IsSearching, Is.True);
            Assert.That(model.Selected, Is.Null);
            Assert.That(model.CanPlay, Is.False);
            Assert.That(model.SelectedGame, Is.SameAs(Game.EmpireEarth));
            Assert.That(model.Versions, Is.Empty);
        }

        [Test]
        public async Task AfterTheSearch_PlayIsPossible_AndTheVersionsAreShown()
        {
            await installations.RefreshAsync();
            await model.RefreshVersionsAsync();

            Assert.That(model.CanPlay, Is.True);
            Assert.That(model.IsAvailable(PlayEntry.NeoEmpireEarthArtOfConquest), Is.True);
            Assert.That(model.Versions.Select(v => v.ToString()),
                Is.EqualTo(new[] { "Empire Earth.exe 2.0.0.2949", "EE-AOC.exe without version" }));
            Assert.That(w.Logger.Messages, Has.Some.Contains("Program versions of " + GameSettingsWorld.NeoRoot));
            Assert.That(changed, Is.GreaterThanOrEqualTo(3), "search started, search ended, versions");
        }

        [Test]
        public async Task TheGameChoice_IsSavedAsTheLastGame()
        {
            await installations.RefreshAsync();

            model.SelectEntry(PlayEntry.NeoEmpireEarthArtOfConquest);

            Assert.That(model.SelectedGame, Is.SameAs(Game.ArtOfConquest));
            Assert.That(w.FileSystem.GetText(SettingsFile), Does.Contain("\"LastGame\": \"AoC\""));
            Assert.That(new SettingsStore(w.FileSystem, SettingsFile, w.Logger).LoadAndGet().LastGame, Is.EqualTo("AoC"));
        }

        [Test]
        public async Task ArtOfConquest_OnlyWhenTheInstallationHasAnAocFolder()
        {
            Create(world => world.AddAdminInstallationOfAnotherAccount(GameSettingsWorld.NeoRoot, Product.NeoEE, "game"));
            settings.Current.LastGame = "AoC";
            await installations.RefreshAsync();

            Assert.That(model.IsAvailable(PlayEntry.NeoEmpireEarthArtOfConquest), Is.False);
            Assert.That(model.SelectedGame, Is.SameAs(Game.EmpireEarth), "the last game AoC counts only with an AoC folder");
            Assert.That(() => model.SelectEntry(PlayEntry.NeoEmpireEarthArtOfConquest), Throws.InvalidOperationException);
        }

        // --- The four games (launcher 1.1.0, contract 1.4 revision 6) -------------------------------------------------------

        private void CreateWithBothProducts(string eeComponents = "game,gameaoc")
        {
            Create(world =>
            {
                world.AddAdminInstallationOfAnotherAccount(GameSettingsWorld.NeoRoot, Product.NeoEE);
                world.AddAdminInstallationOfAnotherAccount(GameSettingsWorld.EERoot, Product.EE, eeComponents);
            });
        }

        private string SavedSettings()
        {
            return w.FileSystem.GetText(SettingsFile);
        }

        [Test]
        public void TheFourGames_AreListedInTheOrderOfThePage()
        {
            Assert.That(model.Entries, Is.SameAs(PlayEntry.All));
            Assert.That(model.Entries.Select(entry => entry.EnglishName), Is.EqualTo(new[]
            {
                "Empire Earth", "Empire Earth - The Art of Conquest", "Neo Empire Earth", "Neo Empire Earth - The Art of Conquest"
            }));
        }

        [Test]
        public async Task EntriesAreDisabled_UntilTheFirstSearchHasAResult_AndForEveryGameThatIsNotInstalled()
        {
            CreateWithBothProducts(eeComponents: "game");
            Assert.That(model.Entries.Select(model.IsAvailable), Is.EqualTo(new[] { false, false, false, false }), "searching");

            await installations.RefreshAsync();

            Assert.That(model.Entries.Select(model.IsAvailable), Is.EqualTo(new[] { true, false, true, true }), "EE has no Art of Conquest");
        }

        [Test]
        public async Task OnlyNeoEE_TheEntriesOfEEAreDisabled()
        {
            await installations.RefreshAsync();

            Assert.That(model.Entries.Select(model.IsAvailable), Is.EqualTo(new[] { false, false, true, true }));
        }

        [Test]
        public async Task WhileAStartRuns_NoEntryCanBeChosen()
        {
            CreateWithBothProducts();
            await installations.RefreshAsync();
            var duringTheStart = new System.Collections.Generic.List<bool[]>();
            model.Changed += (sender, e) => duringTheStart.Add(model.Entries.Select(model.IsAvailable).ToArray());

            await model.StartAsync(false);

            Assert.That(duringTheStart.First(), Is.EqualTo(new[] { false, false, false, false }), "the first change is the start itself");
            Assert.That(model.Entries.Select(model.IsAvailable), Is.EqualTo(new[] { true, true, true, true }), "free again");
        }

        [Test]
        public async Task TheSelectedEntry_IsNeoEmpireEarth_ForTwoCommunityInstallationsWithoutAChoice()
        {
            CreateWithBothProducts();
            await installations.RefreshAsync();

            Assert.That(model.SelectedEntry, Is.SameAs(PlayEntry.NeoEmpireEarth), "contract 1.4: NeoEE comes first; the game is Empire Earth");
        }

        [Test]
        public async Task TheSelectedEntry_IsTheRememberedOne()
        {
            CreateWithBothProducts();
            settings.Current.LastProduct = "EE";
            settings.Current.LastGame = "AoC";

            Assert.That(model.SelectedEntry, Is.SameAs(PlayEntry.EmpireEarthArtOfConquest), "while the first search runs");
            await installations.RefreshAsync();

            Assert.That(model.SelectedEntry, Is.SameAs(PlayEntry.EmpireEarthArtOfConquest));
        }

        [Test]
        public async Task TheArtOfConquestOfAnInstallationWithoutIt_FallsBackToEmpireEarth()
        {
            CreateWithBothProducts(eeComponents: "game");
            settings.Current.LastProduct = "EE";
            settings.Current.LastGame = "AoC";

            await installations.RefreshAsync();

            Assert.That(model.SelectedEntry, Is.SameAs(PlayEntry.EmpireEarth));
            Assert.That(model.SelectedGame, Is.SameAs(Game.EmpireEarth));
        }

        [Test]
        public async Task SelectEntry_SwitchesTheProductAndTheGame_WithOneSave_AndEveryPageFollows()
        {
            CreateWithBothProducts();
            await installations.RefreshAsync();
            int before = counting.Saves(SettingsFile);
            changed = 0;

            model.SelectEntry(PlayEntry.EmpireEarthArtOfConquest);

            Assert.That(counting.Saves(SettingsFile) - before, Is.EqualTo(1));
            Assert.That(model.SelectedEntry, Is.SameAs(PlayEntry.EmpireEarthArtOfConquest));
            Assert.That(installations.Selected.Product, Is.SameAs(Product.EE), "the selected installation of every page");
            Assert.That(installations.Selected.Root, Is.EqualTo(GameSettingsWorld.EERoot));
            Assert.That(SavedSettings(), Does.Contain("\"LastProduct\": \"EE\"").And.Contain("\"LastGame\": \"AoC\""));
            Assert.That(changed, Is.GreaterThan(0));
            Assert.That(w.Logger.Messages, Has.Some.Contains("Play: the player chose Empire Earth - The Art of Conquest (EE, AoC)."));
        }

        [Test]
        public async Task SelectEntry_OfTheSameProduct_ChangesOnlyTheGame()
        {
            CreateWithBothProducts();
            await installations.RefreshAsync();
            model.SelectEntry(PlayEntry.NeoEmpireEarth);
            DiscoveryResult result = installations.Result;

            model.SelectEntry(PlayEntry.NeoEmpireEarthArtOfConquest);

            Assert.That(installations.Result, Is.SameAs(result), "the installation stays");
            Assert.That(model.SelectedEntry, Is.SameAs(PlayEntry.NeoEmpireEarthArtOfConquest));
            Assert.That(SavedSettings(), Does.Contain("\"LastProduct\": \"NeoEE\"").And.Contain("\"LastGame\": \"AoC\""));
        }

        [Test]
        public async Task SelectEntry_OfTheEntryThatIsChosenAndSaved_SavesNothing()
        {
            CreateWithBothProducts();
            await installations.RefreshAsync();
            model.SelectEntry(PlayEntry.EmpireEarth);
            int saves = counting.Saves(SettingsFile);

            model.SelectEntry(PlayEntry.EmpireEarth);

            Assert.That(counting.Saves(SettingsFile), Is.EqualTo(saves));
        }

        [Test]
        public async Task SelectEntry_RefusesAGameThatIsNotAvailable()
        {
            await installations.RefreshAsync();

            Assert.That(() => model.SelectEntry(PlayEntry.EmpireEarth), Throws.InvalidOperationException);
            Assert.That(() => model.SelectEntry(null), Throws.ArgumentNullException);
            Assert.That(installations.Selected.Product, Is.SameAs(Product.NeoEE));
            Assert.That(counting.Written, Is.Empty);
        }

        [Test]
        public async Task SelectEntry_RefusesEveryGameWhileAStartRuns()
        {
            CreateWithBothProducts();
            await installations.RefreshAsync();
            InvalidOperationException refused = null;
            model.Changed += (sender, e) =>
            {
                if (model.IsStarting && refused == null)
                {
                    try
                    {
                        model.SelectEntry(PlayEntry.EmpireEarth);
                    }
                    catch (InvalidOperationException ex)
                    {
                        refused = ex;
                    }
                }
            };

            await model.StartAsync(false);

            Assert.That(refused, Is.Not.Null);
        }

        [Test]
        public async Task Start_StartsTheGameOfTheChosenEntry_InTheFolderOfItsProduct()
        {
            CreateWithBothProducts();
            await installations.RefreshAsync();
            model.SelectEntry(PlayEntry.EmpireEarthArtOfConquest);

            StartResult result = await model.StartAsync(false);

            string folder = GameSettingsWorld.EERoot + @"\Empire Earth - The Art of Conquest";
            Assert.That(result.Outcome, Is.EqualTo(StartOutcome.Started));
            Assert.That(shell.Started, Is.EqualTo(new[] { Tuple.Create(folder + @"\EE-AOC.exe", folder) }));
        }

        [Test]
        public async Task Contract_4_2_WhileASetupRuns_PlayIsBlocked()
        {
            await installations.RefreshAsync();
            int before = changed;
            w.Mutexes.With("NeoEE_Setup");
            w.World.Clock.Advance(SetupWatcher.Interval);

            watcher.Tick();

            Assert.That(model.RunningSetup, Is.SameAs(SetupKind.NeoEE));
            Assert.That(model.CanPlay, Is.False);
            Assert.That(changed, Is.GreaterThan(before), "the page shows the setup at once");

            w.Mutexes.Remove("NeoEE_Setup");
            w.World.Clock.Advance(SetupWatcher.Interval);
            watcher.Tick();
            await installations.RefreshAfterSetup;

            Assert.That(model.RunningSetup, Is.Null);
            Assert.That(model.CanPlay, Is.True);
        }

        [Test]
        public async Task Start_StartsTheChosenGame_InItsRealFolder()
        {
            await installations.RefreshAsync();
            model.SelectEntry(PlayEntry.NeoEmpireEarthArtOfConquest);

            StartResult result = await model.StartAsync(false);

            Assert.That(result.Outcome, Is.EqualTo(StartOutcome.Started));
            Assert.That(model.LastResult, Is.SameAs(result));
            Assert.That(model.IsStarting, Is.False);
            Assert.That(shell.Started, Is.EqualTo(new[] { Tuple.Create(AocFolder + @"\EE-AOC.exe", AocFolder) }));
        }

        [Test]
        public async Task AfterTheStart_TheForegroundGoesToTheWindowOfTheGame()
        {
            await installations.RefreshAsync();

            StartResult result = await model.StartAsync(false);

            Assert.That(result.IsStarted, Is.True);
            Assert.That((await model.WindowHandOver).HandOver, Is.EqualTo(ActivationOutcome.GameInForeground));
            Assert.That(windows.AllowCalls, Is.EqualTo(new[] { ForegroundRight.AnyProcess }), "allowed right before the start");
            Assert.That(windows.SetForegroundCalls, Is.EqualTo(new[] { windows.Window }));
            Assert.That(w.Logger.Messages, Has.Some.Contains("Game window 0x1234 of Empire Earth.exe (pid 4242) brought to the foreground after"));
        }

        [Test]
        public async Task AStartThatDoesNotHappen_HandsNothingOver()
        {
            await installations.RefreshAsync();
            w.FileSystem.DeleteFile(EeFolder + @"\Empire Earth.exe");

            StartResult result = await model.StartAsync(false);

            Assert.That(result.Outcome, Is.EqualTo(StartOutcome.Damaged));
            Assert.That(model.WindowHandOver, Is.Null);
            Assert.That(windows.FindCalls, Is.EqualTo(0));
            Assert.That(windows.AllowCalls, Is.Empty);
        }

        [Test]
        public async Task AStartWithoutAProcessId_PollsNothing()
        {
            await installations.RefreshAsync();
            shell.ProcessId = null;

            await model.StartAsync(false);

            Assert.That((await model.WindowHandOver).HandOver, Is.EqualTo(ActivationOutcome.ProcessIdUnknown));
            Assert.That(windows.FindCalls, Is.EqualTo(0));
        }

        [Test]
        public async Task ClosingTheLauncher_EndsTheHandOverOfTheForeground()
        {
            await installations.RefreshAsync();
            handOverDelay = Task.Delay;
            windows.LooksWithoutWindow = int.MaxValue;
            await model.StartAsync(false);

            model.CancelWindowHandOver();

            Assert.That((await model.WindowHandOver).HandOver, Is.EqualTo(ActivationOutcome.Cancelled));
            Assert.That(windows.SetForegroundCalls, Is.Empty);
        }

        [Test]
        public async Task TheDisplayQuestionOfAFirstPlay_GoesToTheInfoBar()
        {
            // Two installations share the settings key, so the launcher start set up nothing (ADR 0015); Play does.
            Create(world =>
            {
                world.World.AddLegacyInstallation(GameSettingsWorld.EERoot, Product.EE, artOfConquest: false);
                world.World.AddForeignInstallation(@"C:\Games\EE");
                world.RawRegistry.Seed(GameSettingsWorld.Settings(Product.EE, Game.EmpireEarth), "Game Bit Depth",
                    RegistryValue.FromDWord(16));
            });
            await installations.RefreshAsync();
            await gameSettings.ApplyAfterDiscoveryAsync(installations.Result);
            Assert.That(gameSettings.Question, Is.Null);

            StartResult result = await model.StartAsync(false);

            Assert.That(result.IsStarted, Is.True, "the question never blocks the start (contract 3.6)");
            Assert.That(result.Defaults, Is.EqualTo(DefaultsAtStart.FirstRunWithQuestion));
            Assert.That(gameSettings.Question, Is.Not.Null);
            Assert.That(gameSettings.Question.Items.Single().Differences.Single().Setting.ValueName, Is.EqualTo("Game Bit Depth"));
        }

        [Test]
        public async Task ADamagedInstallation_ReadsTheVersionsAgain()
        {
            await installations.RefreshAsync();
            await model.RefreshVersionsAsync();
            w.FileSystem.DeleteFile(EeFolder + @"\Empire Earth.exe");

            StartResult result = await model.StartAsync(false);

            Assert.That(result.Outcome, Is.EqualTo(StartOutcome.Damaged));
            Assert.That(model.Versions.First().ToString(), Is.EqualTo("Empire Earth.exe missing"));
        }

        [Test]
        public void StartWithoutAnInstallation_IsAProgrammingError()
        {
            Assert.ThrowsAsync<InvalidOperationException>(() => model.StartAsync(false));
        }
    }
}

using System;
using System.Linq;
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

        private GameSettingsWorld w;
        private SettingsStore settings;
        private SetupWatcher watcher;
        private InstallationService installations;
        private GameSettingsModel gameSettings;
        private FakeProcessStarter shell;
        private PlayModel model;
        private int changed;

        private void Create(Action<GameSettingsWorld> addInstallations)
        {
            w = new GameSettingsWorld();
            addInstallations(w);
            settings = new SettingsStore(w.FileSystem, SettingsFile, w.Logger);
            settings.Load();
            watcher = new SetupWatcher(w.Mutexes, w.World.Clock, w.Logger);
            installations = new InstallationService(w.Logger, settings, w.World.CreateDiscovery(), w.FileSystem, null, watcher);
            gameSettings = new GameSettingsModel(w.CreateDefaultsService(), new ConsistencyChecker(w.Registry, w.FileSystem, w.SystemInfo),
                new CompatibilityOptions(w.Registry, w.SystemInfo, w.Guard, w.Backups, w.Logger), settings, w.SystemInfo,
                GameSettingsWorld.BackupsFolder, w.Logger);
            shell = new FakeProcessStarter();
            var starter = new GameStarter(new RunningGameDetector(w.Mutexes, new FakeProcessList()), w.FileSystem,
                w.CreateDefaultsService(), shell, w.Logger);
            var versions = new ProgramVersions(w.FileSystem, new FakeFileVersionReader().With(EeFolder + @"\Empire Earth.exe", "2.0.0.2949"));
            model = new PlayModel(starter, versions, watcher, installations, settings, gameSettings, w.Logger);
            model.Changed += (sender, e) => changed++;
        }

        [SetUp]
        public void SetUp()
        {
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
            Assert.That(model.CanChooseArtOfConquest, Is.True);
            Assert.That(model.Versions.Select(v => v.ToString()),
                Is.EqualTo(new[] { "Empire Earth.exe 2.0.0.2949", "EE-AOC.exe without version" }));
            Assert.That(w.Logger.Messages, Has.Some.Contains("Program versions of " + GameSettingsWorld.NeoRoot));
            Assert.That(changed, Is.GreaterThanOrEqualTo(3), "search started, search ended, versions");
        }

        [Test]
        public async Task TheGameChoice_IsSavedAsTheLastGame()
        {
            await installations.RefreshAsync();

            model.SelectGame(Game.ArtOfConquest);

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

            Assert.That(model.CanChooseArtOfConquest, Is.False);
            Assert.That(model.SelectedGame, Is.SameAs(Game.EmpireEarth), "the last game AoC counts only with an AoC folder");
            Assert.That(() => model.SelectGame(Game.ArtOfConquest), Throws.InvalidOperationException);
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
            model.SelectGame(Game.ArtOfConquest);

            StartResult result = await model.StartAsync(false);

            Assert.That(result.Outcome, Is.EqualTo(StartOutcome.Started));
            Assert.That(model.LastResult, Is.SameAs(result));
            Assert.That(model.IsStarting, Is.False);
            Assert.That(shell.Started, Is.EqualTo(new[] { Tuple.Create(AocFolder + @"\EE-AOC.exe", AocFolder) }));
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

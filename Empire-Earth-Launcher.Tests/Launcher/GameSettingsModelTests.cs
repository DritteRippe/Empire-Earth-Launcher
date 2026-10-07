using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.GameSettings;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Core.Settings;
using Empire_Earth_Launcher.Tests.Fakes;
using Empire_Earth_Launcher.Tests.TestSupport;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Launcher
{
    /// <summary>
    /// <see cref="GameSettingsModel"/>: the state of the Game settings page and of the info bar of the Play page after a
    /// discovery, and the actions of the page (L-WP5).
    /// </summary>
    [TestFixture]
    public class GameSettingsModelTests
    {
        private const string SettingsFile = @"C:\Users\Player\AppData\Local\Empire Earth Launcher\settings.json";
        private static readonly RegistryLocation NeoEE = GameSettingsWorld.Settings(Product.NeoEE, Game.EmpireEarth);

        private GameSettingsWorld w;
        private SettingsStore settings;
        private GameSettingsModel model;
        private int changed;

        [SetUp]
        public void SetUp()
        {
            w = new GameSettingsWorld();
            w.AddAdminInstallationOfAnotherAccount(GameSettingsWorld.NeoRoot, Product.NeoEE, "game");
            settings = new SettingsStore(w.FileSystem, SettingsFile, w.Logger);
            model = new GameSettingsModel(w.CreateDefaultsService(), new ConsistencyChecker(w.Registry, w.FileSystem, w.SystemInfo),
                new CompatibilityOptions(w.Registry, w.SystemInfo, w.Guard, w.Backups, w.Logger), settings, w.SystemInfo,
                GameSettingsWorld.BackupsFolder, w.Logger);
            model.Changed += (sender, e) => changed++;
        }

        [Test]
        public async Task AfterTheDiscovery_TheDefaultsAreAppliedAndShown()
        {
            await model.ApplyAfterDiscoveryAsync(w.Discover());

            Assert.That(model.Selected, Is.Not.Null);
            Assert.That(model.Lines.Select(line => line.Game.Id + ":" + line.Status), Is.EqualTo(new[] { "EE:Applied" }));
            Assert.That(model.Question, Is.Null);
            Assert.That(model.Findings, Is.Empty);
            Assert.That(model.Compatibility.SwitchesOffered, Is.True);
            Assert.That(changed, Is.EqualTo(1));
            Assert.That(w.Logger.Messages, Has.Some.Contains("Game defaults at the start: "));
        }

        [Test]
        public async Task TheQuestion_StaysUntilItIsAnswered()
        {
            w.RawRegistry.Seed(NeoEE, "Game Bit Depth", RegistryValue.FromDWord(16));
            await model.ApplyAfterDiscoveryAsync(w.Discover());
            Assert.That(model.Question, Is.Not.Null);
            Assert.That(model.Lines.Single().Status, Is.EqualTo(DefaultsStatus.Pending));

            await model.AnswerQuestionAsync(true);

            Assert.That(model.Question, Is.Null);
            Assert.That(model.LastResult.Outcome, Is.EqualTo(GameSettingsOutcome.Done));
            Assert.That(model.LastResult.BackupFolder, Does.StartWith(GameSettingsWorld.BackupsFolder));
            Assert.That(model.Lines.Single().Status, Is.EqualTo(DefaultsStatus.Applied));
        }

        [Test]
        public async Task Reset_AnswersAPendingQuestionOfTheSameInstallation()
        {
            w.RawRegistry.Seed(NeoEE, "Game Bit Depth", RegistryValue.FromDWord(16));
            await model.ApplyAfterDiscoveryAsync(w.Discover());

            await model.ResetAsync();

            Assert.That(model.LastResult.Outcome, Is.EqualTo(GameSettingsOutcome.Done));
            Assert.That(model.Question, Is.Null);
            Assert.That(w.Get(NeoEE, "Game Bit Depth"), Is.EqualTo(RegistryValue.FromDWord(32)));
        }

        [Test]
        public async Task Blocked_TheStartWritesNothingAndSaysWhy()
        {
            w.Mutexes.With("NeoEE_Setup");

            await model.ApplyAfterDiscoveryAsync(w.Discover());

            Assert.That(model.StartBlock.Block, Is.EqualTo(Empire_Earth_Launcher.Core.Play.MutationBlock.SetupRunning));
            Assert.That(model.Lines.Single().Status, Is.EqualTo(DefaultsStatus.Pending));
            Assert.That(w.Changes, Is.Empty);
        }

        [Test]
        public async Task HidingAHint_SavesSettingsJson_AndRemovesItFromThePlayPage()
        {
            w.RawRegistry.Seed(NeoEE, "Rasterizer Name", RegistryValue.FromString("Direct3D"));
            await model.ApplyAfterDiscoveryAsync(w.Discover());
            ConsistencyFinding finding = model.Findings.Single();
            Assert.That(model.VisibleFindings, Has.Count.EqualTo(1));

            model.SetHidden(finding, true);

            Assert.That(model.IsHidden(finding), Is.True);
            Assert.That(model.VisibleFindings, Is.Empty);
            Assert.That(model.Findings, Has.Count.EqualTo(1), "the Game settings page lists every hint");
            Assert.That(w.FileSystem.GetText(SettingsFile), Does.Contain("RasterizerMismatch"));
        }

        [Test]
        public async Task CompatibilityOptions_AreSwitchedForTheSelectedInstallation()
        {
            await model.ApplyAfterDiscoveryAsync(w.Discover());

            await model.SetCompatibilityEntryAsync(CompatibilityLayers.HighDpiAware, true);

            Assert.That(model.LastCompatibilityResult.Outcome, Is.EqualTo(GameSettingsOutcome.Done));
            Assert.That(model.Compatibility.Entries.Single(e => e.Name == CompatibilityLayers.HighDpiAware).State, Is.EqualTo(EntryState.On));
        }

        [Test]
        public async Task WithoutAnInstallation_NothingIsShown()
        {
            w = new GameSettingsWorld();
            settings = new SettingsStore(w.FileSystem, SettingsFile, w.Logger);
            model = new GameSettingsModel(w.CreateDefaultsService(), new ConsistencyChecker(w.Registry, w.FileSystem, w.SystemInfo),
                new CompatibilityOptions(w.Registry, w.SystemInfo, w.Guard, w.Backups, w.Logger), settings, w.SystemInfo,
                GameSettingsWorld.BackupsFolder, w.Logger);

            await model.ApplyAfterDiscoveryAsync(w.Discover());
            await model.ResetAsync();

            Assert.That(model.Selected, Is.Null);
            Assert.That(model.Lines, Is.Empty);
            Assert.That(model.Compatibility, Is.Null);
            Assert.That(model.LastResult, Is.Null);
        }

        [Test]
        public void ScalingAndBackupFolder()
        {
            w.SystemInfo.WithScreen(1920, 1080, 125);

            Assert.That(model.ScalingPercent, Is.EqualTo(125));
            Assert.That(model.BackupFolder, Is.EqualTo(GameSettingsWorld.BackupsFolder));
            Assert.That(model.NeedsHighDpiHint(CompatibilityLayers.HighDpiAware, false), Is.True);
        }
    }
}

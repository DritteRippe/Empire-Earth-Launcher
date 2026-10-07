using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Diagnostics;
using Empire_Earth_Launcher.Core.Logging;
using Empire_Earth_Launcher.Core.Mods;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Tests.TestSupport;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Launcher
{
    /// <summary>
    /// <see cref="ModsModel"/>, the state of the Mods page (launcher 1.1.0): the dreXmod presets and <c>dreXmod.config</c> of each
    /// game folder of the selected installation, read where the game reads them, only for dreXmod 3, never while a setup runs;
    /// and the two buttons that open the folder and the config. The model never writes a file (its file system fails the test at
    /// the first change). With the fake file system, mutexes and process starter.
    /// </summary>
    [TestFixture]
    [SetUICulture("en-US")]
    public class ModsModelTests
    {
        private const string EeFolder = ModsModelWorld.EeFolder;
        private const string AocFolder = ModsModelWorld.AocFolder;

        private ModsModelWorld m;
        private ModsModel model;
        private int changed;

        [SetUp]
        public void SetUp()
        {
            m = new ModsModelWorld();
            model = m.Model;
            model.Changed += (sender, e) => changed++;
        }

        [Test]
        public void BeforeTheSearch_NothingIsRead_AndThePageDoesNotExist()
        {
            Assert.That(model.Snapshot, Is.Null);
            Assert.That(model.IsAvailable, Is.False);
            Assert.That(model.HasResult, Is.False);
            Assert.That(model.CanOpen, Is.False);
        }

        [Test]
        public async Task WithoutAnInstallation_ThereIsNoSnapshot_AndNoPage()
        {
            await m.Installations.RefreshAsync();

            Assert.That(model.Snapshot, Is.Null);
            Assert.That(model.IsAvailable, Is.False);
            Assert.That(model.HasResult, Is.True);
        }

        [Test]
        public async Task WithDreXmod3_EveryGameFolderIsRead_PresetsAndConfig()
        {
            m.AddInstallation();
            m.AddShippedPresets(ModsModelWorld.Config(mod: "1", modName: "yukon"));

            await m.Search();

            Assert.That(model.IsAvailable, Is.True);
            Assert.That(model.Snapshot.Version, Is.EqualTo(DreXmodVersion.Version3));
            Assert.That(model.Snapshot.Games.Select(line => line.Game), Is.EqualTo(new[] { Game.EmpireEarth, Game.ArtOfConquest }));
            GameModsLine ee = model.Snapshot.Games[0];
            Assert.That(ee.ModsFolder, Is.EqualTo(EeFolder + @"\Data\dxm\mods"));
            Assert.That(ee.Scan.Presets.Select(preset => preset.FolderName), Is.EqualTo(new[] { "dxm", "energycube", "template", "yukon" }));
            Assert.That(ee.Config.Config.Mod.Selects("yukon"), Is.True);
            Assert.That(model.Snapshot.Games[1].ModsFolder, Is.EqualTo(AocFolder + @"\Data\dxm\mods"));
        }

        [Test]
        public async Task WithoutTheArtOfConquest_OnlyTheFirstGameIsRead()
        {
            m.AddInstallation(@"game,additional,additional\drexmod,additional\drexmod\v3");
            m.AddPreset(EeFolder, "yukon", ModsModelWorld.Credits("yukon"));

            await m.Search();

            Assert.That(model.Snapshot.Games.Select(line => line.Game), Is.EqualTo(new[] { Game.EmpireEarth }));
        }

        [Test]
        public async Task WithDreXmod2_ThereIsNoPage_AndNothingOfTheGameFoldersIsRead()
        {
            m.AddInstallation(ModsModelWorld.DreXmod2);
            m.AddShippedPresets();

            await m.Search();

            Assert.That(model.Snapshot.Version, Is.EqualTo(DreXmodVersion.Version2));
            Assert.That(model.Snapshot.Games, Is.Empty);
            Assert.That(model.IsAvailable, Is.False);
        }

        [Test]
        public async Task WithoutDreXmod_ThereIsNoPage()
        {
            m.AddInstallation("game,gameaoc");

            await m.Search();

            Assert.That(model.Snapshot.Version, Is.EqualTo(DreXmodVersion.None));
            Assert.That(model.IsAvailable, Is.False);
        }

        [Test]
        public async Task AMissingFolderAndAMissingConfig_AreStates_NotErrors()
        {
            m.AddInstallation();

            await m.Search();

            GameModsLine ee = model.Snapshot.Games[0];
            Assert.That(model.IsAvailable, Is.True, "dreXmod 3 is installed; its files are what the page reports on");
            Assert.That(ee.Scan.Status, Is.EqualTo(ConfigFileStatus.Missing));
            Assert.That(ee.Config.Status, Is.EqualTo(ConfigFileStatus.Missing));
        }

        [Test]
        public async Task TheConfig_IsReadWhereTheGameReadsIt_TheVirtualStoreCopyFirst()
        {
            m.AddInstallation();
            m.AddShippedPresets();
            m.World.FileSystem.AddFile(ModsModelWorld.VirtualStore + @"\Program Files (x86)\Neo Empire Earth\Empire Earth\dreXmod.config",
                ModsModelWorld.Config(mod: "1", modName: "energycube"));

            await m.Search();

            DreXmodConfigFile config = model.Snapshot.Games[0].Config;
            Assert.That(config.IsVirtualStoreCopy, Is.True);
            Assert.That(config.Config.Mod.Selects("energycube"), Is.True);
            Assert.That(model.Snapshot.Games[1].Config.IsVirtualStoreCopy, Is.False);
        }

        [Test]
        public async Task TheNextRead_SeesWhatThePlayerChanged_TheBadgeMoves()
        {
            m.AddInstallation();
            m.AddShippedPresets();
            await m.Search();
            Assert.That(model.Snapshot.Games[0].Config.Config.Mod.Selects("yukon"), Is.False);

            m.World.FileSystem.AddFile(EeFolder + @"\dreXmod.config", ModsModelWorld.Config(mod: "1", modName: "yukon"));
            m.AddPreset(EeFolder, "mine", null);
            model.Read();
            await model.LastRead;

            Assert.That(model.Snapshot.Games[0].Config.Config.Mod.Selects("yukon"), Is.True);
            Assert.That(model.Snapshot.Games[0].Scan.Presets.Select(preset => preset.FolderName), Does.Contain("mine"));
        }

        [Test]
        public async Task WhileASetupRuns_NothingIsRead_AndNothingCanBeOpened()
        {
            m.AddInstallation();
            m.AddShippedPresets();
            await m.Search();
            ModsSnapshot before = model.Snapshot;
            m.World.FileSystem.AddFile(EeFolder + @"\dreXmod.config", ModsModelWorld.Config(mod: "1", modName: "yukon"));

            m.StartSetup();
            await model.RefreshAsync();

            Assert.That(model.Snapshot, Is.SameAs(before), "contract 4.2: the files are not read while a setup runs");
            Assert.That(model.RunningSetup, Is.EqualTo(SetupKind.NeoEE));
            Assert.That(model.CanOpen, Is.False);
            Assert.That(model.OpenModsFolder(Game.EmpireEarth), Is.Not.Null);
            Assert.That(model.OpenConfig(Game.EmpireEarth), Is.Not.Null);
            Assert.That(m.Shell.OpenedFolders, Is.Empty);
            Assert.That(m.Shell.OpenedFiles, Is.Empty);
        }

        [Test]
        public async Task ASetupThatStartsOrEnds_RaisesChanged_SoThatThePageSaysSo()
        {
            m.AddInstallation();
            await m.Search();
            int before = changed;

            m.StartSetup();

            Assert.That(changed, Is.GreaterThan(before));
        }

        [Test]
        public async Task TheTemplateSwitch_IsOffByDefault_AndRaisesChanged()
        {
            m.AddInstallation();
            await m.Search();
            Assert.That(model.ShowTemplates, Is.False);
            int before = changed;

            model.ShowTemplates = true;
            model.ShowTemplates = true;

            Assert.That(model.ShowTemplates, Is.True);
            Assert.That(changed, Is.EqualTo(before + 1), "only a change is announced");
        }

        // --- The buttons ------------------------------------------------------------------------------------------------

        [Test]
        public async Task OpenModsFolder_OpensTheFolderOfTheGame_InTheExplorer()
        {
            m.AddInstallation();
            m.AddShippedPresets();
            await m.Search();

            string problem = model.OpenModsFolder(Game.ArtOfConquest);

            Assert.That(problem, Is.Null);
            Assert.That(m.Shell.OpenedFolders, Is.EqualTo(new[] { AocFolder + @"\Data\dxm\mods" }));
            Assert.That(m.World.Logger.Messages, Has.Some.Contains("was opened"));
        }

        [Test]
        public async Task OpenModsFolder_OfAFolderThatDoesNotExist_SaysSo_AndOpensNothing()
        {
            m.AddInstallation();
            await m.Search();

            string problem = model.OpenModsFolder(Game.EmpireEarth);

            Assert.That(problem, Does.Contain(EeFolder + @"\Data\dxm\mods").And.Contain("does not exist"));
            Assert.That(m.Shell.OpenedFolders, Is.Empty);
        }

        [Test]
        public async Task OpenConfig_OpensTheFileTheGameReads_InItsProgram()
        {
            m.AddInstallation();
            m.AddShippedPresets();
            string copy = ModsModelWorld.VirtualStore + @"\Program Files (x86)\Neo Empire Earth\Empire Earth\dreXmod.config";
            m.World.FileSystem.AddFile(copy, ModsModelWorld.Config());
            await m.Search();

            Assert.That(model.OpenConfig(Game.EmpireEarth), Is.Null);
            Assert.That(model.OpenConfig(Game.ArtOfConquest), Is.Null);

            Assert.That(m.Shell.OpenedFiles, Is.EqualTo(new[] { copy, AocFolder + @"\dreXmod.config" }));
            Assert.That(m.Shell.Started, Is.Empty, "nothing is run");
        }

        [Test]
        public async Task OpenConfig_WithoutTheFile_SaysSo_AndOpensNothing()
        {
            m.AddInstallation();
            await m.Search();

            string problem = model.OpenConfig(Game.EmpireEarth);

            Assert.That(problem, Does.Contain("dreXmod.config was not found"));
            Assert.That(m.Shell.OpenedFiles, Is.Empty);
        }

        [Test]
        public async Task WhenTheShellCannotOpenIt_TheProblemIsReturned_AndLogged()
        {
            m.AddInstallation();
            m.AddShippedPresets();
            await m.Search();
            m.Shell.OpenException = new Win32Exception(5, "Access is denied");

            string folder = model.OpenModsFolder(Game.EmpireEarth);
            string file = model.OpenConfig(Game.EmpireEarth);

            Assert.That(folder, Does.Contain("Access is denied"));
            Assert.That(file, Does.Contain("Access is denied"));
            Assert.That(m.World.Logger.Entries.Count(entry => entry.Level == LogLevel.Warning), Is.EqualTo(2));
        }

        [Test]
        public async Task AGameThatIsNotOnThePage_HasNothingToOpen()
        {
            m.AddInstallation(@"game,additional,additional\drexmod,additional\drexmod\v3");
            await m.Search();

            Assert.That(model.OpenModsFolder(Game.ArtOfConquest), Is.EqualTo(Empire_Earth_Launcher.Properties.Resources.ModsNothingToOpen));
        }
    }
}

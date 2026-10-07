using System.Linq;
using System.Threading.Tasks;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Diagnostics;
using Empire_Earth_Launcher.Core.GameSettings;
using Empire_Earth_Launcher.Core.Graphics;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Core.Play;
using Empire_Earth_Launcher.Tests.TestSupport;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Launcher
{
    /// <summary>
    /// <see cref="GraphicsModel"/>, the state and the one action of the graphics page (launcher 1.1.0): the window size of each
    /// game, the sizes on offer, the wrapper and its <c>dgVoodoo.conf</c> read where the game reads them, and the change of the
    /// window size, which asks the mutation guard, backs up first and writes nothing else; no file is ever written. With the fake
    /// registry, file system and mutexes.
    /// </summary>
    [TestFixture]
    public class GraphicsModelTests
    {
        private const string Wrapper = GraphicsModelWorld.Wrapper;
        private const string EeFolder = GraphicsModelWorld.EeFolder;
        private const string Root = GraphicsModelWorld.Root;
        private const string VirtualStore = GraphicsModelWorld.VirtualStore;

        private static readonly RegistryLocation NeoEE = GraphicsModelWorld.NeoEE;
        private static readonly RegistryLocation NeoAoC = GraphicsModelWorld.NeoAoC;

        private GraphicsModelWorld g;
        private GameSettingsWorld w;
        private InstallationService installations;
        private GameSettingsModel gameSettings;
        private GraphicsModel model;
        private int changed;

        [SetUp]
        public void SetUp()
        {
            g = new GraphicsModelWorld();
            w = g.World;
            installations = g.Installations;
            gameSettings = g.GameSettings;
            model = g.Model;
            model.Changed += (sender, e) => changed++;
        }

        private async Task Search()
        {
            await installations.RefreshAsync();
            Assert.That(model.LastRead, Is.Not.Null, "every search result starts a read");
            await model.LastRead;
        }

        private void AddInstallation(string components = "game,gameaoc")
        {
            g.AddInstallation(components);
        }

        private void Tick(string setupMutex)
        {
            g.StartSetup(setupMutex);
        }

        // --- The state ------------------------------------------------------------------------------------------------

        [Test]
        public void BeforeTheFirstSearch_NothingIsRead()
        {
            Assert.That(model.Snapshot, Is.Null);
            Assert.That(model.LastRead, Is.Null);
            Assert.That(model.HasResult, Is.False);
            Assert.That(model.CanChange, Is.False);
            Assert.That(model.Selected, Is.Null);
        }

        [Test]
        public async Task AfterTheSearch_TheSnapshotHoldsTheWindowSizeOfEachGame()
        {
            AddInstallation();

            await Search();

            Assert.That(model.HasResult, Is.True);
            Assert.That(model.Snapshot.Installation, Is.SameAs(model.Selected));
            Assert.That(model.Snapshot.Windows.Select(line => line.Game.Id + ":" + line.Size), Is.EqualTo(new[] { "EE:1920x1080", "AoC:1920x1080" }));
            Assert.That(model.Snapshot.Options.Select(option => option.Size.ToString()), Does.Contain("1600x900"));
            Assert.That(model.Snapshot.Screen, Is.EqualTo(new ScreenSize(1920, 1080)));
            Assert.That(model.Snapshot.ScalingPercent, Is.EqualTo(100));
            Assert.That(model.IsReading, Is.False);
            Assert.That(model.CanChange, Is.True);
            Assert.That(changed, Is.GreaterThanOrEqualTo(2), "read started, read ended");
        }

        [Test]
        public async Task AGameWithoutWindowValues_HasAnEmptySize()
        {
            AddInstallation("game");
            w.RawRegistry.DeleteValue(NeoEE, "Game Window Height");

            await Search();

            Assert.That(model.Snapshot.Windows.Single().Size, Is.EqualTo(ScreenSize.Empty));
            Assert.That(model.Snapshot.Changes(new ScreenSize(1600, 900)), Is.True);
        }

        [Test]
        public async Task WithoutAnInstallation_ThereIsNoSnapshot()
        {
            await installations.RefreshAsync();

            Assert.That(model.HasResult, Is.True);
            Assert.That(model.Snapshot, Is.Null);
            Assert.That(model.CanChange, Is.False);
        }

        [Test]
        public async Task TheScaling_IsTheOneOfTheScreen()
        {
            w.SystemInfo.WithScreen(1920, 1080, 150);
            AddInstallation();

            await Search();

            Assert.That(model.Snapshot.ScalingPercent, Is.EqualTo(150));
        }

        // --- The wrapper -----------------------------------------------------------------------------------------------

        [Test]
        public async Task WithoutAWrapper_NoConfIsRead()
        {
            AddInstallation();
            w.FileSystem.AddFile(EeFolder + @"\dgVoodoo.conf", "[General]\r\nOutputAPI = d3d11_fl10_1\r\n");

            await Search();

            Assert.That(model.Snapshot.Wrapper.Kind, Is.EqualTo(WrapperKind.None));
            Assert.That(model.Snapshot.Configs, Is.Empty);
        }

        [Test]
        public async Task ADgVoodooWrapper_ShowsTheConfOfEveryGame()
        {
            AddInstallation("game,gameaoc," + Wrapper + @"," + Wrapper + @"\dx11_lvl10_1");
            w.FileSystem.AddFile(EeFolder + @"\dgVoodoo.conf", "[General]\r\nOutputAPI = d3d11_fl10_1\r\nFullScreenMode = true\r\n");

            await Search();

            Assert.That(model.Snapshot.Wrapper.Kind, Is.EqualTo(WrapperKind.DgVoodoo));
            Assert.That(model.Snapshot.Wrapper.ApiLevel, Is.EqualTo("10.1"));
            Assert.That(model.Snapshot.Configs.Select(line => line.Game.Id + ":" + line.File.Status),
                Is.EqualTo(new[] { "EE:Read", "AoC:Missing" }));
            Assert.That(model.Snapshot.Configs[0].File.Conf.Find("OutputAPI").Value, Is.EqualTo("d3d11_fl10_1"));
        }

        [Test]
        public async Task TheConf_IsReadWhereTheGameReadsIt_TheVirtualStoreCopyFirst()
        {
            AddInstallation("game," + Wrapper + @"\dx11_lvl11");
            w.FileSystem.AddFile(EeFolder + @"\dgVoodoo.conf", "[General]\r\nOutputAPI = d3d11_fl11_0\r\n");
            w.FileSystem.AddFile(VirtualStore + @"\Program Files (x86)\Neo Empire Earth\Empire Earth\dgVoodoo.conf",
                "[General]\r\nOutputAPI = d3d11_fl10_0\r\n");

            await Search();

            ConfigFileStatus status = model.Snapshot.Configs.Single().File.Status;
            Assert.That(status, Is.EqualTo(ConfigFileStatus.Read));
            Assert.That(model.Snapshot.Configs.Single().File.IsVirtualStoreCopy, Is.True);
            Assert.That(model.Snapshot.Configs.Single().File.Conf.Find("OutputAPI").Value, Is.EqualTo("d3d11_fl10_0"));
        }

        [Test]
        public async Task AWrapperKnownByItsFilesOnly_WithoutAConf_ShowsNoConfLines()
        {
            w.World.AddEmpireEarth(@"C:\Games\EE");
            w.FileSystem.AddFile(@"C:\Games\EE\DDraw.dll", "x");
            await installations.ChooseFolderAsync(@"C:\Games\EE");
            await model.LastRead;

            Assert.That(model.Snapshot.Wrapper.Kind, Is.EqualTo(WrapperKind.Other));
            Assert.That(model.Snapshot.Configs, Is.Empty, "no dgVoodoo.conf: nothing to show for a wrapper nobody can name");
        }

        // --- The change of the window size -------------------------------------------------------------------------------

        [Test]
        public async Task ApplyingASize_WritesBothGames_ReadsAgain_AndTellsTheGameSettingsPage()
        {
            AddInstallation();
            await Search();
            int gameSettingsChanges = 0;
            gameSettings.Changed += (sender, e) => gameSettingsChanges++;

            await model.ApplyResolutionAsync(new ScreenSize(1600, 900));

            Assert.That(model.LastResult.IsDone, Is.True, model.LastResult.ToString());
            Assert.That(model.LastSize, Is.EqualTo(new ScreenSize(1600, 900)));
            Assert.That(w.Get(NeoEE, "Game Window Width"), Is.EqualTo(RegistryValue.FromDWord(1600)));
            Assert.That(w.Get(NeoAoC, "Game Window Height"), Is.EqualTo(RegistryValue.FromDWord(900)));
            Assert.That(model.Snapshot.Windows.Select(line => line.Size.ToString()), Is.All.EqualTo("1600x900"));
            Assert.That(model.Snapshot.Changes(new ScreenSize(1600, 900)), Is.False, "nothing left to apply");
            Assert.That(model.IsBusy, Is.False);
            Assert.That(gameSettingsChanges, Is.GreaterThan(0), "the hints of the Play page are read again");
            Assert.That(w.FileSystem.DirectoryExists(GameSettingsWorld.BackupsFolder), Is.True, "backup first");
        }

        [Test]
        public async Task ApplyingASize_ChangesNoFileOfTheGameFolders()
        {
            AddInstallation("game," + Wrapper + @"\dx11_lvl10_1");
            const string conf = "[General]\r\nOutputAPI = d3d11_fl10_1\r\nFullScreenMode = true\r\n";
            w.FileSystem.AddFile(EeFolder + @"\dgVoodoo.conf", conf);
            await Search();
            string[] before = w.FileSystem.GetFiles(EeFolder).Value.ToArray();

            await model.ApplyResolutionAsync(new ScreenSize(1280, 960));

            Assert.That(w.FileSystem.GetText(EeFolder + @"\dgVoodoo.conf"), Is.EqualTo(conf));
            Assert.That(w.FileSystem.GetFiles(EeFolder).Value, Is.EqualTo(before));
            Assert.That(model.Snapshot.Configs.Single().File.Conf.Find("FullScreenMode").Value, Is.EqualTo("true"));
        }

        [Test]
        public async Task WhileASetupRuns_NothingCanBeChanged_AndNothingIsRead()
        {
            AddInstallation();
            await Search();
            Tick("NeoEE_Setup");
            Task read = model.RefreshAsync();

            Assert.That(model.CanChange, Is.False);
            Assert.That(model.RunningSetup, Is.SameAs(SetupKind.NeoEE));
            Assert.That(read.IsCompleted, Is.True);
            Assert.That(w.Logger.Messages, Has.Some.Contains("Graphics page: no read while a setup is running"));
            await model.ApplyResolutionAsync(new ScreenSize(1600, 900));
            Assert.That(model.LastResult.Outcome, Is.EqualTo(GameSettingsOutcome.Blocked));
            Assert.That(model.LastResult.Block.Block, Is.EqualTo(MutationBlock.SetupRunning));
            Assert.That(w.Get(NeoEE, "Game Window Width"), Is.EqualTo(RegistryValue.FromDWord(1920)));
        }

        [TestCase("StainlessSteelStudiosPresentsEmpireEarth")]
        [TestCase("MadDocSoftwarePresentsEmpireEarthExpansion")]
        public async Task WhileAGameRuns_TheSizeIsNotWritten(string mutex)
        {
            AddInstallation();
            await Search();
            w.Mutexes.With(mutex);

            await model.ApplyResolutionAsync(new ScreenSize(1600, 900));

            Assert.That(model.LastResult.Outcome, Is.EqualTo(GameSettingsOutcome.Blocked));
            Assert.That(model.LastResult.Block.Block, Is.EqualTo(MutationBlock.GameRunning));
            Assert.That(w.Get(NeoEE, "Game Window Width"), Is.EqualTo(RegistryValue.FromDWord(1920)));
            Assert.That(w.FileSystem.DirectoryExists(GameSettingsWorld.BackupsFolder), Is.False, "no backup either");
        }

        [Test]
        public async Task AnInstallationOfANewerContract_CannotBeChanged()
        {
            w.AddAdminInstallationOfAnotherAccount(Root, Product.NeoEE, "game", contractVersion: 2);

            await Search();

            Assert.That(model.Selected.HasNewerContract, Is.True);
            Assert.That(model.CanChange, Is.False);
        }

        [Test]
        public async Task ApplyingWithoutAnInstallation_DoesNothing()
        {
            await installations.RefreshAsync();

            await model.ApplyResolutionAsync(new ScreenSize(1600, 900));

            Assert.That(model.LastResult, Is.Null);
            Assert.That(w.Changes, Is.Empty);
        }

        [Test]
        public async Task ASizeOutsideTheLimits_IsRefusedByTheCore()
        {
            AddInstallation();
            await Search();

            Assert.That(async () => await model.ApplyResolutionAsync(new ScreenSize(1920, 1201)),
                Throws.InstanceOf<System.ArgumentOutOfRangeException>());

            Assert.That(model.IsBusy, Is.False, "the page can be used again");
            Assert.That(w.Get(NeoEE, "Game Window Height"), Is.EqualTo(RegistryValue.FromDWord(1080)));
        }
    }
}

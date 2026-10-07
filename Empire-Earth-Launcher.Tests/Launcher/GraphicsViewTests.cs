using System.Linq;
using System.Threading.Tasks;
using Empire_Earth_Launcher.Core.GameSettings;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Tests.TestSupport;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Launcher
{
    /// <summary>
    /// <see cref="GraphicsView"/>, everything the Graphics page decides: which controls are shown and enabled, which size of the
    /// list is selected, and the texts of the state, in English and for German and French where the words differ. The page
    /// itself (a Krypton combo box) can only be created on Windows; it assigns this view and is measured by
    /// <c>PageLayoutTests</c>.
    /// </summary>
    [TestFixture]
    [SetUICulture("en-US")]
    public class GraphicsViewTests
    {
        private GraphicsModelWorld g;

        [SetUp]
        public void SetUp()
        {
            g = new GraphicsModelWorld();
        }

        private GraphicsView View(ScreenSize chosen = default)
        {
            return GraphicsView.Of(g.Model, g.Watcher, chosen);
        }

        [Test]
        public void BeforeTheSearchHasFinished_ThePageSaysSearching_AndOffersNothing()
        {
            GraphicsView view = View();

            Assert.That(view.Installation, Is.EqualTo("Searching for Empire Earth installations..."));
            Assert.That(view.ShowChooser, Is.False);
            Assert.That(view.ApplyEnabled, Is.False);
            Assert.That(view.CurrentSizes, Is.Empty);
            Assert.That(view.WrapperInstalled, Is.Empty);
            Assert.That(view.Options, Is.Empty);
            Assert.That(view.SelectedIndex, Is.EqualTo(-1));
        }

        [Test]
        public async Task WithoutAnInstallation_ThePageSaysSo_AndOffersNothing()
        {
            await g.Installations.RefreshAsync();

            GraphicsView view = View();

            Assert.That(view.Installation, Is.EqualTo("Empire Earth installation not found"));
            Assert.That(view.ShowChooser, Is.False);
            Assert.That(view.CurrentSizes, Is.Empty);
        }

        [Test]
        public async Task AfterTheRead_TheChooserShowsTheSizesAndSelectsTheCurrentOne()
        {
            g.AddInstallation();
            await g.Search();

            GraphicsView view = View();

            Assert.That(view.ShowChooser, Is.True);
            Assert.That(view.CurrentSizes, Is.EqualTo("Window size of Empire Earth: 1920x1080" + System.Environment.NewLine +
                                                      "Window size of The Art of Conquest: 1920x1080"));
            Assert.That(view.OptionTexts, Does.Contain("1600x900 (16:9)"));
            Assert.That(view.OptionTexts[view.SelectedIndex], Is.EqualTo("1920x1080 (16:9, recommended)"));
            Assert.That(view.ChooserEnabled, Is.True);
            Assert.That(view.ApplyEnabled, Is.False, "the size is the one of both games already");
            Assert.That(view.Result, Is.Empty);
            Assert.That(view.Scaling, Is.Empty);
            Assert.That(view.WrapperInstalled, Is.EqualTo("Installed: Native (no wrapper)"));
            Assert.That(view.WrapperConfig, Is.Empty);
        }

        [Test]
        public async Task AnotherSizeChosen_EnablesTheButton()
        {
            g.AddInstallation();
            await g.Search();

            GraphicsView view = View(new ScreenSize(1600, 900));

            Assert.That(view.OptionTexts[view.SelectedIndex], Is.EqualTo("1600x900 (16:9)"));
            Assert.That(view.ApplyEnabled, Is.True);
        }

        [Test]
        public async Task ASizeThatDiffersInOneGameOnly_CanStillBeApplied()
        {
            g.AddInstallation();
            g.World.RawRegistry.Seed(GraphicsModelWorld.NeoAoC, "Game Window Width", RegistryValue.FromDWord(1600));
            g.World.RawRegistry.Seed(GraphicsModelWorld.NeoAoC, "Game Window Height", RegistryValue.FromDWord(900));
            await g.Search();

            GraphicsView view = View();

            Assert.That(view.OptionTexts[view.SelectedIndex], Is.EqualTo("1920x1080 (16:9, recommended)"), "the size of the first game");
            Assert.That(view.ApplyEnabled, Is.True, "The Art of Conquest has another size");
        }

        [Test]
        public async Task ACurrentSizeOutsideTheList_SelectsNothing_AndShowsItAsText()
        {
            g.AddInstallation();
            g.World.RawRegistry.Seed(GraphicsModelWorld.NeoEE, "Game Window Width", RegistryValue.FromDWord(1234));
            g.World.RawRegistry.Seed(GraphicsModelWorld.NeoEE, "Game Window Height", RegistryValue.FromDWord(777));
            await g.Search();

            GraphicsView view = View();

            Assert.That(view.SelectedIndex, Is.EqualTo(-1));
            Assert.That(view.ApplyEnabled, Is.False, "nothing is chosen");
            Assert.That(view.CurrentSizes, Does.StartWith("Window size of Empire Earth: 1234x777"));
        }

        [Test]
        public async Task AChosenSizeThatIsNotInTheList_SelectsNothing()
        {
            g.AddInstallation();
            await g.Search();

            GraphicsView view = View(new ScreenSize(1111, 700));

            Assert.That(view.SelectedIndex, Is.EqualTo(-1));
            Assert.That(view.ApplyEnabled, Is.False);
        }

        /// <summary>The Play list switches the installation with every click: a size chosen for one is not carried to the other.</summary>
        [Test]
        public async Task AChosenSize_IsKeptForItsInstallationOnly()
        {
            g.AddInstallation();
            await g.Search();
            GraphicsSnapshot snapshot = g.Model.Snapshot;
            var chosen = new ScreenSize(1600, 900);

            Assert.That(GraphicsView.KeepChoice(chosen, snapshot.Installation.Root, snapshot), Is.EqualTo(chosen));
            Assert.That(GraphicsView.KeepChoice(chosen, snapshot.Installation.Root.ToUpperInvariant() + @"\", snapshot), Is.EqualTo(chosen),
                "the same folder in another spelling");
            Assert.That(GraphicsView.KeepChoice(chosen, @"D:\Another\Neo Empire Earth", snapshot).IsEmpty, Is.True);
            Assert.That(GraphicsView.KeepChoice(chosen, null, snapshot).IsEmpty, Is.True, "chosen for no installation");
            Assert.That(GraphicsView.KeepChoice(chosen, @"D:\Another\Neo Empire Earth", null), Is.EqualTo(chosen),
                "no state is shown: nothing to compare with, the choice waits");
            Assert.That(GraphicsView.KeepChoice(ScreenSize.Empty, @"D:\Another", snapshot).IsEmpty, Is.True);
        }

        [Test]
        public async Task WhileTheFirstReadRuns_ThePageSaysChecking_AndOffersNothing()
        {
            g.AddInstallation();
            GraphicsView during = null;
            g.Model.Changed += (sender, e) =>
            {
                if (g.Model.IsReading && during == null)
                    during = View();
            };

            await g.Search();

            Assert.That(during, Is.Not.Null, "the start of the read raises Changed");
            Assert.That(during.CurrentSizes, Is.EqualTo("Checking..."));
            Assert.That(during.ShowChooser, Is.False);
            Assert.That(during.ApplyEnabled, Is.False);
        }

        [Test]
        public async Task WhileALaterReadRuns_TheLastStateStaysVisible_ButNothingCanBeChanged()
        {
            g.AddInstallation();
            await g.Search();
            GraphicsView during = null;
            g.Model.Changed += (sender, e) =>
            {
                if (g.Model.IsReading && during == null)
                    during = View(new ScreenSize(1600, 900));
            };

            await g.Model.RefreshAsync();

            Assert.That(during.ShowChooser, Is.True);
            Assert.That(during.CurrentSizes, Does.Contain("1920x1080"));
            Assert.That(during.ChooserEnabled, Is.False);
            Assert.That(during.ApplyEnabled, Is.False, "no change while a read runs");
        }

        [Test]
        public async Task WhileASetupRuns_TheSetupIsNamed_AndNothingCanBeChosen()
        {
            g.AddInstallation();
            await g.Search();
            g.StartSetup();

            GraphicsView view = View(new ScreenSize(1600, 900));

            Assert.That(view.Result, Is.EqualTo("The NeoEE setup is running. Until it has ended, the launcher starts no game and changes no game settings."));
            Assert.That(view.ChooserEnabled, Is.False);
            Assert.That(view.ApplyEnabled, Is.False);
        }

        [Test]
        public async Task AfterAChange_TheResultNamesTheSizeAndTheBackup()
        {
            g.AddInstallation();
            await g.Search();

            await g.Model.ApplyResolutionAsync(new ScreenSize(1600, 900));
            GraphicsView view = View(new ScreenSize(1600, 900));

            Assert.That(view.Result, Does.StartWith("The game window is set to 1600x900. The previous values are saved in "));
            Assert.That(view.CurrentSizes, Does.Contain("1600x900"));
            Assert.That(view.ApplyEnabled, Is.False, "nothing left to apply");
        }

        [Test]
        public async Task AGameThatRuns_BlocksTheChange_AndTheResultSaysWhy()
        {
            g.AddInstallation();
            await g.Search();
            g.World.Mutexes.With("StainlessSteelStudiosPresentsEmpireEarth");

            await g.Model.ApplyResolutionAsync(new ScreenSize(1600, 900));

            Assert.That(View().Result, Is.EqualTo("Not possible while Empire Earth.exe is running."));
        }

        [Test]
        public async Task OnAScaledScreen_TheScalingIsNamed()
        {
            g.World.SystemInfo.WithScreen(1920, 1080, 150);
            g.AddInstallation();
            await g.Search();

            Assert.That(View().Scaling, Does.Contain("150 %").And.Contain("HIGHDPIAWARE"));
        }

        [Test]
        public async Task ADgVoodooWrapper_ShowsItsNameAndTheConf()
        {
            g.AddInstallation("game," + GraphicsModelWorld.Wrapper + @"\dx11_lvl10_1");
            g.World.FileSystem.AddFile(GraphicsModelWorld.EeFolder + @"\dgVoodoo.conf", "[General]\r\nOutputAPI = d3d11_fl10_1\r\nFullScreenMode = true\r\n");
            await g.Search();

            GraphicsView view = View();

            Assert.That(view.WrapperInstalled, Is.EqualTo("Installed: DirectX 11 (dgVoodoo), API level 10.1"));
            Assert.That(view.WrapperConfig, Does.Contain("OutputAPI = d3d11_fl10_1").And.Contain("FullScreenMode = true"));
        }

        [TestCase("de", "Installiert: DirectX 11 (dgVoodoo), API-Level 10.1")]
        [TestCase("fr", "Installé : DirectX 11 (dgVoodoo), niveau d'API 10.1")]
        public async Task TheTextsOfTheView_AreInTheUiLanguage(string language, string installed)
        {
            g.AddInstallation("game," + GraphicsModelWorld.Wrapper + @"\dx11_lvl10_1");
            await g.Search();

            using (TestUiLanguage.Use(language))
            {
                GraphicsView view = View();

                Assert.That(view.WrapperInstalled, Is.EqualTo(installed));
                Assert.That(view.OptionTexts.Last(), Does.Contain("1920x1080"));
                Assert.That(view.CurrentSizes, Does.Contain("1920x1080"));
            }
        }
    }
}

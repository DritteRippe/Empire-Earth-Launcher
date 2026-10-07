using System.Globalization;
using System.Linq;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Diagnostics;
using Empire_Earth_Launcher.Core.GameSettings;
using Empire_Earth_Launcher.Core.Graphics;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Tests.Fakes;
using Empire_Earth_Launcher.Tests.TestSupport;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Launcher
{
    /// <summary>
    /// The texts of the graphics page (<see cref="Texts"/>, launcher 1.1.0): the sizes of the list, the window size of a game,
    /// the result of the button, the wrapper and the lines of <c>dgVoodoo.conf</c>. English is the neutral language; German and
    /// French have the same keys (<c>ResourceParityTests</c>) and are checked here for the parts that must not be lost in
    /// translation: the numbers, the names of the keys and the quoted names of the setup.
    /// </summary>
    [TestFixture]
    [SetUICulture("en-US")]
    public class GraphicsTextsTests
    {
        private const string Root = GameSettingsWorld.NeoRoot;
        private const string Wrapper = @"additional\directx_wrapper";

        private static ResolutionOption Option(int width, int height, bool recommended = false)
        {
            return ResolutionOptions.For(new FakeSystemInfo().WithScreen(width, height))
                                    .Single(option => option.Size == new ScreenSize(width, height) && option.IsRecommended == recommended);
        }

        private static WrapperInfo Describe(string components)
        {
            var w = new GameSettingsWorld();
            w.AddAdminInstallationOfAnotherAccount(Root, Product.NeoEE, components);
            return WrapperInfo.Describe(w.Discover().Selected, w.FileSystem);
        }

        // --- Sizes ------------------------------------------------------------------------------------------------------

        [Test]
        public void ASize_NamesItsShape_AndWhetherItIsRecommended()
        {
            var screen = new FakeSystemInfo().WithScreen(1920, 1080);
            ResolutionOption[] options = ResolutionOptions.For(screen).ToArray();

            string[] texts = options.Select(Texts.ResolutionChoice).ToArray();

            Assert.That(texts, Does.Contain("1024x768 (4:3)"));
            Assert.That(texts, Does.Contain("1280x1024 (5:4)"));
            Assert.That(texts, Does.Contain("1440x900 (16:10)"));
            Assert.That(texts, Does.Contain("1600x900 (16:9)"));
            Assert.That(texts, Does.Contain("1920x1080 (16:9, recommended)"));
        }

        [Test]
        public void ARecommendedSizeOfAnUnusualShape_HasOnlyTheTag()
        {
            var screen = new FakeSystemInfo().WithScreen(1280, 768);

            Assert.That(Texts.ResolutionChoice(ResolutionOptions.For(screen).Single(option => option.IsRecommended)), Is.EqualTo("1280x768 (recommended)"));
        }

        [TestCase("de", "empfohlen")]
        [TestCase("fr", "recommandée")]
        public void TheRecommendedTag_IsTranslated_TheShapeIsNot(string language, string tag)
        {
            using (TestUiLanguage.Use(language))
            {
                Assert.That(Texts.ResolutionChoice(Option(1920, 1080, true)), Is.EqualTo("1920x1080 (16:9, " + tag + ")"));
            }
        }

        [Test]
        public void TheWindowSizeOfAGame_OrThatItIsNotSetYet()
        {
            Assert.That(Texts.WindowSize(Game.EmpireEarth, new ScreenSize(1600, 900)), Is.EqualTo("Window size of Empire Earth: 1600x900"));
            Assert.That(Texts.WindowSize(Game.ArtOfConquest, ScreenSize.Empty), Is.EqualTo("Window size of The Art of Conquest: not set yet"));
        }

        // --- The result of the button ------------------------------------------------------------------------------------

        private GameSettingsWorld WorldWithGame()
        {
            var w = new GameSettingsWorld();
            w.AddAdminInstallationOfAnotherAccount(Root, Product.NeoEE, "game");
            w.RawRegistry.Seed(GameSettingsWorld.Settings(Product.NeoEE, Game.EmpireEarth), "Game Window Width", RegistryValue.FromDWord(1920));
            w.RawRegistry.Seed(GameSettingsWorld.Settings(Product.NeoEE, Game.EmpireEarth), "Game Window Height", RegistryValue.FromDWord(1080));
            return w;
        }

        [Test]
        public void TheResult_NamesTheSizeAndTheBackupFolder()
        {
            GameSettingsWorld w = WorldWithGame();
            GameSettingsResult result = w.CreateDefaultsService().SetGameWindow(w.Discover().Selected, new ScreenSize(1600, 900));

            Assert.That(Texts.WindowSizeResult(result, new ScreenSize(1600, 900)),
                Is.EqualTo("The game window is set to 1600x900. The previous values are saved in " + result.BackupFolder + "."));
        }

        [Test]
        public void TheResult_SaysThatNothingChanged_WhenTheSizeWasThereAlready()
        {
            GameSettingsWorld w = WorldWithGame();
            GameSettingsResult result = w.CreateDefaultsService().SetGameWindow(w.Discover().Selected, new ScreenSize(1920, 1080));

            Assert.That(Texts.WindowSizeResult(result, new ScreenSize(1920, 1080)),
                Is.EqualTo("The game window already has the size 1920x1080; nothing was changed."));
        }

        [Test]
        public void TheResult_SaysWhyNot_WhenAGameRuns()
        {
            GameSettingsWorld w = WorldWithGame();
            w.Mutexes.With("StainlessSteelStudiosPresentsEmpireEarth");
            GameSettingsResult result = w.CreateDefaultsService().SetGameWindow(w.Discover().Selected, new ScreenSize(1600, 900));

            Assert.That(Texts.WindowSizeResult(result, new ScreenSize(1600, 900)), Is.EqualTo("Not possible while Empire Earth.exe is running."));
        }

        // --- The wrapper -------------------------------------------------------------------------------------------------

        [Test]
        public void TheWrapperNames_AreWordedLikeTheSetupsCaptions()
        {
            Assert.That(Texts.WrapperName(Describe("game")), Is.EqualTo("Native (no wrapper)"));
            Assert.That(Texts.WrapperName(Describe("game," + Wrapper + @"\dx7")), Is.EqualTo("DirectX 7 (DDrawCompat)"));
            Assert.That(Texts.WrapperName(Describe("game," + Wrapper + @"\dx9")), Is.EqualTo("DirectX 9"));
            Assert.That(Texts.WrapperName(Describe("game," + Wrapper + @"\dx11_lvl10_1")), Is.EqualTo("DirectX 11 (dgVoodoo), API level 10.1"));
            Assert.That(Texts.WrapperName(Describe("game," + Wrapper + @"\dx12_lvl12")), Is.EqualTo("DirectX 12 (dgVoodoo), API level 12"));
            Assert.That(Texts.WrapperName(Describe("game," + Wrapper + @"\future")), Is.EqualTo("a wrapper this launcher does not know (future)"));
            Assert.That(Texts.WrapperName(Describe("game," + Wrapper)), Is.EqualTo("a DirectX wrapper"));
        }

        [TestCase("de", "DirectX 11 (dgVoodoo), API-Level 10.1", "Nativ (kein Wrapper)")]
        [TestCase("fr", "DirectX 11 (dgVoodoo), niveau d'API 10.1", "Natif (aucun wrapper)")]
        public void TheWrapperNames_AreTranslated_TheNumbersAreNot(string language, string dgVoodoo, string native)
        {
            using (TestUiLanguage.Use(language))
            {
                Assert.That(Texts.WrapperName(Describe("game," + Wrapper + @"\dx11_lvl10_1")), Is.EqualTo(dgVoodoo));
                Assert.That(Texts.WrapperName(Describe("game")), Is.EqualTo(native));
            }
        }

        [Test]
        public void AWrapperJudgedFromFiles_SaysSo()
        {
            var w = new GameSettingsWorld();
            w.World.AddEmpireEarth(@"C:\Games\EE");
            w.FileSystem.AddFile(@"C:\Games\EE\DDraw.dll", "x");
            WrapperInfo info = WrapperInfo.Describe(w.Discover(@"C:\Games\EE").Selected, w.FileSystem);

            Assert.That(Texts.WrapperInstalled(info), Is.EqualTo("Installed: a wrapper file (DDraw.dll)" + System.Environment.NewLine +
                "No setup record names the components; this is judged from the files in the game folder."));
            Assert.That(Texts.WrapperInstalled(Describe("game")), Is.EqualTo("Installed: Native (no wrapper)"));
        }

        // --- dgVoodoo.conf -----------------------------------------------------------------------------------------------

        private static WrapperConfLine Conf(Game game, string text, bool virtualStore = false)
        {
            var w = new GameSettingsWorld();
            const string folder = Root + @"\Empire Earth";
            w.FileSystem.AddDrive("C:");
            w.FileSystem.AddFile(folder + @"\dgVoodoo.conf", text);
            var paths = new EffectivePathResolver(w.FileSystem, @"C:\Users\Player\AppData\Local\VirtualStore", new[] { @"C:\Program Files (x86)" });
            if (virtualStore)
                w.FileSystem.AddFile(@"C:\Users\Player\AppData\Local\VirtualStore\Program Files (x86)\Neo Empire Earth\Empire Earth\dgVoodoo.conf", text);
            return new WrapperConfLine(game, DgVoodooConfReader.Read(w.FileSystem, paths, folder));
        }

        [Test]
        public void TheConf_ShowsTheOutputApiAndEveryScreenModeKey_AMissingKeyIsNotSet()
        {
            string text = Texts.WrapperConfigs(new[]
            {
                Conf(Game.EmpireEarth, "[General]\r\nOutputAPI = d3d11_fl10_1\r\nFullScreenMode = true\r\n[DirectX]\r\nAppControlledScreenMode = true\r\n" +
                                       "DisableAltEnterToToggleScreenMode = false\r\n[GeneralExt]\r\nWindowedAttributes\t\t= Borderless, AlwaysOnTop\r\n")
            });

            string[] lines = text.Split(new[] { System.Environment.NewLine }, System.StringSplitOptions.None);
            Assert.That(lines, Is.EqualTo(new[]
            {
                "dgVoodoo.conf of Empire Earth (shown only):",
                "OutputAPI = d3d11_fl10_1",
                "FullScreenMode = true",
                "AppControlledScreenMode = true",
                "DisableAltEnterToToggleScreenMode = false",
                "DeferredScreenModeSwitch = not set",
                "WindowedAttributes = Borderless, AlwaysOnTop",
                "Resolution = not set",
                "ScalingMode = not set",
                "CaptureMouse = not set",
            }));
        }

        [Test]
        public void TheConf_OfAVirtualStoreCopy_SaysThatTheGameReadsIt()
        {
            string text = Texts.WrapperConfigs(new[] { Conf(Game.EmpireEarth, "[General]\r\nOutputAPI = d3d11_fl10_0\r\n", true) });

            Assert.That(text, Does.Contain("The game reads the copy in the VirtualStore."));
        }

        [Test]
        public void TheConfs_OfTwoGames_AreSeparatedByABlankLine()
        {
            string text = Texts.WrapperConfigs(new[]
            {
                Conf(Game.EmpireEarth, "[General]\r\nOutputAPI = a\r\n"), Conf(Game.ArtOfConquest, "[General]\r\nOutputAPI = b\r\n")
            });

            Assert.That(text, Does.Contain("OutputAPI = a"));
            Assert.That(text, Does.Contain(System.Environment.NewLine + System.Environment.NewLine + "dgVoodoo.conf of The Art of Conquest (shown only):"));
        }

        [Test]
        public void AMissingOrUnreadableConf_IsOneLine()
        {
            var w = new GameSettingsWorld();
            w.FileSystem.AddDrive("C:");
            w.FileSystem.AddDirectory(Root + @"\Empire Earth");
            w.FileSystem.AddFile(Root + @"\Empire Earth - The Art of Conquest\dgVoodoo.conf", new byte[KeyValueFile.MaxBytes + 1]);
            var paths = new EffectivePathResolver(w.FileSystem, null, new string[0]);
            var missing = new WrapperConfLine(Game.EmpireEarth, DgVoodooConfReader.Read(w.FileSystem, paths, Root + @"\Empire Earth"));
            var unreadable = new WrapperConfLine(Game.ArtOfConquest,
                DgVoodooConfReader.Read(w.FileSystem, paths, Root + @"\Empire Earth - The Art of Conquest"));

            Assert.That(Texts.WrapperConfigs(new[] { missing }), Is.EqualTo("dgVoodoo.conf of Empire Earth was not found."));
            Assert.That(Texts.WrapperConfigs(new[] { unreadable }), Is.EqualTo("dgVoodoo.conf of The Art of Conquest could not be read."));
        }

        [TestCase("de", "nicht gesetzt")]
        [TestCase("fr", "non défini")]
        public void TheNamesOfTheKeys_AreNotTranslated(string language, string notSet)
        {
            using (TestUiLanguage.Use(language))
            {
                string text = Texts.WrapperConfigs(new[] { Conf(Game.EmpireEarth, "[General]\r\nFullScreenMode = true\r\n") });

                Assert.That(text, Does.Contain("FullScreenMode = true"));
                Assert.That(text, Does.Contain("CaptureMouse = " + notSet));
            }
        }

        // --- The hand-off ------------------------------------------------------------------------------------------------

        [TestCase("en", "Advanced: go through the setup of each game myself", "Custom install settings", "DirectX Wrapper", "Recommended settings", "Native")]
        [TestCase("de", "Erweitert: Das Setup jedes Spiels selbst durchgehen", "Benutzerdefinierte Installationseinstellungen", "DirectX-Wrapper", "Empfohlene Einstellungen", "Nativ")]
        [TestCase("fr", "Avancé : parcourir moi-même le programme d'installation de chaque jeu", "Paramètres d'installation personnalisés", "Wrapper DirectX", "Paramètres recommandés", "Natif")]
        public void TheHandOff_UsesTheWordsOfTheSuiteAndTheSetupWizard(string language, params string[] names)
        {
            using (TestUiLanguage.Use(language))
            {
                string text = Empire_Earth_Launcher.Properties.Resources.GraphicsWrapperChangeInfo;

                foreach (string name in names)
                    Assert.That(text, Does.Contain(name), language);
            }
        }

        [TestCase("en")]
        [TestCase("de")]
        [TestCase("fr")]
        public void TheExplanations_NameTheLimitOfTheList(string language)
        {
            using (TestUiLanguage.Use(language))
            {
                Assert.That(Empire_Earth_Launcher.Properties.Resources.GraphicsWindowSizeInfo, Does.Contain("1920x1080").And.Contain("1024x768"));
                Assert.That(string.Format(CultureInfo.CurrentCulture, Empire_Earth_Launcher.Properties.Resources.GraphicsWindowSizeScalingFormat, 150),
                    Does.Contain("150").And.Contain("HIGHDPIAWARE"));
            }
        }
    }
}

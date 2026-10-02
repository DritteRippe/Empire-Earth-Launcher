using System;
using System.Linq;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.GameSettings;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Tests.Fakes;
using Empire_Earth_Launcher.Tests.TestSupport;
using Microsoft.Win32;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.GameSettings
{
    /// <summary>
    /// The computed values of contract 3.3: "Installed From" from the real game folder (ADR 0015), the rasterizer by the
    /// wrapper rule, the window size in physical pixels with its limits, and the warning below 768 pixels (R13).
    /// </summary>
    [TestFixture]
    public class ComputedValuesTests
    {
        private const string NeoRoot = @"C:\Program Files (x86)\Neo Empire Earth";

        // --- Installed From (class S) ------------------------------------------------------------------------------

        /// <summary>For community installations the values are byte-identical with the formula of contract 3.3.</summary>
        [Test]
        public void InstalledFrom_3_3_CommunityInstallationIsTheContractExample()
        {
            Assert.That(InstalledFromValues.TryCompute(NeoRoot + @"\Empire Earth", out InstalledFromValues ee), Is.True);
            Assert.That(InstalledFromValues.TryCompute(NeoRoot + @"\Empire Earth - The Art of Conquest", out InstalledFromValues aoc), Is.True);

            Assert.That(ee.Volume, Is.EqualTo("C:"));
            Assert.That(ee.Directory, Is.EqualTo(@"\PROGRAM FILES (X86)\NEO EMPIRE EARTH\Empire Earth\"));
            Assert.That(aoc.Directory, Is.EqualTo(@"\PROGRAM FILES (X86)\NEO EMPIRE EARTH\Empire Earth - The Art of Conquest\"));
        }

        /// <summary>The setup's formula for every root: the root without its first two characters, upper-cased, then the folder.</summary>
        [TestCase(@"C:\Program Files (x86)\Empire Earth")]
        [TestCase(@"D:\Spiele\Neo Empire Earth")]
        [TestCase(@"C:\Users\Player\AppData\Local\Programs\Neo Empire Earth")]
        [TestCase(@"E:\EE Portable")]
        public void InstalledFrom_3_3_IsTheSetupFormulaForCommunityRoots(string root)
        {
            foreach (Game game in Game.All)
            {
                InstalledFromValues.TryCompute(root + @"\" + game.FolderName, out InstalledFromValues values);

                Assert.That(values.Volume, Is.EqualTo(root.Substring(0, 2)));
                Assert.That(values.Directory, Is.EqualTo(root.Substring(2).ToUpperInvariant() + @"\" + game.FolderName + @"\"));
            }
        }

        /// <summary>A foreign installation keeps its real folder name (ADR 0015).</summary>
        [Test]
        public void InstalledFrom_ForeignFolderWithAnotherName()
        {
            var world = new InstallationWorld();
            world.AddForeignInstallation(@"C:\Games\EE", aocFolder: @"C:\Games\AoC");
            Installation installation = world.Discover().Selected;

            InstalledFromValues.TryCompute(installation.EeFolder, out InstalledFromValues ee);
            InstalledFromValues.TryCompute(installation.AocFolder, out InstalledFromValues aoc);

            Assert.That(ee.Volume, Is.EqualTo("C:"));
            Assert.That(ee.Directory, Is.EqualTo(@"\GAMES\EE\"));
            Assert.That(aoc.Directory, Is.EqualTo(@"\GAMES\AoC\").IgnoreCase);
        }

        /// <summary><c>D:\Empire Earth</c> has the root <c>D:\</c>: the directory is just the folder (ADR 0015).</summary>
        [Test]
        public void InstalledFrom_FolderDirectlyBelowADrive()
        {
            var world = new InstallationWorld();
            world.AddForeignInstallation(@"D:\Empire Earth");
            Installation installation = world.Discover().Selected;
            Assert.That(installation.Root, Is.EqualTo(@"D:\"));

            Assert.That(InstalledFromValues.TryCompute(installation.EeFolder, out InstalledFromValues values), Is.True);

            Assert.That(values.Volume, Is.EqualTo("D:"));
            Assert.That(values.Directory, Is.EqualTo(@"\Empire Earth\"));
        }

        /// <summary>Contract 3.3: a root that does not start with a drive letter cannot be expressed.</summary>
        [TestCase(@"\\server\games\Empire Earth")]
        [TestCase(@"\\?\C:\Games\EE")]
        [TestCase(@"Games\EE")]
        [TestCase(@"D:\")]
        [TestCase("")]
        public void InstalledFrom_NetworkPathsAndRootsHaveNoValues(string folder)
        {
            Assert.That(InstalledFromValues.TryCompute(folder, out InstalledFromValues values), Is.False);
            Assert.That(values, Is.Null);
        }

        [Test]
        public void InstalledFrom_NormalizesTheFolderFirst()
        {
            InstalledFromValues.TryCompute(@"c:/games//EE\", out InstalledFromValues values);

            Assert.That(values.Volume, Is.EqualTo("c:"));
            Assert.That(values.Directory, Is.EqualTo(@"\GAMES\EE\"));
        }

        /// <summary>
        /// Upper-casing and comparisons are independent of the culture: under <c>tr-TR</c> an <c>i</c> must become
        /// <c>I</c>, not the dotted <c>İ</c>, and <c>I</c> must equal <c>i</c> (ADR 0015, ADR 0012 design review).
        /// </summary>
        [Test]
        [SetCulture("tr-TR")]
        [SetUICulture("tr-TR")]
        public void InstalledFrom_UnderTurkishCulture()
        {
            InstalledFromValues.TryCompute(@"C:\Oyunlar\Bilgisayar\Empire Earth", out InstalledFromValues values);

            Assert.That(values.Directory, Is.EqualTo(@"\OYUNLAR\BILGISAYAR\Empire Earth\"));
            Assert.That(values.IsSameAs("c:", @"\oyunlar\bilgisayar\EMPIRE EARTH\"), Is.True);
            Assert.That(values.IsSameAs("C:", @"\OYUNLAR\BİLGİSAYAR\Empire Earth\"), Is.False, "dotted capital I is another letter");
        }

        /// <summary>
        /// The setup upper-cases ASCII letters only (Pascal Script UpperCase); the launcher with the invariant culture.
        /// The bytes differ for non-ASCII letters, the comparison treats them as equal, so the launcher never rewrites the
        /// setup's values (ADR 0015).
        /// </summary>
        [Test]
        public void InstalledFrom_SetupSpellingOfNonAsciiLettersIsTheSameFolder()
        {
            InstalledFromValues.TryCompute(@"C:\Spiele\Ägypten\Empire Earth", out InstalledFromValues values);

            Assert.That(values.Directory, Is.EqualTo(@"\SPIELE\ÄGYPTEN\Empire Earth\"));
            Assert.That(values.IsSameAs("C:", @"\SPIELE\äGYPTEN\Empire Earth\"), Is.True);
        }

        [TestCase("C:", @"\GAMES\EE\", true)]
        [TestCase(" c: ", @"\games\ee\", true)]
        [TestCase("C:", @"\\GAMES\\EE\\", true)]
        [TestCase("C:", "/GAMES/EE/", true)]
        [TestCase("D:", @"\GAMES\EE\", false)]
        [TestCase("C:", @"\GAMES\EE2\", false)]
        [TestCase("C:", @"\GAMES\EE", false)]
        [TestCase(null, @"\GAMES\EE\", false)]
        [TestCase("C:", null, false)]
        public void InstalledFrom_IsSameAs_IgnoresCaseSeparatorsAndDoubledBackslashes(string volume, string directory, bool expected)
        {
            InstalledFromValues.TryCompute(@"C:\Games\EE", out InstalledFromValues values);

            Assert.That(values.IsSameAs(volume, directory), Is.EqualTo(expected));
        }

        // --- Rasterizer Name ---------------------------------------------------------------------------------------

        private static Installation Community(InstallationWorld world, string components, bool legacy = false)
        {
            if (legacy)
            {
                world.AddCommunityFiles(NeoRoot, Product.NeoEE);
                world.AddUninstallKey(RegistryHive.LocalMachine, RegistryView.Registry64, Product.NeoEE, NeoRoot, components: components);
            }
            else
            {
                world.AddCommunityFiles(NeoRoot, Product.NeoEE);
                world.AddInstallInfo(NeoRoot, Product.NeoEE, components: components);
                world.AddUninstallKey(RegistryHive.LocalMachine, RegistryView.Registry64, Product.NeoEE, NeoRoot,
                    components: "game,gameaoc,additional\\directx_wrapper", contractVersion: 1);
            }
            return world.Discover().Selected;
        }

        [Test]
        public void Rasterizer_3_3_WrapperInInstallIni()
        {
            var world = new InstallationWorld();
            Installation installation = Community(world, @"game,gameaoc,additional,additional\directx_wrapper\dx11_lvl11");

            RasterizerRecommendation rasterizer = ComputedValues.Rasterizer(installation, Game.EmpireEarth, new FakeSystemInfo(), world.FileSystem);

            Assert.That(rasterizer.Name, Is.EqualTo("Direct3D"));
            Assert.That(rasterizer.Reason, Is.EqualTo(RasterizerReason.WrapperInInstallInfo));
        }

        /// <summary>install.ini wins over the uninstall key and over wrapper files in the folder.</summary>
        [Test]
        public void Rasterizer_3_3_NoWrapperInInstallIni_IgnoresTheUninstallKeyAndTheFiles()
        {
            var world = new InstallationWorld();
            Installation installation = Community(world, "game,gameaoc");
            world.FileSystem.AddFile(installation.EeFolder + @"\DDraw.dll", "dll");

            RasterizerRecommendation rasterizer = ComputedValues.Rasterizer(installation, Game.EmpireEarth, new FakeSystemInfo(), world.FileSystem);

            Assert.That(rasterizer.Name, Is.EqualTo("Direct3D Hardware TnL"));
            Assert.That(rasterizer.Reason, Is.EqualTo(RasterizerReason.NoWrapperInInstallInfo));
        }

        [Test]
        public void Rasterizer_3_3_WrapperInTheUninstallKeyOfASetupUpTo172()
        {
            var world = new InstallationWorld();
            Installation installation = Community(world, @"game,gameaoc,additional\directx_wrapper", legacy: true);

            RasterizerRecommendation rasterizer = ComputedValues.Rasterizer(installation, Game.ArtOfConquest, new FakeSystemInfo(), world.FileSystem);

            Assert.That(rasterizer.Name, Is.EqualTo("Direct3D"));
            Assert.That(rasterizer.Reason, Is.EqualTo(RasterizerReason.WrapperInUninstallKey));
        }

        [Test]
        public void Rasterizer_3_3_NoWrapperInTheUninstallKey_IgnoresTheFiles()
        {
            var world = new InstallationWorld();
            Installation installation = Community(world, "game,gameaoc", legacy: true);
            world.FileSystem.AddFile(installation.EeFolder + @"\D3D9.dll", "dll");

            RasterizerRecommendation rasterizer = ComputedValues.Rasterizer(installation, Game.EmpireEarth, new FakeSystemInfo(), world.FileSystem);

            Assert.That(rasterizer.Reason, Is.EqualTo(RasterizerReason.NoWrapperInUninstallKey));
            Assert.That(rasterizer.Name, Is.EqualTo("Direct3D Hardware TnL"));
        }

        [TestCase("DDraw.dll")]
        [TestCase("D3DImm.dll")]
        [TestCase("D3D8.dll")]
        [TestCase("d3d9.DLL")]
        public void Rasterizer_3_3_WithoutComponents_AWrapperFileInTheGameFolder(string file)
        {
            var world = new InstallationWorld();
            world.AddForeignInstallation(@"C:\Games\EE", aocFolder: @"C:\Games\AoC");
            world.FileSystem.AddFile(@"C:\Games\AoC\" + file, "dll");
            Installation installation = world.Discover().Selected;

            RasterizerRecommendation aoc = ComputedValues.Rasterizer(installation, Game.ArtOfConquest, new FakeSystemInfo(), world.FileSystem);
            RasterizerRecommendation ee = ComputedValues.Rasterizer(installation, Game.EmpireEarth, new FakeSystemInfo(), world.FileSystem);

            Assert.That(aoc.Name, Is.EqualTo("Direct3D"));
            Assert.That(aoc.Reason, Is.EqualTo(RasterizerReason.WrapperFile));
            Assert.That(aoc.WrapperFile, Is.EqualTo(file == "d3d9.DLL" ? "D3D9.dll" : file));
            Assert.That(ee.Name, Is.EqualTo("Direct3D Hardware TnL"), "the files of the other game folder do not count");
            Assert.That(ee.Reason, Is.EqualTo(RasterizerReason.NoWrapperFile));
        }

        [Test]
        public void Rasterizer_3_3_WineIsAlwaysDirect3D()
        {
            var world = new InstallationWorld();
            Installation installation = Community(world, "game,gameaoc");

            RasterizerRecommendation rasterizer = ComputedValues.Rasterizer(installation, Game.EmpireEarth,
                new FakeSystemInfo { IsWine = true }, world.FileSystem);

            Assert.That(rasterizer.Name, Is.EqualTo("Direct3D"));
            Assert.That(rasterizer.Reason, Is.EqualTo(RasterizerReason.Wine));
        }

        /// <summary>
        /// The wrapper rule on its own, for the diagnostics report (ADR 0014): also under Wine it says whether a wrapper is
        /// installed and where that comes from; <see cref="ComputedValues.IsWrapper"/> reads the reason.
        /// </summary>
        [Test]
        public void DirectXWrapper_3_3_TheWrapperRuleWithoutWine()
        {
            var world = new InstallationWorld();
            Installation community = Community(world, @"game,gameaoc,additional\directx_wrapper");
            world.AddForeignInstallation(@"C:\Games\EE");
            world.FileSystem.AddFile(@"C:\Games\EE\DDraw.dll", "dll");
            Installation foreign = world.Discover().Installations.Single(installation => installation.Kind == InstallationKind.Foreign);

            RasterizerRecommendation fromInstallIni = ComputedValues.DirectXWrapper(community, Game.EmpireEarth, world.FileSystem);
            RasterizerRecommendation fromFile = ComputedValues.DirectXWrapper(foreign, Game.EmpireEarth, world.FileSystem);
            RasterizerRecommendation underWine = ComputedValues.Rasterizer(foreign, Game.EmpireEarth,
                new FakeSystemInfo { IsWine = true }, world.FileSystem);

            Assert.That(fromInstallIni.Reason, Is.EqualTo(RasterizerReason.WrapperInInstallInfo));
            Assert.That(fromFile.Reason, Is.EqualTo(RasterizerReason.WrapperFile));
            Assert.That(fromFile.WrapperFile, Is.EqualTo("DDraw.dll"));
            Assert.That(underWine.Reason, Is.EqualTo(RasterizerReason.Wine), "the rasterizer rule keeps Wine first");
            Assert.That(Enum.GetValues(typeof(RasterizerReason)).Cast<RasterizerReason>().Where(ComputedValues.IsWrapper),
                Is.EquivalentTo(new[] { RasterizerReason.WrapperInInstallInfo, RasterizerReason.WrapperInUninstallKey, RasterizerReason.WrapperFile }));
        }

        // --- Window size -------------------------------------------------------------------------------------------

        [TestCase(1920, 1080, 100, 1920, 1080)]
        [TestCase(1366, 768, 100, 1366, 768)]
        [TestCase(2560, 1440, 100, 1920, 1080)]
        [TestCase(3840, 2160, 150, 1920, 1080)]
        [TestCase(1920, 1200, 100, 1920, 1080)]
        [TestCase(1280, 1024, 100, 1280, 1024)]
        [TestCase(1024, 600, 100, 1024, 768)]
        [TestCase(800, 600, 100, 1024, 768)]
        [TestCase(1920, 1080, 150, 1920, 1080)]
        [TestCase(1600, 900, 125, 1600, 900)]
        public void GameWindow_3_3_PhysicalPixelsEachDimensionClamped(int width, int height, int percent, int expectedWidth, int expectedHeight)
        {
            var systemInfo = new FakeSystemInfo().WithScreen(width, height, percent);

            Assert.That(ComputedValues.GameWindow(systemInfo), Is.EqualTo(new ScreenSize(expectedWidth, expectedHeight)));
        }

        [Test]
        public void GameWindow_UsesTheUnawareSizeOrTheMinimumWhenThePhysicalSizeIsUnknown()
        {
            var unknownPhysical = new FakeSystemInfo { PrimaryScreen = ScreenSize.Empty, PrimaryScreenUnaware = new ScreenSize(1280, 720) };
            var unknown = new FakeSystemInfo { PrimaryScreen = ScreenSize.Empty, PrimaryScreenUnaware = ScreenSize.Empty };

            Assert.That(ComputedValues.GameWindow(unknownPhysical), Is.EqualTo(new ScreenSize(1280, 768)));
            Assert.That(ComputedValues.GameWindow(unknown), Is.EqualTo(new ScreenSize(1024, 768)));
        }

        [TestCase(1024, 600, true)]
        [TestCase(1366, 767, true)]
        [TestCase(1366, 768, false)]
        [TestCase(1920, 1080, false)]
        public void IsScreenTooLow_R13_BelowSevenHundredSixtyEightPhysicalPixels(int width, int height, bool expected)
        {
            Assert.That(ComputedValues.IsScreenTooLow(new FakeSystemInfo().WithScreen(width, height)), Is.EqualTo(expected));
        }

        [Test]
        public void IsScreenTooLow_FalseForAnUnknownScreen()
        {
            Assert.That(ComputedValues.IsScreenTooLow(new FakeSystemInfo { PrimaryScreen = ScreenSize.Empty }), Is.False);
        }

        // --- All values of a game ----------------------------------------------------------------------------------

        [Test]
        public void RecommendedValues_GiveEveryValueOfTheTable()
        {
            var world = new InstallationWorld();
            world.AddCommunityInstallation(NeoRoot, Product.NeoEE);
            Installation installation = world.Discover().Selected;

            RecommendedValues values = RecommendedValues.For(installation, Game.ArtOfConquest,
                new FakeSystemInfo().WithScreen(2560, 1440), world.FileSystem);

            Assert.That(values.ValueOf(GameSettingsTable.Find("Installed From Directory")),
                Is.EqualTo(RegistryValue.FromString(@"\PROGRAM FILES (X86)\NEO EMPIRE EARTH\Empire Earth - The Art of Conquest\")));
            Assert.That(values.ValueOf(GameSettingsTable.Find("Rasterizer Name")), Is.EqualTo(RegistryValue.FromString("Direct3D Hardware TnL")));
            Assert.That(values.ValueOf(GameSettingsTable.Find("Game Window Width")), Is.EqualTo(RegistryValue.FromDWord(1920)));
            Assert.That(values.ValueOf(GameSettingsTable.Find("Game Window Height")), Is.EqualTo(RegistryValue.FromDWord(1080)));
            Assert.That(values.ValueOf(GameSettingsTable.Find(@"Game Options\Ending Epoch")), Is.EqualTo(RegistryValue.FromDWord(14)));
            Assert.That(values.ValueOf(GameSettingsTable.Find("Music Volume")), Is.EqualTo(RegistryValue.FromDWord(44)));
            foreach (GameSetting setting in GameSettingsTable.All)
                Assert.That(values.ValueOf(setting).Type, Is.EqualTo(setting.Type), setting.Name);
        }

        [Test]
        public void RecommendedValues_WithoutADriveLetterHaveNoClassSValues()
        {
            var world = new InstallationWorld();
            world.AddEmpireEarth(@"\\server\games\Empire Earth");
            Installation installation = world.Discover(@"\\server\games\Empire Earth").Selected;

            RecommendedValues values = RecommendedValues.For(installation, Game.EmpireEarth, new FakeSystemInfo(), world.FileSystem);

            Assert.That(values.InstalledFrom, Is.Null);
            Assert.That(values.ValueOf(GameSettingsTable.Find("Installed From Volume")), Is.Null);
            Assert.That(values.ValueOf(GameSettingsTable.Find("Wait for VSync")), Is.EqualTo(RegistryValue.FromDWord(0)));
        }

        [Test]
        public void Table_ValueNamesPerKey()
        {
            Assert.That(GameSettingsTable.ValueNamesIn(string.Empty), Has.Count.EqualTo(12));
            Assert.That(GameSettingsTable.ValueNamesIn("Game Options"), Has.Count.EqualTo(15));
            Assert.That(GameSettingsTable.ValueNamesIn("game options"), Does.Contain("Ending Epoch"));
            Assert.That(GameSettingsTable.OfClass(SettingClass.S), Has.Count.EqualTo(2));
            Assert.That(GameSettingsTable.OfClass(SettingClass.D), Has.Count.EqualTo(6));
            Assert.That(GameSettingsTable.OfClass(SettingClass.P), Has.Count.EqualTo(19));
            Assert.That(GameSettingsTable.Find("game options\\map type").ValueName, Is.EqualTo("Map Type"));
        }
    }
}

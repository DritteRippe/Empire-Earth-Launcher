using System.Collections.Generic;
using System.Linq;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.GameSettings;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Tests.Fakes;
using Empire_Earth_Launcher.Tests.TestSupport;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.GameSettings
{
    /// <summary>
    /// The consistency checks of contract 3.6 (<see cref="ConsistencyChecker"/>), with the window seen as the game sees it
    /// (ADR 0011 plan review, contract O4): physical pixels with an effective <c>HIGHDPIAWARE</c> in HKCU or HKLM, else the
    /// logical size; at 100 % and 150 %, with and without the layer.
    /// </summary>
    [TestFixture]
    public class ConsistencyChecksTests
    {
        private const string Root = GameSettingsWorld.NeoRoot;
        private const string EeProgram = Root + @"\Empire Earth\Empire Earth.exe";
        private static readonly RegistryLocation NeoEE = GameSettingsWorld.Settings(Product.NeoEE, Game.EmpireEarth);

        private GameSettingsWorld w;

        private void Create(FakeSystemInfo systemInfo = null, string components = "game")
        {
            w = new GameSettingsWorld(systemInfo);
            w.AddAdminInstallationOfAnotherAccount(Root, Product.NeoEE, components);
        }

        private IReadOnlyList<ConsistencyFinding> Check()
        {
            return new ConsistencyChecker(w.Registry, w.FileSystem, w.SystemInfo).Check(w.Discover().Selected);
        }

        private void Seed(string name, int value)
        {
            w.RawRegistry.Seed(NeoEE, name, RegistryValue.FromDWord(value));
        }

        [Test]
        public void NoValues_NoFindings()
        {
            Create();

            Assert.That(Check(), Is.Empty);
        }

        [Test]
        public void BitDepthsDiffer_WhiteMenu()
        {
            Create();
            Seed("Game Bit Depth", 32);
            Seed("Texture Bit Depth", 24);

            ConsistencyFinding finding = Check().Single();

            Assert.That(finding.Code, Is.EqualTo(FindingCode.BitDepthMismatch));
            Assert.That(finding.Game, Is.SameAs(Game.EmpireEarth));
            Assert.That(finding.HintKey, Is.EqualTo(@"BitDepthMismatch HKCU\Software\Neo\Empire Earth"));
            Assert.That(finding.HintValues, Is.EqualTo("Game Bit Depth=32; Texture Bit Depth=24"));
        }

        [Test]
        public void SixteenBit_FromWindows8On()
        {
            Create();
            Seed("Game Bit Depth", 16);
            Seed("Texture Bit Depth", 16);

            Assert.That(Check().Select(f => f.Code), Is.EqualTo(new[] { FindingCode.SixteenBitOnWindows8 }));
        }

        [Test]
        public void SixteenBit_NotOnWindows7()
        {
            Create(FakeSystemInfo.Windows7());
            Seed("Game Bit Depth", 16);
            Seed("Texture Bit Depth", 16);

            Assert.That(Check(), Is.Empty);
        }

        [Test]
        public void SixteenBitAndDifferentDepths_BothFindings()
        {
            Create(FakeSystemInfo.Windows81());
            Seed("Game Bit Depth", 16);
            Seed("Texture Bit Depth", 32);

            Assert.That(Check().Select(f => f.Code), Is.EqualTo(new[] { FindingCode.BitDepthMismatch, FindingCode.SixteenBitOnWindows8 }));
        }

        [TestCase("Direct3D", false, true, "Direct3D Hardware TnL")]
        [TestCase("direct3d hardware tnl", false, false, null)]
        [TestCase("Direct3D Hardware TnL", true, true, "Direct3D")]
        [TestCase("Direct3D", true, false, null)]
        public void Rasterizer_AgainstTheWrapperRule(string current, bool wine, bool expected, string recommended)
        {
            Create(new FakeSystemInfo { IsWine = wine });
            w.RawRegistry.Seed(NeoEE, "Rasterizer Name", RegistryValue.FromString(current));

            IReadOnlyList<ConsistencyFinding> findings = Check();

            Assert.That(findings.Any(f => f.Code == FindingCode.RasterizerMismatch), Is.EqualTo(expected));
            if (expected)
                Assert.That(findings.Single().Recommended, Is.EqualTo(recommended));
        }

        private void Window(int width, int height)
        {
            Seed("Game Window Width", width);
            Seed("Game Window Height", height);
        }

        [Test]
        public void Window_100Percent_Fits()
        {
            Create(new FakeSystemInfo().WithScreen(1920, 1080, 100));
            Window(1920, 1080);

            Assert.That(Check(), Is.Empty);
        }

        /// <summary>O4: at 150 % the game without HIGHDPIAWARE sees 1280x720; the window fits only physically.</summary>
        [Test]
        public void Window_150Percent_WithoutTheLayer_FitsOnlyWithHighDpiAware()
        {
            Create(new FakeSystemInfo().WithScreen(1920, 1080, 150));
            Window(1920, 1080);

            ConsistencyFinding finding = Check().Single();

            Assert.That(finding.Code, Is.EqualTo(FindingCode.WindowFitsOnlyWithHighDpiAware));
            Assert.That(finding.GameScreen, Is.EqualTo(new ScreenSize(1280, 720)));
            Assert.That(finding.Recommended, Is.EqualTo("1920x1080"), "a reset would write the same values");
        }

        [TestCase(@"HKCU", "~ HIGHDPIAWARE")]
        [TestCase(@"HKLM64", "~ DWM8And16BitMitigation HIGHDPIAWARE HeapClearAllocation WIN7RTM")]
        [TestCase(@"HKLM32", "~ RUNASADMIN HIGHDPIAWARE")]
        public void Window_150Percent_WithTheLayerInHkcuOrHklm_Fits(string hive, string layers)
        {
            Create(new FakeSystemInfo().WithScreen(1920, 1080, 150));
            Window(1920, 1080);
            w.RawRegistry.Seed(RegistryLocation.Parse(hive + @"\" + ContractNames.CompatibilityLayersKey), EeProgram,
                RegistryValue.FromString(layers));

            Assert.That(Check(), Is.Empty);
        }

        [TestCase(100, false)]
        [TestCase(150, false)]
        [TestCase(150, true)]
        public void Window_LargerThanThePhysicalScreen(int percent, bool layer)
        {
            Create(new FakeSystemInfo().WithScreen(1920, 1080, percent));
            Window(2560, 1440);
            if (layer)
                w.RawRegistry.Seed(GameSettingsWorld.Layers, EeProgram, RegistryValue.FromString("~ HIGHDPIAWARE"));

            ConsistencyFinding finding = Check().Single();

            Assert.That(finding.Code, Is.EqualTo(FindingCode.WindowLargerThanScreen));
            Assert.That(finding.GameScreen, Is.EqualTo(layer || percent == 100 ? new ScreenSize(1920, 1080) : new ScreenSize(1280, 720)));
            Assert.That(finding.HintValues, Does.Contain("Game Window Width=2560; Game Window Height=1440"));
        }

        [Test]
        public void Window_OtherLayersDoNotCount()
        {
            Create(new FakeSystemInfo().WithScreen(1920, 1080, 150));
            Window(1600, 900);
            w.RawRegistry.Seed(GameSettingsWorld.Layers, EeProgram, RegistryValue.FromString("~ HeapClearAllocation"));

            Assert.That(Check().Single().Code, Is.EqualTo(FindingCode.WindowFitsOnlyWithHighDpiAware));
        }

        [Test]
        public void ScreenLowerThan768_R13()
        {
            Create(new FakeSystemInfo().WithScreen(1024, 600));

            ConsistencyFinding finding = Check().Single();

            Assert.That(finding.Code, Is.EqualTo(FindingCode.ScreenTooLow));
            Assert.That(finding.Game, Is.Null);
            Assert.That(finding.HintKey, Is.EqualTo("ScreenTooLow screen"));
        }

        [Test]
        public void NetworkPath_HasAFinding()
        {
            w = new GameSettingsWorld();
            w.World.AddEmpireEarth(@"\\server\games\Empire Earth");
            Installation installation = w.Discover(@"\\server\games\Empire Earth").Selected;

            IReadOnlyList<ConsistencyFinding> findings = new ConsistencyChecker(w.Registry, w.FileSystem, w.SystemInfo).Check(installation);

            Assert.That(findings.Select(f => f.Code), Is.EqualTo(new[] { FindingCode.InstalledFromNotOnADrive }));
        }

        [Test]
        public void TheChecksOnlyRead()
        {
            Create(new FakeSystemInfo().WithScreen(1920, 1080, 150));
            Window(2560, 1440);
            Seed("Game Bit Depth", 16);

            new ConsistencyChecker(new WriteForbiddingRegistry(w.RawRegistry), w.FileSystem, w.SystemInfo).Check(w.Discover().Selected);

            Assert.That(w.Changes, Is.Empty);
        }
    }
}

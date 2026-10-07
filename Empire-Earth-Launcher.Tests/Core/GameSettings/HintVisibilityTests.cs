using System.Linq;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.GameSettings;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Core.Settings;
using Empire_Earth_Launcher.Tests.TestSupport;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.GameSettings
{
    /// <summary>
    /// Hiding hints per value and content (ADR 0015): stored in <c>settings.json</c>, shown again when a value changes.
    /// </summary>
    [TestFixture]
    public class HintVisibilityTests
    {
        private static readonly RegistryLocation NeoEE = GameSettingsWorld.Settings(Product.NeoEE, Game.EmpireEarth);

        private GameSettingsWorld w;

        [SetUp]
        public void SetUp()
        {
            w = new GameSettingsWorld();
            w.AddAdminInstallationOfAnotherAccount(GameSettingsWorld.NeoRoot, Product.NeoEE, "game");
        }

        private ConsistencyFinding SixteenBit(int gameBits)
        {
            w.RawRegistry.Seed(NeoEE, "Game Bit Depth", RegistryValue.FromDWord(gameBits));
            w.RawRegistry.Seed(NeoEE, "Texture Bit Depth", RegistryValue.FromDWord(16));
            return new ConsistencyChecker(w.Registry, w.FileSystem, w.SystemInfo).Check(w.Discover().Selected)
                                                                               .Single(f => f.Code == FindingCode.SixteenBitOnWindows8);
        }

        [Test]
        public void AHiddenHint_StaysHiddenWhileItsValuesStayTheSame()
        {
            var settings = new LauncherSettings();
            HintVisibility.Hide(settings, SixteenBit(16));

            Assert.That(HintVisibility.IsHidden(settings, SixteenBit(16)), Is.True);
            Assert.That(HintVisibility.Visible(settings, new[] { SixteenBit(16) }), Is.Empty);
        }

        [Test]
        public void AChangedValue_ShowsTheHintAgain()
        {
            var settings = new LauncherSettings();
            HintVisibility.Hide(settings, SixteenBit(16));

            Assert.That(HintVisibility.IsHidden(settings, SixteenBit(32)), Is.False);
        }

        [Test]
        public void HidingAgain_ReplacesTheOldEntry_ShowRemovesIt()
        {
            var settings = new LauncherSettings();
            HintVisibility.Hide(settings, SixteenBit(16));
            HintVisibility.Hide(settings, SixteenBit(32));

            Assert.That(settings.HiddenHints, Has.Count.EqualTo(1));
            HintVisibility.Show(settings, SixteenBit(32));
            Assert.That(settings.HiddenHints, Is.Empty);
        }

        [Test]
        public void HiddenHints_AreSavedInSettingsJson()
        {
            const string file = @"C:\Users\Player\AppData\Local\Empire Earth Launcher\settings.json";
            var store = new SettingsStore(w.FileSystem, file, w.Logger);
            HintVisibility.Hide(store.Current, SixteenBit(16));
            store.Save();

            var reloaded = new SettingsStore(w.FileSystem, file, w.Logger);
            reloaded.Load();

            Assert.That(w.FileSystem.GetText(file), Does.Contain("\"HiddenHints\"").And.Contain("SixteenBitOnWindows8"));
            Assert.That(HintVisibility.IsHidden(reloaded.Current, SixteenBit(16)), Is.True);
            Assert.That(HintVisibility.IsHidden(reloaded.Current, SixteenBit(32)), Is.False);
        }

        [Test]
        public void ASettingsFileWithoutHiddenHints_HasAnEmptyList()
        {
            const string file = @"C:\settings.json";
            w.FileSystem.AddFile(file, "{\"SchemaVersion\":1,\"HiddenHints\":null}");
            var store = new SettingsStore(w.FileSystem, file, w.Logger);

            store.Load();

            Assert.That(store.Current.HiddenHints, Is.Empty);
        }
    }
}

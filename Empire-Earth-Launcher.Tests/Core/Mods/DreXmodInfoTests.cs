using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Mods;
using Empire_Earth_Launcher.Tests.TestSupport;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.Mods
{
    /// <summary>
    /// <see cref="DreXmodInfo"/>: which dreXmod the setup installed, from the components of <c>install.ini</c>; only for an
    /// installation without component information the folder of the presets decides. The Mods page exists for dreXmod 3 only.
    /// </summary>
    [TestFixture]
    public class DreXmodInfoTests
    {
        private const string Root = GameSettingsWorld.NeoRoot;

        private GameSettingsWorld w;

        [SetUp]
        public void SetUp()
        {
            w = new GameSettingsWorld();
        }

        private DreXmodVersion Describe(string components)
        {
            w.AddAdminInstallationOfAnotherAccount(Root, Product.NeoEE, components);
            return DreXmodInfo.Describe(w.Discover().Selected, w.FileSystem);
        }

        [Test]
        public void TheVersion3Component_IsDreXmod3()
        {
            Assert.That(Describe(@"game,gameaoc,additional,additional\drexmod,additional\drexmod\v3"), Is.EqualTo(DreXmodVersion.Version3));
        }

        [Test]
        public void TheVersion2Component_IsDreXmod2_WhichHasNoModSystem()
        {
            Assert.That(Describe(@"game,gameaoc,additional,additional\drexmod,additional\drexmod\v2"), Is.EqualTo(DreXmodVersion.Version2));
        }

        [TestCase("game,gameaoc")]
        [TestCase(@"game,gameaoc,additional,additional\directx_wrapper,additional\directx_wrapper\dx11_lvl10_1")]
        [TestCase(@"game,additional,additional\drexmod")]
        public void WithoutAVersionComponent_ThereIsNoDreXmod(string components)
        {
            Assert.That(Describe(components), Is.EqualTo(DreXmodVersion.None));
        }

        [Test]
        public void TheComponents_AreComparedIgnoringCase()
        {
            Assert.That(Describe(@"game,Additional\DreXmod\V3"), Is.EqualTo(DreXmodVersion.Version3));
        }

        [Test]
        public void TheComponents_DecideEvenIfAFolderOfPresetsExists()
        {
            w.FileSystem.AddDirectory(Root + @"\Empire Earth\Data\dxm\mods\yukon");

            Assert.That(Describe(@"game,gameaoc"), Is.EqualTo(DreXmodVersion.None), "the setup says there is no dreXmod 3");
        }
    }
}

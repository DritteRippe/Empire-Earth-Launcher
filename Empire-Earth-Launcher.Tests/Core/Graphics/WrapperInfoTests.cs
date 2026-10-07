using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Graphics;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Tests.TestSupport;
using Microsoft.Win32;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.Graphics
{
    /// <summary>
    /// <see cref="WrapperInfo"/>: which DirectX wrapper the setup installed, from the components of <c>install.ini</c>, else of
    /// the uninstall key, and only without component information from the wrapper files (contract 3.3). The launcher shows it
    /// and never changes it.
    /// </summary>
    [TestFixture]
    public class WrapperInfoTests
    {
        private const string Root = GameSettingsWorld.NeoRoot;
        private const string Wrapper = @"additional\directx_wrapper";

        private GameSettingsWorld w;

        [SetUp]
        public void SetUp()
        {
            w = new GameSettingsWorld();
        }

        private WrapperInfo Describe(string components)
        {
            w.AddAdminInstallationOfAnotherAccount(Root, Product.NeoEE, components);
            return WrapperInfo.Describe(w.Discover().Selected, w.FileSystem);
        }

        [TestCase("dx11_lvl10", "11", "10")]
        [TestCase("dx11_lvl10_1", "11", "10.1")]
        [TestCase("dx11_lvl11", "11", "11")]
        [TestCase("dx12_lvl11", "12", "11")]
        [TestCase("dx12_lvl12", "12", "12")]
        public void ADgVoodooComponent_NamesTheDirectXVersionAndTheApiLevel(string component, string version, string level)
        {
            WrapperInfo info = Describe("game,gameaoc," + Wrapper + "," + Wrapper + @"\" + component);

            Assert.That(info.Kind, Is.EqualTo(WrapperKind.DgVoodoo));
            Assert.That(info.Source, Is.EqualTo(WrapperSource.InstallInfo));
            Assert.That(info.Component, Is.EqualTo(component));
            Assert.That(info.DirectXVersion, Is.EqualTo(version));
            Assert.That(info.ApiLevel, Is.EqualTo(level));
            Assert.That(info.IsInstalled, Is.True);
        }

        [Test]
        public void TheDirectX7Component_IsDDrawCompat()
        {
            WrapperInfo info = Describe("game," + Wrapper + @"\dx7");

            Assert.That(info.Kind, Is.EqualTo(WrapperKind.DirectX7));
            Assert.That(info.ApiLevel, Is.Null);
        }

        [Test]
        public void TheDirectX9Component_IsTheOtherWrapper()
        {
            Assert.That(Describe("game," + Wrapper + @"\dx9").Kind, Is.EqualTo(WrapperKind.DirectX9));
        }

        [Test]
        public void WithoutAWrapperComponent_NothingIsInstalled_Native()
        {
            WrapperInfo info = Describe("game,gameaoc,additional\\rms");

            Assert.That(info.Kind, Is.EqualTo(WrapperKind.None));
            Assert.That(info.IsInstalled, Is.False);
            Assert.That(info.Source, Is.EqualTo(WrapperSource.InstallInfo));
        }

        [Test]
        public void AComponentThisLauncherDoesNotKnow_IsOther_WithItsName()
        {
            WrapperInfo info = Describe("game," + Wrapper + @"\vulkan_future");

            Assert.That(info.Kind, Is.EqualTo(WrapperKind.Other));
            Assert.That(info.Component, Is.EqualTo("vulkan_future"));
        }

        [Test]
        public void TheWrapperComponentWithoutAVariant_IsOther()
        {
            WrapperInfo info = Describe("game," + Wrapper);

            Assert.That(info.Kind, Is.EqualTo(WrapperKind.Other));
            Assert.That(info.Component, Is.Null);
        }

        [Test]
        public void ComponentNamesIgnoreCase()
        {
            WrapperInfo info = Describe("game,Additional\\DirectX_Wrapper\\DX11_LVL10_1");

            Assert.That(info.Kind, Is.EqualTo(WrapperKind.DgVoodoo));
            Assert.That(info.ApiLevel, Is.EqualTo("10.1"));
        }

        [Test]
        public void WithoutComponentInformation_TheWrapperFilesDecide()
        {
            w.World.AddEmpireEarth(@"C:\Games\EE");
            w.FileSystem.AddFile(@"C:\Games\EE\DDraw.dll", "x");

            WrapperInfo info = WrapperInfo.Describe(w.Discover(@"C:\Games\EE").Selected, w.FileSystem);

            Assert.That(info.Kind, Is.EqualTo(WrapperKind.Other));
            Assert.That(info.Source, Is.EqualTo(WrapperSource.GameFolder));
            Assert.That(info.WrapperFile, Is.EqualTo("DDraw.dll"));
            Assert.That(info.Component, Is.Null);
        }

        [Test]
        public void WithoutComponentInformationAndWithoutFiles_NothingIsInstalled()
        {
            w.World.AddEmpireEarth(@"C:\Games\EE");

            WrapperInfo info = WrapperInfo.Describe(w.Discover(@"C:\Games\EE").Selected, w.FileSystem);

            Assert.That(info.Kind, Is.EqualTo(WrapperKind.None));
            Assert.That(info.Source, Is.EqualTo(WrapperSource.GameFolder));
        }

        [Test]
        public void ASetupUpTo172_HasItsComponentsInTheUninstallKey()
        {
            w.World.AddCommunityFiles(Root, Product.NeoEE, false);
            w.World.AddUninstallKey(RegistryHive.LocalMachine, RegistryView.Registry64, Product.NeoEE, Root,
                components: "game," + Wrapper + @"\dx11_lvl11");

            WrapperInfo info = WrapperInfo.Describe(w.Discover().Selected, w.FileSystem);

            Assert.That(info.Kind, Is.EqualTo(WrapperKind.DgVoodoo));
            Assert.That(info.Source, Is.EqualTo(WrapperSource.UninstallKey));
            Assert.That(info.ApiLevel, Is.EqualTo("11"));
        }
    }
}

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Play;
using Empire_Earth_Launcher.Core.Settings;
using Empire_Earth_Launcher.Tests.Fakes;
using Empire_Earth_Launcher.Tests.TestSupport;
using Microsoft.Win32;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Launcher
{
    /// <summary>
    /// <see cref="LauncherInstanceTarget"/>: what the running launcher does with the product of a second launcher (contract 1.4,
    /// revision 4), through <see cref="InstanceReceiver"/> and the real <see cref="InstallationService"/>; no window.
    /// </summary>
    [TestFixture]
    public class LauncherInstanceTargetTests
    {
        private const string SettingsFolder = @"C:\Users\Player\AppData\Local\Empire Earth Launcher";
        private const string NeoRoot = @"C:\Program Files (x86)\Neo Empire Earth";
        private const string RetailFolder = @"C:\Games\EE";

        private InstallationWorld world;
        private InstallationService installations;
        private bool starting;
        private List<string> calls;
        private InstanceReceiver receiver;

        [SetUp]
        public async Task SetUp()
        {
            starting = false;
            world = new InstallationWorld();
            world.AddCommunityInstallation(NeoRoot, Product.NeoEE);
            world.AddForeignInstallation(RetailFolder, RegistryHive.LocalMachine, RegistryView.Registry32);
            world.FileSystem.AddDirectory(SettingsFolder);
            var settings = new SettingsStore(world.FileSystem, SettingsFolder + @"\settings.json", world.Logger);
            settings.Load();
            installations = new InstallationService(world.Logger, settings, world.CreateDiscovery(), world.FileSystem, null);
            await installations.RefreshAsync();
            calls = new List<string>();
            var target = new LauncherInstanceTarget(installations, () => starting, () => calls.Add("front"), world.Logger);
            receiver = new InstanceReceiver(target, world.Logger);
        }

        private bool Handle(Product product)
        {
            return receiver.Handle(InstanceMessage.ToBytes(InstanceMessage.Encode(product)));
        }

        [Test]
        public void WhenIdle_TheWindowComesToTheFront_AndTheProductIsSelected()
        {
            Assert.That(installations.Selected.Product, Is.SameAs(Product.NeoEE));

            Assert.That(Handle(Product.EE), Is.True);

            Assert.That(calls, Is.EqualTo(new[] { "front" }));
            Assert.That(installations.Selected.Product, Is.SameAs(Product.EE));
            Assert.That(installations.SessionProduct, Is.SameAs(Product.EE));
        }

        [Test]
        public void DuringAGameStart_TheSelectionStays_ButTheWindowComesToTheFront()
        {
            starting = true;

            Assert.That(Handle(Product.EE), Is.True);

            Assert.That(calls, Is.EqualTo(new[] { "front" }));
            Assert.That(installations.Selected.Product, Is.SameAs(Product.NeoEE), "the start would otherwise run for another installation");
            Assert.That(installations.SessionProduct, Is.Null);
        }

        [Test]
        public void AProductWithoutAnInstallation_ChangesNothing()
        {
            var settings = new SettingsStore(world.FileSystem, SettingsFolder + @"\settings.json", world.Logger);
            world = new InstallationWorld();
            world.AddCommunityInstallation(NeoRoot, Product.NeoEE);
            world.FileSystem.AddDirectory(SettingsFolder);
            installations = new InstallationService(world.Logger, settings, world.CreateDiscovery(), world.FileSystem, null);
            installations.RefreshAsync().Wait();
            var target = new LauncherInstanceTarget(installations, () => false, () => calls.Add("front"), world.Logger);

            Assert.That(new InstanceReceiver(target, world.Logger).Handle(InstanceMessage.ToBytes("product=EE")), Is.True);

            Assert.That(installations.Selected.Product, Is.SameAs(Product.NeoEE));
        }

        [Test]
        public void TheSelection_IsNotSaved()
        {
            Handle(Product.EE);

            Assert.That(world.FileSystem.FileExists(SettingsFolder + @"\settings.json"), Is.False, "nothing is saved");
        }

        [Test]
        public void IsIdle_FollowsTheGameStart()
        {
            var target = new LauncherInstanceTarget(installations, () => starting, () => { }, world.Logger);

            Assert.That(target.IsIdle, Is.True);
            starting = true;
            Assert.That(target.IsIdle, Is.False);
        }

        [Test]
        public void Arguments_AreChecked()
        {
            Assert.That(() => new LauncherInstanceTarget(null, () => false, () => { }, world.Logger), Throws.ArgumentNullException);
            Assert.That(() => new LauncherInstanceTarget(installations, null, () => { }, world.Logger), Throws.ArgumentNullException);
            Assert.That(() => new LauncherInstanceTarget(installations, () => false, null, world.Logger), Throws.ArgumentNullException);
            Assert.That(() => new LauncherInstanceTarget(installations, () => false, () => { }, null), Throws.ArgumentNullException);
        }
    }
}

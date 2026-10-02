using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Logging;
using Empire_Earth_Launcher.Core.Settings;
using Empire_Earth_Launcher.Tests.Core.Settings;
using Empire_Earth_Launcher.Tests.Fakes;
using Empire_Earth_Launcher.Tests.TestSupport;
using Microsoft.Win32;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Launcher
{
    /// <summary>
    /// <see cref="InstallationService"/>: the discovery with the folder of settings.json as user choice, the choice of an
    /// installation of the list (saved as source 1), Auto-detect, and the Changed event the pages listen to.
    /// </summary>
    [TestFixture]
    public class InstallationServiceTests
    {
        private const string SettingsFolder = @"C:\Users\Player\AppData\Local\Empire Earth Launcher";
        private const string NeoRoot = @"C:\Program Files (x86)\Neo Empire Earth";
        private const string RetailFolder = @"C:\Games\EE";

        private InstallationWorld world;
        private SettingsStore settings;
        private InstallationService service;
        private List<string> events;

        [SetUp]
        public void SetUp()
        {
            world = new InstallationWorld();
            world.AddCommunityInstallation(NeoRoot, Product.NeoEE);
            world.AddForeignInstallation(RetailFolder, RegistryHive.LocalMachine, RegistryView.Registry32);
            world.FileSystem.AddDirectory(SettingsFolder);
            settings = new SettingsStore(world.FileSystem, SettingsFolder + @"\settings.json", world.Logger);
            settings.Load();
            service = new InstallationService(world.Logger, settings, world.CreateDiscovery(), world.FileSystem, null);
            events = new List<string>();
            service.Changed += (sender, e) => events.Add(service.IsSearching ? "searching" : "done");
        }

        private string SavedChoice()
        {
            return new SettingsStore(world.FileSystem, SettingsFolder + @"\settings.json", world.Logger).LoadAndGet().GameDirectory;
        }

        [Test]
        public void BeforeTheFirstDiscovery_NothingIsKnown()
        {
            Assert.That(service.Result, Is.Null);
            Assert.That(service.Selected, Is.Null);
            Assert.That(service.IsSearching, Is.False);
        }

        [Test]
        public async Task Refresh_FindsTheInstallations_AndRaisesChangedTwice()
        {
            await service.RefreshAsync();

            Assert.That(service.Result.Installations, Has.Count.EqualTo(2));
            Assert.That(service.Selected.Root, Is.EqualTo(NeoRoot));
            Assert.That(service.IsSearching, Is.False);
            Assert.That(events, Is.EqualTo(new[] { "searching", "done" }));
            Assert.That(world.LogLinesAbout("Empire Earth folder: " + NeoRoot + @"\Empire Earth (source 2)"), Has.Length.EqualTo(1));
        }

        [Test]
        public async Task Select_SavesTheEEFolderAsTheUsersChoice()
        {
            await service.RefreshAsync();
            Installation retail = service.Result.Installations.Single(installation => installation.Kind == InstallationKind.Foreign);

            await service.SelectAsync(retail);

            Assert.That(SavedChoice(), Is.EqualTo(retail.EeFolder), "the EE folder, which keeps a foreign folder name");
            Assert.That(service.Selected.Root, Is.EqualTo(retail.Root));
            Assert.That(service.Result.IsSelectedByUser, Is.True);
            Assert.That(service.Result.Installations[0], Is.SameAs(service.Selected));
        }

        [Test]
        public async Task ChooseFolder_TrimsAndSaves_AndAutoDetectClearsIt()
        {
            await service.ChooseFolderAsync("  " + NeoRoot + "  ");

            Assert.That(SavedChoice(), Is.EqualTo(NeoRoot));
            Assert.That(service.Result.IsSelectedByUser, Is.True);

            await service.UseAutomaticDetectionAsync();

            Assert.That(SavedChoice(), Is.Empty);
            Assert.That(service.Result.IsSelectedByUser, Is.False);
            Assert.That(service.Selected.Root, Is.EqualTo(NeoRoot), "the first one found");
        }

        [Test]
        public async Task NothingFound_IsLoggedAsWarning()
        {
            world = new InstallationWorld();
            world.FileSystem.AddDirectory(SettingsFolder);
            settings = new SettingsStore(world.FileSystem, SettingsFolder + @"\settings.json", world.Logger);
            service = new InstallationService(world.Logger, settings, world.CreateDiscovery(), world.FileSystem, null);

            await service.RefreshAsync();

            Assert.That(service.Selected, Is.Null);
            Assert.That(world.Logger.MessagesOf(LogLevel.Warning),
                Does.Contain("No Empire Earth installation found. Choose the game folder in the launcher settings."));
        }

        [Test]
        public async Task OnlyTheLatestRefresh_SetsTheResult()
        {
            using (var blocking = new BlockingRegistry(world.Registry))
            {
                var slow = new InstallationService(world.Logger, settings,
                    new InstallationDiscovery(blocking, world.FileSystem, world.Logger), world.FileSystem, null);
                Task first = slow.RefreshAsync();
                Assert.That(blocking.Entered.WaitOne(TimeSpan.FromSeconds(10)), Is.True);
                settings.Current.GameDirectory = RetailFolder;
                Task second = slow.RefreshAsync();
                Assert.That(slow.IsSearching, Is.True);

                blocking.Release();
                await Task.WhenAll(first, second);

                Assert.That(slow.Selected.EeFolder, Is.EqualTo(RetailFolder).IgnoreCase, "the result of the second refresh");
                Assert.That(slow.IsSearching, Is.False);
            }
        }

        [Test]
        public void ClassifyFolder_JudgesTheFolderTheUserPicked()
        {
            Assert.That(service.ClassifyFolder(NeoRoot), Is.EqualTo(GameFolderKind.InstallRoot));
            Assert.That(service.ClassifyFolder(RetailFolder), Is.EqualTo(GameFolderKind.EmpireEarthFolder));
            Assert.That(service.ClassifyFolder(SettingsFolder), Is.EqualTo(GameFolderKind.None));
        }

        [Test]
        public void Arguments_AreChecked()
        {
            InstallationDiscovery discovery = world.CreateDiscovery();
            Assert.That(() => new InstallationService(null, settings, discovery, world.FileSystem, null), Throws.ArgumentNullException);
            Assert.That(() => new InstallationService(world.Logger, null, discovery, world.FileSystem, null), Throws.ArgumentNullException);
            Assert.That(() => new InstallationService(world.Logger, settings, null, world.FileSystem, null), Throws.ArgumentNullException);
            Assert.That(() => new InstallationService(world.Logger, settings, discovery, null, null), Throws.ArgumentNullException);
            Assert.That(() => service.SelectAsync(null), Throws.ArgumentNullException);
        }
    }
}

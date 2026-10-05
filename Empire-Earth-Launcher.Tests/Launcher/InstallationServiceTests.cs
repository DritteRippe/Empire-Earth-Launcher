using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Logging;
using Empire_Earth_Launcher.Core.Play;
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

        // --- A running setup (contract 4.2, L-WP6) ---------------------------------------------------------------------

        private FakeMutexProbe mutexes;
        private SetupWatcher watcher;

        private void UseSetupWatcher(params string[] runningMutexes)
        {
            mutexes = new FakeMutexProbe().With(runningMutexes);
            watcher = new SetupWatcher(mutexes, world.Clock, world.Logger);
            service = new InstallationService(world.Logger, settings, world.CreateDiscovery(), world.FileSystem, null, watcher);
            events.Clear();
            service.Changed += (sender, e) =>
                events.Add(service.IsWaitingForSetup ? "waiting" : service.IsSearching ? "searching" : "done");
        }

        private void EndSetup(string mutex)
        {
            mutexes.Remove(mutex);
            world.Clock.Advance(SetupWatcher.Interval);
            watcher.Tick();
        }

        [Test]
        public async Task Contract_4_2_WhileASetupRuns_TheSearchWaits_AndRunsWhenTheSetupHasEnded()
        {
            UseSetupWatcher("NeoEE_Setup");

            await service.RefreshAsync();

            Assert.That(service.Result, Is.Null);
            Assert.That(service.IsWaitingForSetup, Is.True);
            Assert.That(world.FileSystem.OpenCount(InstallationWorld.InstallInfoPath(NeoRoot, Product.NeoEE)), Is.Zero,
                "install.ini is not read while a setup runs");
            Assert.That(world.LogLinesAbout("searched when the NeoEE setup has ended"), Has.Length.EqualTo(1));

            EndSetup("NeoEE_Setup");
            Assert.That(service.RefreshAfterSetup, Is.Not.Null, "the end of the setup starts the search");
            await service.RefreshAfterSetup;

            Assert.That(service.IsWaitingForSetup, Is.False);
            Assert.That(service.Selected.Root, Is.EqualTo(NeoRoot));
            Assert.That(world.FileSystem.OpenCount(InstallationWorld.InstallInfoPath(NeoRoot, Product.NeoEE)), Is.GreaterThan(0));
            Assert.That(events, Is.EqualTo(new[] { "waiting", "searching", "done" }));
        }

        [Test]
        public async Task ASetupThatStartsLater_KeepsThePreviousResult_UntilItHasEnded()
        {
            UseSetupWatcher();
            await service.RefreshAsync();
            DiscoveryResult before = service.Result;
            mutexes.With("EE_Setup");
            world.Clock.Advance(SetupWatcher.Interval);
            watcher.Tick();

            await service.ChooseFolderAsync(RetailFolder);

            Assert.That(service.Result, Is.SameAs(before), "the choice is saved, the search waits");
            Assert.That(service.IsWaitingForSetup, Is.True);
            Assert.That(SavedChoice(), Is.EqualTo(RetailFolder));

            EndSetup("EE_Setup");
            await service.RefreshAfterSetup;

            Assert.That(service.Selected.EeFolder, Is.EqualTo(RetailFolder).IgnoreCase);
        }

        // --- --product=EE|NeoEE for one session (contract 1.4, revision 4) ---------------------------------------------

        [Test]
        public async Task SessionProduct_EE_SelectsTheFirstEEInstallation_AndSavesNothing()
        {
            Assert.That(service.SelectProductForSession(Product.EE), Is.False, "no result yet: remembered for the search");
            Assert.That(service.SessionProduct, Is.SameAs(Product.EE));

            await service.RefreshAsync();

            Assert.That(service.Selected.Product, Is.SameAs(Product.EE));
            Assert.That(service.Selected.EeFolder, Is.EqualTo(RetailFolder).IgnoreCase);
            Assert.That(service.Result.IsSelectedByUser, Is.False);
            Assert.That(service.Result.Installations, Has.Count.EqualTo(2), "the list and its order stay");
            Assert.That(service.Result.Installations[0].Root, Is.EqualTo(NeoRoot));
            Assert.That(world.FileSystem.FileExists(SettingsFolder + @"\settings.json"), Is.False, "nothing is saved");
            Assert.That(settings.Current.GameDirectory, Is.Empty.Or.Null);
            Assert.That(world.LogLinesAbout("for this session (--product=EE; nothing is saved)"), Has.Length.EqualTo(1));
        }

        [Test]
        public async Task SessionProduct_OfTheSelectedProduct_ChangesNothing()
        {
            service.SelectProductForSession(Product.NeoEE);

            await service.RefreshAsync();

            Assert.That(service.Selected.Root, Is.EqualTo(NeoRoot));
            Assert.That(world.LogLinesAbout("for this session"), Is.Empty);
            Assert.That(world.Logger.MessagesOf(LogLevel.Warning), Is.Empty);
        }

        [Test]
        public async Task SessionProduct_WithoutAnInstallationOfIt_IsIgnored_AndLogged()
        {
            world = new InstallationWorld();
            world.AddCommunityInstallation(NeoRoot, Product.NeoEE);
            world.FileSystem.AddDirectory(SettingsFolder);
            settings = new SettingsStore(world.FileSystem, SettingsFolder + @"\settings.json", world.Logger);
            service = new InstallationService(world.Logger, settings, world.CreateDiscovery(), world.FileSystem, null);
            service.SelectProductForSession(Product.EE);

            await service.RefreshAsync();

            Assert.That(service.Selected.Root, Is.EqualTo(NeoRoot), "the usual selection applies");
            Assert.That(world.Logger.MessagesOf(LogLevel.Warning),
                Does.Contain("No EE installation was found; --product=EE is ignored and the usual selection applies."));
        }

        [Test]
        public async Task SessionProduct_ComesBeforeTheUsersSavedChoiceOfAnotherProduct()
        {
            settings.Current.GameDirectory = RetailFolder;
            service.SelectProductForSession(Product.NeoEE);

            await service.RefreshAsync();

            Assert.That(service.Selected.Root, Is.EqualTo(NeoRoot));
            Assert.That(service.Result.IsSelectedByUser, Is.False);
            Assert.That(settings.Current.GameDirectory, Is.EqualTo(RetailFolder), "the saved choice stays as it was");
        }

        [Test]
        public async Task SessionProduct_TheUsersChoiceOfThatProduct_StaysTheUsersChoice()
        {
            settings.Current.GameDirectory = RetailFolder;
            service.SelectProductForSession(Product.EE);

            await service.RefreshAsync();

            Assert.That(service.Selected.EeFolder, Is.EqualTo(RetailFolder).IgnoreCase);
            Assert.That(service.Result.IsSelectedByUser, Is.True);
        }

        [Test]
        public async Task SessionProduct_SwitchedWhileRunning_RaisesChanged_AndIsAppliedByEveryLaterSearch()
        {
            await service.RefreshAsync();
            Assert.That(service.Selected.Product, Is.SameAs(Product.NeoEE));
            events.Clear();

            bool selected = service.SelectProductForSession(Product.EE);

            Assert.That(selected, Is.True);
            Assert.That(service.Selected.Product, Is.SameAs(Product.EE));
            Assert.That(events, Is.EqualTo(new[] { "done" }), "the pages hear of it");
            Assert.That(world.FileSystem.FileExists(SettingsFolder + @"\settings.json"), Is.False, "nothing is saved");

            await service.RefreshAsync();

            Assert.That(service.Selected.Product, Is.SameAs(Product.EE), "also after the next search, e.g. when a setup has ended");
        }

        [Test]
        public async Task SessionProduct_TheSameProductAgain_RaisesNoChange()
        {
            await service.RefreshAsync();
            service.SelectProductForSession(Product.NeoEE);
            events.Clear();

            Assert.That(service.SelectProductForSession(Product.NeoEE), Is.True);

            Assert.That(events, Is.Empty);
        }

        [Test]
        public async Task SessionProduct_TheUsersOwnChoice_Wins_AndEndsIt()
        {
            await service.RefreshAsync();
            service.SelectProductForSession(Product.EE);
            Installation neo = service.Result.Installations.Single(installation => installation.Product == Product.NeoEE);

            await service.SelectAsync(neo);

            Assert.That(service.SessionProduct, Is.Null);
            Assert.That(service.Selected.Root, Is.EqualTo(NeoRoot));
            Assert.That(world.LogLinesAbout("The choice of the user replaces the product of the command line (EE)"), Has.Length.EqualTo(1));
            await service.RefreshAsync();
            Assert.That(service.Selected.Root, Is.EqualTo(NeoRoot), "the session product does not come back");
        }

        [Test]
        public void SessionProduct_ChecksItsArgument()
        {
            Assert.That(() => service.SelectProductForSession(null), Throws.ArgumentNullException);
        }

        // --- The suite is a setup (contract 4.2, revision 4) -------------------------------------------------------------

        [Test]
        public async Task Contract_4_2_WhileTheSuiteRuns_TheSearchWaits_EvenBetweenTwoProductSetups()
        {
            UseSetupWatcher("EmpireEarthCommunity_Suite", "NeoEE_Setup");

            await service.RefreshAsync();

            Assert.That(service.Result, Is.Null);
            Assert.That(service.IsWaitingForSetup, Is.True);

            EndSetup("NeoEE_Setup");
            await service.RefreshAsync();

            Assert.That(service.IsWaitingForSetup, Is.True, "no product mutex, but the suite still runs");
            Assert.That(service.Result, Is.Null);
            Assert.That(world.FileSystem.OpenCount(InstallationWorld.InstallInfoPath(NeoRoot, Product.NeoEE)), Is.Zero,
                "install.ini is not read in the gap between the two product setups");
            Assert.That(service.RefreshAfterSetup, Is.Null, "the setup did not end");

            EndSetup("EmpireEarthCommunity_Suite");
            Assert.That(service.RefreshAfterSetup, Is.Not.Null, "the end of the suite starts the search");
            await service.RefreshAfterSetup;

            Assert.That(service.IsWaitingForSetup, Is.False);
            Assert.That(service.Selected.Root, Is.EqualTo(NeoRoot));
        }

        [Test]
        public async Task ASetupThatEndedBetweenTwoTicks_IsSearchedOnce()
        {
            UseSetupWatcher("EE_Setup");
            watcher.Tick();
            mutexes.Remove("EE_Setup");

            await service.RefreshAsync();

            Assert.That(service.RefreshAfterSetup, Is.Null, "the refresh that saw the end goes on by itself");
            Assert.That(service.Result, Is.Not.Null);
            Assert.That(events, Is.EqualTo(new[] { "searching", "done" }));
        }
    }
}

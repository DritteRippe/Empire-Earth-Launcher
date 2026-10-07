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

        // --- One folder per product and the product chosen last (contract 1.4, revision 6) ----------------------------------

        private CountingFileSystem counting;

        /// <summary>The service with a settings store on a file system that counts the saves.</summary>
        private void UseCountingSettings()
        {
            counting = new CountingFileSystem(world.FileSystem);
            settings = new SettingsStore(counting, SettingsFolder + @"\settings.json", world.Logger);
            settings.Load();
            service = new InstallationService(world.Logger, settings, world.CreateDiscovery(), world.FileSystem, null);
            events.Clear();
            service.Changed += (sender, e) => events.Add(service.IsSearching ? "searching" : "done");
        }

        private LauncherSettings Saved()
        {
            return new SettingsStore(world.FileSystem, SettingsFolder + @"\settings.json", world.Logger).LoadAndGet();
        }

        private int Discoveries()
        {
            return world.LogLinesAbout("installation(s) found").Length;
        }

        [Test]
        public async Task SelectProduct_SelectsTheInstallationOfTheProductForEveryPage_SavesOnce_AndRunsNoDiscovery()
        {
            UseCountingSettings();
            await service.RefreshAsync();
            Assert.That(service.Selected.Product, Is.SameAs(Product.NeoEE), "the default selection");
            int discoveries = Discoveries();
            events.Clear();

            bool selected = service.SelectProduct(Product.EE);

            Assert.That(selected, Is.True);
            Assert.That(service.Selected.Product, Is.SameAs(Product.EE));
            Assert.That(service.Selected.EeFolder, Is.EqualTo(RetailFolder).IgnoreCase);
            Assert.That(events, Is.EqualTo(new[] { "done" }), "the pages hear of it once");
            Assert.That(Discoveries(), Is.EqualTo(discoveries), "the installations are known: no discovery");
            Assert.That(counting.Saves(SettingsFolder + @"\settings.json"), Is.EqualTo(1));
            Assert.That(Saved().LastProduct, Is.EqualTo("EE"));
            Assert.That(Saved().GameDirectory, Is.Empty, "no folder is chosen for EE: nothing for launcher 1.0.0 to select");
            Assert.That(world.LogLinesAbout("Selection: EE installation"), Has.Length.EqualTo(1));
        }

        [Test]
        public async Task SelectProduct_TheProductAlreadySelected_RaisesNoChange_ButRemembersIt()
        {
            await service.RefreshAsync();
            events.Clear();

            Assert.That(service.SelectProduct(Product.NeoEE), Is.True);

            Assert.That(events, Is.Empty);
            Assert.That(Saved().LastProduct, Is.EqualTo("NeoEE"), "the click is a choice, also if it changes nothing on the screen");
        }

        [Test]
        public async Task SelectProduct_WithoutAnInstallationOfIt_ChangesNothing()
        {
            world = new InstallationWorld();
            world.AddCommunityInstallation(NeoRoot, Product.NeoEE);
            world.FileSystem.AddDirectory(SettingsFolder);
            UseCountingSettings();
            Assert.That(service.SelectProduct(Product.NeoEE), Is.False, "before the first search nothing is known");
            await service.RefreshAsync();

            Assert.That(service.SelectProduct(Product.EE), Is.False);

            Assert.That(service.Selected.Product, Is.SameAs(Product.NeoEE));
            Assert.That(counting.Written, Is.Empty, "nothing is saved");
            Assert.That(() => service.SelectProduct(null), Throws.ArgumentNullException);
        }

        [Test]
        public async Task TheProductChosenLast_IsAppliedAfterTheNextSearch()
        {
            settings.Current.LastProduct = "EE";

            await service.RefreshAsync();

            Assert.That(service.Selected.Product, Is.SameAs(Product.EE), "not NeoEE, which the default selection prefers");
            Assert.That(world.LogLinesAbout("Selection: EE installation"), Has.Length.EqualTo(1));
            Assert.That(world.LogLinesAbout("the product chosen last"), Has.Length.EqualTo(1));
            Assert.That(world.FileSystem.FileExists(SettingsFolder + @"\settings.json"), Is.False, "a search saves nothing");
        }

        [Test]
        public async Task TheProductChosenLast_WithoutAnInstallation_FallsBackToTheDefaultSelection()
        {
            world = new InstallationWorld();
            world.AddCommunityInstallation(NeoRoot, Product.NeoEE);
            world.FileSystem.AddDirectory(SettingsFolder);
            UseCountingSettings();
            settings.Current.LastProduct = "EE";

            await service.RefreshAsync();

            Assert.That(service.Selected.Product, Is.SameAs(Product.NeoEE));
            Assert.That(counting.Written, Is.Empty, "the remembered product is kept as it is, nothing is written");
            Assert.That(world.LogLinesAbout("the default selection"), Has.Length.EqualTo(1));
        }

        [Test]
        public async Task TheChoiceOfAnOlderLauncher_BecomesTheChoiceOfItsProduct_InMemory_AndIsWrittenWithTheNextChoice()
        {
            UseCountingSettings();
            settings.Current.GameDirectory = RetailFolder;

            await service.RefreshAsync();

            Assert.That(service.Selected.EeFolder, Is.EqualTo(RetailFolder).IgnoreCase);
            Assert.That(settings.Current.LastProduct, Is.EqualTo("EE"));
            Assert.That(ProductChoices.FolderOf(settings.Current, Product.EE), Is.EqualTo(RetailFolder));
            Assert.That(settings.Current.GameDirectory, Is.EqualTo(RetailFolder), "the mirror stays");
            Assert.That(counting.Written, Is.Empty, "the launcher writes settings.json only after a choice of the player");
            Assert.That(world.LogLinesAbout("the folder chosen by an older launcher, " + RetailFolder + ", is the choice for EE"),
                Has.Length.EqualTo(1));

            service.SelectProduct(Product.NeoEE);

            Assert.That(Saved().ProductFolders.Single().Folder, Is.EqualTo(RetailFolder), "the folder of the older launcher is kept");
            Assert.That(Saved().LastProduct, Is.EqualTo("NeoEE"));
            Assert.That(Saved().GameDirectory, Is.Empty);

            service.SelectProduct(Product.EE);

            Assert.That(service.Selected.EeFolder, Is.EqualTo(RetailFolder).IgnoreCase, "the folder chosen for EE");
            Assert.That(service.Result.IsSelectedByUser, Is.True);
            Assert.That(Saved().GameDirectory, Is.EqualTo(RetailFolder));
        }

        /// <summary>
        /// Launcher 1.0.0 ran in between: its "Auto-detect" emptied <c>GameDirectory</c> but left the folder of EE in
        /// <c>ProductFolders</c>. The choice of 1.0.0 counts: the first installation is used, in memory, and the file is
        /// written with the next choice.
        /// </summary>
        [Test]
        public async Task AutoDetectOfAnOlderLauncher_DropsTheFolderOfTheProductChosenLast_InMemory()
        {
            UseCountingSettings();
            ProductChoices.Choose(settings.Current, Product.EE, @"D:\Removed\Empire Earth");
            settings.Current.GameDirectory = string.Empty;

            await service.RefreshAsync();

            Assert.That(ProductChoices.FolderOf(settings.Current, Product.EE), Is.Empty);
            Assert.That(service.Selected.Product, Is.SameAs(Product.EE));
            Assert.That(service.Selected.EeFolder, Is.EqualTo(RetailFolder).IgnoreCase, "the first EE installation, not the removed folder");
            Assert.That(service.Result.IsSelectedByUser, Is.False);
            Assert.That(counting.Written, Is.Empty, "the launcher writes settings.json only after a choice of the player");
            Assert.That(world.LogLinesAbout("an older launcher emptied the game folder (Auto-detect); the folder chosen for EE is dropped"),
                Has.Length.EqualTo(1));
        }

        [Test]
        public async Task TheSessionProduct_WinsOverTheProductChosenLast_AndIsNotSaved()
        {
            UseCountingSettings();
            settings.Current.LastProduct = "NeoEE";
            service.SelectProductForSession(Product.EE);

            await service.RefreshAsync();

            Assert.That(service.Selected.Product, Is.SameAs(Product.EE));
            Assert.That(settings.Current.LastProduct, Is.EqualTo("NeoEE"), "the choice stays as it was");
            Assert.That(counting.Written, Is.Empty);
        }

        [Test]
        public async Task TheChoiceOfThePlayer_EndsTheSessionProduct_AndIsSaved()
        {
            UseCountingSettings();
            service.SelectProductForSession(Product.EE);
            await service.RefreshAsync();

            service.SelectProduct(Product.NeoEE);

            Assert.That(service.SessionProduct, Is.Null);
            Assert.That(service.Selected.Product, Is.SameAs(Product.NeoEE));
            Assert.That(Saved().LastProduct, Is.EqualTo("NeoEE"));
            Assert.That(world.LogLinesAbout("replaces the product of the command line (EE)"), Has.Length.EqualTo(1));

            await service.RefreshAsync();

            Assert.That(service.Selected.Product, Is.SameAs(Product.NeoEE), "also after the next search");
        }

        [Test]
        public async Task Select_OfARowOfAnotherProduct_ChoosesItsFolder_KeepsTheOther_AndSwitchesTheProduct()
        {
            await service.RefreshAsync();
            Installation retail = service.Result.Installations.Single(installation => installation.Kind == InstallationKind.Foreign);
            Installation neo = service.Result.Installations.Single(installation => installation.Kind == InstallationKind.Community);

            await service.SelectAsync(retail);

            Assert.That(Saved().LastProduct, Is.EqualTo("EE"));
            Assert.That(Saved().ProductFolders.Single().Folder, Is.EqualTo(retail.EeFolder));
            Assert.That(service.Selected.Root, Is.EqualTo(retail.Root).IgnoreCase);

            await service.SelectAsync(neo);

            Assert.That(Saved().LastProduct, Is.EqualTo("NeoEE"));
            Assert.That(Saved().ProductFolders.Select(entry => entry.Product), Is.EquivalentTo(new[] { "EE", "NeoEE" }), "one folder per product");
            Assert.That(Saved().GameDirectory, Is.EqualTo(neo.EeFolder), "the mirror is the folder of the product chosen last");
            Assert.That(service.Selected.Root, Is.EqualTo(NeoRoot));

            Assert.That(service.SelectProduct(Product.EE), Is.True);

            Assert.That(service.Selected.EeFolder, Is.EqualTo(retail.EeFolder).IgnoreCase, "the folder chosen for EE comes back");
            Assert.That(service.Result.IsSelectedByUser, Is.True);
        }

        [Test]
        public async Task ChooseFolder_IsResolvedToItsProduct_AndSavedTwice()
        {
            UseCountingSettings();
            string file = SettingsFolder + @"\settings.json";

            await service.ChooseFolderAsync(RetailFolder);

            Assert.That(service.Selected.EeFolder, Is.EqualTo(RetailFolder).IgnoreCase);
            Assert.That(counting.Saves(file), Is.EqualTo(2), "the folder, then the product it turned out to belong to");
            Assert.That(Saved().LastProduct, Is.EqualTo("EE"));
            Assert.That(Saved().ProductFolders.Single().Folder, Is.EqualTo(RetailFolder));
            Assert.That(Saved().GameDirectory, Is.EqualTo(RetailFolder));

            await service.ChooseFolderAsync(NeoRoot);

            Assert.That(Saved().LastProduct, Is.EqualTo("NeoEE"));
            Assert.That(Saved().ProductFolders.Select(entry => entry.Product), Is.EquivalentTo(new[] { "EE", "NeoEE" }));
            Assert.That(service.Selected.Root, Is.EqualTo(NeoRoot));
        }

        [Test]
        public async Task AutoDetect_KeepsTheProduct_AndTheFolderOfTheOther_AndUsesTheFirstInstallationOfTheProduct()
        {
            await service.RefreshAsync();
            Installation retail = service.Result.Installations.Single(installation => installation.Kind == InstallationKind.Foreign);
            Installation neo = service.Result.Installations.Single(installation => installation.Kind == InstallationKind.Community);
            await service.SelectAsync(neo);
            await service.SelectAsync(retail);

            await service.UseAutomaticDetectionAsync();

            Assert.That(Saved().LastProduct, Is.EqualTo("EE"), "the product stays");
            Assert.That(Saved().ProductFolders.Select(entry => entry.Product), Is.EqualTo(new[] { "NeoEE" }), "the folder of NeoEE stays");
            Assert.That(Saved().GameDirectory, Is.Empty);
            Assert.That(service.Selected.Product, Is.SameAs(Product.EE));
            Assert.That(service.Result.IsSelectedByUser, Is.False);

            service.SelectProduct(Product.NeoEE);

            Assert.That(service.Result.IsSelectedByUser, Is.True, "NeoEE still has its chosen folder");
        }

        /// <summary>
        /// Started with <c>--product=NeoEE</c> (an old shortcut, the hand-off of a second launcher) while EE was the product chosen
        /// last: "Auto-detect" keeps the NeoEE the player is looking at, and saves it, so that the next start opens with it.
        /// </summary>
        [Test]
        public async Task AutoDetect_WithTheSessionProductOfTheOtherProduct_KeepsTheSessionProduct_AndSavesItAsTheProductChosenLast()
        {
            await service.RefreshAsync();
            Installation retail = service.Result.Installations.Single(installation => installation.Kind == InstallationKind.Foreign);
            await service.SelectAsync(retail);
            Assert.That(settings.Current.LastProduct, Is.EqualTo("EE"));
            service.SelectProductForSession(Product.NeoEE);
            Assert.That(service.Selected.Product, Is.SameAs(Product.NeoEE), "the session shows NeoEE");

            await service.UseAutomaticDetectionAsync();

            Assert.That(service.Selected.Product, Is.SameAs(Product.NeoEE), "NeoEE stays selected, no page jumps to EE");
            Assert.That(service.SessionProduct, Is.Null, "the click of the player ended the argument");
            Assert.That(Saved().LastProduct, Is.EqualTo("NeoEE"), "and it is the product chosen last");
            Assert.That(Saved().GameDirectory, Is.Empty, "the mirror follows NeoEE, which has no chosen folder");
            Assert.That(Saved().ProductFolders.Select(entry => entry.Product), Is.EqualTo(new[] { "EE" }), "the folder chosen for EE stays");
        }

        [Test]
        public async Task AFolderChosenForAProductThatIsGone_StaysItsChoice_AndTheProductCanBeSelected()
        {
            UseCountingSettings();
            ProductChoices.Choose(settings.Current, Product.EE, @"D:\Removed\Empire Earth");
            ProductChoices.ChooseProduct(settings.Current, Product.NeoEE);

            await service.RefreshAsync();

            Assert.That(service.Selected.Product, Is.SameAs(Product.NeoEE));
            Assert.That(service.Result.Has(Product.EE), Is.True, "the missing folder is listed as an installation of EE");
            Assert.That(service.SelectProduct(Product.EE), Is.True);
            Assert.That(service.Selected.State, Is.EqualTo(InstallationState.FolderMissing));
            Assert.That(Saved().ProductFolders.Single().Folder, Is.EqualTo(@"D:\Removed\Empire Earth"));
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

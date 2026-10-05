using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Empire_Earth_Launcher.Core.Backup;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Lobby;
using Empire_Earth_Launcher.Core.Maintenance;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Core.Play;
using Empire_Earth_Launcher.Core.Settings;
using Empire_Earth_Launcher.Tests.Fakes;
using Empire_Earth_Launcher.Tests.TestSupport;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Launcher
{
    /// <summary>
    /// <see cref="MaintenanceModel"/>, the maintenance tools of the Tools page (L-WP8): the read-only scans after every
    /// search, the actions with a scan after them, no change while a setup runs (contract 4.2), and "Open backup folder"
    /// through the shell. With the fake registry, file system, mutexes and shell.
    /// </summary>
    [TestFixture]
    public class MaintenanceModelTests
    {
        private const string SettingsFile = @"C:\Users\Player\AppData\Local\Empire Earth Launcher\settings.json";
        private const string Root = @"C:\Program Files (x86)\Neo Empire Earth";
        private const string VirtualStore = @"C:\Users\Player\AppData\Local\VirtualStore";

        private static readonly string[] Virtualized =
            { @"C:\Program Files", @"C:\Program Files (x86)", @"C:\ProgramData", @"C:\Windows" };

        private GameSettingsWorld w;
        private SetupWatcher watcher;
        private InstallationService installations;
        private FakeProcessStarter shell;
        private MaintenanceModel model;
        private int changed;

        [SetUp]
        public void SetUp()
        {
            w = new GameSettingsWorld();
            var settings = new SettingsStore(w.FileSystem, SettingsFile, w.Logger);
            settings.Load();
            watcher = new SetupWatcher(w.Mutexes, w.World.Clock, w.Logger);
            installations = new InstallationService(w.Logger, settings, w.World.CreateDiscovery(), w.FileSystem, null, watcher);
            shell = new FakeProcessStarter();
            var paths = new EffectivePathResolver(w.FileSystem, VirtualStore, Virtualized);
            var fileBackup = new FileBackup(w.FileSystem, w.Backups, w.Logger);
            model = new MaintenanceModel(new RegistryCleanup(w.Registry, w.FileSystem, w.Guard, w.Backups, w.Logger),
                new WonLoginReset(w.FileSystem, paths, w.Guard, fileBackup, w.Logger), new VirtualStoreScanner(w.FileSystem, paths, w.Guard, w.Logger),
                new SavedGames(w.FileSystem, paths, w.SystemInfo, w.Guard, fileBackup, w.World.Clock, w.Logger),
                new NameChecks(w.FileSystem, paths, new LobbyProfileRepository(w.Logger, w.FileSystem, paths), w.Logger),
                installations, watcher, shell, w.FileSystem, GameSettingsWorld.BackupsFolder, w.Logger);
            model.Changed += (sender, e) => changed++;
        }

        private async Task Search()
        {
            await installations.RefreshAsync();
            Assert.That(model.LastScan, Is.Not.Null, "every search result starts the scans");
            await model.LastScan;
        }

        [Test]
        public void BeforeTheFirstSearch_NothingIsScanned()
        {
            Assert.That(model.Scan, Is.Null);
            Assert.That(model.LastScan, Is.Null);
            Assert.That(model.Selected, Is.Null);
        }

        [Test]
        public async Task AfterTheSearch_TheScansRunInTheBackground()
        {
            w.World.AddCommunityInstallation(Root, Product.NeoEE);
            w.FileSystem.AddFile(Root + @"\Empire Earth\_wonlogin.ks", "x");
            w.FileSystem.AddFile(Root + @"\Empire Earth\Data\Saved Games\a.ees", "s");

            await Search();

            Assert.That(model.Scan, Is.Not.Null);
            Assert.That(model.IsScanning, Is.False);
            Assert.That(model.Scan.Cleanup.HasCandidates, Is.False);
            Assert.That(model.Scan.WonFiles.ToMove, Has.Count.EqualTo(1));
            Assert.That(model.Scan.SavedGames, Has.Count.EqualTo(1));
            Assert.That(model.Scan.VirtualStore.IsVirtualizable, Is.True);
            Assert.That(model.Scan.Names.NamesChecked, Is.EqualTo(0));
            Assert.That(changed, Is.GreaterThanOrEqualTo(2), "scan started, scan ended");
            Assert.That(model.CanChange, Is.True);
        }

        [Test]
        public async Task WithoutAnInstallation_OnlyTheRegistryIsScanned()
        {
            await Search();

            Assert.That(model.Scan.Cleanup, Is.Not.Null);
            Assert.That(model.Scan.WonFiles, Is.Null);
            Assert.That(model.Scan.SavedGames, Is.Null);
        }

        [Test]
        public async Task AnAction_ScansAgain()
        {
            w.World.AddCommunityInstallation(Root, Product.NeoEE);
            w.FileSystem.AddFile(Root + @"\Empire Earth\_wonlogin.ks", "x");
            await Search();

            WonResetResult result = await model.ResetWonLoginAsync();

            Assert.That(result.Outcome, Is.EqualTo(WonResetOutcome.Done));
            Assert.That(model.IsBusy, Is.False);
            Assert.That(model.Scan.WonFiles.ToMove, Is.Empty, "the scan after the action");
        }

        [Test]
        public async Task TheRegistryCleanup_DeletesTheSelectedKeys()
        {
            RegistryLocation key = CleanupCandidates.All.Single(entry => entry.Id == "hkcu-ee-ee").Key;
            w.RawRegistry.SeedKey(key);
            w.World.SetInstalledFrom(key, @"D:\Old\Empire Earth");
            await Search();
            Assert.That(model.Scan.Cleanup.HasCandidates, Is.True);

            CleanupResult result = await model.DeleteKeysAsync(model.Scan.Cleanup.Offered);

            Assert.That(result.Outcome, Is.EqualTo(CleanupOutcome.Done));
            Assert.That(model.Scan.Cleanup.HasCandidates, Is.False);
        }

        /// <summary>Contract 4.2: while the setup watcher knows that a setup runs the page offers no change.</summary>
        [Test]
        public async Task WhileASetupRuns_NothingCanBeChanged()
        {
            w.World.AddCommunityInstallation(Root, Product.NeoEE);
            await Search();

            w.Mutexes.With("NeoEE_Setup");
            w.World.Clock.Advance(SetupWatcher.Interval);
            watcher.Tick();

            Assert.That(model.CanChange, Is.False);
            Assert.That(model.RunningSetup, Is.SameAs(SetupKind.NeoEE));
        }

        /// <summary>
        /// Contract 4.2 (coverage review): while a setup mutex exists, neither the scans nor the import plan read the manifest
        /// (files.sha256) of the installation, also not the scan after an action the guard refused.
        /// </summary>
        [Test]
        public async Task WhileASetupRuns_TheManifestIsNeverRead()
        {
            const string manifest = Root + @"\_setupdata_NeoEE\files.sha256";
            const string download = @"C:\Users\Player\Downloads\Duel.ees";
            w.World.AddCommunityInstallation(Root, Product.NeoEE);
            w.FileSystem.AddFile(manifest, SampleHashes.Of(1) + "  Empire Earth/Data/Language.dll\n");
            w.FileSystem.AddFile(download, "save");
            await Search();
            int before = w.FileSystem.OpenCount(manifest);
            Assert.That(before, Is.GreaterThan(0), "the scans read the manifest when no setup runs");

            w.Mutexes.With("NeoEE_Setup");
            w.World.Clock.Advance(SetupWatcher.Interval);
            watcher.Tick();
            ImportPlan plan = model.PlanImport(Game.EmpireEarth, new[] { download });
            ImportResult refused = await model.ImportAsync(plan, false);
            await model.RefreshAsync();

            Assert.That(refused.Block.Block, Is.EqualTo(MutationBlock.SetupRunning));
            Assert.That(plan.Files.Single().Check, Is.EqualTo(ImportCheck.ManifestUnusable), "nothing is known while the setup runs");
            Assert.That(w.FileSystem.OpenCount(manifest), Is.EqualTo(before));
        }

        /// <summary>
        /// A scan that fails after a successful action (a bug, logged by the scan) does not hide the result of the action
        /// (build/UI review): the keys are deleted and backed up, so the page must say so.
        /// </summary>
        [Test]
        public async Task AFailingScanAfterAnAction_DoesNotHideItsResult()
        {
            w.World.AddCommunityInstallation(Root, Product.NeoEE);
            w.FileSystem.AddFile(Root + @"\Empire Earth\_wonlogin.ks", "x");
            await Search();
            bool failScans = false;
            model.Changed += (sender, e) =>
            {
                if (!model.IsBusy && model.IsScanning)
                    failScans = true; // the scan after the action has started
            };
            w.FileSystem.OnFileExists = path =>
            {
                if (failScans)
                    throw new InvalidOperationException("injected bug of a scanner");
            };

            WonResetResult result = await model.ResetWonLoginAsync();

            Assert.That(failScans, Is.True, "the scan after the action ran");
            Assert.That(result.Outcome, Is.EqualTo(WonResetOutcome.Done));
            Assert.That(model.IsBusy, Is.False);
            Assert.That(model.IsScanning, Is.False);
        }

        [Test]
        public void OpenBackupFolder_CreatesItAndOpensItInTheExplorer()
        {
            Assert.That(model.OpenBackupFolder(), Is.Null);

            Assert.That(w.FileSystem.DirectoryExists(GameSettingsWorld.BackupsFolder), Is.True);
            Assert.That(shell.OpenedFolders, Is.EqualTo(new[] { GameSettingsWorld.BackupsFolder }));
        }

        [Test]
        public void OpenBackupFolder_ReportsAnError()
        {
            shell.OpenException = new Win32Exception(2, "The system cannot find the file specified");

            Assert.That(model.OpenBackupFolder(), Is.EqualTo("The system cannot find the file specified"));
            w.FileSystem.FailOn(GameSettingsWorld.BackupsFolder, FileSystemOperation.CreateDirectory, FileSystemStatus.AccessDenied);
            Assert.That(model.OpenBackupFolder(), Does.Contain("Injected fault"));
        }
    }
}

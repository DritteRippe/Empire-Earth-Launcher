using System;
using System.Threading.Tasks;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.GameSettings;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Core.Play;
using Empire_Earth_Launcher.Core.Settings;
using Empire_Earth_Launcher.Tests.Core.Settings;
using Empire_Earth_Launcher.Tests.Fakes;

namespace Empire_Earth_Launcher.Tests.TestSupport
{
    /// <summary>
    /// A real <see cref="PlayModel"/> over the fake registry, file system, mutexes and shell with a NeoEE installation and, if asked,
    /// an EE installation, so that a page can be bound to it (<c>PlayPageEntriesTests</c>): the installations are searched, the
    /// choice is saved in the settings of the world (<see cref="SettingsFile"/>), and no program is started for real.
    /// </summary>
    internal sealed class PlayModelWorld
    {
        public const string SettingsFile = @"C:\Users\Player\AppData\Local\Empire Earth Launcher\settings.json";

        /// <param name="withEE">True to add an EE installation next to the NeoEE one.</param>
        /// <param name="eeComponents">The components of the EE installation; "game" only has no Art of Conquest.</param>
        public PlayModelWorld(bool withEE, string eeComponents = "game,gameaoc")
        {
            const int launcherPid = 100;
            World = new GameSettingsWorld();
            World.AddAdminInstallationOfAnotherAccount(GameSettingsWorld.NeoRoot, Product.NeoEE);
            if (withEE)
                World.AddAdminInstallationOfAnotherAccount(GameSettingsWorld.EERoot, Product.EE, eeComponents);
            Settings = new SettingsStore(World.FileSystem, SettingsFile, World.Logger);
            Settings.Load();
            var watcher = new SetupWatcher(World.Mutexes, World.World.Clock, World.Logger);
            Installations = new InstallationService(World.Logger, Settings, World.World.CreateDiscovery(), World.FileSystem, null, watcher);
            var gameSettings = new GameSettingsModel(World.CreateDefaultsService(),
                new ConsistencyChecker(World.Registry, World.FileSystem, World.SystemInfo),
                new CompatibilityOptions(World.Registry, World.SystemInfo, World.Guard, World.Backups, World.Logger), Settings,
                World.SystemInfo, GameSettingsWorld.BackupsFolder, World.Logger);
            var shell = new FakeProcessStarter();
            var windows = new FakeWindowSystem(shell.ProcessId.Value) { Foreground = launcherPid };
            var starter = new GameStarter(new RunningGameDetector(World.Mutexes, new FakeProcessList()), World.FileSystem,
                World.CreateDefaultsService(), shell, World.Logger, null, windows);
            var activator = new GameWindowActivator(windows, World.World.Clock, World.Logger, launcherPid, (time, token) =>
            {
                World.World.Clock.Advance(time);
                return Task.CompletedTask;
            });
            var versions = new ProgramVersions(World.FileSystem, new FakeFileVersionReader());
            Model = new PlayModel(starter, versions, watcher, Installations, Settings, gameSettings, World.Logger, activator);
        }

        public GameSettingsWorld World { get; }

        public SettingsStore Settings { get; }

        public InstallationService Installations { get; }

        public PlayModel Model { get; }

        /// <summary>The settings as the file holds them now (what launcher 1.0.0 or the next start reads).</summary>
        public LauncherSettings Saved()
        {
            return new SettingsStore(World.FileSystem, SettingsFile, World.Logger).LoadAndGet();
        }

        /// <summary>Searches the installations and waits for the result.</summary>
        public void Search()
        {
            Installations.RefreshAsync().GetAwaiter().GetResult();
        }
    }
}

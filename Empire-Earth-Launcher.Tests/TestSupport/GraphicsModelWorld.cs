using System.Threading.Tasks;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.GameSettings;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Core.Play;
using Empire_Earth_Launcher.Core.Settings;

namespace Empire_Earth_Launcher.Tests.TestSupport
{
    /// <summary>
    /// The fake computer of the graphics page without a window: a <see cref="GraphicsModel"/> on the real
    /// <see cref="InstallationService"/>, <see cref="SetupWatcher"/> and <see cref="GameSettingsModel"/>, for the tests of the
    /// model and of the view that the page shows.
    /// </summary>
    internal sealed class GraphicsModelWorld
    {
        public const string Root = GameSettingsWorld.NeoRoot;
        public const string EeFolder = Root + @"\Empire Earth";
        public const string AocFolder = Root + @"\Empire Earth - The Art of Conquest";
        public const string VirtualStore = @"C:\Users\Player\AppData\Local\VirtualStore";
        public const string Wrapper = @"additional\directx_wrapper";

        private const string SettingsFile = @"C:\Users\Player\AppData\Local\Empire Earth Launcher\settings.json";

        public static readonly RegistryLocation NeoEE = GameSettingsWorld.Settings(Product.NeoEE, Game.EmpireEarth);
        public static readonly RegistryLocation NeoAoC = GameSettingsWorld.Settings(Product.NeoEE, Game.ArtOfConquest);

        public GraphicsModelWorld()
        {
            World = new GameSettingsWorld();
            var settings = new SettingsStore(World.FileSystem, SettingsFile, World.Logger);
            settings.Load();
            Watcher = new SetupWatcher(World.Mutexes, World.World.Clock, World.Logger);
            Installations = new InstallationService(World.Logger, settings, World.World.CreateDiscovery(), World.FileSystem, null, Watcher);
            GameSettings = new GameSettingsModel(World.CreateDefaultsService(),
                new ConsistencyChecker(World.Registry, World.FileSystem, World.SystemInfo),
                new CompatibilityOptions(World.Registry, World.SystemInfo, World.Guard, World.Backups, World.Logger), settings,
                World.SystemInfo, GameSettingsWorld.BackupsFolder, World.Logger);
            var paths = new EffectivePathResolver(World.FileSystem, VirtualStore, new[] { @"C:\Program Files (x86)" });
            Model = new GraphicsModel(World.CreateDefaultsService(), GameSettings, Installations, Watcher, World.SystemInfo,
                World.FileSystem, paths, World.Logger);
        }

        public GameSettingsWorld World { get; }

        public SetupWatcher Watcher { get; }

        public InstallationService Installations { get; }

        public GameSettingsModel GameSettings { get; }

        public GraphicsModel Model { get; }

        /// <summary>A community installation with the window values the setup writes (1920x1080) for each game of <paramref name="components"/>.</summary>
        public void AddInstallation(string components = "game,gameaoc")
        {
            World.AddAdminInstallationOfAnotherAccount(Root, Product.NeoEE, components);
            World.RawRegistry.Seed(NeoEE, "Game Window Width", RegistryValue.FromDWord(1920));
            World.RawRegistry.Seed(NeoEE, "Game Window Height", RegistryValue.FromDWord(1080));
            if (components.Contains("gameaoc"))
            {
                World.RawRegistry.Seed(NeoAoC, "Game Window Width", RegistryValue.FromDWord(1920));
                World.RawRegistry.Seed(NeoAoC, "Game Window Height", RegistryValue.FromDWord(1080));
            }
        }

        /// <summary>The search of the installations, and the read of the graphics state that follows it.</summary>
        public async Task Search()
        {
            await Installations.RefreshAsync();
            await Model.LastRead;
        }

        /// <summary>A setup starts: its mutex exists and the watcher has seen it.</summary>
        public void StartSetup(string mutex = "NeoEE_Setup")
        {
            World.Mutexes.With(mutex);
            World.World.Clock.Advance(SetupWatcher.Interval);
            Watcher.Tick();
        }
    }
}

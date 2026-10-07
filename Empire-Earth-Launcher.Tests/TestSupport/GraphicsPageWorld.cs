using System;
using System.Windows.Forms;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.GameSettings;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Core.Play;
using Empire_Earth_Launcher.Core.Settings;
using Empire_Earth_Launcher.Tests.Fakes;

namespace Empire_Earth_Launcher.Tests.TestSupport
{
    /// <summary>The states of the Graphics page the geometry tests drive it through (<see cref="GraphicsPageWorld"/>).</summary>
    public enum GraphicsPageState
    {
        /// <summary>The discovery has not finished: the page says that it is searching.</summary>
        Searching,

        /// <summary>An installation of both games without a wrapper ("Native"), the window size of the setup chosen.</summary>
        Native,

        /// <summary>
        /// A dgVoodoo wrapper with the conf of Empire Earth read from the VirtualStore (a long note, and the hint about the old
        /// settings of its VirtualStore copy) and no conf of The Art of Conquest, on a scaled screen: every text of the page at
        /// its longest, with the result of a change of the size.
        /// </summary>
        DgVoodoo,

        /// <summary>A setup runs: the page says so and the list and the button are disabled.</summary>
        SetupRunning,

        /// <summary>
        /// A dgVoodoo wrapper whose <c>dgVoodoo.conf</c> of both games still has the settings of a setup before 1.1.0 (dgVoodoo
        /// 2.82.1): the hint to repair with the setup is shown with one paragraph per game.
        /// </summary>
        OldPreset,
    }

    /// <summary>
    /// A Graphics page that runs on the real <see cref="GraphicsModel"/>, <see cref="InstallationService"/> and
    /// <see cref="SetupWatcher"/> with the fake computer of the other tests, filled the way the launcher fills it: the page is
    /// created hidden in a window that is not shown, initialized (its first <c>ShowState</c>), and then receives the state of the
    /// discovery, all before anything is shown. Windows only (its list is a Krypton combo box).
    /// </summary>
    internal sealed class GraphicsPageWorld : IDisposable
    {
        private const string SettingsFile = @"C:\Users\Player\AppData\Local\Empire Earth Launcher\settings.json";
        private const string VirtualStore = @"C:\Users\Player\AppData\Local\VirtualStore";
        private const string Wrapper = @"additional\directx_wrapper";
        private const string Conf = "[General]\r\nOutputAPI = d3d11_fl10_1\r\nFullScreenMode = true\r\n\r\n[DirectX]\r\n" +
                                    "AppControlledScreenMode = true\r\nDisableAltEnterToToggleScreenMode = false\r\n\r\n[GeneralExt]\r\n" +
                                    "WindowedAttributes\t\t\t= Borderless, AlwaysOnTop, FullscreenSize";

        private readonly GameSettingsWorld world = new GameSettingsWorld();
        private readonly Form window;

        private GraphicsPageWorld(GraphicsPageState state, bool hidden, float fontScale)
        {
            if (state != GraphicsPageState.Searching)
            {
                bool dgVoodoo = state == GraphicsPageState.DgVoodoo || state == GraphicsPageState.OldPreset;
                world.AddAdminInstallationOfAnotherAccount(GameSettingsWorld.NeoRoot, Product.NeoEE,
                    dgVoodoo ? "game,gameaoc," + Wrapper + @"," + Wrapper + @"\dx11_lvl10_1" : "game,gameaoc");
                foreach (Game game in Game.All)
                {
                    RegistryLocation key = GameSettingsWorld.Settings(Product.NeoEE, game);
                    world.RawRegistry.Seed(key, "Game Window Width", RegistryValue.FromDWord(1920));
                    world.RawRegistry.Seed(key, "Game Window Height", RegistryValue.FromDWord(1080));
                }
                if (state == GraphicsPageState.DgVoodoo)
                {
                    world.SystemInfo.WithScreen(1920, 1080, 150);
                    world.FileSystem.AddFile(VirtualStore + @"\Program Files (x86)\Neo Empire Earth\Empire Earth\dgVoodoo.conf", Conf);
                }
                if (state == GraphicsPageState.OldPreset)
                {
                    world.FileSystem.AddFile(GameSettingsWorld.NeoRoot + @"\Empire Earth\dgVoodoo.conf", Core.Graphics.DgVoodooPresetTests.OldConf);
                    world.FileSystem.AddFile(GameSettingsWorld.NeoRoot + @"\Empire Earth - The Art of Conquest\dgVoodoo.conf", Core.Graphics.DgVoodooPresetTests.OldConf);
                }
            }

            var settings = new SettingsStore(world.FileSystem, SettingsFile, world.Logger);
            Watcher = new SetupWatcher(world.Mutexes, world.World.Clock, world.Logger);
            Installations = new InstallationService(world.Logger, settings, world.World.CreateDiscovery(), world.FileSystem, null, Watcher);
            var gameSettings = new GameSettingsModel(world.CreateDefaultsService(),
                new ConsistencyChecker(world.Registry, world.FileSystem, world.SystemInfo),
                new CompatibilityOptions(world.Registry, world.SystemInfo, world.Guard, world.Backups, world.Logger), settings,
                world.SystemInfo, GameSettingsWorld.BackupsFolder, world.Logger);
            var paths = new EffectivePathResolver(world.FileSystem, VirtualStore, new[] { @"C:\Program Files (x86)" });
            Model = new GraphicsModel(world.CreateDefaultsService(), gameSettings, Installations, Watcher, world.SystemInfo,
                world.FileSystem, paths, world.Logger);

            Page = new GraphicsUserControl();
            ConstructedSize = Page.Size;
            LauncherPages.ScaleFonts(Page, fontScale);
            // The window of the main window is not shown while the pages are filled; without it the page is visible by itself.
            window = hidden ? LauncherPages.Host(Page) : null;
            Page.Initialize(new FakeThemeService(), Model, Watcher, new UiOperation(world.Logger));

            if (state == GraphicsPageState.Searching)
                return;
            UiThread.Run(async () =>
            {
                await Installations.RefreshAsync();
                await Model.LastRead;
            });
            if (state == GraphicsPageState.DgVoodoo)
                UiThread.Run(() => Model.ApplyResolutionAsync(new ScreenSize(1600, 900)));
            if (state == GraphicsPageState.SetupRunning)
            {
                world.Mutexes.With("NeoEE_Setup");
                world.World.Clock.Advance(SetupWatcher.Interval);
                Watcher.Tick();
            }
        }

        public GraphicsUserControl Page { get; }

        public GraphicsModel Model { get; }

        public InstallationService Installations { get; }

        public SetupWatcher Watcher { get; }

        /// <summary>The window that holds the page, never shown unless the caller shows it; null for a page that was created visible.</summary>
        public Form Window
        {
            get { return window; }
        }

        /// <summary>The size of the page after construction (the designer size, scaled by the font of the computer).</summary>
        public System.Drawing.Size ConstructedSize { get; }

        /// <summary>
        /// The page in <paramref name="state"/>. <paramref name="hidden"/> is true as in the launcher, where the page is created
        /// hidden in a window that is not shown; <paramref name="fontScale"/> makes the fonts that much larger before the state
        /// arrives.
        /// </summary>
        public static GraphicsPageWorld In(GraphicsPageState state, bool hidden = true, float fontScale = 1f)
        {
            return new GraphicsPageWorld(state, hidden, fontScale);
        }

        public void Dispose()
        {
            if (window != null)
                window.Dispose();
            else
                Page.Dispose();
        }
    }
}

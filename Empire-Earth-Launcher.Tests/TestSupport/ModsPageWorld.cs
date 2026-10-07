using System;
using System.Windows.Forms;
using Empire_Earth_Launcher.Core.Play;
using Empire_Earth_Launcher.Tests.Fakes;

namespace Empire_Earth_Launcher.Tests.TestSupport
{
    /// <summary>The states of the Mods page the geometry tests drive it through (<see cref="ModsPageWorld"/>).</summary>
    public enum ModsPageState
    {
        /// <summary>The discovery has not finished: the page says that it is searching.</summary>
        Searching,

        /// <summary>dreXmod 3 with the presets of the setup in both games, the config of the setup, the template hidden.</summary>
        Presets,

        /// <summary>
        /// Every text of the page at its longest: a self-made preset with long credits and a size that is only a lower bound, the
        /// template shown, a config in the VirtualStore that names a folder that does not exist, and a game whose config and folder
        /// are missing.
        /// </summary>
        Everything,

        /// <summary>A setup runs: the page says so and the buttons are disabled.</summary>
        SetupRunning,
    }

    /// <summary>
    /// A Mods page that runs on the real <see cref="ModsModel"/>, <see cref="InstallationService"/> and <see cref="SetupWatcher"/>
    /// with the fake computer of the other tests, filled the way the launcher fills it: the page is created hidden in a window
    /// that is not shown, initialized (its first <c>ShowState</c>), and then receives the state of the discovery, all before
    /// anything is shown.
    /// </summary>
    internal sealed class ModsPageWorld : IDisposable
    {
        private readonly ModsModelWorld world = new ModsModelWorld();
        private readonly Form window;

        private ModsPageWorld(ModsPageState state, bool hidden, float fontScale)
        {
            if (state != ModsPageState.Searching)
            {
                world.AddInstallation();
                world.AddShippedPresets(ModsModelWorld.Config(mod: "1", modName: "yukon", lobby: "1", lobbyName: "energycube"));
                if (state == ModsPageState.Everything)
                    AddEverything();
            }

            Page = new ModsUserControl();
            ConstructedSize = Page.Size;
            LauncherPages.ScaleFonts(Page, fontScale);
            // The window of the main window is not shown while the pages are filled; without it the page is visible by itself.
            window = hidden ? LauncherPages.Host(Page) : null;
            Page.Initialize(new FakeThemeService(), world.Model, world.Watcher);

            if (state == ModsPageState.Searching)
                return;
            UiThread.Run(async () => await world.Search());
            if (state == ModsPageState.Everything)
                world.Model.ShowTemplates = true;
            if (state == ModsPageState.SetupRunning)
                world.StartSetup();
        }

        private void AddEverything()
        {
            world.AddPreset(ModsModelWorld.EeFolder, "a-preset-with-a-very-long-folder-name-that-the-player-made-himself",
                ModsModelWorld.Credits("A preset with a very long name that its author gave it in the credits",
                    "the second week of October in the year two thousand and twenty three",
                    "an author with a long name, another author with a long name, and a third author"), 3000);
            world.World.FileSystem.AddFile(ModsModelWorld.EeFolder + @"\Data\dxm\mods\a-preset-with-a-very-long-folder-name-that-the-player-made-himself\textures\b.sst", new byte[2000]);
            world.World.FileSystem.AddFile(ModsModelWorld.VirtualStore + @"\Program Files (x86)\Neo Empire Earth\Empire Earth\dreXmod.config",
                ModsModelWorld.Config(mod: "1", modName: "doesnotexist", lobby: "1", lobbyName: "another-name-that-does-not-exist"));
            // The second game has neither a config nor a folder of presets.
            world.World.FileSystem.DeleteFile(ModsModelWorld.AocFolder + @"\dreXmod.config");
        }

        public ModsUserControl Page { get; }

        public ModsModel Model
        {
            get { return world.Model; }
        }

        public SetupWatcher Watcher
        {
            get { return world.Watcher; }
        }

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
        public static ModsPageWorld In(ModsPageState state, bool hidden = true, float fontScale = 1f)
        {
            return new ModsPageWorld(state, hidden, fontScale);
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

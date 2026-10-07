using System.Threading.Tasks;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Play;
using Empire_Earth_Launcher.Core.Settings;
using Empire_Earth_Launcher.Tests.Fakes;

namespace Empire_Earth_Launcher.Tests.TestSupport
{
    /// <summary>
    /// The fake computer of the Mods page without a window: a <see cref="ModsModel"/> on the real
    /// <see cref="InstallationService"/> and <see cref="SetupWatcher"/>, a file system that fails the test at the first change
    /// (the page only reads) and a process starter that records what is opened. Synthetic presets and configs, never a copy of
    /// the data of dreXmod.
    /// </summary>
    internal sealed class ModsModelWorld
    {
        public const string Root = GameSettingsWorld.NeoRoot;
        public const string EeFolder = Root + @"\Empire Earth";
        public const string AocFolder = Root + @"\Empire Earth - The Art of Conquest";
        public const string VirtualStore = @"C:\Users\Player\AppData\Local\VirtualStore";

        /// <summary>The components of a setup run with dreXmod 3 (the component names of the setup).</summary>
        public const string DreXmod3 = @"game,gameaoc,additional,additional\drexmod,additional\drexmod\v3";

        public const string DreXmod2 = @"game,gameaoc,additional,additional\drexmod,additional\drexmod\v2";

        private const string SettingsFile = @"C:\Users\Player\AppData\Local\Empire Earth Launcher\settings.json";

        public ModsModelWorld()
        {
            World = new GameSettingsWorld();
            var settings = new SettingsStore(World.FileSystem, SettingsFile, World.Logger);
            settings.Load();
            Watcher = new SetupWatcher(World.Mutexes, World.World.Clock, World.Logger);
            Installations = new InstallationService(World.Logger, settings, World.World.CreateDiscovery(), World.FileSystem, null, Watcher);
            Shell = new FakeProcessStarter();
            var paths = new EffectivePathResolver(World.FileSystem, VirtualStore, new[] { @"C:\Program Files (x86)" });
            Model = new ModsModel(Installations, Watcher, new WriteForbiddingFileSystem(World.FileSystem), paths, Shell, World.Logger);
        }

        public GameSettingsWorld World { get; }

        public SetupWatcher Watcher { get; }

        public InstallationService Installations { get; }

        public FakeProcessStarter Shell { get; }

        public ModsModel Model { get; }

        /// <summary>A community installation; with dreXmod 3 by default.</summary>
        public void AddInstallation(string components = DreXmod3)
        {
            World.AddAdminInstallationOfAnotherAccount(Root, Product.NeoEE, components);
        }

        /// <summary>The text of a <c>dreXmod.config</c> in the shape of dreXmod 3 with the choice of the mod and the lobby theme.</summary>
        public static string Config(string mod = "0", string modName = "dxm", string lobby = "1", string lobbyName = "dxm")
        {
            return "<!--\r\n#### Configuration file of a sample ####\r\n-->\r\n\r\n<config>\r\n\t<Drexmod>\r\n\t\t<Enabled>1</Enabled>\r\n" +
                   "\t</Drexmod>\r\n\t<Mod>\r\n\t\t<Enabled>" + mod + "</Enabled>\r\n\t\t<Name>" + modName + "</Name>\r\n\t</Mod>\r\n" +
                   "\t<LobbyTheme>\r\n\t\t<Enabled>" + lobby + "</Enabled>\r\n\t\t<Name>" + lobbyName + "</Name>\r\n\t</LobbyTheme>\r\n</config>\r\n";
        }

        /// <summary>The text of a <c>CREDITS</c> file with the three lines.</summary>
        public static string Credits(string name, string lastEdit = "21/10/2023", string createdBy = "somebody")
        {
            return "=====\r\n\r\nName: " + name + "\r\nLast Edit: " + lastEdit + "\r\nCreated by: " + createdBy +
                   "\r\n\r\n=====\r\n\r\nFree text of the credits.\r\n";
        }

        /// <summary>A preset folder in <c>Data\dxm\mods</c> of <paramref name="gameFolder"/> with one file of <paramref name="bytes"/>.</summary>
        public void AddPreset(string gameFolder, string folder, string credits, int bytes = 1000)
        {
            string path = gameFolder + @"\Data\dxm\mods\" + folder;
            World.FileSystem.AddDirectory(path);
            if (credits != null)
                World.FileSystem.AddFile(path + @"\CREDITS", credits);
            World.FileSystem.AddFile(path + @"\textures\a.sst", new byte[bytes]);
        }

        /// <summary>The usual presets of dreXmod 3 and a config, in both game folders.</summary>
        public void AddShippedPresets(string config = null)
        {
            foreach (string folder in new[] { EeFolder, AocFolder })
            {
                AddPreset(folder, "dxm", Credits("dxm (dreXmod)", createdBy: "author one, author two"), 9000);
                AddPreset(folder, "energycube", Credits("energycube", createdBy: "author two"), 11000);
                AddPreset(folder, "template", Credits("x", "XX/XX/XXXX", "x"), 1000);
                AddPreset(folder, "yukon", Credits("yukon", createdBy: "author one, author two"), 18 * 1000);
                World.FileSystem.AddFile(folder + @"\dreXmod.config", config ?? Config());
            }
        }

        /// <summary>The search of the installations, and the read of the presets that follows it.</summary>
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

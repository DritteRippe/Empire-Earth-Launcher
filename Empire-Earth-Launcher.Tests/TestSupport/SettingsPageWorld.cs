using System;
using System.Reflection;
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
    /// <summary>The states of the Game settings page the geometry tests drive it through (<see cref="SettingsPageWorld"/>).</summary>
    public enum SettingsPageState
    {
        /// <summary>The discovery has not finished: the page says that it is searching.</summary>
        Searching,

        /// <summary>An installation with its defaults applied; the compatibility warning with the book picture is shown.</summary>
        Warning,

        /// <summary>The display question of the first run, a hint of the consistency checks and the warning.</summary>
        QuestionAndHint,

        /// <summary>The player confirmed the warning: the four compatibility options with their explanation.</summary>
        Options,

        /// <summary>"Reset game settings" was clicked: the confirmation with Yes and No below the button.</summary>
        Confirmation,

        /// <summary>A setup runs: the page says so and every change is disabled.</summary>
        SetupRunning,
    }

    /// <summary>
    /// A Game settings page that runs on the real <see cref="GameSettingsModel"/>, <see cref="InstallationService"/> and
    /// <see cref="SetupWatcher"/> with the fake computer of the other tests, filled the way the launcher fills it: the page is
    /// created hidden in a window that is not shown, initialized (its first <c>ShowState</c>), and then receives the state of the
    /// discovery, all before anything is shown.
    /// </summary>
    internal sealed class SettingsPageWorld : IDisposable
    {
        private const string SettingsFile = @"C:\Users\Player\AppData\Local\Empire Earth Launcher\settings.json";

        private readonly GameSettingsWorld world = new GameSettingsWorld();
        private readonly Form window;

        private SettingsPageWorld(SettingsPageState state, bool hidden, float fontScale)
        {
            switch (state)
            {
                case SettingsPageState.QuestionAndHint:
                    world.AddAdminInstallationOfAnotherAccount(GameSettingsWorld.NeoRoot, Product.NeoEE, "game");
                    // A first run with a 16 bit display, and a wrapper the rasterizer does not match.
                    world.RawRegistry.Seed(GameSettingsWorld.Settings(Product.NeoEE, Game.EmpireEarth), "Game Bit Depth",
                        RegistryValue.FromDWord(16));
                    world.RawRegistry.Seed(GameSettingsWorld.Settings(Product.NeoEE, Game.EmpireEarth), "Rasterizer Name",
                        RegistryValue.FromString("Direct3D"));
                    break;
                case SettingsPageState.SetupRunning:
                    world.AddAdminInstallationOfAnotherAccount(GameSettingsWorld.NeoRoot, Product.NeoEE, "game");
                    world.Mutexes.With("NeoEE_Setup");
                    break;
                case SettingsPageState.Searching:
                    break;
                default:
                    // Both games of Empire Earth: the page shows one defaults line for each.
                    world.AddAdminInstallationOfAnotherAccount(GameSettingsWorld.EERoot, Product.EE);
                    break;
            }

            var settings = new SettingsStore(world.FileSystem, SettingsFile, world.Logger);
            Model = new GameSettingsModel(world.CreateDefaultsService(),
                new ConsistencyChecker(world.Registry, world.FileSystem, world.SystemInfo),
                new CompatibilityOptions(world.Registry, world.SystemInfo, world.Guard, world.Backups, world.Logger), settings,
                world.SystemInfo, GameSettingsWorld.BackupsFolder, world.Logger);
            var watcher = new SetupWatcher(world.Mutexes, world.World.Clock, world.Logger);
            var installations = new InstallationService(world.Logger, settings, world.World.CreateDiscovery(), world.FileSystem, null,
                watcher);

            Page = new SettingsUserControl();
            ConstructedSize = Page.Size;
            LauncherPages.ScaleFonts(Page, fontScale);
            // The window of the main window is not shown while the pages are filled; without it the page is visible by itself.
            window = hidden ? LauncherPages.Host(Page) : null;
            Page.Initialize(new FakeThemeService(), Model, installations, watcher, new UiOperation(world.Logger));

            if (state != SettingsPageState.Searching)
                UiThread.Run(() => Model.ApplyAfterDiscoveryAsync(world.Discover()));
            if (state == SettingsPageState.SetupRunning)
                watcher.Tick();
            if (state == SettingsPageState.Options)
                ConfirmCompatibilityWarning();
            if (state == SettingsPageState.Confirmation)
                Invoke("resetGameSettingsKryptonButton_Click");
        }

        public SettingsUserControl Page { get; }

        public GameSettingsModel Model { get; }

        /// <summary>The window that holds the page, never shown unless the caller shows it; null for a page that was created visible.</summary>
        public Form Window
        {
            get { return window; }
        }

        /// <summary>The size of the page after construction (the designer size, scaled by the font of the computer).</summary>
        public System.Drawing.Size ConstructedSize { get; }

        /// <summary>
        /// The page in <paramref name="state"/>. <paramref name="hidden"/> is true as in the launcher, where the page is created
        /// hidden in a window that is not shown; false for a page that is visible while it receives its state.
        /// <paramref name="fontScale"/> makes the fonts of the page that much larger before the state arrives.
        /// </summary>
        public static SettingsPageWorld In(SettingsPageState state, bool hidden = true, float fontScale = 1f)
        {
            return new SettingsPageWorld(state, hidden, fontScale);
        }

        /// <summary>
        /// What the click on "I understand" does, without its modal dialog: the warning is confirmed and the state shown
        /// again.
        /// </summary>
        private void ConfirmCompatibilityWarning()
        {
            Field("compatibilityConfirmed").SetValue(Page, true);
            Invoke("ShowState");
        }

        private FieldInfo Field(string name)
        {
            FieldInfo field = typeof(SettingsUserControl).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
                throw new InvalidOperationException("SettingsUserControl has no field " + name + "; adjust SettingsPageWorld.");
            return field;
        }

        private void Invoke(string method)
        {
            MethodInfo info = typeof(SettingsUserControl).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic);
            if (info == null)
                throw new InvalidOperationException("SettingsUserControl has no method " + method + "; adjust SettingsPageWorld.");
            object[] arguments = info.GetParameters().Length == 0 ? null : new object[] { Page, EventArgs.Empty };
            try
            {
                info.Invoke(Page, arguments);
            }
            catch (TargetInvocationException ex)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            }
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

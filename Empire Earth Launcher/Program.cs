using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Lobby;
using Empire_Earth_Launcher.Core.Logging;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Core.Settings;
using Empire_Earth_Launcher.Properties;
using Empire_Earth_WON;

namespace Empire_Earth_Launcher
{
    static class Program
    {
        /// <summary>
        /// Only for the global exception handlers below, which cannot get it passed in. Everything else
        /// receives the logger from <see cref="Main"/>.
        /// </summary>
        private static ILogger logger;

        /// <summary>
        /// The main entry point of the application and its composition root: the services (logger, settings,
        /// theme, registry, installations, Neo client) are created here, once, and passed to the windows that need them.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // Install the global handlers before anything else: SetUnhandledExceptionMode must be called
            // before the first window is created. Exceptions on the UI thread are reported and the launcher
            // keeps running, exceptions on other threads (or before Application.Run) end the process but
            // are logged and reported first.
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += OnUiThreadException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

            logger = new TraceFileLogger(LauncherPaths.LogFile);
            logger.Info("Starting Empire Earth Launcher v" + Application.ProductVersion);
            // A failed task whose exception nobody awaited would otherwise vanish silently (ADR 0004).
            TaskScheduler.UnobservedTaskException += (sender, e) => LogUnobservedTaskException(logger, e);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(true);

            // User settings: settings.json below %LOCALAPPDATA% (ADR 0005). A missing, damaged or unreadable file
            // means the defaults; SettingsStore logs it and moves a damaged file aside.
            var fileSystem = new LocalFileSystem();
            var settingsStore = new SettingsStore(fileSystem, LauncherPaths.SettingsFile, logger);
            settingsStore.Load();
            ApplyUiLanguage(logger, settingsStore.Current.UiCulture);
            var themeService = new KryptonThemeService(logger, LauncherPaths.ThemesDirectory);
            ApplySavedTheme(themeService, settingsStore.Current);

            // Every change of the registry passes the write policy (ADR 0007); the discovery only reads.
            var registry = new PolicyCheckedRegistry(new WindowsRegistry(), RegistryWritePolicy.Default);
            // The discovery starts when the main window is shown (MainForm.OnShown) and runs in the background.
            var installations = new InstallationService(logger, settingsStore,
                new InstallationDiscovery(registry, fileSystem, logger), fileSystem, LauncherPaths.ApplicationDirectory);
            var lobbyProfiles = new LobbyProfileRepository(logger, fileSystem, CreateEffectivePathResolver(fileSystem));
            var uiOperation = new UiOperation(logger);

            // Server settings stay application settings in "Empire Earth Launcher.exe.config" (ADR 0005).
            int playerListPollIntervalMilliseconds;
            NeoApiClient neoClient = CreateNeoClient(Settings.Default, out playerListPollIntervalMilliseconds);

            logger.Info("Starting Empire Earth Launcher Form");
            Application.Run(new MainForm(logger, themeService, settingsStore, installations, lobbyProfiles, uiOperation,
                neoClient, playerListPollIntervalMilliseconds));
        }

        /// <summary>
        /// The effective game paths of this account (ADR 0016): the folders UAC virtualizes for legacy programs such as
        /// the game (Program Files, Program Files (x86), ProgramData, Windows) and the VirtualStore below
        /// %LOCALAPPDATA%. Without %LOCALAPPDATA% nothing is virtualized.
        /// </summary>
        private static EffectivePathResolver CreateEffectivePathResolver(IFileSystem fileSystem)
        {
            string localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string virtualStore = string.IsNullOrEmpty(localApplicationData)
                ? null
                : Path.Combine(localApplicationData, "VirtualStore");
            return new EffectivePathResolver(fileSystem, virtualStore, new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.Windows)
            });
        }

        /// <summary>
        /// Uses the UI language chosen in the launcher settings (ADR 0009) on this thread and on every thread started
        /// later, before the first window exists; without a choice the texts follow the Windows display language. The
        /// formats of numbers and dates stay those of Windows.
        /// </summary>
        /// <param name="log">Log of the launcher.</param>
        /// <param name="setting"><see cref="LauncherSettings.UiCulture"/>; an unknown value is logged and ignored.</param>
        /// <returns>The culture that was set, or null when the Windows language is used.</returns>
        internal static CultureInfo ApplyUiLanguage(ILogger log, string setting)
        {
            if (!UiLanguage.TryNormalize(setting, out string language))
                log.Warning("The UI language \"" + setting + "\" of the launcher settings is unknown, the Windows language is used.");

            CultureInfo culture = UiLanguage.ToCulture(language);
            if (culture != null)
            {
                Thread.CurrentThread.CurrentUICulture = culture;
                CultureInfo.DefaultThreadCurrentUICulture = culture;
            }

            log.Info("UI language: " + CultureInfo.CurrentUICulture.Name +
                     (culture != null ? " (launcher setting)" : " (Windows)"));
            return culture;
        }

        /// <summary>
        /// Applies the theme the user selected last time (a custom theme file or a theme of the themes
        /// folder); if it cannot be loaded, the default theme. Problems are logged, never fatal.
        /// </summary>
        private static void ApplySavedTheme(IThemeService themeService, LauncherSettings settings)
        {
            const string DefaultThemeName = LauncherSettings.DefaultThemeName;
            string themeName = settings.ThemeName;
            string customThemeFile = settings.CustomThemeFile;

            if (!string.IsNullOrEmpty(customThemeFile) && themeService.ApplyThemeFile(customThemeFile))
                return;

            // No theme files are shipped yet (see README). A missing default theme is the normal case then, not
            // a problem worth a warning on every start: the designer colors are used.
            bool defaultThemeInstalled = themeService.GetAvailableThemeNames()
                .Contains(DefaultThemeName, StringComparer.OrdinalIgnoreCase);
            if (string.Equals(themeName, DefaultThemeName, StringComparison.OrdinalIgnoreCase) && !defaultThemeInstalled)
            {
                logger.Info("The default theme \"" + DefaultThemeName + "\" is not installed, the built-in colors are used.");
                return;
            }

            if (!string.IsNullOrEmpty(themeName) && themeService.ApplyTheme(themeName))
                return;
            if (themeName != DefaultThemeName && defaultThemeInstalled)
                themeService.ApplyTheme(DefaultThemeName);
        }

        /// <summary>
        /// Creates the client for the Neo server configured in the application settings (NeoServerHost,
        /// NeoServerPort, NeoTimeoutMilliseconds and PlayerListPollIntervalMilliseconds in
        /// "Empire Earth Launcher.exe.config").
        /// </summary>
        /// <returns>
        /// null if the settings are invalid or cannot be read (logged); the launcher then runs without the player
        /// list.
        /// </returns>
        private static NeoApiClient CreateNeoClient(Settings settings, out int playerListPollIntervalMilliseconds)
        {
            playerListPollIntervalMilliseconds = 0;
            try
            {
                playerListPollIntervalMilliseconds = settings.PlayerListPollIntervalMilliseconds;
                if (playerListPollIntervalMilliseconds <= 0)
                    throw new ArgumentOutOfRangeException(nameof(settings.PlayerListPollIntervalMilliseconds),
                        playerListPollIntervalMilliseconds, "The poll interval must be positive.");
                return new NeoApiClient(new NeoServerEndpoint(settings.NeoServerHost, settings.NeoServerPort,
                    settings.NeoTimeoutMilliseconds));
            }
            catch (Exception ex) when (ex is ArgumentException || ex is System.Configuration.ConfigurationException)
            {
                // A damaged or hand-edited Empire Earth Launcher.exe.config must not prevent the start.
                logger.Error("The Neo server settings are invalid, the online player list is disabled.", ex);
                return null;
            }
        }

        /// <summary>
        /// Logs the exception of a task that failed while nobody awaited it, and marks it observed. Since .NET 4.5
        /// such an exception does not end the process by default, so without this it would not show up anywhere.
        /// </summary>
        internal static void LogUnobservedTaskException(ILogger log, UnobservedTaskExceptionEventArgs e)
        {
            log.Error("A background task failed and nobody handled its error.", e.Exception);
            e.SetObserved();
        }

        private static void OnUiThreadException(object sender, ThreadExceptionEventArgs e)
        {
            ReportUnhandledException(e.Exception, false);
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            ReportUnhandledException(e.ExceptionObject as Exception, e.IsTerminating);
        }

        private static void ReportUnhandledException(Exception exception, bool isTerminating)
        {
            try
            {
                if (logger != null)
                {
                    logger.Error(isTerminating
                        ? "Unhandled exception, the launcher has to close."
                        : "Unhandled exception on the UI thread, the launcher continues.", exception);
                }
                else
                {
                    Console.Error.WriteLine(exception);
                }
            }
            catch (Exception logException)
            {
                // Reporting must go on even if the log is not writable.
                Console.Error.WriteLine(logException);
            }

            UnexpectedError.Show(null, exception, isTerminating);
        }
    }
}

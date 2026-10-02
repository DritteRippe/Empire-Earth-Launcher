using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Empire_Earth_Launcher.Core.Backup;
using Empire_Earth_Launcher.Core.Diagnostics;
using Empire_Earth_Launcher.Core.GameSettings;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Integrity;
using Empire_Earth_Launcher.Core.Lobby;
using Empire_Earth_Launcher.Core.Logging;
using Empire_Earth_Launcher.Core.Maintenance;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Core.Play;
using Empire_Earth_Launcher.Core.Repair;
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

            // One launcher per Windows session (ADR 0010); a second one says so in the UI language and ends. The handle is
            // kept until the launcher ends.
            using (IDisposable singleInstance = ClaimSingleInstance(new WindowsMutexOwner(logger), logger, ShowAlreadyRunning))
            {
                if (singleInstance == null)
                    return;
                Run(fileSystem, settingsStore);
            }
        }

        /// <summary>Creates the services of the launcher and runs the main window (the composition root).</summary>
        private static void Run(LocalFileSystem fileSystem, SettingsStore settingsStore)
        {
            var themeService = new KryptonThemeService(logger, LauncherPaths.ThemesDirectory);
            ApplySavedTheme(themeService, settingsStore.Current);

            // Every change of the registry passes the write policy (ADR 0007): the values of the contract tables, the
            // program paths and, from Windows 8 on and outside Wine, the compatibility entries of contract 3.7. The
            // discovery only reads.
            var systemInfo = new WindowsSystemInfo(logger);
            logger.Info(systemInfo.Describe());
            // The TLS versions of every HTTPS request, set once before the first one (ADR 0008 plan review).
            ConfigureTls(systemInfo.WindowsVersion, logger);
            var registry = new PolicyCheckedRegistry(new WindowsRegistry(), LauncherWritePolicy.For(systemInfo));
            // The setup mutexes (contract 4.2, ADR 0010): the main window ticks the watcher every half second, it probes every
            // two seconds. While a setup runs nothing reads install.ini, no game starts and nothing changes.
            var mutexProbe = new WindowsMutexProbe(logger);
            var setupWatcher = new SetupWatcher(mutexProbe, SystemClock.Instance, logger);
            // The discovery starts when the main window is shown (MainForm.OnShown) and runs in the background; it waits
            // while a setup runs and runs again when it has ended.
            var installations = new InstallationService(logger, settingsStore,
                new InstallationDiscovery(registry, fileSystem, logger), fileSystem, LauncherPaths.ApplicationDirectory,
                setupWatcher);
            // The files the game really uses (VirtualStore copy first, ADR 0016): lobby profiles, saves, WON files.
            EffectivePathResolver effectivePaths = CreateEffectivePathResolver(fileSystem);
            var lobbyProfiles = new LobbyProfileRepository(logger, fileSystem, effectivePaths);
            var uiOperation = new UiOperation(logger);

            // Game settings of contract 3 (L-WP5): every change asks the mutation guard (no setup, no game running, ADR 0016)
            // and backs up into %LOCALAPPDATA%\Empire Earth Launcher\Backups first (ADR 0007).
            var guard = new MutationGuard(mutexProbe, logger);
            var backups = new BackupLocations(LauncherPaths.BackupsDirectory, fileSystem, SystemClock.Instance, logger);
            var defaults = new GameDefaultsService(registry, fileSystem, systemInfo, guard, backups, logger);
            var gameSettings = new GameSettingsModel(defaults,
                new ConsistencyChecker(registry, fileSystem, systemInfo),
                new CompatibilityOptions(registry, systemInfo, guard, backups, logger),
                settingsStore, systemInfo, backups.Directory, logger);

            // Play (L-WP6, ADR 0010): setup and game mutexes, the program, class S and the first run, then the start through
            // the shell in the real game folder (contract 3.6, 3.7, 4.2); the repair advice opens the download page.
            var shell = new ShellProcessStarter();
            var gameStarter = new GameStarter(new RunningGameDetector(mutexProbe, new WindowsProcessList(logger)), fileSystem,
                defaults, shell, logger);
            var play = new PlayModel(gameStarter, new ProgramVersions(fileSystem, new WindowsFileVersionReader()), setupWatcher,
                installations, settingsStore, gameSettings, logger);

            // Integrity (L-WP7, contract 2): the quick check after every search, in the background, read-only, never while a
            // setup runs; the full check on request. Files are opened so that a setup can still delete and rename them.
            var integrity = new IntegrityModel(new IntegrityChecker(fileSystem, registry, mutexProbe, logger), installations,
                setupWatcher, logger);

            // Maintenance tools (L-WP8): read-only scans after every search; the registry cleanup, the WON login reset and the
            // import of saved games ask the mutation guard and back up first (ADR 0007, ADR 0016); the export only reads.
            var fileBackup = new FileBackup(fileSystem, backups, logger);
            var maintenance = new MaintenanceModel(new RegistryCleanup(registry, fileSystem, guard, backups, logger),
                new WonLoginReset(fileSystem, effectivePaths, guard, fileBackup, logger),
                new VirtualStoreScanner(fileSystem, effectivePaths, logger),
                new SavedGames(fileSystem, effectivePaths, systemInfo, guard, fileBackup, SystemClock.Instance, logger),
                new NameChecks(fileSystem, effectivePaths, lobbyProfiles, logger), installations, setupWatcher, shell, fileSystem,
                backups.Directory, logger);

            // Server settings stay application settings in "Empire Earth Launcher.exe.config" (ADR 0005). The poller sends no
            // request before the Play page starts it (ADR 0004).
            int playerListPollIntervalMilliseconds;
            NeoApiClient neoClient = CreateNeoClient(Settings.Default, out playerListPollIntervalMilliseconds);
            PlayerListPoller playerList = neoClient == null
                ? null
                : new PlayerListPoller(new NeoPlayerListSource(neoClient),
                    TimeSpan.FromMilliseconds(playerListPollIntervalMilliseconds), logger);

            // The update API (contract 4.3, 4.5, ADR 0008): only on request, HTTPS with the certificate check of Windows, no
            // redirects, 10 s, at most 4 KiB; the fixed download page for every failure.
            using (var https = new HttpsClient())
            {
                var updates = new UpdateModel(new SetupDownloadLocator(https, logger), new UpdateChecker(https, logger),
                    installations, shell, logger);

                // Network diagnostics and the diagnostics report (L-WP9, R7): only on request; DNS, the update API with the
                // AppId and the status server, nothing else (ADR 0008). The report and the log lines of the check follow the
                // privacy rules of ADR 0013 (plan review); the report is copied or saved, never sent.
                var anonymizer = new ReportAnonymizer(CurrentPrivateNames());
                var networkDiagnostics = new NetworkDiagnostics(new WindowsNetworkInfo(),
                    neoClient == null ? null : new NeoStatusServer(neoClient), https, fileSystem, effectivePaths, anonymizer,
                    SystemClock.Instance, logger);
                var diagnostics = new DiagnosticsModel(networkDiagnostics, installations,
                    () => DiagnosticsModel.Collect(Application.ProductVersion, systemInfo, SystemClock.Instance,
                        Environment.Is64BitOperatingSystem, installations, play, integrity, gameSettings, maintenance),
                    anonymizer, fileSystem, logger);

                logger.Info("Starting Empire Earth Launcher Form");
                Application.Run(new MainForm(logger, themeService, settingsStore, installations, lobbyProfiles, gameSettings,
                    play, integrity, updates, maintenance, diagnostics, setupWatcher, uiOperation, playerList));
            }
        }

        /// <summary>
        /// The TLS versions the launcher asks for on <paramref name="windowsVersion"/> (ADR 0008 plan review): exactly TLS 1.2
        /// on Windows 7 (NT 6.1), where SChannel offers TLS 1.2 to a program only when the program asks for it; null on
        /// every other Windows, which keeps <see cref="SecurityProtocolType.SystemDefault"/> (Windows chooses, TLS 1.2 or
        /// 1.3). Never TLS 1.3 by name (handshakes fail where SChannel has no TLS 1.3) and never an older version.
        /// </summary>
        internal static SecurityProtocolType? TlsProtocolsFor(Version windowsVersion)
        {
            if (windowsVersion == null)
                throw new ArgumentNullException(nameof(windowsVersion));
            return windowsVersion.Major == 6 && windowsVersion.Minor == 1 ? SecurityProtocolType.Tls12 : (SecurityProtocolType?)null;
        }

        /// <summary>
        /// Sets <see cref="ServicePointManager.SecurityProtocol"/>, process-wide, from <see cref="TlsProtocolsFor"/>: the only
        /// assignment in the launcher (an architecture test checks it), made once before the first request.
        /// </summary>
        private static void ConfigureTls(Version windowsVersion, ILogger log)
        {
            SecurityProtocolType? protocols = TlsProtocolsFor(windowsVersion);
            if (protocols == null)
            {
                log.Info("TLS: the versions Windows chooses (" + ServicePointManager.SecurityProtocol + ").");
                return;
            }
            ServicePointManager.SecurityProtocol = protocols.Value;
            log.Info("TLS: Windows 7, TLS 1.2 requested explicitly (" + ServicePointManager.SecurityProtocol + ").");
        }

        /// <summary>
        /// Claims the single-instance mutex <see cref="SingleInstance.MutexName"/> (ADR 0010); if another launcher holds it,
        /// shows <see cref="Resources.LauncherAlreadyRunning"/> and returns null, and the launcher ends.
        /// </summary>
        internal static IDisposable ClaimSingleInstance(IMutexOwner owner, ILogger log, Action<string> showMessage)
        {
            if (showMessage == null)
                throw new ArgumentNullException(nameof(showMessage));
            IDisposable handle = SingleInstance.TryClaim(owner, log);
            if (handle == null)
                showMessage(Resources.LauncherAlreadyRunning);
            return handle;
        }

        private static void ShowAlreadyRunning(string message)
        {
            MessageBox.Show(message, Resources.LauncherTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
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
        /// What identifies this player and computer and never appears in the diagnostics report or in the log lines of the
        /// network diagnostics (ADR 0013 plan review): the user name, the profile folders, the computer and the domain name.
        /// </summary>
        private static PrivateNames CurrentPrivateNames()
        {
            string hostName = null;
            string domainName = null;
            try
            {
                IPGlobalProperties properties = IPGlobalProperties.GetIPGlobalProperties();
                hostName = properties.HostName;
                domainName = properties.DomainName;
            }
            catch (NetworkInformationException ex)
            {
                logger.Warning("The host and domain name of this computer are unknown; the report replaces the NetBIOS name only.", ex);
            }
            return new PrivateNames(Environment.UserName, new[] { Environment.MachineName, hostName }, domainName,
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
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

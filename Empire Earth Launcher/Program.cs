using System;
using System.Threading;
using System.Windows.Forms;
using Empire_Earth_Launcher.Properties;
using Empire_Earth_WON;

namespace Empire_Earth_Launcher
{
    static class Program
    {
        /// <summary>Theme applied at start.</summary>
        private const string DefaultThemeName = "Light";

        /// <summary>
        /// Only for the global exception handlers below, which cannot get it passed in. Everything else
        /// receives the logger from <see cref="Main"/>.
        /// </summary>
        private static ILogger logger;

        /// <summary>
        /// The main entry point of the application and its composition root: the services (logger, theme,
        /// Neo client) are created here, once, and passed to the windows that need them.
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
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(true);

            var themeService = new KryptonThemeService(logger, LauncherPaths.ThemesDirectory);
            themeService.ApplyTheme(DefaultThemeName);

            int playerListPollIntervalMilliseconds;
            NeoApiClient neoClient = CreateNeoClient(Settings.Default, out playerListPollIntervalMilliseconds);

            logger.Info("Starting Empire Earth Launcher Form");
            Application.Run(new Form1(logger, themeService, neoClient, playerListPollIntervalMilliseconds));
        }

        /// <summary>
        /// Creates the client for the Neo server configured in the application settings (NeoServerHost,
        /// NeoServerPort, NeoTimeoutMilliseconds and PlayerListPollIntervalMilliseconds in
        /// "Empire Earth Launcher.exe.config").
        /// </summary>
        /// <returns>null if the settings are invalid (logged); the launcher then runs without the player list.</returns>
        private static NeoApiClient CreateNeoClient(Settings settings, out int playerListPollIntervalMilliseconds)
        {
            playerListPollIntervalMilliseconds = settings.PlayerListPollIntervalMilliseconds;
            try
            {
                if (playerListPollIntervalMilliseconds <= 0)
                    throw new ArgumentOutOfRangeException(nameof(settings.PlayerListPollIntervalMilliseconds),
                        playerListPollIntervalMilliseconds, "The poll interval must be positive.");
                return new NeoApiClient(new NeoServerEndpoint(settings.NeoServerHost, settings.NeoServerPort,
                    settings.NeoTimeoutMilliseconds));
            }
            catch (ArgumentException ex)
            {
                logger.Error("The Neo server settings are invalid, the online player list is disabled.", ex);
                return null;
            }
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

            string message = (isTerminating
                                 ? "An unexpected error occurred and the launcher has to close."
                                 : "An unexpected error occurred. The launcher will try to continue.")
                             + Environment.NewLine + Environment.NewLine
                             + (exception != null ? exception.Message : "Unknown error.")
                             + Environment.NewLine + Environment.NewLine
                             + "Details have been written to " + LauncherPaths.LogFile + ".";
            MessageBox.Show(message, "Empire Earth Launcher", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace Empire_Earth_Launcher
{
    static class Program
    {
        
        public readonly static Logging Logging =  new Logging("log.txt");
        public readonly static LauncherKryptonTheme LauncherKryptonTheme = new LauncherKryptonTheme();

        /// <summary>
        /// Point d'entrée principal de l'application.
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

            Logging.Log("Starting Empire Earth Launcher v" + Application.ProductVersion);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(true);

            LauncherKryptonTheme.SwitchThemeFromName("Light");

            Logging.Log("Starting Empire Earth Launcher Form");
            Application.Run(new Form1());
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
                Logging.Log(isTerminating
                    ? "Unhandled exception, the launcher has to close."
                    : "Unhandled exception on the UI thread, the launcher continues.", exception);
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
                             + "Details have been written to log.txt.";
            MessageBox.Show(message, "Empire Earth Launcher", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}

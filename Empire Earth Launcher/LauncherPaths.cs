using System;
using System.IO;

namespace Empire_Earth_Launcher
{
    /// <summary>
    /// File system locations of the launcher, in one place. Nothing depends on the current directory, so
    /// starting the launcher from a shortcut with another "Start in" folder behaves the same.
    /// </summary>
    internal static class LauncherPaths
    {
        /// <summary>File name of the launcher log inside <see cref="UserDataDirectory"/>.</summary>
        public const string LogFileName = "log.txt";

        /// <summary>Folder with the theme files (*.xml) next to the launcher executable.</summary>
        public const string ThemesFolderName = "themes";

        /// <summary>Folder of the launcher executable (with a trailing separator).</summary>
        public static string ApplicationDirectory
        {
            get { return AppDomain.CurrentDomain.BaseDirectory; }
        }

        /// <summary>Theme files shipped with the launcher.</summary>
        public static string ThemesDirectory
        {
            get { return Path.Combine(ApplicationDirectory, ThemesFolderName); }
        }

        /// <summary>
        /// Per-user data of the launcher: %LOCALAPPDATA%\Empire Earth Launcher (the mod creator uses a
        /// subfolder of it). The launcher folder itself may be read-only, e.g. below Program Files.
        /// </summary>
        public static string UserDataDirectory
        {
            get
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                // Empty for accounts without a profile; the temporary folder is still per user.
                if (string.IsNullOrEmpty(localAppData))
                    localAppData = Path.GetTempPath();
                return Path.Combine(localAppData, "Empire Earth Launcher");
            }
        }

        /// <summary>Full path of the launcher log.</summary>
        public static string LogFile
        {
            get { return Path.Combine(UserDataDirectory, LogFileName); }
        }
    }
}

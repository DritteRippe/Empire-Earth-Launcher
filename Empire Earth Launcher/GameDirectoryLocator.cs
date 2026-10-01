using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Security;
using Microsoft.Win32;

namespace Empire_Earth_Launcher
{
    /// <summary>Where the game folder used by the launcher comes from.</summary>
    internal enum GameDirectorySource
    {
        /// <summary>No Empire Earth installation was found.</summary>
        NotFound,

        /// <summary>The folder chosen by the user in the launcher settings.</summary>
        UserSetting,

        /// <summary>The installation registered by an Empire Earth setup.</summary>
        Registry,

        /// <summary>The launcher is installed inside the game folder.</summary>
        LauncherFolder
    }

    /// <summary>
    /// Detects the Empire Earth installation folder (the folder of "Empire Earth.exe" and the WON lobby files).
    /// </summary>
    /// <remarks>
    /// The Empire Earth setups (see the EE-modders/Empire-Earth-Setup repository, setup_is6.iss) register the
    /// game folder below <see cref="RegistryKeys"/> in two values, the drive (<see cref="VolumeValueName"/>,
    /// e.g. "C:") and the folder on that drive (<see cref="DirectoryValueName"/>, e.g.
    /// "\PROGRAM FILES (X86)\NEO EMPIRE EARTH\Empire Earth\"). The community setups write them to
    /// HKEY_CURRENT_USER, older setups to HKEY_LOCAL_MACHINE (32-bit view on 64-bit Windows), so both are
    /// searched.
    /// </remarks>
    internal class GameDirectoryLocator
    {
        /// <summary>
        /// Registry keys of the game, in order of preference: NeoEE (the online lobby the launcher supports)
        /// first, then the original Empire Earth key.
        /// </summary>
        public static readonly ReadOnlyCollection<string> RegistryKeys = new ReadOnlyCollection<string>(new[]
        {
            @"Software\Neo\Empire Earth",
            @"Software\SSSI\Empire Earth"
        });

        /// <summary>Registry value with the drive of the game folder, e.g. "C:".</summary>
        public const string VolumeValueName = "Installed From Volume";

        /// <summary>Registry value with the game folder without the drive, e.g. "\GAMES\Empire Earth\".</summary>
        public const string DirectoryValueName = "Installed From Directory";

        /// <summary>Executable of the game, used to recognize a game folder.</summary>
        public const string GameExecutableName = "Empire Earth.exe";

        /// <summary>
        /// Finds the game folder: the folder chosen by the user if there is one, otherwise the registered
        /// installation, otherwise the launcher folder if the game is installed there.
        /// </summary>
        /// <param name="userDirectory">Folder chosen by the user (empty for automatic detection). It is used
        /// even if it does not exist (any more), so that the user sees the configured value.</param>
        /// <param name="source">Where the folder comes from.</param>
        /// <returns>Full path of the game folder, or null if none was found.</returns>
        public string Locate(string userDirectory, out GameDirectorySource source)
        {
            if (!string.IsNullOrWhiteSpace(userDirectory))
            {
                source = GameDirectorySource.UserSetting;
                return userDirectory.Trim();
            }

            string registered = FindRegisteredDirectory();
            if (registered != null)
            {
                source = GameDirectorySource.Registry;
                return registered;
            }

            if (IsGameDirectory(LauncherPaths.ApplicationDirectory))
            {
                source = GameDirectorySource.LauncherFolder;
                return Path.GetFullPath(LauncherPaths.ApplicationDirectory);
            }

            source = GameDirectorySource.NotFound;
            return null;
        }

        /// <summary>True if <paramref name="directory"/> contains the game executable.</summary>
        public static bool IsGameDirectory(string directory)
        {
            try
            {
                return !string.IsNullOrEmpty(directory) && File.Exists(Path.Combine(directory, GameExecutableName));
            }
            catch (ArgumentException)
            {
                return false; // invalid characters in a path from the settings or the registry
            }
        }

        private string FindRegisteredDirectory()
        {
            RegistryView[] views = Environment.Is64BitOperatingSystem
                ? new[] { RegistryView.Registry32, RegistryView.Registry64 }
                : new[] { RegistryView.Default };

            foreach (RegistryHive hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
            {
                foreach (RegistryView view in views)
                {
                    foreach (string key in RegistryKeys)
                    {
                        string directory = CombineInstallLocation(ReadRegistryValue(hive, view, key, VolumeValueName),
                            ReadRegistryValue(hive, view, key, DirectoryValueName));
                        if (directory != null && Directory.Exists(directory))
                            return directory;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Joins the two registry values into a full path, e.g. "C:" and "\GAMES\Empire Earth\" into
        /// "C:\GAMES\Empire Earth". Path.Combine cannot be used: it drops the drive of a rooted folder.
        /// </summary>
        /// <returns>null if a value is missing or the result is not a valid path.</returns>
        internal static string CombineInstallLocation(string volume, string directory)
        {
            if (string.IsNullOrWhiteSpace(volume) || string.IsNullOrWhiteSpace(directory))
                return null;
            string combined = volume.Trim().TrimEnd('\\', '/') + "\\" + directory.Trim().TrimStart('\\', '/');
            try
            {
                return Path.GetFullPath(combined).TrimEnd('\\', '/');
            }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is PathTooLongException)
            {
                return null;
            }
        }

        /// <summary>Reads a string value; null if the key or value does not exist or cannot be read.</summary>
        protected virtual string ReadRegistryValue(RegistryHive hive, RegistryView view, string keyName, string valueName)
        {
            try
            {
                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, view))
                using (RegistryKey key = baseKey.OpenSubKey(keyName))
                {
                    return key == null ? null : key.GetValue(valueName) as string;
                }
            }
            catch (Exception ex) when (ex is SecurityException || ex is UnauthorizedAccessException || ex is IOException)
            {
                return null;
            }
        }
    }
}

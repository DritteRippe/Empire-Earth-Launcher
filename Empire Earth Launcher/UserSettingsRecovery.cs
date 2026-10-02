using System;
using System.Configuration;
using System.IO;
using Empire_Earth_Launcher.Core.Logging;

namespace Empire_Earth_Launcher
{
    /// <summary>
    /// Start-up check of the user settings (user.config below %LOCALAPPDATA%). A file left half written by a
    /// crash or power loss during Settings.Save, or edited by hand, makes every access to any setting throw a
    /// <see cref="ConfigurationErrorsException"/>, also for application-scoped settings, because the settings
    /// provider loads all values at once. Such a file is moved aside and the defaults are used, as most
    /// .NET Framework applications do.
    /// </summary>
    internal static class UserSettingsRecovery
    {
        /// <summary>Suffix of the copy of a damaged user settings file, kept for diagnosis.</summary>
        public const string DamagedFileSuffix = ".damaged";

        /// <summary>
        /// Loads the settings. If the user settings file is damaged, it is renamed to
        /// "user.config<see cref="DamagedFileSuffix"/>" and the settings are reloaded with their defaults.
        /// Everything is logged; nothing is thrown for configuration or file errors.
        /// </summary>
        /// <param name="settings">Settings to check.</param>
        /// <param name="probePropertyName">Any setting; reading it loads all values of the provider.</param>
        /// <param name="logger">Log of the launcher.</param>
        /// <returns>
        /// true if the settings can be read now; false if they stay unreadable for this session (e.g. a damaged
        /// application config file, which is never touched). Callers must still be prepared for
        /// <see cref="ConfigurationException"/> then.
        /// </returns>
        public static bool EnsureReadable(ApplicationSettingsBase settings, string probePropertyName, ILogger logger)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));
            if (string.IsNullOrEmpty(probePropertyName))
                throw new ArgumentException("A setting name is required.", nameof(probePropertyName));
            if (logger == null)
                throw new ArgumentNullException(nameof(logger));

            ConfigurationException error = TryLoad(settings, probePropertyName);
            if (error == null)
                return true;

            string damagedFile = FindDamagedFile(error);
            if (damagedFile == null ||
                IsApplicationConfigFile(damagedFile, AppDomain.CurrentDomain.SetupInformation.ConfigurationFile) ||
                !File.Exists(damagedFile))
            {
                logger.Error("Unable to read the launcher settings; the defaults are used where possible.", error);
                return false;
            }

            string backupFile;
            try
            {
                backupFile = MoveAside(damagedFile);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                logger.Error("Unable to read the launcher settings (" + damagedFile + ").", error);
                logger.Error("Unable to move the damaged settings file aside; the defaults are used where possible.", ex);
                return false;
            }

            logger.Error("The launcher settings were damaged and have been reset to their defaults. The damaged file " +
                         "was kept as " + backupFile + ".", error);
            settings.Reload();
            error = TryLoad(settings, probePropertyName);
            if (error == null)
                return true;
            logger.Error("Unable to read the launcher settings after resetting them.", error);
            return false;
        }

        private static ConfigurationException TryLoad(ApplicationSettingsBase settings, string probePropertyName)
        {
            try
            {
                GC.KeepAlive(settings[probePropertyName]);
                return null;
            }
            catch (ConfigurationException ex)
            {
                return ex;
            }
        }

        /// <summary>
        /// The configuration file named by <paramref name="exception"/> or one of its inner exceptions (the
        /// settings provider wraps the error of the file), or null.
        /// </summary>
        internal static string FindDamagedFile(Exception exception)
        {
            for (Exception current = exception; current != null; current = current.InnerException)
            {
                var configurationError = current as ConfigurationErrorsException;
                if (configurationError != null && !string.IsNullOrEmpty(configurationError.Filename))
                    return configurationError.Filename;
            }
            return null;
        }

        /// <summary>
        /// True if <paramref name="file"/> is the application config file next to the executable. It is part of
        /// the installation (server settings) and must never be renamed.
        /// </summary>
        internal static bool IsApplicationConfigFile(string file, string applicationConfigFile)
        {
            if (string.IsNullOrEmpty(file))
                throw new ArgumentException("A file name is required.", nameof(file));
            return !string.IsNullOrEmpty(applicationConfigFile) &&
                   string.Equals(Path.GetFullPath(file), Path.GetFullPath(applicationConfigFile),
                       StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Renames <paramref name="file"/> to <paramref name="file"/> + <see cref="DamagedFileSuffix"/>,
        /// replacing an older copy.
        /// </summary>
        /// <returns>Full path of the copy.</returns>
        internal static string MoveAside(string file)
        {
            string backupFile = file + DamagedFileSuffix;
            if (File.Exists(backupFile))
                File.Delete(backupFile);
            File.Move(file, backupFile);
            return backupFile;
        }
    }
}

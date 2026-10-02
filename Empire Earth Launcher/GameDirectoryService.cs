using System;
using Empire_Earth_Launcher.Core.Logging;
using Empire_Earth_Launcher.Core.Settings;

namespace Empire_Earth_Launcher
{
    /// <summary>
    /// The Empire Earth folder the launcher works with: the folder chosen by the user in the launcher
    /// settings (GameDirectory in settings.json), otherwise the detected installation.
    /// </summary>
    /// <remarks>Created once by <see cref="Program"/>; use it on the UI thread.</remarks>
    internal sealed class GameDirectoryService
    {
        private readonly ILogger logger;
        private readonly SettingsStore settings;
        private readonly GameDirectoryLocator locator;

        public GameDirectoryService(ILogger logger, SettingsStore settings, GameDirectoryLocator locator)
        {
            if (logger == null)
                throw new ArgumentNullException(nameof(logger));
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));
            if (locator == null)
                throw new ArgumentNullException(nameof(locator));
            this.logger = logger;
            this.settings = settings;
            this.locator = locator;
        }

        /// <summary>Raised after <see cref="Location"/> or <see cref="Source"/> changed.</summary>
        public event EventHandler Changed;

        /// <summary>Full path of the game folder, or null if no installation was found.</summary>
        public string Location { get; private set; }

        public GameDirectorySource Source { get; private set; }

        /// <summary>Determines the game folder again (setting first, then detection).</summary>
        public void Refresh()
        {
            string userDirectory = settings.Current.GameDirectory;
            GameDirectorySource source;
            string location = locator.Locate(userDirectory, out source);
            if (location == Location && source == Source)
                return;

            Location = location;
            Source = source;
            if (location == null)
                logger.Warning("No Empire Earth installation found. Choose the game folder in the launcher settings.");
            else
                logger.Info("Empire Earth folder: " + location + " (" + source + ")");
            Changed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Saves the folder chosen by the user, or automatic detection for null/empty, and applies it.
        /// </summary>
        public void SetUserDirectory(string directory)
        {
            settings.Current.GameDirectory = string.IsNullOrWhiteSpace(directory) ? string.Empty : directory.Trim();
            // If saving fails (logged by the store), the folder is still used for this session.
            settings.Save();
            Refresh();
        }
    }
}

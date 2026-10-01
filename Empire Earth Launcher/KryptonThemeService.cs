using Krypton.Toolkit;
using Empire_Earth_Launcher.Properties;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Empire_Earth_Launcher
{
    /// <summary>
    /// <see cref="IThemeService"/> for Krypton palettes exported as XML files.
    /// </summary>
    internal sealed class KryptonThemeService : IThemeService
    {
        /// <summary>File extension of the theme files.</summary>
        public const string ThemeFileExtension = ".xml";

        private readonly ILogger logger;
        private readonly string themesDirectory;
        private readonly Dictionary<KryptonPalette, Control> registeredPalettes = new Dictionary<KryptonPalette, Control>();
        private string currentThemeFile;

        /// <param name="logger">Receives the reasons why a theme cannot be applied.</param>
        /// <param name="themesDirectory">Folder of the themes that <see cref="ApplyTheme"/> finds by name.</param>
        public KryptonThemeService(ILogger logger, string themesDirectory)
        {
            if (logger == null)
                throw new ArgumentNullException(nameof(logger));
            if (string.IsNullOrEmpty(themesDirectory))
                throw new ArgumentException("The themes folder is missing.", nameof(themesDirectory));
            this.logger = logger;
            this.themesDirectory = themesDirectory;
        }

        public string CurrentThemeFile
        {
            get { return currentThemeFile; }
        }

        public IList<string> GetAvailableThemeNames()
        {
            try
            {
                if (!Directory.Exists(themesDirectory))
                    return new List<string>();
                // On Windows the pattern "*.xml" also matches longer extensions such as ".xmlx".
                return Directory.GetFiles(themesDirectory, "*" + ThemeFileExtension)
                    .Where(file => Path.GetExtension(file).Equals(ThemeFileExtension, StringComparison.OrdinalIgnoreCase))
                    .Select(Path.GetFileNameWithoutExtension)
                    .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                logger.Warning("Unable to list the themes in " + themesDirectory + ".", ex);
                return new List<string>();
            }
        }

        public void Register(KryptonPalette palette, Control control)
        {
            if (palette == null)
                throw new ArgumentNullException(nameof(palette));
            if (control == null)
                throw new ArgumentNullException(nameof(control));
            if (registeredPalettes.ContainsKey(palette))
                return;

            registeredPalettes.Add(palette, control);
            // Without this, closed dialogs stayed in the list forever and later theme changes were applied to
            // disposed windows.
            control.Disposed += (sender, e) => Unregister(palette);

            if (control is Form form)
                form.Icon = Resources.EmpireEarthLauncher;
            if (currentThemeFile != null)
                TryImport(palette, control, currentThemeFile);
        }

        public void Unregister(KryptonPalette palette)
        {
            if (palette != null)
                registeredPalettes.Remove(palette);
        }

        public bool ApplyTheme(string themeName)
        {
            if (string.IsNullOrEmpty(themeName) || themeName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                logger.Warning("Invalid theme name \"" + themeName + "\".");
                return false;
            }
            return ApplyThemeFile(Path.Combine(themesDirectory, themeName + ThemeFileExtension));
        }

        public bool ApplyThemeFile(string themeFilePath)
        {
            if (string.IsNullOrEmpty(themeFilePath))
                throw new ArgumentException("The theme file is missing.", nameof(themeFilePath));

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(themeFilePath);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is PathTooLongException)
            {
                logger.Warning("Invalid theme file path \"" + themeFilePath + "\".", ex);
                return false;
            }

            if (!File.Exists(fullPath))
            {
                logger.Warning("Unable to find the theme file " + fullPath + ".");
                return false;
            }

            // Check the file once on a scratch palette, so that an invalid file changes nothing instead of
            // leaving some windows with the new theme and others with a half imported one.
            if (!CanImport(fullPath))
                return false;

            currentThemeFile = fullPath;
            // ToList: importing must not run while the dictionary could change.
            foreach (KeyValuePair<KryptonPalette, Control> registration in registeredPalettes.ToList())
                TryImport(registration.Key, registration.Value, fullPath);
            return true;
        }

        private bool CanImport(string themeFile)
        {
            using (var scratchPalette = new KryptonPalette())
            {
                return TryImportPalette(scratchPalette, themeFile);
            }
        }

        private void TryImport(KryptonPalette palette, Control control, string themeFile)
        {
            if (TryImportPalette(palette, themeFile))
                control.BackColor = palette.FormStyles.FormCommon.StateCommon.Back.Color1;
        }

        private bool TryImportPalette(KryptonPalette palette, string themeFile)
        {
            try
            {
                // Silent: the interactive overload shows a message box for every imported palette.
                palette.Import(themeFile, true);
                return true;
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException))
            {
                // Krypton reports invalid files with various exception types (XML, format, argument, I/O...).
                logger.Warning("Unable to load the theme file " + themeFile + ".", ex);
                return false;
            }
        }
    }
}

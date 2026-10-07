using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Krypton.Toolkit;

namespace Empire_Earth_Launcher.Tests.Fakes
{
    /// <summary>
    /// <see cref="IThemeService"/> with the themes of a list: applying one records it and makes it the current theme file
    /// (below <see cref="ThemesDirectory"/>); palettes are not touched.
    /// </summary>
    internal sealed class FakeThemeService : IThemeService
    {
        public const string ThemesDirectory = @"C:\Launcher\themes";

        private readonly List<string> themeNames;

        public FakeThemeService(params string[] themeNames)
        {
            this.themeNames = themeNames.ToList();
        }

        /// <summary>The theme names and files applied, in order.</summary>
        public List<string> Applied { get; } = new List<string>();

        /// <summary>Theme files that cannot be loaded.</summary>
        public HashSet<string> BrokenFiles { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public string CurrentThemeFile { get; private set; }

        public IList<string> GetAvailableThemeNames()
        {
            return themeNames.ToList();
        }

        public void Register(KryptonPalette palette, Control control)
        {
        }

        public void Unregister(KryptonPalette palette)
        {
        }

        public bool ApplyTheme(string themeName)
        {
            if (!themeNames.Contains(themeName, StringComparer.OrdinalIgnoreCase))
                return false;
            Applied.Add(themeName);
            CurrentThemeFile = ThemesDirectory + @"\" + themeName + ".xml";
            return true;
        }

        public bool ApplyThemeFile(string themeFilePath)
        {
            if (BrokenFiles.Contains(themeFilePath))
                return false;
            Applied.Add(themeFilePath);
            CurrentThemeFile = themeFilePath;
            return true;
        }
    }
}

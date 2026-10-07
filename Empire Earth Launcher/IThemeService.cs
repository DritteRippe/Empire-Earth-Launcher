using System.Collections.Generic;
using System.Windows.Forms;
using Krypton.Toolkit;

namespace Empire_Earth_Launcher
{
    /// <summary>
    /// Applies the launcher theme (a Krypton palette file) to every registered window and control.
    /// </summary>
    /// <remarks>
    /// Created once by <see cref="Program"/> and passed to the windows and controls that have a palette.
    /// All members must be called on the UI thread.
    /// </remarks>
    public interface IThemeService
    {
        /// <summary>
        /// Full path of the theme file applied to the registered palettes, or null while no theme file could be
        /// applied (the palettes then keep the values set in the designer).
        /// </summary>
        string CurrentThemeFile { get; }

        /// <summary>Names of the themes in the launcher's themes folder (file names without ".xml").</summary>
        IList<string> GetAvailableThemeNames();

        /// <summary>
        /// Registers the palette of <paramref name="control"/>, applies the current theme to it and keeps it up
        /// to date until the control is disposed (it is unregistered automatically then).
        /// </summary>
        void Register(KryptonPalette palette, Control control);

        /// <summary>Stops updating the palette. Does nothing if it is not registered.</summary>
        void Unregister(KryptonPalette palette);

        /// <summary>Applies the theme <paramref name="themeName"/> from the launcher's themes folder.</summary>
        /// <returns>false if the theme file is missing or invalid (the reason is logged); nothing changes then.</returns>
        bool ApplyTheme(string themeName);

        /// <summary>Applies the theme file <paramref name="themeFilePath"/>.</summary>
        /// <returns>false if the theme file is missing or invalid (the reason is logged); nothing changes then.</returns>
        bool ApplyThemeFile(string themeFilePath);
    }
}

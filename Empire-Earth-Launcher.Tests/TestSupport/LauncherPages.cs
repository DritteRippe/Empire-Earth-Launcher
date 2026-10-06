using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using Krypton.Toolkit;

namespace Empire_Earth_Launcher.Tests.TestSupport
{
    /// <summary>
    /// The four pages of the launcher as the main window holds them (hidden, in a window that is not shown), for the geometry
    /// tests and the page pictures of the CI run.
    /// </summary>
    internal static class LauncherPages
    {
        public const string Play = "Play";
        public const string GameSettings = "Game settings";
        public const string Tools = "Tools";
        public const string Launcher = "Launcher";

        /// <summary>
        /// The width the navigation bar takes from the client area of the main window (the pages start at x = 126 today, the
        /// window has 4 px of margin); a page gets the rest.
        /// </summary>
        public const int NavigationWidth = 130;

        /// <summary>The size of a page in today's smallest window: the client area of 684 x 381 without the navigation bar.</summary>
        public static readonly Size MinimumPageSize = new Size(554, 380);

        public static IEnumerable<string> Names
        {
            get { return new[] { Play, GameSettings, Tools, Launcher }; }
        }

        /// <summary>The page sizes of the window sizes of the geometry tests: the minimum, 800 x 500, 1024 x 640 and 1920 x 1080.</summary>
        public static IEnumerable<Size> PageSizes
        {
            get
            {
                yield return MinimumPageSize;
                foreach (Size window in new[] { new Size(800, 500), new Size(1024, 640), new Size(1920, 1080) })
                    yield return PageSizeOf(window);
            }
        }

        /// <summary>The page size in a window with the client area <paramref name="window"/>.</summary>
        public static Size PageSizeOf(Size window)
        {
            return new Size(window.Width - NavigationWidth, window.Height - 1);
        }

        /// <summary>A new page, not initialized and not shown.</summary>
        public static Control Create(string name)
        {
            switch (name)
            {
                case Play:
                    return new GeneralUserControl();
                case GameSettings:
                    return new SettingsUserControl();
                case Tools:
                    return new ToolsUserControl();
                case Launcher:
                    return new LauncherSettingsUserControl();
                default:
                    throw new ArgumentException("Unknown page: " + name, nameof(name));
            }
        }

        /// <summary>
        /// Puts <paramref name="page"/> into a window that is never shown and hides it, as <c>MainForm</c> holds its pages while
        /// it is constructed: <see cref="Control.Visible"/> reads false for the page and everything on it.
        /// </summary>
        public static Form Host(Control page)
        {
            var window = new Form { ShowInTaskbar = false };
            page.Visible = false;
            window.Controls.Add(page);
            return window;
        }

        /// <summary>
        /// Gives the page the size <paramref name="size"/> and lays it out. Outside Windows the designer sizes of the pages are
        /// scaled by the font of Mono (7 x 14 per character instead of 6 x 13) while the constants of the layout code are not,
        /// so a page is never made smaller than it is after construction there.
        /// </summary>
        public static void Resize(Control page, Size size, Size constructedSize)
        {
            bool windows = Environment.OSVersion.Platform == PlatformID.Win32NT;
            page.Size = windows ? size : new Size(Math.Max(size.Width, constructedSize.Width), Math.Max(size.Height, constructedSize.Height));
            LayoutAll(page);
        }

        /// <summary>Lays out <paramref name="control"/> and everything below it.</summary>
        public static void LayoutAll(Control control)
        {
            foreach (Control child in control.Controls)
                LayoutAll(child);
            control.PerformLayout();
        }

        /// <summary>
        /// Makes the Krypton fonts of <paramref name="page"/> <paramref name="factor"/> times as large, as Windows does with "Text
        /// size" in the accessibility settings: call it after the page is created and before it receives its state, as the
        /// launcher starts with that setting. Only the palette of the page changes, never the one shared by all pages.
        /// </summary>
        public static void ScaleFonts(Control page, float factor)
        {
            if (Math.Abs(factor - 1f) < 0.001f)
                return;
            FieldInfo field = page.GetType().GetField("launcherKryptonPalette", BindingFlags.Instance | BindingFlags.NonPublic);
            var palette = field?.GetValue(page) as KryptonPalette;
            if (palette == null)
                throw new InvalidOperationException(page.GetType().Name + " has no launcherKryptonPalette.");
            Font current = palette.GetContentShortTextFont(PaletteContentStyle.LabelNormalControl, PaletteState.Normal);
            // The common styles are the parents of every label and button style; the fonts belong to the palette, which keeps them.
            palette.LabelStyles.LabelCommon.StateCommon.ShortText.Font = new Font(current.FontFamily, current.Size * factor);
            palette.ButtonStyles.ButtonCommon.StateCommon.Content.ShortText.Font = new Font(current.FontFamily, current.Size * factor);
        }
    }
}

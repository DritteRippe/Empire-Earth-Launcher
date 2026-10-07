using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Empire_Earth_Launcher.Tests.TestSupport;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Launcher
{
    /// <summary>
    /// Pictures of the six pages of the launcher, as PNG files, for the CI run on Windows (1.1.0, ADR 0012 amendment): the
    /// artifact <c>page-pictures</c> of the build shows what the geometry tests of <see cref="PageLayoutTests"/> measured, and
    /// replaces the screenshot of the player when a layout has to be judged by eye.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The tests do nothing unless the environment variable <see cref="FolderVariable"/> names a folder and the program runs on
    /// Windows (they are ignored otherwise), so the test program on the laptop and the run with <c>xvfb-run</c> show no window.
    /// With the variable set, each test shows the page for a moment in a borderless window in the upper left corner of the
    /// screen and saves two pictures of it: <c>name.png</c> with <see cref="Control.DrawToBitmap"/> and <c>name.screen.png</c>
    /// copied from the screen (the first has no window frame, the second is the real paint; whichever the runner supports).
    /// </para>
    /// <para>
    /// The pages are filled as the launcher fills them (<see cref="SettingsPageWorld"/>, <see cref="PlayPageWorld"/> and
    /// <see cref="LauncherPageWorld"/>: hidden first, shown afterwards); the Game settings, Play and Launcher pages in each of
    /// their states (the Graphics and Mods pages too: <see cref="GraphicsPageWorld"/>, <see cref="ModsPageWorld"/>), English and German, the smallest size and 1024 x 640,
    /// and once with fonts 50 % larger; the Tools page as
    /// the designer made it. Mono cannot create a window handle for these pages, so the pictures exist on Windows only.
    /// </para>
    /// </remarks>
    [TestFixture]
    [Category(TestCategories.WinForms)]
    [Apartment(ApartmentState.STA)]
    public class PageScreenshotTests
    {
        /// <summary>The folder the pictures are written to; the tests are ignored while it is not set.</summary>
        public const string FolderVariable = "EE_LAUNCHER_PAGE_PNG_DIR";

        private static readonly string[] PictureLanguages = { "en", "de" };

        private IDisposable language;

        [TearDown]
        public void TearDown()
        {
            language?.Dispose();
            language = null;
        }

        private static string PictureFolder()
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                Assert.Ignore("The page pictures need a window handle, which the Krypton controls only get on Windows.");
            string folder = Environment.GetEnvironmentVariable(FolderVariable);
            if (string.IsNullOrEmpty(folder))
                Assert.Ignore("Set " + FolderVariable + " to a folder to write the pictures of the pages (the CI build does).");
            Directory.CreateDirectory(folder);
            return folder;
        }

        /// <summary>The page sizes of the pictures: the smallest window and 1024 x 640.</summary>
        private static IEnumerable<Size> PictureSizes()
        {
            yield return LauncherPages.MinimumPageSize;
            yield return LauncherPages.PageSizeOf(new Size(1024, 640));
        }

        public static IEnumerable<TestCaseData> Languages()
        {
            foreach (string lang in PictureLanguages)
                yield return new TestCaseData(lang).SetArgDisplayNames(lang);
        }

        public static IEnumerable<TestCaseData> OtherPages()
        {
            foreach (string page in new[] { LauncherPages.Tools })
                foreach (string lang in PictureLanguages)
                    yield return new TestCaseData(page, lang).SetArgDisplayNames(page, lang);
        }

        [TestCaseSource(nameof(Languages))]
        public void GameSettingsPage_EveryState_IsSavedAsPicture(string lang)
        {
            string folder = PictureFolder();
            language = TestUiLanguage.Use(lang);
            foreach (SettingsPageState state in Enum.GetValues(typeof(SettingsPageState)))
            {
                foreach (float scale in new[] { 1f, 1.5f })
                {
                    if (scale > 1f && state != SettingsPageState.QuestionAndHint)
                        continue;
                    using (SettingsPageWorld world = SettingsPageWorld.In(state, true, scale))
                    {
                        foreach (Size size in PictureSizes())
                        {
                            string name = Name(LauncherPages.GameSettings, state.ToString(), lang, size, scale);
                            SavePictures(world.Window, world.Page, size, world.ConstructedSize, folder, name);
                        }
                    }
                }
            }
        }

        [TestCaseSource(nameof(Languages))]
        public void PlayPage_EveryState_IsSavedAsPicture(string lang)
        {
            string folder = PictureFolder();
            language = TestUiLanguage.Use(lang);
            foreach (PlayPageState state in Enum.GetValues(typeof(PlayPageState)))
            {
                foreach (float scale in new[] { 1f, 1.5f })
                {
                    if (scale > 1f && state != PlayPageState.LongTextsAndInfoBar)
                        continue;
                    using (PlayPageWorld world = PlayPageWorld.In(state, true, scale))
                    {
                        foreach (Size size in PictureSizes())
                        {
                            string name = Name(LauncherPages.Play, state.ToString(), lang, size, scale);
                            SavePictures(world.Window, world.Page, size, world.ConstructedSize, folder, name);
                        }
                    }
                }
            }
        }

        [TestCaseSource(nameof(Languages))]
        public void LauncherPage_EveryState_IsSavedAsPicture(string lang)
        {
            string folder = PictureFolder();
            language = TestUiLanguage.Use(lang);
            foreach (LauncherPageState state in Enum.GetValues(typeof(LauncherPageState)))
            {
                foreach (float scale in new[] { 1f, 1.5f })
                {
                    if (scale > 1f && state != LauncherPageState.LongTexts)
                        continue;
                    using (LauncherPageWorld world = LauncherPageWorld.In(state, true, scale))
                    {
                        foreach (Size size in PictureSizes())
                        {
                            string name = Name(LauncherPages.Launcher, state.ToString(), lang, size, scale);
                            SavePictures(world.Window, world.Page, size, world.ConstructedSize, folder, name);
                        }
                    }
                }
            }
        }

        [TestCaseSource(nameof(Languages))]
        public void GraphicsPage_EveryState_IsSavedAsPicture(string lang)
        {
            string folder = PictureFolder();
            language = TestUiLanguage.Use(lang);
            foreach (GraphicsPageState state in Enum.GetValues(typeof(GraphicsPageState)))
            {
                foreach (float scale in new[] { 1f, 1.5f })
                {
                    if (scale > 1f && state != GraphicsPageState.DgVoodoo)
                        continue;
                    using (GraphicsPageWorld world = GraphicsPageWorld.In(state, true, scale))
                    {
                        foreach (Size size in PictureSizes())
                        {
                            string name = Name(LauncherPages.Graphics, state.ToString(), lang, size, scale);
                            SavePictures(world.Window, world.Page, size, world.ConstructedSize, folder, name);
                        }
                    }
                }
            }
        }

        [TestCaseSource(nameof(Languages))]
        public void ModsPage_EveryState_IsSavedAsPicture(string lang)
        {
            string folder = PictureFolder();
            language = TestUiLanguage.Use(lang);
            foreach (ModsPageState state in Enum.GetValues(typeof(ModsPageState)))
            {
                foreach (float scale in new[] { 1f, 1.5f })
                {
                    if (scale > 1f && state != ModsPageState.Everything)
                        continue;
                    using (ModsPageWorld world = ModsPageWorld.In(state, true, scale))
                    {
                        foreach (Size size in PictureSizes())
                        {
                            string name = Name(LauncherPages.Mods, state.ToString(), lang, size, scale);
                            SavePictures(world.Window, world.Page, size, world.ConstructedSize, folder, name);
                        }
                    }
                }
            }
        }

        [TestCaseSource(nameof(OtherPages))]
        public void OtherPage_IsSavedAsPicture(string pageName, string lang)
        {
            string folder = PictureFolder();
            language = TestUiLanguage.Use(lang);
            using (Control page = LauncherPages.Create(pageName))
            using (Form window = LauncherPages.Host(page))
            {
                foreach (Size size in PictureSizes())
                    SavePictures(window, page, size, page.Size, folder, Name(pageName, "designer", lang, size, 1f));
            }
        }

        private static string Name(string page, string state, string lang, Size size, float fontScale)
        {
            return (page + "_" + state + "_" + lang + "_" + size.Width + "x" + size.Height + (fontScale > 1f ? "_font150" : ""))
                .Replace(' ', '-');
        }

        /// <summary>
        /// Shows <paramref name="window"/> with the page at <paramref name="size"/> and saves <c>name.png</c> and
        /// <c>name.screen.png</c> into <paramref name="folder"/>.
        /// </summary>
        private static void SavePictures(Form window, Control page, Size size, Size constructedSize, string folder, string name)
        {
            // Program.Main sets compatible text rendering: the labels draw with GDI+ like in the launcher.
            foreach (Label label in WinForms.Descendants(page).OfType<Label>())
                label.UseCompatibleTextRendering = true;
            window.FormBorderStyle = FormBorderStyle.None;
            window.StartPosition = FormStartPosition.Manual;
            window.Location = Point.Empty;
            window.TopMost = true;
            window.ClientSize = size;
            page.Location = Point.Empty;
            if (!window.Visible)
                window.Show();
            page.Visible = true;
            LauncherPages.Resize(page, size, constructedSize);
            Application.DoEvents();

            using (var bitmap = new Bitmap(size.Width, size.Height))
            {
                page.DrawToBitmap(bitmap, new Rectangle(Point.Empty, size));
                bitmap.Save(Path.Combine(folder, name + ".png"), ImageFormat.Png);
            }
            try
            {
                using (var bitmap = new Bitmap(size.Width, size.Height))
                using (Graphics graphics = Graphics.FromImage(bitmap))
                {
                    graphics.CopyFromScreen(window.PointToScreen(Point.Empty), Point.Empty, size);
                    bitmap.Save(Path.Combine(folder, name + ".screen.png"), ImageFormat.Png);
                }
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // A runner without a desktop to copy from: the DrawToBitmap picture is all there is.
            }
        }
    }
}

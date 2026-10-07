using System.IO;
using System.Linq;
using Empire_Earth_Launcher.Core.Logging;
using Empire_Earth_Launcher.Core.Settings;
using Empire_Earth_Launcher.Tests.Fakes;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Launcher
{
    /// <summary>
    /// The theme list of the Launcher page and the theme of the start (bug report of 2026-10-03: without theme files, which
    /// are not shipped yet, the list "Design" showed nothing). The first item is now "Built-in colors", shown while no theme
    /// file is applied; choosing it while one is applied saves <see cref="LauncherSettings.BuiltInThemeName"/> for the next
    /// start.
    /// </summary>
    [TestFixture]
    public class ThemeChoiceTests
    {
        private const string ThemesDirectory = FakeThemeService.ThemesDirectory;
        private static readonly string[] ThemeNames = { "Dark", "Light" };

        private static int Index(string appliedThemeFile, bool builtInColorsChosen = false)
        {
            return LauncherSettingsUserControl.ThemeListIndex(appliedThemeFile, builtInColorsChosen, ThemesDirectory,
                ThemeNames.ToList());
        }

        [Test]
        public void WithoutAThemeFile_TheListShowsTheBuiltInColors()
        {
            Assert.That(Index(null), Is.EqualTo(LauncherSettingsUserControl.BuiltInThemeIndex));
            Assert.That(LauncherSettingsUserControl.ThemeListIndex(null, false, ThemesDirectory, new string[0]),
                Is.EqualTo(LauncherSettingsUserControl.BuiltInThemeIndex), "also without any theme in the folder");
        }

        [Test]
        public void AThemeOfTheFolder_IsShownByItsName()
        {
            Assert.That(Index(Path.Combine(ThemesDirectory, "Light.xml")), Is.EqualTo(LauncherSettingsUserControl.FirstThemeIndex + 1));
            Assert.That(Index(Path.Combine(ThemesDirectory, "dark.xml")), Is.EqualTo(LauncherSettingsUserControl.FirstThemeIndex));
        }

        [Test]
        public void AnotherThemeFile_IsShownAsCustomFile()
        {
            Assert.That(Index(@"D:\My themes\Gold.xml"), Is.EqualTo(LauncherSettingsUserControl.CustomThemeIndex));
            Assert.That(Index(Path.Combine(ThemesDirectory, "Added later.xml")), Is.EqualTo(LauncherSettingsUserControl.CustomThemeIndex),
                "a file of the folder that is not in the list");
        }

        [Test]
        public void TheBuiltInColorsChosenForTheNextStart_AreShownAlready()
        {
            Assert.That(Index(Path.Combine(ThemesDirectory, "Dark.xml"), builtInColorsChosen: true),
                Is.EqualTo(LauncherSettingsUserControl.BuiltInThemeIndex));
        }

        [Test]
        public void ChoosingTheBuiltInColors_SavesThemForTheNextStart()
        {
            var settings = new LauncherSettings { ThemeName = "Dark", CustomThemeFile = @"D:\My themes\Gold.xml" };

            bool nextStart = LauncherSettingsUserControl.ChooseBuiltInColors(settings, @"D:\My themes\Gold.xml");

            Assert.That(nextStart, Is.True, "a theme file is applied: its colors stay until the next start");
            Assert.That(settings.ThemeName, Is.EqualTo(LauncherSettings.BuiltInThemeName));
            Assert.That(settings.CustomThemeFile, Is.Empty);
            Assert.That(LauncherSettingsUserControl.ChooseBuiltInColors(new LauncherSettings(), null), Is.False,
                "without a theme file the built-in colors are in use already");
        }

        [Test]
        public void TheBuiltInColors_ApplyNoThemeAtTheStart_NotEvenTheDefaultTheme()
        {
            var themes = new FakeThemeService("Light");
            var logger = new RecordingLogger();

            global::Empire_Earth_Launcher.Program.ApplySavedTheme(themes,
                new LauncherSettings { ThemeName = LauncherSettings.BuiltInThemeName }, logger);

            Assert.That(themes.Applied, Is.Empty);
            Assert.That(themes.CurrentThemeFile, Is.Null);
            Assert.That(logger.MessagesOf(LogLevel.Info), Has.Some.Contains("built-in colors"));
        }

        [Test]
        public void ACustomFileChosenLater_WinsOverTheBuiltInColors_AndTheyAreTheFallback()
        {
            var themes = new FakeThemeService("Light");
            var settings = new LauncherSettings { ThemeName = LauncherSettings.BuiltInThemeName, CustomThemeFile = @"D:\Gold.xml" };

            global::Empire_Earth_Launcher.Program.ApplySavedTheme(themes, settings, new RecordingLogger());
            Assert.That(themes.Applied, Is.EqualTo(new[] { @"D:\Gold.xml" }));

            var broken = new FakeThemeService("Light");
            broken.BrokenFiles.Add(@"D:\Gold.xml");
            global::Empire_Earth_Launcher.Program.ApplySavedTheme(broken, settings, new RecordingLogger());
            Assert.That(broken.Applied, Is.Empty, "a custom file that cannot be loaded leaves the built-in colors");
        }

        [Test]
        public void TheDefaultTheme_IsStillAppliedWhenInstalled_AndOtherwiseTheBuiltInColorsAreUsed()
        {
            var installed = new FakeThemeService("Light");
            global::Empire_Earth_Launcher.Program.ApplySavedTheme(installed, new LauncherSettings(), new RecordingLogger());
            Assert.That(installed.Applied, Is.EqualTo(new[] { "Light" }));

            var none = new FakeThemeService();
            global::Empire_Earth_Launcher.Program.ApplySavedTheme(none, new LauncherSettings(), new RecordingLogger());
            Assert.That(none.Applied, Is.Empty);
            Assert.That(none.CurrentThemeFile, Is.Null, "so the list shows \"Built-in colors\"");
        }

        [Test]
        public void TheBuiltInThemeName_CannotBeTheNameOfAThemeFile()
        {
            // Path.GetInvalidFileNameChars of Windows; Mono only lists '\0' and '/'.
            char[] invalidOnWindows = { '<', '>', ':', '"', '/', '\\', '|', '?', '*' };

            Assert.That(LauncherSettings.BuiltInThemeName.IndexOfAny(invalidOnWindows), Is.GreaterThanOrEqualTo(0));
        }
    }
}

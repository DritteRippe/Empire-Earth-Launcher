using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Empire_Earth_Launcher.Tests.TestSupport;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Launcher
{
    /// <summary>
    /// The geometry of the five pages of the launcher (1.1.0, ADR 0012 amendment): at the smallest window, 800 x 500, 1024 x 640
    /// and 1920 x 1080, in English, German and French, with the fonts of the system and 50 % larger, no two controls overlap,
    /// nothing sticks out of its page, every text has room, and the content grows with the page.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The bug report of 2026-10-06 (screenshots of the normal and the maximized window) showed the header, the description and
    /// the "NeoEE in ..." line of the Game settings page on top of each other, the book picture of the compatibility warning over
    /// the two buttons, and content that stays at the left when the window is maximized. The first two had one cause, found by
    /// these tests: the main window creates its pages hidden and fills them before it is shown, and the page skipped every
    /// control that it had not set visible itself, because <see cref="Control.Visible"/> reads false then; so the header, the
    /// installation line and the two buttons kept the places of the designer while everything else was stacked from the top
    /// (the buttons ended up under the book picture). The tests create the pages like that (<see cref="LauncherPages.Host"/>) and
    /// drive the Game settings page through its real model in the states of <see cref="SettingsPageState"/>
    /// (<see cref="SettingsPageWorld"/>); <see cref="GameSettingsPage_FilledBeforeItIsShown_NothingOverlaps"/> failed on all of
    /// them before that was fixed.
    /// </para>
    /// <para>
    /// The rules are in <see cref="LayoutChecker"/>. Under Mono they are logic checks on the fonts of Mono; only the Game settings
    /// page can be created there, the other three are ignored (<see cref="WinForms.CreateOrIgnore{T}"/>) and run on Windows (CI,
    /// laptop). The authoritative run is the one on Windows. The pictures of the pages are made by
    /// <see cref="PageScreenshotTests"/>.
    /// </para>
    /// <para>
    /// All five pages keep every rule at every size (the layout work of 1.1.0, ADR 0017: the pages stack by text height for the
    /// width of the window); no rule is excused for a page, except the few controls that <see cref="LayoutChecker"/> names with
    /// their reason. The Play page and the Launcher page are driven through the states of <see cref="PlayPageState"/> and
    /// <see cref="LauncherPageState"/>, filled with the real texts of the language (<see cref="PlayPageWorld"/>,
    /// <see cref="LauncherPageWorld"/>). A page is laid out when its size changes and when it is laid out the first time, so
    /// <see cref="LauncherPages.Resize"/> goes through another size first.
    /// </para>
    /// </remarks>
    [TestFixture]
    [Category(TestCategories.WinForms)]
    [Apartment(ApartmentState.STA)]
    public class PageLayoutTests
    {
        private const string Overlap = "overlap";
        private const string Outside = "outside";
        private const string Clipped = "clipped";
        private const string Narrow = "narrow";
        private const string NotGrowing = "not growing";

        private static readonly string[] Rules = { Overlap, Outside, Clipped, Narrow, NotGrowing };
        private static readonly string[] Languages = { "en", "de", "fr" };
        private static readonly float[] FontScales = { 1f, 1.5f };

        private IDisposable language;

        [SetUp]
        public void SetUp()
        {
            WinForms.RequireDisplay();
        }

        [TearDown]
        public void TearDown()
        {
            language?.Dispose();
            language = null;
        }

        public static IEnumerable<TestCaseData> GameSettingsScenarios()
        {
            foreach (string lang in Languages)
                foreach (float scale in FontScales)
                    foreach (SettingsPageState state in Enum.GetValues(typeof(SettingsPageState)))
                        yield return new TestCaseData(lang, scale, state).SetArgDisplayNames(lang, Fonts(scale), state.ToString());
        }

        public static IEnumerable<TestCaseData> PlayScenarios()
        {
            foreach (string lang in Languages)
                foreach (float scale in FontScales)
                    foreach (PlayPageState state in Enum.GetValues(typeof(PlayPageState)))
                        yield return new TestCaseData(lang, scale, state).SetArgDisplayNames(lang, Fonts(scale), state.ToString());
        }

        public static IEnumerable<TestCaseData> LauncherScenarios()
        {
            foreach (string lang in Languages)
                foreach (float scale in FontScales)
                    foreach (LauncherPageState state in Enum.GetValues(typeof(LauncherPageState)))
                        yield return new TestCaseData(lang, scale, state).SetArgDisplayNames(lang, Fonts(scale), state.ToString());
        }

        public static IEnumerable<TestCaseData> GraphicsScenarios()
        {
            foreach (string lang in Languages)
                foreach (float scale in FontScales)
                    foreach (GraphicsPageState state in Enum.GetValues(typeof(GraphicsPageState)))
                        yield return new TestCaseData(lang, scale, state).SetArgDisplayNames(lang, Fonts(scale), state.ToString());
        }

        public static IEnumerable<TestCaseData> OtherPageScenarios()
        {
            foreach (string page in new[] { LauncherPages.Tools })
                foreach (string lang in Languages)
                    foreach (float scale in FontScales)
                        yield return new TestCaseData(page, lang, scale).SetArgDisplayNames(page, lang, Fonts(scale));
        }

        public static IEnumerable<TestCaseData> StatesInEnglish()
        {
            foreach (SettingsPageState state in Enum.GetValues(typeof(SettingsPageState)))
                yield return new TestCaseData(state).SetArgDisplayNames(state.ToString());
        }

        private static string Fonts(float scale)
        {
            return scale > 1f ? "font +50%" : "system font";
        }

        // --- The rules at every size ------------------------------------------------------------------------------

        /// <summary>
        /// The findings of every rule for <paramref name="page"/> at every page size, by rule, each line with the size. The
        /// page is made smaller than its designer size nowhere (<see cref="LauncherPages.Resize"/>).
        /// </summary>
        private static Dictionary<string, List<string>> Evaluate(Control page, Size constructedSize)
        {
            Dictionary<string, List<string>> findings = Rules.ToDictionary(rule => rule, rule => new List<string>());
            LayoutChecker.Snapshot minimum = null;
            foreach (Size size in LauncherPages.PageSizes)
            {
                LauncherPages.Resize(page, size, constructedSize);
                string at = "at " + page.Width + "x" + page.Height + ": ";
                findings[Overlap].AddRange(LayoutChecker.Overlaps(page).Select(line => at + line));
                findings[Outside].AddRange(LayoutChecker.OutsideParent(page).Select(line => at + line));
                findings[Clipped].AddRange(LayoutChecker.ClippedWrapLabels(page).Select(line => at + line));
                findings[Narrow].AddRange(LayoutChecker.TooNarrow(page).Select(line => at + line));
                if (minimum == null)
                    minimum = LayoutChecker.Take(page);
                else
                    findings[NotGrowing].AddRange(
                        LayoutChecker.NotGrowing(page, minimum, LayoutChecker.Take(page)).Select(line => at + line));
            }
            return findings;
        }

        private static void AssertKeepsTheRules(string page, Dictionary<string, List<string>> findings)
        {
            List<string> broken = findings.SelectMany(rule => rule.Value).ToList();
            Assert.That(broken, Is.Empty, "the geometry rules of the page '" + page + "'");
        }

        [TestCaseSource(nameof(GameSettingsScenarios))]
        public void GameSettingsPage_FilledBeforeItIsShown_NothingOverlaps(string lang, float fontScale, SettingsPageState state)
        {
            language = TestUiLanguage.Use(lang);
            using (SettingsPageWorld world = WinForms.CreateOrIgnore(() => SettingsPageWorld.In(state, true, fontScale)))
                AssertKeepsTheRules(LauncherPages.GameSettings, Evaluate(world.Page, world.ConstructedSize));
        }

        /// <summary>
        /// The Play page in the states of <see cref="PlayPageState"/>, filled with the real texts of the language: on Windows
        /// only, because its Krypton combo box and list need Windows libraries to be created.
        /// </summary>
        [TestCaseSource(nameof(PlayScenarios))]
        public void PlayPage_FilledBeforeItIsShown_NothingOverlapsOrIsCutOff(string lang, float fontScale, PlayPageState state)
        {
            language = TestUiLanguage.Use(lang);
            using (PlayPageWorld world = WinForms.CreateOrIgnore(() => PlayPageWorld.In(state, true, fontScale)))
                AssertKeepsTheRules(LauncherPages.Play, Evaluate(world.Page, world.ConstructedSize));
        }

        /// <summary>
        /// The Launcher page in the states of <see cref="LauncherPageState"/>, filled with the real texts of the language: on
        /// Windows only, because its Krypton combo boxes, text box and list need Windows libraries to be created.
        /// </summary>
        [TestCaseSource(nameof(LauncherScenarios))]
        public void LauncherPage_FilledBeforeItIsShown_NothingOverlapsOrIsCutOff(string lang, float fontScale, LauncherPageState state)
        {
            language = TestUiLanguage.Use(lang);
            using (LauncherPageWorld world = WinForms.CreateOrIgnore(() => LauncherPageWorld.In(state, true, fontScale)))
                AssertKeepsTheRules(LauncherPages.Launcher, Evaluate(world.Page, world.ConstructedSize));
        }

        /// <summary>
        /// The Graphics page in the states of <see cref="GraphicsPageState"/>, filled with the real texts of the language: on
        /// Windows only, because its list of sizes is a Krypton combo box, which needs Windows libraries to be created.
        /// </summary>
        [TestCaseSource(nameof(GraphicsScenarios))]
        public void GraphicsPage_FilledBeforeItIsShown_NothingOverlapsOrIsCutOff(string lang, float fontScale, GraphicsPageState state)
        {
            language = TestUiLanguage.Use(lang);
            using (GraphicsPageWorld world = WinForms.CreateOrIgnore(() => GraphicsPageWorld.In(state, true, fontScale)))
                AssertKeepsTheRules(LauncherPages.Graphics, Evaluate(world.Page, world.ConstructedSize));
        }

        /// <summary>
        /// The Tools page as the designer made it (its states come from its models): on Windows only, because its Krypton
        /// list and text box controls need Windows libraries to be created.
        /// </summary>
        [TestCaseSource(nameof(OtherPageScenarios))]
        public void OtherPage_NothingOverlapsOrIsCutOff(string name, string lang, float fontScale)
        {
            language = TestUiLanguage.Use(lang);
            using (Control page = WinForms.CreateOrIgnore(() => LauncherPages.Create(name)))
            using (LauncherPages.Host(page))
            {
                Size constructed = page.Size;
                LauncherPages.ScaleFonts(page, fontScale);
                AssertKeepsTheRules(name, Evaluate(page, constructed));
            }
        }

        // --- The page does not depend on whether it was shown ------------------------------------------------------

        /// <summary>
        /// The layout is the same whether the page is hidden or visible while it receives its state: the bug of the report
        /// was a layout that asked <see cref="Control.Visible"/>.
        /// </summary>
        [TestCaseSource(nameof(StatesInEnglish))]
        public void GameSettingsPage_HiddenOrVisibleWhileFilled_HasTheSameLayout(SettingsPageState state)
        {
            language = TestUiLanguage.Use("en");
            List<string> hidden;
            using (SettingsPageWorld world = WinForms.CreateOrIgnore(() => SettingsPageWorld.In(state, true)))
                hidden = Bounds(world.Page);
            List<string> visible;
            using (SettingsPageWorld world = WinForms.CreateOrIgnore(() => SettingsPageWorld.In(state, false)))
                visible = Bounds(world.Page);

            Assert.That(hidden, Is.Not.Empty);
            Assert.That(hidden, Is.EqualTo(visible));
        }

        private static List<string> Bounds(Control page)
        {
            return LayoutChecker.VisibleDescendants(page)
                                .Select(control => LayoutChecker.PathOf(page, control) + " " + control.Bounds)
                                .ToList();
        }
    }
}

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
    /// The geometry of the four pages of the launcher (1.1.0, ADR 0012 amendment): at the smallest window, 800 x 500, 1024 x 640
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
    /// laptop). The authoritative run is the one on Windows, where no rule is excused. The pictures of the pages are made by
    /// <see cref="PageScreenshotTests"/>.
    /// </para>
    /// <para>
    /// <see cref="KnownDefects"/> lists the rules the Game settings page is known to break on Mono until the layout work of 1.1.0
    /// has fixed it (the content does not grow, texts have fixed heights, and so on). A listed rule must still be broken in some
    /// scenario (<see cref="KnownDefects_AreStillBroken"/>), so whoever fixes a defect has to remove its entry; a rule not listed
    /// must hold everywhere.
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

        /// <summary>A rule a page breaks today at some font size, and why.</summary>
        private sealed class KnownDefect
        {
            public KnownDefect(string page, string rule, bool largeFontOnly, string reason)
            {
                Page = page;
                Rule = rule;
                LargeFontOnly = largeFontOnly;
                Reason = reason;
            }

            public string Page { get; }

            public string Rule { get; }

            /// <summary>True if the rule holds with the font of the system and breaks only with the font 50 % larger.</summary>
            public bool LargeFontOnly { get; }

            public string Reason { get; }

            public bool Covers(string page, string rule, float fontScale)
            {
                return Page == page && Rule == rule && (!LargeFontOnly || fontScale > 1f);
            }
        }

        /// <summary>
        /// The rules the Game settings page breaks on Mono, each with the reason. The layout work of 1.1.0 (pages that stack by
        /// text height and grow with the window) removes them. Windows excuses nothing (<see cref="IsKnown"/>): the first run
        /// there shows which of them are real on the fonts of Windows.
        /// </summary>
        private static readonly KnownDefect[] KnownDefects =
        {
            new KnownDefect(LauncherPages.GameSettings, NotGrowing, false,
                "the controls have the fixed width of 505 px of the designer and the page does not use more width"),
            new KnownDefect(LauncherPages.GameSettings, Clipped, false,
                "the text of the compatibility warning has a fixed height of the designer (222 px)"),
            new KnownDefect(LauncherPages.GameSettings, Narrow, true,
                "buttons and check boxes have the fixed widths of the designer, which a 50 % larger font does not fit"),
            new KnownDefect(LauncherPages.GameSettings, Outside, true,
                "the content has the fixed width of 505 px and the large font makes controls wider than the page"),
            new KnownDefect(LauncherPages.GameSettings, Overlap, true,
                "a hint row has a check box of fixed width (116 px) next to its text, which a 50 % larger font overflows"),
        };

        private static bool IsWindows
        {
            get { return Environment.OSVersion.Platform == PlatformID.Win32NT; }
        }

        /// <summary>True if <paramref name="rule"/> may be broken by <paramref name="page"/> (never on Windows).</summary>
        private static bool IsKnown(string page, string rule, float fontScale)
        {
            return !IsWindows && KnownDefects.Any(defect => defect.Covers(page, rule, fontScale));
        }

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

        public static IEnumerable<TestCaseData> OtherPageScenarios()
        {
            foreach (string page in new[] { LauncherPages.Play, LauncherPages.Tools, LauncherPages.Launcher })
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

        private static void AssertKeepsTheRules(string page, float fontScale, Dictionary<string, List<string>> findings)
        {
            List<string> broken = findings.Where(rule => !IsKnown(page, rule.Key, fontScale)).SelectMany(rule => rule.Value).ToList();
            Assert.That(broken, Is.Empty, "the geometry rules of the page '" + page + "'");
        }

        [TestCaseSource(nameof(GameSettingsScenarios))]
        public void GameSettingsPage_FilledBeforeItIsShown_NothingOverlaps(string lang, float fontScale, SettingsPageState state)
        {
            language = TestUiLanguage.Use(lang);
            using (SettingsPageWorld world = WinForms.CreateOrIgnore(() => SettingsPageWorld.In(state, true, fontScale)))
                AssertKeepsTheRules(LauncherPages.GameSettings, fontScale, Evaluate(world.Page, world.ConstructedSize));
        }

        /// <summary>
        /// The pages that need no state (they are created as the designer made them): on Windows only, because the Krypton
        /// combo box and list controls of these pages need Windows libraries to be created.
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
                AssertKeepsTheRules(name, fontScale, Evaluate(page, constructed));
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

        // --- The list of known defects ----------------------------------------------------------------------------

        /// <summary>
        /// A rule in <see cref="KnownDefects"/> is broken in at least one scenario; if the layout work fixed it, the entry has to
        /// go, so that the rule holds from then on. Runs on Mono, the only place with exceptions.
        /// </summary>
        [Test]
        public void KnownDefects_AreStillBroken()
        {
            if (IsWindows)
                Assert.Ignore("Windows excuses no rule; the list applies to the Mono run.");
            language = TestUiLanguage.Use("en");
            var broken = new HashSet<string>();
            foreach (float scale in FontScales)
            {
                foreach (SettingsPageState state in Enum.GetValues(typeof(SettingsPageState)))
                {
                    using (SettingsPageWorld world = SettingsPageWorld.In(state, true, scale))
                    {
                        foreach (KeyValuePair<string, List<string>> rule in Evaluate(world.Page, world.ConstructedSize))
                            if (rule.Value.Count > 0)
                                broken.Add(LauncherPages.GameSettings + "|" + rule.Key + "|" + (scale > 1f));
                    }
                }
            }

            foreach (KnownDefect defect in KnownDefects.Where(defect => defect.Page == LauncherPages.GameSettings))
            {
                bool still = broken.Contains(defect.Page + "|" + defect.Rule + "|True") ||
                             (!defect.LargeFontOnly && broken.Contains(defect.Page + "|" + defect.Rule + "|False"));
                Assert.That(still, Is.True,
                    "the rule '" + defect.Rule + "' of the page '" + defect.Page + "' ('" + defect.Reason + "') holds now: remove it from KnownDefects");
            }
        }
    }
}

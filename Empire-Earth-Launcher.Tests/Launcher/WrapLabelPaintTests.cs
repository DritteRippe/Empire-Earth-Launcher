using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Empire_Earth_Launcher.Tests.TestSupport;
using Krypton.Toolkit;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Launcher
{
    /// <summary>
    /// The wrapping labels of every page keep painting when a Krypton palette disposes its fonts (bug report of 2026-10-03: the
    /// four status lines of the Play page turned into red X and the launcher said "Ungültiger Parameter" while the game ran in
    /// full screen).
    /// </summary>
    /// <remarks>
    /// On every Windows setting change (<c>WM_SETTINGCHANGE</c>, also sent when a full-screen game changes the display mode)
    /// the Krypton palette disposes every font it handed out and creates new ones with the same values. KryptonWrapLabel kept
    /// the disposed font, because <c>Control.Font</c> ignores a new font that is equal in value; <c>Label.OnPaint</c> then threw
    /// <see cref="ArgumentException"/> in <c>Graphics.DrawString</c> and WinForms drew a red X instead of the label until the
    /// launcher was restarted. The tests find the wrapping labels as <see cref="Label"/>s and their palette by the name of
    /// its property, so that without the check of the fallback they ran unchanged against KryptonWrapLabel: on Mono the second
    /// case failed for every label of the Game settings page (ArgumentException, GDI+ status InvalidParameter); the first
    /// passes there even with KryptonWrapLabel, because Mono's <c>Control.Font</c> takes every new instance. On Windows both
    /// fail with it.
    /// </remarks>
    [TestFixture]
    [Category(TestCategories.WinForms)]
    [Apartment(ApartmentState.STA)]
    public class WrapLabelPaintTests
    {
        /// <summary>The labels that failed to paint and drew the fallback of <see cref="LauncherWrapLabel"/>.</summary>
        private readonly List<string> fallbacks = new List<string>();
        private Action<string, Exception> previousReporter;

        public static IEnumerable<TestCaseData> Pages()
        {
            yield return Page("Play", () => new GeneralUserControl());
            yield return Page("Game settings", () => new SettingsUserControl());
            yield return Page("Tools", () => new ToolsUserControl());
            yield return Page("Launcher", () => new LauncherSettingsUserControl());
        }

        private static TestCaseData Page(string name, Func<Control> create)
        {
            return new TestCaseData(create).SetArgDisplayNames(name);
        }

        [SetUp]
        public void SetUp()
        {
            WinForms.RequireDisplay();
            previousReporter = LauncherWrapLabel.PaintFailureReporter;
            fallbacks.Clear();
            LauncherWrapLabel.PaintFailureReporter = (name, exception) => fallbacks.Add(name + ": " + exception.Message);
        }

        [TearDown]
        public void TearDown()
        {
            LauncherWrapLabel.PaintFailureReporter = previousReporter;
        }

        [TestCaseSource(nameof(Pages))]
        public void TheGlobalPaletteRenewsItsFonts_EveryWrapLabelStillPaints(Func<Control> create)
        {
            using (Control page = WinForms.CreateOrIgnore(create))
            {
                List<Label> labels = WrapLabels(page);
                AssertEveryLabelPaints(labels);

                var global = KryptonManager.CurrentGlobalPalette as PaletteBase;
                Assert.That(global, Is.Not.Null, "the base palette of the launcher palettes");
                Font handedOut = global.GetContentShortTextFont(PaletteContentStyle.LabelNormalControl, PaletteState.Normal);
                WinForms.RunOrIgnoreWithoutWindows(() => RenewFonts(global));
                Assert.That(WinForms.IsUsable(handedOut), Is.False, "the palette disposed the font it had handed out");

                AssertEveryLabelPaints(labels);
            }
        }

        [TestCaseSource(nameof(Pages))]
        public void TheFontOfThePalette_IsDisposed_EveryWrapLabelStillPaints(Func<Control> create)
        {
            using (Control page = WinForms.CreateOrIgnore(create))
            using (var font = new Font(FontFamily.GenericSansSerif, 10f))
            {
                List<Label> labels = WrapLabels(page);
                foreach (KryptonPalette palette in labels.Select(PaletteOf).Distinct())
                    palette.LabelStyles.LabelCommon.StateCommon.ShortText.Font = font;
                AssertEveryLabelPaints(labels);

                font.Dispose();

                AssertEveryLabelPaints(labels);
            }
        }

        /// <summary>
        /// Lets <paramref name="palette"/> define its fonts again, as <c>PaletteOffice365Base.OnUserPreferenceChanged</c> does
        /// on every Windows setting change; <see cref="PaletteBase.BaseFontSize"/> is the public way to it. Setting the size
        /// it already has and then 0 (the size of Windows) defines the fonts twice, with the same values.
        /// </summary>
        private static void RenewFonts(PaletteBase palette)
        {
            palette.BaseFontSize = palette.BaseFontSize;
            palette.BaseFontSize = 0;
        }

        /// <summary>The wrapping labels of <paramref name="page"/>, each with a text and GDI+ text as in the launcher.</summary>
        private static List<Label> WrapLabels(Control page)
        {
            List<Label> labels = WinForms.Descendants(page).OfType<Label>().ToList();
            Assert.That(labels, Is.Not.Empty, "the page has wrapping labels");
            foreach (Label label in labels)
            {
                // Program calls Application.SetCompatibleTextRenderingDefault(true): the labels draw with GDI+.
                label.UseCompatibleTextRendering = true;
                if (string.IsNullOrEmpty(label.Text))
                    label.Text = "Text of " + label.Name;
            }
            return labels;
        }

        private static KryptonPalette PaletteOf(Label label)
        {
            var palette = label.GetType().GetProperty("Palette")?.GetValue(label) as KryptonPalette;
            Assert.That(palette, Is.Not.Null, label.Name + " has the launcher palette");
            return palette;
        }

        private void AssertEveryLabelPaints(IEnumerable<Label> labels)
        {
            var failures = new List<string>();
            foreach (Label label in labels)
            {
                try
                {
                    WinForms.Paint(label);
                }
                catch (Exception ex) when (ex is ArgumentException || ex is ExternalException)
                {
                    failures.Add(label.Name + ": " + ex.GetType().Name + ": " + ex.Message);
                    continue;
                }
                if (!WinForms.IsUsable(label.Font))
                    failures.Add(label.Name + ": painted, but holds a disposed font");
            }

            Assert.That(failures, Is.Empty, "a label whose paint throws shows a red X until the launcher is restarted");
            Assert.That(fallbacks, Is.Empty, "every label drew with its font, none needed the fallback");
        }
    }
}

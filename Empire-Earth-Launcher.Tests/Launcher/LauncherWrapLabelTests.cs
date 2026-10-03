using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using Empire_Earth_Launcher.Tests.TestSupport;
using Krypton.Toolkit;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Launcher
{
    /// <summary>
    /// <see cref="LauncherWrapLabel"/>: it draws with a copy of the palette font that no palette can dispose, its paint never
    /// throws (a failure draws the text with the default font and is reported once), and it measures with the font it paints
    /// with. The palette is a private instance of the launcher's base palette (Office 365 Blue), so the global palette stays
    /// as it is; <see cref="PaletteBase.BaseFontSize"/> is the public way to let it define its fonts again, as it does on
    /// every Windows setting change.
    /// </summary>
    [TestFixture]
    [Category(TestCategories.WinForms)]
    [Apartment(ApartmentState.STA)]
    public class LauncherWrapLabelTests
    {
        private const string Text = "Empire Earth.exe: Version 2.0.0.2949, EE-AOC.exe: Version 1.0.0.2473";
        private const string LabelName = "programVersionsKryptonWrapLabel";

        private PaletteOffice365Blue basePalette;
        private KryptonPalette palette;
        private readonly List<IDisposable> disposables = new List<IDisposable>();

        /// <summary>The paints that failed and drew the fallback (<see cref="LauncherWrapLabel.PaintFailureReporter"/>).</summary>
        private readonly List<string> fallbacks = new List<string>();
        private Action<string, Exception> previousReporter;

        [SetUp]
        public void SetUp()
        {
            WinForms.RequireDisplay();
            previousReporter = LauncherWrapLabel.PaintFailureReporter;
            fallbacks.Clear();
            LauncherWrapLabel.PaintFailureReporter = (name, exception) => fallbacks.Add(name);
            basePalette = new PaletteOffice365Blue();
            palette = new KryptonPalette { BasePaletteMode = PaletteMode.Custom, BasePalette = basePalette };
            disposables.Add(palette);
            disposables.Add(basePalette);
        }

        [TearDown]
        public void TearDown()
        {
            LauncherWrapLabel.PaintFailureReporter = previousReporter;
            disposables.Reverse();
            foreach (IDisposable disposable in disposables)
                disposable.Dispose();
            disposables.Clear();
        }

        private LauncherWrapLabel CreateLabel()
        {
            var label = new LauncherWrapLabel
            {
                AutoSize = false,
                Size = new Size(300, 40),
                Text = Text,
                Name = LabelName,
                LabelStyle = LabelStyle.NormalControl,
                Palette = palette,
                // Program calls Application.SetCompatibleTextRenderingDefault(true): GDI+ text, as in the bug report.
                UseCompatibleTextRendering = true,
            };
            disposables.Insert(0, label);
            return label;
        }

        private Font PaletteFont()
        {
            return palette.GetContentShortTextFont(PaletteContentStyle.LabelNormalControl, PaletteState.Normal);
        }

        /// <summary>
        /// The palette defines its fonts again, twice (the size it has, then 0 for the size of Windows): it disposes the fonts
        /// it handed out and creates new ones with the same values.
        /// </summary>
        private void RenewTheFontsOfThePalette()
        {
            Font handedOut = PaletteFont();
            basePalette.BaseFontSize = basePalette.BaseFontSize;
            basePalette.BaseFontSize = 0;
            Assert.That(WinForms.IsUsable(handedOut), Is.False, "the palette disposed the font it had handed out");
            Assert.That(PaletteFont(), Is.Not.SameAs(handedOut).And.EqualTo(handedOut), "and created one with the same values");
        }

        /// <summary>Paints <paramref name="label"/>: no exception, drawn with its font (not with the fallback), which is usable.</summary>
        private void AssertPaints(LauncherWrapLabel label)
        {
            Assert.DoesNotThrow(() => WinForms.Paint(label));
            Assert.That(fallbacks, Is.Empty, "drawn with the palette font, not with the fallback");
            Assert.That(WinForms.IsUsable(label.Font), Is.True, "the font of the label");
        }

        [Test]
        public void TheLabel_DrawsWithACopyOfThePaletteFont_TakenWhenThePaletteIsSet()
        {
            LauncherWrapLabel label = CreateLabel();

            Assert.That(label.Font, Is.EqualTo(PaletteFont()), "already before the first paint, so that layouts measure with it");
            Assert.That(label.Font, Is.Not.SameAs(PaletteFont()), "a copy the label owns");
            AssertPaints(label);
        }

        [Test]
        public void ThePaletteRenewsItsFonts_TheLabelStillPaints()
        {
            LauncherWrapLabel label = CreateLabel();
            AssertPaints(label);
            Font copy = label.Font;

            RenewTheFontsOfThePalette();

            AssertPaints(label);
            Assert.That(label.Font, Is.SameAs(copy), "the values did not change, so the copy stays");
        }

        [Test]
        public void ThePaletteFontIsDisposedLater_TheLabelStillPaints()
        {
            // A size no palette of Windows or Mono uses, so that the label really takes this font's values.
            var font = new Font(FontFamily.GenericSansSerif, 13f);
            palette.LabelStyles.LabelNormalControl.StateCommon.ShortText.Font = font;
            LauncherWrapLabel label = CreateLabel();
            AssertPaints(label);

            font.Dispose();

            AssertPaints(label);
            Assert.That(label.Font.Size, Is.EqualTo(13f));
        }

        [Test]
        public void ThePaletteFontIsAlreadyDisposed_TheLabelPaintsWithACopyOfItsValues()
        {
            var font = new Font(FontFamily.GenericSansSerif, 12f, FontStyle.Bold);
            font.Dispose();
            palette.LabelStyles.LabelNormalControl.StateCommon.ShortText.Font = font;

            LauncherWrapLabel label = CreateLabel();

            AssertPaints(label);
            Assert.That(label.Font.Size, Is.EqualTo(12f));
            Assert.That(label.Font.Style, Is.EqualTo(FontStyle.Bold));
        }

        /// <summary>The state KryptonWrapLabel ended in on Windows: Control.Font holds a disposed font equal to the palette's.</summary>
        [Test]
        public void ADisposedFontEqualToThePalettes_IsReplacedByTheCopy()
        {
            LauncherWrapLabel label = CreateLabel();
            AssertPaints(label);
            var stale = new Font(label.Font.FontFamily, label.Font.Size, label.Font.Style, label.Font.Unit);
            label.Font = stale;
            stale.Dispose();

            AssertPaints(label);
            Assert.That(label.Font, Is.Not.SameAs(stale));
        }

        [Test]
        public void ThePaletteFontGetsOtherValues_TheLabelTakesThem()
        {
            LauncherWrapLabel label = CreateLabel();
            var large = new Font(FontFamily.GenericSansSerif, 14f);
            disposables.Add(large);

            palette.LabelStyles.LabelNormalControl.StateCommon.ShortText.Font = large;
            AssertPaints(label);

            Assert.That(label.Font, Is.EqualTo(large).And.Not.SameAs(large));
        }

        [Test]
        public void APaintThatFails_DrawsTheFallback_AndIsReportedOncePerLabel()
        {
            LauncherWrapLabel label = CreateLabel();
            using (var bitmap = new Bitmap(300, 40))
            {
                // A graphics GDI+ cannot use any more: every drawing call fails, the fallback too.
                Graphics broken = Graphics.FromImage(bitmap);
                broken.Dispose();

                Assert.DoesNotThrow(() => WinForms.Paint(label, broken), "no red X");
                Assert.DoesNotThrow(() => WinForms.Paint(label, broken));
            }

            Assert.That(fallbacks, Is.EqualTo(new[] { LabelName }), "the first failure, once");
            fallbacks.Clear();
            AssertPaints(label);
        }

        [Test]
        public void TextHeight_IsMeasuredWithTheCopy_AlsoAfterThePaletteRenewedItsFonts()
        {
            LauncherWrapLabel label = CreateLabel();
            int before = label.TextHeight(120);

            RenewTheFontsOfThePalette();

            Assert.That(label.TextHeight(120), Is.EqualTo(before));
            Assert.That(before, Is.GreaterThan(label.Font.Height * 2), "the text wraps to several lines at 120 pixels");
            label.Text = string.Empty;
            Assert.That(label.TextHeight(120), Is.EqualTo(0));
        }

        /// <summary>The labels of the repair advice have no palette of their own: they use the global palette of Krypton.</summary>
        [Test]
        public void WithoutAPalette_TheLabelUsesTheGlobalPalette()
        {
            var label = new LauncherWrapLabel
            {
                Size = new Size(300, 40),
                Text = Text,
                LabelStyle = LabelStyle.NormalControl,
                UseCompatibleTextRendering = true,
            };
            disposables.Insert(0, label);

            Assert.That(label.PaletteMode, Is.EqualTo(PaletteMode.Global));
            Assert.That(label.Font, Is.EqualTo(KryptonManager.CurrentGlobalPalette.GetContentShortTextFont(
                PaletteContentStyle.LabelNormalControl, PaletteState.Normal)));
            AssertPaints(label);
        }

        /// <summary>
        /// The cause, documented on Windows (CI, laptop): KryptonWrapLabel keeps the font the palette disposed, because
        /// Control.Font ignores the new font that is equal in value, and its paint throws. Mono's Control.Font takes every new
        /// font, so there the case does not occur.
        /// </summary>
        [Test]
        [Platform(Exclude = "Mono")]
        public void KryptonWrapLabel_KeepsTheDisposedFont_AndItsPaintThrows()
        {
            var label = new KryptonWrapLabel
            {
                AutoSize = false,
                Size = new Size(300, 40),
                Text = Text,
                Palette = palette,
                UseCompatibleTextRendering = true,
            };
            disposables.Insert(0, label);
            WinForms.Paint(label);
            Font taken = label.Font;

            RenewTheFontsOfThePalette();

            Assert.Throws<ArgumentException>(() => WinForms.Paint(label));
            Assert.That(label.Font, Is.SameAs(taken), "Control.Font ignored the new font with the same values");
        }
    }
}

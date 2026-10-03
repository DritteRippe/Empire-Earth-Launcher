using System;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Krypton.Toolkit;
using Microsoft.Win32;

namespace Empire_Earth_Launcher
{
    /// <summary>
    /// A label that wraps its text and shows it with the font, color and text rendering hint of a Krypton palette, like
    /// <c>KryptonWrapLabel</c>, but whose font no palette can dispose and whose paint never throws. Every wrapping text of the
    /// launcher uses it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Why not <c>KryptonWrapLabel</c> (bug report of 2026-10-03: four red X on the Play page and the error dialog "Ungültiger
    /// Parameter" while Empire Earth ran in full screen): on every Windows setting change (<c>WM_SETTINGCHANGE</c>, also sent
    /// when a full-screen game changes the display mode) the Krypton palettes dispose every font they handed out and create new
    /// ones with the same values (<c>PaletteOffice365Base.DefineFonts</c>). <c>KryptonWrapLabel</c> assigns the palette font
    /// to <see cref="Control.Font"/> when it paints, and <c>Control.Font</c> ignores a font that is equal in value to the one it
    /// has, also when that one is disposed. The label kept the disposed font, <c>Label.OnPaint</c> threw
    /// <see cref="ArgumentException"/> in <c>Graphics.DrawString</c>, and WinForms drew a red X instead of the label until the
    /// launcher was restarted. <c>KryptonWrapLabel</c> is sealed, so this class derives from <see cref="Label"/>.
    /// </para>
    /// <para>
    /// The label therefore draws with a copy of the palette font that it owns: a palette that disposes its fonts does not
    /// reach it, and <see cref="Control.Font"/>, which the layout and <see cref="TextHeight"/> also use, is always usable. A
    /// palette font with other values (a theme, another font size of Windows) gives a new copy. Should drawing fail anyway,
    /// the label draws its text with <see cref="Control.DefaultFont"/>, reports the first failure
    /// (<see cref="PaintFailureReporter"/>) and tries normally again at the next paint; it never shows a red X.
    /// </para>
    /// <para>
    /// The copies are not disposed when they are replaced: <see cref="Control.Font"/> hands them out, and a disposed font that
    /// someone still holds is exactly the failure this class prevents. The garbage collector finalizes them; a copy is made
    /// only when the values of the palette font change. The fields of the pages keep their names (<c>...KryptonWrapLabel</c>),
    /// which ADR 0014 and the git history use.
    /// </para>
    /// </remarks>
    [ToolboxItem(true)]
    [DesignerCategory("Code")]
    [Description("Displays wrapping text with the font and color of a Krypton palette.")]
    public sealed class LauncherWrapLabel : Label
    {
        private IPalette palette;
        private PaletteMode paletteMode = PaletteMode.Global;
        private LabelStyle labelStyle = LabelStyle.NormalPanel;

        /// <summary>The copy of the palette font the label draws with; null until the label read the palette.</summary>
        private Font ownFont;

        /// <summary>The palette font <see cref="ownFont"/> has the values of (compared by reference only, it may be disposed).</summary>
        private Font ownFontSource;

        /// <summary>True after the first paint that failed was reported.</summary>
        private bool paintFailureReported;

        public LauncherWrapLabel()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            // Transparent: the background of the page shows through, as with KryptonWrapLabel.
            base.BackColor = Color.Transparent;
            // Repaint after a setting change of Windows, when the palette has new fonts (KryptonWrapLabel did the same).
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        }

        /// <summary>
        /// Reports the first paint of each label that failed and was drawn with <see cref="Control.DefaultFont"/>: the name of
        /// the label and the exception. Program logs it; null reports nothing.
        /// </summary>
        internal static Action<string, Exception> PaintFailureReporter { get; set; }

        /// <summary>The custom palette; setting one sets <see cref="PaletteMode"/> to <see cref="Krypton.Toolkit.PaletteMode.Custom"/>.</summary>
        [Category("Visuals")]
        [Description("Custom palette applied to drawing.")]
        [DefaultValue(null)]
        public IPalette Palette
        {
            get { return palette; }
            set
            {
                if (palette == value)
                    return;
                if (palette != null)
                    palette.PalettePaint -= OnPalettePaint;
                palette = value;
                paletteMode = value == null ? PaletteMode.Global : PaletteMode.Custom;
                if (palette != null)
                    palette.PalettePaint += OnPalettePaint;
                OnPaletteValuesChanged();
            }
        }

        /// <summary>
        /// The palette used without a custom <see cref="Palette"/>; <see cref="Krypton.Toolkit.PaletteMode.Custom"/> needs a
        /// palette (as with Krypton controls, setting it alone changes nothing).
        /// </summary>
        [Category("Visuals")]
        [Description("Palette applied to drawing.")]
        [DefaultValue(PaletteMode.Global)]
        public PaletteMode PaletteMode
        {
            get { return paletteMode; }
            set
            {
                if (value == paletteMode || value == PaletteMode.Custom)
                    return;
                Palette = null;
                paletteMode = value;
                OnPaletteValuesChanged();
            }
        }

        /// <summary>The label style whose palette values the text uses.</summary>
        [Category("Visuals")]
        [Description("Label style.")]
        [DefaultValue(LabelStyle.NormalPanel)]
        public LabelStyle LabelStyle
        {
            get { return labelStyle; }
            set
            {
                if (labelStyle == value)
                    return;
                labelStyle = value;
                OnPaletteValuesChanged();
            }
        }

        /// <summary>Transparent: the background of the parent shows through.</summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public override Color BackColor
        {
            get { return base.BackColor; }
            set { base.BackColor = value; }
        }

        /// <summary>A copy of the palette font; a font set here is replaced by it.</summary>
        [Browsable(false)]
        public override Font Font
        {
            get { return base.Font; }
            set { base.Font = value; }
        }

        /// <summary>The text color of the palette; a color set here is replaced by it when the label paints.</summary>
        [Browsable(false)]
        public override Color ForeColor
        {
            get { return base.ForeColor; }
            set { base.ForeColor = value; }
        }

        /// <summary>
        /// The height the label needs to show its whole text at <paramref name="width"/>: GDI+ text as the launcher draws it
        /// (compatible text rendering), measured at 96 DPI like the DPI-unaware launcher and without a window handle, plus 6
        /// pixels; 0 without text. Measured with the font the label paints with, never with a disposed one.
        /// </summary>
        internal int TextHeight(int width)
        {
            if (string.IsNullOrEmpty(Text))
                return 0;
            UpdateFont();
            try
            {
                using (var bitmap = new Bitmap(1, 1))
                using (Graphics graphics = Graphics.FromImage(bitmap))
                    return (int)Math.Ceiling(graphics.MeasureString(Text, Font, width).Height) + 6;
            }
            catch (Exception ex) when (IsDrawingFailure(ex))
            {
                // The same measure as the fallback of OnPaint draws with.
                return TextRenderer.MeasureText(Text, DefaultFont, new Size(width, 0), FallbackFormat).Height + 6;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            try
            {
                UpdateFont();
                IPalette resolved = ResolvedPalette;
                PaletteContentStyle style = ContentStyle;
                PaletteState state = State;
                ForeColor = resolved.GetContentShortTextColor1(style, state);
                e.Graphics.TextRenderingHint =
                    CommonHelper.PaletteTextHintToRenderingHint(resolved.GetContentShortTextHint(style, state));
                base.OnPaint(e);
            }
            catch (Exception ex) when (!IsCritical(ex))
            {
                // An exception leaving OnPaint makes WinForms show a red X instead of the label until the launcher restarts.
                // Every failure is caught, not only those of GDI+ (which also reports failures as OutOfMemoryException); the
                // first one of each label is logged.
                ReportPaintFailure(ex);
                ForgetUnusableFont();
                PaintFallback(e);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
                if (palette != null)
                    palette.PalettePaint -= OnPalettePaint;
                palette = null;
            }
            base.Dispose(disposing);
        }

        private IPalette ResolvedPalette
        {
            get
            {
                if (palette != null)
                    return palette;
                return KryptonManager.GetPaletteForMode(paletteMode == PaletteMode.Custom ? PaletteMode.Global : paletteMode);
            }
        }

        private PaletteContentStyle ContentStyle
        {
            get { return CommonHelper.ContentStyleFromLabelStyle(labelStyle); }
        }

        private PaletteState State
        {
            get { return Enabled ? PaletteState.Normal : PaletteState.Disabled; }
        }

        private const TextFormatFlags FallbackFormat =
            TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.TextBoxControl;

        /// <summary>The failures of GDI+ and GDI: invalid parameter (a disposed font), generic error, object busy.</summary>
        private static bool IsDrawingFailure(Exception ex)
        {
            return ex is ArgumentException || ex is ExternalException || ex is InvalidOperationException;
        }

        /// <summary>Exceptions after which the process must not go on as if nothing happened.</summary>
        private static bool IsCritical(Exception ex)
        {
            return ex is StackOverflowException || ex is ThreadAbortException || ex is AccessViolationException;
        }

        /// <summary>True if GDI+ can use <paramref name="font"/>; a disposed font fails.</summary>
        private static bool IsUsable(Font font)
        {
            try
            {
                font.GetHeight(96f);
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        /// <summary>
        /// Makes <see cref="Control.Font"/> a copy of the palette font: a new copy when the palette has a font with other
        /// values, else the copy the label has.
        /// </summary>
        private void UpdateFont()
        {
            Font source = ResolvedPalette?.GetContentShortTextFont(ContentStyle, State);
            if (source != null && !ReferenceEquals(source, ownFontSource))
            {
                ownFontSource = source;
                // Font.Equals compares the values only, so it also works on a disposed palette font.
                if (ownFont == null || !ownFont.Equals(source))
                    ownFont = CopyOf(source) ?? ownFont;
            }

            if (ownFont == null || ReferenceEquals(base.Font, ownFont))
                return;
            // Control.Font ignores a font equal in value to the one it has, even a disposed one: clear it first.
            if (base.Font.Equals(ownFont))
                base.Font = null;
            base.Font = ownFont;
        }

        /// <summary>
        /// A new font with the values of <paramref name="source"/>, made from the values alone, so that a palette font that
        /// was already disposed gives a usable copy too; null if GDI+ cannot create it.
        /// </summary>
        private static Font CopyOf(Font source)
        {
            try
            {
                return new Font(source.FontFamily, source.Size, source.Style, source.Unit, source.GdiCharSet,
                    source.GdiVerticalFont);
            }
            catch (Exception ex) when (IsDrawingFailure(ex))
            {
                return null;
            }
        }

        /// <summary>After a failed paint: a copy that GDI+ can no longer use is made again at the next paint.</summary>
        private void ForgetUnusableFont()
        {
            if (ownFont != null && !IsUsable(ownFont))
            {
                ownFont = null;
                ownFontSource = null;
            }
        }

        private void ReportPaintFailure(Exception ex)
        {
            if (paintFailureReported)
                return;
            paintFailureReported = true;
            PaintFailureReporter?.Invoke(Name, ex);
        }

        /// <summary>The text with GDI and the default font of WinForms; draws nothing if that fails too.</summary>
        private void PaintFallback(PaintEventArgs e)
        {
            try
            {
                TextRenderer.DrawText(e.Graphics, Text, DefaultFont, ClientRectangle, ForeColor, FallbackFormat);
            }
            catch (Exception ex) when (IsDrawingFailure(ex))
            {
                // Nothing can be drawn now; the next paint tries again.
            }
        }

        /// <summary>The palette, the palette mode or the label style changed: take the font now, so that layouts measure with it.</summary>
        private void OnPaletteValuesChanged()
        {
            UpdateFont();
            Invalidate();
        }

        private void OnPalettePaint(object sender, PaletteLayoutEventArgs e)
        {
            Invalidate();
        }

        private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            Invalidate();
        }
    }
}

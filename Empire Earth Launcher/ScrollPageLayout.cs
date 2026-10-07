using System;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using Krypton.Toolkit;

namespace Empire_Earth_Launcher
{
    /// <summary>
    /// Stacks the controls of a scrolling page from top to bottom for the width of the page, so that the page grows with the
    /// window (ADR 0017): used by the Game settings page and the Tools page, which show a long column of texts, buttons and
    /// lists that scrolls when it is longer than the window, and, with its own margin and without the scroll bar, for the blocks
    /// of the Play page and the Launcher page (the content of a group box, the info bar).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="Run"/> lays the page out: the code it runs calls <see cref="Place"/> or <see cref="PlaceRow"/> for each control,
    /// which gives the control its place below the previous one. Texts, lists and boxes take the whole content width; a wrapping label is as
    /// high as its text needs at that width (<see cref="LauncherWrapLabel.TextHeight"/>); Krypton labels, check boxes and buttons
    /// are as high as their text needs; buttons keep the width of the designer or the width their text needs, whichever is more,
    /// and a row of buttons wraps into the next line where the width of the page does not suffice (long translations, large
    /// fonts).
    /// </para>
    /// <para>
    /// The content width is taken from the width of the panel minus the width of the scroll bar, which is reserved whether the
    /// bar is shown or not: the bar appears and disappears with the length of the content, and a width that depended on it would
    /// lay the page out again and again. The height of the page does not matter; the position of the scroll bar is kept.
    /// </para>
    /// <para>
    /// A check box or a label that shows one line cannot wrap. If the text of one is wider than the content width (a long
    /// translation in a large font in the smallest window), the content gets that width and the page scrolls sideways, so that
    /// no text is cut off; the width is found by a second pass.
    /// </para>
    /// <para>
    /// The caller decides what is shown with <c>isShown</c> and not with <see cref="Control.Visible"/>, which reads false for
    /// every control of a page that is not shown yet, and the launcher fills its pages before its window is shown.
    /// </para>
    /// <para>
    /// A block that never scrolls (the content of a group box) has no scroll bar to reserve room for and takes its margin from
    /// the caller; <see cref="ContentHeight"/> then tells how high the block has to be.
    /// </para>
    /// </remarks>
    internal sealed class ScrollPageLayout
    {
        /// <summary>The space left and right of the content of a page (the default of the constructor).</summary>
        public const int Margin = 12;

        /// <summary>The space between two controls.</summary>
        public const int Gap = 6;

        /// <summary>The space above the first control.</summary>
        private const int TopMargin = 8;

        /// <summary>The narrowest content the layout works with (a page is never narrower than its window allows).</summary>
        private const int MinimumContentWidth = 200;

        /// <summary>The narrowest a field gets in a row of a form before its buttons go to the next line.</summary>
        private const int MinimumFieldWidth = 120;

        private readonly ScrollableControl panel;
        private readonly Func<Control, bool> isShown;
        private readonly int margin;
        private readonly bool reservesScrollBar;

        /// <summary>
        /// The size each button and Krypton control had when the layout first saw it: the designer size. The page replaces
        /// controls (the rows of hints), so the table must not keep them alive.
        /// </summary>
        private readonly ConditionalWeakTable<Control, DesignSizeBox> designSizes = new ConditionalWeakTable<Control, DesignSizeBox>();

        private int y;
        private int scroll;
        private int scrollX;
        private int laidOutWidth = -1;

        /// <summary>The width the content must have at least (0 in the first pass; in the second the width one-line texts need).</summary>
        private int minimumContentWidth;

        /// <summary>The widest natural width of a control of the pass that can neither wrap nor be narrower than its text.</summary>
        private int rigidWidth;

        /// <param name="panel">The panel that holds the controls; it scrolls (AutoScroll) unless the block is as high as its
        /// content.</param>
        /// <param name="isShown">True for a control that takes room; a hidden one is skipped.</param>
        /// <param name="margin">The space left and right of the content.</param>
        /// <param name="reservesScrollBar">True (a panel that scrolls) to keep the width of the scroll bar free whether it is
        /// shown or not; false for a block that never scrolls.</param>
        public ScrollPageLayout(ScrollableControl panel, Func<Control, bool> isShown, int margin = Margin,
            bool reservesScrollBar = true)
        {
            this.panel = panel ?? throw new ArgumentNullException(nameof(panel));
            this.isShown = isShown ?? throw new ArgumentNullException(nameof(isShown));
            this.margin = margin;
            this.reservesScrollBar = reservesScrollBar;
        }

        /// <summary>The width of the controls of the pass that runs.</summary>
        public int ContentWidth { get; private set; }

        /// <summary>
        /// The height of what the last pass placed, with the space below the last control: the height a block needs that does
        /// not scroll.
        /// </summary>
        public int ContentHeight { get; private set; }

        /// <summary>True if the width of the panel is not the one the last pass used: the page has to be laid out again.</summary>
        public bool NeedsLayout
        {
            get { return panel.Width != laidOutWidth; }
        }

        /// <summary>
        /// Lays the page out: <paramref name="placeControls"/> places the controls with <see cref="Place"/>,
        /// <see cref="PlaceRow"/> and <see cref="PlaceBeside"/>, top to bottom. It runs a second time, with a wider content,
        /// if a one-line text needs more than the width of the page gave.
        /// </summary>
        public void Run(Action placeControls)
        {
            minimumContentWidth = 0;
            Begin();
            placeControls();
            if (rigidWidth > ContentWidth)
            {
                minimumContentWidth = rigidWidth;
                panel.ResumeLayout(false);
                Begin();
                placeControls();
            }
            End();
        }

        /// <summary>Starts a pass: takes the width of the panel and the position of the scroll bars.</summary>
        private void Begin()
        {
            panel.SuspendLayout();
            scroll = panel.AutoScrollPosition.Y;
            scrollX = panel.AutoScrollPosition.X;
            y = TopMargin;
            laidOutWidth = panel.Width;
            ContentWidth = Math.Max(Math.Max(MinimumContentWidth, minimumContentWidth),
                panel.Width - (reservesScrollBar ? SystemInformation.VerticalScrollBarWidth : 0) - 2 * margin);
            rigidWidth = 0;
        }

        /// <summary>
        /// Ends the pass. The panel works out its scroll bars from the new bounds and, if the content became shorter than the
        /// page was scrolled (a wider page needs fewer lines), scrolls back by itself.
        /// </summary>
        private void End()
        {
            ContentHeight = y;
            panel.ResumeLayout(true);
        }

        /// <summary>Adds room of <paramref name="pixels"/> before the next control.</summary>
        public void Space(int pixels)
        {
            y += pixels;
        }

        /// <summary>
        /// Places <paramref name="control"/> below the previous one over the content width, <paramref name="indent"/> pixels
        /// from its left edge on (a line under the field of a row of a form); a button keeps its natural width
        /// (<see cref="PlaceRow"/>). A hidden control takes no room.
        /// </summary>
        public void Place(Control control, int indent = 0)
        {
            if (!isShown(control))
                return;
            if (control is KryptonButton)
            {
                PlaceRow(control);
                return;
            }
            if (control is KryptonLabel || control is KryptonCheckBox)
                rigidWidth = Math.Max(rigidWidth, indent + control.GetPreferredSize(Size.Empty).Width);
            control.Left = margin + indent + scrollX;
            control.Width = ContentWidth - indent;
            FitHeight(control);
            control.Top = y + scroll;
            y += control.Height + Gap;
        }

        /// <summary>
        /// Places the controls that are shown side by side, each with its natural width, left to right; where one would reach
        /// beyond the content width it starts the next line. The line is as high as its highest control.
        /// </summary>
        public void PlaceRow(params Control[] controls)
        {
            int x = margin;
            int lineHeight = 0;
            foreach (Control control in controls.Where(isShown))
            {
                FitHeight(control);
                int width = Math.Min(ContentWidth, NaturalWidth(control));
                if (x > margin && x + width > margin + ContentWidth)
                {
                    y += lineHeight + Gap;
                    x = margin;
                    lineHeight = 0;
                }
                control.SetBounds(x + scrollX, y + scroll, width, control.Height);
                x += width + Gap;
                lineHeight = Math.Max(lineHeight, control.Height);
            }
            if (lineHeight > 0)
                y += lineHeight + Gap;
        }

        /// <summary>
        /// Places a check box or a button that belongs to a text next to it (a row of hints): the control takes its natural
        /// width and the text the rest of the content width, both start at the top of the line.
        /// </summary>
        /// <returns>The width the text can have.</returns>
        public int PlaceBeside(Control control, Control text)
        {
            FitHeight(control);
            int width = Math.Min(ContentWidth / 2, NaturalWidth(control));
            control.SetBounds(margin + scrollX, y + scroll, width, control.Height);
            int textWidth = ContentWidth - width - Gap;
            text.SetBounds(margin + scrollX + width + Gap, y + scroll, textWidth, text.Height);
            if (text is LauncherWrapLabel label)
                label.Height = label.TextHeight(textWidth);
            y += Math.Max(control.Height, text.Height) + Gap;
            return textWidth;
        }

        /// <summary>
        /// Places one row of a form: <paramref name="label"/> in a column of <paramref name="labelWidth"/> (the rows of a form
        /// share one width, so that the fields start at the same place), next to it <paramref name="field"/>, after it the
        /// <paramref name="buttons"/> at their natural width, each centered on the line. The field is
        /// <paramref name="fieldWidth"/> wide (a combo box), or, with 0, takes the rest of the content width; where that is less
        /// than <see cref="MinimumFieldWidth"/> the buttons go below the field. A hidden field hides the row.
        /// </summary>
        public void PlaceField(Control label, int labelWidth, Control field, int fieldWidth, params Control[] buttons)
        {
            if (!isShown(field))
                return;
            Control[] shownButtons = buttons.Where(isShown).ToArray();
            FitHeight(label);
            FitHeight(field);
            foreach (Control button in shownButtons)
                FitHeight(button);

            int fieldLeft = margin + labelWidth + Gap;
            int rest = ContentWidth - labelWidth - Gap;
            int buttonsWidth = shownButtons.Sum(button => Gap + Math.Min(rest, NaturalWidth(button)));
            int width = fieldWidth > 0 ? Math.Min(fieldWidth, rest) : rest - buttonsWidth;
            bool wrapButtons = fieldWidth <= 0 && shownButtons.Length > 0 && width < MinimumFieldWidth;
            if (wrapButtons)
                width = rest;

            int lineHeight = Math.Max(label.Height, field.Height);
            if (!wrapButtons && shownButtons.Length > 0)
                lineHeight = Math.Max(lineHeight, shownButtons.Max(button => button.Height));
            label.SetBounds(margin + scrollX, y + scroll + (lineHeight - label.Height) / 2, labelWidth, label.Height);
            field.SetBounds(fieldLeft + scrollX, y + scroll + (lineHeight - field.Height) / 2, width, field.Height);
            y += lineHeight + Gap;

            int x = fieldLeft + (wrapButtons ? 0 : width + Gap);
            int buttonLine = 0;
            foreach (Control button in shownButtons)
            {
                int buttonWidth = Math.Min(rest, NaturalWidth(button));
                int top = wrapButtons ? y : y - Gap - lineHeight + (lineHeight - button.Height) / 2;
                button.SetBounds(x + scrollX, top + scroll, buttonWidth, button.Height);
                x += buttonWidth + Gap;
                buttonLine = Math.Max(buttonLine, button.Height);
            }
            if (wrapButtons)
                y += buttonLine + Gap;
        }

        /// <summary>
        /// The size a button (or another Krypton control) has in a row: as high as its text and font need and as wide as the
        /// designer made it or its text needs, never wider than the content. For controls placed by the page itself, as the
        /// button inside a block.
        /// </summary>
        public Size NaturalSize(Control control)
        {
            FitHeight(control);
            return new Size(Math.Min(ContentWidth, NaturalWidth(control)), control.Height);
        }

        /// <summary>The width of <paramref name="control"/> in a row: the designer width or what its text needs.</summary>
        private int NaturalWidth(Control control)
        {
            return Math.Max(DesignSize(control).Width, control.GetPreferredSize(Size.Empty).Width);
        }

        /// <summary>
        /// Makes a wrapping label as high as its text needs at its width, and a Krypton label, check box, radio button or button
        /// as high as its text and font need (at least the designer height); other controls keep their height.
        /// </summary>
        private void FitHeight(Control control)
        {
            if (control is LauncherWrapLabel label)
            {
                label.Height = label.TextHeight(label.Width);
            }
            else if (control is KryptonLabel || control is KryptonCheckBox || control is KryptonRadioButton ||
                     control is KryptonButton)
            {
                control.Height = Math.Max(DesignSize(control).Height, control.GetPreferredSize(Size.Empty).Height);
            }
        }

        private Size DesignSize(Control control)
        {
            return designSizes.GetValue(control, key => new DesignSizeBox(key.Size)).Size;
        }

        private sealed class DesignSizeBox
        {
            public DesignSizeBox(Size size)
            {
                Size = size;
            }

            public Size Size { get; }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Empire_Earth_Launcher.Tests.TestSupport;
using Krypton.Toolkit;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Launcher
{
    /// <summary>
    /// <see cref="ScrollPageLayout"/>, which stacks the controls of the Game settings page and of the Tools page for the width
    /// of the window (ADR 0017), on a small panel with the kinds of controls the pages hold. The Tools page itself cannot be
    /// created under Mono (its list and text boxes need Windows libraries), so the rules of its layout are tested here and its
    /// bounds on Windows by <see cref="PageLayoutTests"/>.
    /// </summary>
    [TestFixture]
    [Category(TestCategories.WinForms)]
    [Apartment(ApartmentState.STA)]
    public class ScrollPageLayoutTests
    {
        private const string LongText = "Empire Earth is an old game. Although it hardly needs any power, it runs badly on many " +
                                        "computers, especially on laptops, and the text of this label needs several lines.";

        private Panel panel;
        private ScrollPageLayout stack;
        private readonly HashSet<Control> hidden = new HashSet<Control>();

        [SetUp]
        public void SetUp()
        {
            WinForms.RequireDisplay();
            panel = new Panel { AutoScroll = true, Size = new Size(554, 380) };
            stack = new ScrollPageLayout(panel, control => !hidden.Contains(control));
        }

        [TearDown]
        public void TearDown()
        {
            panel.Dispose();
        }

        private T Add<T>(T control, int width = 100, int height = 20) where T : Control
        {
            control.Size = new Size(width, height);
            panel.Controls.Add(control);
            return control;
        }

        private LauncherWrapLabel WrapLabel(string text)
        {
            return Add(new LauncherWrapLabel { AutoSize = false, Text = text }, 505, 20);
        }

        private KryptonButton Button(string text, int width = 140)
        {
            var button = new KryptonButton();
            button.Values.Text = text;
            return Add(button, width, 28);
        }

        private KryptonCheckBox CheckBox(string text)
        {
            var checkBox = new KryptonCheckBox { AutoSize = false };
            checkBox.Values.Text = text;
            return Add(checkBox, 505, 22);
        }

        private static int ScrollBar
        {
            get { return SystemInformation.VerticalScrollBarWidth; }
        }

        // --- Width -------------------------------------------------------------------------------------------------

        [Test]
        public void Place_GivesTheControlsTheWidthOfThePageWithoutTheScrollBarAndTheMargins()
        {
            LauncherWrapLabel label = WrapLabel("A text");
            KryptonCheckBox checkBox = CheckBox("An option");

            stack.Run(() =>
            {
                stack.Place(label);
                stack.Place(checkBox);
            });

            int expected = 554 - ScrollBar - 2 * ScrollPageLayout.Margin;
            Assert.That(label.Left, Is.EqualTo(ScrollPageLayout.Margin));
            Assert.That(label.Width, Is.EqualTo(expected));
            Assert.That(checkBox.Left, Is.EqualTo(ScrollPageLayout.Margin));
            Assert.That(checkBox.Width, Is.EqualTo(expected));
        }

        [Test]
        public void Place_AWiderPage_GivesTheControlsMoreWidth()
        {
            LauncherWrapLabel label = WrapLabel("A text");
            Action place = () => stack.Place(label);
            stack.Run(place);
            int narrow = label.Width;

            panel.Width = 1200;
            Assert.That(stack.NeedsLayout, Is.True, "the width changed");
            stack.Run(place);

            Assert.That(label.Width, Is.EqualTo(narrow + 1200 - 554));
            Assert.That(stack.NeedsLayout, Is.False);
        }

        [Test]
        public void Place_AButton_KeepsItsWidthAndIsNotStretched()
        {
            KryptonButton button = Button("OK", 248);

            stack.Run(() => stack.Place(button));

            Assert.That(button.Left, Is.EqualTo(ScrollPageLayout.Margin));
            Assert.That(button.Width, Is.EqualTo(Math.Max(248, button.GetPreferredSize(Size.Empty).Width)));
            panel.Width = 1600;
            stack.Run(() => stack.Place(button));
            Assert.That(button.Width, Is.EqualTo(Math.Max(248, button.GetPreferredSize(Size.Empty).Width)));
        }

        // --- Height ------------------------------------------------------------------------------------------------

        [Test]
        public void Place_AWrapLabel_IsAsHighAsItsTextAtTheWidthOfThePage()
        {
            LauncherWrapLabel label = WrapLabel(LongText);
            Action place = () => stack.Run(() => stack.Place(label));

            place();
            int narrowHeight = label.Height;
            Assert.That(narrowHeight, Is.EqualTo(label.TextHeight(label.Width)));

            panel.Width = 1600;
            place();

            Assert.That(label.Height, Is.EqualTo(label.TextHeight(label.Width)));
            Assert.That(label.Height, Is.LessThan(narrowHeight), "a wider page needs fewer lines");
        }

        [Test]
        public void Place_StacksTheControlsOneBelowTheOtherWithAGap_AndSkipsAHiddenOne()
        {
            LauncherWrapLabel first = WrapLabel(LongText);
            LauncherWrapLabel skipped = WrapLabel("hidden text");
            KryptonCheckBox last = CheckBox("An option");
            hidden.Add(skipped);

            stack.Run(() =>
            {
                stack.Place(first);
                stack.Place(skipped);
                stack.Place(last);
            });

            Assert.That(first.Top, Is.EqualTo(8));
            Assert.That(last.Top, Is.EqualTo(first.Bottom + ScrollPageLayout.Gap));
            Assert.That(skipped.Top, Is.Not.EqualTo(last.Top).And.Not.EqualTo(first.Bottom + ScrollPageLayout.Gap));
        }

        [Test]
        public void Place_AKryptonControl_IsAtLeastAsHighAsItsTextNeeds()
        {
            KryptonCheckBox checkBox = CheckBox("An option");
            checkBox.Height = 2;

            stack.Run(() => stack.Place(checkBox));

            Assert.That(checkBox.Height, Is.GreaterThanOrEqualTo(checkBox.GetPreferredSize(Size.Empty).Height));
        }

        // --- Rows --------------------------------------------------------------------------------------------------

        [Test]
        public void PlaceRow_PutsTheButtonsSideBySideAtTheirWidths_WhenTheyFit()
        {
            KryptonButton first = Button("First", 140);
            KryptonButton second = Button("Second", 140);

            stack.Run(() => stack.PlaceRow(first, second));

            Assert.That(second.Top, Is.EqualTo(first.Top));
            Assert.That(first.Left, Is.EqualTo(ScrollPageLayout.Margin));
            Assert.That(second.Left, Is.EqualTo(first.Right + ScrollPageLayout.Gap));
        }

        [Test]
        public void PlaceRow_StartsANewLine_WhereTheButtonsDoNotFit()
        {
            KryptonButton first = Button("First", 300);
            KryptonButton second = Button("Second", 300);
            KryptonButton third = Button("Third", 100);

            stack.Run(() => stack.PlaceRow(first, second, third));

            Assert.That(first.Left, Is.EqualTo(ScrollPageLayout.Margin));
            Assert.That(second.Left, Is.EqualTo(ScrollPageLayout.Margin), "the second one does not fit next to the first");
            Assert.That(second.Top, Is.GreaterThanOrEqualTo(first.Bottom + ScrollPageLayout.Gap));
            Assert.That(third.Top, Is.EqualTo(second.Top), "the small one fits next to the second");
            Assert.That(third.Left, Is.EqualTo(second.Right + ScrollPageLayout.Gap));
        }

        [Test]
        public void PlaceRow_AHiddenButton_TakesNoRoom()
        {
            KryptonButton first = Button("First", 140);
            KryptonButton second = Button("Second", 140);
            KryptonCheckBox below = CheckBox("An option");
            hidden.Add(first);

            stack.Run(() =>
            {
                stack.PlaceRow(first, second);
                stack.Place(below);
            });

            Assert.That(second.Left, Is.EqualTo(ScrollPageLayout.Margin));
            Assert.That(below.Top, Is.EqualTo(second.Bottom + ScrollPageLayout.Gap));
        }

        [Test]
        public void PlaceBeside_GivesTheTextTheWidthThatIsLeftOfTheCheckBox()
        {
            KryptonCheckBox checkBox = CheckBox("Show");
            checkBox.Width = 116;
            LauncherWrapLabel text = WrapLabel(LongText);

            int textWidth = 0;
            stack.Run(() => textWidth = stack.PlaceBeside(checkBox, text));

            Assert.That(text.Left, Is.EqualTo(checkBox.Right + ScrollPageLayout.Gap));
            Assert.That(text.Right, Is.EqualTo(ScrollPageLayout.Margin + stack.ContentWidth));
            Assert.That(text.Width, Is.EqualTo(textWidth));
            Assert.That(text.Top, Is.EqualTo(checkBox.Top));
            Assert.That(text.Height, Is.EqualTo(text.TextHeight(text.Width)));
        }

        // --- A block that does not scroll (the content of a group box, the info bar) --------------------------------

        [Test]
        public void ABlockWithItsOwnMargin_UsesItAndKeepsNoRoomForAScrollBar()
        {
            var block = new ScrollPageLayout(panel, control => !hidden.Contains(control), 6, false);
            LauncherWrapLabel label = WrapLabel("A text");

            block.Run(() => block.Place(label));

            Assert.That(label.Left, Is.EqualTo(6));
            Assert.That(label.Width, Is.EqualTo(554 - 2 * 6));
            Assert.That(block.ContentWidth, Is.EqualTo(554 - 2 * 6));
        }

        [Test]
        public void ContentHeight_IsWhatThePassPlaced_WithTheSpaceBelowTheLastControl()
        {
            LauncherWrapLabel label = WrapLabel(LongText);
            KryptonButton button = Button("A button");

            stack.Run(() =>
            {
                stack.Place(label);
                stack.PlaceRow(button);
            });

            Assert.That(stack.ContentHeight, Is.EqualTo(button.Bottom + ScrollPageLayout.Gap));
        }

        [Test]
        public void ContentHeight_OfAnEmptyPass_IsTheSpaceAboveTheFirstControl()
        {
            LauncherWrapLabel skipped = WrapLabel("hidden text");
            hidden.Add(skipped);

            stack.Run(() => stack.Place(skipped));

            Assert.That(stack.ContentHeight, Is.EqualTo(8));
        }

        [Test]
        public void Place_WithAnIndent_StartsFurtherRightAndIsNarrowerByIt()
        {
            LauncherWrapLabel label = WrapLabel("A line under the field of a row");

            stack.Run(() => stack.Place(label, 100));

            Assert.That(label.Left, Is.EqualTo(ScrollPageLayout.Margin + 100));
            Assert.That(label.Right, Is.EqualTo(ScrollPageLayout.Margin + stack.ContentWidth), "the right edge of the content");
        }

        [Test]
        public void Place_ARadioButton_IsAsHighAsItsTextNeeds()
        {
            var radioButton = new KryptonRadioButton();
            radioButton.Values.Text = "A choice";
            Add(radioButton, 100, 2);

            stack.Run(() => stack.PlaceRow(radioButton));

            Assert.That(radioButton.Height, Is.GreaterThanOrEqualTo(radioButton.GetPreferredSize(Size.Empty).Height));
        }

        // --- Rows of a form ------------------------------------------------------------------------------------------

        [Test]
        public void PlaceField_PutsTheLabelAndTheFieldOnOneLine_WithTheFieldAfterTheLabelColumn()
        {
            KryptonLabel label = Add(new KryptonLabel { AutoSize = false }, 60, 20);
            TextBox field = Add(new TextBox(), 40, 23);

            stack.Run(() => stack.PlaceField(label, 120, field, 200));

            Assert.That(label.Left, Is.EqualTo(ScrollPageLayout.Margin));
            Assert.That(label.Width, Is.EqualTo(120), "the column of the labels");
            Assert.That(field.Left, Is.EqualTo(ScrollPageLayout.Margin + 120 + ScrollPageLayout.Gap));
            Assert.That(field.Width, Is.EqualTo(200));
            Assert.That(label.Top + label.Height / 2, Is.EqualTo(field.Top + field.Height / 2).Within(1), "centered on one line");
        }

        [Test]
        public void PlaceField_AFieldWithoutAWidth_TakesWhatTheButtonsLeave()
        {
            KryptonLabel label = Add(new KryptonLabel { AutoSize = false }, 60, 20);
            TextBox field = Add(new TextBox(), 40, 23);
            KryptonButton browse = Button("...", 40);
            KryptonButton detect = Button("Detect", 100);

            stack.Run(() => stack.PlaceField(label, 100, field, 0, browse, detect));

            Assert.That(browse.Left, Is.EqualTo(field.Right + ScrollPageLayout.Gap));
            Assert.That(detect.Left, Is.EqualTo(browse.Right + ScrollPageLayout.Gap));
            Assert.That(detect.Right, Is.EqualTo(ScrollPageLayout.Margin + stack.ContentWidth), "the buttons end at the right edge");
            Assert.That(field.Width, Is.GreaterThan(120));
            Assert.That(detect.Top + detect.Height / 2, Is.EqualTo(field.Top + field.Height / 2).Within(1));
        }

        [Test]
        public void PlaceField_AWiderPage_GivesTheFieldMoreWidthAndKeepsTheButtonsAtTheirWidth()
        {
            KryptonLabel label = Add(new KryptonLabel { AutoSize = false }, 60, 20);
            TextBox field = Add(new TextBox(), 40, 23);
            KryptonButton detect = Button("Detect", 100);
            Action place = () => stack.PlaceField(label, 100, field, 0, detect);
            stack.Run(place);
            int narrow = field.Width;
            int buttonWidth = detect.Width;

            panel.Width = 1200;
            stack.Run(place);

            Assert.That(field.Width, Is.EqualTo(narrow + 1200 - 554));
            Assert.That(detect.Width, Is.EqualTo(buttonWidth));
        }

        [Test]
        public void PlaceField_WhereTheFieldWouldBeTooNarrow_PutsTheButtonsBelowIt()
        {
            panel.Width = 330;
            KryptonLabel label = Add(new KryptonLabel { AutoSize = false }, 60, 20);
            TextBox field = Add(new TextBox(), 40, 23);
            KryptonButton browse = Button("...", 60);
            KryptonButton detect = Button("Auto-detect", 140);
            KryptonCheckBox below = CheckBox("An option");

            stack.Run(() =>
            {
                stack.PlaceField(label, 100, field, 0, browse, detect);
                stack.Place(below);
            });

            Assert.That(field.Width, Is.EqualTo(stack.ContentWidth - 100 - ScrollPageLayout.Gap), "the whole width of the column");
            Assert.That(browse.Top, Is.GreaterThanOrEqualTo(field.Bottom + ScrollPageLayout.Gap));
            Assert.That(browse.Left, Is.EqualTo(field.Left), "below the field");
            Assert.That(detect.Top, Is.EqualTo(browse.Top));
            Assert.That(below.Top, Is.EqualTo(browse.Bottom + ScrollPageLayout.Gap));
        }

        [Test]
        public void PlaceField_AHiddenField_HidesTheRow()
        {
            KryptonLabel label = Add(new KryptonLabel { AutoSize = false }, 60, 20);
            TextBox field = Add(new TextBox(), 40, 23);
            KryptonCheckBox below = CheckBox("An option");
            hidden.Add(field);

            stack.Run(() =>
            {
                stack.PlaceField(label, 100, field, 200);
                stack.Place(below);
            });

            Assert.That(below.Top, Is.EqualTo(8), "the row took no room");
        }

        // --- A text that cannot wrap -------------------------------------------------------------------------------

        [Test]
        public void Run_ACheckBoxWiderThanThePage_GivesAllControlsItsWidth_SoThePageScrollsSideways()
        {
            KryptonCheckBox wide = CheckBox(new string('W', 120));
            LauncherWrapLabel label = WrapLabel(LongText);
            int needed = wide.GetPreferredSize(Size.Empty).Width;
            Assume.That(needed, Is.GreaterThan(554), "the text must not fit on the page");

            stack.Run(() =>
            {
                stack.Place(wide);
                stack.Place(label);
            });

            Assert.That(stack.ContentWidth, Is.EqualTo(needed));
            Assert.That(wide.Width, Is.EqualTo(needed), "no text is cut off");
            Assert.That(label.Width, Is.EqualTo(needed), "the content has one width");
            Assert.That(label.Height, Is.EqualTo(label.TextHeight(label.Width)));
        }

        [Test]
        public void Run_APageThatFits_NeedsNoSecondPassAndKeepsTheWidthOfThePage()
        {
            KryptonCheckBox shortText = CheckBox("Short");
            int passes = 0;

            stack.Run(() =>
            {
                passes++;
                stack.Place(shortText);
            });

            Assert.That(passes, Is.EqualTo(1));
            Assert.That(stack.ContentWidth, Is.EqualTo(554 - ScrollBar - 2 * ScrollPageLayout.Margin));
        }

        // --- The same result every time ---------------------------------------------------------------------------

        [Test]
        public void Run_Twice_GivesTheSameBounds()
        {
            LauncherWrapLabel label = WrapLabel(LongText);
            KryptonButton first = Button("First");
            KryptonButton second = Button("Second");
            KryptonCheckBox checkBox = CheckBox("An option");
            Action place = () =>
            {
                stack.Place(label);
                stack.PlaceRow(first, second);
                stack.Place(checkBox);
            };

            stack.Run(place);
            List<Rectangle> before = panel.Controls.Cast<Control>().Select(control => control.Bounds).ToList();
            stack.Run(place);

            Assert.That(panel.Controls.Cast<Control>().Select(control => control.Bounds).ToList(), Is.EqualTo(before));
        }

        [Test]
        public void Run_KeepsTheScrollPosition()
        {
            LauncherWrapLabel label = WrapLabel(LongText);
            KryptonButton[] buttons = Enumerable.Range(0, 30).Select(i => Button("Button " + i)).ToArray();
            Action place = () =>
            {
                stack.Place(label);
                foreach (KryptonButton button in buttons)
                    stack.Place(button);
            };
            // The .NET Framework scrolls only a panel whose window exists (ScrollableControl.AutoScrollPosition does nothing
            // until Created), so on Windows the panel gets its window, without being shown; Mono scrolls a panel without one.
            // Whether Windows moves the controls of a window that is not shown does not matter: the top is taken before the
            // scrolling, and the layout places every control by the scroll position.
            bool windows = Environment.OSVersion.Platform == PlatformID.Win32NT;
            if (windows)
                panel.CreateControl();
            stack.Run(place);
            panel.PerformLayout();
            int top = buttons[10].Top;

            panel.AutoScrollPosition = new Point(0, 200);
            int scrolled = panel.AutoScrollPosition.Y;
            if (!windows && scrolled == 0)
                Assert.Ignore("This system does not scroll a panel without a window.");
            Assert.That(scrolled, Is.LessThan(0), "the page is scrolled down");
            stack.Run(place);

            Assert.That(panel.AutoScrollPosition.Y, Is.EqualTo(scrolled), "the scroll position is kept");
            Assert.That(buttons[10].Top, Is.EqualTo(top + scrolled), "the content stays where the scroll bar shows it");
        }
    }
}

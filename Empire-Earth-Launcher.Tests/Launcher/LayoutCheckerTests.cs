using System;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Empire_Earth_Launcher.Tests.TestSupport;
using Krypton.Toolkit;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Launcher
{
    /// <summary>
    /// The geometry rules of <see cref="LayoutChecker"/> against small layouts that break them on purpose: a rule that finds
    /// nothing in the pages would prove nothing if it could not find anything at all (ADR 0012 amendment).
    /// </summary>
    [TestFixture]
    [Category(TestCategories.WinForms)]
    [Apartment(ApartmentState.STA)]
    public class LayoutCheckerTests
    {
        private Panel page;

        [SetUp]
        public void SetUp()
        {
            WinForms.RequireDisplay();
            page = new Panel { Name = "page", Size = new System.Drawing.Size(300, 200) };
        }

        [TearDown]
        public void TearDown()
        {
            page.Dispose();
        }

        private T Add<T>(T control, string name, int x, int y, int width, int height) where T : Control
        {
            control.Name = name;
            control.SetBounds(x, y, width, height);
            page.Controls.Add(control);
            return control;
        }

        // --- Visibility --------------------------------------------------------------------------------------------

        [Test]
        public void IsSelfVisible_IgnoresAHiddenParent_ButNotTheControlsOwnFlag()
        {
            using (var window = new Form())
            {
                Label shown = Add(new Label(), "shown", 0, 0, 50, 20);
                Label hidden = Add(new Label(), "hidden", 0, 30, 50, 20);
                hidden.Visible = false;
                window.Controls.Add(page);

                Assert.That(shown.Visible, Is.False, "Control.Visible reads false in a window that is not shown");
                Assert.That(LayoutChecker.IsSelfVisible(shown), Is.True);
                Assert.That(LayoutChecker.IsSelfVisible(hidden), Is.False);
                Assert.That(LayoutChecker.VisibleDescendants(page).Select(control => control.Name), Is.EqualTo(new[] { "shown" }));
            }
        }

        // --- Overlap ----------------------------------------------------------------------------------------------

        [Test]
        public void Overlaps_FindsTwoSiblingsThatIntersect()
        {
            Add(new Label(), "header", 10, 10, 200, 20);
            Add(new Label(), "description", 10, 20, 200, 40);

            string[] found = LayoutChecker.Overlaps(page).ToArray();

            Assert.That(found, Has.Length.EqualTo(1));
            Assert.That(found[0], Does.Contain("header").And.Contain("description").And.Contain("intersect in 200x10"));
        }

        [Test]
        public void Overlaps_IgnoresTouchingControls_HiddenControls_AndTheNamedOverlay()
        {
            Add(new Label(), "first", 10, 10, 200, 20);
            Add(new Label(), "touching", 10, 30, 200, 20);
            Label hidden = Add(new Label(), "hidden", 10, 10, 200, 20);
            hidden.Visible = false;
            Add(new Label(), "networkCheckKryptonLinkLabel", 10, 40, 200, 20);

            Assert.That(LayoutChecker.Overlaps(page), Is.Empty);
        }

        [Test]
        public void Overlaps_LooksInsideContainers()
        {
            var group = Add(new Panel(), "group", 0, 0, 280, 180);
            var first = new Label { Name = "inner1", Bounds = new System.Drawing.Rectangle(5, 5, 100, 20) };
            var second = new Label { Name = "inner2", Bounds = new System.Drawing.Rectangle(50, 10, 100, 20) };
            group.Controls.Add(first);
            group.Controls.Add(second);

            Assert.That(LayoutChecker.Overlaps(page).Single(), Does.Contain("group/inner1").And.Contain("group/inner2"));
        }

        // --- Outside ----------------------------------------------------------------------------------------------

        [Test]
        public void OutsideParent_FindsAControlWiderThanThePage_AndOneBelowIt()
        {
            Add(new Label(), "wide", 10, 10, 400, 20);
            Add(new Label(), "low", 10, 190, 100, 40);

            string[] found = LayoutChecker.OutsideParent(page).ToArray();

            Assert.That(found, Has.Length.EqualTo(2));
            Assert.That(found[0], Does.Contain("wide"));
            Assert.That(found[1], Does.Contain("low"));
        }

        [Test]
        public void OutsideParent_AScrollingPageMayBeLongerButKeepsTheScrollBarFree()
        {
            page.AutoScroll = true;
            Add(new Label(), "long", 10, 10, 100, 500);
            Add(new Label(), "fits", 10, 10, 200, 20);
            Add(new Label(), "nearlyFull", 10, 40, 290 - SystemInformation.VerticalScrollBarWidth, 20);
            Add(new Label(), "underTheBar", 10, 70, 285, 20);

            string[] found = LayoutChecker.OutsideParent(page).ToArray();

            Assert.That(found, Has.Length.EqualTo(1), string.Join("; ", found));
            Assert.That(found[0], Does.Contain("underTheBar"));
        }

        // --- Texts ------------------------------------------------------------------------------------------------

        [Test]
        public void ClippedWrapLabels_FindsALabelThatIsTooLowForItsText()
        {
            LauncherWrapLabel label = WinForms.CreateOrIgnore(() => new LauncherWrapLabel { AutoSize = false });
            Add(label, "text", 10, 10, 120, 12);
            label.Text = string.Join(" ", Enumerable.Repeat("Lots of words that wrap over many lines.", 6));

            Assert.That(LayoutChecker.ClippedWrapLabels(page).Single(), Does.Contain("text").And.Contain("needs"));

            label.Height = label.TextHeight(label.Width);

            Assert.That(LayoutChecker.ClippedWrapLabels(page), Is.Empty);
        }

        [Test]
        public void TooNarrow_FindsAButtonThatIsTooNarrowForItsText()
        {
            var button = new KryptonButton();
            button.Values.Text = "A text that needs much more than ten pixels";
            Add(button, "button", 10, 10, 10, 30);
            string[] found = null;
            WinForms.RunOrIgnoreWithoutWindows(() => found = LayoutChecker.TooNarrow(page).ToArray());

            Assert.That(found.Single(), Does.Contain("button"));
        }

        [Test]
        public void TooNarrow_FindsARadioButtonThatIsTooNarrowForItsText()
        {
            var radioButton = new KryptonRadioButton { AutoSize = false };
            radioButton.Values.Text = "A text that needs much more than ten pixels";
            Add(radioButton, "radioButton", 10, 10, 10, 30);
            string[] found = null;
            WinForms.RunOrIgnoreWithoutWindows(() => found = LayoutChecker.TooNarrow(page).ToArray());

            Assert.That(found.Single(), Does.Contain("radioButton"));
        }

        [Test]
        public void TooNarrow_ExcusesTheControlsOfNarrowByDesign_AndOnlyThose()
        {
            var face = new KryptonButton();
            face.Values.Text = "A text that needs much more than ten pixels";
            Add(face, "playKryptonButton", 10, 10, 10, 30);
            var other = new KryptonButton();
            other.Values.Text = "A text that needs much more than ten pixels";
            Add(other, "otherButton", 10, 50, 10, 30);
            string[] found = null;
            WinForms.RunOrIgnoreWithoutWindows(() => found = LayoutChecker.TooNarrow(page).ToArray());

            Assert.That(found.Single(), Does.Contain("otherButton"));
            Assert.That(LayoutChecker.NarrowByDesign.Keys, Is.EquivalentTo(new[] { "playKryptonButton", "networkCheckKryptonLinkLabel" }),
                "every excuse is named here, with its reason in LayoutChecker");
        }

        // --- Growth -----------------------------------------------------------------------------------------------

        [Test]
        public void NotGrowing_FindsAWideControlThatStaysWhenThePageGrows()
        {
            Add(new Label(), "fixed", 10, 10, 250, 20);
            Label anchored = Add(new Label(), "anchored", 10, 40, 250, 20);
            anchored.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Add(new Label(), "small", 10, 70, 50, 20);
            Label centered = Add(new Label(), "centered", 50, 100, 200, 20);
            LayoutChecker.Snapshot narrow = LayoutChecker.Take(page);

            page.Width = 600;
            page.PerformLayout();
            centered.Left = (page.ClientSize.Width - centered.Width) / 2;
            string[] found = LayoutChecker.NotGrowing(page, narrow, LayoutChecker.Take(page)).ToArray();

            Assert.That(found.Single(), Does.Contain("fixed").And.Contain("250 px wide"), string.Join("; ", found));
        }

        [Test]
        public void NotGrowing_AcceptsARadioButton_ThatKeepsTheWidthOfItsText()
        {
            // As wide as more than half of the page, as with a large font in the smallest window.
            var radioButton = new KryptonRadioButton { AutoSize = false };
            radioButton.Values.Text = "The Art of Conquest";
            Add(radioButton, "radioButton", 10, 10, 250, 20);
            LayoutChecker.Snapshot narrow = LayoutChecker.Take(page);

            page.Width = 600;
            page.PerformLayout();

            Assert.That(LayoutChecker.NotGrowing(page, narrow, LayoutChecker.Take(page)), Is.Empty);
        }

        [Test]
        public void NotGrowing_AcceptsADockedPage()
        {
            Panel inner = Add(new Panel(), "inner", 0, 0, 300, 200);
            inner.Dock = DockStyle.Fill;
            LayoutChecker.Snapshot narrow = LayoutChecker.Take(page);

            page.Width = 700;
            page.PerformLayout();

            Assert.That(LayoutChecker.NotGrowing(page, narrow, LayoutChecker.Take(page)), Is.Empty);
        }
    }
}

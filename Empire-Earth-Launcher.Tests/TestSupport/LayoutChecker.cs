using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Krypton.Toolkit;

namespace Empire_Earth_Launcher.Tests.TestSupport
{
    /// <summary>
    /// The geometry rules of the launcher pages (ADR 0012 amendment, 1.1.0): what a page must satisfy at every window size,
    /// language and font, computed from the bounds of the controls after <see cref="Control.PerformLayout()"/>. Every rule
    /// returns one line per violation (empty if the page keeps it), naming the controls and their bounds.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The rules work on controls that were never shown: a page of the launcher is created hidden and receives its state
    /// before the window is shown (<c>MainForm</c> constructor), and exactly there the first overlap of the Game settings
    /// page came from (a layout that asked <see cref="Control.Visible"/>, which reads false then). The rules therefore use
    /// <see cref="IsSelfVisible"/>, the flag the control itself carries, and never <see cref="Control.Visible"/>.
    /// </para>
    /// <para>
    /// Mono has other fonts than Windows, so the rules compare the page with itself (its own text heights, its own preferred
    /// sizes), never with fixed pixel values; whether the page looks right is for the PNGs of the CI run and the manual cases of
    /// the test plan. The rules are no UI automation: nothing is clicked or shown.
    /// </para>
    /// </remarks>
    internal static class LayoutChecker
    {
        /// <summary>Controls that overlap another control on purpose, by field name; each entry says why.</summary>
        public static readonly IReadOnlyDictionary<string, string> IntentionalOverlays = new Dictionary<string, string>
        {
            // The link of the Play page lies over the empty player list (test plan WP9-14).
            { "networkCheckKryptonLinkLabel", "lies over the empty player list" },
        };

        private static readonly FieldInfo MonoVisibleField =
            typeof(Control).GetField("is_visible", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly MethodInfo GetStateMethod =
            typeof(Control).GetMethod("GetState", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(int) }, null);

        /// <summary><c>Control.STATE_VISIBLE</c> of the .NET Framework.</summary>
        private const int StateVisible = 0x2;

        private static bool IsWindows
        {
            get { return Environment.OSVersion.Platform == PlatformID.Win32NT; }
        }

        /// <summary>
        /// True if the control itself is set visible, whatever its parents are: <see cref="Control.Visible"/> reads false for
        /// every control of a page whose window is not shown yet, which is how the launcher creates and fills its pages.
        /// </summary>
        public static bool IsSelfVisible(Control control)
        {
            if (IsWindows && GetStateMethod != null)
                return (bool)GetStateMethod.Invoke(control, new object[] { StateVisible });
            if (MonoVisibleField != null)
                return (bool)MonoVisibleField.GetValue(control);
            return control.Visible;
        }

        /// <summary>
        /// True for the controls that hold other controls of the page (pages, panels, group boxes); the controls inside a text
        /// box, a grid or a combo box are parts of that control and not part of the layout of the page.
        /// </summary>
        private static bool IsContainer(Control control)
        {
            return control is UserControl || control is Panel || control is GroupBox || control is KryptonPanel ||
                   control is KryptonGroupBox || control is KryptonGroup || control is KryptonHeaderGroup ||
                   control.GetType().Name.EndsWith("Panel", StringComparison.Ordinal);
        }

        /// <summary>
        /// The controls of <paramref name="root"/> that are visible themselves, depth first, the root excluded; only containers
        /// are entered.
        /// </summary>
        public static IEnumerable<Control> VisibleDescendants(Control root)
        {
            foreach (Control child in root.Controls)
            {
                if (!IsSelfVisible(child))
                    continue;
                yield return child;
                if (!IsContainer(child))
                    continue;
                foreach (Control descendant in VisibleDescendants(child))
                    yield return descendant;
            }
        }

        /// <summary>The container itself and the containers below it that have visible children.</summary>
        private static IEnumerable<Control> Containers(Control root)
        {
            yield return root;
            foreach (Control control in VisibleDescendants(root).Where(control => IsContainer(control) && control.Controls.Count > 0))
                yield return control;
        }

        private static IEnumerable<Control> VisibleChildren(Control container)
        {
            return container.Controls.Cast<Control>().Where(IsSelfVisible);
        }

        /// <summary>The names of <paramref name="control"/> and its parents below <paramref name="root"/>, as "a/b/c".</summary>
        public static string PathOf(Control root, Control control)
        {
            var names = new List<string>();
            for (Control current = control; current != null && current != root; current = current.Parent)
                names.Insert(0, string.IsNullOrEmpty(current.Name) ? current.GetType().Name : current.Name);
            return string.Join("/", names);
        }

        private static string Describe(Control root, Control control)
        {
            Rectangle b = control.Bounds;
            return PathOf(root, control) + " (" + b.X + "," + b.Y + " " + b.Width + "x" + b.Height + ")";
        }

        // --- Rule: no two visible siblings intersect -------------------------------------------------------------

        /// <summary>
        /// Rule 1: no two visible siblings intersect (this is the overlap of the header, the description and the
        /// installation line, and of the book picture over the two buttons). A control of <see cref="IntentionalOverlays"/>
        /// may overlap.
        /// </summary>
        public static IEnumerable<string> Overlaps(Control root)
        {
            foreach (Control container in Containers(root))
            {
                List<Control> children = VisibleChildren(container).ToList();
                for (int i = 0; i < children.Count; i++)
                {
                    for (int j = i + 1; j < children.Count; j++)
                    {
                        Control a = children[i];
                        Control b = children[j];
                        if (IntentionalOverlays.ContainsKey(a.Name) || IntentionalOverlays.ContainsKey(b.Name))
                            continue;
                        Rectangle common = Rectangle.Intersect(a.Bounds, b.Bounds);
                        if (common.Width > 0 && common.Height > 0)
                            yield return "overlap: " + Describe(root, a) + " and " + Describe(root, b) + " intersect in " +
                                         common.Width + "x" + common.Height;
                    }
                }
            }
        }

        // --- Rule: nothing sticks out of its parent ----------------------------------------------------------------

        /// <summary>
        /// Rule 2: no visible child is wider than its parent, or starts left of it; below a parent that does not scroll no
        /// child ends below it either. A parent that scrolls vertically keeps the width of its scroll bar free. A page does not
        /// scroll sideways, except where a text of one line (a check box, a label) is wider than the page: it cannot wrap, so
        /// the content gets its width (<c>ScrollPageLayout</c>); a parent that scrolls may then be as wide as such a control
        /// whose width is just what its text needs.
        /// </summary>
        public static IEnumerable<string> OutsideParent(Control root)
        {
            foreach (Control container in Containers(root))
            {
                var scrolling = container as ScrollableControl;
                bool scrolls = scrolling != null && scrolling.AutoScroll;
                List<Control> children = VisibleChildren(container).Where(child => child.Dock == DockStyle.None).ToList();
                int width = container.ClientSize.Width;
                int height = container.ClientSize.Height;
                if (scrolls && children.Count > 0 && children.Max(child => child.Bottom) > height)
                    width -= SystemInformation.VerticalScrollBarWidth;
                if (scrolls)
                    width = Math.Max(width, children.Where(IsAsWideAsItsText).Select(child => child.Right).DefaultIfEmpty(0).Max());
                foreach (Control child in children)
                {
                    if (child.Right > width)
                        yield return "outside: " + Describe(root, child) + " ends at " + child.Right + ", the width of " +
                                     PathOf(root, container) + " is " + width;
                    else if (child.Left < 0)
                        yield return "outside: " + Describe(root, child) + " starts left of " + PathOf(root, container);
                    if (!scrolls && child.Bottom > height)
                        yield return "outside: " + Describe(root, child) + " ends at " + child.Bottom + ", the height of " +
                                     PathOf(root, container) + " is " + height;
                }
            }
        }

        // --- Rule: texts fit ---------------------------------------------------------------------------------------

        /// <summary>
        /// Rule 3: every wrapping label is as high as its text needs at its width (<see cref="LauncherWrapLabel.TextHeight"/>);
        /// a label with a fixed height, as on the Play page, cuts off a longer translation.
        /// </summary>
        public static IEnumerable<string> ClippedWrapLabels(Control root)
        {
            foreach (LauncherWrapLabel label in VisibleDescendants(root).OfType<LauncherWrapLabel>())
            {
                if (string.IsNullOrEmpty(label.Text))
                    continue;
                int needed = label.TextHeight(label.Width);
                if (label.Height < needed)
                    yield return "clipped: " + Describe(root, label) + " needs " + needed + " px for its text, has " + label.Height;
            }
        }

        /// <summary>
        /// Rule 4: a Krypton button, check box or label is at least as wide as its text and its glyph need
        /// (<see cref="Control.GetPreferredSize"/>), so that no translation is cut off.
        /// </summary>
        public static IEnumerable<string> TooNarrow(Control root)
        {
            foreach (Control control in VisibleDescendants(root).Where(IsKryptonTextControl))
            {
                int preferred;
                if (!TryPreferredWidth(control, out preferred))
                    continue;
                if (control.Width < preferred)
                    yield return "narrow: " + Describe(root, control) + " needs " + preferred + " px of width for its text";
            }
        }

        /// <summary>True for a Krypton text control that is not wider than its text needs (it could not be narrower).</summary>
        private static bool IsAsWideAsItsText(Control control)
        {
            int preferred;
            return IsKryptonTextControl(control) && TryPreferredWidth(control, out preferred) && control.Width <= preferred;
        }

        private static bool IsKryptonTextControl(Control control)
        {
            return control is KryptonButton || control is KryptonCheckBox || control is KryptonLabel || control is KryptonCheckButton;
        }

        /// <summary>
        /// The preferred width; false outside Windows where a Krypton control calls a Windows library to measure itself
        /// (the rule runs on Windows, CI and laptop).
        /// </summary>
        private static bool TryPreferredWidth(Control control, out int width)
        {
            try
            {
                width = control.GetPreferredSize(Size.Empty).Width;
                return true;
            }
            catch (Exception ex) when (!IsWindows && (ex is DllNotFoundException || ex is EntryPointNotFoundException))
            {
                width = 0;
                return false;
            }
        }

        // --- Rule: the content grows with the page ----------------------------------------------------------------

        /// <summary>The bounds of every visible control of a page, to compare the page at two sizes.</summary>
        public sealed class Snapshot
        {
            internal Dictionary<Control, Rectangle> Bounds { get; } = new Dictionary<Control, Rectangle>();

            internal Dictionary<Control, int> ParentWidth { get; } = new Dictionary<Control, int>();

            /// <summary>The parents whose content is as wide as a text of one line needs (<see cref="IsAsWideAsItsText"/>).</summary>
            internal HashSet<Control> TextBoundParents { get; } = new HashSet<Control>();
        }

        /// <summary>Records the bounds of the visible controls of <paramref name="root"/>.</summary>
        public static Snapshot Take(Control root)
        {
            var snapshot = new Snapshot();
            foreach (Control control in VisibleDescendants(root))
            {
                snapshot.Bounds[control] = control.Bounds;
                snapshot.ParentWidth[control] = control.Parent.ClientSize.Width;
                if (IsAsWideAsItsText(control))
                    snapshot.TextBoundParents.Add(control.Parent);
            }
            return snapshot;
        }

        /// <summary>
        /// Rule 5: every control that fills at least half of its parent's width keeps doing so when the page gets wider: it
        /// grows with the parent, or (a picture, a block of fixed width) moves to stay centered. "The content does not
        /// grow when the window is maximized" is this rule failing for the whole page. Controls that are not visible in
        /// both snapshots are ignored, and so are the controls of a parent whose width a text of one line dictates (it cannot
        /// wrap, so the page scrolls sideways instead of growing) and buttons: a button has the width its text needs and is
        /// not stretched over a wide page (a button as wide as half of a small page, as with a large font, is no content that
        /// has to grow).
        /// </summary>
        public static IEnumerable<string> NotGrowing(Control root, Snapshot narrow, Snapshot wide)
        {
            foreach (KeyValuePair<Control, Rectangle> pair in narrow.Bounds)
            {
                Control control = pair.Key;
                if (control is KryptonButton || narrow.TextBoundParents.Contains(control.Parent))
                    continue;
                Rectangle before = pair.Value;
                Rectangle after;
                if (!wide.Bounds.TryGetValue(control, out after))
                    continue;
                int parentBefore = narrow.ParentWidth[control];
                int parentAfter = wide.ParentWidth[control];
                int extra = parentAfter - parentBefore;
                if (extra <= 0 || before.Width * 2 < parentBefore)
                    continue;
                bool grew = after.Width - before.Width >= extra * 3 / 4;
                bool centered = after.Width == before.Width && after.Left - before.Left >= extra * 2 / 5;
                if (!grew && !centered)
                    yield return "not growing: " + PathOf(root, control) + " is " + before.Width + " px wide in a parent of " +
                                 parentBefore + " px and " + after.Width + " px in a parent of " + parentAfter + " px";
            }
        }
    }
}

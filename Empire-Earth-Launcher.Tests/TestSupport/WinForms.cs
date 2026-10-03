using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows.Forms;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.TestSupport
{
    /// <summary>
    /// Helpers of the tests in the category <see cref="TestCategories.WinForms"/>: they create controls without showing them and
    /// paint them into a bitmap the way Windows has them painted on <c>WM_PAINT</c>.
    /// </summary>
    internal static class WinForms
    {
        private static readonly MethodInfo OnPaintMethod =
            typeof(Control).GetMethod("OnPaint", BindingFlags.Instance | BindingFlags.NonPublic);

        private static bool IsWindows
        {
            get { return Environment.OSVersion.Platform == PlatformID.Win32NT; }
        }

        /// <summary>
        /// Ignores the test where WinForms has no display: Windows always has one; elsewhere (Mono) WinForms needs an X server,
        /// for example <c>xvfb-run</c>.
        /// </summary>
        public static void RequireDisplay()
        {
            if (!IsWindows && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY")))
                Assert.Ignore("WinForms needs a display here; run the tests with xvfb-run.");
        }

        /// <summary>
        /// Creates a page or window of the launcher. Outside Windows a page whose Krypton controls call Windows libraries
        /// (<c>uxtheme.dll</c>, GDI) cannot be created; the test is ignored there and runs on Windows (CI, laptop).
        /// </summary>
        public static T CreateOrIgnore<T>(Func<T> create) where T : Control
        {
            try
            {
                return create();
            }
            catch (Exception ex) when (!IsWindows && (ex is DllNotFoundException || ex is EntryPointNotFoundException))
            {
                Assert.Ignore(typeof(T).Name + " needs Windows: " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Runs <paramref name="action"/>; outside Windows, where Krypton controls that react to it call Windows libraries
        /// (for example <c>KryptonComboBox</c> calls <c>GetWindow</c> of <c>user32.dll</c>), the test is ignored when it fails
        /// for that reason. It runs on Windows (CI, laptop).
        /// </summary>
        public static void RunOrIgnoreWithoutWindows(Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex) when (!IsWindows && (ex is DllNotFoundException || ex is EntryPointNotFoundException))
            {
                Assert.Ignore("needs Windows: " + ex.Message);
            }
        }

        /// <summary>
        /// Calls <c>OnPaint</c> of <paramref name="control"/> with the graphics of a bitmap of its size, as
        /// <c>Control.PaintWithErrorHandling</c> does for <c>WM_PAINT</c>: an exception thrown here is what made WinForms
        /// draw a red X instead of the control until the launcher was restarted.
        /// </summary>
        public static void Paint(Control control)
        {
            using (var bitmap = new Bitmap(Math.Max(1, control.Width), Math.Max(1, control.Height)))
            using (Graphics graphics = Graphics.FromImage(bitmap))
                Paint(control, graphics);
        }

        /// <summary>Calls <c>OnPaint</c> of <paramref name="control"/> with <paramref name="graphics"/>.</summary>
        public static void Paint(Control control, Graphics graphics)
        {
            try
            {
                OnPaintMethod.Invoke(control, new object[] { new PaintEventArgs(graphics, new Rectangle(Point.Empty, control.Size)) });
            }
            catch (TargetInvocationException ex)
            {
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            }
        }

        /// <summary>Every control below <paramref name="parent"/>, depth first.</summary>
        public static IEnumerable<Control> Descendants(Control parent)
        {
            foreach (Control child in parent.Controls)
            {
                yield return child;
                foreach (Control descendant in Descendants(child))
                    yield return descendant;
            }
        }

        /// <summary>True if GDI+ can still use <paramref name="font"/>; a disposed font fails with <see cref="ArgumentException"/>.</summary>
        public static bool IsUsable(Font font)
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
    }
}

using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Empire_Earth_Launcher
{
    /// <summary>
    /// Brings the main window to the front when a second launcher handed over a product. The second launcher was started by
    /// the user and has the foreground right; it passes it on with <c>AllowSetForegroundWindow</c>
    /// (<see cref="Empire_Earth_Launcher.Core.Platform.WindowsInstanceChannel"/>), so <c>SetForegroundWindow</c> works here.
    /// Without the right Windows only flashes the taskbar button, which is an acceptable result. Checked on real Windows by the
    /// test plan.
    /// </summary>
    internal static class ForegroundWindow
    {
        private const int ShowRestore = 9;

        /// <summary>Restores the window if it is minimized and makes it the foreground window; does nothing before it exists.</summary>
        public static void BringToFront(Form form)
        {
            if (form == null || form.IsDisposed || !form.IsHandleCreated)
                return;
            IntPtr handle = form.Handle;
            if (form.WindowState == FormWindowState.Minimized)
                ShowWindow(handle, ShowRestore);
            SetForegroundWindow(handle);
        }

        /// <summary>
        /// True if <paramref name="form"/> cannot take input because a modal dialog (message box, file dialog) of the launcher
        /// is open: Windows disables the owner while its dialog runs, but still delivers <c>WM_COPYDATA</c> to the thread.
        /// </summary>
        public static bool IsBlockedByDialog(Form form)
        {
            return form != null && !form.IsDisposed && form.IsHandleCreated && !IsWindowEnabled(form.Handle);
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowEnabled(IntPtr window);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr window);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ShowWindow(IntPtr window, int command);
    }
}

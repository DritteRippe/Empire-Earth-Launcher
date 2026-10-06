using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Empire_Earth_Launcher.Core.Logging;

namespace Empire_Earth_Launcher.Core.Platform
{
    /// <summary>
    /// <see cref="IWindowSystem"/> with <c>user32.dll</c> (ADR 0010 amendment of 1.1.0): <c>GetForegroundWindow</c> and
    /// <c>GetWindowThreadProcessId</c> for the foreground process, <c>EnumWindows</c> with <c>IsWindowVisible</c> and
    /// <c>GetWindow(GW_OWNER)</c> for the window of a process, <c>SetForegroundWindow</c> and <c>AllowSetForegroundWindow</c> for
    /// the right. Checked on real Windows by the test plan (WP6-18).
    /// </summary>
    /// <remarks>
    /// A thin adapter: a missing <c>user32.dll</c> (the tests under Mono) is logged once and gives the neutral value (no
    /// foreground process, no window, refused), never an exception (ADR 0013). The class changes no window except through
    /// <see cref="SetForegroundWindow"/>.
    /// </remarks>
    public sealed class WindowsWindowSystem : IWindowSystem
    {
        private const int GwOwner = 4;

        private readonly ILogger logger;
        private int unavailableLogged;

        public WindowsWindowSystem(ILogger logger)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public int GetForegroundProcessId()
        {
            try
            {
                IntPtr window = GetForegroundWindow();
                if (window == IntPtr.Zero)
                    return 0;
                GetWindowThreadProcessId(window, out int processId);
                return processId;
            }
            catch (Exception ex) when (ex is DllNotFoundException || ex is EntryPointNotFoundException)
            {
                LogUnavailable(ex);
                return 0;
            }
        }

        public IntPtr FindVisibleTopLevelWindow(int processId)
        {
            IntPtr found = IntPtr.Zero;
            try
            {
                EnumWindows((window, parameter) =>
                {
                    GetWindowThreadProcessId(window, out int owner);
                    if (owner != processId || !IsWindowVisible(window) || GetWindow(window, GwOwner) != IntPtr.Zero)
                        return true;
                    found = window;
                    return false;
                }, IntPtr.Zero);
            }
            catch (Exception ex) when (ex is DllNotFoundException || ex is EntryPointNotFoundException)
            {
                LogUnavailable(ex);
            }
            return found;
        }

        public bool SetForegroundWindow(IntPtr window)
        {
            try
            {
                return SetForegroundWindowNative(window);
            }
            catch (Exception ex) when (ex is DllNotFoundException || ex is EntryPointNotFoundException)
            {
                LogUnavailable(ex);
                return false;
            }
        }

        public bool AllowSetForegroundWindow(int processId)
        {
            try
            {
                if (AllowSetForegroundWindowNative(processId))
                    return true;
                logger.Info("AllowSetForegroundWindow(" + processId + ") failed: " + new Win32Exception(Marshal.GetLastWin32Error()).Message);
                return false;
            }
            catch (Exception ex) when (ex is DllNotFoundException || ex is EntryPointNotFoundException)
            {
                LogUnavailable(ex);
                return false;
            }
        }

        private void LogUnavailable(Exception exception)
        {
            if (System.Threading.Interlocked.Exchange(ref unavailableLogged, 1) == 0)
                logger.Warning("The window functions of Windows are not available; the foreground is not handed to the game.", exception);
        }

        [return: MarshalAs(UnmanagedType.Bool)]
        private delegate bool EnumWindowsProc(IntPtr window, IntPtr parameter);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern int GetWindowThreadProcessId(IntPtr window, out int processId);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr window);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindow(IntPtr window, int command);

        [DllImport("user32.dll", EntryPoint = "SetForegroundWindow")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindowNative(IntPtr window);

        [DllImport("user32.dll", EntryPoint = "AllowSetForegroundWindow", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AllowSetForegroundWindowNative(int processId);
    }
}

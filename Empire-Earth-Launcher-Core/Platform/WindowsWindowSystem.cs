using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Empire_Earth_Launcher.Core.Logging;

namespace Empire_Earth_Launcher.Core.Platform
{
    /// <summary>
    /// <see cref="IWindowSystem"/> with <c>user32.dll</c> (ADR 0010 amendment of 1.1.0): <c>GetForegroundWindow</c> and
    /// <c>GetWindowThreadProcessId</c> for the foreground process, <c>EnumWindows</c> with <c>IsWindowVisible</c> and
    /// <c>GetWindow(GW_OWNER)</c> and <c>WS_EX_TOOLWINDOW</c> for the main window of a process, <c>SetForegroundWindow</c> and
    /// <c>AllowSetForegroundWindow</c> for the right, <c>GetClassName</c>, <c>GetWindowRect</c> and <c>GetWindowLong</c> for
    /// the read-only watch (<see cref="ReadWindow"/>), <c>IsHungAppWindow</c> to see whether a window responds
    /// (<see cref="IsWindowResponding"/>), and <c>PostMessage</c> for the one activation message of A1b
    /// (<see cref="PostActivateMessage"/>). Checked on real Windows by the test plan (WP6-18, WP6-21).
    /// </summary>
    /// <remarks>
    /// A thin adapter: a missing <c>user32.dll</c> (the tests under Mono) is logged once and gives the neutral value (no
    /// foreground process, no window, refused), never an exception (ADR 0013). The class changes no window except through
    /// <see cref="SetForegroundWindow"/>, and posts nothing but the one <c>WM_ACTIVATE</c> of <see cref="PostActivateMessage"/>
    /// (<c>WindowMessageRulesTests</c>).
    /// </remarks>
    public sealed class WindowsWindowSystem : IWindowSystem
    {
        private const int GwOwner = 4;
        private const int GwlStyle = -16;
        private const int GwlExStyle = -20;
        private const int MaxClassName = 256;

        // The one message the launcher posts to a window of another program (A1b): WM_ACTIVATE with WA_ACTIVE.
        private const int WmActivate = 0x0006;
        private const int WaActive = 1;
        private const int ErrorInvalidWindowHandle = 1400;

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
                IntPtr window = GetForegroundWindowNative();
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

        public IntPtr GetForegroundWindow()
        {
            try
            {
                return GetForegroundWindowNative();
            }
            catch (Exception ex) when (ex is DllNotFoundException || ex is EntryPointNotFoundException)
            {
                LogUnavailable(ex);
                return IntPtr.Zero;
            }
        }

        public WindowState ReadWindow(IntPtr window)
        {
            if (window == IntPtr.Zero)
                return null;
            try
            {
                if (!IsWindow(window) || !GetWindowRect(window, out Rect rectangle))
                    return null;
                GetWindowThreadProcessId(window, out int processId);
                var name = new StringBuilder(MaxClassName);
                int length = GetClassName(window, name, name.Capacity);
                return new WindowState(window, processId, length > 0 ? name.ToString() : string.Empty, rectangle.Left, rectangle.Top,
                    rectangle.Right, rectangle.Bottom, (uint)GetWindowLong(window, GwlStyle), (uint)GetWindowLong(window, GwlExStyle));
            }
            catch (Exception ex) when (ex is DllNotFoundException || ex is EntryPointNotFoundException)
            {
                LogUnavailable(ex);
                return null;
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
                    if (owner != processId)
                        return true;
                    // Not the splash of the game ('Loading Game Window' is a tool window that lives a moment before the real one).
                    if (!WindowRules.IsMainWindowCandidate(IsWindowVisible(window), GetWindow(window, GwOwner) != IntPtr.Zero,
                            (uint)GetWindowLong(window, GwlExStyle)))
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

        public bool IsWindowResponding(IntPtr window)
        {
            try
            {
                return !IsHungAppWindow(window);
            }
            catch (Exception ex) when (ex is DllNotFoundException || ex is EntryPointNotFoundException)
            {
                LogUnavailable(ex);
                return true;
            }
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

        public bool PostActivateMessage(IntPtr window, out int error)
        {
            error = 0;
            try
            {
                // Never to "no window" (a message to the thread of the caller) or to a broadcast handle: only to a window that exists.
                if (window == IntPtr.Zero || !IsWindow(window))
                {
                    error = ErrorInvalidWindowHandle;
                    return false;
                }
                if (PostMessageNative(window, WmActivate, new IntPtr(WaActive), IntPtr.Zero))
                    return true;
                error = Marshal.GetLastWin32Error();
                return false;
            }
            catch (Exception ex) when (ex is DllNotFoundException || ex is EntryPointNotFoundException)
            {
                LogUnavailable(ex);
                error = -1;
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

        [StructLayout(LayoutKind.Sequential)]
        private struct Rect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("user32.dll", EntryPoint = "GetForegroundWindow")]
        private static extern IntPtr GetForegroundWindowNative();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindow(IntPtr window);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr window, out Rect rectangle);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr window, StringBuilder name, int maxCount);

        // GetWindowLongW returns the 32-bit styles on 32-bit and 64-bit Windows alike (GWL_STYLE, GWL_EXSTYLE).
        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
        private static extern int GetWindowLong(IntPtr window, int index);

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

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsHungAppWindow(IntPtr window);

        [DllImport("user32.dll", EntryPoint = "SetForegroundWindow")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindowNative(IntPtr window);

        [DllImport("user32.dll", EntryPoint = "AllowSetForegroundWindow", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AllowSetForegroundWindowNative(int processId);

        [DllImport("user32.dll", EntryPoint = "PostMessageW", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool PostMessageNative(IntPtr window, int message, IntPtr wParam, IntPtr lParam);
    }
}

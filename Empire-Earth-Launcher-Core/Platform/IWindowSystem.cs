using System;

namespace Empire_Earth_Launcher.Core.Platform
{
    /// <summary>Values of <see cref="IWindowSystem"/> that an interface cannot carry.</summary>
    public static class ForegroundRight
    {
        /// <summary>The process id for <see cref="IWindowSystem.AllowSetForegroundWindow"/> that means "any process" (<c>ASFW_ANY</c>).</summary>
        public const int AnyProcess = -1;
    }

    /// <summary>The rule that picks the main window of a game among the windows of its process (A1 of 1.1.0).</summary>
    public static class WindowRules
    {
        /// <summary><c>WS_EX_TOOLWINDOW</c>: a window that is not a main window (the splash 'Loading Game Window' of Empire Earth has it).</summary>
        public const long ToolWindowExStyle = 0x80;

        /// <summary>
        /// True if a window can be the main window of a game: visible, without an owner and not a tool window. Empire Earth shows
        /// its splash before the real window; the hand-over and the log must refer to the main window (class 'SSSI Empire Earth').
        /// </summary>
        public static bool IsMainWindowCandidate(bool visible, bool hasOwner, long exStyle)
        {
            return visible && !hasOwner && (exStyle & ToolWindowExStyle) == 0;
        }
    }

    /// <summary>
    /// The windows of the computer as far as the hand-over of the foreground to a started game needs them (ADR 0010
    /// amendment of 1.1.0): who owns the foreground, the first visible top-level window of a process, and the right to take
    /// the foreground, and (for the watch after the hand-over) the foreground window and the rectangle and styles of a window, and whether a window responds.
    /// Nothing else is read or changed, and, once per start, one <c>WM_ACTIVATE</c> goes to the main window of a started game
    /// (A1b, <see cref="PostActivateMessage"/>): nothing else is posted or sent. The launcher never minimizes, hides, closes or
    /// moves a window of the game and never ends a process.
    /// </summary>
    /// <remarks>
    /// The real implementation is <c>WindowsWindowSystem</c> (the only place of the core that calls the foreground functions of
    /// <c>user32.dll</c>, <c>ForegroundRulesTests</c>); the unit tests use a fake. A window is an <see cref="IntPtr"/>
    /// (<c>HWND</c>); <see cref="IntPtr.Zero"/> means "no window".
    /// </remarks>
    public interface IWindowSystem
    {
        /// <summary>The id of the process that owns the foreground window; 0 if there is none or it cannot be read.</summary>
        int GetForegroundProcessId();

        /// <summary>
        /// The foreground window itself; <see cref="IntPtr.Zero"/> if there is none. Windows briefly has none while a window is
        /// created or a wrapper such as dgVoodoo switches the display mode; that is no player who switched to another program.
        /// </summary>
        IntPtr GetForegroundWindow();

        /// <summary>
        /// What can be read of <paramref name="window"/>: process, class, rectangle and styles; null if the window does not
        /// exist (any more). Read-only.
        /// </summary>
        WindowState ReadWindow(IntPtr window);

        /// <summary>
        /// The first visible top-level window without an owner and without <c>WS_EX_TOOLWINDOW</c> that belongs to
        /// <paramref name="processId"/> (<see cref="WindowRules.IsMainWindowCandidate"/>); <see cref="IntPtr.Zero"/> if the
        /// process has none (yet).
        /// </summary>
        IntPtr FindVisibleTopLevelWindow(int processId);

        /// <summary>
        /// False if Windows counts <paramref name="window"/> as not responding (<c>IsHungAppWindow</c>: its thread has not
        /// processed a message for about five seconds, for example while the game loads); true if it responds or if that cannot
        /// be told. Read-only. A message posted to such a window waits in its queue, so the activation signal is not sent then.
        /// </summary>
        bool IsWindowResponding(IntPtr window);

        /// <summary>
        /// <c>SetForegroundWindow</c>: asks Windows to make <paramref name="window"/> the foreground window. Windows may
        /// refuse (the caller owns no foreground right, the process is elevated); false then.
        /// </summary>
        bool SetForegroundWindow(IntPtr window);

        /// <summary>
        /// <c>AllowSetForegroundWindow</c>: lets <paramref name="processId"/> (<see cref="ForegroundRight.AnyProcess"/> for any
        /// process) take the foreground. Only a process that owns the foreground right may grant it; false if Windows refused.
        /// </summary>
        bool AllowSetForegroundWindow(int processId);

        /// <summary>
        /// <c>PostMessage(window, WM_ACTIVATE, WA_ACTIVE, 0)</c>: puts one activation message into the queue of
        /// <paramref name="window"/> (A1b of 1.1.0, <c>ActivationSignal</c>); the only message the launcher posts to a window of
        /// another program. False if Windows refused; <paramref name="error"/> is the Win32 error then (5, ERROR_ACCESS_DENIED,
        /// when the game runs with higher rights: User Interface Privilege Isolation), else 0.
        /// </summary>
        bool PostActivateMessage(IntPtr window, out int error);
    }
}

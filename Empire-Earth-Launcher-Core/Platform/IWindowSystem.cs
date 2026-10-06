using System;

namespace Empire_Earth_Launcher.Core.Platform
{
    /// <summary>Values of <see cref="IWindowSystem"/> that an interface cannot carry.</summary>
    public static class ForegroundRight
    {
        /// <summary>The process id for <see cref="IWindowSystem.AllowSetForegroundWindow"/> that means "any process" (<c>ASFW_ANY</c>).</summary>
        public const int AnyProcess = -1;
    }

    /// <summary>
    /// The windows of the computer as far as the hand-over of the foreground to a started game needs them (ADR 0010
    /// amendment of 1.1.0): who owns the foreground, the first visible top-level window of a process, and the right to take
    /// the foreground. Nothing else is read or changed: the launcher never minimizes, hides, closes or moves a window of the
    /// game and never ends a process.
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
        /// The first visible top-level window without an owner that belongs to <paramref name="processId"/>;
        /// <see cref="IntPtr.Zero"/> if the process has none (yet).
        /// </summary>
        IntPtr FindVisibleTopLevelWindow(int processId);

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
    }
}

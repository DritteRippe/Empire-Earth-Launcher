using System;
using System.Collections.Generic;
using System.Linq;
using Empire_Earth_Launcher.Core.Platform;

namespace Empire_Earth_Launcher.Tests.Fakes
{
    /// <summary>
    /// <see cref="IWindowSystem"/> without windows: one process (the game) owns one window that appears after a number of
    /// looks, the foreground belongs to a process the test sets, and every call is recorded. A successful
    /// <see cref="SetForegroundWindow"/> moves the foreground to the game, as Windows does; a test moves it elsewhere (the
    /// player switched, the launcher took it back) from the delay of its activator.
    /// </summary>
    internal sealed class FakeWindowSystem : IWindowSystem
    {
        private readonly List<IntPtr> setForegroundCalls = new List<IntPtr>();
        private readonly List<int> allowCalls = new List<int>();

        /// <param name="gameProcessId">The process that owns the window.</param>
        public FakeWindowSystem(int gameProcessId)
        {
            GameProcessId = gameProcessId;
        }

        public int GameProcessId { get; }

        /// <summary>The window of the game.</summary>
        public IntPtr Window { get; set; } = new IntPtr(0x1234);

        /// <summary>How many looks find no window before it appears; <see cref="int.MaxValue"/> for a window that never appears.</summary>
        public int LooksWithoutWindow { get; set; }

        /// <summary>The process that owns the foreground; 0 for none.</summary>
        public int Foreground { get; set; }

        /// <summary>The answer of <see cref="SetForegroundWindow"/>; with false the foreground stays where it is.</summary>
        public bool SetForegroundResult { get; set; } = true;

        /// <summary>Thrown by every method instead of answering, if set.</summary>
        public Exception Failure { get; set; }

        /// <summary>Called first by every method with its name and arguments (order tests).</summary>
        public Action<string> OnCall { get; set; }

        /// <summary>The managed thread id of the last <see cref="FindVisibleTopLevelWindow"/>.</summary>
        public int LastFindThreadId { get; private set; }

        /// <summary>How often <see cref="FindVisibleTopLevelWindow"/> was called.</summary>
        public int FindCalls { get; private set; }

        public IReadOnlyList<IntPtr> SetForegroundCalls
        {
            get { return setForegroundCalls.ToList(); }
        }

        public IReadOnlyList<int> AllowCalls
        {
            get { return allowCalls.ToList(); }
        }

        public int GetForegroundProcessId()
        {
            OnCall?.Invoke("foreground");
            if (Failure != null)
                throw Failure;
            return Foreground;
        }

        public IntPtr FindVisibleTopLevelWindow(int processId)
        {
            OnCall?.Invoke("find " + processId);
            if (Failure != null)
                throw Failure;
            LastFindThreadId = Environment.CurrentManagedThreadId;
            FindCalls++;
            return processId == GameProcessId && FindCalls > LooksWithoutWindow ? Window : IntPtr.Zero;
        }

        public bool SetForegroundWindow(IntPtr window)
        {
            OnCall?.Invoke("set foreground " + window);
            if (Failure != null)
                throw Failure;
            setForegroundCalls.Add(window);
            if (SetForegroundResult)
                Foreground = GameProcessId;
            return SetForegroundResult;
        }

        public bool AllowSetForegroundWindow(int processId)
        {
            OnCall?.Invoke("allow " + processId);
            if (Failure != null)
                throw Failure;
            allowCalls.Add(processId);
            return true;
        }
    }
}

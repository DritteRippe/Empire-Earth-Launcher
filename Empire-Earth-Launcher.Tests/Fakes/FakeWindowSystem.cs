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
    /// player switched, the launcher took it back) from the delay of its activator. The window of the game has a class, a
    /// rectangle and styles that a test changes to see them in the log of the watch; every other foreground process owns a window
    /// of its own (<see cref="WindowOf"/>). With <see cref="HasSplash"/> the game also owns the splash 'Loading Game Window', a
    /// visible tool window that comes first in the order of the windows: <see cref="FindVisibleTopLevelWindow"/> picks the main
    /// window by the rule of the real adapter (<see cref="WindowRules.IsMainWindowCandidate"/>).
    /// </summary>
    internal sealed class FakeWindowSystem : IWindowSystem
    {
        private readonly List<IntPtr> setForegroundCalls = new List<IntPtr>();
        private readonly List<int> allowCalls = new List<int>();
        private readonly List<IntPtr> postActivateCalls = new List<IntPtr>();

        /// <param name="gameProcessId">The process that owns the window.</param>
        public FakeWindowSystem(int gameProcessId)
        {
            GameProcessId = gameProcessId;
        }

        public int GameProcessId { get; }

        /// <summary>The window of the game.</summary>
        public IntPtr Window { get; set; } = new IntPtr(0x1234);

        /// <summary>The splash of the game: a visible tool window of its process without an owner (only while <see cref="HasSplash"/>).</summary>
        public static readonly IntPtr SplashWindow = new IntPtr(0x5555);

        /// <summary>True if the game has the splash from the first look on, in front of its main window in the order of the windows.</summary>
        public bool HasSplash { get; set; }

        /// <summary>How many looks find no window before it appears; <see cref="int.MaxValue"/> for a window that never appears.</summary>
        public int LooksWithoutWindow { get; set; }

        /// <summary>The process that owns the foreground; 0 for none.</summary>
        public int Foreground { get; set; }

        /// <summary>The class of the window of the game.</summary>
        public string GameClass { get; set; } = "SSSI Empire Earth";

        public int GameLeft { get; set; }

        public int GameTop { get; set; }

        public int GameRight { get; set; } = 1920;

        public int GameBottom { get; set; } = 1080;

        public long GameStyle { get; set; } = 0x16CF0000;

        public long GameExStyle { get; set; } = 0x00040008;

        /// <summary>The lobby popup of the game: an owned window of its process (class 'WONLobbyPopup'), no main window.</summary>
        public static readonly IntPtr LobbyWindow = new IntPtr(0x7777);

        /// <summary>
        /// The window of the game that is in front while the game owns the foreground; null for the main window
        /// (<see cref="Window"/>). A test sets <see cref="LobbyWindow"/> to see the lobby popup in front.
        /// </summary>
        public IntPtr? GameForegroundWindow { get; set; }

        /// <summary>True if the main window is minimized (<c>WS_MINIMIZE</c> is added to its style).</summary>
        public bool GameMinimized { get; set; }

        /// <summary>True if the main window does not respond (<see cref="IsWindowResponding"/> says false for it).</summary>
        public bool GameHung { get; set; }

        /// <summary>The error <see cref="PostActivateMessage"/> reports; 0 = the message is posted.</summary>
        public int PostActivateError { get; set; }

        /// <summary>The window that a process other than the game owns when it owns the foreground.</summary>
        public static IntPtr WindowOf(int processId)
        {
            return new IntPtr(0x9000 + processId);
        }

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

        /// <summary>The windows <see cref="PostActivateMessage"/> was called for, also when it was refused.</summary>
        public IReadOnlyList<IntPtr> PostActivateCalls
        {
            get { return postActivateCalls.ToList(); }
        }

        public int GetForegroundProcessId()
        {
            OnCall?.Invoke("foreground");
            if (Failure != null)
                throw Failure;
            return Foreground;
        }

        public IntPtr GetForegroundWindow()
        {
            OnCall?.Invoke("foreground window");
            if (Failure != null)
                throw Failure;
            return Foreground == 0 ? IntPtr.Zero : Foreground == GameProcessId ? GameForegroundWindow ?? Window : WindowOf(Foreground);
        }

        public WindowState ReadWindow(IntPtr window)
        {
            OnCall?.Invoke("read " + window);
            if (Failure != null)
                throw Failure;
            if (window == IntPtr.Zero)
                return null;
            if (window == Window)
                return new WindowState(window, GameProcessId, GameClass, GameLeft, GameTop, GameRight, GameBottom,
                    GameMinimized ? GameStyle | 0x20000000 : GameStyle, GameExStyle);
            if (window == LobbyWindow)
                return new WindowState(window, GameProcessId, "WONLobbyPopup", 200, 100, 1000, 700, 0x94CA0000, 0x00000100);
            if (window == SplashWindow)
                return new WindowState(window, GameProcessId, "Loading Game Window", 660, 440, 1260, 640, 0x90000000, 0x00000088);
            return new WindowState(window, window.ToInt32() - 0x9000, "OtherClass", 100, 100, 900, 700, 0x14CF0000, 0x100);
        }

        public IntPtr FindVisibleTopLevelWindow(int processId)
        {
            OnCall?.Invoke("find " + processId);
            if (Failure != null)
                throw Failure;
            LastFindThreadId = Environment.CurrentManagedThreadId;
            FindCalls++;
            if (processId != GameProcessId)
                return IntPtr.Zero;
            // The windows of the game in the order of EnumWindows: the splash first, then the main window once it exists.
            if (HasSplash && WindowRules.IsMainWindowCandidate(true, false, 0x00000088))
                return SplashWindow;
            return FindCalls > LooksWithoutWindow && WindowRules.IsMainWindowCandidate(Window != IntPtr.Zero, false, GameExStyle)
                ? Window
                : IntPtr.Zero;
        }

        public bool IsWindowResponding(IntPtr window)
        {
            OnCall?.Invoke("responding " + window);
            if (Failure != null)
                throw Failure;
            return !(GameHung && window == Window);
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

        public bool PostActivateMessage(IntPtr window, out int error)
        {
            OnCall?.Invoke("post activate " + window);
            if (Failure != null)
                throw Failure;
            postActivateCalls.Add(window);
            error = PostActivateError;
            return PostActivateError == 0;
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

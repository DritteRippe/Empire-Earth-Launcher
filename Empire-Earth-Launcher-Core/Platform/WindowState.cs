using System;
using System.Globalization;

namespace Empire_Earth_Launcher.Core.Platform
{
    /// <summary>
    /// What <see cref="IWindowSystem.ReadWindow"/> reads of a window (A1 of 1.1.0, the read-only watch after the hand-over of the
    /// foreground): the process, the window class, the rectangle on the screen and the window styles. An immutable snapshot.
    /// </summary>
    public sealed class WindowState
    {
        /// <param name="handle">The window (<c>HWND</c>).</param>
        /// <param name="processId">The process that owns the window.</param>
        /// <param name="className">The window class, for example <c>SSSI Empire Earth</c>; empty if it cannot be read.</param>
        /// <param name="left">Left edge on the screen, in pixels.</param>
        /// <param name="top">Top edge on the screen, in pixels.</param>
        /// <param name="right">Right edge on the screen (exclusive), in pixels.</param>
        /// <param name="bottom">Bottom edge on the screen (exclusive), in pixels.</param>
        /// <param name="style"><c>GWL_STYLE</c> of the window (<c>WS_*</c>).</param>
        /// <param name="exStyle"><c>GWL_EXSTYLE</c> of the window (<c>WS_EX_*</c>).</param>
        public WindowState(IntPtr handle, int processId, string className, int left, int top, int right, int bottom, long style,
            long exStyle)
        {
            Handle = handle;
            ProcessId = processId;
            ClassName = className ?? string.Empty;
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
            Style = style;
            ExStyle = exStyle;
        }

        public IntPtr Handle { get; }

        public int ProcessId { get; }

        public string ClassName { get; }

        public int Left { get; }

        public int Top { get; }

        public int Right { get; }

        public int Bottom { get; }

        public long Style { get; }

        public long ExStyle { get; }

        /// <summary>The window as one phrase for the log: "window 0x1234 (pid 4242, class 'SSSI Empire Earth')".</summary>
        public string Describe()
        {
            return "window " + FormatHandle(Handle) + " (pid " + ProcessId.ToString(CultureInfo.InvariantCulture) + ", class '" +
                   ClassName + "')";
        }

        /// <summary>The rectangle for the log: "0,0,1920,1080 (1920x1080)" (left, top, right, bottom, then the size).</summary>
        public string FormatRectangle()
        {
            return string.Format(CultureInfo.InvariantCulture, "{0},{1},{2},{3} ({4}x{5})", Left, Top, Right, Bottom,
                Right - Left, Bottom - Top);
        }

        /// <summary>The styles for the log: "style 0x16CF0000, exstyle 0x00040008".</summary>
        public string FormatStyles()
        {
            return "style " + FormatHex(Style) + ", exstyle " + FormatHex(ExStyle);
        }

        /// <summary>True if the rectangle is the same as that of <paramref name="other"/>.</summary>
        public bool HasSameRectangle(WindowState other)
        {
            return other != null && Left == other.Left && Top == other.Top && Right == other.Right && Bottom == other.Bottom;
        }

        /// <summary>True if both window styles are the same as those of <paramref name="other"/>.</summary>
        public bool HasSameStyles(WindowState other)
        {
            return other != null && Style == other.Style && ExStyle == other.ExStyle;
        }

        /// <summary>A window handle as the log shows it: "0x1234".</summary>
        public static string FormatHandle(IntPtr handle)
        {
            return "0x" + handle.ToInt64().ToString("X", CultureInfo.InvariantCulture);
        }

        private static string FormatHex(long value)
        {
            return "0x" + value.ToString("X8", CultureInfo.InvariantCulture);
        }
    }
}

using System;
using System.Globalization;

namespace Empire_Earth_Launcher.Core.Platform
{
    /// <summary>Width and height of a screen in pixels; <see cref="Empty"/> if it could not be measured.</summary>
    public readonly struct ScreenSize : IEquatable<ScreenSize>
    {
        public ScreenSize(int width, int height)
        {
            if (width < 0)
                throw new ArgumentOutOfRangeException(nameof(width), width, "A screen width is not negative.");
            if (height < 0)
                throw new ArgumentOutOfRangeException(nameof(height), height, "A screen height is not negative.");
            Width = width;
            Height = height;
        }

        /// <summary>A size that could not be measured.</summary>
        public static ScreenSize Empty
        {
            get { return default; }
        }

        public int Width { get; }

        public int Height { get; }

        /// <summary>True if the size is unknown (a width or height of 0).</summary>
        public bool IsEmpty
        {
            get { return Width == 0 || Height == 0; }
        }

        public bool Equals(ScreenSize other)
        {
            return Width == other.Width && Height == other.Height;
        }

        public override bool Equals(object obj)
        {
            return obj is ScreenSize other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (Width * 397) ^ Height;
        }

        public static bool operator ==(ScreenSize left, ScreenSize right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ScreenSize left, ScreenSize right)
        {
            return !left.Equals(right);
        }

        /// <summary><c>1920x1080</c>, or <c>unknown</c>.</summary>
        public override string ToString()
        {
            return IsEmpty ? "unknown" : string.Format(CultureInfo.InvariantCulture, "{0}x{1}", Width, Height);
        }
    }

    /// <summary>
    /// What the launcher needs to know about the Windows it runs on (ADR 0006, ADR 0011): the Windows version, Wine, the
    /// size of the primary screen in physical pixels and as a DPI-unaware program such as the game sees it, and which
    /// characters an ANSI program such as the game can use in a path (ADR 0015).
    /// </summary>
    /// <remarks>
    /// Implemented by <see cref="WindowsSystemInfo"/> and checked on real Windows by the test plan; the tests use a fake.
    /// The screen properties are measured on every call (the player may change the resolution while the launcher runs).
    /// </remarks>
    public interface ISystemInfo
    {
        /// <summary>
        /// The NT version of Windows: 6.1 Windows 7, 6.2 Windows 8, 6.3 Windows 8.1, 10.0 Windows 10 and 11 (the build
        /// tells them apart). Under Wine the version Wine reports.
        /// </summary>
        Version WindowsVersion { get; }

        /// <summary>True under Wine (<c>ntdll.dll</c> exports <c>wine_get_version</c>, contract 3.3).</summary>
        bool IsWine { get; }

        /// <summary>
        /// The primary screen in physical pixels: the current display mode of the primary display device (contract 3.3,
        /// O4, ADR 0011); <see cref="ScreenSize.Empty"/> if it cannot be measured.
        /// </summary>
        ScreenSize PrimaryScreen { get; }

        /// <summary>
        /// The primary screen as a DPI-unaware program sees it (<c>GetSystemMetrics</c> in the DPI-unaware launcher): on a
        /// scaled screen the logical size, which is what the game sees without the compatibility layer
        /// <c>HIGHDPIAWARE</c> (contract O4, ADR 0011 plan review); <see cref="ScreenSize.Empty"/> if unknown.
        /// </summary>
        ScreenSize PrimaryScreenUnaware { get; }

        /// <summary>
        /// The display adapter of the primary screen as Windows names it (its driver name, e.g. "NVIDIA GeForce GTX 1060");
        /// null if unknown. Only for the diagnostics report, which names the display adapter instead of a GPU driver
        /// version (ADR 0014, design review).
        /// </summary>
        string PrimaryDisplayAdapter { get; }

        /// <summary>
        /// True if every character of <paramref name="text"/> exists in the ANSI code page of Windows (the code page for
        /// non-Unicode programs). The game is such a program: it cannot open a path with other characters (forum report
        /// section 8, test case 20; ADR 0015). True if the code page cannot be determined.
        /// </summary>
        bool IsInAnsiCodePage(string text);
    }

    /// <summary>Questions about <see cref="ISystemInfo"/> that follow from its values.</summary>
    public static class SystemInfoExtensions
    {
        private static readonly Version Windows8 = new Version(6, 2);
        private static readonly Version Windows10 = new Version(10, 0);

        /// <summary>Windows 8 (NT 6.2) or later, the Windows versions of the compatibility values (contract 3.7).</summary>
        public static bool IsWindows8OrLater(this ISystemInfo systemInfo)
        {
            if (systemInfo == null)
                throw new ArgumentNullException(nameof(systemInfo));
            return systemInfo.WindowsVersion >= Windows8;
        }

        /// <summary>Windows 10 (NT 10.0) or later, the Windows versions of the GPU preference (contract 3.4).</summary>
        public static bool IsWindows10OrLater(this ISystemInfo systemInfo)
        {
            if (systemInfo == null)
                throw new ArgumentNullException(nameof(systemInfo));
            return systemInfo.WindowsVersion >= Windows10;
        }

        /// <summary>
        /// The scaling of the primary screen in percent, derived from the physical and the DPI-unaware size (ADR 0011
        /// plan review): 100 without scaling, 150 at 150 %; 100 if one of the sizes is unknown.
        /// </summary>
        public static int ScalingPercent(this ISystemInfo systemInfo)
        {
            if (systemInfo == null)
                throw new ArgumentNullException(nameof(systemInfo));
            ScreenSize physical = systemInfo.PrimaryScreen;
            ScreenSize unaware = systemInfo.PrimaryScreenUnaware;
            if (physical.IsEmpty || unaware.IsEmpty)
                return 100;
            return (int)Math.Round(physical.Width * 100.0 / unaware.Width, MidpointRounding.AwayFromZero);
        }

        /// <summary>One line for the log: version, Wine, screen sizes and scaling.</summary>
        public static string Describe(this ISystemInfo systemInfo)
        {
            if (systemInfo == null)
                throw new ArgumentNullException(nameof(systemInfo));
            return string.Format(CultureInfo.InvariantCulture,
                "Windows NT {0}{1}, primary screen {2} physical, {3} for DPI-unaware programs ({4} %)",
                systemInfo.WindowsVersion, systemInfo.IsWine ? " (Wine)" : string.Empty, systemInfo.PrimaryScreen,
                systemInfo.PrimaryScreenUnaware, systemInfo.ScalingPercent());
        }
    }
}

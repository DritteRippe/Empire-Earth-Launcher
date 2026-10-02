using System;
using Empire_Earth_Launcher.Core.Platform;

namespace Empire_Earth_Launcher.Tests.Fakes
{
    /// <summary>
    /// <see cref="ISystemInfo"/> with settable values. The default is Windows 10 (NT 10.0) without Wine and a
    /// 1920x1080 screen at 100 %.
    /// </summary>
    internal sealed class FakeSystemInfo : ISystemInfo
    {
        public Version WindowsVersion { get; set; } = new Version(10, 0, 19045);

        public bool IsWine { get; set; }

        public ScreenSize PrimaryScreen { get; set; } = new ScreenSize(1920, 1080);

        public ScreenSize PrimaryScreenUnaware { get; set; } = new ScreenSize(1920, 1080);

        /// <summary>Windows 7 SP1 (NT 6.1).</summary>
        public static FakeSystemInfo Windows7()
        {
            return new FakeSystemInfo { WindowsVersion = new Version(6, 1, 7601) };
        }

        /// <summary>Windows 8.1 (NT 6.3).</summary>
        public static FakeSystemInfo Windows81()
        {
            return new FakeSystemInfo { WindowsVersion = new Version(6, 3, 9600) };
        }

        /// <summary>
        /// A screen of <paramref name="width"/> x <paramref name="height"/> physical pixels at <paramref name="percent"/> %
        /// scaling: a DPI-unaware program sees the size divided by the scaling.
        /// </summary>
        public FakeSystemInfo WithScreen(int width, int height, int percent = 100)
        {
            PrimaryScreen = new ScreenSize(width, height);
            PrimaryScreenUnaware = new ScreenSize(width * 100 / percent, height * 100 / percent);
            return this;
        }
    }
}

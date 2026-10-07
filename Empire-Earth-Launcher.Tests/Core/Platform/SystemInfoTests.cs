using System;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Tests.Fakes;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.Platform
{
    /// <summary>
    /// <see cref="ScreenSize"/> and the questions of <see cref="SystemInfoExtensions"/> (ADR 0011): Windows 8 and 10 for
    /// the compatibility values and the GPU preference (contract 3.4, 3.7), and the scaling derived from the physical
    /// and the DPI-unaware screen size. <see cref="WindowsSystemInfo"/> itself is checked by the test plan.
    /// </summary>
    [TestFixture]
    public class SystemInfoTests
    {
        [TestCase(6, 1, false, false)]
        [TestCase(6, 2, true, false)]
        [TestCase(6, 3, true, false)]
        [TestCase(10, 0, true, true)]
        public void WindowsVersions_OfTheContractTables(int major, int minor, bool windows8, bool windows10)
        {
            var systemInfo = new FakeSystemInfo { WindowsVersion = new Version(major, minor, 1) };

            Assert.That(systemInfo.IsWindows8OrLater(), Is.EqualTo(windows8));
            Assert.That(systemInfo.IsWindows10OrLater(), Is.EqualTo(windows10));
        }

        [TestCase(100, 100)]
        [TestCase(125, 125)]
        [TestCase(150, 150)]
        [TestCase(175, 175)]
        public void ScalingPercent_IsThePhysicalSizeDividedByTheUnawareSize(int percent, int expected)
        {
            var systemInfo = new FakeSystemInfo().WithScreen(1920, 1080, percent);

            Assert.That(systemInfo.ScalingPercent(), Is.EqualTo(expected));
        }

        [Test]
        public void ScalingPercent_Is100WhenASizeIsUnknown()
        {
            var systemInfo = new FakeSystemInfo { PrimaryScreenUnaware = ScreenSize.Empty };

            Assert.That(systemInfo.ScalingPercent(), Is.EqualTo(100));
        }

        [Test]
        public void ScreenSize_EmptyAndText()
        {
            Assert.That(ScreenSize.Empty.IsEmpty, Is.True);
            Assert.That(new ScreenSize(1920, 0).IsEmpty, Is.True);
            Assert.That(ScreenSize.Empty.ToString(), Is.EqualTo("unknown"));
            Assert.That(new ScreenSize(1366, 768).ToString(), Is.EqualTo("1366x768"));
            Assert.That(new ScreenSize(1366, 768), Is.EqualTo(new ScreenSize(1366, 768)));
            Assert.That(new ScreenSize(1366, 768) != new ScreenSize(768, 1366), Is.True);
            Assert.That(() => new ScreenSize(-1, 768), Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void Describe_NamesVersionWineAndScreen()
        {
            var systemInfo = new FakeSystemInfo { IsWine = true }.WithScreen(2560, 1440, 150);

            Assert.That(systemInfo.Describe(), Is.EqualTo(
                "Windows NT 10.0.19045 (Wine), primary screen 2560x1440 physical, 1706x960 for DPI-unaware programs (150 %)"));
        }
    }
}

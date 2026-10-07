using System;
using Empire_Earth_Launcher.Core.Logging;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Tests.Fakes;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.Platform
{
    /// <summary>
    /// <see cref="WindowsWindowSystem"/> as far as it can be checked without touching a window of the computer (the tests also
    /// run on real Windows): the reads work, a process nobody owns has no window, and a missing <c>user32.dll</c> (Mono on Linux)
    /// is a neutral answer and one warning, never an exception. <c>SetForegroundWindow</c> and <c>AllowSetForegroundWindow</c> on
    /// real windows are a case of the test plan (WP6-18).
    /// </summary>
    [TestFixture]
    public class WindowsWindowSystemTests
    {
        private RecordingLogger logger;
        private WindowsWindowSystem windows;

        [SetUp]
        public void SetUp()
        {
            logger = new RecordingLogger();
            windows = new WindowsWindowSystem(logger);
        }

        [Test]
        public void TheForegroundProcess_IsAnIdOrNone()
        {
            Assert.That(windows.GetForegroundProcessId(), Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void TheForegroundWindow_IsAHandleOrNone()
        {
            Assert.That(windows.GetForegroundWindow(), Is.Not.Null);
            Assert.That(windows.ReadWindow(IntPtr.Zero), Is.Null, "no window, nothing to read");
        }

        [Test]
        public void AWindowThatDoesNotExist_CannotBeRead()
        {
            Assert.That(windows.ReadWindow(new IntPtr(0x7FFFFFF0)), Is.Null);
        }

        [Test]
        public void AProcessNobodyOwns_HasNoWindow()
        {
            Assert.That(windows.FindVisibleTopLevelWindow(int.MaxValue), Is.EqualTo(IntPtr.Zero));
        }

        [Test]
        public void NoWindow_CannotBeBroughtToTheForeground()
        {
            Assert.That(windows.SetForegroundWindow(IntPtr.Zero), Is.False);
        }

        [Test]
        public void WithoutUser32_TheWarningIsLoggedOnce()
        {
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                Assert.Ignore("user32.dll exists on Windows.");

            windows.GetForegroundProcessId();
            windows.GetForegroundWindow();
            windows.ReadWindow(new IntPtr(0x10));
            windows.FindVisibleTopLevelWindow(1);
            windows.SetForegroundWindow(IntPtr.Zero);

            Assert.That(logger.Entries, Has.Count.EqualTo(1));
            Assert.That(logger.Entries[0].Level, Is.EqualTo(LogLevel.Warning));
            Assert.That(logger.Entries[0].Message, Does.Contain("not available"));
        }

        [Test]
        public void ALoggerIsRequired()
        {
            Assert.That(() => new WindowsWindowSystem(null), Throws.ArgumentNullException);
        }
    }
}

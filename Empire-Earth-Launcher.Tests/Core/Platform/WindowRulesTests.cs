using Empire_Earth_Launcher.Core.Platform;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.Platform
{
    /// <summary>
    /// <see cref="WindowRules.IsMainWindowCandidate"/>, the rule that picks the main window of a game among the windows of its
    /// process (A1 of 1.1.0): Empire Earth shows the splash 'Loading Game Window', a tool window, before the real window of the
    /// class 'SSSI Empire Earth', and the hand-over and the log must refer to the real one.
    /// </summary>
    [TestFixture]
    public class WindowRulesTests
    {
        private const long ExStyleOfTheMainWindow = 0x00040008;  // WS_EX_APPWINDOW | WS_EX_TOPMOST
        private const long ExStyleOfTheSplash = 0x00000088;      // WS_EX_TOOLWINDOW | WS_EX_TOPMOST

        [Test]
        public void AVisibleWindowWithoutOwner_IsTheMainWindow()
        {
            Assert.That(WindowRules.IsMainWindowCandidate(true, false, ExStyleOfTheMainWindow), Is.True);
            Assert.That(WindowRules.IsMainWindowCandidate(true, false, 0), Is.True, "no extended style at all");
        }

        [Test]
        public void TheSplashOfEmpireEarth_ATopmostToolWindow_IsNot()
        {
            Assert.That(WindowRules.IsMainWindowCandidate(true, false, ExStyleOfTheSplash), Is.False);
        }

        [TestCase(0x80L)]
        [TestCase(0x80L | 0x40000L)]
        [TestCase(0xFFFFFFFFL)]
        public void EveryExtendedStyleWithTheToolWindowBit_IsNot(long exStyle)
        {
            Assert.That(WindowRules.IsMainWindowCandidate(true, false, exStyle), Is.False);
        }

        [Test]
        public void AHiddenWindow_AndAnOwnedWindow_AreNot()
        {
            Assert.That(WindowRules.IsMainWindowCandidate(false, false, ExStyleOfTheMainWindow), Is.False, "hidden");
            Assert.That(WindowRules.IsMainWindowCandidate(true, true, ExStyleOfTheMainWindow), Is.False, "owned (the lobby popup)");
        }

        [Test]
        public void TheToolWindowStyle_IsWsExToolWindow()
        {
            Assert.That(WindowRules.ToolWindowExStyle, Is.EqualTo(0x80L));
        }
    }
}

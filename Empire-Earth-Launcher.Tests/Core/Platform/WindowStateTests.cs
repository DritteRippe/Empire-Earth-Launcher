using System;
using Empire_Earth_Launcher.Core.Platform;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.Platform
{
    /// <summary>
    /// <see cref="WindowState"/>, what the read-only watch after the hand-over of the foreground reads of a window (A1 of 1.1.0)
    /// and how its parts appear in <c>log.txt</c>: the same forms in every language, the handle and the styles in hexadecimal.
    /// </summary>
    [TestFixture]
    public class WindowStateTests
    {
        private static WindowState Game(int left = 0, int top = 0, int right = 1920, int bottom = 1080, long style = 0x16CF0000,
            long exStyle = 0x00040008)
        {
            return new WindowState(new IntPtr(0x1234), 4242, "SSSI Empire Earth", left, top, right, bottom, style, exStyle);
        }

        [Test]
        public void TheWindow_IsDescribedWithItsHandleProcessAndClass()
        {
            Assert.That(Game().Describe(), Is.EqualTo("window 0x1234 (pid 4242, class 'SSSI Empire Earth')"));
        }

        [Test]
        public void TheRectangle_IsShownWithItsEdgesAndItsSize()
        {
            Assert.That(Game().FormatRectangle(), Is.EqualTo("0,0,1920,1080 (1920x1080)"));
            Assert.That(Game(-8, -8, 1928, 1088).FormatRectangle(), Is.EqualTo("-8,-8,1928,1088 (1936x1096)"), "a window moved beyond the screen");
        }

        [Test]
        public void TheStyles_AreShownAsEightHexDigits()
        {
            Assert.That(Game().FormatStyles(), Is.EqualTo("style 0x16CF0000, exstyle 0x00040008"));
            Assert.That(Game(style: 0x94000000, exStyle: 0).FormatStyles(), Is.EqualTo("style 0x94000000, exstyle 0x00000000"));
        }

        [Test]
        public void TheHandle_IsShownInHexadecimalWithoutLeadingZeros()
        {
            Assert.That(WindowState.FormatHandle(new IntPtr(0xABC)), Is.EqualTo("0xABC"));
            Assert.That(WindowState.FormatHandle(IntPtr.Zero), Is.EqualTo("0x0"));
        }

        [Test]
        public void TheRectangleAndTheStyles_AreComparedSeparately()
        {
            Assert.That(Game().HasSameRectangle(Game()), Is.True);
            Assert.That(Game().HasSameRectangle(Game(right: 1280)), Is.False);
            Assert.That(Game().HasSameRectangle(Game(top: 1)), Is.False);
            Assert.That(Game().HasSameRectangle(null), Is.False);
            Assert.That(Game().HasSameStyles(Game(right: 1280)), Is.True, "the rectangle is not a style");
            Assert.That(Game().HasSameStyles(Game(style: 0x14CF0000)), Is.False);
            Assert.That(Game().HasSameStyles(Game(exStyle: 0x00040000)), Is.False);
            Assert.That(Game().HasSameStyles(null), Is.False);
        }

        [Test]
        public void ANameThatCannotBeRead_IsEmpty()
        {
            Assert.That(new WindowState(new IntPtr(1), 1, null, 0, 0, 1, 1, 0, 0).ClassName, Is.Empty);
        }
    }
}

using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Launcher
{
    /// <summary>
    /// <see cref="NavigationStack"/>: the buttons of the navigation bar sit one below the other; the button of the Mods page
    /// comes and goes and takes no room while it is hidden.
    /// </summary>
    [TestFixture]
    public class NavigationStackTests
    {
        [Test]
        public void EveryButtonThatIsShown_SitsOneStepBelowTheOneBefore()
        {
            Assert.That(NavigationStack.Tops(66, 52, new[] { true, true, true, true, true, true }),
                Is.EqualTo(new[] { 66, 118, 170, 222, 274, 326 }));
        }

        [Test]
        public void AHiddenButton_TakesNoRoom_TheOnesBelowMoveUp()
        {
            Assert.That(NavigationStack.Tops(66, 52, new[] { true, true, true, false, true, true }),
                Is.EqualTo(new[] { 66, 118, 170, 222, 222, 274 }));
        }

        [Test]
        public void TheStepOfTheDesigner_IsKeptWhateverTheFontScale()
        {
            Assert.That(NavigationStack.Tops(80, 65, new[] { true, false, true }), Is.EqualTo(new[] { 80, 145, 145 }));
        }

        [Test]
        public void WithoutButtons_ThereAreNoTops()
        {
            Assert.That(NavigationStack.Tops(66, 52, new bool[0]), Is.Empty);
        }

        [Test]
        public void TheLastButton_OfTheFullBar_StaysInsideTheSmallestWindow()
        {
            // The window opens 381 pixels high (ADR 0017: never smaller); six buttons of 52 from the first top must fit.
            int last = NavigationStack.Tops(66, 52, new[] { true, true, true, true, true, true })[5];

            Assert.That(last + 52, Is.LessThanOrEqualTo(381));
        }
    }
}

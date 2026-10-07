using System.Linq;
using Empire_Earth_Launcher.Core.GameSettings;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Tests.Fakes;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.GameSettings
{
    /// <summary>
    /// <see cref="ResolutionOptions"/>: the game window sizes of the graphics page (launcher 1.1.0): what fits the primary
    /// screen, never above 1920x1080 and never below 1024x768 (contract 3.3), always the recommended size.
    /// </summary>
    [TestFixture]
    public class ResolutionOptionsTests
    {
        private static string[] Sizes(FakeSystemInfo systemInfo)
        {
            return ResolutionOptions.For(systemInfo).Select(option => option.Size.ToString()).ToArray();
        }

        [Test]
        public void For_AScreenOf1920x1200_StopsAtTheLimitsOfContract33()
        {
            var systemInfo = new FakeSystemInfo().WithScreen(1920, 1200);

            string[] sizes = Sizes(systemInfo);

            Assert.That(sizes, Does.Contain("1920x1080").And.Contain("1680x1050").And.Contain("1600x900").And.Contain("1024x768"));
            Assert.That(ResolutionOptions.For(systemInfo).Select(option => option.Size), Is.All.Matches<ScreenSize>(ResolutionOptions.IsWithinLimits));
            Assert.That(sizes, Does.Not.Contain("1920x1200"), "above the limit of the height");
        }

        [Test]
        public void For_AScreenOf1920x1200_TheRecommendedSizeIsTheCap()
        {
            var systemInfo = new FakeSystemInfo().WithScreen(1920, 1200);

            ResolutionOption[] recommended = ResolutionOptions.For(systemInfo).Where(option => option.IsRecommended).ToArray();

            Assert.That(recommended.Select(option => option.Size), Is.EqualTo(new[] { new ScreenSize(1920, 1080) }));
            Assert.That(ComputedValues.GameWindow(systemInfo), Is.EqualTo(new ScreenSize(1920, 1080)));
        }

        [Test]
        public void For_AScreenOf1366x768_OffersTheMinimumAndTheScreen()
        {
            var systemInfo = new FakeSystemInfo().WithScreen(1366, 768);

            Assert.That(Sizes(systemInfo), Is.EqualTo(new[] { "1024x768", "1366x768" }));
            Assert.That(ResolutionOptions.For(systemInfo).Single(option => option.IsRecommended).Size, Is.EqualTo(new ScreenSize(1366, 768)));
        }

        [Test]
        public void For_AScreenOf1024x768_OffersOnlyTheMinimum()
        {
            Assert.That(Sizes(new FakeSystemInfo().WithScreen(1024, 768)), Is.EqualTo(new[] { "1024x768" }));
        }

        [Test]
        public void For_AScreenBelowTheMinimum_StillOffersTheMinimum()
        {
            // The setup and the launcher write 1024x768 there (contract 3.3); the page warns about the screen elsewhere.
            Assert.That(Sizes(new FakeSystemInfo().WithScreen(1024, 600)), Is.EqualTo(new[] { "1024x768" }));
        }

        [Test]
        public void For_AnUnknownScreen_OffersOnlyTheMinimum()
        {
            var systemInfo = new FakeSystemInfo
            {
                PrimaryScreen = ScreenSize.Empty,
                PrimaryScreenUnaware = ScreenSize.Empty,
            };

            Assert.That(Sizes(systemInfo), Is.EqualTo(new[] { "1024x768" }));
        }

        [Test]
        public void For_AScreenOf1280x1024_OffersItsOwnSize()
        {
            var systemInfo = new FakeSystemInfo().WithScreen(1280, 1024);

            Assert.That(Sizes(systemInfo), Is.EqualTo(new[] { "1024x768", "1152x864", "1280x800", "1280x960", "1280x1024" }));
            Assert.That(ResolutionOptions.For(systemInfo).Single(option => option.IsRecommended).Size, Is.EqualTo(new ScreenSize(1280, 1024)));
        }

        [Test]
        public void For_A4KScreen_StopsAt1920x1080()
        {
            var systemInfo = new FakeSystemInfo().WithScreen(3840, 2160);

            string[] sizes = Sizes(systemInfo);

            Assert.That(sizes.Last(), Is.EqualTo("1920x1080"));
            Assert.That(ResolutionOptions.For(systemInfo).Single(option => option.IsRecommended).Size, Is.EqualTo(new ScreenSize(1920, 1080)));
        }

        [Test]
        public void For_AScaledScreen_UsesThePhysicalPixelsLikeTheRecommendedSize()
        {
            // 2560x1440 at 150 %: the game window defaults come from the physical size (contract 3.3, O4).
            var systemInfo = new FakeSystemInfo().WithScreen(2560, 1440, 150);

            Assert.That(Sizes(systemInfo).Last(), Is.EqualTo("1920x1080"));
        }

        [TestCase(1920, 1200)]
        [TestCase(1366, 768)]
        [TestCase(1280, 1024)]
        [TestCase(3840, 2160)]
        [TestCase(1024, 768)]
        [TestCase(1024, 600)]
        public void For_IsSortedWithoutDuplicates(int width, int height)
        {
            ScreenSize[] sizes = ResolutionOptions.For(new FakeSystemInfo().WithScreen(width, height)).Select(option => option.Size).ToArray();

            Assert.That(sizes, Is.Unique);
            Assert.That(sizes, Is.EqualTo(sizes.OrderBy(size => size.Width).ThenBy(size => size.Height).ToArray()));
        }

        [TestCase(1024, 768, true)]
        [TestCase(1920, 1080, true)]
        [TestCase(1023, 768, false)]
        [TestCase(1024, 767, false)]
        [TestCase(1921, 1080, false)]
        [TestCase(1920, 1081, false)]
        [TestCase(1920, 1200, false)]
        [TestCase(0, 0, false)]
        public void IsWithinLimits_IsTheRangeOfContract33(int width, int height, bool expected)
        {
            Assert.That(ResolutionOptions.IsWithinLimits(new ScreenSize(width, height)), Is.EqualTo(expected));
        }

        [TestCase(1024, 768, AspectKind.FourByThree)]
        [TestCase(1400, 1050, AspectKind.FourByThree)]
        [TestCase(1280, 1024, AspectKind.FiveByFour)]
        [TestCase(1440, 900, AspectKind.SixteenByTen)]
        [TestCase(1680, 1050, AspectKind.SixteenByTen)]
        [TestCase(1600, 900, AspectKind.SixteenByNine)]
        [TestCase(1366, 768, AspectKind.SixteenByNine)]
        [TestCase(1920, 1080, AspectKind.SixteenByNine)]
        [TestCase(1280, 768, AspectKind.Other)]
        [TestCase(0, 0, AspectKind.Other)]
        public void AspectOf_NamesTheUsualShapes(int width, int height, AspectKind expected)
        {
            Assert.That(ResolutionOptions.AspectOf(new ScreenSize(width, height)), Is.EqualTo(expected));
        }

        [Test]
        public void EverySizeOfTheList_HasItsAspect()
        {
            foreach (ResolutionOption option in ResolutionOptions.For(new FakeSystemInfo().WithScreen(1920, 1080)))
                Assert.That(option.Aspect, Is.EqualTo(ResolutionOptions.AspectOf(option.Size)), option.ToString());
        }
    }
}

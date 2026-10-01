using System;
using System.Drawing;
using Empire_Earth_Mod_Lib;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Mod
{
    /// <summary>
    /// Size rules for icon and banners (<see cref="ModImageRules"/>) and the banner list of
    /// <see cref="ModAssets"/>.
    /// </summary>
    [TestFixture]
    public class ModAssetsTests
    {
        [TestCase(128, 128, true)]
        [TestCase(64, 64, false)]
        [TestCase(128, 127, false)]
        [TestCase(256, 256, false)]
        public void ValidateIcon_AcceptsOnly128x128(int width, int height, bool valid)
        {
            using (var icon = new Bitmap(width, height))
            {
                if (valid)
                    Assert.That(() => ModImageRules.ValidateIcon(icon), Throws.Nothing);
                else
                    Assert.That(() => ModImageRules.ValidateIcon(icon), Throws.TypeOf<FormatException>());
            }
        }

        [TestCase(1280, 720, true)]
        [TestCase(1600, 900, true)]
        [TestCase(1920, 1080, true)]
        [TestCase(1366, 768, true)] // 1.7786 rounds to 1.78
        [TestCase(1280, 800, false)] // 16:10
        [TestCase(1024, 576, false)] // 16:9 but too small
        [TestCase(2560, 1440, false)] // 16:9 but too large
        public void ValidateBanner_Accepts16To9Between720pAnd1080p(int width, int height, bool valid)
        {
            using (var banner = new Bitmap(width, height))
            {
                if (valid)
                    Assert.That(() => ModImageRules.ValidateBanner(banner), Throws.Nothing);
                else
                    Assert.That(() => ModImageRules.ValidateBanner(banner), Throws.TypeOf<FormatException>());
            }
        }

        [Test]
        public void Icon_InvalidSize_IsNotStored()
        {
            var assets = new ModAssets();
            using (var tooSmall = new Bitmap(64, 64))
            {
                Assert.That(() => assets.Icon = tooSmall, Throws.TypeOf<FormatException>());
            }
            Assert.That(assets.Icon, Is.Null);
        }

        [Test]
        public void Banners_AreKeptPerVariantInTheirOrder()
        {
            var assets = new ModAssets();
            Guid variant = Guid.NewGuid();
            var first = new Bitmap(1280, 720);
            var second = new Bitmap(1920, 1080);

            assets.AddBanner(variant, first);
            assets.AddBanner(variant, second);

            Assert.That(assets.HasBanner(variant), Is.True);
            Assert.That(assets.HasBanner(Guid.Empty), Is.False);
            Assert.That(assets.GetBanners(variant), Is.EqualTo(new Image[] { first, second }));
            Assert.That(assets.GetBanners(Guid.Empty), Is.Empty);

            assets.RemoveBanner(variant, 0);
            Assert.That(assets.GetBanners(variant), Is.EqualTo(new Image[] { second }));
            Assert.That(() => assets.RemoveBanner(variant, 1), Throws.TypeOf<ArgumentOutOfRangeException>());

            Assert.That(assets.RemoveVariant(variant), Is.True);
            Assert.That(assets.HasBanner(variant), Is.False);
            Assert.That(assets.RemoveVariant(variant), Is.False);
        }

        [Test]
        public void Banners_CannotBeChangedThroughTheReturnedList()
        {
            var assets = new ModAssets();
            Guid variant = Guid.NewGuid();
            assets.AddBanner(variant, new Bitmap(1280, 720));

            Assert.That(() => assets.GetBanners(variant).Clear(), Throws.TypeOf<NotSupportedException>());
            Assert.That(() => assets.GetBanners(Guid.Empty).Add(new Bitmap(1280, 720)), Throws.TypeOf<NotSupportedException>());
        }
    }
}

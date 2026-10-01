using System;
using System.Drawing;

namespace Empire_Earth_Mod_Lib
{
    /// <summary>
    /// Size rules for the images of a mod (icon and banners).
    /// </summary>
    public static class ModImageRules
    {
        /// <summary>Width and height of the icon in pixels.</summary>
        public const int IconSize = 128;

        /// <summary>Required width/height ratio of a banner: 16:9, rounded to two decimals.</summary>
        public const double BannerAspectRatio = 1.78;

        public const int MinBannerWidth = 1280;
        public const int MinBannerHeight = 720;
        public const int MaxBannerWidth = 1920;
        public const int MaxBannerHeight = 1080;

        /// <exception cref="FormatException">The icon is not <see cref="IconSize"/> x <see cref="IconSize"/>.</exception>
        public static void ValidateIcon(Image icon)
        {
            if (icon == null)
                throw new ArgumentNullException(nameof(icon));
            if (icon.Width != IconSize || icon.Height != IconSize)
                throw new FormatException("Icon must be " + IconSize + "x" + IconSize);
        }

        /// <exception cref="FormatException">The banner is not 16:9 or not between the minimum and maximum size.</exception>
        public static void ValidateBanner(Image banner)
        {
            if (banner == null)
                throw new ArgumentNullException(nameof(banner));
            // ReSharper disable once CompareOfFloatsByEqualityOperator (both values are rounded to two decimals)
            if (Math.Round((double)banner.Width / banner.Height, 2) != BannerAspectRatio)
                throw new FormatException("Banner must be 16:9");
            if (banner.Width < MinBannerWidth || banner.Width > MaxBannerWidth ||
                banner.Height < MinBannerHeight || banner.Height > MaxBannerHeight)
                throw new FormatException("Banner must be >= " + MinBannerWidth + "x" + MinBannerHeight + " and <= " +
                                          MaxBannerWidth + "x" + MaxBannerHeight);
        }
    }
}

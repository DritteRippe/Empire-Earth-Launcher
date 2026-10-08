using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;

namespace Empire_Earth_Mod_Lib
{
    /// <summary>
    /// Icon and banners of a mod while it is edited. They are not part of <see cref="ModData"/>:
    /// <see cref="ModPackageBuilder"/> writes them as image entries of the archive.
    /// </summary>
    /// <remarks>
    /// GDI+ images are not thread-safe: use the images from one thread at a time.
    /// </remarks>
    public class ModAssets
    {
        private static readonly ReadOnlyCollection<Image> NoBanners = new ReadOnlyCollection<Image>(new Image[0]);

        private readonly Dictionary<Guid, List<Image>> banners = new Dictionary<Guid, List<Image>>();
        private Image icon;

        /// <summary>
        /// Loads an image file, e.g. the picture the mod author chose for the icon or a banner, into memory.
        /// </summary>
        /// <remarks>
        /// Image.FromFile keeps the file open and locked for as long as the image lives: the author could not save a
        /// corrected version of the picture while the mod creator was running, also after its window was closed, until the
        /// garbage collector happened to release the image. The copy returned here holds no file.
        /// </remarks>
        /// <exception cref="FormatException">The file is not an image that Windows can read.</exception>
        /// <exception cref="System.IO.FileNotFoundException">The file does not exist.</exception>
        public static Image LoadImageFile(string path)
        {
            Image image;
            try
            {
                image = Image.FromFile(path);
            }
            catch (OutOfMemoryException ex)
            {
                // GDI+ reports a file it cannot decode as "Out of memory.", which is what the mod creator showed.
                throw new FormatException("The file is not an image that Windows can read: " + path, ex);
            }
            using (image)
            {
                return new Bitmap(image);
            }
        }

        /// <summary>Icon of the mod; null until one is set.</summary>
        /// <exception cref="FormatException">The new icon does not follow <see cref="ModImageRules.ValidateIcon"/>.</exception>
        public Image Icon
        {
            get { return icon; }
            set
            {
                if (value != null)
                    ModImageRules.ValidateIcon(value);
                icon = value;
            }
        }

        /// <summary>Adds a banner to a variant.</summary>
        /// <exception cref="FormatException">The banner does not follow <see cref="ModImageRules.ValidateBanner"/>.</exception>
        public void AddBanner(Guid variant, Image banner)
        {
            ModImageRules.ValidateBanner(banner);
            List<Image> variantBanners;
            if (!banners.TryGetValue(variant, out variantBanners))
            {
                variantBanners = new List<Image>();
                banners.Add(variant, variantBanners);
            }
            variantBanners.Add(banner);
        }

        /// <exception cref="ArgumentOutOfRangeException">The variant has no banner at <paramref name="index"/>.</exception>
        public void RemoveBanner(Guid variant, int index)
        {
            List<Image> variantBanners;
            if (!banners.TryGetValue(variant, out variantBanners) || index < 0 || index >= variantBanners.Count)
                throw new ArgumentOutOfRangeException(nameof(index), index, "The variant has no banner at this index.");
            variantBanners.RemoveAt(index);
        }

        public bool HasBanner(Guid variant)
        {
            List<Image> variantBanners;
            return banners.TryGetValue(variant, out variantBanners) && variantBanners.Count > 0;
        }

        /// <summary>The banners of a variant in their order; empty if it has none.</summary>
        public IList<Image> GetBanners(Guid variant)
        {
            List<Image> variantBanners;
            return banners.TryGetValue(variant, out variantBanners)
                ? variantBanners.AsReadOnly()
                : NoBanners;
        }

        /// <summary>Removes all banners of a variant.</summary>
        /// <returns>true if the variant had banners.</returns>
        public bool RemoveVariant(Guid variant)
        {
            bool hadBanners = HasBanner(variant);
            banners.Remove(variant);
            return hadBanners;
        }
    }
}

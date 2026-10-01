using System;
using System.Collections.ObjectModel;
using System.IO;

namespace Empire_Earth_Mod_Lib
{
    /// <summary>
    /// Layout of an Empire Earth Mod archive (.eem). Export (<see cref="ModData.Creator"/>) and import
    /// (<see cref="ModData.LoadFromEEM(Stream)"/>) both use these definitions, so they cannot drift apart.
    /// </summary>
    /// <remarks>
    /// An .eem file is a ZIP archive. Entry names are UTF-8, use '/' as separator and are relative to the
    /// root of the archive (no enclosing folder):
    /// <code>
    /// data                      mod information as JSON (member names: [DataMember] of ModData/ModFile)
    /// Icon.png                  icon of the mod (128x128)
    /// {variant}/Banner{i}.png   banners of a variant; {variant} is the variant GUID, {i} starts at 0
    /// {variant}/{product}/...   game files of a variant; {product} is "all", "EEC" or "AOC"
    /// </code>
    /// The working directory of <see cref="ModData.Creator"/> has the same layout on disk.
    /// </remarks>
    public static class EemFormat
    {
        /// <summary>File extension of mod archives.</summary>
        public const string Extension = ".eem";

        /// <summary>Search pattern for mod archives in a directory.</summary>
        public const string SearchPattern = "*" + Extension;

        /// <summary>Entry with the JSON serialized <see cref="ModData"/>.</summary>
        public const string DataEntryName = "data";

        /// <summary>Entry with the icon of the mod.</summary>
        public const string IconEntryName = "Icon.png";

        /// <summary>File name prefix of the banners inside a variant folder.</summary>
        public const string BannerFilePrefix = "Banner";

        /// <summary>File name extension of the banners.</summary>
        public const string BannerFileExtension = ".png";

        /// <summary>Separator of the folders in entry names.</summary>
        public const char EntryNameSeparator = '/';

        /// <summary>Product folder for files used by Empire Earth and The Art of Conquest.</summary>
        public const string ProductFolderBoth = "all";

        /// <summary>Product folder for files used by Empire Earth only.</summary>
        public const string ProductFolderEec = "EEC";

        /// <summary>Product folder for files used by The Art of Conquest only.</summary>
        public const string ProductFolderAoc = "AOC";

        /// <summary>Largest accepted <see cref="DataEntryName"/> entry, to reject broken archives early.</summary>
        public const long MaxDataEntryBytes = 16 * 1024 * 1024;

        /// <summary>All product folders of a variant.</summary>
        public static readonly ReadOnlyCollection<string> ProductFolders =
            new ReadOnlyCollection<string>(new[] { ProductFolderBoth, ProductFolderEec, ProductFolderAoc });

        /// <summary>File name of the banner with the given index, e.g. "Banner0.png".</summary>
        public static string GetBannerFileName(int index)
        {
            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(index));
            return BannerFilePrefix + index + BannerFileExtension;
        }

        /// <summary>
        /// Converts a path relative to the working directory into the corresponding entry name.
        /// </summary>
        public static string ToEntryName(string relativePath)
        {
            if (relativePath == null)
                throw new ArgumentNullException(nameof(relativePath));
            return relativePath
                .Replace(Path.DirectorySeparatorChar, EntryNameSeparator)
                .Replace(Path.AltDirectorySeparatorChar, EntryNameSeparator)
                .Trim(EntryNameSeparator);
        }
    }
}

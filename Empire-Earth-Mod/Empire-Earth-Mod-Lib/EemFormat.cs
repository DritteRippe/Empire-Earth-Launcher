using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace Empire_Earth_Mod_Lib
{
    /// <summary>
    /// Layout of an Empire Earth Mod archive (.eem). Export (<see cref="ModPackageBuilder"/>) and import
    /// (<see cref="ModArchiveReader"/>) both use these definitions, so they cannot drift apart.
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
    /// The working directory of <see cref="ModPackageBuilder"/> has the same layout on disk. The paths of the game files in
    /// the data (<see cref="ModFile.RelativeFilePath"/>) follow <see cref="IsValidFilePath"/>.
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

        /// <summary>Separators of the folders in the paths of mod files: '\' as written on Windows, '/' as in entry names.</summary>
        private static readonly char[] FilePathSeparators = { '\\', '/' };

        /// <summary>Characters Windows does not allow in file names, besides the control characters U+0000 to U+001F.</summary>
        private const string InvalidFileNameCharacters = "<>:\"|?*";

        /// <summary>Names Windows reserves for devices, also with an extension ("NUL.txt").</summary>
        private static readonly HashSet<string> ReservedFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "CON", "PRN", "AUX", "NUL",
            "COM0", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "COM\u00B9", "COM\u00B2", "COM\u00B3",
            "LPT0", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9", "LPT\u00B9", "LPT\u00B2", "LPT\u00B3"
        };

        /// <summary>File name of the banner with the given index, e.g. "Banner0.png".</summary>
        public static string GetBannerFileName(int index)
        {
            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(index));
            return BannerFilePrefix + index + BannerFileExtension;
        }

        /// <summary>
        /// Normalizes a path relative to a variant folder for comparisons and display: directory separators of
        /// the current OS, no leading or trailing separator.
        /// </summary>
        public static string NormalizeRelativePath(string relativePath)
        {
            if (relativePath == null)
                throw new ArgumentNullException(nameof(relativePath));
            return relativePath
                .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
                .Trim(Path.DirectorySeparatorChar);
        }

        /// <summary>
        /// Product folder that a path relative to a variant folder starts with, e.g. "EEC" for
        /// "EEC\Data\file.xml" (case-insensitive, returned as defined in <see cref="ProductFolders"/>).
        /// </summary>
        /// <returns>null for a file that is not inside a product folder, e.g. directly in the variant folder.</returns>
        public static string GetProductFolder(string relativePath)
        {
            string[] parts = NormalizeRelativePath(relativePath)
                .Split(new[] { Path.DirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
                return null;
            return ProductFolders.FirstOrDefault(folder => folder.Equals(parts[0], StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// The rule for the path of a mod file (<see cref="ModFile.RelativeFilePath"/>), relative to its variant folder: a
        /// product folder (<see cref="ProductFolders"/>, any case) and at least one more part, separated by '\' or '/', e.g.
        /// "EEC\Data\units.xml". Every part is a file name that Windows accepts: not empty, not "." or "..", no ':' (drive
        /// letters, alternate data streams), none of the other characters Windows forbids in file names, no device name such
        /// as "CON" or "NUL.txt", no '.' or ' ' at the end (Windows removes them). Such a path stays inside the product
        /// folder, whatever folder it is combined with.
        /// </summary>
        /// <remarks>
        /// Mod archives come from other people: <see cref="ModArchiveReader"/> rejects an archive with a path that breaks
        /// this rule, so that code that writes the files of a mod cannot be sent outside the game folder (path traversal,
        /// "zip slip"), and <see cref="ModPackageBuilder.ExportModInfos"/> does not write such a path into a mod.
        /// </remarks>
        public static bool IsValidFilePath(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath))
                return false;
            string[] parts = relativePath.Split(FilePathSeparators);
            return parts.Length >= 2 &&
                   ProductFolders.Any(folder => folder.Equals(parts[0], StringComparison.OrdinalIgnoreCase)) &&
                   parts.All(IsValidFileName);
        }

        private static bool IsValidFileName(string name)
        {
            if (name.Length == 0 || name.EndsWith(".", StringComparison.Ordinal) || name.EndsWith(" ", StringComparison.Ordinal))
                return false;
            if (name.Any(c => c < ' ' || InvalidFileNameCharacters.IndexOf(c) >= 0))
                return false;
            // Windows takes the name up to the first '.', without trailing spaces, for a device: "NUL.txt", "CON .log".
            int dot = name.IndexOf('.');
            return !ReservedFileNames.Contains((dot < 0 ? name : name.Substring(0, dot)).TrimEnd(' '));
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

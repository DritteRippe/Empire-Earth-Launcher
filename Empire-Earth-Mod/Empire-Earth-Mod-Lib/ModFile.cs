using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;

namespace Empire_Earth_Mod_Lib
{
    /// <remarks>The [DataMember] names are part of the mod archive format, see <see cref="EemFormat"/>.</remarks>
    [Serializable]
    [DataContract(Name = "ModFile", Namespace = "")]
    public class ModFile
    {
        [DataMember(Name = "path")]
        public string RelativeFilePath { get; set; }
        [DataMember(Name = "type")]
        public ModFileType FileType { get; set; }
        [DataMember(Name = "variant")]
        public Guid Variant { get; set; }
        /// <summary>
        /// Reserved, not used yet: the Creator always stores an empty string and nothing verifies it. MD5
        /// would only detect accidental corruption, not tampering; an integrity check for installing mods
        /// should use SHA-256 (and a trusted source for executable content) instead.
        /// </summary>
        [DataMember(Name = "md5")]
        public string Md5 { get; set; }

        /// <remarks>
        /// The [Description] is the name shown to the user (see <see cref="EnumExtensions.GetDescription"/>);
        /// each description must be unique.
        /// </remarks>
        public enum ModFileType
        {
            [Description("Data")] Data = 0,
            [Description("Config File")] ConfigFile = 1,
            [Description("Executable")] Executable = 2
        }

        /// <remarks>
        /// The [Description] is the name shown to the user, the folder of a product in a variant is defined
        /// by <see cref="GetFolderName"/>.
        /// </remarks>
        public enum ModFileProduct
        {
            // ReSharper disable once InconsistentNaming
            [Description("EEC")] EEC = 0,
            // ReSharper disable once InconsistentNaming
            [Description("AOC")] AOC = 1,
            [Description("Both")] Both = 2
        }

        /// <summary>
        /// The only mapping between the products and their folders in a variant (see <see cref="EemFormat"/>).
        /// </summary>
        private static readonly Dictionary<ModFileProduct, string> ProductFolderNames =
            new Dictionary<ModFileProduct, string>
            {
                { ModFileProduct.EEC, EemFormat.ProductFolderEec },
                { ModFileProduct.AOC, EemFormat.ProductFolderAoc },
                { ModFileProduct.Both, EemFormat.ProductFolderBoth }
            };

        public ModFile(string relativeFilePath, ModFileType type, Guid uuid, string md5)
        {
            FileType = type;
            Md5 = md5;
            RelativeFilePath = relativeFilePath;
            Variant = uuid;
        }

        /// <summary>Folder of <paramref name="product"/> in a variant, e.g. "all" for <see cref="ModFileProduct.Both"/>.</summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="product"/> is not a defined product.</exception>
        public static string GetFolderName(ModFileProduct product)
        {
            string folderName;
            if (!ProductFolderNames.TryGetValue(product, out folderName))
                throw new ArgumentOutOfRangeException(nameof(product), product, "Unknown product.");
            return folderName;
        }

        /// <summary>
        /// Product of a product folder, e.g. <see cref="ModFileProduct.Both"/> for "all" (case-insensitive).
        /// </summary>
        /// <returns>false if <paramref name="folderName"/> is not a product folder.</returns>
        public static bool TryParseFolderName(string folderName, out ModFileProduct product)
        {
            foreach (KeyValuePair<ModFileProduct, string> entry in ProductFolderNames)
            {
                if (string.Equals(entry.Value, folderName, StringComparison.OrdinalIgnoreCase))
                {
                    product = entry.Key;
                    return true;
                }
            }
            product = default;
            return false;
        }

        /// <summary>
        /// Product of the file, taken from the product folder its path starts with.
        /// </summary>
        /// <exception cref="InvalidOperationException">The file is not inside a product folder.</exception>
        public ModFileProduct GetProduct()
        {
            ModFileProduct product;
            if (!TryGetProduct(out product))
                throw new InvalidOperationException("The mod file " + RelativeFilePath + " is not inside a product folder.");
            return product;
        }

        /// <summary>
        /// Product of the file, taken from the product folder its path starts with (see
        /// <see cref="EemFormat.ProductFolders"/>).
        /// </summary>
        /// <returns>false if the file is not inside a product folder.</returns>
        public bool TryGetProduct(out ModFileProduct product)
        {
            return TryParseFolderName(EemFormat.GetProductFolder(RelativeFilePath), out product);
        }

        /// <summary>
        /// Path of the file below its product folder, e.g. "Data\file.xml" for "EEC\Data\file.xml".
        /// </summary>
        public string GetPathInProduct()
        {
            string relativePath = EemFormat.NormalizeRelativePath(RelativeFilePath);
            int separator = relativePath.IndexOf(Path.DirectorySeparatorChar);
            return separator < 0 ? relativePath : relativePath.Substring(separator + 1);
        }

        /// <summary>
        /// Suggested <see cref="ModFileType"/> for a file extension (e.g. ".exe"), compared case-insensitively.
        /// The mod author can change it in the mod creator, so it is a default and not a security check.
        /// </summary>
        public static ModFileType GetDefaultModFileType(string fileExtension)
        {
            var fileExtensions = new Dictionary<ModFileType, HashSet<string>>
            {
                {
                    ModFileType.Executable, new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ".exe",
                        ".dll",
                        ".bat",
                        ".cmd",
                        ".com",
                        ".scr",
                        ".msi",
                        ".ps1",
                        ".vbs",
                        ".js"
                    }
                },
                {
                    ModFileType.ConfigFile, new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ".json",
                        ".cfg",
                        ".xml",
                        ".config",
                        ".ini"
                    }
                }
            };
            
            return fileExtensions.Keys.FirstOrDefault(fileType => fileExtensions[fileType].Contains(fileExtension ?? string.Empty));
        }
    }
}
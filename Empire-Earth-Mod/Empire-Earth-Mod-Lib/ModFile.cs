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

        // Each description must be unique
        public enum ModFileType
        {
            [Description("Data")] Data = 0,
            [Description("Config File")] ConfigFile = 1,
            [Description("Executable")] Executable = 2
        }

        public enum ModFileProduct
        {
            // ReSharper disable once InconsistentNaming
            EEC = 0,
            // ReSharper disable once InconsistentNaming
            AOC = 1,
            Both = 2
        }

        public ModFile(string relativeFilePath, ModFileType type, Guid uuid, string md5)
        {
            FileType = type;
            Md5 = md5;
            RelativeFilePath = relativeFilePath;
            Variant = uuid;
        }

        public static string GetModFileName(ModFileType value)
        {
            DescriptionAttribute attribute = value.GetType()
                .GetField(value.ToString())
                .GetCustomAttributes(typeof(DescriptionAttribute), false)
                .SingleOrDefault() as DescriptionAttribute;
            return attribute == null ? value.ToString() : attribute.Description;
        }

        public static ModFileType ParseModFileType(string description)
        {
            var type = typeof(ModFileType);
            if (!type.IsEnum)
                throw new ArgumentException();
            var fields = type.GetFields();
            var field = fields
                .SelectMany(f => f.GetCustomAttributes(
                    typeof(DescriptionAttribute), false), (
                    f, a) => new { Field = f, Att = a }).SingleOrDefault(a => ((DescriptionAttribute)a.Att)
                    .Description == description);
            return field == null ? default : (ModFileType)field.Field.GetRawConstantValue();
        }

        public static ModFileProduct GetProduct(string filePath)
        {
            if (filePath.StartsWith("EEC"))
                return ModFileProduct.EEC;
            if (filePath.StartsWith("AOC"))
                return ModFileProduct.AOC;
            if (filePath.StartsWith("Both"))
                return ModFileProduct.Both;
            throw new Exception("Unknown product");
        }
        
        public ModFileProduct GetProduct()
        {
            ModFileProduct product;
            if (TryGetProduct(out product))
                return product;
            throw new Exception("Unknown product");
        }

        /// <summary>
        /// Product of the file, taken from the product folder its path starts with (see
        /// <see cref="EemFormat.ProductFolders"/>).
        /// </summary>
        /// <returns>false if the file is not inside a product folder.</returns>
        public bool TryGetProduct(out ModFileProduct product)
        {
            switch (EemFormat.GetProductFolder(RelativeFilePath))
            {
                case EemFormat.ProductFolderEec:
                    product = ModFileProduct.EEC;
                    return true;
                case EemFormat.ProductFolderAoc:
                    product = ModFileProduct.AOC;
                    return true;
                case EemFormat.ProductFolderBoth:
                    product = ModFileProduct.Both;
                    return true;
                default:
                    product = default;
                    return false;
            }
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
        
        public static string ParseModFileProduct(ModFileProduct product)
        {
            switch (product)
            {
                case ModFileProduct.EEC:
                    return EemFormat.ProductFolderEec;
                case ModFileProduct.AOC:
                    return EemFormat.ProductFolderAoc;
                case ModFileProduct.Both:
                    return EemFormat.ProductFolderBoth;
                default:
                    throw new Exception("Unknown product");
            }
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
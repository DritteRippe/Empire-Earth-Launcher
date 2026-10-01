using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Empire_Earth_Mod_Lib.Serialization;

namespace Empire_Earth_Mod_Lib
{
    [Serializable]
    public class ModData
    {
        // Mod UUID
        public Guid Uuid { get; set; }

        // Mod Image & Banner(s)
        [field: NonSerialized] private Image Icon { get; set; }

        [field: NonSerialized] private Dictionary<Guid, List<Image>> Banners { get; set; }

        // Mod Basic Info
        public string Name { get; set; }
        public string Description { get; set; }
        public List<string> Authors { get; set; }

        public string Contact { get; set; }

        //public List<string> SupportedLanguages { get; set; }
        public Dictionary<Guid, string> Variants { get; set; }
        public List<ModFile> ModFiles { get; set; }
        public List<string> RequiredMods { get; set; }
        public List<string> IncompatibleMods { get; set; }

        public string LicenseName { get; set; }
        public string LicenseTxt { get; set; }

        // Mod Version
        public Version Version { get; set; }
        public DateTime BuildDate { get; set; }

        // Mod EE Impact
        //public bool EE { get; set; }
        //public bool AoC { get; set; } 

        public WindowsVersion.WindowsVersionEnum MinWindows { get; set; }

        public ModData()
        {
            Uuid = Guid.NewGuid();
            Banners = new Dictionary<Guid, List<Image>>();
            Authors = new List<string>();
            //SupportedLanguages = new List<string>();
            Variants = new Dictionary<Guid, string> { { Guid.Empty, "default" } };
            ModFiles = new List<ModFile>();
            RequiredMods = new List<string>();
            IncompatibleMods = new List<string>();
            BuildDate = DateTime.Now;
        }

        public void ClearVariants()
        {
            Variants.ToList().ForEach(variant =>
            {
                if (variant.Key != Guid.Empty)
                    Variants.Remove(variant.Key);
            });
        }

        public void AddOrUpdateVariant(Guid uuid, string name)
        {
            if (Variants.ContainsKey(uuid))
                Variants[uuid] = name;
            else
                Variants.Add(uuid, name);
            
            // Create list if it doesn't exist
            if (!Banners.ContainsKey(uuid))
                Banners.Add(uuid, new List<Image>());
        }

        /// <summary>
        /// Remove a variant from the mod
        /// </summary>
        /// <param name="uuid">The variant UUID</param>
        /// <returns>true if some related variant data got deleted, false if not</returns>
        /// <exception cref="DataException">variant don't exist or trying to delete default variant</exception>
        public bool RemoveVariant(Guid uuid)
        {
            if (!Variants.ContainsKey(uuid))
                throw new DataException("Variant does not exist");
            if (uuid == Guid.Empty)
                throw new DataException("Unable to delete the default variant");
            bool affected = Banners.ContainsKey(uuid);
            Variants.Remove(uuid);
            Banners.Remove(uuid);
            return affected;
        }

        public bool DoesVariantExist(Guid variant)
        {
            return Variants.ContainsKey(variant);
        }

        public void SetIcon(Image icon)
        {
            if (icon.Size != new Size(128, 128))
                throw new FormatException("Icon must be 128x128");
            Icon = icon;
        }

        public Image GetIcon()
        {
            return Icon;
        }

        public void AddBanner(Image banner, Guid variant)
        {
            if (!DoesVariantExist(variant))
                throw new Exception("Variant does not exist");
            if (!Banners.ContainsKey(variant))
                Banners.Add(variant, new List<Image>());
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (Math.Round(double.Parse(banner.Width.ToString()) /
                           double.Parse(banner.Height.ToString()), 2) != 1.78)
                throw new FormatException("Banner must be 16:9");
            if (banner.Width < 1280 || banner.Width > 1920 || banner.Height < 720 || banner.Height > 1080)
                throw new FormatException("Banner must be >= 1280x720 and <= 1920x1080");
            Banners[variant].Add(banner);
        }

        public bool HasBanner(Guid variant)
        {
            return Banners.ContainsKey(variant) && Banners[variant].Count > 0;
        }

        public List<Image> GetBanners(Guid variant)
        {
            if (!DoesVariantExist(variant))
                throw new Exception("Variant does not exist");
            return Banners[variant];
        }

        public void GetImageAsync(string url)
        {
            // Since I use .NET 4 (to support WinXP) I can't download that asych...
            // So I need some background worker sh$t or idk...
        }

        /// <summary>
        /// Load a mod from a mod archive
        /// </summary>
        /// <param name="eemPath">Path to the mod archive</param>
        /// <returns>Mod present in the archive</returns>
        /// <exception cref="Exception">If the archive isn't valid or parse failed</exception>
        // ReSharper disable once InconsistentNaming
        public static ModData LoadFromEEM(string eemPath)
        {
            using (MemoryStream dataStream = new MemoryStream())
            {
                using (var zip = ZipStorer.Open(eemPath, FileAccess.Read))
                {
                    var entry = zip.ReadCentralDir();
                    bool valid = entry.Exists(etr => etr.FilenameInZip.Equals("data"));
                    if (!valid)
                        throw new Exception("Unable to load the EEM: data file not found!");
                    zip.ExtractFile(entry.First(etr => etr.FilenameInZip.Equals("data")), dataStream);
                }

                if (dataStream.Length == 0 && !dataStream.CanRead)
                    throw new Exception("Unable to parse the data file of EEM!");

                ModData modData = JsonSerializer<ModData>.Deserialize(dataStream);
                return modData;
            }
        }

        /// <summary>
        /// Return a JSON equivalent of the ModData (without Icon & Banners)
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return JsonSerializer<ModData>.Serialize(this);
        }

        public class Creator
        {
            private ModData ModData { get; set; }
            private string WorkingDir { get; set; }

            public Creator(ModData mod, string workingDir, bool eraseData = true)
            {
                ModData = mod;
                WorkingDir = Path.GetFullPath(workingDir);
                if (eraseData && Directory.Exists(WorkingDir))
                    Directory.Delete(WorkingDir, true);
                if (!Directory.Exists(WorkingDir))
                    Directory.CreateDirectory(WorkingDir);
            }

            ~Creator()
            {
                if (Directory.Exists(WorkingDir))
                    Directory.Delete(WorkingDir, true);
            }

            /// <summary>
            /// This function reload all variants in order to have in the
            /// working directory a list of all variants UUID and the game
            /// base mod directory pre-created
            /// </summary>
            public void GenerateVariantsFolders()
            {
                if (!Directory.Exists(WorkingDir))
                    Directory.CreateDirectory(WorkingDir);

                // Clear old variants
                foreach (var alreadyCreatedVariantDir in new DirectoryInfo(WorkingDir).GetDirectories())
                {
                    Guid supposedUuid;
                    try
                    {
                        supposedUuid = Guid.Parse(alreadyCreatedVariantDir.Name);
                    }
                    catch (FormatException)
                    {
                        continue; // Just ignore, not a variant folder... not normal
                    }

                    if (!ModData.DoesVariantExist(supposedUuid))
                        alreadyCreatedVariantDir.Delete(true);
                }

                // Create new variants
                List<string> baseFilesProduct = new List<string>
                {
                    "all", "EEC", "AOC"
                };

                List<string> baseFilesStructure = new List<string>
                {
                    "Data",
                    "Data/Campaigns",
                    "Data/db",
                    "Data/Models",
                    "Data/Movies",
                    "Data/Music",
                    "Data/Random Map Scripts",
                    "Data/Saved Games",
                    "Data/Scenarios",
                    "Data/Sounds",
                    "Data/Textures",
                    "Data/Unit AI Scripts",
                    "Data/WONLobby Resources",
                    "Users/default/Civilizations"
                };

                foreach (var variant in ModData.Variants.Where(variant =>
                             !Directory.Exists(Path.Combine(WorkingDir, variant.Key.ToString()))))
                {
                    Directory.CreateDirectory(Path.Combine(WorkingDir, variant.Key.ToString()));
                    foreach (var baseFile in baseFilesProduct)
                    {
                        Directory.CreateDirectory(Path.Combine(WorkingDir, variant.Key.ToString(), baseFile));
                        foreach (var baseFileStructure in baseFilesStructure)
                        {
                            Directory.CreateDirectory(Path.Combine(WorkingDir, variant.Key.ToString(), baseFile,
                                baseFileStructure));
                        }
                    }
                }
            }

            public void ExportBannersAndIcon(Task task = null)
            {
                GenerateVariantsFolders();

                // Delete old banners and icon
                if (File.Exists(Path.Combine(WorkingDir, "Icon.png")))
                    File.Delete(Path.Combine(WorkingDir, "Icon.png"));
                
                foreach (var variant in ModData.Variants)
                {
                    foreach (var banner in new DirectoryInfo(Path.Combine(WorkingDir, variant.Key.ToString()))
                                 .GetFiles("Banner*"))
                    {
                        banner.Delete();
                    }
                }

                // Export banners and icon
                ModData.Icon.Save(Path.Combine(WorkingDir, "Icon.png"));
                foreach (var variant in ModData.Variants.Keys.Where(variant => ModData.HasBanner(variant)))
                {
                    for (int i = 0; i != ModData.GetBanners(variant).Count; ++i)
                        ModData.GetBanners(variant)[i].Save(Path.Combine(WorkingDir,
                            variant.ToString(), "Banner" + i + ".png"));
                }
            }

            public void ExportModInfos()
            {
                File.WriteAllText(Path.Combine(WorkingDir, "data"), ModData.ToString());
            }

            public void ExportToZip(Task task = null)
            {
                var parentDir = new DirectoryInfo(WorkingDir).Parent;
                if (parentDir == null)
                    return;
                using (ZipStorer zipStore =
                       ZipStorer.Create(Path.Combine(parentDir.FullName,
                           ModData.Uuid.ToString())))
                {
                    zipStore.AddDirectory(ZipStorer.Compression.Deflate,
                        WorkingDir,
                        Path.DirectorySeparatorChar.ToString(),
                        "Created with Launcher v" + BuildInfo.InformationalVersion);
                }
            }

            public void ReloadModFiles(Guid variant)
            {
                GenerateVariantsFolders();
                
                var allFiles = new DirectoryInfo(Path.Combine(WorkingDir, variant.ToString()))
                    .EnumerateFiles("*", SearchOption.AllDirectories).ToList();

                // Index new files
                foreach (var file in allFiles)
                {
                    string localFilePath = file.FullName.Replace(
                        Path.Combine(WorkingDir, variant.ToString()), string.Empty);
                    if (localFilePath.StartsWith(Path.DirectorySeparatorChar.ToString()))
                        localFilePath = localFilePath.Substring(1);

                    // Avoid Banner indexation
                    if (localFilePath.StartsWith("Banner"))
                        continue;

                    bool containsFile = ModData.ModFiles.Any(modFile =>
                        modFile.Variant == variant &&
                        modFile.RelativeFilePath.Equals(localFilePath, StringComparison.InvariantCultureIgnoreCase));

                    if (!containsFile)
                    {
                        ModData.ModFiles.Add(new ModFile(localFilePath,
                            ModFile.GetDefaultModFileType(Path.GetExtension(localFilePath)),
                            variant, string.Empty));
                    }
                }


                // Remove old files
                foreach (var modFile in ModData.ModFiles.Where(modFile => modFile.Variant == variant))
                {
                    bool exist = false;
                    foreach (var file in allFiles)
                    {
                        string localFilePath =
                            file.FullName.Replace(Path.Combine(WorkingDir, variant.ToString()), string.Empty);
                        if (localFilePath.StartsWith(Path.DirectorySeparatorChar.ToString()))
                            localFilePath = localFilePath.Substring(1);
                        if (localFilePath.Equals(modFile.RelativeFilePath, StringComparison.InvariantCultureIgnoreCase))
                        {
                            exist = true;
                            break;
                        }
                    }

                    if (!exist)
                        ModData.ModFiles.Remove(modFile);
                }
            }

            public void UpdateModFiles(Guid variant, string relativePath, ModFile.ModFileType modFileType)
            {
                var find = ModData.ModFiles.Find(
                    modFile => modFile.Variant == variant
                               && modFile.RelativeFilePath.Equals(relativePath,
                                   StringComparison.InvariantCultureIgnoreCase));
                if (find != null)
                    find.FileType = modFileType;
            }

            public string GetWorkingDir()
            {
                return WorkingDir;
            }
        }
    }
}
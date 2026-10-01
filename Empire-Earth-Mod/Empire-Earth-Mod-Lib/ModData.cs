using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.Serialization;
using Empire_Earth_Mod_Lib.Serialization;

namespace Empire_Earth_Mod_Lib
{
    /// <remarks>
    /// The [DataMember] names define the JSON of the <see cref="EemFormat.DataEntryName"/> entry of a mod
    /// archive; without [DataContract] the JSON keys were compiler generated backing field names such as
    /// "&lt;Uuid&gt;k__BackingField".
    /// </remarks>
    [DataContract(Name = "ModData", Namespace = "")]
    public class ModData
    {
        /// <summary>Name of the variant every mod has (<see cref="Guid.Empty"/>).</summary>
        public const string DefaultVariantName = "default";

        // Mod UUID
        [DataMember(Name = "uuid")]
        public Guid Uuid { get; set; }

        // Mod Image & Banner(s)
        private Image Icon { get; set; }

        private Dictionary<Guid, List<Image>> Banners { get; set; }

        // Mod Basic Info
        [DataMember(Name = "name")]
        public string Name { get; set; }
        [DataMember(Name = "description")]
        public string Description { get; set; }
        [DataMember(Name = "authors")]
        public List<string> Authors { get; set; }

        [DataMember(Name = "contact")]
        public string Contact { get; set; }

        //public List<string> SupportedLanguages { get; set; }
        [DataMember(Name = "variants")]
        public Dictionary<Guid, string> Variants { get; set; }
        [DataMember(Name = "files")]
        public List<ModFile> ModFiles { get; set; }
        [DataMember(Name = "requiredMods")]
        public List<string> RequiredMods { get; set; }
        [DataMember(Name = "incompatibleMods")]
        public List<string> IncompatibleMods { get; set; }

        [DataMember(Name = "licenseName")]
        public string LicenseName { get; set; }
        [DataMember(Name = "licenseText")]
        public string LicenseTxt { get; set; }

        // Mod Version
        public Version Version { get; set; }
        [DataMember(Name = "buildDate")]
        public DateTime BuildDate { get; set; }

        /// <summary>
        /// <see cref="Version"/> as text (e.g. "1.0.0.0") in the data file, instead of the private fields of
        /// System.Version.
        /// </summary>
        [DataMember(Name = "version")]
        private string VersionText
        {
            get { return Version == null ? null : Version.ToString(); }
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    Version = null;
                    return;
                }

                System.Version version;
                if (!System.Version.TryParse(value, out version))
                    throw new SerializationException("Invalid mod version \"" + value + "\".");
                Version = version;
            }
        }

        // Mod EE Impact
        //public bool EE { get; set; }
        //public bool AoC { get; set; } 

        [DataMember(Name = "minWindows")]
        public WindowsVersion.WindowsVersionEnum MinWindows { get; set; }

        public ModData()
        {
            Uuid = Guid.NewGuid();
            Banners = new Dictionary<Guid, List<Image>>();
            Authors = new List<string>();
            //SupportedLanguages = new List<string>();
            Variants = new Dictionary<Guid, string> { { Guid.Empty, DefaultVariantName } };
            ModFiles = new List<ModFile>();
            RequiredMods = new List<string>();
            IncompatibleMods = new List<string>();
            BuildDate = DateTime.Now;
        }

        /// <summary>
        /// Deserialization does not run the constructor. Creates the members that are not serialized
        /// (otherwise Banners stayed null and AddOrUpdateVariant/HasBanner threw a NullReferenceException) and
        /// tolerates collections missing from a data file.
        /// </summary>
        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            Banners = new Dictionary<Guid, List<Image>>();
            if (Authors == null)
                Authors = new List<string>();
            if (Variants == null)
                Variants = new Dictionary<Guid, string> { { Guid.Empty, DefaultVariantName } };
            if (ModFiles == null)
                ModFiles = new List<ModFile>();
            if (RequiredMods == null)
                RequiredMods = new List<string>();
            if (IncompatibleMods == null)
                IncompatibleMods = new List<string>();
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
        /// Remove a variant from the mod, together with its banners and its entries in <see cref="ModFiles"/>
        /// </summary>
        /// <param name="uuid">The variant UUID</param>
        /// <returns>true if some related variant data (banners, files) got deleted, false if not</returns>
        /// <exception cref="DataException">variant don't exist or trying to delete default variant</exception>
        public bool RemoveVariant(Guid uuid)
        {
            if (!Variants.ContainsKey(uuid))
                throw new DataException("Variant does not exist");
            if (uuid == Guid.Empty)
                throw new DataException("Unable to delete the default variant");
            bool hadBanners = HasBanner(uuid);
            bool hadFiles = ModFiles.RemoveAll(modFile => modFile.Variant == uuid) > 0;
            Variants.Remove(uuid);
            Banners.Remove(uuid);
            return hadBanners || hadFiles;
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
            // Variants that never had a banner (e.g. the default variant or a loaded mod) have no list yet.
            if (!Banners.ContainsKey(variant))
                Banners.Add(variant, new List<Image>());
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
        /// <returns>Mod present in the archive (icon and banners are not loaded)</returns>
        /// <exception cref="IOException">The file cannot be read.</exception>
        /// <exception cref="InvalidDataException">The file is not a valid mod archive.</exception>
        // ReSharper disable once InconsistentNaming
        public static ModData LoadFromEEM(string eemPath)
        {
            using (FileStream eemStream = File.OpenRead(eemPath))
            {
                return LoadFromEEM(eemStream);
            }
        }

        /// <summary>
        /// Load a mod from a mod archive in a seekable stream. The stream is not closed.
        /// </summary>
        /// <param name="eemStream">Stream containing the mod archive (see <see cref="EemFormat"/>)</param>
        /// <returns>Mod present in the archive (icon and banners are not loaded)</returns>
        /// <exception cref="IOException">The stream cannot be read.</exception>
        /// <exception cref="InvalidDataException">The stream is not a valid mod archive.</exception>
        // ReSharper disable once InconsistentNaming
        public static ModData LoadFromEEM(Stream eemStream)
        {
            if (eemStream == null)
                throw new ArgumentNullException(nameof(eemStream));

            byte[] data;
            using (var zip = ZipStorer.Open(eemStream, FileAccess.Read, true))
            {
                ZipStorer.ZipFileEntry dataEntry = zip.ReadCentralDir()
                    .FirstOrDefault(entry => entry.FilenameInZip == EemFormat.DataEntryName);
                if (dataEntry == null)
                    throw new InvalidDataException("Invalid mod archive: the \"" + EemFormat.DataEntryName + "\" entry is missing.");
                if (dataEntry.FileSize > EemFormat.MaxDataEntryBytes)
                    throw new InvalidDataException("Invalid mod archive: the \"" + EemFormat.DataEntryName + "\" entry is too large.");
                if (!zip.ExtractFile(dataEntry, out data) || data.Length == 0)
                    throw new InvalidDataException("Invalid mod archive: the \"" + EemFormat.DataEntryName + "\" entry cannot be read.");
            }

            ModData modData;
            try
            {
                // A new stream starts at position 0; the deserializer reads from the current position.
                using (var dataStream = new MemoryStream(data))
                {
                    modData = DataContractJsonHelper<ModData>.Deserialize(dataStream);
                }
            }
            catch (SerializationException ex)
            {
                throw new InvalidDataException("Invalid mod archive: the \"" + EemFormat.DataEntryName + "\" entry is not valid mod data. " + ex.Message, ex);
            }

            if (modData == null)
                throw new InvalidDataException("Invalid mod archive: the \"" + EemFormat.DataEntryName + "\" entry is empty.");
            return modData;
        }

        /// <summary>
        /// Return a JSON equivalent of the ModData (without Icon & Banners)
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return DataContractJsonHelper<ModData>.Serialize(this);
        }

        /// <summary>
        /// Prepares the files of a mod in a working directory and packs them into a mod archive.
        /// </summary>
        /// <remarks>
        /// Every Creator works in its own, newly created directory (a GUID below
        /// <see cref="DefaultWorkspaceRoot"/> or the given root), never in a path relative to the current
        /// directory, and never deletes anything it did not create. The working directory is only deleted
        /// on request (<see cref="DeleteWorkingDirectory"/>, or <see cref="Dispose"/> with
        /// <see cref="EraseDataOnDispose"/>), because the mod author copies the mod files into it.
        /// </remarks>
        public sealed class Creator : IDisposable
        {
            private ModData ModData { get; set; }
            private string WorkingDir { get; set; }
            private bool disposed;

            /// <summary>
            /// Default parent folder of the working directories:
            /// %LOCALAPPDATA%\Empire Earth Launcher\Mod Creator.
            /// </summary>
            public static string DefaultWorkspaceRoot
            {
                get
                {
                    return Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "Empire Earth Launcher", "Mod Creator");
                }
            }

            /// <summary>
            /// Whether <see cref="Dispose"/> deletes the working directory and everything in it.
            /// </summary>
            public bool EraseDataOnDispose { get; set; }

            /// <summary>
            /// Creates a new working directory below <see cref="DefaultWorkspaceRoot"/>.
            /// </summary>
            /// <param name="mod">The mod to build.</param>
            /// <param name="eraseData">Delete the working directory on <see cref="Dispose"/>. Off by default so
            /// that files copied into it are never lost by accident.</param>
            public Creator(ModData mod, bool eraseData = false)
                : this(mod, DefaultWorkspaceRoot, eraseData)
            {
            }

            /// <summary>
            /// Creates a new working directory below <paramref name="workspaceRoot"/>.
            /// </summary>
            /// <param name="mod">The mod to build.</param>
            /// <param name="workspaceRoot">Absolute path of the folder that receives the working directory.</param>
            /// <param name="eraseData">Delete the working directory on <see cref="Dispose"/>. Off by default so
            /// that files copied into it are never lost by accident.</param>
            /// <exception cref="ArgumentException"><paramref name="workspaceRoot"/> is not an absolute path.</exception>
            public Creator(ModData mod, string workspaceRoot, bool eraseData = false)
            {
                if (mod == null)
                    throw new ArgumentNullException(nameof(mod));
                if (string.IsNullOrEmpty(workspaceRoot) || !Path.IsPathRooted(workspaceRoot))
                    throw new ArgumentException("The workspace root must be an absolute path.", nameof(workspaceRoot));

                string workingDir = Path.Combine(Path.GetFullPath(workspaceRoot), Guid.NewGuid().ToString("N"));
                // Practically impossible with a new GUID, but an existing folder must never be adopted (and
                // later deleted) as if this instance had created it.
                if (Directory.Exists(workingDir))
                    throw new IOException("The working directory " + workingDir + " already exists.");
                Directory.CreateDirectory(workingDir);

                ModData = mod;
                WorkingDir = workingDir;
                EraseDataOnDispose = eraseData;
            }

            /// <summary>
            /// Ends the session. With <see cref="EraseDataOnDispose"/> the working directory created by this
            /// instance is deleted; errors (e.g. a file still opened in Explorer) are ignored here, call
            /// <see cref="DeleteWorkingDirectory"/> first to get them reported.
            /// </summary>
            public void Dispose()
            {
                if (disposed)
                    return;
                disposed = true;

                if (!EraseDataOnDispose)
                    return;
                try
                {
                    DeleteWorkingDirectoryCore();
                }
                catch (IOException)
                {
                    // Best effort, see summary.
                }
                catch (UnauthorizedAccessException)
                {
                    // Best effort, see summary.
                }
            }

            /// <summary>
            /// Deletes the working directory created by this instance, including the files the mod author
            /// copied into it.
            /// </summary>
            public void DeleteWorkingDirectory()
            {
                ThrowIfDisposed();
                DeleteWorkingDirectoryCore();
            }

            private void DeleteWorkingDirectoryCore()
            {
                if (Directory.Exists(WorkingDir))
                    Directory.Delete(WorkingDir, true);
            }

            /// <summary>
            /// True if the mod author put files into the product folders of any variant, i.e. deleting the
            /// working directory would lose work.
            /// </summary>
            public bool ContainsModFiles()
            {
                ThrowIfDisposed();
                if (!Directory.Exists(WorkingDir))
                    return false;
                return new DirectoryInfo(WorkingDir).GetDirectories()
                    .SelectMany(variantDir => variantDir.GetDirectories())
                    .Any(productDir => productDir.EnumerateFiles("*", SearchOption.AllDirectories).Any());
            }

            private void ThrowIfDisposed()
            {
                if (disposed)
                    throw new ObjectDisposedException(GetType().Name);
            }

            /// <summary>
            /// This function reload all variants in order to have in the
            /// working directory a list of all variants UUID and the game
            /// base mod directory pre-created
            /// </summary>
            public void GenerateVariantsFolders()
            {
                ThrowIfDisposed();
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
                IList<string> baseFilesProduct = EemFormat.ProductFolders;

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

            /// <summary>
            /// Runs all build steps in order: <see cref="ExportModInfos"/>, <see cref="ExportBannersAndIcon"/>
            /// and <see cref="ExportToZip"/>. A failing step throws and stops the build.
            /// </summary>
            /// <remarks>
            /// Synchronous; for large mods call it from a background thread. The icon and banner images of the
            /// mod are saved by the calling thread, so nothing else (e.g. a PictureBox) may use them meanwhile:
            /// GDI+ images are not thread-safe.
            /// </remarks>
            /// <param name="eemPath">Path of the mod archive to create.</param>
            /// <exception cref="InvalidOperationException">The mod is not complete (e.g. it has no icon).</exception>
            public void Build(string eemPath)
            {
                ThrowIfDisposed();
                if (ModData.Icon == null)
                    throw new InvalidOperationException("The mod has no icon. Select an icon before building the mod.");

                ExportModInfos();
                ExportBannersAndIcon();
                ExportToZip(eemPath);
            }

            public void ExportBannersAndIcon()
            {
                if (ModData.Icon == null)
                    throw new InvalidOperationException("The mod has no icon. Select an icon before building the mod.");
                GenerateVariantsFolders();

                // Delete old banners and icon (only the files written by a previous export)
                string iconPath = Path.Combine(WorkingDir, EemFormat.IconEntryName);
                if (File.Exists(iconPath))
                    File.Delete(iconPath);
                
                foreach (var variant in ModData.Variants)
                {
                    foreach (var banner in new DirectoryInfo(Path.Combine(WorkingDir, variant.Key.ToString()))
                                 .GetFiles(EemFormat.BannerFilePrefix + "*" + EemFormat.BannerFileExtension))
                    {
                        banner.Delete();
                    }
                }

                // Export banners and icon. The format is explicit: Image.Save(path) would keep the format the
                // image was loaded from (e.g. JPEG) despite the .png name.
                ModData.Icon.Save(iconPath, ImageFormat.Png);
                foreach (var variant in ModData.Variants.Keys.Where(variant => ModData.HasBanner(variant)))
                {
                    for (int i = 0; i != ModData.GetBanners(variant).Count; ++i)
                        ModData.GetBanners(variant)[i].Save(Path.Combine(WorkingDir,
                            variant.ToString(), EemFormat.GetBannerFileName(i)), ImageFormat.Png);
                }
            }

            public void ExportModInfos()
            {
                ThrowIfDisposed();
                File.WriteAllText(Path.Combine(WorkingDir, EemFormat.DataEntryName), ModData.ToString());
            }

            /// <summary>
            /// Packs the working directory into the mod archive <paramref name="eemPath"/>. The archive is
            /// written to a temporary file first, so a failed build neither leaves a partial archive nor
            /// destroys an existing one.
            /// </summary>
            /// <exception cref="ArgumentException"><paramref name="eemPath"/> is empty or inside the working directory.</exception>
            public void ExportToZip(string eemPath)
            {
                ThrowIfDisposed();
                if (string.IsNullOrEmpty(eemPath))
                    throw new ArgumentException("The path of the mod archive is missing.", nameof(eemPath));

                string archivePath = Path.GetFullPath(eemPath);
                if (archivePath.StartsWith(WorkingDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("The mod archive cannot be saved inside the working directory.", nameof(eemPath));

                string temporaryPath = archivePath + ".tmp";
                try
                {
                    WriteArchive(temporaryPath);
                    if (File.Exists(archivePath))
                        File.Replace(temporaryPath, archivePath, null);
                    else
                        File.Move(temporaryPath, archivePath);
                }
                finally
                {
                    if (File.Exists(temporaryPath))
                        File.Delete(temporaryPath);
                }
            }

            private void WriteArchive(string archivePath)
            {
                using (ZipStorer zipStore = ZipStorer.Create(archivePath,
                           "Created with Launcher v" + BuildInfo.InformationalVersion))
                {
                    zipStore.EncodeUTF8 = true;
                    // One entry per file, named relative to the working directory. ZipStorer.AddDirectory would
                    // put everything below the name of the working directory ("creator/data"), where the import
                    // does not look.
                    foreach (string file in Directory.GetFiles(WorkingDir, "*", SearchOption.AllDirectories)
                                 .OrderBy(file => file, StringComparer.Ordinal))
                    {
                        zipStore.AddFile(ZipStorer.Compression.Deflate, file,
                            EemFormat.ToEntryName(GetRelativePath(WorkingDir, file)), string.Empty);
                    }
                }
            }

            /// <summary>
            /// Path of <paramref name="fullPath"/> relative to <paramref name="baseDirectory"/>, which must
            /// contain it. Both paths must be absolute.
            /// </summary>
            private static string GetRelativePath(string baseDirectory, string fullPath)
            {
                string prefix = baseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                                Path.DirectorySeparatorChar;
                if (!fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException(fullPath + " is not inside " + baseDirectory + ".", nameof(fullPath));
                return fullPath.Substring(prefix.Length);
            }

            /// <summary>
            /// Synchronizes the <see cref="ModData.ModFiles"/> of a variant with its folder: indexes new files
            /// and removes the entries of deleted files. Only files inside a product folder
            /// (<see cref="EemFormat.ProductFolders"/>) are mod files.
            /// </summary>
            /// <returns>
            /// Paths (relative to the variant folder) of the files that are ignored because they are not inside
            /// a product folder. The banners written by the export are not reported.
            /// </returns>
            public List<string> ReloadModFiles(Guid variant)
            {
                GenerateVariantsFolders();

                string variantDir = Path.Combine(WorkingDir, variant.ToString());
                var existingFiles = new List<string>();
                var existingFileSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var ignoredFiles = new List<string>();
                foreach (var file in new DirectoryInfo(variantDir).EnumerateFiles("*", SearchOption.AllDirectories))
                {
                    string relativePath = GetRelativePath(variantDir, file.FullName);
                    if (EemFormat.GetProductFolder(relativePath) == null)
                    {
                        if (!IsExportedBanner(relativePath))
                            ignoredFiles.Add(relativePath);
                        continue;
                    }

                    if (existingFileSet.Add(relativePath))
                        existingFiles.Add(relativePath);
                }

                // Remove old files. RemoveAll instead of Remove inside a foreach over the same list, which threw
                // "Collection was modified" as soon as one file had been deleted.
                ModData.ModFiles.RemoveAll(modFile => modFile.Variant == variant &&
                    !existingFileSet.Contains(EemFormat.NormalizeRelativePath(modFile.RelativeFilePath)));

                // Index new files
                var indexedFileSet = new HashSet<string>(
                    ModData.ModFiles.Where(modFile => modFile.Variant == variant)
                        .Select(modFile => EemFormat.NormalizeRelativePath(modFile.RelativeFilePath)),
                    StringComparer.OrdinalIgnoreCase);
                foreach (string relativePath in existingFiles)
                {
                    if (indexedFileSet.Add(relativePath))
                    {
                        ModData.ModFiles.Add(new ModFile(relativePath,
                            ModFile.GetDefaultModFileType(Path.GetExtension(relativePath)),
                            variant, string.Empty));
                    }
                }

                return ignoredFiles;
            }

            /// <summary>
            /// True for a banner written by <see cref="ExportBannersAndIcon"/> (directly in the variant folder).
            /// </summary>
            private static bool IsExportedBanner(string relativePath)
            {
                string fileName = Path.GetFileName(relativePath);
                return fileName == relativePath &&
                       fileName.StartsWith(EemFormat.BannerFilePrefix, StringComparison.OrdinalIgnoreCase) &&
                       fileName.EndsWith(EemFormat.BannerFileExtension, StringComparison.OrdinalIgnoreCase);
            }

            public string GetWorkingDir()
            {
                return WorkingDir;
            }
        }
    }
}
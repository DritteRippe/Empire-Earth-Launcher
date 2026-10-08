using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace Empire_Earth_Mod_Lib
{
    /// <summary>
    /// Prepares the files of a mod in a working directory and packs them into a mod archive (.eem).
    /// </summary>
    /// <remarks>
    /// Every builder works in its own, newly created directory (a GUID below
    /// <see cref="DefaultWorkspaceRoot"/> or the given root), never in a path relative to the current
    /// directory, and never deletes anything it did not create. The working directory is only deleted
    /// on request (<see cref="DeleteWorkingDirectory"/>, or <see cref="Dispose"/> with
    /// <see cref="EraseDataOnDispose"/>), because the mod author copies the mod files into it.
    /// </remarks>
    public sealed class ModPackageBuilder : IDisposable
    {
        private readonly ModData mod;
        private readonly ModAssets assets;
        private bool disposed;

        /// <summary>
        /// Default parent folder of the working directories:
        /// %LOCALAPPDATA%\Empire Earth Launcher\Mod Creator, or the same below the temporary folder for
        /// accounts without a profile (like the launcher's LauncherPaths.UserDataDirectory).
        /// </summary>
        public static string DefaultWorkspaceRoot
        {
            get
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                // Empty for accounts without a profile; a relative root would be rejected by the constructor.
                if (string.IsNullOrEmpty(localAppData))
                    localAppData = Path.GetTempPath();
                return Path.Combine(localAppData, "Empire Earth Launcher", "Mod Creator");
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
        /// <param name="assets">Icon and banners of the mod.</param>
        /// <param name="eraseData">Delete the working directory on <see cref="Dispose"/>. Off by default so
        /// that files copied into it are never lost by accident.</param>
        public ModPackageBuilder(ModData mod, ModAssets assets, bool eraseData = false)
            : this(mod, assets, DefaultWorkspaceRoot, eraseData)
        {
        }

        /// <summary>
        /// Creates a new working directory below <paramref name="workspaceRoot"/>.
        /// </summary>
        /// <param name="mod">The mod to build.</param>
        /// <param name="assets">Icon and banners of the mod.</param>
        /// <param name="workspaceRoot">Absolute path of the folder that receives the working directory.</param>
        /// <param name="eraseData">Delete the working directory on <see cref="Dispose"/>. Off by default so
        /// that files copied into it are never lost by accident.</param>
        /// <exception cref="ArgumentException"><paramref name="workspaceRoot"/> is not an absolute path.</exception>
        public ModPackageBuilder(ModData mod, ModAssets assets, string workspaceRoot, bool eraseData = false)
        {
            if (mod == null)
                throw new ArgumentNullException(nameof(mod));
            if (assets == null)
                throw new ArgumentNullException(nameof(assets));
            if (string.IsNullOrEmpty(workspaceRoot) || !Path.IsPathRooted(workspaceRoot))
                throw new ArgumentException("The workspace root must be an absolute path.", nameof(workspaceRoot));

            string workingDir = Path.Combine(Path.GetFullPath(workspaceRoot), Guid.NewGuid().ToString("N"));
            // Practically impossible with a new GUID, but an existing folder must never be adopted (and
            // later deleted) as if this instance had created it.
            if (Directory.Exists(workingDir))
                throw new IOException("The working directory " + workingDir + " already exists.");
            Directory.CreateDirectory(workingDir);

            this.mod = mod;
            this.assets = assets;
            WorkingDirectory = workingDir;
            EraseDataOnDispose = eraseData;
        }

        /// <summary>Full path of the working directory created by this instance.</summary>
        public string WorkingDirectory { get; }

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
            if (Directory.Exists(WorkingDirectory))
                Directory.Delete(WorkingDirectory, true);
        }

        /// <summary>
        /// True if the mod author put files into the product folders of any variant, i.e. deleting the
        /// working directory would lose work.
        /// </summary>
        public bool ContainsModFiles()
        {
            ThrowIfDisposed();
            if (!Directory.Exists(WorkingDirectory))
                return false;
            return new DirectoryInfo(WorkingDirectory).GetDirectories()
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
            if (!Directory.Exists(WorkingDirectory))
                Directory.CreateDirectory(WorkingDirectory);

            // Clear old variants
            foreach (var alreadyCreatedVariantDir in new DirectoryInfo(WorkingDirectory).GetDirectories())
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

                if (!mod.DoesVariantExist(supposedUuid))
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

            foreach (var variant in mod.Variants.Where(variant =>
                         !Directory.Exists(Path.Combine(WorkingDirectory, variant.Key.ToString()))))
            {
                Directory.CreateDirectory(Path.Combine(WorkingDirectory, variant.Key.ToString()));
                foreach (var baseFile in baseFilesProduct)
                {
                    Directory.CreateDirectory(Path.Combine(WorkingDirectory, variant.Key.ToString(), baseFile));
                    foreach (var baseFileStructure in baseFilesStructure)
                    {
                        Directory.CreateDirectory(Path.Combine(WorkingDirectory, variant.Key.ToString(), baseFile,
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
        /// <exception cref="InvalidOperationException">The mod is not complete (no icon, name or version), or the path of a
        /// mod file breaks <see cref="EemFormat.IsValidFilePath"/>.</exception>
        public void Build(string eemPath)
        {
            ThrowIfDisposed();
            if (assets.Icon == null)
                throw new InvalidOperationException("The mod has no icon. Select an icon before building the mod.");
            // The mod creator validates these on its first page; checked here too, so that no caller can
            // publish an archive that the launcher could not identify.
            if (string.IsNullOrWhiteSpace(mod.Name))
                throw new InvalidOperationException("The mod has no name.");
            if (mod.Version == null)
                throw new InvalidOperationException("The mod has no version.");

            ExportModInfos();
            ExportBannersAndIcon();
            ExportToZip(eemPath);
        }

        public void ExportBannersAndIcon()
        {
            if (assets.Icon == null)
                throw new InvalidOperationException("The mod has no icon. Select an icon before building the mod.");
            GenerateVariantsFolders();

            // Delete old banners and icon (only the files written by a previous export)
            string iconPath = Path.Combine(WorkingDirectory, EemFormat.IconEntryName);
            if (File.Exists(iconPath))
                File.Delete(iconPath);
            
            foreach (var variant in mod.Variants)
            {
                foreach (var banner in new DirectoryInfo(Path.Combine(WorkingDirectory, variant.Key.ToString()))
                             .GetFiles(EemFormat.BannerFilePrefix + "*" + EemFormat.BannerFileExtension))
                {
                    banner.Delete();
                }
            }

            // Export banners and icon. The format is explicit: Image.Save(path) would keep the format the
            // image was loaded from (e.g. JPEG) despite the .png name.
            assets.Icon.Save(iconPath, ImageFormat.Png);
            // Only the variants of the mod: banners of a removed variant are not exported.
            foreach (Guid variant in mod.Variants.Keys.Where(assets.HasBanner))
            {
                IList<Image> banners = assets.GetBanners(variant);
                for (int i = 0; i != banners.Count; ++i)
                    banners[i].Save(Path.Combine(WorkingDirectory, variant.ToString(), EemFormat.GetBannerFileName(i)),
                        ImageFormat.Png);
            }
        }

        /// <summary>
        /// Writes the mod data (<see cref="EemFormat.DataEntryName"/>) into the working directory.
        /// </summary>
        /// <exception cref="InvalidOperationException">The path of a mod file breaks <see cref="EemFormat.IsValidFilePath"/>:
        /// <see cref="ModArchiveReader"/> would reject the archive.</exception>
        public void ExportModInfos()
        {
            ThrowIfDisposed();
            foreach (ModFile modFile in mod.ModFiles)
            {
                if (modFile == null || !EemFormat.IsValidFilePath(modFile.RelativeFilePath))
                    throw new InvalidOperationException("The path of the mod file \"" + modFile?.RelativeFilePath +
                                                        "\" is not a relative path inside one of the folders " +
                                                        string.Join(", ", EemFormat.ProductFolders) + ".");
            }
            File.WriteAllText(Path.Combine(WorkingDirectory, EemFormat.DataEntryName), mod.ToString());
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
            if (archivePath.StartsWith(WorkingDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
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
                foreach (string file in Directory.GetFiles(WorkingDirectory, "*", SearchOption.AllDirectories)
                             .OrderBy(file => file, StringComparer.Ordinal))
                {
                    zipStore.AddFile(ZipStorer.Compression.Deflate, file,
                        EemFormat.ToEntryName(GetRelativePath(WorkingDirectory, file)), string.Empty);
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

            string variantDir = Path.Combine(WorkingDirectory, variant.ToString());
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
            mod.ModFiles.RemoveAll(modFile => modFile.Variant == variant &&
                !existingFileSet.Contains(EemFormat.NormalizeRelativePath(modFile.RelativeFilePath)));

            // Index new files
            var indexedFileSet = new HashSet<string>(
                mod.ModFiles.Where(modFile => modFile.Variant == variant)
                    .Select(modFile => EemFormat.NormalizeRelativePath(modFile.RelativeFilePath)),
                StringComparer.OrdinalIgnoreCase);
            foreach (string relativePath in existingFiles)
            {
                if (indexedFileSet.Add(relativePath))
                {
                    mod.ModFiles.Add(new ModFile(relativePath,
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
    }
}

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Empire_Earth_Launcher.Tests.TestSupport;
using Empire_Earth_Mod_Lib;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Mod
{
    /// <summary>
    /// The working directory of the mod creator: what it creates, what it deletes (only its own folder) and
    /// how it indexes the files the mod author copies into it.
    /// </summary>
    [TestFixture]
    public class ModPackageBuilderTests
    {
        private TemporaryDirectory workspace;
        private ModData mod;
        private ModAssets assets;

        [SetUp]
        public void SetUp()
        {
            workspace = new TemporaryDirectory();
            mod = new ModData { Name = "Test mod", Version = new Version(1, 0) };
            assets = new ModAssets();
        }

        [TearDown]
        public void TearDown()
        {
            workspace.Dispose();
        }

        private ModPackageBuilder CreateBuilder(bool eraseData = false)
        {
            return new ModPackageBuilder(mod, assets, workspace.Path, eraseData);
        }

        /// <summary>Creates a file below the folder of <paramref name="variant"/> in the working directory.</summary>
        private static string AddFile(ModPackageBuilder builder, Guid variant, string relativePath)
        {
            string path = Path.Combine(builder.WorkingDirectory, variant.ToString(),
                relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, relativePath);
            return path;
        }

        private List<string> IndexedPaths(Guid variant)
        {
            return mod.ModFiles.Where(file => file.Variant == variant)
                .Select(file => file.RelativeFilePath.Replace(Path.DirectorySeparatorChar, '/'))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToList();
        }

        /* Working directory: never somebody else's folder */

        [Test]
        public void Constructor_CreatesANewFolderBelowTheWorkspaceRoot()
        {
            string existingFolder = Directory.CreateDirectory(workspace.Combine("existing")).FullName;

            using (ModPackageBuilder builder = CreateBuilder())
            {
                Assert.That(Directory.Exists(builder.WorkingDirectory), Is.True);
                Assert.That(Path.GetDirectoryName(builder.WorkingDirectory), Is.EqualTo(workspace.Path));
                Assert.That(builder.WorkingDirectory, Is.Not.EqualTo(existingFolder));
                Assert.That(Directory.GetFileSystemEntries(builder.WorkingDirectory), Is.Empty);
            }
        }

        [Test]
        public void Constructor_RelativeWorkspaceRoot_IsRejectedAndCreatesNothing()
        {
            // The creator used to work in "./creator", relative to whatever the current directory was (korr-S2).
            string relativeRoot = "creator-" + Guid.NewGuid().ToString("N");

            Assert.That(() => new ModPackageBuilder(mod, assets, relativeRoot), Throws.TypeOf<ArgumentException>());
            Assert.That(() => new ModPackageBuilder(mod, assets, string.Empty), Throws.TypeOf<ArgumentException>());
            Assert.That(Directory.Exists(Path.Combine(Environment.CurrentDirectory, relativeRoot)), Is.False);
        }

        [Test]
        public void TwoBuilders_UseDifferentFolders()
        {
            using (ModPackageBuilder first = CreateBuilder())
            using (ModPackageBuilder second = CreateBuilder())
            {
                Assert.That(second.WorkingDirectory, Is.Not.EqualTo(first.WorkingDirectory));
            }
        }

        [Test]
        public void DeleteWorkingDirectory_DeletesOnlyItsOwnFolder()
        {
            string foreignFile = workspace.CreateFile("foreign/keep.txt", "not created by the builder");
            string rootFile = workspace.CreateFile("keep-too.txt");

            using (ModPackageBuilder other = CreateBuilder())
            using (ModPackageBuilder builder = CreateBuilder())
            {
                builder.GenerateVariantsFolders();
                AddFile(builder, Guid.Empty, "EEC/Data/file.xml");
                string otherFile = AddFile(other, Guid.Empty, "EEC/Data/other.xml");

                builder.DeleteWorkingDirectory();

                Assert.That(builder.WorkingDirectory, Does.Not.Exist);
                Assert.That(otherFile, Does.Exist);
                Assert.That(foreignFile, Does.Exist);
                Assert.That(rootFile, Does.Exist);
                Assert.That(workspace.Path, Does.Exist);
            }
        }

        [Test]
        public void Dispose_ByDefault_KeepsTheWorkingDirectory()
        {
            string workingDirectory;
            using (ModPackageBuilder builder = CreateBuilder())
            {
                workingDirectory = builder.WorkingDirectory;
                AddFile(builder, Guid.Empty, "EEC/Data/file.xml");
            }

            Assert.That(workingDirectory, Does.Exist);
        }

        [Test]
        public void Dispose_WithEraseData_DeletesOnlyItsOwnFolder()
        {
            string foreignFile = workspace.CreateFile("foreign/keep.txt");
            string workingDirectory;
            using (ModPackageBuilder other = CreateBuilder(eraseData: false))
            {
                using (ModPackageBuilder builder = CreateBuilder(eraseData: true))
                {
                    workingDirectory = builder.WorkingDirectory;
                    AddFile(builder, Guid.Empty, "EEC/Data/file.xml");
                }

                Assert.That(workingDirectory, Does.Not.Exist);
                Assert.That(other.WorkingDirectory, Does.Exist);
                Assert.That(foreignFile, Does.Exist);
            }
        }

        [Test]
        public void AbandonedBuilder_IsNotDeletedByTheGarbageCollector()
        {
            // A finalizer of an old creator instance used to delete the folder of the instance that was open
            // at that moment (wart-M1). Without a finalizer nothing happens when a builder is collected.
            string workingDirectory = CreateAbandonedBuilder(eraseData: true);

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            Assert.That(workingDirectory, Does.Exist);
            Assert.That(Directory.GetFiles(workingDirectory, "*", SearchOption.AllDirectories), Is.Not.Empty);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private string CreateAbandonedBuilder(bool eraseData)
        {
            ModPackageBuilder builder = CreateBuilder(eraseData);
            AddFile(builder, Guid.Empty, "EEC/Data/file.xml");
            return builder.WorkingDirectory;
        }

        [Test]
        public void DisposedBuilder_RefusesFurtherWork()
        {
            ModPackageBuilder builder = CreateBuilder();
            builder.Dispose();
            builder.Dispose(); // a second Dispose does nothing

            Assert.That(() => builder.DeleteWorkingDirectory(), Throws.TypeOf<ObjectDisposedException>());
            Assert.That(() => builder.GenerateVariantsFolders(), Throws.TypeOf<ObjectDisposedException>());
            Assert.That(() => builder.ExportModInfos(), Throws.TypeOf<ObjectDisposedException>());
        }

        [Test]
        public void ExportToZip_InsideTheWorkingDirectory_IsRejected()
        {
            using (ModPackageBuilder builder = CreateBuilder())
            {
                string archiveInside = Path.Combine(builder.WorkingDirectory, "mod" + EemFormat.Extension);

                Assert.That(() => builder.ExportToZip(archiveInside), Throws.TypeOf<ArgumentException>());
                Assert.That(() => builder.ExportToZip(string.Empty), Throws.TypeOf<ArgumentException>());
                Assert.That(archiveInside, Does.Not.Exist);
            }
        }

        [TestCase(@"EEC\..\..\evil.dll")]
        [TestCase(@"C:\Windows\evil.dll")]
        [TestCase("readme.txt")]
        public void ExportModInfos_FilePathOutsideTheProductFolders_WritesNoModData(string relativePath)
        {
            // The mod archive reader would reject the archive (EemFormat.IsValidFilePath); a caller of the library that
            // adds such a file to the mod gets the reason when it builds, not a mod that nobody can load.
            mod.ModFiles.Add(new ModFile(relativePath, ModFile.ModFileType.Data, Guid.Empty, string.Empty));
            using (ModPackageBuilder builder = CreateBuilder())
            {
                Assert.That(() => builder.ExportModInfos(),
                    Throws.TypeOf<InvalidOperationException>().With.Message.Contains(relativePath));
                Assert.That(Path.Combine(builder.WorkingDirectory, EemFormat.DataEntryName), Does.Not.Exist);
            }
        }

        [Test]
        public void GenerateVariantsFolders_RemovesOnlyFoldersOfRemovedVariants()
        {
            Guid removedVariant = Guid.NewGuid();
            mod.AddOrUpdateVariant(removedVariant, "Removed");
            using (ModPackageBuilder builder = CreateBuilder())
            {
                builder.GenerateVariantsFolders();
                string notAVariant = Directory.CreateDirectory(Path.Combine(builder.WorkingDirectory, "notes")).FullName;
                string removedFolder = Path.Combine(builder.WorkingDirectory, removedVariant.ToString());
                Assert.That(removedFolder, Does.Exist);

                mod.RemoveVariant(removedVariant);
                builder.GenerateVariantsFolders();

                Assert.That(removedFolder, Does.Not.Exist);
                Assert.That(notAVariant, Does.Exist);
                Assert.That(Path.Combine(builder.WorkingDirectory, Guid.Empty.ToString()), Does.Exist);
            }
        }

        [Test]
        public void GenerateVariantsFolders_CreatesTheProductFoldersOfEveryVariant()
        {
            Guid variant = Guid.NewGuid();
            mod.AddOrUpdateVariant(variant, "HD");
            using (ModPackageBuilder builder = CreateBuilder())
            {
                builder.GenerateVariantsFolders();

                foreach (Guid id in new[] { Guid.Empty, variant })
                {
                    foreach (string product in EemFormat.ProductFolders)
                        Assert.That(Path.Combine(builder.WorkingDirectory, id.ToString(), product, "Data"), Does.Exist);
                }
                // Empty folders are not mod files.
                Assert.That(builder.ContainsModFiles(), Is.False);
            }
        }

        [Test]
        public void ContainsModFiles_IsTrueOnceAFileIsInAProductFolder()
        {
            using (ModPackageBuilder builder = CreateBuilder())
            {
                builder.GenerateVariantsFolders();
                Assert.That(builder.ContainsModFiles(), Is.False);

                AddFile(builder, Guid.Empty, "all/Data/db/dbobjects.dat");

                Assert.That(builder.ContainsModFiles(), Is.True);
            }
        }

        /* ReloadModFiles */

        [Test]
        public void ReloadModFiles_IndexesTheFilesOfTheProductFoldersWithTheirDefaultType()
        {
            using (ModPackageBuilder builder = CreateBuilder())
            {
                AddFile(builder, Guid.Empty, "EEC/Data/units.XML");
                AddFile(builder, Guid.Empty, "AOC/Tools/Patch.EXE");
                AddFile(builder, Guid.Empty, "all/Data/Textures/grass.tga");

                List<string> ignored = builder.ReloadModFiles(Guid.Empty);

                Assert.That(ignored, Is.Empty);
                Assert.That(IndexedPaths(Guid.Empty), Is.EqualTo(new[]
                {
                    "AOC/Tools/Patch.EXE", "EEC/Data/units.XML", "all/Data/Textures/grass.tga"
                }));
                Assert.That(mod.ModFiles.Single(f => f.RelativeFilePath.EndsWith("Patch.EXE")).FileType,
                    Is.EqualTo(ModFile.ModFileType.Executable));
                Assert.That(mod.ModFiles.Single(f => f.RelativeFilePath.EndsWith("units.XML")).FileType,
                    Is.EqualTo(ModFile.ModFileType.ConfigFile));
                Assert.That(mod.ModFiles.Single(f => f.RelativeFilePath.EndsWith("grass.tga")).FileType,
                    Is.EqualTo(ModFile.ModFileType.Data));
            }
        }

        [Test]
        public void ReloadModFiles_ReportsFilesOutsideTheProductFoldersButNotTheBanners()
        {
            using (ModPackageBuilder builder = CreateBuilder())
            {
                AddFile(builder, Guid.Empty, "EEC/Data/file.xml");
                AddFile(builder, Guid.Empty, "readme.txt");
                AddFile(builder, Guid.Empty, "Data/misplaced.xml");
                AddFile(builder, Guid.Empty, EemFormat.GetBannerFileName(0));

                List<string> ignored = builder.ReloadModFiles(Guid.Empty);

                Assert.That(ignored.Select(path => path.Replace(Path.DirectorySeparatorChar, '/')),
                    Is.EquivalentTo(new[] { "readme.txt", "Data/misplaced.xml" }));
                Assert.That(IndexedPaths(Guid.Empty), Is.EqualTo(new[] { "EEC/Data/file.xml" }));
            }
        }

        [Test]
        public void ReloadModFiles_ReportsAFileThatOnlyLooksLikeABanner()
        {
            using (ModPackageBuilder builder = CreateBuilder())
            {
                AddFile(builder, Guid.Empty, EemFormat.GetBannerFileName(1));
                AddFile(builder, Guid.Empty, "BannerSource.png");

                List<string> ignored = builder.ReloadModFiles(Guid.Empty);

                Assert.That(ignored, Is.EqualTo(new[] { "BannerSource.png" }));
            }
        }

        [Test]
        public void ExportBannersAndIcon_DeletesOnlyTheBannersOfAnEarlierExport()
        {
            // All files the search pattern "Banner*.png" found were deleted, also a source image of the author (and, where
            // Windows matches the pattern against short names, "Banner1.pngx").
            assets.Icon = new Bitmap(ModImageRules.IconSize, ModImageRules.IconSize);
            using (ModPackageBuilder builder = CreateBuilder())
            {
                builder.GenerateVariantsFolders();
                string oldBanner = AddFile(builder, Guid.Empty, EemFormat.GetBannerFileName(3));
                string[] filesOfTheAuthor =
                {
                    AddFile(builder, Guid.Empty, "BannerSource.png"),
                    AddFile(builder, Guid.Empty, "Banner1.png.bak"),
                    AddFile(builder, Guid.Empty, "Banner1.pngx"),
                };

                builder.ExportBannersAndIcon();

                Assert.That(oldBanner, Does.Not.Exist);
                foreach (string file in filesOfTheAuthor)
                    Assert.That(file, Does.Exist);
                Assert.That(Path.Combine(builder.WorkingDirectory, EemFormat.IconEntryName), Does.Exist);
            }
        }

        [Test]
        public void ReloadModFiles_DeletedFiles_AreRemovedFromTheMod()
        {
            // Removing entries while enumerating the list threw "Collection was modified" (korr-S6, wart-S4).
            Guid otherVariant = Guid.NewGuid();
            mod.AddOrUpdateVariant(otherVariant, "HD");
            using (ModPackageBuilder builder = CreateBuilder())
            {
                string first = AddFile(builder, Guid.Empty, "EEC/Data/a.xml");
                AddFile(builder, Guid.Empty, "EEC/Data/b.xml");
                string third = AddFile(builder, Guid.Empty, "EEC/Data/c.xml");
                AddFile(builder, otherVariant, "EEC/Data/a.xml");
                builder.ReloadModFiles(Guid.Empty);
                builder.ReloadModFiles(otherVariant);
                Assert.That(IndexedPaths(Guid.Empty).Count, Is.EqualTo(3));

                File.Delete(first);
                File.Delete(third);
                builder.ReloadModFiles(Guid.Empty);

                Assert.That(IndexedPaths(Guid.Empty), Is.EqualTo(new[] { "EEC/Data/b.xml" }));
                // The files of another variant are not touched.
                Assert.That(IndexedPaths(otherVariant), Is.EqualTo(new[] { "EEC/Data/a.xml" }));
            }
        }

        [Test]
        public void ReloadModFiles_AllFilesDeleted_LeavesNoEntry()
        {
            using (ModPackageBuilder builder = CreateBuilder())
            {
                string first = AddFile(builder, Guid.Empty, "EEC/Data/a.xml");
                string second = AddFile(builder, Guid.Empty, "AOC/Data/b.xml");
                builder.ReloadModFiles(Guid.Empty);

                File.Delete(first);
                File.Delete(second);
                builder.ReloadModFiles(Guid.Empty);

                Assert.That(mod.ModFiles, Is.Empty);
            }
        }

        [Test]
        public void ReloadModFiles_Repeated_KeepsEntriesAndTheTypeChosenByTheAuthor()
        {
            using (ModPackageBuilder builder = CreateBuilder())
            {
                AddFile(builder, Guid.Empty, "EEC/Data/a.xml");
                builder.ReloadModFiles(Guid.Empty);
                mod.ModFiles.Single().FileType = ModFile.ModFileType.Data;

                builder.ReloadModFiles(Guid.Empty);
                builder.ReloadModFiles(Guid.Empty);

                Assert.That(mod.ModFiles.Count, Is.EqualTo(1));
                Assert.That(mod.ModFiles.Single().FileType, Is.EqualTo(ModFile.ModFileType.Data));
            }
        }
    }
}

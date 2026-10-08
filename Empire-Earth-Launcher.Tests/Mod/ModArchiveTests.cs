using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using Empire_Earth_Launcher.Tests.TestSupport;
using Empire_Earth_Mod_Lib;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Mod
{
    /// <summary>
    /// Mod archives (.eem): export with <see cref="ModPackageBuilder"/>, import with
    /// <see cref="ModArchiveReader"/> and <see cref="ModManager"/>. Before the fixes an exported archive could
    /// never be imported (korr-S7, wart-S3).
    /// </summary>
    [TestFixture]
    public class ModArchiveTests
    {
        private TemporaryDirectory directory;

        [SetUp]
        public void SetUp()
        {
            directory = new TemporaryDirectory();
        }

        [TearDown]
        public void TearDown()
        {
            directory.Dispose();
        }

        private static Bitmap CreateImage(int width, int height, Color color)
        {
            var image = new Bitmap(width, height);
            using (Graphics graphics = Graphics.FromImage(image))
            {
                graphics.Clear(color);
            }
            return image;
        }

        private static void AddModFile(ModPackageBuilder builder, Guid variant, string relativePath, string content)
        {
            string path = Path.Combine(builder.WorkingDirectory, variant.ToString(),
                relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, content);
        }

        private static List<string> EntryNames(string eemPath)
        {
            using (ZipStorer zip = ZipStorer.Open(eemPath, FileAccess.Read))
            {
                return zip.ReadCentralDir().Select(entry => entry.FilenameInZip).ToList();
            }
        }

        private static byte[] ExtractEntry(string eemPath, string entryName)
        {
            using (ZipStorer zip = ZipStorer.Open(eemPath, FileAccess.Read))
            {
                ZipStorer.ZipFileEntry entry = zip.ReadCentralDir().Single(e => e.FilenameInZip == entryName);
                byte[] content;
                Assert.That(zip.ExtractFile(entry, out content), Is.True, entryName);
                return content;
            }
        }

        /// <summary>An archive with the given entries (name, UTF-8 content).</summary>
        private static MemoryStream CreateArchive(params KeyValuePair<string, string>[] entries)
        {
            var stream = new MemoryStream();
            using (ZipStorer zip = ZipStorer.Create(stream, string.Empty, true))
            {
                zip.EncodeUTF8 = true;
                foreach (KeyValuePair<string, string> entry in entries)
                {
                    using (var content = new MemoryStream(Encoding.UTF8.GetBytes(entry.Value)))
                    {
                        zip.AddStream(ZipStorer.Compression.Deflate, entry.Key, content, DateTime.Now);
                    }
                }
            }
            stream.Position = 0;
            return stream;
        }

        private static KeyValuePair<string, string> Entry(string name, string content)
        {
            return new KeyValuePair<string, string>(name, content);
        }

        /// <summary>Builds a small mod without images into <paramref name="eemPath"/>.</summary>
        private void ExportWithoutImages(ModData mod, string eemPath)
        {
            using (var builder = new ModPackageBuilder(mod, new ModAssets(), directory.Combine("workspace"), true))
            {
                builder.ExportModInfos();
                builder.ExportToZip(eemPath);
            }
        }

        /* Round trip */

        [Test]
        public void Build_ThenRead_RestoresTheModAndCleansUp()
        {
            var mod = new ModData
            {
                Name = "Better Graphics – ü",
                Description = "First line\nSecond line with \"quotes\" and 玩家",
                Version = new Version(1, 2, 3, 4),
                Contact = "modder@example.invalid",
                LicenseName = "GPL-3.0",
                LicenseText = "Some license text",
                MinWindows = WindowsVersion.WindowsVersionEnum.Xp
            };
            mod.Authors.Add("Alice");
            mod.Authors.Add("Bjørn");
            mod.RequiredMods.Add("base-mod");
            mod.IncompatibleMods.Add("other-mod");
            Guid hd = Guid.NewGuid();
            mod.AddOrUpdateVariant(hd, "HD");

            var assets = new ModAssets { Icon = CreateImage(ModImageRules.IconSize, ModImageRules.IconSize, Color.Red) };
            assets.AddBanner(Guid.Empty, CreateImage(1280, 720, Color.Green));
            assets.AddBanner(hd, CreateImage(1920, 1080, Color.Blue));
            assets.AddBanner(hd, CreateImage(1280, 720, Color.Yellow));

            string workspace = directory.Combine("workspace");
            string eemPath = directory.Combine("Better Graphics" + EemFormat.Extension);
            string workingDirectory;
            using (var builder = new ModPackageBuilder(mod, assets, workspace))
            {
                workingDirectory = builder.WorkingDirectory;
                AddModFile(builder, Guid.Empty, "EEC/Data/units.xml", "<units/>");
                AddModFile(builder, hd, "all/Data/Textures/grass.tga", "texture");
                AddModFile(builder, hd, "AOC/Tools/Patch.EXE", "MZ");
                Assert.That(builder.ReloadModFiles(Guid.Empty), Is.Empty);
                Assert.That(builder.ReloadModFiles(hd), Is.Empty);

                builder.Build(eemPath);

                builder.DeleteWorkingDirectory();
            }

            // Cleanup: no working directory, no temporary archive, nothing else in the workspace.
            Assert.That(workingDirectory, Does.Not.Exist);
            Assert.That(Directory.GetFileSystemEntries(workspace), Is.Empty);
            Assert.That(Directory.GetFiles(directory.Path), Is.EqualTo(new[] { eemPath }));

            ModData read = ModArchiveReader.ReadModData(eemPath);

            Assert.That(read.Uuid, Is.EqualTo(mod.Uuid));
            Assert.That(read.Name, Is.EqualTo(mod.Name));
            Assert.That(read.Description, Is.EqualTo(mod.Description));
            Assert.That(read.Version, Is.EqualTo(mod.Version));
            Assert.That(read.Contact, Is.EqualTo(mod.Contact));
            Assert.That(read.LicenseName, Is.EqualTo(mod.LicenseName));
            Assert.That(read.LicenseText, Is.EqualTo(mod.LicenseText));
            Assert.That(read.MinWindows, Is.EqualTo(mod.MinWindows));
            Assert.That(read.BuildDate, Is.EqualTo(mod.BuildDate).Within(TimeSpan.FromSeconds(1)));
            Assert.That(read.Authors, Is.EqualTo(mod.Authors));
            Assert.That(read.RequiredMods, Is.EqualTo(mod.RequiredMods));
            Assert.That(read.IncompatibleMods, Is.EqualTo(mod.IncompatibleMods));
            Assert.That(read.Variants, Is.EquivalentTo(mod.Variants));
            Assert.That(read.ModFiles.Select(file => file.Variant + " " + file.RelativeFilePath + " " + file.FileType),
                Is.EquivalentTo(mod.ModFiles.Select(file => file.Variant + " " + file.RelativeFilePath + " " + file.FileType)));
            Assert.That(read.ModFiles.Single(file => file.Variant == hd && file.RelativeFilePath.EndsWith("Patch.EXE")).FileType,
                Is.EqualTo(ModFile.ModFileType.Executable));
        }

        [Test]
        public void Build_WritesTheEntriesAtTheRootOfTheArchive()
        {
            var mod = new ModData { Name = "Layout", Version = new Version(1, 0) };
            Guid hd = Guid.NewGuid();
            mod.AddOrUpdateVariant(hd, "HD");
            var assets = new ModAssets { Icon = CreateImage(128, 128, Color.Red) };
            assets.AddBanner(hd, CreateImage(1280, 720, Color.Blue));
            assets.AddBanner(hd, CreateImage(1280, 720, Color.Green));
            string eemPath = directory.Combine("layout" + EemFormat.Extension);

            using (var builder = new ModPackageBuilder(mod, assets, directory.Combine("workspace"), true))
            {
                AddModFile(builder, Guid.Empty, "EEC/Data/units.xml", "<units/>");
                AddModFile(builder, hd, "all/Data/db/dbobjects.dat", "db");
                builder.ReloadModFiles(Guid.Empty);
                builder.ReloadModFiles(hd);
                builder.Build(eemPath);
            }

            // No "creator/" prefix, '/' as separator, the documented names (see EemFormat).
            Assert.That(EntryNames(eemPath), Is.EquivalentTo(new[]
            {
                EemFormat.DataEntryName,
                EemFormat.IconEntryName,
                hd + "/" + EemFormat.GetBannerFileName(0),
                hd + "/" + EemFormat.GetBannerFileName(1),
                Guid.Empty + "/EEC/Data/units.xml",
                hd + "/all/Data/db/dbobjects.dat"
            }));
            Assert.That(Encoding.UTF8.GetString(ExtractEntry(eemPath, Guid.Empty + "/EEC/Data/units.xml")),
                Is.EqualTo("<units/>"));

            // Icon and banners are PNG files, whatever format they were loaded from.
            using (var iconStream = new MemoryStream(ExtractEntry(eemPath, EemFormat.IconEntryName)))
            using (Image icon = Image.FromStream(iconStream))
            {
                Assert.That(icon.RawFormat.Guid, Is.EqualTo(ImageFormat.Png.Guid));
                Assert.That(icon.Size, Is.EqualTo(new Size(128, 128)));
            }
            using (var bannerStream = new MemoryStream(ExtractEntry(eemPath, hd + "/" + EemFormat.GetBannerFileName(1))))
            using (Image banner = Image.FromStream(bannerStream))
            {
                Assert.That(banner.RawFormat.Guid, Is.EqualTo(ImageFormat.Png.Guid));
                Assert.That(banner.Size, Is.EqualTo(new Size(1280, 720)));
            }
        }

        [Test]
        public void ExportToZip_PacksOnlyTheFilesOfTheFormat()
        {
            // Everything in the working directory was packed, also files that ReloadModFiles reports as ignored, such as
            // notes of the author next to the product folders, which then were published with the mod.
            var mod = new ModData { Name = "Clean", Version = new Version(1, 0) };
            string eemPath = directory.Combine("clean" + EemFormat.Extension);
            using (var builder = new ModPackageBuilder(mod, new ModAssets(), directory.Combine("workspace"), true))
            {
                AddModFile(builder, Guid.Empty, "EEC/Data/units.xml", "<units/>");
                AddModFile(builder, Guid.Empty, EemFormat.GetBannerFileName(0), "banner");
                AddModFile(builder, Guid.Empty, "notes.txt", "private notes");
                AddModFile(builder, Guid.Empty, "Data/misplaced.xml", "<misplaced/>");
                AddModFile(builder, Guid.Empty, "BannerSource.png", "source image");
                Assert.That(builder.ReloadModFiles(Guid.Empty), Has.Count.EqualTo(3), "files reported as ignored");
                // Left next to the variants, and the folder of a variant that is not (or no longer) part of the mod.
                File.WriteAllText(Path.Combine(builder.WorkingDirectory, "todo.txt"), "next steps");
                AddModFile(builder, Guid.NewGuid(), "EEC/Data/old.xml", "<old/>");
                builder.ExportModInfos();

                builder.ExportToZip(eemPath);
            }

            Assert.That(EntryNames(eemPath), Is.EquivalentTo(new[]
            {
                EemFormat.DataEntryName,
                Guid.Empty + "/" + EemFormat.GetBannerFileName(0),
                Guid.Empty + "/EEC/Data/units.xml"
            }));
            Assert.That(ModArchiveReader.ReadModData(eemPath).ModFiles.Count, Is.EqualTo(1));
        }

        /// <summary>
        /// The vendored ZipStorer keeps sizes and offsets in 32 bits and wrote a damaged archive, without an error, for a file
        /// or an archive of 4 GB or more; the mod creator then reported the mod as built. The tests lower the limit, which
        /// stands for ModPackageBuilder.MaxArchiveBytes.
        /// </summary>
        [TestCase(20000, 1, TestName = "ExportToZip_FileLargerThanTheLimit_KeepsTheExistingArchive")]
        [TestCase(6000, 2, TestName = "ExportToZip_ArchiveLargerThanTheLimit_KeepsTheExistingArchive")]
        public void ExportToZip_LargerThanTheLimit_KeepsTheExistingArchive(int fileBytes, int files)
        {
            string eemPath = directory.Combine("large" + EemFormat.Extension);
            ExportWithoutImages(new ModData { Name = "Before", Version = new Version(1, 0) }, eemPath);
            byte[] before = File.ReadAllBytes(eemPath);

            var mod = new ModData { Name = "Large", Version = new Version(2, 0) };
            using (var builder = new ModPackageBuilder(mod, new ModAssets(), directory.Combine("workspace"), true))
            {
                var random = new Random(1);
                for (int i = 0; i < files; i++)
                {
                    // Random bytes do not compress: the archive is at least as large as the files.
                    var content = new byte[fileBytes];
                    random.NextBytes(content);
                    string path = Path.Combine(builder.WorkingDirectory, Guid.Empty.ToString(), "EEC", "Data", "Movies",
                        "movie" + i + ".bik");
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    File.WriteAllBytes(path, content);
                }
                builder.ReloadModFiles(Guid.Empty);
                builder.ExportModInfos();
                builder.ArchiveSizeLimit = 10000;

                Assert.That(() => builder.ExportToZip(eemPath),
                    Throws.TypeOf<IOException>().With.Message.Contains("4 GB or larger"));
            }

            Assert.That(File.ReadAllBytes(eemPath), Is.EqualTo(before));
            Assert.That(Directory.GetFiles(directory.Path), Is.EqualTo(new[] { eemPath }), "no temporary archive is left");
        }

        [Test]
        public void ExportToZip_ArchiveOfTheLimit_IsWritten()
        {
            string eemPath = directory.Combine("limit" + EemFormat.Extension);
            using (var builder = new ModPackageBuilder(new ModData { Name = "Limit", Version = new Version(1, 0) },
                       new ModAssets(), directory.Combine("workspace"), true))
            {
                AddModFile(builder, Guid.Empty, "EEC/Data/units.xml", "<units/>");
                builder.ReloadModFiles(Guid.Empty);
                builder.ExportModInfos();
                builder.ExportToZip(eemPath);
                long size = new FileInfo(eemPath).Length;

                builder.ArchiveSizeLimit = size;
                builder.ExportToZip(eemPath);

                Assert.That(new FileInfo(eemPath).Length, Is.EqualTo(size));
            }
            Assert.That(ModPackageBuilder.MaxArchiveBytes, Is.EqualTo(4L * 1024 * 1024 * 1024 - 1));
        }

        [Test]
        public void Build_WithoutIcon_FailsWithoutWritingAnArchive()
        {
            var mod = new ModData { Name = "No icon" };
            string eemPath = directory.Combine("no-icon" + EemFormat.Extension);

            using (var builder = new ModPackageBuilder(mod, new ModAssets(), directory.Combine("workspace"), true))
            {
                Assert.That(() => builder.Build(eemPath), Throws.TypeOf<InvalidOperationException>());
            }

            Assert.That(eemPath, Does.Not.Exist);
        }

        [TestCase(null, "1.0", TestName = "Build_WithoutName_FailsWithoutWritingAnArchive")]
        [TestCase("  ", "1.0", TestName = "Build_WithBlankName_FailsWithoutWritingAnArchive")]
        [TestCase("Named", null, TestName = "Build_WithoutVersion_FailsWithoutWritingAnArchive")]
        public void Build_IncompleteMod_FailsWithoutWritingAnArchive(string name, string version)
        {
            // The mod creator validates its first page, but the library must not rely on it (the page could
            // be skipped through the tab headers before that was fixed).
            var mod = new ModData { Name = name, Version = version == null ? null : new Version(version) };
            var assets = new ModAssets { Icon = CreateImage(ModImageRules.IconSize, ModImageRules.IconSize, Color.Red) };
            string eemPath = directory.Combine("incomplete" + EemFormat.Extension);

            using (var builder = new ModPackageBuilder(mod, assets, directory.Combine("workspace"), true))
            {
                Assert.That(() => builder.Build(eemPath), Throws.TypeOf<InvalidOperationException>());
            }

            Assert.That(eemPath, Does.Not.Exist);
        }

        [Test]
        public void ExportToZip_ExistingArchive_IsReplaced()
        {
            string eemPath = directory.Combine("mod" + EemFormat.Extension);
            ExportWithoutImages(new ModData { Name = "First", Version = new Version(1, 0) }, eemPath);

            ExportWithoutImages(new ModData { Name = "Second", Version = new Version(2, 0) }, eemPath);

            ModData read = ModArchiveReader.ReadModData(eemPath);
            Assert.That(read.Name, Is.EqualTo("Second"));
            Assert.That(read.Version, Is.EqualTo(new Version(2, 0)));
            // Only the archive is left: no temporary file, no working directories (erased on Dispose).
            Assert.That(Directory.GetFiles(directory.Path), Is.EqualTo(new[] { eemPath }));
            Assert.That(Directory.GetFileSystemEntries(directory.Combine("workspace")), Is.Empty);
        }

        /* Reader errors */

        [Test]
        public void ReadModData_StreamIsNotClosed()
        {
            var mod = new ModData { Name = "Stream", Version = new Version(1, 0) };
            using (MemoryStream archive = CreateArchive(Entry(EemFormat.DataEntryName, mod.ToString())))
            {
                ModData read = ModArchiveReader.ReadModData(archive);

                Assert.That(read.Name, Is.EqualTo("Stream"));
                Assert.That(archive.CanRead, Is.True);
            }
        }

        [Test]
        public void ReadModData_DataEntryInAFolder_IsRejected()
        {
            // The old export wrote "creator/data", which the import never found.
            string json = new ModData { Name = "Old" }.ToString();
            using (MemoryStream archive = CreateArchive(Entry("creator/" + EemFormat.DataEntryName, json)))
            {
                Assert.That(() => ModArchiveReader.ReadModData(archive), Throws.TypeOf<InvalidDataException>());
            }
        }

        [TestCase("", TestName = "ReadModData_EmptyDataEntry_ThrowsInvalidData")]
        [TestCase("not json", TestName = "ReadModData_DataEntryNotJson_ThrowsInvalidData")]
        [TestCase("{\"version\":\"1.x\"}", TestName = "ReadModData_InvalidVersion_ThrowsInvalidData")]
        public void ReadModData_InvalidDataEntry_ThrowsInvalidData(string data)
        {
            using (MemoryStream archive = CreateArchive(Entry(EemFormat.DataEntryName, data)))
            {
                Assert.That(() => ModArchiveReader.ReadModData(archive), Throws.TypeOf<InvalidDataException>());
            }
        }

        /// <summary>
        /// An archive whose "data" entry declares more uncompressed bytes in the central directory than it
        /// contains. The vendored ZipStorer used to loop forever on such an entry (bugs-security review).
        /// </summary>
        private static MemoryStream CreateArchiveWithOversizedDataEntry(ZipStorer.Compression method)
        {
            var stream = new MemoryStream();
            using (ZipStorer zip = ZipStorer.Create(stream, string.Empty, true))
            {
                zip.ForceDeflating = true;
                string json = new ModData { Name = "Truncated", Version = new Version(1, 0) }.ToString();
                using (var content = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                {
                    zip.AddStream(method, EemFormat.DataEntryName, content, DateTime.Now);
                }
            }

            byte[] bytes = stream.ToArray();
            int patched = 0;
            for (int i = 0; i + 28 <= bytes.Length; i++)
            {
                // Central directory file header: signature 0x02014b50, uncompressed size at offset 24.
                if (BitConverter.ToUInt32(bytes, i) != 0x02014b50)
                    continue;
                BitConverter.GetBytes(100000u).CopyTo(bytes, i + 24);
                patched++;
            }
            Assert.That(patched, Is.EqualTo(1), "central directory entries");
            return new MemoryStream(bytes);
        }

        /// <summary>
        /// Runs <paramref name="action"/> on a background thread and fails the test if it does not finish in
        /// time. NUnit's [Timeout] cannot interrupt a tight loop on every runtime, so a regression would hang
        /// the whole test run instead of failing this test.
        /// </summary>
        private static Exception RunWithDeadline(Action action)
        {
            Exception thrown = null;
            var thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    thrown = ex;
                }
            }) { IsBackground = true };
            thread.Start();
            if (!thread.Join(TimeSpan.FromSeconds(20)))
                Assert.Fail("Reading the archive did not finish (endless loop on a truncated entry).");
            return thrown;
        }

        [TestCase(ZipStorer.Compression.Deflate, TestName = "ReadModData_DeflatedEntryShorterThanDeclared_ThrowsInvalidData")]
        [TestCase(ZipStorer.Compression.Store, TestName = "ReadModData_StoredEntryShorterThanDeclared_ThrowsInvalidData")]
        public void ReadModData_EntryShorterThanDeclared_ThrowsInvalidData(ZipStorer.Compression method)
        {
            using (MemoryStream archive = CreateArchiveWithOversizedDataEntry(method))
            {
                Exception thrown = RunWithDeadline(() => ModArchiveReader.ReadModData(archive));

                Assert.That(thrown, Is.TypeOf<InvalidDataException>());
            }
        }

        [Test]
        public void ModManager_ArchiveWithOversizedEntry_IsReportedInsteadOfHanging()
        {
            string mods = Directory.CreateDirectory(directory.Combine("mods")).FullName;
            using (MemoryStream archive = CreateArchiveWithOversizedDataEntry(ZipStorer.Compression.Deflate))
            {
                File.WriteAllBytes(Path.Combine(mods, "crafted" + EemFormat.Extension), archive.ToArray());
            }
            var manager = new ModManager(mods);
            bool loadedEverything = true;

            Exception thrown = RunWithDeadline(() => loadedEverything = manager.Init());

            Assert.That(thrown, Is.Null);
            Assert.That(loadedEverything, Is.False);
            Assert.That(manager.Mods, Is.Empty);
            Assert.That(manager.LoadErrors.Count, Is.EqualTo(1));
        }

        [Test]
        public void ReadModData_MissingDataEntry_ThrowsInvalidData()
        {
            using (MemoryStream archive = CreateArchive(Entry(EemFormat.IconEntryName, "png")))
            {
                Assert.That(() => ModArchiveReader.ReadModData(archive), Throws.TypeOf<InvalidDataException>());
            }
        }

        /// <summary>
        /// The file list of a mod comes from its author. A path that leaves the product folder would send the code that
        /// installs the files anywhere on the disk (path traversal, "zip slip"), so the reader rejects the archive.
        /// </summary>
        [TestCase(@"EEC\..\..\..\Windows\System32\evil.dll", TestName = "ReadModData_FilePathWithParentFolders_IsRejected")]
        [TestCase(@"C:\Windows\System32\evil.dll", TestName = "ReadModData_AbsoluteFilePath_IsRejected")]
        [TestCase("/EEC/Data/units.xml", TestName = "ReadModData_FilePathFromTheRoot_IsRejected")]
        [TestCase("Data/units.xml", TestName = "ReadModData_FilePathOutsideTheProductFolders_IsRejected")]
        public void ReadModData_UnsafeFilePath_IsRejected(string relativePath)
        {
            var mod = new ModData { Name = "Unsafe", Version = new Version(1, 0) };
            mod.ModFiles.Add(new ModFile(@"EEC\Data\units.xml", ModFile.ModFileType.ConfigFile, Guid.Empty, string.Empty));
            mod.ModFiles.Add(new ModFile(relativePath, ModFile.ModFileType.Executable, Guid.Empty, string.Empty));

            using (MemoryStream archive = CreateArchive(Entry(EemFormat.DataEntryName, mod.ToString())))
            {
                Assert.That(() => ModArchiveReader.ReadModData(archive),
                    Throws.TypeOf<InvalidDataException>().With.Message.Contains(relativePath));
            }
        }

        [Test]
        public void ReadModData_EmptyEntryInTheFileList_IsRejected()
        {
            var mod = new ModData { Name = "Empty entry", Version = new Version(1, 0) };
            mod.ModFiles.Add(null);

            using (MemoryStream archive = CreateArchive(Entry(EemFormat.DataEntryName, mod.ToString())))
            {
                Assert.That(() => ModArchiveReader.ReadModData(archive), Throws.TypeOf<InvalidDataException>());
            }
        }

        [Test]
        public void ReadModData_FilePathsOfTheModCreator_AreRead()
        {
            var mod = new ModData { Name = "Paths", Version = new Version(1, 0) };
            mod.ModFiles.Add(new ModFile(@"EEC\Data\units.xml", ModFile.ModFileType.ConfigFile, Guid.Empty, string.Empty));
            mod.ModFiles.Add(new ModFile("all/Data/Textures/grass.tga", ModFile.ModFileType.Data, Guid.Empty, string.Empty));

            using (MemoryStream archive = CreateArchive(Entry(EemFormat.DataEntryName, mod.ToString())))
            {
                Assert.That(ModArchiveReader.ReadModData(archive).ModFiles.Select(file => file.RelativeFilePath),
                    Is.EqualTo(new[] { @"EEC\Data\units.xml", "all/Data/Textures/grass.tga" }));
            }
        }

        [TestCase("not a zip archive, but longer than the end record of one", TestName = "ReadModData_TextFile_SaysItIsNoZipArchive")]
        [TestCase("PK", TestName = "ReadModData_FileShorterThanAnEndRecord_SaysItIsNoZipArchive")]
        public void ReadModData_NoZipArchive_SaysSo(string content)
        {
            // ZipStorer's own exception has no message of its own: "Found invalid data while decoding."
            using (var archive = new MemoryStream(Encoding.UTF8.GetBytes(content)))
            {
                Assert.That(() => ModArchiveReader.ReadModData(archive),
                    Throws.TypeOf<InvalidDataException>().With.Message.Contains("not a ZIP archive"));
            }
        }

        /// <summary>
        /// A damaged record in the central directory: a time such as 31:00, a name longer than the directory. The vendored
        /// ZipStorer threw ArgumentOutOfRangeException for both, not the documented InvalidDataException.
        /// </summary>
        [TestCase(12, 0xF800, TestName = "ReadModData_InvalidTimeInTheCentralDirectory_ThrowsInvalidData")]
        [TestCase(28, 0xFFFF, TestName = "ReadModData_NameLongerThanTheCentralDirectory_ThrowsInvalidData")]
        public void ReadModData_DamagedCentralDirectory_ThrowsInvalidData(int offset, int value)
        {
            string json = new ModData { Name = "Damaged", Version = new Version(1, 0) }.ToString();
            byte[] bytes;
            using (MemoryStream archive = CreateArchive(Entry(EemFormat.DataEntryName, json)))
            {
                bytes = archive.ToArray();
            }
            // Central directory file header: signature 0x02014b50, time at offset 12, length of the name at 28.
            List<int> records = Enumerable.Range(0, bytes.Length - 3)
                .Where(i => BitConverter.ToUInt32(bytes, i) == 0x02014b50).ToList();
            Assert.That(records.Count, Is.EqualTo(1), "central directory entries");
            BitConverter.GetBytes((ushort)value).CopyTo(bytes, records[0] + offset);

            using (var damaged = new MemoryStream(bytes))
            {
                Assert.That(() => ModArchiveReader.ReadModData(damaged),
                    Throws.TypeOf<InvalidDataException>().With.Message.Contains("directory of the ZIP archive is damaged"));
            }
        }

        /* ModManager */

        [Test]
        public void ModManager_LoadsValidArchivesAndReportsBrokenOnes()
        {
            string mods = Directory.CreateDirectory(directory.Combine("mods")).FullName;
            ExportWithoutImages(new ModData { Name = "Valid", Version = new Version(1, 0) },
                Path.Combine(mods, "valid" + EemFormat.Extension));
            File.WriteAllText(Path.Combine(mods, "broken" + EemFormat.Extension), "not a zip archive");
            File.WriteAllText(Path.Combine(mods, "notes.txt"), "not a mod");

            var manager = new ModManager(mods);

            Assert.That(manager.Init(), Is.False);
            Assert.That(manager.Mods.Select(m => m.Name), Is.EqualTo(new[] { "Valid" }));
            Assert.That(manager.LoadErrors.Count, Is.EqualTo(1));
            Assert.That(manager.LoadErrors[0], Does.StartWith("broken" + EemFormat.Extension + ": "));
        }

        [Test]
        public void ModManager_InitAgain_StartsFromScratch()
        {
            string mods = Directory.CreateDirectory(directory.Combine("mods")).FullName;
            string broken = Path.Combine(mods, "broken" + EemFormat.Extension);
            File.WriteAllText(broken, "not a zip archive");
            var manager = new ModManager(mods);
            Assert.That(manager.Init(), Is.False);

            File.Delete(broken);
            ExportWithoutImages(new ModData { Name = "Valid" }, Path.Combine(mods, "valid" + EemFormat.Extension));

            Assert.That(manager.Init(), Is.True);
            Assert.That(manager.Mods.Count, Is.EqualTo(1));
            Assert.That(manager.LoadErrors, Is.Empty);
        }

        [Test]
        public void ModManager_MissingDirectory_Throws()
        {
            Assert.That(() => new ModManager(directory.Combine("missing")), Throws.TypeOf<DirectoryNotFoundException>());
        }
    }
}

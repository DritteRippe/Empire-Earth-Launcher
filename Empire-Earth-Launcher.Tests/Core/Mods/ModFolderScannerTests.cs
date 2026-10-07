using System.Linq;
using System.Text;
using Empire_Earth_Launcher.Core.Diagnostics;
using Empire_Earth_Launcher.Core.Mods;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Tests.Fakes;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.Mods
{
    /// <summary>
    /// <see cref="ModFolderScanner"/>: the presets of <c>Data\dxm\mods</c>, with the head of their <c>CREDITS</c> file and their
    /// size. Synthetic folders and texts, never a copy of the data of dreXmod.
    /// </summary>
    [TestFixture]
    public class ModFolderScannerTests
    {
        private const string GameFolder = @"C:\Program Files (x86)\Empire Earth\Empire Earth";
        private const string ModsFolder = GameFolder + @"\Data\dxm\mods";

        private InMemoryFileSystem fileSystem;

        [SetUp]
        public void SetUp()
        {
            fileSystem = new InMemoryFileSystem();
        }

        private void AddPreset(string name, string credits, params int[] fileSizes)
        {
            fileSystem.AddDirectory(ModsFolder + @"\" + name);
            if (credits != null)
                fileSystem.AddFile(ModsFolder + @"\" + name + @"\CREDITS", credits);
            for (int i = 0; i < fileSizes.Length; i++)
                fileSystem.AddFile(ModsFolder + @"\" + name + @"\textures\file" + i + ".sst", new byte[fileSizes[i]]);
        }

        [Test]
        public void TheModsFolder_IsBelowTheGameFolder()
        {
            Assert.That(ModFolderScanner.ModsFolder(GameFolder), Is.EqualTo(ModsFolder));
        }

        [Test]
        public void EveryFolder_IsAPreset_ByNameIgnoringCase()
        {
            AddPreset("yukon", "Name: yukon\r\nLast Edit: 21/10/2023\r\nCreated by: somebody\r\n", 100);
            AddPreset("Dxm", "Name: dxm (sample)\r\n", 10);
            AddPreset("energycube", null);

            ModFolderScan scan = ModFolderScanner.Scan(fileSystem, ModsFolder);

            Assert.That(scan.Status, Is.EqualTo(ConfigFileStatus.Read));
            Assert.That(scan.Presets.Select(preset => preset.FolderName), Is.EqualTo(new[] { "Dxm", "energycube", "yukon" }));
        }

        [Test]
        public void ThePresetsCredits_AreRead_AndTheFolderNameIsTheFallback()
        {
            AddPreset("yukon", "Name: yukon\r\nLast Edit: 21/10/2023\r\nCreated by: somebody\r\n");
            AddPreset("dxm", "Name: dxm (sample)\r\n");
            AddPreset("mine", null);

            ModFolderScan scan = ModFolderScanner.Scan(fileSystem, ModsFolder);
            ModPreset yukon = scan.Presets.Single(preset => preset.FolderName == "yukon");
            ModPreset dxm = scan.Presets.Single(preset => preset.FolderName == "dxm");
            ModPreset mine = scan.Presets.Single(preset => preset.FolderName == "mine");

            Assert.That(yukon.Credits.CreatedBy, Is.EqualTo("somebody"));
            Assert.That(yukon.CreditsName, Is.Null, "the name is the folder name");
            Assert.That(dxm.CreditsName, Is.EqualTo("dxm (sample)"));
            Assert.That(mine.Credits, Is.Null, "a self-made folder without CREDITS is a preset as well");
            Assert.That(mine.CreditsName, Is.Null);
            Assert.That(mine.IsTemplate, Is.False);
        }

        [Test]
        public void TheTemplate_IsFlagged()
        {
            AddPreset("template", "Name: x\r\nLast Edit: XX/XX/XXXX\r\n");
            AddPreset("Template2", null);

            ModFolderScan scan = ModFolderScanner.Scan(fileSystem, ModsFolder);

            Assert.That(scan.Presets.Single(preset => preset.FolderName == "template").IsTemplate, Is.True);
            Assert.That(scan.Presets.Single(preset => preset.FolderName == "Template2").IsTemplate, Is.False);
        }

        [Test]
        public void TheSize_IsTheSumOfEveryFileBelowTheFolder()
        {
            AddPreset("yukon", "Name: yukon\r\n", 1000, 2000);
            fileSystem.AddFile(ModsFolder + @"\yukon\WONLobby Resources\Images\a\b\c.png", new byte[500]);
            long credits = Encoding.UTF8.GetByteCount("Name: yukon\r\n");

            ModPreset preset = ModFolderScanner.Scan(fileSystem, ModsFolder).Presets.Single();

            Assert.That(preset.SizeBytes, Is.EqualTo(3500 + credits));
            Assert.That(preset.IsSizeComplete, Is.True);
        }

        [Test]
        public void ALooseFileInTheModsFolder_IsNoPreset()
        {
            AddPreset("yukon", null);
            fileSystem.AddFile(ModsFolder + @"\notes.txt", "a note of the player");

            ModFolderScan scan = ModFolderScanner.Scan(fileSystem, ModsFolder);

            Assert.That(scan.Presets.Select(preset => preset.FolderName), Is.EqualTo(new[] { "yukon" }));
        }

        [Test]
        public void WithoutTheFolder_TheScanIsMissing_AndEmpty()
        {
            fileSystem.AddDirectory(GameFolder);

            ModFolderScan scan = ModFolderScanner.Scan(fileSystem, ModsFolder);

            Assert.That(scan.Status, Is.EqualTo(ConfigFileStatus.Missing));
            Assert.That(scan.Presets, Is.Empty);
        }

        [Test]
        public void AnEmptyFolder_IsReadWithoutPresets()
        {
            fileSystem.AddDirectory(ModsFolder);

            ModFolderScan scan = ModFolderScanner.Scan(fileSystem, ModsFolder);

            Assert.That(scan.Status, Is.EqualTo(ConfigFileStatus.Read));
            Assert.That(scan.Presets, Is.Empty);
        }

        [Test]
        public void AFolderThatCannotBeListed_IsUnreadable_WithTheReason()
        {
            AddPreset("yukon", null);
            fileSystem.FailOn(ModsFolder, FileSystemOperation.Enumerate, FileSystemStatus.AccessDenied);

            ModFolderScan scan = ModFolderScanner.Scan(fileSystem, ModsFolder);

            Assert.That(scan.Status, Is.EqualTo(ConfigFileStatus.Unreadable));
            Assert.That(scan.Problem, Does.Contain("AccessDenied"));
            Assert.That(scan.Presets, Is.Empty);
        }

        [Test]
        public void ACreditsFileThatCannotBeRead_LeavesThePresetWithoutCredits_NotAnError()
        {
            AddPreset("yukon", "Name: yukon\r\n", 10);
            fileSystem.FailOn(ModsFolder + @"\yukon\CREDITS", FileSystemOperation.Read, FileSystemStatus.IoError);

            ModPreset preset = ModFolderScanner.Scan(fileSystem, ModsFolder).Presets.Single();

            Assert.That(preset.Credits, Is.Null);
            Assert.That(preset.SizeBytes, Is.GreaterThanOrEqualTo(10));
        }

        [Test]
        public void ACreditsFileThatIsTooLarge_IsNotRead()
        {
            AddPreset("yukon", null);
            fileSystem.AddFile(ModsFolder + @"\yukon\CREDITS", new byte[(int)ModFolderScanner.MaxCreditsBytes + 1]);

            Assert.That(ModFolderScanner.Scan(fileSystem, ModsFolder).Presets.Single().Credits, Is.Null);
        }

        [Test]
        public void AFolderWithMoreFilesThanTheScanFollows_HasAnIncompleteSize()
        {
            fileSystem.AddDirectory(ModsFolder + @"\big");
            for (int i = 0; i <= ModFolderScanner.MaxFiles; i++)
                fileSystem.AddFile(ModsFolder + @"\big\f" + i, new byte[1]);

            ModPreset preset = ModFolderScanner.Scan(fileSystem, ModsFolder).Presets.Single();

            Assert.That(preset.IsSizeComplete, Is.False);
            Assert.That(preset.SizeBytes, Is.EqualTo(ModFolderScanner.MaxFiles));
        }

        [Test]
        public void AFolderNestedDeeperThanTheScanFollows_HasAnIncompleteSize()
        {
            string path = ModsFolder + @"\deep";
            for (int level = 0; level < ModFolderScanner.MaxDepth + 2; level++)
                path += @"\d" + level;
            fileSystem.AddFile(path + @"\file.bin", new byte[7]);

            Assert.That(ModFolderScanner.Scan(fileSystem, ModsFolder).Presets.Single().IsSizeComplete, Is.False);
        }

        [Test]
        public void TheScan_NeverWrites()
        {
            AddPreset("yukon", "Name: yukon\r\n", 5);
            var guarded = new WriteForbiddingFileSystem(fileSystem);

            Assert.That(ModFolderScanner.Scan(guarded, ModsFolder).Presets, Has.Count.EqualTo(1));
        }
    }
}

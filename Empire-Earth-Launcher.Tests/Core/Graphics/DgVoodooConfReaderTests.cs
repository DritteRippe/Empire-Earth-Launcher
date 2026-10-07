using System.Linq;
using Empire_Earth_Launcher.Core.Diagnostics;
using Empire_Earth_Launcher.Core.Graphics;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Tests.Fakes;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.Graphics
{
    /// <summary>
    /// <see cref="DgVoodooConf"/> and <see cref="DgVoodooConfReader"/>: the launcher of 1.1.0 only reads <c>dgVoodoo.conf</c> and
    /// shows the keys that decide the screen mode. The texts are synthetic (written here, laid out like the file the setup
    /// installs), never a copy of a real file.
    /// </summary>
    [TestFixture]
    public class DgVoodooConfReaderTests
    {
        private const string GameFolder = @"C:\Program Files (x86)\Empire Earth\Empire Earth";
        private const string Conf = GameFolder + @"\dgVoodoo.conf";
        private const string VirtualStoreFolder = @"C:\Users\Player\AppData\Local\VirtualStore";
        private const string VirtualStoreConf = VirtualStoreFolder + @"\Program Files (x86)\Empire Earth\Empire Earth\dgVoodoo.conf";

        private const string Sample =
            "Version                              = 0x282\r\n" +
            "\r\n" +
            "[General]\r\n" +
            "OutputAPI                            = d3d11_fl10_1\r\n" +
            "FullScreenMode                       = true\r\n" +
            "\r\n" +
            "[DirectX]\r\n" +
            "dgVoodooWatermark                   = false\r\n" +
            "; Antialiasing                        = 2x\r\n" +
            "AppControlledScreenMode             = true\r\n" +
            "DisableAltEnterToToggleScreenMode   = false\r\n" +
            "\r\n" +
            "[GeneralExt]\r\n" +
            "WindowedAttributes\t\t\t= Borderless, AlwaysOnTop, FullscreenSize";

        // --- The parser ----------------------------------------------------------------------------------------------

        [Test]
        public void Parse_ReadsKeysWithTheirSections()
        {
            DgVoodooConf conf = DgVoodooConf.Parse(Sample);

            Assert.That(conf.Find("OutputAPI").Value, Is.EqualTo("d3d11_fl10_1"));
            Assert.That(conf.Find("OutputAPI").Section, Is.EqualTo("General"));
            Assert.That(conf.Find("AppControlledScreenMode", "DirectX").Value, Is.EqualTo("true"));
            Assert.That(conf.Find("Version").Section, Is.EqualTo(string.Empty), "a line before the first section");
            Assert.That(conf.Find("Version").Value, Is.EqualTo("0x282"));
        }

        [Test]
        public void Parse_ToleratesTabsAndAMissingTrailingNewline()
        {
            DgVoodooConf conf = DgVoodooConf.Parse(Sample);

            Assert.That(conf.Find("WindowedAttributes").Value, Is.EqualTo("Borderless, AlwaysOnTop, FullscreenSize"));
        }

        [TestCase("\r\n")]
        [TestCase("\n")]
        [TestCase("\r")]
        public void Parse_ToleratesEveryLineEnd(string lineEnd)
        {
            DgVoodooConf conf = DgVoodooConf.Parse(Sample.Replace("\r\n", lineEnd));

            Assert.That(conf.Entries, Has.Count.EqualTo(DgVoodooConf.Parse(Sample).Entries.Count));
            Assert.That(conf.Find("FullScreenMode").Value, Is.EqualTo("true"));
        }

        [Test]
        public void Parse_SkipsCommentsAndLinesWithoutAKey()
        {
            DgVoodooConf conf = DgVoodooConf.Parse("; a = b\r\n[General]\r\njust text\r\n= value\r\n   \r\nKey = v\r\n");

            Assert.That(conf.Entries.Select(entry => entry.ToString()), Is.EqualTo(new[] { "[General] Key = v" }));
        }

        [Test]
        public void Parse_KeysAndSectionsIgnoreCase()
        {
            DgVoodooConf conf = DgVoodooConf.Parse("[GENERAL]\r\nfullscreenmode = false\r\n");

            Assert.That(conf.Find("FullScreenMode", "General").Value, Is.EqualTo("false"));
        }

        [Test]
        public void Parse_AMissingKeyIsNull_AndANullTextIsEmpty()
        {
            Assert.That(DgVoodooConf.Parse(Sample).Find("CaptureMouse"), Is.Null);
            Assert.That(DgVoodooConf.Parse(null).Entries, Is.Empty);
            Assert.That(DgVoodooConf.Parse(string.Empty).Find("OutputAPI"), Is.Null);
        }

        [Test]
        public void Parse_AKeyInAnotherSectionIsNotFoundThere()
        {
            Assert.That(DgVoodooConf.Parse(Sample).Find("OutputAPI", "DirectX"), Is.Null);
        }

        [Test]
        public void Parse_TheLastLineOfAKeyCounts()
        {
            Assert.That(DgVoodooConf.Parse("[General]\r\nFullScreenMode = true\r\nFullScreenMode = false\r\n").Find("FullScreenMode").Value,
                Is.EqualTo("false"));
        }

        [Test]
        public void Parse_AKeyWithoutAValueHasAnEmptyValue()
        {
            Assert.That(DgVoodooConf.Parse("[General]\r\nWindowedAttributes =\r\n").Find("WindowedAttributes").Value, Is.EqualTo(string.Empty));
        }

        [Test]
        public void Parse_AByteOrderMarkInFrontOfTheFirstLineIsIgnored()
        {
            Assert.That(DgVoodooConf.Parse("\uFEFF[General]\r\nOutputAPI = d3d12_fl12_0\r\n").Find("OutputAPI").Value, Is.EqualTo("d3d12_fl12_0"));
        }

        [Test]
        public void TheScreenModeKeys_DoNotIncludeTheOutputApi()
        {
            Assert.That(DgVoodooConf.ScreenModeKeys, Does.Contain("FullScreenMode").And.Contain("AppControlledScreenMode").And.Contain("FullscreenAttributes"));
            Assert.That(DgVoodooConf.ScreenModeKeys, Does.Not.Contain(DgVoodooConf.OutputApiKey));
        }

        // --- The reader ----------------------------------------------------------------------------------------------

        private static InMemoryFileSystem FileSystem()
        {
            var fileSystem = new InMemoryFileSystem();
            fileSystem.AddDrive("C:");
            fileSystem.AddDirectory(GameFolder);
            return fileSystem;
        }

        private static EffectivePathResolver Resolver(InMemoryFileSystem fileSystem)
        {
            return new EffectivePathResolver(fileSystem, VirtualStoreFolder, new[] { @"C:\Program Files", @"C:\Program Files (x86)" });
        }

        [Test]
        public void Read_TheFileInTheGameFolder()
        {
            InMemoryFileSystem fileSystem = FileSystem();
            fileSystem.AddFile(Conf, Sample);

            DgVoodooConfFile file = DgVoodooConfReader.Read(fileSystem, Resolver(fileSystem), GameFolder);

            Assert.That(file.Status, Is.EqualTo(ConfigFileStatus.Read));
            Assert.That(file.Path, Is.EqualTo(Conf));
            Assert.That(file.IsVirtualStoreCopy, Is.False);
            Assert.That(file.Conf.Find("OutputAPI").Value, Is.EqualTo("d3d11_fl10_1"));
        }

        [Test]
        public void Read_PrefersTheVirtualStoreCopyTheGameReads()
        {
            InMemoryFileSystem fileSystem = FileSystem();
            fileSystem.AddFile(Conf, Sample);
            fileSystem.AddFile(VirtualStoreConf, "[General]\r\nOutputAPI = d3d11_fl11_0\r\nFullScreenMode = false\r\n");

            DgVoodooConfFile file = DgVoodooConfReader.Read(fileSystem, Resolver(fileSystem), GameFolder);

            Assert.That(file.IsVirtualStoreCopy, Is.True);
            Assert.That(file.Path, Is.EqualTo(VirtualStoreConf));
            Assert.That(file.Conf.Find("OutputAPI").Value, Is.EqualTo("d3d11_fl11_0"));
        }

        [Test]
        public void Read_AMissingFile_IsMissing_WithoutAConf()
        {
            InMemoryFileSystem fileSystem = FileSystem();

            DgVoodooConfFile file = DgVoodooConfReader.Read(fileSystem, Resolver(fileSystem), GameFolder);

            Assert.That(file.Status, Is.EqualTo(ConfigFileStatus.Missing));
            Assert.That(file.Conf, Is.Null);
        }

        [Test]
        public void Read_AFileThatCannotBeRead_IsUnreadable_WithTheProblem()
        {
            InMemoryFileSystem fileSystem = FileSystem();
            fileSystem.AddFile(Conf, new byte[KeyValueFile.MaxBytes + 1]);

            DgVoodooConfFile file = DgVoodooConfReader.Read(fileSystem, Resolver(fileSystem), GameFolder);

            Assert.That(file.Status, Is.EqualTo(ConfigFileStatus.Unreadable));
            Assert.That(file.Problem, Does.Contain("TooLarge"));
            Assert.That(file.Conf, Is.Null);
        }

        [Test]
        public void Read_NeverWritesAnything()
        {
            InMemoryFileSystem fileSystem = FileSystem();
            fileSystem.AddFile(Conf, Sample);
            byte[] before = fileSystem.GetContent(Conf);
            var guard = new WriteForbiddingFileSystem(fileSystem);

            DgVoodooConfReader.Read(guard, Resolver(fileSystem), GameFolder);

            Assert.That(fileSystem.GetContent(Conf), Is.EqualTo(before));
        }
    }
}

using System.Text;
using Empire_Earth_Launcher.Core.Diagnostics;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Mods;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Tests.Fakes;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.Mods
{
    /// <summary>
    /// <see cref="DreXmodConfig"/> and <see cref="DreXmodConfigReader"/>: the launcher of 1.1.0 only reads <c>dreXmod.config</c>
    /// to show which preset is the active mod and the active lobby theme. The texts are synthetic (written here, laid out like
    /// the file of dreXmod 3: a comment before the first element, tabs, CRLF, comments with element names in them), never a copy
    /// of a real file.
    /// </summary>
    [TestFixture]
    public class DreXmodConfigReaderTests
    {
        private const string GameFolder = @"C:\Program Files (x86)\Empire Earth\Empire Earth";
        private const string ConfigPath = GameFolder + @"\dreXmod.config";
        private const string VirtualStoreFolder = @"C:\Users\Player\AppData\Local\VirtualStore";
        private const string VirtualStoreConfig = VirtualStoreFolder + @"\Program Files (x86)\Empire Earth\Empire Earth\dreXmod.config";

        private static string Config(string mod = "0", string modName = "dxm", string lobby = "1", string lobbyName = "dxm",
            string analytics = "1")
        {
            return string.Join("\r\n", new[]
            {
                "<!--",
                "#################################################",
                "|        Configuration file of a sample          |",
                "#################################################",
                "-->",
                "",
                "<config>",
                "\t<Drexmod>",
                "\t\t<Enabled>1</Enabled>",
                "\t\t<GoogleAnalytics>",
                "\t\t\t<SendViews>" + analytics + "</SendViews>",
                "\t\t</GoogleAnalytics>",
                "\t</Drexmod>",
                "",
                "\t<Camera>",
                "\t\t<Enabled>1</Enabled>",
                "\t\t<Zoom>",
                "\t\t\t<Style>2</Style>\t<!-- Zoom Style: 1=Normal, 2= Isometric -->",
                "\t\t</Zoom>",
                "\t</Camera>",
                "",
                "\t<Mod>",
                "\t\t<Enabled>" + mod + "</Enabled>",
                "",
                "\t\t<!--",
                "\t\t\tAccepted default values: 'dxm', 'yukon' or 'energycube'",
                "\t\t\t<Name>this one is a comment</Name>",
                "\t\t-->",
                "\t\t<Name>" + modName + "</Name>",
                "\t</Mod>",
                "",
                "\t<LobbyTheme>",
                "\t\t<Enabled>" + lobby + "</Enabled>",
                "\t\t<Name>" + lobbyName + "</Name>",
                "\t\t<Icons AutoDetect=\"1\" InLobbyIconSize=\"24\" InGameIconSize=\"32\"/>",
                "\t</LobbyTheme>",
                "</config>",
                ""
            });
        }

        // --- The parser ----------------------------------------------------------------------------------------------

        [Test]
        public void TheShippedDefault_ModOff_LobbyThemeOn()
        {
            DreXmodConfig config = DreXmodConfig.Parse(Config());

            Assert.That(config.HasModSystem, Is.True);
            Assert.That(config.Mod.IsPresent, Is.True);
            Assert.That(config.Mod.Enabled, Is.False);
            Assert.That(config.Mod.Name, Is.EqualTo("dxm"));
            Assert.That(config.LobbyTheme.Enabled, Is.True);
            Assert.That(config.LobbyTheme.Name, Is.EqualTo("dxm"));
            Assert.That(config.Mod.Selects("dxm"), Is.False, "the mod is off");
            Assert.That(config.LobbyTheme.Selects("dxm"), Is.True);
        }

        [Test]
        public void TheTwoSelectors_AreIndependent()
        {
            DreXmodConfig config = DreXmodConfig.Parse(Config(mod: "1", modName: "yukon", lobby: "1", lobbyName: "energycube"));

            Assert.That(config.Mod.Selects("yukon"), Is.True);
            Assert.That(config.Mod.Selects("energycube"), Is.False);
            Assert.That(config.LobbyTheme.Selects("energycube"), Is.True);
            Assert.That(config.LobbyTheme.Selects("yukon"), Is.False);
        }

        [Test]
        public void TheNameInAComment_IsNotTheName()
        {
            DreXmodConfig config = DreXmodConfig.Parse(Config(modName: "yukon", mod: "1"));

            Assert.That(config.Mod.Name, Is.EqualTo("yukon"));
        }

        [Test]
        public void ThePrivacyVariant_DiffersOnlyInTheAnalyticsValues_AndReadsTheSame()
        {
            DreXmodConfig normal = DreXmodConfig.Parse(Config(mod: "1", modName: "yukon", analytics: "1"));
            DreXmodConfig privacy = DreXmodConfig.Parse(Config(mod: "1", modName: "yukon", analytics: "0"));

            Assert.That(privacy.Mod.Name, Is.EqualTo(normal.Mod.Name));
            Assert.That(privacy.Mod.Enabled, Is.EqualTo(normal.Mod.Enabled));
            Assert.That(privacy.LobbyTheme.Name, Is.EqualTo(normal.LobbyTheme.Name));
        }

        [TestCase("\r\n")]
        [TestCase("\n")]
        [TestCase("\r")]
        public void EveryLineEnd_IsRead(string lineEnd)
        {
            DreXmodConfig config = DreXmodConfig.Parse(Config(mod: "1", modName: "yukon").Replace("\r\n", lineEnd));

            Assert.That(config.Mod.Selects("yukon"), Is.True);
            Assert.That(config.LobbyTheme.Selects("dxm"), Is.True);
        }

        [Test]
        public void Spaces_TabsAndLineBreaksAroundValues_AreIgnored()
        {
            DreXmodConfig config = DreXmodConfig.Parse(
                "<config><Mod>\r\n\t<Enabled>\t1 </Enabled>\r\n\t<Name>\r\n\t\t Yukon \r\n\t</Name>\r\n</Mod></config>");

            Assert.That(config.Mod.Enabled, Is.True);
            Assert.That(config.Mod.Name, Is.EqualTo("Yukon"));
        }

        [Test]
        public void TheTags_AreReadIgnoringCase()
        {
            DreXmodConfig config = DreXmodConfig.Parse(
                "<CONFIG><mod><ENABLED>1</enabled><name>yukon</NAME></mod><lobbytheme><enabled>0</enabled><name>dxm</name></lobbytheme></CONFIG>");

            Assert.That(config.Mod.Selects("yukon"), Is.True);
            Assert.That(config.LobbyTheme.Enabled, Is.False);
        }

        [TestCase("1", true)]
        [TestCase("true", true)]
        [TestCase("TRUE", true)]
        [TestCase("0", false)]
        [TestCase("false", false)]
        [TestCase("yes", null)]
        [TestCase("", null)]
        [TestCase("2", null)]
        public void Enabled_IsOneOrTrue_ZeroOrFalse_AndUnknownOtherwise(string text, bool? expected)
        {
            DreXmodConfig config = DreXmodConfig.Parse("<Mod><Enabled>" + text + "</Enabled><Name>x</Name></Mod>");

            Assert.That(config.Mod.Enabled, Is.EqualTo(expected));
        }

        [Test]
        public void AFolderName_IsComparedIgnoringCase()
        {
            DreXmodConfig config = DreXmodConfig.Parse(Config(mod: "1", modName: "YuKon"));

            Assert.That(config.Mod.Selects("yukon"), Is.True);
            Assert.That(config.Mod.Selects("yukon2"), Is.False);
            Assert.That(config.Mod.Selects(string.Empty), Is.False);
        }

        [Test]
        public void ANameWithEntities_IsDecoded()
        {
            DreXmodConfig config = DreXmodConfig.Parse("<Mod><Enabled>1</Enabled><Name>R&amp;D</Name></Mod>");

            Assert.That(config.Mod.Selects("R&D"), Is.True);
        }

        [Test]
        public void MissingElements_AreMissing_NotAnError()
        {
            DreXmodConfig onlyEnabled = DreXmodConfig.Parse("<Mod><Enabled>1</Enabled></Mod>");
            DreXmodConfig onlyName = DreXmodConfig.Parse("<Mod><Name>yukon</Name></Mod>");
            DreXmodConfig emptyName = DreXmodConfig.Parse("<Mod><Enabled>1</Enabled><Name></Name></Mod>");

            Assert.That(onlyEnabled.Mod.Name, Is.Null);
            Assert.That(onlyEnabled.Mod.Selects("yukon"), Is.False);
            Assert.That(onlyName.Mod.Enabled, Is.Null);
            Assert.That(onlyName.Mod.Selects("yukon"), Is.False, "without Enabled the mod is not known to be on");
            Assert.That(emptyName.Mod.Name, Is.Null);
            Assert.That(onlyName.LobbyTheme.IsPresent, Is.False);
            Assert.That(onlyName.LobbyTheme.Selects("yukon"), Is.False);
        }

        [Test]
        public void ThePartsOfDreXmod2_HaveNoModSystem()
        {
            DreXmodConfig config = DreXmodConfig.Parse(
                "<config><Camera><Enabled>1</Enabled></Camera><ScenarioHosting><Enabled>1</Enabled></ScenarioHosting>" +
                "<LobbyExtension><Enabled>1</Enabled></LobbyExtension><MenuSettings /><HUDPlayers /></config>");

            Assert.That(config.HasModSystem, Is.False);
            Assert.That(config.Mod.IsPresent, Is.False);
            Assert.That(config.LobbyTheme.IsPresent, Is.False);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("not a config at all <Mod> broken")]
        [TestCase("<!-- never closed <Mod><Enabled>1</Enabled><Name>x</Name></Mod>")]
        public void ADamagedOrEmptyFile_HasNoModSystem(string text)
        {
            Assert.That(DreXmodConfig.Parse(text).HasModSystem, Is.False);
        }

        [Test]
        public void AModInsideAComment_IsNotASelector()
        {
            DreXmodConfig config = DreXmodConfig.Parse("<!-- <Mod><Enabled>1</Enabled><Name>x</Name></Mod> --><config/>");

            Assert.That(config.Mod.IsPresent, Is.False);
        }

        // --- The reader ----------------------------------------------------------------------------------------------

        private static EffectivePathResolver Paths(InMemoryFileSystem fileSystem)
        {
            return new EffectivePathResolver(fileSystem, VirtualStoreFolder, new[] { @"C:\Program Files (x86)" });
        }

        [Test]
        public void Read_ReturnsTheSelectors_OfTheFileInTheGameFolder()
        {
            var fileSystem = new InMemoryFileSystem();
            fileSystem.AddFile(ConfigPath, Config(mod: "1", modName: "yukon"));

            DreXmodConfigFile file = DreXmodConfigReader.Read(fileSystem, Paths(fileSystem), GameFolder);

            Assert.That(file.Status, Is.EqualTo(ConfigFileStatus.Read));
            Assert.That(file.Path, Is.EqualTo(ConfigPath));
            Assert.That(file.IsVirtualStoreCopy, Is.False);
            Assert.That(file.Config.Mod.Selects("yukon"), Is.True);
        }

        [Test]
        public void Read_PrefersTheVirtualStoreCopy_WhichTheGameUses()
        {
            var fileSystem = new InMemoryFileSystem();
            fileSystem.AddFile(ConfigPath, Config(mod: "0"));
            fileSystem.AddFile(VirtualStoreConfig, Config(mod: "1", modName: "energycube"));

            DreXmodConfigFile file = DreXmodConfigReader.Read(fileSystem, Paths(fileSystem), GameFolder);

            Assert.That(file.IsVirtualStoreCopy, Is.True);
            Assert.That(file.Path, Is.EqualTo(VirtualStoreConfig));
            Assert.That(file.Config.Mod.Selects("energycube"), Is.True);
        }

        [Test]
        public void Read_WithoutTheFile_IsMissing()
        {
            var fileSystem = new InMemoryFileSystem();
            fileSystem.AddDirectory(GameFolder);

            DreXmodConfigFile file = DreXmodConfigReader.Read(fileSystem, Paths(fileSystem), GameFolder);

            Assert.That(file.Status, Is.EqualTo(ConfigFileStatus.Missing));
            Assert.That(file.Config, Is.Null);
        }

        [Test]
        public void Read_OfAFileThatCannotBeRead_IsUnreadable_WithTheReason()
        {
            var fileSystem = new InMemoryFileSystem();
            fileSystem.AddFile(ConfigPath, Config());
            fileSystem.FailOn(ConfigPath, FileSystemOperation.Read, FileSystemStatus.AccessDenied);

            DreXmodConfigFile file = DreXmodConfigReader.Read(fileSystem, Paths(fileSystem), GameFolder);

            Assert.That(file.Status, Is.EqualTo(ConfigFileStatus.Unreadable));
            Assert.That(file.Problem, Does.Contain("AccessDenied"));
        }

        [Test]
        public void Read_OfAFileThatIsTooLarge_IsUnreadable()
        {
            var fileSystem = new InMemoryFileSystem();
            fileSystem.AddFile(ConfigPath, new byte[(int)DreXmodConfigReader.MaxBytes + 1]);

            DreXmodConfigFile file = DreXmodConfigReader.Read(fileSystem, Paths(fileSystem), GameFolder);

            Assert.That(file.Status, Is.EqualTo(ConfigFileStatus.Unreadable));
        }

        [Test]
        public void Read_DecodesUtf8_WithAndWithoutAByteOrderMark_AndFallsBackToLatin1()
        {
            string text = Config(mod: "1", modName: "Mäx");
            var fileSystem = new InMemoryFileSystem();

            fileSystem.AddFile(ConfigPath, new UTF8Encoding(false).GetBytes(text));
            Assert.That(DreXmodConfigReader.Read(fileSystem, Paths(fileSystem), GameFolder).Config.Mod.Selects("Mäx"), Is.True, "UTF-8");

            fileSystem.AddFile(ConfigPath, Concat(new byte[] { 0xEF, 0xBB, 0xBF }, new UTF8Encoding(false).GetBytes(text)));
            Assert.That(DreXmodConfigReader.Read(fileSystem, Paths(fileSystem), GameFolder).Config.Mod.Selects("Mäx"), Is.True, "UTF-8 with mark");

            fileSystem.AddFile(ConfigPath, Encoding.GetEncoding(28591).GetBytes(text));
            Assert.That(DreXmodConfigReader.Read(fileSystem, Paths(fileSystem), GameFolder).Config.Mod.Selects("Mäx"), Is.True, "Latin-1");
        }

        private static byte[] Concat(byte[] first, byte[] second)
        {
            var all = new byte[first.Length + second.Length];
            first.CopyTo(all, 0);
            second.CopyTo(all, first.Length);
            return all;
        }

        [Test]
        public void Read_NeverWrites()
        {
            var inner = new InMemoryFileSystem();
            inner.AddFile(ConfigPath, Config(mod: "1", modName: "yukon"));
            var fileSystem = new WriteForbiddingFileSystem(inner);

            DreXmodConfigFile file = DreXmodConfigReader.Read(fileSystem, Paths(inner), GameFolder);

            Assert.That(file.Status, Is.EqualTo(ConfigFileStatus.Read));
        }
    }
}

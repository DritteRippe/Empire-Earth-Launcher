using System.Text;
using Empire_Earth_Launcher.Core.Installations;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.Installations
{
    /// <summary>
    /// The install info file of contract 1.2 and the component and task lists of 1.2/1.3: the format the setup writes and
    /// what readers must accept (BOM, LF and CRLF, unknown sections and keys, names ignoring case).
    /// </summary>
    [TestFixture]
    public class InstallInfoFileTests
    {
        /// <summary>The example of contract 1.2, as the setup writes it (ASCII, CRLF).</summary>
        private const string ContractExample =
            "[Install]\r\n" +
            "ContractVersion=1\r\n" +
            "Product=NeoEE\r\n" +
            "AppId=00000000-0000-0000-0000-000000000AEE\r\n" +
            "InstallMode=admin\r\n" +
            "GameVersion=2.0.0.5\r\n" +
            "SetupVersion=2.0.0\r\n" +
            "SetupBuild=a1b2c3d\r\n" +
            @"Components=game,gameaoc,additional,additional\directx_wrapper,additional\directx_wrapper\dx11_lvl11,language,language\de" + "\r\n" +
            "Tasks=compatibility,compatibility_windows,firewallexception,neoee_cdkeys\r\n" +
            "Written=2026-10-02 18:04:31\r\n" +
            "\r\n" +
            "[MissingAfterInstall]\r\n" +
            "1=Empire Earth/DDraw.dll\r\n";

        [Test]
        public void ContractExample_IsReadCompletely()
        {
            InstallInfoFile file = InstallInfoFile.Parse(Encoding.ASCII.GetBytes(ContractExample));

            Assert.That(file.ContractVersion, Is.EqualTo(1));
            Assert.That(file.HasInvalidContractVersion, Is.False);
            Assert.That(file.ProductId, Is.EqualTo("NeoEE"));
            Assert.That(file.AppId, Is.EqualTo("00000000-0000-0000-0000-000000000AEE"));
            Assert.That(file.InstallModeText, Is.EqualTo("admin"));
            Assert.That(file.InstallMode, Is.EqualTo(InstallMode.Admin));
            Assert.That(file.GameVersion, Is.EqualTo("2.0.0.5"));
            Assert.That(file.SetupVersion, Is.EqualTo("2.0.0"));
            Assert.That(file.SetupBuild, Is.EqualTo("a1b2c3d"));
            Assert.That(file.Written, Is.EqualTo("2026-10-02 18:04:31"));
            Assert.That(file.Components.Names, Has.Count.EqualTo(7));
            Assert.That(file.Components.HasArtOfConquest, Is.True);
            Assert.That(file.Components.HasDirectXWrapper, Is.True);
            Assert.That(file.Components.GameLanguage, Is.EqualTo("de"));
            Assert.That(file.Tasks.Contains("compatibility_windows"), Is.True);
            Assert.That(file.Tasks.Contains("everyoneadminstart"), Is.False);
            Assert.That(file.MissingAfterInstall, Is.EqualTo(new[] { "Empire Earth/DDraw.dll" }));
        }

        [TestCase("\r\n", false, TestName = "CRLF without BOM (as the setup writes it)")]
        [TestCase("\n", false, TestName = "LF without BOM")]
        [TestCase("\r\n", true, TestName = "CRLF with UTF-8 BOM")]
        [TestCase("\n", true, TestName = "LF with UTF-8 BOM")]
        public void LineEndsAndBom_AreAccepted(string lineEnd, bool bom)
        {
            string text = ContractExample.Replace("\r\n", lineEnd);
            byte[] content = new UTF8Encoding(bom).GetPreamble();
            content = Concat(content, Encoding.UTF8.GetBytes(text));

            InstallInfoFile file = InstallInfoFile.Parse(content);

            Assert.That(file.ContractVersion, Is.EqualTo(1));
            Assert.That(file.ProductId, Is.EqualTo("NeoEE"), "the first key must not keep the BOM");
            Assert.That(file.Written, Is.EqualTo("2026-10-02 18:04:31"), "no \\r may stay at the end of a value");
            Assert.That(file.MissingAfterInstall, Is.EqualTo(new[] { "Empire Earth/DDraw.dll" }));
        }

        [Test]
        public void UnknownSectionsAndKeys_AreIgnored()
        {
            InstallInfoFile file = InstallInfoFile.Parse(
                "[Future]\r\nContractVersion=9\r\n" +
                "[Install]\r\nFutureKey=x\r\nContractVersion=1\r\nProduct=EE\r\nno separator line\r\n=value without key\r\n" +
                "; a comment\r\n[Other]\r\nProduct=NeoEE\r\n");

            Assert.That(file.ContractVersion, Is.EqualTo(1), "keys of other sections do not count");
            Assert.That(file.ProductId, Is.EqualTo("EE"));
        }

        [Test]
        public void KeysBeforeTheFirstSection_AreIgnored()
        {
            InstallInfoFile file = InstallInfoFile.Parse("ContractVersion=1\r\n[Install]\r\nProduct=EE\r\n");

            Assert.That(file.ContractVersionText, Is.Null);
            Assert.That(file.ContractVersion, Is.EqualTo(0));
        }

        [Test]
        public void SectionAndKeyNames_IgnoreCase_AndTheFirstKeyWins()
        {
            InstallInfoFile file = InstallInfoFile.Parse(
                "[INSTALL]\r\ncontractversion = 1 \r\nPRODUCT=NeoEE\r\nProduct=EE\r\n[missingafterinstall]\r\n2=b\r\n1=a\r\n");

            Assert.That(file.ContractVersion, Is.EqualTo(1));
            Assert.That(file.ProductId, Is.EqualTo("NeoEE"), "like GetPrivateProfileString, the first occurrence wins");
            Assert.That(file.MissingAfterInstall, Is.EqualTo(new[] { "a", "b" }), "ordered by the numbers of the keys");
        }

        [Test]
        public void MissingAfterInstall_IgnoresKeysThatAreNoNumbersAndEmptyValues()
        {
            InstallInfoFile file = InstallInfoFile.Parse(
                "[MissingAfterInstall]\r\n10=ten\r\n2=two\r\nx=ignored\r\n3=\r\n2=duplicate\r\n-1=negative\r\n");

            Assert.That(file.MissingAfterInstall, Is.EqualTo(new[] { "two", "ten" }));
        }

        [Test]
        public void MissingAfterInstall_IsEmptyWithoutTheSection()
        {
            Assert.That(InstallInfoFile.Parse("[Install]\r\nContractVersion=1\r\n").MissingAfterInstall, Is.Empty);
        }

        [TestCase(null, 0, false)]
        [TestCase("1", 1, false)]
        [TestCase(" 2 ", 2, false)]
        [TestCase("0", 0, false)]
        [TestCase("one", 0, true)]
        [TestCase("-1", 0, true)]
        [TestCase("1.0", 0, true)]
        [TestCase("", 0, true)]
        public void ContractVersion_MissingOrInvalidCountsAsZero(string written, int expected, bool invalid)
        {
            string text = "[Install]\r\n" + (written == null ? string.Empty : "ContractVersion=" + written + "\r\n");

            InstallInfoFile file = InstallInfoFile.Parse(text);

            Assert.That(file.ContractVersion, Is.EqualTo(expected));
            Assert.That(file.HasInvalidContractVersion, Is.EqualTo(invalid));
        }

        [Test]
        public void EmptyOrBrokenContent_GivesAnEmptyFile()
        {
            InstallInfoFile file = InstallInfoFile.Parse(new byte[] { 0xFF, 0xFE, 0x00, 0x5B });

            Assert.That(file.ContractVersion, Is.EqualTo(0));
            Assert.That(file.ProductId, Is.Null);
            Assert.That(file.Components.Names, Is.Empty);
            Assert.That(file.Tasks.Names, Is.Empty);
            Assert.That(file.InstallMode, Is.EqualTo(InstallMode.Unknown));
        }

        [TestCase("admin", InstallMode.Admin)]
        [TestCase("USER", InstallMode.User)]
        [TestCase(" portable ", InstallMode.Portable)]
        [TestCase("other", InstallMode.Unknown)]
        [TestCase("", InstallMode.Unknown)]
        [TestCase(null, InstallMode.Unknown)]
        public void InstallModes_AreParsedIgnoringCase(string text, InstallMode expected)
        {
            Assert.That(InstallModes.Parse(text), Is.EqualTo(expected));
        }

        // --- Components and tasks (contract 1.2) ---------------------------------------------------------------------

        [TestCase("game,gameaoc", true)]
        [TestCase("GAME,GAMEAOC", true)]
        [TestCase(" game , GameAoC ", true)]
        [TestCase("game", false)]
        [TestCase("game,gameaoc2", false)]
        [TestCase("", false)]
        [TestCase(null, false)]
        public void Components_ArtOfConquestIsTheComponentGameaoc(string components, bool expected)
        {
            Assert.That(SetupNameList.Parse(components).HasArtOfConquest, Is.EqualTo(expected));
        }

        [TestCase(@"game,additional\directx_wrapper", true)]
        [TestCase(@"game,additional,additional\directx_wrapper\dx11_lvl11", true)]
        [TestCase(@"ADDITIONAL\DIRECTX_WRAPPER\DDRAWCOMPAT", true)]
        [TestCase(@"game,additional", false)]
        [TestCase(@"additional\directx_wrapper2", false)]
        [TestCase(@"additional\directx_wrapperx\dx9", false)]
        [TestCase("", false)]
        public void Components_WrapperRule(string components, bool expected)
        {
            Assert.That(SetupNameList.Parse(components).HasDirectXWrapper, Is.EqualTo(expected));
        }

        [TestCase(@"game,language,language\de", "de")]
        [TestCase(@"game,LANGUAGE\pt_BR", "pt_BR")]
        [TestCase(@"game,language", null)]
        [TestCase(@"game,language\", null)]
        [TestCase("", null)]
        public void Components_GameLanguageIsTheNameAfterLanguage(string components, string expected)
        {
            Assert.That(SetupNameList.Parse(components).GameLanguage, Is.EqualTo(expected));
        }

        [Test]
        [SetCulture("tr-TR")]
        [SetUICulture("tr-TR")]
        public void Components_IgnoreCaseIndependentOfTheCulture()
        {
            // Turkish upper-cases "i" to the dotted capital I; the names are compared ordinally.
            SetupNameList components = SetupNameList.Parse(@"GAME,GAMEAOC,ADDITIONAL\DIRECTX_WRAPPER,LANGUAGE\IT");
            SetupNameList tasks = SetupNameList.Parse("COMPATIBILITY_WINDOWS,FIREWALLEXCEPTION");

            Assert.That(components.HasArtOfConquest, Is.True);
            Assert.That(components.HasDirectXWrapper, Is.True);
            Assert.That(components.GameLanguage, Is.EqualTo("IT"));
            Assert.That(tasks.Contains("compatibility_windows"), Is.True);
            Assert.That(tasks.Contains("firewallexception"), Is.True);
        }

        [Test]
        public void NameList_KeepsTheOrderAndDropsEmptyNames()
        {
            SetupNameList list = SetupNameList.Parse(" b ,,a, ");

            Assert.That(list.Names, Is.EqualTo(new[] { "b", "a" }));
            Assert.That(list.ToString(), Is.EqualTo("b,a"));
            Assert.That(SetupNameList.Parse("  ").Names, Is.Empty);
            Assert.That(() => list.Contains(null), Throws.ArgumentNullException);
        }

        private static byte[] Concat(byte[] first, byte[] second)
        {
            var result = new byte[first.Length + second.Length];
            first.CopyTo(result, 0);
            second.CopyTo(result, first.Length);
            return result;
        }
    }
}

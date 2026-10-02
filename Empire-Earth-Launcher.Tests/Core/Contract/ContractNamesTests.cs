using System.IO;
using System.Linq;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Tests.TestSupport;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.Contract
{
    /// <summary>
    /// The fixed names of the core (<see cref="ContractNames"/>, <see cref="Product"/>, <see cref="Game"/>)
    /// against the text of <c>docs/CONTRACT.md</c>, section 0 and the sections that define the keys and files.
    /// A name that changes in one place only fails here.
    /// </summary>
    [TestFixture]
    [Category(TestCategories.SourceTree)]
    public class ContractNamesTests
    {
        private static string contract;

        [OneTimeSetUp]
        public void ReadContract()
        {
            contract = File.ReadAllText(RepositoryRoot.GetFullPath("docs/CONTRACT.md"));
        }

        /// <summary>The text appears in the contract as inline code: <c>`text`</c>.</summary>
        private static void AssertCode(string text)
        {
            Assert.That(contract, Does.Contain("`" + text + "`"), "CONTRACT.md does not contain `" + text + "`");
        }

        [Test]
        public void Section0_ContractVersion()
        {
            Assert.That(contract, Does.Contain("| Contract version | **" + ContractNames.ContractVersion + "** |"));
        }

        [Test]
        public void Section0_Products()
        {
            foreach (Product product in Product.All)
            {
                AssertCode(product.Id);
                AssertCode(product.AppName);
                AssertCode(product.Publisher);
                AssertCode(@"{autopf32}\" + product.DefaultInstallFolderName);
                AssertCode(product.SetupDataFolderName);
                AssertCode(product.SetupMutexName);
                AssertCode(product.GetGameSettingsKey(Game.EmpireEarth));
                AssertCode(product.GetGameSettingsKey(Game.ArtOfConquest));
            }
            AssertCode(Product.NeoEE.ProductOnlyFileName);
            Assert.That(Product.EE.ProductOnlyFileName, Is.Null, "EE installs no file of its own");
        }

        [Test]
        public void Section0_ProductRows()
        {
            Assert.That(contract, Does.Contain("| Product id (`InstallType` of the setup) | `EE` | `NeoEE` |"));
            Assert.That(contract, Does.Contain("| `Publisher` in the uninstall key | `Empire Earth Community` | `Empire Earth Community & NeoEE` |"));
            Assert.That(contract, Does.Contain(@"| Game settings key of Empire Earth | `Software\SSSI\Empire Earth` | `Software\Neo\Empire Earth` |"));
            Assert.That(contract, Does.Contain(@"| Game settings key of The Art of Conquest | `Software\Mad Doc Software\EE-AOC` | `Software\Neo\Art of Conquest` |"));
            Assert.That(contract, Does.Contain("| Setup mutex (`SetupMutex`) | `EE_Setup` | `NeoEE_Setup` |"));
            Assert.That(contract, Does.Contain("| Setup data folder | `_setupdata_EE` | `_setupdata_NeoEE` |"));
        }

        [Test]
        public void Section0_FoldersProgramsAndMutexes()
        {
            foreach (Game game in Game.All)
            {
                AssertCode(@"<root>\" + game.FolderName);
                AssertCode(game.ProgramName);
                AssertCode(game.MutexName);
            }
        }

        [Test]
        public void Section1And2_RecordKeyAndFiles()
        {
            AssertCode(ContractNames.InstallRecordsKey + @"\<Product>");
            AssertCode(ContractNames.UninstallKey);
            AssertCode(@"<root>\_setupdata_<Product>\" + ContractNames.InstallInfoFileName);
            AssertCode(@"<root>\_setupdata_<Product>\" + ContractNames.ManifestFileName);
        }

        [Test]
        public void Section3_SettingsKeysMarkerGpuAndLayers()
        {
            AssertCode(@"HKCU\" + ContractNames.DefaultsMarkersKey + @"\<Product>");
            AssertCode("EE");
            AssertCode("AoC");
            Assert.That(Game.All.Select(game => game.Id), Is.EqualTo(new[] { "EE", "AoC" }),
                "the game ids are the value names of the defaults marker (contract 3.5)");
            AssertCode(ContractNames.GameOptionsSubKeyName + @"\");
            AssertCode(@"HKCU\" + ContractNames.GpuPreferencesKey);
            AssertCode(ContractNames.CompatibilityLayersKey);
            AssertCode(ContractNames.CdKeysKey);
            AssertCode(ContractNames.GpuPreferenceData);
            AssertCode(ContractNames.CompatibilityWindowsTask);
            AssertCode(ContractNames.CompatibilityLegacyTask);
        }

        [Test]
        public void Section1_ValuesOfRecordInstallIniUninstallKeyAndInstalledFrom()
        {
            foreach (string name in new[]
                     {
                         ContractNames.ContractVersionName, ContractNames.InstallPathName, ContractNames.InstallModeName,
                         ContractNames.AppIdName, ContractNames.GameVersionName, ContractNames.SetupVersionName,
                         ContractNames.SetupBuildName, ContractNames.ProductName, ContractNames.ComponentsName,
                         ContractNames.TasksName, ContractNames.WrittenName, ContractNames.AdminInstallMode,
                         ContractNames.UserInstallMode, ContractNames.PortableInstallMode, ContractNames.ArtOfConquestComponent,
                         ContractNames.DirectXWrapperComponent, ContractNames.UninstallAppPathName,
                         ContractNames.UninstallInstallLocationName, ContractNames.UninstallPublisherName,
                         ContractNames.UninstallDisplayNameName, ContractNames.UninstallDisplayVersionName,
                         ContractNames.UninstallComponentsName, ContractNames.UninstallTasksName,
                         ContractNames.UninstallContractVersionName, ContractNames.InstalledFromVolumeName,
                         ContractNames.InstalledFromDirectoryName
                     })
            {
                AssertCode(name);
            }
            AssertCode(ContractNames.LanguageComponentPrefix + "<language>");
            AssertCode("[" + ContractNames.MissingAfterInstallSectionName + "]");
            Assert.That(contract, Does.Contain("[" + ContractNames.InstallInfoSectionName + "]"));
        }

        [Test]
        public void Products_NeoEEBeforeEE()
        {
            Assert.That(Product.All, Is.EqualTo(new[] { Product.NeoEE, Product.EE }), "contract 1.4: NeoEE before EE");
        }

        [Test]
        public void Products_DerivedNames()
        {
            Assert.That(Product.NeoEE.InstallRecordKey, Is.EqualTo(@"Software\Empire Earth Community\Installations\NeoEE"));
            Assert.That(Product.EE.DefaultsMarkerKey, Is.EqualTo(@"Software\Empire Earth Community\GameDefaults\EE"));
            Assert.That(Product.EE.SetupDataFolderName, Is.EqualTo("_setupdata_EE"));
            Assert.That(Product.NeoEE.SetupMutexName, Is.EqualTo("NeoEE_Setup"));
        }

        [TestCase("EE", "EE")]
        [TestCase("NeoEE", "NeoEE")]
        [TestCase("neoee", "NeoEE")]
        [TestCase("ee", "EE")]
        [TestCase("Neo", null)]
        [TestCase("", null)]
        [TestCase(null, null)]
        public void Products_FromId(string id, string expected)
        {
            Assert.That(Product.FromId(id)?.Id, Is.EqualTo(expected));
        }

        [Test]
        [SetCulture("tr-TR")]
        public void Products_FromId_IgnoresCaseIndependentOfTheCulture()
        {
            // "i" upper-cases to the dotted capital I in Turkish; an ordinal comparison is not affected.
            Assert.That(Product.FromId("NEOEE"), Is.SameAs(Product.NeoEE));
            Assert.That(Product.FromId("neoee"), Is.SameAs(Product.NeoEE));
        }
    }
}

using System;
using System.Linq;
using System.Text;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.GameSettings;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Integrity;
using Empire_Earth_Launcher.RealMachineTests.Expectations;
using Empire_Earth_Launcher.Tests.Fakes;
using NUnit.Framework;

namespace Empire_Earth_Launcher.RealMachineTests.SelfTest
{
    /// <summary>
    /// The expectation files of the CI job: the example of the README reads into the model, and every mistake (an unknown member,
    /// a wrong name, a number for a name, a relative path, a contradiction) is an error listed with all others.
    /// </summary>
    [TestFixture]
    [Category(RealMachineCategories.SelfTest)]
    public class ExpectationFileTests
    {
        /// <summary>The example of README, section Tests, "Real machine" (keep both equal).</summary>
        internal const string ReadmeExample = @"{
  ""schema"": 1,
  ""scenario"": ""A"",
  ""step"": ""installed"",
  ""selectedRoot"": ""C:\\Program Files (x86)\\Empire Earth"",
  ""installations"": [
    {
      ""product"": ""EE"",
      ""root"": ""C:\\Program Files (x86)\\Empire Earth"",
      ""kind"": ""Community"",
      ""mode"": ""Admin"",
      ""appId"": ""4C0B46D8-E7EB-4B95-97D4-A578D9B914C6"",
      ""contractVersion"": 1,
      ""hasArtOfConquest"": true,
      ""state"": ""Ok"",
      ""missingPrograms"": [],
      ""sources"": [""RegistryRecord"", ""UninstallKey"", ""InstalledFrom""],
      ""integrity"": {
        ""quick"": { ""state"": ""Ok"", ""findings"": [], ""offersRepair"": false },
        ""full"": { ""state"": ""Ok"", ""findings"": [] }
      },
      ""defaultsStatus"": { ""EE"": ""Applied"", ""AoC"": ""Applied"" },
      ""consistency"": { ""expected"": [], ""allowed"": [""ScreenTooLow"", ""WindowLargerThanScreen"", ""WindowFitsOnlyWithHighDpiAware""] },
      ""defaultsAtStart"": { ""EE"": ""None"", ""AoC"": ""None"" },
      ""installedFromAtStart"": { ""EE"": ""Present"", ""AoC"": ""Present"" }
    }
  ],
  ""defaults"": {
    ""recordSetupValuesTo"": ""D:\\a\\_temp\\e2e\\A\\installed\\setup-values-EE.json"",
    ""expectNoWrites"": true
  }
}";

        private static FormatException Error(string json)
        {
            return Assert.Throws<FormatException>(() => ExpectationFile.Parse(json.Replace('\'', '"')));
        }

        [Test]
        public void TheReadmeExample_ReadsIntoTheModel()
        {
            Expectation expectation = ExpectationFile.Parse(ReadmeExample);

            Assert.That(expectation.Scenario, Is.EqualTo("A"));
            Assert.That(expectation.ExactInstallations, Is.True);
            Assert.That(expectation.SelectedRoot, Is.EqualTo(@"C:\Program Files (x86)\Empire Earth"));
            InstallationExpectation installation = expectation.Installations.Single();
            Assert.That(installation.Product, Is.SameAs(Product.EE));
            Assert.That(installation.Kind, Is.EqualTo(InstallationKind.Community));
            Assert.That(installation.Mode, Is.EqualTo(InstallMode.Admin));
            Assert.That(installation.ContractVersion, Is.EqualTo(1));
            Assert.That(installation.MissingPrograms, Is.Empty);
            Assert.That(installation.Sources, Is.EqualTo(new[]
                { InstallationSource.RegistryRecord, InstallationSource.UninstallKey, InstallationSource.InstalledFrom }));
            Assert.That(installation.SourcesInclude, Is.Null);
            Assert.That(installation.ChecksOtherProductInRoot, Is.False);
            Assert.That(installation.Quick.State, Is.EqualTo(IntegrityState.Ok));
            Assert.That(installation.Quick.OffersRepair, Is.False);
            Assert.That(installation.Full.Findings, Is.Empty);
            Assert.That(installation.DefaultsStatus[Game.ArtOfConquest], Is.EqualTo(DefaultsStatus.Applied));
            Assert.That(installation.Consistency.Allowed, Has.Member(FindingCode.ScreenTooLow));
            Assert.That(installation.DefaultsAtStart[Game.EmpireEarth], Is.EqualTo(DefaultsAtStart.None));
            Assert.That(installation.InstalledFromAtStart[Game.ArtOfConquest], Is.EqualTo(InstalledFromAtStart.Present));
            Assert.That(expectation.Defaults.ExpectNoWrites, Is.True);
            Assert.That(expectation.Defaults.SecondStartChangesNothing, Is.True);
            Assert.That(expectation.Defaults.RecordSetupValuesTo, Is.EqualTo(@"D:\a\_temp\e2e\A\installed\setup-values-EE.json"));
            Assert.That(expectation.Roots, Is.EqualTo(new[] { @"C:\Program Files (x86)\Empire Earth" }));
        }

        [Test]
        public void AFileWithBom_IsRead_AndOnlyUtf8IsAccepted()
        {
            byte[] content = new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes(ReadmeExample)).ToArray();

            Assert.That(ExpectationFile.Parse(content).Installations, Has.Count.EqualTo(1));
            Assert.Throws<FormatException>(() => ExpectationFile.Parse(new byte[] { 0x7B, 0xFF, 0x7D }));
        }

        [Test]
        public void Load_NamesTheFile()
        {
            var fileSystem = new InMemoryFileSystem();
            fileSystem.AddFile(@"D:\e2e\expect.json", "{ \"schema\": 2, \"installations\": [] }");

            var error = Assert.Throws<FormatException>(() => ExpectationFile.Load(fileSystem, @"D:\e2e\expect.json"));
            var missing = Assert.Throws<FormatException>(() => ExpectationFile.Load(fileSystem, @"D:\e2e\other.json"));

            Assert.That(error.Message, Is.EqualTo(@"The expectation file D:\e2e\expect.json is not valid: schema is 2; this harness reads schema 1."));
            Assert.That(missing.Message, Does.StartWith(@"The expectation file D:\e2e\other.json cannot be read: NotFound"));
        }

        [Test]
        public void TheEmptyList_IsAValidStep()
        {
            Expectation expectation = ExpectationFile.Parse("{ \"schema\": 1, \"installations\": [], \"watchRoots\": [\"C:\\\\Games\\\\EE\"] }");

            Assert.That(expectation.Installations, Is.Empty);
            Assert.That(expectation.Defaults, Is.Null);
            Assert.That(expectation.Roots, Is.EqualTo(new[] { @"C:\Games\EE" }));
        }

        [Test]
        public void EveryMistake_IsListed()
        {
            FormatException error = Error(
                "{ 'schema': 1, 'scenario': 7, 'extra': true, 'installations': [ {" +
                " 'product': 'ee', 'root': 'Empire Earth', 'kind': 'Comunity', 'mode': 1, 'appId': '{4C0B46D8}'," +
                " 'missingPrograms': ['EE', 'Aoc'], 'sources': ['2'], 'integrity': { 'quick': { 'findings': [ { 'path': 'x', 'kind': 'Gone', 'hash': 'y' } ] }, 'slow': {} }," +
                " 'defaultsStatus': { 'EE': 'Done', 'Neo': 'Applied' }, 'defaultsAtStart': { 'EE': 'None' }, 'consistency': { 'expected': 'ScreenTooLow' } } ] }");

            string[] expected =
            {
                "scenario must be a string.",
                "the file has the unknown member \"extra\".",
                "installations[0].product is \"ee\", not EE or NeoEE.",
                "installations[0].root must be a full Windows path (C:\\...), not Empire Earth.",
                "installations[0].kind is \"Comunity\", not one of Community, CommunityLegacy, Foreign.",
                "installations[0].mode must be a string.",
                "installations[0].appId is written without braces.",
                "installations[0].missingPrograms[1] is \"Aoc\", not EE or AoC.",
                "installations[0].sources[0] is \"2\", not one of",
                "installations[0].integrity.quick.state is required.",
                "installations[0].integrity.quick.findings[0].kind is \"Gone\", not one of",
                "installations[0].integrity.quick.findings[0] has the unknown member \"hash\".",
                "installations[0].integrity has the unknown member \"slow\".",
                "installations[0].defaultsStatus.EE is \"Done\", not one of",
                "installations[0].defaultsStatus has the unknown member \"Neo\".",
                "installations[0].consistency.expected must be an array.",
                "installations[0] has defaultsAtStart or installedFromAtStart, which need \"defaults\"."
            };
            foreach (string text in expected)
                Assert.That(error.Message, Does.Contain(text), text);
        }

        [TestCase("{ 'installations': [] }", "schema is required.")]
        [TestCase("{ 'schema': 1 }", "installations is required.")]
        [TestCase("{ 'schema': 1, 'installations': {} }", "installations must be an array.")]
        [TestCase("[]", "the file must be an object.")]
        [TestCase("{ 'schema': 1, 'installations': [ { 'root': 'C:\\\\EE' } ] }", "installations[0].product is required.")]
        [TestCase("{ 'schema': 1, 'installations': [ { 'product': 'EE' } ] }", "installations[0].root is required.")]
        [TestCase("{ 'schema': 1, 'installations': [ { 'product': 'EE', 'root': 'C:\\\\EE' }, { 'product': 'NeoEE', 'root': 'c:\\\\ee\\\\' } ] }",
            "installations names the root C:\\EE more than once.")]
        [TestCase("{ 'schema': 1, 'installations': [], 'defaults': { 'expectNoWrites': true, 'reset': true } }",
            "defaults.expectNoWrites contradicts expectRecommendedValues and reset")]
        [TestCase("{ 'schema': 1, 'installations': [], 'defaults': { 'allowedDifferences': ['Music Volume'] } }",
            "defaults.allowedDifferences needs compareWithSetupValuesFrom.")]
        [TestCase("{ 'schema': 1, 'installations': [], 'defaults': { 'compareWithSetupValuesFrom': 'C:\\\\v.json', 'allowedDifferences': ['Volume'] } }",
            "defaults.allowedDifferences[0] is \"Volume\", neither a setting of contract 3.2 nor Marker or GpuPreference.")]
        [TestCase("{ 'schema': 1, 'installations': [], 'exactInstallations': 'yes' }", "exactInstallations must be true or false.")]
        [TestCase("{ 'schema': 1, 'installations': [ { 'product': 'EE', 'root': 'C:\\\\EE', 'contractVersion': '1' } ] }",
            "installations[0].contractVersion must be an integer.")]
        public void AMistake_IsNamed(string json, string message)
        {
            Assert.That(Error(json).Message, Does.Contain(message));
        }

        [Test]
        public void AllowedDifferences_AcceptTheSettingsTheMarkerAndTheGpuPreference()
        {
            Expectation expectation = ExpectationFile.Parse(("{ 'schema': 1, 'installations': [], 'defaults': { 'compareWithSetupValuesFrom': " +
                                                             "'C:\\\\v.json', 'allowedDifferences': ['Game Window Width', 'game options\\\\map size', " +
                                                             "'Marker', 'gpupreference'] } }").Replace('\'', '"'));

            Assert.That(expectation.Defaults.AllowedDifferences, Has.Count.EqualTo(4));
        }
    }
}

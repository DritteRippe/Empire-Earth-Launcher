using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Tests.TestSupport;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.Contract
{
    /// <summary>
    /// <see cref="CompatibilityLayers"/> and <see cref="CompatibilityLayerValue"/> (contract 3.7): the entries the launcher
    /// switches, where it puts them, the old values it only shows and the Windows version modes.
    /// </summary>
    [TestFixture]
    public class CompatibilityLayersTests
    {
        [TestCase("", "~ HIGHDPIAWARE", "HIGHDPIAWARE")]
        [TestCase("~ RUNASADMIN", "~ RUNASADMIN WIN7RTM", "WIN7RTM")]
        [TestCase("~ HIGHDPIAWARE WIN7RTM", "~ DWM8And16BitMitigation HIGHDPIAWARE WIN7RTM", "DWM8And16BitMitigation")]
        [TestCase("~ DWM8And16BitMitigation WIN7RTM", "~ DWM8And16BitMitigation HeapClearAllocation WIN7RTM", "HeapClearAllocation")]
        [TestCase("~ RUNASADMIN HIGHDPIAWARE OTHER", "~ RUNASADMIN HIGHDPIAWARE WIN7RTM OTHER", "WIN7RTM")]
        [TestCase("~ HIGHDPIAWARE", "~ HIGHDPIAWARE", "highdpiaware")]
        [TestCase("WINXPSP3", "WINXPSP3 HIGHDPIAWARE", "HIGHDPIAWARE")]
        public void With_PutsTheEntryWhereTheContractTableHasIt(string value, string expected, string entry)
        {
            Assert.That(CompatibilityLayerValue.Parse(value).With(entry).ToString(), Is.EqualTo(expected));
        }

        [Test]
        public void With_OnlyTheEntriesOfTheContractRows()
        {
            Assert.That(() => CompatibilityLayerValue.Parse("~").With(CompatibilityLayers.RunAsAdmin), Throws.ArgumentException);
            Assert.That(() => CompatibilityLayerValue.Parse("~").With("WINXPSP3"), Throws.ArgumentException);
        }

        [TestCase("~ RUNASADMIN HIGHDPIAWARE", "HIGHDPIAWARE", "~ RUNASADMIN")]
        [TestCase("~ HIGHDPIAWARE", "highdpiaware", "~")]
        [TestCase("~ A B", "C", "~ A B")]
        public void Without_KeepsTheOtherEntries(string value, string entry, string expected)
        {
            Assert.That(CompatibilityLayerValue.Parse(value).Without(entry).ToString(), Is.EqualTo(expected));
        }

        [Test]
        public void Parse_SplitsPrefixAndEntries()
        {
            CompatibilityLayerValue value = CompatibilityLayerValue.Parse("  ~  RUNASADMIN   HIGHDPIAWARE ");

            Assert.That(value.HasPrefix, Is.True);
            Assert.That(value.Entries, Is.EqualTo(new[] { "RUNASADMIN", "HIGHDPIAWARE" }));
            Assert.That(value.Contains("highdpiaware"), Is.True);
            Assert.That(CompatibilityLayerValue.Parse(null).IsEmpty, Is.True);
            Assert.That(CompatibilityLayerValue.Parse("~").IsEmpty, Is.True);
        }

        [TestCase("~ WINXPSP3", true)]
        [TestCase("~ RUNASADMIN WINXPSP3", true)]
        [TestCase("~ DWM8And16BitMitigation HIGHDPIAWARE HeapClearAllocation", true)]
        [TestCase("~ DWM8And16BitMitigation HIGHDPIAWARE HeapClearAllocation WINXPSP3", true)]
        [TestCase("~ RUNASADMIN DWM8And16BitMitigation HIGHDPIAWARE HeapClearAllocation", true)]
        [TestCase("~ RUNASADMIN DWM8And16BitMitigation HIGHDPIAWARE HeapClearAllocation WINXPSP3", true)]
        [TestCase("~ RUNASADMIN", false)]
        [TestCase("~ winxpsp3", false)]
        [TestCase("~ WINXPSP3 ", false)]
        [TestCase("~ DWM8And16BitMitigation HIGHDPIAWARE HeapClearAllocation WIN7RTM", false)]
        [TestCase(null, false)]
        public void IsLegacyVistaValue_IsExactlyOneOfTheSixValuesOfTheSetup(string value, bool expected)
        {
            Assert.That(CompatibilityLayers.IsLegacyVistaValue(value), Is.EqualTo(expected));
        }

        [TestCase("WIN95", true)]
        [TestCase("WIN98", true)]
        [TestCase("NT4SP5", true)]
        [TestCase("WIN2000", true)]
        [TestCase("WINXP", true)]
        [TestCase("WINXPSP2", true)]
        [TestCase("winxpsp3", true)]
        [TestCase("WINSRV03SP1", true)]
        [TestCase("WINSRV08SP1", true)]
        [TestCase("VISTARTM", true)]
        [TestCase("VISTASP2", true)]
        [TestCase("WIN7RTM", true)]
        [TestCase("WIN8RTM", true)]
        [TestCase("HIGHDPIAWARE", false)]
        [TestCase("RUNASADMIN", false)]
        [TestCase("DWM8And16BitMitigation", false)]
        [TestCase("~", false)]
        public void IsWindowsVersionMode(string entry, bool expected)
        {
            Assert.That(CompatibilityLayers.IsWindowsVersionMode(entry), Is.EqualTo(expected));
        }

        /// <summary>The six old values and the switchable entries are those of contract 3.7.</summary>
        [Test]
        [Category(TestCategories.SourceTree)]
        public void TheValuesAreThoseOfContract_3_7()
        {
            string contract = Regex.Replace(File.ReadAllText(RepositoryRoot.GetFullPath("docs/CONTRACT.md")), @"\s+", " ");

            foreach (string value in CompatibilityLayers.LegacyVistaValues)
                Assert.That(contract, Does.Contain("`" + value + "`"), value);
            Assert.That(contract, Does.Contain("| `compatibility` | `" + string.Join(" ", CompatibilityLayers.LauncherEntries.Take(3)) + "` | 8 and later |"));
            Assert.That(contract, Does.Contain("| `compatibility_windows` | `" + CompatibilityLayers.Windows7Mode + "` | 8 and later |"));
            Assert.That(contract, Does.Contain("exactly `" + CompatibilityLayers.LegacyRunAsAdminValue + "`"));
        }
    }
}

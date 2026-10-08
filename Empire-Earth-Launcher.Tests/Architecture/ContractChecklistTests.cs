using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Empire_Earth_Launcher.Tests.TestSupport;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Architecture
{
    /// <summary>
    /// The ticked launcher checklist of CONTRACT.md section 7 in ARCHITECTURE.md section 15 (L-WP9): one row per launcher
    /// item of the contract, every row ticked, and every test class it names exists in this test program, so the checklist
    /// cannot claim tests that are not there. The test plan side of the same items is checked by <see cref="TestPlanTests"/>.
    /// </summary>
    [TestFixture]
    [Category(TestCategories.SourceTree)]
    public class ContractChecklistTests
    {
        private const string Architecture = "docs/ARCHITECTURE.md";
        private const string Heading = "### Launcher checklist of CONTRACT 7";
        private const string Header = "| Done | Contract 7, launcher v2 | Unit tests | Test plan |";
        private const string Revision4Heading = "### Launcher checklist of CONTRACT 7, revision 4 additions";
        private const string Revision4Header = "| Done | Contract 7, additions of revision 4 | Unit tests | Test plan |";
        private const string Revision5Heading = "### Launcher checklist of CONTRACT 7, revision 5 additions";
        private const string Revision5Header = "| Done | Contract 7, additions of revision 5 | Unit tests | Test plan |";
        private const string Revision6Heading = "### Launcher checklist of CONTRACT 7, revision 6 additions";
        private const string Revision6Header = "| Done | Contract 7, additions of revision 6 | Unit tests | Test plan |";
        private const string Revision7Heading = "### Launcher checklist of CONTRACT 7, revision 7 additions";
        private const string Revision7Header = "| Done | Contract 7, additions of revision 7 | Unit tests | Test plan |";

        private static readonly Regex ClassName = new Regex("`([A-Za-z_][A-Za-z0-9_]*)`", RegexOptions.CultureInvariant);

        /// <summary>The rows of the checklist: (cells, line).</summary>
        private static List<Tuple<string[], int>> ChecklistRows()
        {
            return ChecklistRows(Heading, Header);
        }

        /// <summary>The rows of the table <paramref name="header"/> below <paramref name="heading"/>: (cells, line).</summary>
        private static List<Tuple<string[], int>> ChecklistRows(string heading, string header)
        {
            string[] lines = File.ReadAllLines(RepositoryRoot.GetFullPath(Architecture));
            int headingLine = Array.FindIndex(lines, line => line == heading);
            Assert.That(headingLine, Is.GreaterThanOrEqualTo(0), Architecture + " has no section \"" + heading + "\"");
            int headerLine = Array.FindIndex(lines, headingLine, line => line.StartsWith(header, StringComparison.Ordinal));
            Assert.That(headerLine, Is.GreaterThan(headingLine), "the checklist has no table " + header);
            var rows = new List<Tuple<string[], int>>();
            for (int i = headerLine + 2; i < lines.Length && lines[i].StartsWith("|", StringComparison.Ordinal); i++)
            {
                string[] cells = lines[i].Trim().Trim('|').Split('|').Select(cell => cell.Trim()).ToArray();
                Assert.That(cells, Has.Length.EqualTo(4), Architecture + ":" + (i + 1));
                rows.Add(Tuple.Create(cells, i + 1));
            }
            return rows;
        }

        /// <summary>True if <paramref name="name"/> is a class of this test program with at least one test.</summary>
        private static bool IsTestClass(string name)
        {
            return typeof(ContractChecklistTests).Assembly.GetTypes()
                .Where(type => type.IsClass && type.Name == name)
                .Any(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                                 .Any(method => method.IsDefined(typeof(TestAttribute), false) ||
                                                method.IsDefined(typeof(TestCaseAttribute), false) ||
                                                method.IsDefined(typeof(TestCaseSourceAttribute), false)));
        }

        [Test]
        public void EveryLauncherItemOfTheContract_IsTicked()
        {
            List<Tuple<string[], int>> rows = ChecklistRows();
            int items = TestPlanTests.ContractLauncherItems();

            Assert.That(rows, Has.Count.EqualTo(items), "one row per launcher item of CONTRACT.md section 7");
            Assert.That(rows.Where(row => row.Item1[0] != "[x]").Select(row => Architecture + ":" + row.Item2), Is.Empty,
                "every launcher item is done");
            Assert.That(rows.Select(row => row.Item1[3]),
                Is.EqualTo(Enumerable.Range(1, items).Select(n => "Launcher " + n.ToString(CultureInfo.InvariantCulture))),
                "the test plan column names the rows of the table Vertrag 7 in order");
        }

        [Test]
        public void EveryNamedTestClass_Exists()
        {
            var missing = new List<string>();
            foreach (var row in ChecklistRows())
            {
                MatchCollection names = ClassName.Matches(row.Item1[2]);
                if (names.Count == 0)
                    missing.Add(Architecture + ":" + row.Item2 + ": no test class named");
                foreach (Match name in names)
                {
                    if (!IsTestClass(name.Groups[1].Value))
                        missing.Add(Architecture + ":" + row.Item2 + ": " + name.Groups[1].Value);
                }
            }

            Assert.That(missing, Is.Empty, "test classes that this test program does not have");
        }

        /// <summary>
        /// The additions of revision 4 and 5 (launcher 1.0.0, CONTRACT.md section 7 "Launcher 1.0.0"), of revision 6
        /// (launcher 1.1.0, "Launcher 1.1.0") and of revision 7 (launcher 1.1.1, "Launcher 1.1.1"): every one has a ticked
        /// row with existing test classes and cases of the test plan; there are at least as many rows as list items of the
        /// contract.
        /// </summary>
        [TestCase("Launcher 1.0.0 (optional additions", Revision4Heading, Revision4Header)]
        [TestCase("Launcher 1.0.0 (revision 5)", Revision5Heading, Revision5Header)]
        [TestCase("Launcher 1.1.0 (revision 6", Revision6Heading, Revision6Header)]
        [TestCase("Launcher 1.1.1 (revision 7", Revision7Heading, Revision7Header)]
        public void EveryRevisionAddition_IsTicked_WithTestsAndTestPlanCases(string listStart, string heading, string header)
        {
            List<Tuple<string[], int>> rows = ChecklistRows(heading, header);
            string[] contract = File.ReadAllLines(RepositoryRoot.GetFullPath("docs/CONTRACT.md"));
            int launcher = Array.FindIndex(contract, line => line.StartsWith(listStart, StringComparison.Ordinal));
            Assert.That(launcher, Is.GreaterThanOrEqualTo(0), "CONTRACT.md section 7 has no list \"" + listStart + "\"");
            int items = 0;
            for (int i = launcher + 1; i < contract.Length && !contract[i].StartsWith("#", StringComparison.Ordinal); i++)
            {
                if (contract[i].StartsWith("- ", StringComparison.Ordinal))
                    items++;
            }

            Assert.That(rows.Count, Is.GreaterThanOrEqualTo(items), "one row per launcher item of CONTRACT.md \"" + listStart + "\"");
            Assert.That(rows.Where(row => row.Item1[0] != "[x]").Select(row => Architecture + ":" + row.Item2), Is.Empty);
            string plan = File.ReadAllText(RepositoryRoot.GetFullPath("docs/TEST-PLAN.de.md"));
            foreach (var row in rows)
            {
                MatchCollection names = ClassName.Matches(row.Item1[2]);
                Assert.That(names.Count, Is.GreaterThan(0), Architecture + ":" + row.Item2 + ": no test class named");
                foreach (Match name in names)
                    Assert.That(IsTestClass(name.Groups[1].Value), Is.True, Architecture + ":" + row.Item2 + ": " + name.Groups[1].Value);
                foreach (string id in row.Item1[3].Split(',').Select(id => id.Trim()))
                    Assert.That(plan, Does.Contain("| " + id + " |"), Architecture + ":" + row.Item2 + ": the test plan has no case " + id);
            }
        }

        /// <summary>The rules above also find what they are meant to find (a check of the check).</summary>
        [Test]
        public void TheRulesFindErrors()
        {
            Assert.That(IsTestClass(nameof(ContractChecklistTests)) && IsTestClass("DiscoveryContractTests"), Is.True);
            Assert.That(IsTestClass("NoSuchTests"), Is.False);
            Assert.That(IsTestClass(nameof(RepositoryRoot)), Is.False, "a helper without tests is no test class");
            Assert.That(ClassName.Matches("`GameStarterTests`, `SetupWatcherTests` and GameStarter").Cast<Match>()
                                 .Select(match => match.Groups[1].Value),
                Is.EqualTo(new[] { "GameStarterTests", "SetupWatcherTests" }));
        }
    }
}

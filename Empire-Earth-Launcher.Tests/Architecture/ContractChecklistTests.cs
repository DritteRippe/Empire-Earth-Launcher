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

        private static readonly Regex ClassName = new Regex("`([A-Za-z_][A-Za-z0-9_]*)`", RegexOptions.CultureInvariant);

        /// <summary>The rows of the checklist: (cells, line).</summary>
        private static List<Tuple<string[], int>> ChecklistRows()
        {
            string[] lines = File.ReadAllLines(RepositoryRoot.GetFullPath(Architecture));
            int heading = Array.FindIndex(lines, line => line == Heading);
            Assert.That(heading, Is.GreaterThanOrEqualTo(0), Architecture + " has no section \"" + Heading + "\"");
            int header = Array.FindIndex(lines, heading, line => line.StartsWith(Header, StringComparison.Ordinal));
            Assert.That(header, Is.GreaterThan(heading), "the checklist has no table " + Header);
            var rows = new List<Tuple<string[], int>>();
            for (int i = header + 2; i < lines.Length && lines[i].StartsWith("|", StringComparison.Ordinal); i++)
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

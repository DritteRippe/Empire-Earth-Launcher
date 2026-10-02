using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Empire_Earth_Launcher.Tests.TestSupport;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Architecture
{
    /// <summary>
    /// The German test plan <c>docs/TEST-PLAN.de.md</c> (ADR 0012, amendment "plan review"): its case ids are unique, every
    /// id named in it or in the other documents exists, every work package up to the one of its line "Stand" has cases, and
    /// its mapping table (section 7) assigns every requirement R1 to R10 and R17 and every test case 1 to 22 of the forum
    /// report (section 8) to cases, to "offen (L-WPn)" for a later work package, or to "Setup:"/"entfällt:" with a reason.
    /// From L-WP9 on, nothing may be "offen" anywhere in the plan, and the table "Vertrag 7" assigns each launcher item of the
    /// implementation checklist of <c>docs/CONTRACT.md</c> (section 7) to existing cases.
    /// </summary>
    [TestFixture]
    [Category(TestCategories.SourceTree)]
    public class TestPlanTests
    {
        private const string TestPlan = "docs/TEST-PLAN.de.md";
        private const string Contract = "docs/CONTRACT.md";

        /// <summary>The last work package of the launcher v2; from it on the plan is complete.</summary>
        private const int LastPackage = 9;

        private static readonly Regex CaseId = new Regex(@"^(?:WP\d+-\d{2}|W7-\d{2})$", RegexOptions.CultureInvariant);
        private static readonly Regex CaseReference = new Regex(@"(?<![\w-])(?:WP\d+-\d{2}|W7-\d{2})(?![\w-])", RegexOptions.CultureInvariant);
        private static readonly Regex Open = new Regex(@"^offen \(L-WP(\d+)\)$", RegexOptions.CultureInvariant);
        private static readonly Regex Reason = new Regex(@"^(?:Setup|entfällt): \S.{2,}$", RegexOptions.CultureInvariant);
        private static readonly Regex Stand = new Regex(@"^\| Stand \| Fälle von L-WP1 bis L-WP(\d+)[;| ]", RegexOptions.CultureInvariant);
        private static readonly Regex OpenAnywhere = new Regex(@"offen \(L-WP\d+\)|[Ww]ird mit L-WP\d+|kommt mit L-WP\d+",
            RegexOptions.CultureInvariant);

        private static readonly string[] RequiredRows =
            new[] { "R1", "R2", "R3", "R4", "R5", "R6", "R7", "R8", "R9", "R10", "R17" }
                .Concat(Enumerable.Range(1, 22).Select(n => "Forum " + n.ToString(CultureInfo.InvariantCulture)))
                .ToArray();

        private static string[] lines;

        [OneTimeSetUp]
        public void ReadTestPlan()
        {
            lines = File.ReadAllLines(RepositoryRoot.GetFullPath(TestPlan));
        }

        /// <summary>(line number, text) of the lines outside fenced code blocks.</summary>
        private static IEnumerable<Tuple<int, string>> OutsideCode(IEnumerable<string> text)
        {
            bool fenced = false;
            int number = 0;
            foreach (string line in text)
            {
                number++;
                if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
                {
                    fenced = !fenced;
                    continue;
                }
                if (!fenced)
                    yield return Tuple.Create(number, line);
            }
        }

        private static string[] Cells(string row)
        {
            return row.Trim().Trim('|').Split('|').Select(cell => cell.Trim()).ToArray();
        }

        /// <summary>The ids of the case tables (first cell of a table row), with their line.</summary>
        private static List<Tuple<string, int>> DefinedCases()
        {
            return OutsideCode(lines)
                .Where(line => line.Item2.StartsWith("| ", StringComparison.Ordinal))
                .Select(line => Tuple.Create(Cells(line.Item2)[0], line.Item1))
                .Where(cell => CaseId.IsMatch(cell.Item1))
                .ToList();
        }

        /// <summary>The rows of the mapping table of section 7: (key, assignment, line).</summary>
        private static List<Tuple<string, string, int>> MappingRows()
        {
            int section = Array.FindIndex(lines, line => line.StartsWith("## 7. Zuordnung", StringComparison.Ordinal));
            Assert.That(section, Is.GreaterThanOrEqualTo(0), "the test plan has no section 7. Zuordnung");
            int header = Array.FindIndex(lines, section, line => line.StartsWith("| Bezug | Thema | Zuordnung |", StringComparison.Ordinal));
            Assert.That(header, Is.GreaterThan(section), "section 7 has no table Bezug | Thema | Zuordnung");
            var rows = new List<Tuple<string, string, int>>();
            for (int i = header + 2; i < lines.Length && lines[i].StartsWith("|", StringComparison.Ordinal); i++)
            {
                string[] cells = Cells(lines[i]);
                Assert.That(cells, Has.Length.EqualTo(3), TestPlan + ":" + (i + 1));
                rows.Add(Tuple.Create(cells[0], cells[2], i + 1));
            }
            return rows;
        }

        /// <summary>The rows of the table "Vertrag 7" (launcher items of the contract checklist): (key, assignment, line).</summary>
        private static List<Tuple<string, string, int>> ContractRows()
        {
            int header = Array.FindIndex(lines, line => line.StartsWith("| Vertrag 7 | Punkt | Zuordnung |", StringComparison.Ordinal));
            Assert.That(header, Is.GreaterThanOrEqualTo(0), "the test plan has no table Vertrag 7 | Punkt | Zuordnung");
            var rows = new List<Tuple<string, string, int>>();
            for (int i = header + 2; i < lines.Length && lines[i].StartsWith("|", StringComparison.Ordinal); i++)
            {
                string[] cells = Cells(lines[i]);
                Assert.That(cells, Has.Length.EqualTo(3), TestPlan + ":" + (i + 1));
                rows.Add(Tuple.Create(cells[0], cells[2], i + 1));
            }
            return rows;
        }

        /// <summary>The number of launcher items of the implementation checklist (CONTRACT.md section 7, list "Launcher v2").</summary>
        internal static int ContractLauncherItems()
        {
            string[] contract = File.ReadAllLines(RepositoryRoot.GetFullPath(Contract));
            int section = Array.FindIndex(contract, line => line.StartsWith("## 7. Implementation checklist", StringComparison.Ordinal));
            Assert.That(section, Is.GreaterThanOrEqualTo(0), Contract + " has no section 7. Implementation checklist");
            int launcher = Array.FindIndex(contract, section, line => line.StartsWith("Launcher v2", StringComparison.Ordinal));
            Assert.That(launcher, Is.GreaterThan(section), Contract + " section 7 has no list Launcher v2");
            int items = 0;
            for (int i = launcher + 1; i < contract.Length && !contract[i].StartsWith("#", StringComparison.Ordinal); i++)
            {
                if (contract[i].StartsWith("- ", StringComparison.Ordinal))
                    items++;
            }
            return items;
        }

        /// <summary>The work package the test plan has reached (line "Stand").</summary>
        private static int CurrentPackage()
        {
            Match match = lines.Select(line => Stand.Match(line)).FirstOrDefault(m => m.Success);
            Assert.That(match, Is.Not.Null, "the test plan has no line \"| Stand | Fälle von L-WP1 bis L-WPn ...\"");
            return int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        }

        [Test]
        public void CaseIds_AreUnique()
        {
            var duplicates = DefinedCases().GroupBy(c => c.Item1).Where(g => g.Count() > 1)
                                           .Select(g => g.Key + " (lines " + string.Join(", ", g.Select(c => c.Item2)) + ")");

            Assert.That(duplicates, Is.Empty);
            Assert.That(DefinedCases(), Has.Count.GreaterThan(50), "the case tables were found");
        }

        [Test]
        public void EveryPackageUpToTheCurrent_HasItsCases()
        {
            int current = CurrentPackage();
            List<string> ids = DefinedCases().Select(c => c.Item1).ToList();

            Assert.That(current, Is.InRange(1, LastPackage));
            for (int package = 1; package <= current; package++)
            {
                Assert.That(ids, Has.Some.StartsWith("WP" + package.ToString(CultureInfo.InvariantCulture) + "-"),
                    "the test plan names L-WP" + current + " in its line Stand, so it has cases WP" + package + "-xx");
            }
        }

        /// <summary>From L-WP9 on, the plan is complete: no "offen (L-WPn)" and no "wird/kommt mit L-WPn" anywhere.</summary>
        [Test]
        public void FromTheLastPackageOn_NothingIsOpen()
        {
            if (CurrentPackage() < LastPackage)
                Assert.Ignore("the test plan has not reached L-WP" + LastPackage);
            var open = OutsideCode(lines).Where(line => OpenAnywhere.IsMatch(line.Item2))
                                         .Select(line => TestPlan + ":" + line.Item1 + ": " + OpenAnywhere.Match(line.Item2).Value);

            Assert.That(open, Is.Empty);
        }

        /// <summary>
        /// Every launcher item of the contract's implementation checklist (section 7) has exactly one row "Launcher n" in the
        /// table "Vertrag 7", with existing case ids only (ADR 0012, L-WP9).
        /// </summary>
        [Test]
        public void TheContractChecklist_HasEveryLauncherItemOnce()
        {
            if (CurrentPackage() < LastPackage)
                Assert.Ignore("the table Vertrag 7 comes with L-WP" + LastPackage);
            var defined = new HashSet<string>(DefinedCases().Select(c => c.Item1));
            List<Tuple<string, string, int>> rows = ContractRows();
            int items = ContractLauncherItems();
            var problems = new List<string>();
            foreach (var row in rows)
            {
                string[] ids = row.Item2.Split(',').Select(id => id.Trim()).ToArray();
                foreach (string id in ids)
                {
                    if (!CaseId.IsMatch(id))
                        problems.Add(TestPlan + ":" + row.Item3 + " (" + row.Item1 + "): \"" + id + "\" is no case id");
                    else if (!defined.Contains(id))
                        problems.Add(TestPlan + ":" + row.Item3 + " (" + row.Item1 + "): the case " + id + " does not exist");
                }
            }

            Assert.That(items, Is.EqualTo(5), "the contract lists five launcher items; a new one needs a row and this number");
            Assert.That(rows.Select(row => row.Item1),
                Is.EqualTo(Enumerable.Range(1, items).Select(n => "Launcher " + n.ToString(CultureInfo.InvariantCulture))));
            Assert.That(problems, Is.Empty);
        }

        /// <summary>Every case id named in the test plan or in another document of the repository is defined.</summary>
        [Test]
        public void EveryNamedCase_Exists()
        {
            var defined = new HashSet<string>(DefinedCases().Select(c => c.Item1));
            var documents = new[] { TestPlan, "README.md", "CHANGELOG.md", "docs/ARCHITECTURE.md", "docs/TRANSLATING.md" }
                .Concat(Directory.EnumerateFiles(RepositoryRoot.GetFullPath("docs/adr"), "*.md").Select(RepositoryRoot.ToRelativePath));
            var missing = new List<string>();
            foreach (string document in documents)
            {
                foreach (var line in OutsideCode(File.ReadAllLines(RepositoryRoot.GetFullPath(document))))
                {
                    foreach (Match reference in CaseReference.Matches(line.Item2))
                    {
                        if (!defined.Contains(reference.Value))
                            missing.Add(document + ":" + line.Item1 + ": " + reference.Value);
                    }
                }
            }

            Assert.That(missing, Is.Empty, "case ids that the test plan does not define");
        }

        [Test]
        public void TheMappingTable_HasEveryRequirementAndForumCaseOnce()
        {
            List<string> keys = MappingRows().Select(row => row.Item1).ToList();

            Assert.That(keys, Is.EquivalentTo(RequiredRows), "R1-R10, R17 and Forum 1-22, each exactly once");
        }

        [Test]
        public void EveryAssignment_IsACaseALaterPackageTheSetupOrDropped()
        {
            var defined = new HashSet<string>(DefinedCases().Select(c => c.Item1));
            int current = CurrentPackage();
            var problems = new List<string>();
            foreach (var row in MappingRows())
            {
                string where = TestPlan + ":" + row.Item3 + " (" + row.Item1 + ")";
                string[] parts = row.Item2.Split(';').Select(part => part.Trim()).ToArray();
                if (parts.Length == 0 || parts.Any(part => part.Length == 0))
                    problems.Add(where + ": empty assignment");
                foreach (string part in parts.Where(part => part.Length > 0))
                {
                    Match open = Open.Match(part);
                    if (open.Success)
                    {
                        int package = int.Parse(open.Groups[1].Value, CultureInfo.InvariantCulture);
                        if (current >= 9)
                            problems.Add(where + ": from L-WP9 on nothing may be \"offen\"");
                        else if (package <= current || package > 9)
                            problems.Add(where + ": \"" + part + "\" names a package that is not after L-WP" + current);
                        continue;
                    }
                    if (part.StartsWith("Setup", StringComparison.Ordinal) || part.StartsWith("entfällt", StringComparison.Ordinal))
                    {
                        if (!Reason.IsMatch(part))
                            problems.Add(where + ": \"" + part + "\" needs a colon and a reason");
                        continue;
                    }
                    foreach (string id in part.Split(',').Select(id => id.Trim()))
                    {
                        if (!CaseId.IsMatch(id))
                            problems.Add(where + ": \"" + id + "\" is no case id, \"offen (L-WPn)\", \"Setup: ...\" or \"entfällt: ...\"");
                        else if (!defined.Contains(id))
                            problems.Add(where + ": the case " + id + " does not exist");
                    }
                }
            }

            Assert.That(problems, Is.Empty);
        }

        /// <summary>The rules above also find what they are meant to find (a check of the check).</summary>
        [Test]
        public void TheRulesFindErrors()
        {
            Assert.That(CaseId.IsMatch("WP5-01") && CaseId.IsMatch("W7-05"), Is.True);
            Assert.That(CaseId.IsMatch("WP5-1") || CaseId.IsMatch("TP-10") || CaseId.IsMatch("WP5-001"), Is.False);
            Assert.That(CaseReference.Matches("see WP4-16, WP5-02; not XWP5-03 or WP5-031").Cast<Match>().Select(m => m.Value),
                Is.EqualTo(new[] { "WP4-16", "WP5-02" }));
            Assert.That(Open.IsMatch("offen (L-WP7)"), Is.True);
            Assert.That(Open.IsMatch("offen L-WP7"), Is.False);
            Assert.That(OpenAnywhere.IsMatch("| R7 | Netzwerk | offen (L-WP9) |"), Is.True);
            Assert.That(OpenAnywhere.IsMatch("Wird mit L-WP9 ausgearbeitet."), Is.True);
            Assert.That(OpenAnywhere.IsMatch("Bis L-WP8 hieß „offen (L-WPn)“ ...; den Launcher offen lassen"), Is.False);
            Assert.That(Reason.IsMatch("Setup: TP-76 (Firewall)"), Is.True);
            Assert.That(Reason.IsMatch("Setup"), Is.False);
            Assert.That(Reason.IsMatch("entfällt:"), Is.False);
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.GameSettings;
using Empire_Earth_Launcher.Core.Maintenance;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Tests.TestSupport;
using Microsoft.Win32;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.Maintenance
{
    /// <summary>
    /// The cleanup list (R5, contract 3.8, ADR 0007 and its amendments): explicit keys with evidence, nothing protected as a
    /// target, the launcher deletes only HKCU keys, and the code table equals the table of ARCHITECTURE 4.6. A manipulated
    /// entry cannot be built, and a delete rule made for a protected key is refused by the policy.
    /// </summary>
    [TestFixture]
    public class CleanupCandidatesTests
    {
        private static readonly RegistryOperation[] AllOperations =
            (RegistryOperation[])Enum.GetValues(typeof(RegistryOperation));

        [Test]
        public void EveryEntry_HasEvidence_OfTheForumOrTheSetup()
        {
            Assert.That(CleanupEntry.EvidencePattern.ToString(), Is.EqualTo(@"t=\d+|p=\d+|setup:"));
            foreach (CleanupEntry entry in CleanupCandidates.All)
                Assert.That(entry.Evidence, Does.Match(@"t=\d+|p=\d+|setup:"), entry.ToString());
        }

        [Test]
        public void EveryEntry_NamesOneExplicitKey_OnceWithAUniqueId()
        {
            Assert.That(CleanupCandidates.All, Has.Count.EqualTo(17));
            Assert.That(CleanupCandidates.All.Select(entry => entry.Id), Is.Unique);
            Assert.That(CleanupCandidates.All.Select(entry => entry.Key), Is.Unique);
            foreach (CleanupEntry entry in CleanupCandidates.All)
            {
                Assert.That(entry.Key.IsRoot, Is.False, entry.ToString());
                Assert.That(entry.Key.Path.IndexOfAny(new[] { '*', '?', '/' }), Is.EqualTo(-1), "no wildcard: " + entry);
                Assert.That(entry.Key.Path, Does.StartWith(@"Software\"), entry.ToString());
            }
        }

        /// <summary>
        /// No key the cleanup may delete or advise to delete is a protected key, below one or an ancestor of one in canonical
        /// form (contract 3.8); the protected entries are exactly the <c>Software\Sierra</c> keys, ancestors of the CD keys.
        /// </summary>
        [Test]
        public void NoTarget_IsProtected_AndTheProtectedEntriesAreTheSierraKeys()
        {
            foreach (CleanupEntry entry in CleanupCandidates.All)
            {
                RegistryWriteDenial protection = RegistryWritePolicy.ProtectionOf(entry.Key);
                if (entry.Scope == CleanupScope.Protected)
                {
                    Assert.That(protection, Is.EqualTo(RegistryWriteDenial.CdKeys), entry.ToString());
                    Assert.That(RegistryPath.Canonicalize(entry.Key).Path, Is.EqualTo(@"SOFTWARE\SIERRA"), entry.ToString());
                }
                else
                {
                    Assert.That(protection, Is.EqualTo(RegistryWriteDenial.None), entry.ToString());
                }
            }
        }

        [Test]
        public void TheLauncherDeletes_OnlyHkcuKeys_AndHklmKeysAreAdviceOnly()
        {
            foreach (CleanupEntry entry in CleanupCandidates.All)
            {
                if (entry.Scope == CleanupScope.LauncherDeletes)
                    Assert.That(entry.Key.Hive, Is.EqualTo(RegistryHive.CurrentUser), entry.ToString());
                if (entry.Key.Hive == RegistryHive.LocalMachine)
                    Assert.That(entry.Scope, Is.Not.EqualTo(CleanupScope.LauncherDeletes), entry.ToString());
            }
            Assert.That(CleanupCandidates.All.Count(entry => entry.Scope == CleanupScope.LauncherDeletes), Is.EqualTo(8));
            Assert.That(CleanupCandidates.All.Count(entry => entry.Scope == CleanupScope.AdviceOnly), Is.EqualTo(4));
            Assert.That(CleanupCandidates.All.Count(entry => entry.Scope == CleanupScope.Protected), Is.EqualTo(5));
        }

        /// <summary>
        /// No vendor root and no unproven entry (forum p=4756: "make sure to only get ones for ee and aoc"; Stainless Steel
        /// Studios has no path yet).
        /// </summary>
        [Test]
        public void NoVendorRoot_AndNoUnprovenKey_IsOnTheList()
        {
            foreach (CleanupEntry entry in CleanupCandidates.All.Where(entry => entry.Scope != CleanupScope.Protected))
            {
                string canonical = RegistryPath.Canonicalize(entry.Key).Path;
                Assert.That(canonical, Does.EndWith(@"\EMPIRE EARTH").Or.EndWith(@"\EE-AOC").Or.EndWith(@"\ART OF CONQUEST"),
                    entry.ToString());
                Assert.That(canonical, Does.Not.Contain("STAINLESS"), entry.ToString());
                Assert.That(canonical, Does.Not.Contain("INSTALLSHIELD"), entry.ToString());
            }
        }

        /// <summary>The game settings keys of contract 3.1 belong to their product and game (they decide "installation found").</summary>
        [Test]
        public void TheGameSettingsKeys_BelongToTheirProductAndGame()
        {
            foreach (Product product in Product.All)
            {
                foreach (Game game in Game.All)
                {
                    CleanupEntry entry = CleanupCandidates.All.Single(candidate =>
                        candidate.Key.Equals(RegistryLocation.CurrentUser(product.GetGameSettingsKey(game))));
                    Assert.That(entry.Product, Is.SameAs(product));
                    Assert.That(entry.Game, Is.SameAs(game));
                    Assert.That(entry.Scope, Is.EqualTo(CleanupScope.LauncherDeletes));
                    Assert.That(entry.ShownWhenKept, Is.False, "the player's settings while an installation uses them");
                }
            }
        }

        // --- Manipulated entries ---------------------------------------------------------------------------------------

        public static IEnumerable<TestCaseData> ManipulatedEntries()
        {
            string[] protectedKeys =
            {
                @"HKCU\Software\Sierra", @"HKCU\Software\Sierra\CDKeys", @"HKCU\Software", @"HKCU\Software\WOW6432Node\Sierra",
                @"HKCU\Software\Classes\VirtualStore\MACHINE\SOFTWARE\Sierra",
                @"HKCU\Software\Classes\VirtualStore\MACHINE\SOFTWARE\WOW6432Node\Sierra\CDKeys",
                @"HKCU\Software\Classes\VirtualStore", @"HKCU\Software\Classes",
                @"HKCU\Software\Empire Earth Community\Installations\NeoEE", @"HKCU\Software\Empire Earth Community",
                @"HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\{x}_is1", @"HKCU\Software\Microsoft",
            };
            foreach (string key in protectedKeys)
            {
                yield return new TestCaseData(key, CleanupScope.LauncherDeletes, "t=1").SetName("LauncherDeletes(" + key + ")");
            }
            foreach (string key in new[] { @"HKLM64\Software\Sierra", @"HKLM32\Software", @"HKLM64\Software\WOW6432Node\Sierra\CDKeys",
                         @"HKLM64\Software\Empire Earth Community\Installations" })
            {
                yield return new TestCaseData(key, CleanupScope.AdviceOnly, "t=1").SetName("AdviceOnly(" + key + ")");
            }
            yield return new TestCaseData(@"HKLM32\Software\SSSI\Empire Earth", CleanupScope.LauncherDeletes, "t=1")
                .SetName("LauncherDeletes_Hklm");
            yield return new TestCaseData(@"HKCU\Software\SSSI\Empire Earth", CleanupScope.AdviceOnly, "t=1").SetName("AdviceOnly_Hkcu");
            yield return new TestCaseData(@"HKCU\Software\SSSI\Empire Earth", CleanupScope.Protected, "t=1").SetName("Protected_NotSierra");
            yield return new TestCaseData(@"HKCU\Software\SSSI\*", CleanupScope.LauncherDeletes, "t=1").SetName("Wildcard");
            yield return new TestCaseData(@"HKCU\Software\SSSI\Empire Earth", CleanupScope.LauncherDeletes, "forum")
                .SetName("WithoutEvidence");
            yield return new TestCaseData(@"HKCU\Software\SSSI\Empire Earth", CleanupScope.LauncherDeletes, null).SetName("NullEvidence");
        }

        [TestCaseSource(nameof(ManipulatedEntries))]
        public void AManipulatedEntry_CannotBeBuilt(string location, CleanupScope scope, string evidence)
        {
            Product product = scope == CleanupScope.Protected ? null : Product.EE;
            Game game = scope == CleanupScope.Protected ? null : Game.EmpireEarth;

            Assert.That(() => new CleanupEntry("manipulated", RegistryLocation.Parse(location), scope, product, game, true, evidence),
                Throws.ArgumentException);
        }

        /// <summary>
        /// Even a delete rule made for a protected key, under any alias, is refused by the policy: the protected keys are
        /// refused after the canonical form, before the allow-list (ADR 0007).
        /// </summary>
        [TestCase(@"HKCU\Software\Sierra")]
        [TestCase(@"HKCU\Software\Sierra\CDKeys")]
        [TestCase(@"HKCU\SOFTWARE\SIERRA\CDKEYS\")]
        [TestCase(@"HKCU\Software\WOW6432Node\Sierra\CDKeys")]
        [TestCase(@"HKCU\Software\Classes\VirtualStore\MACHINE\SOFTWARE\Sierra")]
        [TestCase(@"HKCU\Software\Classes\VirtualStore\MACHINE\SOFTWARE\WOW6432Node\Sierra\CDKeys")]
        [TestCase(@"HKCU\Software\Classes\VirtualStore\MACHINE")]
        [TestCase(@"HKCU\Software")]
        [TestCase(@"HKCU\Software\Empire Earth Community\Installations\EE")]
        [TestCase(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall")]
        public void ADeleteRuleForAProtectedKey_IsRefusedByThePolicy(string location)
        {
            RegistryLocation key = RegistryLocation.Parse(location);
            var policy = new RegistryWritePolicy(new[] { CleanupCandidates.DeleteRuleFor(key) });

            RegistryWriteDecision decision = policy.Check(RegistryOperation.DeleteSubKeyTree, key);

            Assert.That(decision.IsAllowed, Is.False, decision.ToString());
            Assert.That(decision.Denial, Is.AnyOf(RegistryWriteDenial.CdKeys, RegistryWriteDenial.InstallRecord,
                RegistryWriteDenial.UninstallKey));
        }

        [Test]
        public void TheWriteRules_AllowOnlyDeletingTheHkcuKeysOfTheList()
        {
            List<RegistryWriteRule> rules = CleanupCandidates.WriteRules(CleanupCandidates.All).ToList();

            Assert.That(rules.Select(rule => rule.Key), Is.EquivalentTo(
                CleanupCandidates.All.Where(entry => entry.Scope == CleanupScope.LauncherDeletes).Select(entry => entry.Key)));
            Assert.That(rules.SelectMany(rule => rule.Operations).Distinct(), Is.EqualTo(new[] { RegistryOperation.DeleteSubKeyTree }));
            Assert.That(() => CleanupCandidates.DeleteRuleFor(RegistryLocation.LocalMachine32(@"Software\SSSI\Empire Earth")),
                Throws.ArgumentException);
            foreach (RegistryWriteRule rule in rules)
            {
                // The game settings keys keep their own value rules (contract 3.2); the cleanup rules open nothing else.
                bool settingsKey = Product.All.Any(product => Game.All.Any(game =>
                    RegistryLocation.CurrentUser(product.GetGameSettingsKey(game)).Equals(rule.Key)));
                Assert.That(LauncherWritePolicy.Default.Check(RegistryOperation.SetValue, rule.Key, "Not a contract value").IsAllowed,
                    Is.False, rule.ToString());
                Assert.That(LauncherWritePolicy.Default.Check(RegistryOperation.DeleteValue, rule.Key, "Not a contract value").IsAllowed,
                    Is.False, rule.ToString());
                Assert.That(LauncherWritePolicy.Default.Check(RegistryOperation.CreateSubKey, rule.Key).IsAllowed, Is.EqualTo(settingsKey),
                    rule.ToString());
            }
        }

        // --- The table of ARCHITECTURE 4.6 -------------------------------------------------------------------------------

        private static readonly Regex Row = new Regex(
            @"^\s*\| `(?<id>[a-z0-9-]+)` \| `(?<key>[^`]+)` \| (?<scope>launcher deletes|advice only|protected) \|(?<when>[^|]*)\|(?<otherwise>[^|]*)\| (?<evidence>[^|]+) \|\s*$",
            RegexOptions.CultureInvariant);

        /// <summary>The rows of the cleanup table in section 4.6 of ARCHITECTURE.md: id, key, scope, evidence.</summary>
        private static List<Tuple<string, RegistryLocation, CleanupScope, string>> ArchitectureRows()
        {
            string[] lines = File.ReadAllLines(RepositoryRoot.GetFullPath("docs/ARCHITECTURE.md"));
            int start = Array.FindIndex(lines, line => line.StartsWith("### 4.6 Tools", StringComparison.Ordinal));
            int end = Array.FindIndex(lines, start + 1, line => line.StartsWith("## ", StringComparison.Ordinal));
            Assert.That(start, Is.GreaterThan(0), "section 4.6");
            var rows = new List<Tuple<string, RegistryLocation, CleanupScope, string>>();
            for (int i = start; i < end; i++)
            {
                Match match = Row.Match(lines[i]);
                if (!match.Success)
                    continue;
                CleanupScope scope = match.Groups["scope"].Value == "launcher deletes" ? CleanupScope.LauncherDeletes
                    : match.Groups["scope"].Value == "advice only" ? CleanupScope.AdviceOnly : CleanupScope.Protected;
                rows.Add(Tuple.Create(match.Groups["id"].Value, RegistryLocation.Parse(match.Groups["key"].Value), scope,
                    match.Groups["evidence"].Value.Trim()));
            }
            return rows;
        }

        [Test]
        [Category(TestCategories.SourceTree)]
        public void TheCodeTable_EqualsTheTableOfArchitecture_4_6()
        {
            List<Tuple<string, RegistryLocation, CleanupScope, string>> rows = ArchitectureRows();

            Assert.That(rows.Select(row => row.Item1), Is.EqualTo(CleanupCandidates.All.Select(entry => entry.Id)), "ids and order");
            for (int i = 0; i < rows.Count; i++)
            {
                CleanupEntry entry = CleanupCandidates.All[i];
                Assert.That(rows[i].Item2, Is.EqualTo(entry.Key), entry.Id);
                Assert.That(rows[i].Item2.Path, Is.EqualTo(entry.Key.Path), "spelled as written: " + entry.Id);
                Assert.That(rows[i].Item3, Is.EqualTo(entry.Scope), entry.Id);
                Assert.That(rows[i].Item4, Is.EqualTo(entry.Evidence), entry.Id);
            }
        }
    }
}

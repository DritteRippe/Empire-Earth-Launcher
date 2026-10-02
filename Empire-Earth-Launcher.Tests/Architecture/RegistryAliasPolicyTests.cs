using System;
using System.Collections.Generic;
using System.Linq;
using Empire_Earth_Launcher.Core.GameSettings;
using Empire_Earth_Launcher.Core.Maintenance;
using Empire_Earth_Launcher.Core.Platform;
using Microsoft.Win32;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Architecture
{
    /// <summary>
    /// Table test of the protected registry keys (ADR 0007 amendment, contract 3.8, briefing D6): every alias of
    /// <c>Software\Sierra\CDKeys</c>, of the registry records and of the uninstall keys (case, <c>/</c>, doubled and
    /// trailing backslashes, <c>WOW6432Node</c>, the registry VirtualStore), every key below them and every
    /// ancestor is refused for every operation.
    /// </summary>
    /// <remarks>
    /// Each case runs twice: with the launcher's policy and with a policy whose allow-list names the alias itself
    /// with every operation. The second proves that the protection does not depend on the allow-list ("deny after
    /// canonicalization, then allow"). The expected reason is part of each case, so that removing one alias rule
    /// (e.g. <c>WOW6432Node</c>) fails here even where another rule would still refuse the change. Since L-WP8 the
    /// launcher's policy contains the delete rules of the cleanup list (<see cref="CleanupCandidates"/>), so every case also
    /// proves that no cleanup rule opens a protected key; <see cref="AliasesOfTheCleanupKeys_AreNotDeleted"/> checks that an
    /// alias of a listed key does not reach it either.
    /// </remarks>
    [TestFixture]
    public class RegistryAliasPolicyTests
    {
        private static readonly RegistryOperation[] Operations =
            (RegistryOperation[])Enum.GetValues(typeof(RegistryOperation));

        /// <summary>Every spelling of the CD-key key that Windows or our canonical form treats as the same key.</summary>
        private static readonly string[] CdKeyAliases =
        {
            @"HKCU\Software\Sierra\CDKeys",
            @"HKCU\SOFTWARE\SIERRA\CDKEYS",
            @"HKCU\software\sierra\cdkeys",
            @"HKCU\Software/Sierra/CDKeys",
            @"HKCU\Software\Sierra/CDKeys",
            @"HKCU\\Software\\Sierra\\\CDKeys\\",
            @"HKCU\Software\WOW6432Node\Sierra\CDKeys",
            @"HKCU\Software\Wow6432Node\Wow6432Node\Sierra\CDKeys",
            @"HKCU\Software/WOW6432Node/Sierra/CDKeys",
            @"HKLM64\Software\Sierra\CDKeys",
            @"HKLM32\Software\Sierra\CDKeys",
            @"HKLM64\SOFTWARE\WOW6432Node\Sierra\CDKeys",
            @"HKLM32\Software\WOW6432Node\Sierra\CDKeys",
            @"HKLM64\Software/Wow6432Node/Sierra/CDKeys",
            @"HKLM64\software\wow6432node\WOW6432NODE\sierra\cdkeys",
            @"HKCU\Software\Classes\VirtualStore\MACHINE\SOFTWARE\Sierra\CDKeys",
            @"HKCU\Software\Classes\VirtualStore\MACHINE\SOFTWARE\WOW6432Node\Sierra\CDKeys",
            @"HKCU\software\classes\virtualstore\machine\software\wow6432node\sierra\cdkeys",
            @"HKCU\Software/Classes/VirtualStore/MACHINE/SOFTWARE/WOW6432Node/Sierra/CDKeys",
            @"HKCU\Software\Classes\VirtualStore\MACHINE\SOFTWARE\Wow6432Node\Wow6432Node\Sierra\CDKeys",
            @"HKCU\\Software\Classes\\VirtualStore\MACHINE\\SOFTWARE\Sierra\CDKeys\",
        };

        /// <summary>Ancestors of the CD keys and of their aliases: deleting one of them deletes the CD keys.</summary>
        private static readonly string[] CdKeyAncestors =
        {
            "HKCU", @"HKCU\Software", @"HKCU\Software\Sierra", @"HKCU\SOFTWARE\SIERRA\", @"HKCU\software/sierra",
            @"HKCU\Software\WOW6432Node", @"HKCU\Software\WOW6432Node\Sierra",
            "HKLM64", @"HKLM64\Software", @"HKLM64\Software\Sierra", @"HKLM64\Software\WOW6432Node",
            @"HKLM64\Software\WOW6432Node\Sierra", "HKLM32", @"HKLM32\Software", @"HKLM32\Software\Sierra",
            @"HKLM32\Software\WOW6432Node\Sierra",
            @"HKCU\Software\Classes", @"HKCU\Software\Classes\VirtualStore", @"HKCU\software/classes/virtualstore",
            @"HKCU\Software\Classes\VirtualStore\MACHINE", @"HKCU\Software\Classes\VirtualStore\MACHINE\SOFTWARE",
            @"HKCU\Software\Classes\VirtualStore\MACHINE\SOFTWARE\WOW6432Node",
            @"HKCU\Software\Classes\VirtualStore\MACHINE\SOFTWARE\Sierra",
            @"HKCU\Software\Classes\VirtualStore\MACHINE\SOFTWARE\WOW6432Node\Sierra",
        };

        private static readonly string[] InstallRecordKeys =
        {
            @"HKCU\Software\Empire Earth Community\Installations",
            @"HKCU\Software\Empire Earth Community\Installations\NeoEE",
            @"HKCU\software\empire earth community\installations\EE\Sub",
            @"HKCU\Software/Empire Earth Community/Installations/EE",
            @"HKCU\Software\Empire Earth Community",
            @"HKCU\Software\WOW6432Node\Empire Earth Community\Installations\NeoEE",
            @"HKLM64\Software\Empire Earth Community\Installations\NeoEE",
            @"HKLM64\Software\Empire Earth Community",
            @"HKLM32\Software\Empire Earth Community\Installations\EE",
            @"HKLM64\Software\WOW6432Node\Empire Earth Community\Installations\EE",
            @"HKCU\Software\Classes\VirtualStore\MACHINE\SOFTWARE\Empire Earth Community\Installations\NeoEE",
            @"HKCU\Software\Classes\VirtualStore\MACHINE\SOFTWARE\WOW6432Node\Empire Earth Community",
        };

        private static readonly string[] UninstallKeys =
        {
            @"HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall",
            @"HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\{00000000-0000-0000-0000-000000000AEE}_is1",
            @"HKCU\software\microsoft\windows\currentversion\uninstall\{x}_is1\",
            @"HKCU\Software\Microsoft\Windows\CurrentVersion",
            @"HKCU\Software\Microsoft\Windows",
            @"HKCU\Software\Microsoft",
            @"HKCU\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\{x}_is1",
            @"HKLM64\Software\Microsoft\Windows\CurrentVersion\Uninstall\{x}_is1",
            @"HKLM32\Software\Microsoft\Windows\CurrentVersion\Uninstall",
            @"HKLM64\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\{x}_is1",
            @"HKCU\Software\Classes\VirtualStore\MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{x}_is1",
        };

        /// <summary>Keys of other hives, including other accounts' CD keys and the merged HKCR view.</summary>
        private static readonly string[] OtherHiveKeys =
        {
            @"HKU\S-1-5-21-1004336348-1177238915-682003330-1001\Software\Sierra\CDKeys",
            @"HKU\.DEFAULT\Software\Sierra\CDKeys",
            @"HKCR\VirtualStore\MACHINE\SOFTWARE\Sierra\CDKeys",
            @"HKLM64\Software\SSSI\Empire Earth",
            @"HKLM32\Software\Mad Doc Software\EE-AOC",
            @"HKLM64\Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers",
        };

        public static IEnumerable<TestCaseData> ProtectedCases()
        {
            var rows = new List<Tuple<string, RegistryWriteDenial>>();
            foreach (string alias in CdKeyAliases)
            {
                rows.Add(Tuple.Create(alias, RegistryWriteDenial.CdKeys));
                rows.Add(Tuple.Create(alias.TrimEnd('\\') + @"\Empire Earth", RegistryWriteDenial.CdKeys));
                rows.Add(Tuple.Create(alias.TrimEnd('\\') + @"\Empire Earth - The Art of Conquest\Sub", RegistryWriteDenial.CdKeys));
            }
            rows.AddRange(CdKeyAncestors.Select(key => Tuple.Create(key, RegistryWriteDenial.CdKeys)));
            rows.AddRange(InstallRecordKeys.Select(key => Tuple.Create(key, RegistryWriteDenial.InstallRecord)));
            rows.AddRange(UninstallKeys.Select(key => Tuple.Create(key, RegistryWriteDenial.UninstallKey)));
            rows.AddRange(OtherHiveKeys.Select(key => Tuple.Create(key, RegistryWriteDenial.NotCurrentUser)));

            foreach (var row in rows)
            {
                foreach (RegistryOperation operation in Operations)
                {
                    foreach (bool permissive in new[] { false, true })
                    {
                        yield return new TestCaseData(row.Item1, operation, permissive, row.Item2)
                            .SetName((permissive ? "AllowListNamesIt_" : "Default_") + operation + "(" + row.Item1 + ")");
                    }
                }
            }
        }

        [TestCaseSource(nameof(ProtectedCases))]
        public void ProtectedKey_IsRefused(string location, RegistryOperation operation, bool allowListNamesIt,
            RegistryWriteDenial expected)
        {
            RegistryLocation key = RegistryLocation.Parse(location);
            RegistryWritePolicy policy = allowListNamesIt ? PolicyThatLists(key) : LauncherWritePolicy.Default;

            foreach (string valueName in ValueNames(operation))
            {
                RegistryWriteDecision decision = policy.Check(operation, key, valueName);

                Assert.That(decision.IsAllowed, Is.False, decision.ToString());
                Assert.That(decision.Denial, Is.EqualTo(expected), decision.ToString());
            }
        }

        [Test]
        public void TheTableCoversEveryOperationAndBothViewsOfHklm()
        {
            var locations = ProtectedCases().Select(c => RegistryLocation.Parse((string)c.Arguments[0])).ToList();

            Assert.That(locations.Select(l => l.Hive).Distinct(),
                Is.SupersetOf(new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine, RegistryHive.Users, RegistryHive.ClassesRoot }));
            Assert.That(locations.Where(l => l.Hive == RegistryHive.LocalMachine).Select(l => l.View).Distinct(),
                Is.EquivalentTo(new[] { RegistryView.Registry32, RegistryView.Registry64 }));
            Assert.That(ProtectedCases().Select(c => (RegistryOperation)c.Arguments[1]).Distinct(), Is.EquivalentTo(Operations));
        }

        /// <summary>
        /// The physical keys of the aliases exist in the fake registry as on Windows: an alias is not a different key
        /// but the CD keys themselves, so refusing it matters.
        /// </summary>
        [Test]
        public void TheAliasesReachTheCdKeysInTheRegistry()
        {
            var registry = new Fakes.InMemoryRegistry();
            var value = RegistryValue.FromString("key");
            registry.Seed(RegistryLocation.LocalMachine32(@"Software\Sierra\CDKeys"), "Empire Earth", value);

            Assert.That(registry.GetValue(RegistryLocation.Parse(@"HKLM64\Software\WOW6432Node\Sierra\CDKeys"), "Empire Earth").Value,
                Is.EqualTo(value));
            Assert.That(registry.GetValue(RegistryLocation.Parse(@"HKLM32\SOFTWARE\SIERRA\CDKEYS\"), "empire earth").Value,
                Is.EqualTo(value));
        }

        /// <summary>
        /// The keys and values the launcher must be able to change stay allowed (the table test is not vacuous). Since L-WP5
        /// the allow-list names the values of the contract tables, so each key is checked with one of its values.
        /// </summary>
        [TestCase(@"HKCU\Software\Neo\Empire Earth", "Wait for VSync")]
        [TestCase(@"HKCU\Software\SSSI\Empire Earth\Game Options", "Map Type")]
        [TestCase(@"HKCU\Software\Mad Doc Software\EE-AOC", "Installed From Directory")]
        [TestCase(@"HKCU\Software\Empire Earth Community\GameDefaults\NeoEE", "AoC")]
        [TestCase(@"HKCU\Software\Microsoft\DirectX\UserGpuPreferences", @"C:\Games\EE\Empire Earth.exe")]
        [TestCase(@"HKCU\Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers", @"C:\Games\EE\Empire Earth.exe")]
        public void ContractKey_StaysAllowed(string location, string valueName)
        {
            RegistryWriteDecision decision = LauncherWritePolicy.Default.Check(RegistryOperation.SetValue,
                RegistryLocation.Parse(location), valueName, RegistryValue.FromString("~ HIGHDPIAWARE"),
                () => RegistryResult<RegistryValue>.Failure(RegistryStatus.Missing, "missing"));

            Assert.That(decision.IsAllowed, Is.True, decision.ToString());
        }

        /// <summary>
        /// The cleanup may delete exactly the listed HKCU keys (L-WP8): their parents, their subkeys and every other spelling
        /// that names a different physical key are refused, and HKLM is refused whatever the list says (contract 4.1).
        /// </summary>
        [Test]
        public void AliasesOfTheCleanupKeys_AreNotDeleted()
        {
            List<CleanupEntry> deletable = CleanupCandidates.All.Where(entry => entry.Scope == CleanupScope.LauncherDeletes).ToList();
            Assert.That(deletable, Is.Not.Empty);
            foreach (CleanupEntry entry in deletable)
            {
                string path = entry.Key.Path;
                Assert.That(LauncherWritePolicy.Default.Check(RegistryOperation.DeleteSubKeyTree, entry.Key).IsAllowed, Is.True, path);
                var refused = new List<Tuple<RegistryLocation, RegistryWriteDenial>>
                {
                    Tuple.Create(entry.Key.Parent, RegistryWriteDenial.NotInAllowList),
                    Tuple.Create(entry.Key.Child("Game Options"), RegistryWriteDenial.NotInAllowList),
                    Tuple.Create(RegistryLocation.CurrentUser(path.Replace('\\', '/')), RegistryWriteDenial.NotInAllowList),
                    Tuple.Create(RegistryLocation.CurrentUser(path.Replace(@"Software\", @"Software\WOW6432Node\")),
                        RegistryWriteDenial.NotInAllowList),
                    Tuple.Create(RegistryLocation.LocalMachine32(path), RegistryWriteDenial.NotCurrentUser),
                    Tuple.Create(RegistryLocation.LocalMachine64(path), RegistryWriteDenial.NotCurrentUser),
                };
                foreach (var key in refused)
                {
                    RegistryWriteDecision decision = LauncherWritePolicy.Default.Check(RegistryOperation.DeleteSubKeyTree, key.Item1);
                    Assert.That(decision.Denial, Is.EqualTo(key.Item2), decision.ToString());
                }
            }
        }

        private static IEnumerable<string> ValueNames(RegistryOperation operation)
        {
            return operation == RegistryOperation.SetValue || operation == RegistryOperation.DeleteValue
                ? new[] { string.Empty, "Empire Earth" }
                : new string[] { null };
        }

        /// <summary>A policy whose allow-list names <paramref name="key"/> with every operation (if it is in HKCU).</summary>
        private static RegistryWritePolicy PolicyThatLists(RegistryLocation key)
        {
            var rules = new List<RegistryWriteRule>
            {
                new RegistryWriteRule(@"Software\Neo\Empire Earth", Operations),
            };
            if (key.Hive == RegistryHive.CurrentUser && !key.IsRoot)
                rules.Add(new RegistryWriteRule(key.Path, Operations));
            return new RegistryWritePolicy(rules);
        }
    }
}

using System;
using System.Linq;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.GameSettings;
using Empire_Earth_Launcher.Core.Maintenance;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Tests.Fakes;
using Microsoft.Win32;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.GameSettings
{
    /// <summary>
    /// The launcher's allow-list narrowed to values (ADR 0007, L-WP5): the values of the contract tables 3.2 and 3.5, the
    /// program paths of 3.4 and 3.7, and the content of the compatibility values (ADR 0007 plan review: <c>WINXPSP3</c>
    /// and <c>RUNASADMIN</c> are never added, other entries are kept, nothing is switched on Windows 7 and under Wine).
    /// </summary>
    [TestFixture]
    public class LauncherWritePolicyTests
    {
        private const string Program = @"C:\Program Files (x86)\Neo Empire Earth\Empire Earth\Empire Earth.exe";

        private static readonly RegistryLocation Layers = RegistryLocation.CurrentUser(ContractNames.CompatibilityLayersKey);
        private static readonly RegistryLocation Gpu = RegistryLocation.CurrentUser(ContractNames.GpuPreferencesKey);

        private static RegistryWriteDecision SetLayers(RegistryWritePolicy policy, string current, string next)
        {
            return policy.Check(RegistryOperation.SetValue, Layers, Program, RegistryValue.FromString(next), Current(current));
        }

        private static RegistryWriteDecision DeleteLayers(RegistryWritePolicy policy, string current)
        {
            return policy.Check(RegistryOperation.DeleteValue, Layers, Program, null, Current(current));
        }

        private static Func<RegistryResult<RegistryValue>> Current(string value)
        {
            return () => value == null
                ? RegistryResult<RegistryValue>.Failure(RegistryStatus.Missing, "missing")
                : RegistryResult<RegistryValue>.Success(RegistryValue.FromString(value));
        }

        // --- Game settings keys and markers --------------------------------------------------------------------------

        [Test]
        public void EveryValueOfTable_3_2_IsAllowedInItsKey()
        {
            foreach (Product product in Product.All)
            {
                foreach (Game game in Game.All)
                {
                    RegistryLocation settings = RegistryLocation.CurrentUser(product.GetGameSettingsKey(game));
                    foreach (GameSetting setting in GameSettingsTable.All)
                    {
                        foreach (RegistryOperation operation in new[] { RegistryOperation.SetValue, RegistryOperation.DeleteValue })
                        {
                            RegistryWriteDecision decision = LauncherWritePolicy.Default.Check(operation, setting.KeyIn(settings),
                                setting.ValueName);
                            Assert.That(decision.IsAllowed, Is.True, decision.ToString());
                        }
                    }
                }
            }
        }

        [TestCase(@"Software\Neo\Empire Earth", "Player Name")]
        [TestCase(@"Software\Neo\Empire Earth", "")]
        [TestCase(@"Software\Neo\Empire Earth", "Map Type")]
        [TestCase(@"Software\Neo\Empire Earth", "Installed From")]
        [TestCase(@"Software\SSSI\Empire Earth\Game Options", "Wait for VSync")]
        [TestCase(@"Software\Mad Doc Software\EE-AOC\Game Options", "Last Map")]
        [TestCase(@"Software\Empire Earth Community\GameDefaults\NeoEE", "NeoEE")]
        [TestCase(@"Software\Empire Earth Community\GameDefaults\EE", "")]
        public void ValuesOutsideTheContractTables_AreRefused(string key, string valueName)
        {
            foreach (RegistryOperation operation in new[] { RegistryOperation.SetValue, RegistryOperation.DeleteValue })
            {
                RegistryWriteDecision decision = LauncherWritePolicy.Default.Check(operation, RegistryLocation.CurrentUser(key), valueName);
                Assert.That(decision.Denial, Is.EqualTo(RegistryWriteDenial.ValueNotAllowed), decision.ToString());
            }
        }

        /// <summary>
        /// The suite record of contract 1.6 (revision 4) is read-only for the launcher: HKLM in either view and, were it in HKCU,
        /// a key that is in no allow-list; no operation on it is allowed.
        /// </summary>
        [TestCase("Products")]
        [TestCase("SourceDir")]
        [TestCase("")]
        public void TheSuiteRecord_IsNeverWritten(string valueName)
        {
            RegistryLocation[] keys =
            {
                new RegistryLocation(RegistryHive.LocalMachine, RegistryView.Registry64, ContractNames.SuiteRecordKey),
                new RegistryLocation(RegistryHive.LocalMachine, RegistryView.Registry32, ContractNames.SuiteRecordKey),
                RegistryLocation.CurrentUser(ContractNames.SuiteRecordKey)
            };
            foreach (RegistryLocation key in keys)
            {
                foreach (RegistryOperation operation in new[]
                         { RegistryOperation.SetValue, RegistryOperation.DeleteValue, RegistryOperation.CreateSubKey, RegistryOperation.DeleteSubKeyTree })
                    Assert.That(LauncherWritePolicy.Default.Check(operation, key, valueName).IsAllowed, Is.False, key + " " + operation);
            }
        }

        [TestCase("EE")]
        [TestCase("AoC")]
        [TestCase("aoc")]
        public void Markers_EEAndAoC(string valueName)
        {
            foreach (Product product in Product.All)
                Assert.That(LauncherWritePolicy.Default.Check(RegistryOperation.SetValue,
                    RegistryLocation.CurrentUser(product.DefaultsMarkerKey), valueName).IsAllowed, Is.True);
        }

        /// <summary>
        /// Keys of the value rules may be created; the only keys that may be deleted are those of the cleanup list the launcher
        /// deletes (L-WP8): the four game settings keys of contract 3.1 and the four registry VirtualStore copies of the SSSI
        /// and Mad Doc keys, never <c>Game Options</c>, a marker, <c>UserGpuPreferences</c> or <c>Layers</c>.
        /// </summary>
        [Test]
        public void KeysMayBeCreated_OnlyTheKeysOfTheCleanupListMayBeDeleted()
        {
            var deletable = CleanupCandidates.All.Where(entry => entry.Scope == CleanupScope.LauncherDeletes)
                                             .Select(entry => entry.Key).ToList();
            Assert.That(deletable, Has.Count.EqualTo(8));
            foreach (RegistryWriteRule rule in LauncherWritePolicy.Default.AllowList)
            {
                if (rule.Operations.Contains(RegistryOperation.CreateSubKey))
                    Assert.That(LauncherWritePolicy.Default.Check(RegistryOperation.CreateSubKey, rule.Key).IsAllowed, Is.True, rule.ToString());
                RegistryWriteDenial expected = deletable.Contains(rule.Key) ? RegistryWriteDenial.None : RegistryWriteDenial.NotInAllowList;
                Assert.That(LauncherWritePolicy.Default.Check(RegistryOperation.DeleteSubKeyTree, rule.Key).Denial,
                    Is.EqualTo(expected), rule.ToString());
            }
            foreach (RegistryLocation key in deletable)
            {
                Assert.That(LauncherWritePolicy.Default.Check(RegistryOperation.DeleteSubKeyTree, key).IsAllowed, Is.True, key.ToString());
                Assert.That(LauncherWritePolicy.WithoutLayerEntries.Check(RegistryOperation.DeleteSubKeyTree, key).IsAllowed, Is.True,
                    "Windows 7: " + key);
                Assert.That(LauncherWritePolicy.Default.Check(RegistryOperation.DeleteSubKeyTree, key.Child("Game Options")).Denial,
                    Is.EqualTo(RegistryWriteDenial.NotInAllowList), "only the listed key itself, " + key);
            }
        }

        // --- Program paths (GPU preference, compatibility values) ----------------------------------------------------

        [TestCase(Program)]
        [TestCase(@"C:\Program Files (x86)\Neo Empire Earth\Empire Earth - The Art of Conquest\EE-AOC.exe")]
        [TestCase(@"D:\Empire Earth\Empire Earth.exe")]
        [TestCase(@"C:\Games\AoC\ee-aoc.EXE")]
        [TestCase(@"\\server\games\Empire Earth\Empire Earth.exe")]
        public void GpuPreference_3_4_ProgramPathsAreAllowed(string valueName)
        {
            RegistryWriteDecision decision = LauncherWritePolicy.Default.Check(RegistryOperation.SetValue, Gpu, valueName,
                RegistryValue.FromString("GpuPreference=2;"));

            Assert.That(decision.IsAllowed, Is.True, decision.ToString());
        }

        [TestCase(@"C:\Windows\notepad.exe")]
        [TestCase("Empire Earth.exe")]
        [TestCase(@"Games\EE\Empire Earth.exe")]
        [TestCase(@"C:\Games\EE\..\EE\Empire Earth.exe")]
        [TestCase(@"C:\Games\EE\Empire Earth.exe\")]
        [TestCase(@"C:/Games/EE/Empire Earth.exe")]
        [TestCase(@"C:\Games\EE\Empire Earth.exe.bak")]
        [TestCase("")]
        public void GpuPreferenceAndLayers_OtherNamesAreRefused(string valueName)
        {
            Assert.That(LauncherWritePolicy.Default.Check(RegistryOperation.SetValue, Gpu, valueName,
                RegistryValue.FromString("GpuPreference=2;")).Denial, Is.EqualTo(RegistryWriteDenial.ValueNotAllowed));
            Assert.That(LauncherWritePolicy.Default.Check(RegistryOperation.SetValue, Layers, valueName,
                RegistryValue.FromString("~ HIGHDPIAWARE"), Current(null)).Denial, Is.EqualTo(RegistryWriteDenial.ValueNotAllowed));
        }

        // --- Layer content (contract 3.7, ADR 0007 plan review) ------------------------------------------------------

        [TestCase(null, "~ HIGHDPIAWARE")]
        [TestCase(null, "~ DWM8And16BitMitigation HIGHDPIAWARE HeapClearAllocation WIN7RTM")]
        [TestCase("~ HIGHDPIAWARE", "~ HIGHDPIAWARE WIN7RTM")]
        [TestCase("~ RUNASADMIN", "~ RUNASADMIN HIGHDPIAWARE")]
        [TestCase("~ RUNASADMIN DISABLEDXMAXIMIZEDWINDOWEDMODE", "~ RUNASADMIN HeapClearAllocation DISABLEDXMAXIMIZEDWINDOWEDMODE")]
        [TestCase("~ RUNASADMIN HIGHDPIAWARE WIN7RTM", "~ RUNASADMIN WIN7RTM")]
        [TestCase("~ HIGHDPIAWARE", "~")]
        [TestCase("$ ~ 16BITCOLOR", "$ ~ 16BITCOLOR HIGHDPIAWARE")]
        public void Layers_AddingOrRemovingTheEntriesOfTheContractRows_IsAllowed(string current, string next)
        {
            RegistryWriteDecision decision = SetLayers(LauncherWritePolicy.Default, current, next);

            Assert.That(decision.IsAllowed, Is.True, decision.ToString());
        }

        [TestCase(null, "~ WINXPSP3")]
        [TestCase(null, "~ HIGHDPIAWARE WINXPSP3")]
        [TestCase("~ HIGHDPIAWARE", "~ HIGHDPIAWARE WINXPSP3")]
        [TestCase("~ DISABLEDXMAXIMIZEDWINDOWEDMODE", "~ DISABLEDXMAXIMIZEDWINDOWEDMODE HIGHDPIAWARE WINXPSP3")]
        [TestCase(null, "~ RUNASADMIN")]
        [TestCase(null, "~ RUNASADMIN HIGHDPIAWARE")]
        [TestCase("~ HIGHDPIAWARE", "~ RUNASADMIN HIGHDPIAWARE")]
        [TestCase("~ DISABLEDXMAXIMIZEDWINDOWEDMODE", "~ RUNASADMIN DISABLEDXMAXIMIZEDWINDOWEDMODE WIN7RTM")]
        [TestCase(null, "~ WIN8RTM")]
        public void Layers_WINXPSP3AndRUNASADMINAreNeverAdded(string current, string next)
        {
            RegistryWriteDecision decision = SetLayers(LauncherWritePolicy.Default, current, next);

            Assert.That(decision.Denial, Is.EqualTo(RegistryWriteDenial.LayerContent), decision.ToString());
        }

        [TestCase("~ RUNASADMIN HIGHDPIAWARE", "~ HIGHDPIAWARE")]
        [TestCase("~ WINXPSP3", "~")]
        [TestCase("~ WINXPSP3 RUNASADMIN", "~ RUNASADMIN WINXPSP3 HIGHDPIAWARE")]
        [TestCase("~ HIGHDPIAWARE", "HIGHDPIAWARE")]
        [TestCase("~ runasadmin", "~ RUNASADMIN HIGHDPIAWARE")]
        public void Layers_OtherEntriesAndThePrefixAreKeptAsTheyAre(string current, string next)
        {
            Assert.That(SetLayers(LauncherWritePolicy.Default, current, next).Denial, Is.EqualTo(RegistryWriteDenial.LayerContent));
        }

        [Test]
        public void Layers_AreRefusedWhenTheValuesAreNoStringsOrTheCurrentOneIsUnknown()
        {
            RegistryWritePolicy policy = LauncherWritePolicy.Default;

            Assert.That(policy.Check(RegistryOperation.SetValue, Layers, Program, RegistryValue.FromDWord(1), Current(null)).Denial,
                Is.EqualTo(RegistryWriteDenial.LayerContent));
            Assert.That(policy.Check(RegistryOperation.SetValue, Layers, Program, RegistryValue.FromString("~ HIGHDPIAWARE")).Denial,
                Is.EqualTo(RegistryWriteDenial.LayerContent), "no reader of the current value");
            Assert.That(policy.Check(RegistryOperation.SetValue, Layers, Program, RegistryValue.FromString("~ HIGHDPIAWARE"),
                () => RegistryResult<RegistryValue>.Failure(RegistryStatus.AccessDenied, "denied")).Denial,
                Is.EqualTo(RegistryWriteDenial.LayerContent));
            Assert.That(policy.Check(RegistryOperation.SetValue, Layers, Program, RegistryValue.FromString("~ HIGHDPIAWARE"),
                () => RegistryResult<RegistryValue>.Success(RegistryValue.FromDWord(1))).Denial,
                Is.EqualTo(RegistryWriteDenial.LayerContent));
        }

        [TestCase(null)]
        [TestCase("~ RUNASADMIN")]
        [TestCase("~ HIGHDPIAWARE WIN7RTM")]
        [TestCase("~")]
        public void Layers_DeletingIsAllowedWhenNothingElseIsInTheValue(string current)
        {
            Assert.That(DeleteLayers(LauncherWritePolicy.Default, current).IsAllowed, Is.True);
        }

        [TestCase("~ RUNASADMIN HIGHDPIAWARE")]
        [TestCase("~ WINXPSP3")]
        [TestCase("~ DWM8And16BitMitigation HIGHDPIAWARE HeapClearAllocation WINXPSP3")]
        [TestCase("~  RUNASADMIN")]
        [TestCase("~ runasadmin")]
        public void Layers_DeletingAValueWithOtherEntriesIsRefused(string current)
        {
            Assert.That(DeleteLayers(LauncherWritePolicy.Default, current).Denial, Is.EqualTo(RegistryWriteDenial.LayerContent));
        }

        /// <summary>Windows 7 and Wine: no entry is switched; only an HKCU value that is exactly <c>~ RUNASADMIN</c> may go.</summary>
        [Test]
        public void Layers_OnWindows7OnlyRunAsAdminMayBeRemoved()
        {
            RegistryWritePolicy policy = LauncherWritePolicy.WithoutLayerEntries;

            Assert.That(SetLayers(policy, null, "~ HIGHDPIAWARE").Denial, Is.EqualTo(RegistryWriteDenial.LayerContent));
            Assert.That(SetLayers(policy, "~ RUNASADMIN", "~ RUNASADMIN WIN7RTM").Denial, Is.EqualTo(RegistryWriteDenial.LayerContent));
            Assert.That(SetLayers(policy, "~ HIGHDPIAWARE", "~").Denial, Is.EqualTo(RegistryWriteDenial.LayerContent));
            Assert.That(DeleteLayers(policy, "~ RUNASADMIN").IsAllowed, Is.True);
            Assert.That(DeleteLayers(policy, "~ HIGHDPIAWARE").Denial, Is.EqualTo(RegistryWriteDenial.LayerContent));
            Assert.That(DeleteLayers(policy, "~ WINXPSP3").Denial, Is.EqualTo(RegistryWriteDenial.LayerContent),
                "removing the old values is the setup's job (ARCHITECTURE 14)");
        }

        [Test]
        public void For_ChoosesThePolicyByWindowsVersionAndWine()
        {
            Assert.That(LauncherWritePolicy.For(FakeSystemInfo.Windows7()), Is.SameAs(LauncherWritePolicy.WithoutLayerEntries));
            Assert.That(LauncherWritePolicy.For(new FakeSystemInfo { WindowsVersion = new Version(6, 2) }), Is.SameAs(LauncherWritePolicy.Default));
            Assert.That(LauncherWritePolicy.For(FakeSystemInfo.Windows81()), Is.SameAs(LauncherWritePolicy.Default));
            Assert.That(LauncherWritePolicy.For(new FakeSystemInfo()), Is.SameAs(LauncherWritePolicy.Default));
            Assert.That(LauncherWritePolicy.For(new FakeSystemInfo { IsWine = true }), Is.SameAs(LauncherWritePolicy.WithoutLayerEntries));
        }

        [Test]
        public void TheSwitchableEntriesAreTheRowsCompatibilityAndCompatibilityWindows()
        {
            RegistryWriteRule layers = LauncherWritePolicy.Default.AllowList.Single(rule => rule.LayerEntries != null);

            Assert.That(layers.LayerEntries, Is.EqualTo(new[] { "DWM8And16BitMitigation", "HIGHDPIAWARE", "HeapClearAllocation", "WIN7RTM" }));
            Assert.That(LauncherWritePolicy.WithoutLayerEntries.AllowList.Single(rule => rule.LayerEntries != null).LayerEntries, Is.Empty);
        }

        /// <summary>The wrapper reads the current value itself, so the content check runs for every real change.</summary>
        [Test]
        public void PolicyCheckedRegistry_ChecksTheContentAgainstTheRegistry()
        {
            var inner = new InMemoryRegistry();
            inner.Seed(Layers, Program, RegistryValue.FromString("~ RUNASADMIN"));
            var registry = new PolicyCheckedRegistry(inner, LauncherWritePolicy.Default);

            Assert.That(registry.SetValue(Layers, Program, RegistryValue.FromString("~ RUNASADMIN HIGHDPIAWARE")).IsOk, Is.True);
            Assert.That(() => registry.SetValue(Layers, Program, RegistryValue.FromString("~ HIGHDPIAWARE")),
                Throws.TypeOf<RegistryWriteDeniedException>(), "RUNASADMIN removed");
            Assert.That(() => registry.SetValue(Layers, Program, RegistryValue.FromString("~ RUNASADMIN HIGHDPIAWARE WINXPSP3")),
                Throws.TypeOf<RegistryWriteDeniedException>());
            Assert.That(() => registry.DeleteValue(Layers, Program), Throws.TypeOf<RegistryWriteDeniedException>());
            Assert.That(inner.GetValue(Layers, Program).Value, Is.EqualTo(RegistryValue.FromString("~ RUNASADMIN HIGHDPIAWARE")));
        }
    }
}

using System;
using System.Linq;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.GameSettings;
using Empire_Earth_Launcher.Core.Platform;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.Platform
{
    /// <summary>
    /// The allow-list part of <see cref="RegistryWritePolicy"/> and the order of its rules (ADR 0007), with the launcher's
    /// allow-list (<see cref="LauncherWritePolicy"/>) at key level. The protected keys under all their aliases are checked
    /// by <c>Architecture/RegistryAliasPolicyTests</c>, the value names and the layer content by
    /// <c>Core/GameSettings/LauncherWritePolicyTests</c>.
    /// </summary>
    [TestFixture]
    public class RegistryWritePolicyTests
    {
        private static readonly RegistryOperation[] AllOperations =
            (RegistryOperation[])Enum.GetValues(typeof(RegistryOperation));

        /// <summary>
        /// Checks the operation with a value name the allow-list knows for that key (a value of the contract tables, a game
        /// program path), so that only the key decides; for compatibility values the current value is missing.
        /// </summary>
        private static RegistryWriteDecision Check(RegistryOperation operation, string location)
        {
            RegistryLocation key = RegistryLocation.Parse(location);
            string valueName = null;
            if (operation == RegistryOperation.SetValue || operation == RegistryOperation.DeleteValue)
            {
                string path = key.Path.ToUpperInvariant();
                valueName = path.EndsWith(@"\GAME OPTIONS", StringComparison.Ordinal) ? "Map Type"
                    : path.Contains(@"\GAMEDEFAULTS\") ? "EE"
                    : path.EndsWith("USERGPUPREFERENCES", StringComparison.Ordinal) || path.EndsWith("LAYERS", StringComparison.Ordinal)
                        ? @"C:\Games\EE\Empire Earth.exe"
                        : "Wait for VSync";
            }
            return LauncherWritePolicy.Default.Check(operation, key, valueName, RegistryValue.FromString("~ HIGHDPIAWARE"),
                () => RegistryResult<RegistryValue>.Failure(RegistryStatus.Missing, "missing"));
        }

        [Test]
        public void Default_AllowsTheContractKeys()
        {
            var keys = Product.All.SelectMany(product => Game.All.SelectMany(game => new[]
                {
                    product.GetGameSettingsKey(game),
                    product.GetGameSettingsKey(game) + @"\" + ContractNames.GameOptionsSubKeyName,
                }))
                .Concat(Product.All.Select(product => product.DefaultsMarkerKey))
                .Concat(new[] { ContractNames.GpuPreferencesKey, ContractNames.CompatibilityLayersKey })
                .ToList();

            Assert.That(keys, Has.Count.EqualTo(12), "4 settings keys, their Game Options, 2 markers, GPU, layers");
            foreach (string key in keys)
            {
                foreach (RegistryOperation operation in new[] { RegistryOperation.SetValue, RegistryOperation.DeleteValue, RegistryOperation.CreateSubKey })
                    Assert.That(Check(operation, @"HKCU\" + key).IsAllowed, Is.True, operation + " " + key);
                Assert.That(Check(RegistryOperation.DeleteSubKeyTree, @"HKCU\" + key).Denial,
                    Is.EqualTo(RegistryWriteDenial.NotInAllowList), "no key of the contract is ever deleted: " + key);
            }
        }

        [TestCase(@"HKCU\SOFTWARE\NEO\EMPIRE EARTH")]
        [TestCase(@"HKCU\software\mad doc software\ee-aoc\game options")]
        [TestCase(@"HKCU\\Software\\SSSI\Empire Earth\")]
        public void Default_AllowsOtherSpellingsOfTheSamePhysicalKey(string location)
        {
            Assert.That(Check(RegistryOperation.SetValue, location).IsAllowed, Is.True);
        }

        [TestCase(@"HKCU\Software\Neo/Empire Earth")]
        [TestCase(@"HKCU\Software\WOW6432Node\Neo\Empire Earth")]
        [TestCase(@"HKCU\Software\Classes\VirtualStore\MACHINE\SOFTWARE\Neo\Empire Earth")]
        [TestCase(@"HKCU\Software\Neo\Empire Earth\Other Subkey")]
        [TestCase(@"HKCU\Software\Neo")]
        [TestCase(@"HKCU\Software\Empire Earth Community\GameDefaults")]
        [TestCase(@"HKCU\Software\Empire Earth Community\GameDefaults\Other")]
        [TestCase(@"HKCU\Software\Microsoft\DirectX")]
        public void Default_RefusesKeysAndAliasesThatAreNotListed(string location)
        {
            foreach (RegistryOperation operation in AllOperations)
                Assert.That(Check(operation, location).Denial, Is.EqualTo(RegistryWriteDenial.NotInAllowList), operation.ToString());
        }

        [TestCase(@"HKLM64\Software\Neo\Empire Earth")]
        [TestCase(@"HKLM32\Software\SSSI\Empire Earth")]
        [TestCase(@"HKLM64\Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers")]
        [TestCase(@"HKU\S-1-5-21-1\Software\Neo\Empire Earth")]
        [TestCase(@"HKCR\.eem")]
        public void Default_RefusesEveryHiveButHkcu(string location)
        {
            foreach (RegistryOperation operation in AllOperations)
                Assert.That(Check(operation, location).Denial, Is.EqualTo(RegistryWriteDenial.NotCurrentUser), operation.ToString());
        }

        [Test]
        public void ProtectedKeys_WinOverTheAllowList()
        {
            // An allow-list that names the protected keys exactly must not open them.
            var policy = new RegistryWritePolicy(new[]
            {
                new RegistryWriteRule(ContractNames.CdKeysKey, AllOperations),
                new RegistryWriteRule(Product.NeoEE.InstallRecordKey, AllOperations),
                new RegistryWriteRule(ContractNames.UninstallKey, AllOperations),
                new RegistryWriteRule("Software", AllOperations),
            });

            foreach (RegistryOperation operation in AllOperations)
            {
                Assert.That(policy.Check(operation, RegistryLocation.CurrentUser(ContractNames.CdKeysKey)).Denial,
                    Is.EqualTo(RegistryWriteDenial.CdKeys));
                Assert.That(policy.Check(operation, RegistryLocation.CurrentUser(Product.NeoEE.InstallRecordKey)).Denial,
                    Is.EqualTo(RegistryWriteDenial.InstallRecord));
                Assert.That(policy.Check(operation, RegistryLocation.CurrentUser(ContractNames.UninstallKey)).Denial,
                    Is.EqualTo(RegistryWriteDenial.UninstallKey));
                Assert.That(policy.Check(operation, RegistryLocation.CurrentUser("Software")).Denial,
                    Is.EqualTo(RegistryWriteDenial.CdKeys), "an ancestor of the CD keys");
            }
        }

        [Test]
        public void Decision_DescribesItself()
        {
            RegistryWriteDecision decision = LauncherWritePolicy.Default.Check(RegistryOperation.DeleteSubKeyTree,
                RegistryLocation.Parse(@"HKLM64\Software\WOW6432Node\Sierra"));

            Assert.That(decision.IsAllowed, Is.False);
            Assert.That(decision.CanonicalKey.ToString(), Is.EqualTo(@"HKLM32\SOFTWARE\SIERRA"));
            Assert.That(decision.ToString(), Is.EqualTo(
                @"DeleteSubKeyTree HKLM64\Software\WOW6432Node\Sierra refused: CdKeys (canonical HKLM32\SOFTWARE\SIERRA)"));
        }

        [Test]
        public void Rule_NeedsAKeyAndAnOperation()
        {
            Assert.That(() => new RegistryWriteRule("", RegistryOperation.SetValue), Throws.ArgumentException);
            Assert.That(() => new RegistryWriteRule(@"Software\Neo"), Throws.ArgumentException);
            Assert.That(() => new RegistryWritePolicy(new RegistryWriteRule[] { null }), Throws.ArgumentException);
        }
    }
}

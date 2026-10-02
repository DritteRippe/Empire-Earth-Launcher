using System.Collections.Generic;
using System.Linq;
using Empire_Earth_Launcher.Core.Backup;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Tests.Fakes;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.Backup
{
    /// <summary><see cref="RegistryExport"/>: a key with its subkeys, complete or not at all (ADR 0007, contract 3.6).</summary>
    [TestFixture]
    public class RegistryExportTests
    {
        private static readonly RegistryLocation Settings = RegistryLocation.CurrentUser(@"Software\Neo\Empire Earth");

        [Test]
        public void ReadTree_ExportsTheKeyAndItsSubkeysWithEveryValue()
        {
            var registry = new InMemoryRegistry();
            registry.Seed(Settings, "Rasterizer Name", RegistryValue.FromString("Direct3D"));
            registry.Seed(Settings, string.Empty, RegistryValue.FromString("default"));
            registry.Seed(Settings.Child("Game Options"), "Map Size", RegistryValue.FromDWord(2));
            registry.Seed(Settings.Child(@"Game Options\Deeper"), "Raw", RegistryValue.FromBinary(new byte[] { 1 }));
            registry.SeedKey(Settings.Child("Empty"));

            RegistryResult<IReadOnlyList<RegFileKey>> result = RegistryExport.ReadTree(new WriteForbiddingRegistry(registry), Settings);

            Assert.That(result.IsOk, Is.True, result.ToString());
            Assert.That(result.Value.Select(key => key.Key.ToString()), Is.EqualTo(new[]
            {
                @"HKCU\Software\Neo\Empire Earth", @"HKCU\Software\Neo\Empire Earth\Empty",
                @"HKCU\Software\Neo\Empire Earth\Game Options", @"HKCU\Software\Neo\Empire Earth\Game Options\Deeper"
            }));
            Assert.That(result.Value[0].Values.Select(value => value.Key), Is.EquivalentTo(new[] { string.Empty, "Rasterizer Name" }));
            Assert.That(result.Value[2].Values.Single().Value, Is.EqualTo(RegistryValue.FromDWord(2)));
            Assert.That(result.Value[1].Values, Is.Empty);
        }

        [Test]
        public void ReadTree_OfAMissingKeyIsEmpty()
        {
            RegistryResult<IReadOnlyList<RegFileKey>> result = RegistryExport.ReadTree(new InMemoryRegistry(), Settings);

            Assert.That(result.IsOk, Is.True);
            Assert.That(result.Value, Is.Empty);
        }

        [TestCase(RegistryStatus.AccessDenied)]
        [TestCase(RegistryStatus.IoError)]
        public void ReadTree_FailsAsAWholeIfASubkeyCannotBeRead(RegistryStatus status)
        {
            var registry = new InMemoryRegistry();
            registry.Seed(Settings, "Rasterizer Name", RegistryValue.FromString("Direct3D"));
            registry.Seed(Settings.Child("Game Options"), "Map Size", RegistryValue.FromDWord(2));
            registry.SetFault(Settings.Child("Game Options"), status);

            RegistryResult<IReadOnlyList<RegFileKey>> result = RegistryExport.ReadTree(registry, Settings);

            Assert.That(result.Status, Is.EqualTo(status));
            Assert.That(result.Detail, Does.Contain("Game Options"));
        }
    }
}

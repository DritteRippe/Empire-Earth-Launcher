using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.GameSettings;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Tests.Fakes;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.Platform
{
    /// <summary>Every change passes the write policy before it reaches the registry (<see cref="PolicyCheckedRegistry"/>).</summary>
    [TestFixture]
    public class PolicyCheckedRegistryTests
    {
        private static readonly RegistryLocation CdKeys = RegistryLocation.LocalMachine32(ContractNames.CdKeysKey);
        private static readonly RegistryLocation NeoSettings = RegistryLocation.CurrentUser(@"Software\Neo\Empire Earth");

        private InMemoryRegistry inner;
        private PolicyCheckedRegistry registry;

        [SetUp]
        public void SetUp()
        {
            inner = new InMemoryRegistry();
            inner.Seed(CdKeys, "Empire Earth", RegistryValue.FromString("not logged, not shown"));
            registry = new PolicyCheckedRegistry(inner, LauncherWritePolicy.Default);
        }

        [Test]
        public void AllowedChanges_ReachTheRegistry()
        {
            Assert.That(registry.CreateSubKey(NeoSettings).IsOk, Is.True);
            Assert.That(registry.SetValue(NeoSettings, "Game Bit Depth", RegistryValue.FromDWord(32)).IsOk, Is.True);
            Assert.That(registry.DeleteValue(NeoSettings, "Game Bit Depth").IsOk, Is.True);

            Assert.That(inner.Changes, Has.Count.EqualTo(3));
        }

        [Test]
        public void RefusedChanges_ThrowAndChangeNothing()
        {
            var alias = RegistryLocation.Parse(@"HKLM64\Software\WOW6432Node\Sierra\CDKeys");

            var denied = Assert.Throws<RegistryWriteDeniedException>(() => registry.DeleteSubKeyTree(alias));
            Assert.That(denied.Decision.Denial, Is.EqualTo(RegistryWriteDenial.CdKeys));
            Assert.That(() => registry.SetValue(alias, "Empire Earth", RegistryValue.FromString("x")),
                Throws.TypeOf<RegistryWriteDeniedException>());
            Assert.That(() => registry.DeleteValue(CdKeys, "Empire Earth"), Throws.TypeOf<RegistryWriteDeniedException>());
            Assert.That(() => registry.CreateSubKey(CdKeys.Child("New")), Throws.TypeOf<RegistryWriteDeniedException>());
            Assert.That(() => registry.CreateSubKey(RegistryLocation.CurrentUser(@"Software\Other")),
                Throws.TypeOf<RegistryWriteDeniedException>());

            Assert.That(inner.Changes, Is.Empty);
            Assert.That(inner.GetValue(CdKeys, "Empire Earth").IsOk, Is.True);
        }

        [Test]
        public void Reads_AreNotRestricted()
        {
            // The diagnostics may check that the CD keys exist (contract 3.8).
            Assert.That(registry.ProbeKey(CdKeys).IsOk, Is.True);
            Assert.That(registry.GetValueNames(CdKeys).Value, Is.EqualTo(new[] { "Empire Earth" }));
            Assert.That(registry.GetSubKeyNames(RegistryLocation.LocalMachine32(@"Software\Sierra")).Value, Is.EqualTo(new[] { "CDKeys" }));
            Assert.That(registry.GetValue(CdKeys, "Empire Earth").IsOk, Is.True);
        }
    }
}

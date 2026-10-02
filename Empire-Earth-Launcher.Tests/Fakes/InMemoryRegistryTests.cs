using Empire_Earth_Launcher.Core.Platform;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Fakes
{
    /// <summary>
    /// The in-memory registry has the views of Windows (contract 0): separate HKLM views on 64-bit Windows, one on
    /// 32-bit Windows, a shared HKCU, the <c>WOW6432Node</c> redirection, and injectable errors. Later tests rely on
    /// it, so the fake has its own tests.
    /// </summary>
    [TestFixture]
    public class InMemoryRegistryTests
    {
        private static readonly RegistryValue Root = RegistryValue.FromString(@"C:\Program Files (x86)\Neo Empire Earth");

        [Test]
        public void Bit64_TheViewsOfHklmAreSeparate()
        {
            var registry = new InMemoryRegistry();
            registry.Seed(RegistryLocation.LocalMachine64(@"Software\Empire Earth Community\Installations\NeoEE"), "InstallPath", Root);

            Assert.That(registry.GetValue(RegistryLocation.LocalMachine64(@"Software\Empire Earth Community\Installations\NeoEE"), "InstallPath").Value,
                Is.EqualTo(Root));
            Assert.That(registry.GetValue(RegistryLocation.LocalMachine32(@"Software\Empire Earth Community\Installations\NeoEE"), "InstallPath").Status,
                Is.EqualTo(RegistryStatus.Missing));
        }

        [Test]
        public void Bit64_Wow6432NodeIsThe32BitView()
        {
            var registry = new InMemoryRegistry();
            registry.Seed(RegistryLocation.LocalMachine32(@"Software\SSSI\Empire Earth"), "Installed From Volume", RegistryValue.FromString("C:"));

            Assert.That(registry.GetValue(RegistryLocation.LocalMachine64(@"SOFTWARE\Wow6432Node\SSSI\Empire Earth"), "Installed From Volume").IsOk,
                Is.True);
            Assert.That(registry.GetValue(RegistryLocation.LocalMachine32(@"Software\WOW6432Node\SSSI\Empire Earth"), "Installed From Volume").IsOk,
                Is.True, "the 32-bit view does not nest WOW6432Node");
            Assert.That(registry.ProbeKey(RegistryLocation.LocalMachine64("Software")).Status, Is.EqualTo(RegistryStatus.Missing),
                "nothing in the 64-bit view itself");
        }

        [Test]
        public void Bit32_BothViewsAreOneHklm()
        {
            var registry = new InMemoryRegistry(is32BitWindows: true);
            registry.Seed(RegistryLocation.LocalMachine64(@"Software\SSSI\Empire Earth"), "Installed From Volume", RegistryValue.FromString("C:"));

            Assert.That(registry.Is32BitWindows, Is.True);
            Assert.That(registry.GetValue(RegistryLocation.LocalMachine32(@"Software\SSSI\Empire Earth"), "Installed From Volume").IsOk, Is.True);
            Assert.That(registry.GetSubKeyNames(RegistryLocation.LocalMachine32("Software")).Value, Is.EqualTo(new[] { "SSSI" }));
            Assert.That(registry.ProbeKey(RegistryLocation.LocalMachine64(@"Software\WOW6432Node\SSSI")).Status,
                Is.EqualTo(RegistryStatus.Missing), "no redirection on 32-bit Windows");
        }

        [Test]
        public void CurrentUser_IsSharedByBothViews()
        {
            var registry = new InMemoryRegistry();
            var view32 = new RegistryLocation(Microsoft.Win32.RegistryHive.CurrentUser, Microsoft.Win32.RegistryView.Registry32, @"Software\Neo\Empire Earth");
            var view64 = new RegistryLocation(Microsoft.Win32.RegistryHive.CurrentUser, Microsoft.Win32.RegistryView.Registry64, @"Software\Neo\Empire Earth");
            registry.Seed(view32, "Game Bit Depth", RegistryValue.FromDWord(32));

            Assert.That(registry.GetValue(view64, "game bit depth").Value, Is.EqualTo(RegistryValue.FromDWord(32)));
        }

        [Test]
        public void NamesIgnoreCase_AndKeepTheirSpelling()
        {
            var registry = new InMemoryRegistry();
            registry.Seed(RegistryLocation.CurrentUser(@"Software\Mad Doc Software\EE-AOC"), "Music Volume", RegistryValue.FromDWord(44));

            Assert.That(registry.GetValue(RegistryLocation.CurrentUser(@"SOFTWARE\mad doc software\ee-aoc"), "MUSIC VOLUME").IsOk, Is.True);
            Assert.That(registry.GetSubKeyNames(RegistryLocation.CurrentUser("software")).Value, Is.EqualTo(new[] { "Mad Doc Software" }));
            Assert.That(registry.GetValueNames(RegistryLocation.CurrentUser(@"Software\Mad Doc Software\EE-AOC")).Value,
                Is.EqualTo(new[] { "Music Volume" }));
        }

        [Test]
        public void Writes_FollowTheRulesOfIRegistry()
        {
            var registry = new InMemoryRegistry();
            RegistryLocation key = RegistryLocation.CurrentUser(@"Software\Neo\Empire Earth\Game Options");

            Assert.That(registry.SetValue(key, "Map Type", RegistryValue.FromString("Continental")).Status, Is.EqualTo(RegistryStatus.Missing),
                "SetValue needs an existing key");
            Assert.That(registry.CreateSubKey(key).IsOk, Is.True);
            Assert.That(registry.CreateSubKey(key).IsOk, Is.True, "an existing key is fine");
            Assert.That(registry.SetValue(key, "Map Type", RegistryValue.FromString("Continental")).IsOk, Is.True);
            Assert.That(registry.SetValue(key, "map type", RegistryValue.FromDWord(2)).IsOk, Is.True, "another type replaces it");
            Assert.That(registry.GetValueNames(key).Value, Is.EqualTo(new[] { "Map Type" }));
            Assert.That(registry.DeleteValue(key, "Map Type").IsOk, Is.True);
            Assert.That(registry.DeleteValue(key, "Map Type").Status, Is.EqualTo(RegistryStatus.Missing));
            Assert.That(registry.DeleteSubKeyTree(RegistryLocation.CurrentUser(@"Software\Neo")).IsOk, Is.True);
            Assert.That(registry.ProbeKey(key).Status, Is.EqualTo(RegistryStatus.Missing));
            Assert.That(registry.DeleteSubKeyTree(RegistryLocation.CurrentUser(@"Software\Neo")).Status, Is.EqualTo(RegistryStatus.Missing));

            Assert.That(registry.Changes, Is.EqualTo(new[]
            {
                @"CreateSubKey HKCU\Software\Neo\Empire Earth\Game Options",
                @"SetValue HKCU\Software\Neo\Empire Earth\Game Options @""Map Type"" = REG_SZ ""Continental""",
                @"SetValue HKCU\Software\Neo\Empire Earth\Game Options @""map type"" = REG_DWORD 0x00000002 (2)",
                @"DeleteValue HKCU\Software\Neo\Empire Earth\Game Options @""Map Type""",
                @"DeleteSubKeyTree HKCU\Software\Neo",
            }));
        }

        [Test]
        public void Hives_CannotBeCreatedOrDeleted()
        {
            var registry = new InMemoryRegistry();

            Assert.That(() => registry.DeleteSubKeyTree(RegistryLocation.CurrentUser("")), Throws.ArgumentException);
            Assert.That(() => registry.CreateSubKey(RegistryLocation.LocalMachine64("")), Throws.ArgumentException);
        }

        [Test]
        public void InjectedFaults_ApplyToTheKeyAndBelow_AlsoThroughAnAlias()
        {
            var registry = new InMemoryRegistry();
            registry.Seed(RegistryLocation.LocalMachine32(@"Software\Sierra\CDKeys"), "x", RegistryValue.FromDWord(1));
            registry.SetFault(RegistryLocation.LocalMachine32(@"Software\Sierra"), RegistryStatus.AccessDenied);

            Assert.That(registry.GetValue(RegistryLocation.LocalMachine32(@"Software\Sierra\CDKeys"), "x").Status,
                Is.EqualTo(RegistryStatus.AccessDenied));
            Assert.That(registry.ProbeKey(RegistryLocation.LocalMachine64(@"Software\WOW6432Node\Sierra")).Status,
                Is.EqualTo(RegistryStatus.AccessDenied));
            Assert.That(registry.ProbeKey(RegistryLocation.LocalMachine32("Software")).IsOk, Is.True);
        }

        [Test]
        public void InjectedWriteFaults_LeaveReadsWorking()
        {
            var registry = new InMemoryRegistry();
            RegistryLocation key = RegistryLocation.LocalMachine64(@"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers");
            registry.Seed(key, @"C:\Games\EE\Empire Earth.exe", RegistryValue.FromString("~ WIN7RTM"));
            registry.SetFault(key, RegistryStatus.AccessDenied, writesOnly: true);

            Assert.That(registry.GetValue(key, @"C:\Games\EE\Empire Earth.exe").IsOk, Is.True);
            Assert.That(registry.SetValue(key, "x", RegistryValue.FromString("~")).Status, Is.EqualTo(RegistryStatus.AccessDenied));
            Assert.That(registry.DeleteValue(key, @"C:\Games\EE\Empire Earth.exe").Status, Is.EqualTo(RegistryStatus.AccessDenied));
            Assert.That(registry.Changes, Is.Empty);
        }
    }
}

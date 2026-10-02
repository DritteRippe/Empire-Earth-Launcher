using System.Linq;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Tests.Fakes;
using Empire_Earth_Launcher.Tests.TestSupport;
using Microsoft.Win32;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.Installations
{
    /// <summary>The uninstall keys of community setups (contract 1.3) as source 3 of the discovery.</summary>
    [TestFixture]
    public class UninstallKeyScannerTests
    {
        private InstallationWorld world;

        [SetUp]
        public void SetUp()
        {
            world = new InstallationWorld();
        }

        private UninstallKeyScanner Scanner()
        {
            return new UninstallKeyScanner(new WriteForbiddingRegistry(world.Registry), world.Logger);
        }

        [Test]
        public void CommunityKey_IsReadWithAllValues()
        {
            world.AddUninstallKey(RegistryHive.LocalMachine, RegistryView.Registry64, Product.NeoEE,
                @"C:\Program Files (x86)\Neo Empire Earth", components: @"game,GAMEAOC,language\fr", tasks: "neoee_cdkeys",
                contractVersion: 1);

            UninstallEntry entry = Scanner().Scan().Single();

            Assert.That(entry.Product, Is.SameAs(Product.NeoEE));
            Assert.That(entry.AppId, Is.EqualTo(InstallationWorld.NeoEEAppId));
            Assert.That(entry.Key.ToString(), Is.EqualTo(@"HKLM64\Software\Microsoft\Windows\CurrentVersion\Uninstall\{" +
                                                         InstallationWorld.NeoEEAppId + "}_is1"));
            Assert.That(entry.Root, Is.EqualTo(@"C:\Program Files (x86)\Neo Empire Earth"));
            Assert.That(entry.InstallMode, Is.EqualTo(InstallMode.Admin));
            Assert.That(entry.GameVersion, Is.EqualTo("1.7.2"));
            Assert.That(entry.SetupVersion, Is.EqualTo("1.7.2"), "from DisplayName \"... - Setup v1.7.2\"");
            Assert.That(entry.Components.HasArtOfConquest, Is.True);
            Assert.That(entry.Components.GameLanguage, Is.EqualTo("fr"));
            Assert.That(entry.Tasks.Contains("NEOEE_CDKEYS"), Is.True);
            Assert.That(entry.ContractVersion, Is.EqualTo(1));
            Assert.That(world.Logger.Messages, Is.Empty);
        }

        [Test]
        public void KeyInHkcu_IsAUserInstallation()
        {
            world.AddUninstallKey(RegistryHive.CurrentUser, RegistryView.Default, Product.EE, @"C:\Users\Player\AppData\Local\Programs\Empire Earth");

            UninstallEntry entry = Scanner().Scan().Single();

            Assert.That(entry.InstallMode, Is.EqualTo(InstallMode.User));
            Assert.That(entry.ContractVersion, Is.Null, "setups up to 1.7.2 write no contract version");
        }

        [Test]
        public void AppPath_WinsOverInstallLocation()
        {
            RegistryLocation key = world.AddUninstallKey(RegistryHive.LocalMachine, RegistryView.Registry64, Product.EE, @"C:\AppPath");
            world.Registry.Seed(key, ContractNames.UninstallInstallLocationName, RegistryValue.FromString(@"C:\InstallLocation\"));

            Assert.That(Scanner().Scan().Single().Root, Is.EqualTo(@"C:\AppPath"));
        }

        [Test]
        public void InstallLocation_IsUsedWithoutAppPath()
        {
            world.AddUninstallKey(RegistryHive.LocalMachine, RegistryView.Registry64, Product.EE, @"C:\Games\Empire Earth",
                writeAppPath: false);

            Assert.That(Scanner().Scan().Single().Root, Is.EqualTo(@"C:\Games\Empire Earth"), "trailing backslash removed");
        }

        [TestCase("Empire Earth Community", "EE")]
        [TestCase("Empire Earth Community & NeoEE", "NeoEE")]
        [TestCase("empire earth community", null)]
        [TestCase("Empire Earth Community ", null)]
        [TestCase("Empire Earth Community & NeoEE Team", null)]
        [TestCase("Sierra", null)]
        [TestCase("", null)]
        public void OnlyExactlyOneOfTheTwoPublishers_IsACommunitySetup(string publisher, string product)
        {
            world.AddUninstallKey(RegistryHive.LocalMachine, RegistryView.Registry64, Product.EE, @"C:\Any", publisher: publisher);

            Assert.That(Scanner().Scan().Select(entry => entry.Product.Id), Is.EqualTo(product == null ? new string[0] : new[] { product }));
            Assert.That(world.Logger.Messages, Is.Empty, "other programs are skipped silently");
        }

        [TestCase("{00000000-0000-0000-0000-000000000AEE}_is1", true)]
        [TestCase("{abcdef01-2345-6789-abcd-ef0123456789}_IS1", true)]
        [TestCase("00000000-0000-0000-0000-000000000AEE_is1", false)]
        [TestCase("{00000000-0000-0000-0000-000000000AEE}", false)]
        [TestCase("{00000000-0000-0000-0000-000000000AEE}_is2", false)]
        [TestCase("{0000000-0000-0000-0000-000000000AEE}_is1", false)]
        [TestCase("{Empire Earth}_is1", false)]
        [TestCase("Empire Earth", false)]
        public void OnlyKeysNamedGuidIs1_AreRead(string keyName, bool read)
        {
            world.AddUninstallKey(RegistryHive.LocalMachine, RegistryView.Registry64, Product.EE, @"C:\Any", keyName: keyName);

            Assert.That(Scanner().Scan(), Has.Count.EqualTo(read ? 1 : 0));
        }

        [Test]
        [SetCulture("tr-TR")]
        public void KeyNameSuffix_IgnoresCaseIndependentOfTheCulture()
        {
            Assert.That(UninstallKeyScanner.IsInnoSetupKeyName("{00000000-0000-0000-0000-000000000AEE}_IS1", out string appId), Is.True);
            Assert.That(appId, Is.EqualTo("00000000-0000-0000-0000-000000000AEE"));
        }

        [Test]
        public void Order_NeoEEBeforeEE_ThenHkcuHklm64Hklm32()
        {
            world.AddUninstallKey(RegistryHive.LocalMachine, RegistryView.Registry32, Product.EE, @"C:\EE32", appId: "11111111-0000-0000-0000-000000000001");
            world.AddUninstallKey(RegistryHive.CurrentUser, RegistryView.Default, Product.EE, @"C:\EECU", appId: "11111111-0000-0000-0000-000000000002");
            world.AddUninstallKey(RegistryHive.LocalMachine, RegistryView.Registry64, Product.NeoEE, @"C:\Neo64");
            world.AddUninstallKey(RegistryHive.LocalMachine, RegistryView.Registry64, Product.EE, @"C:\EE64", appId: "11111111-0000-0000-0000-000000000003");

            string[] roots = Scanner().Scan().Select(entry => entry.Root).ToArray();

            Assert.That(roots, Is.EqualTo(new[] { @"C:\Neo64", @"C:\EECU", @"C:\EE64", @"C:\EE32" }));
        }

        [Test]
        public void CommunityKeyWithoutRoot_IsDroppedWithOneLogLine()
        {
            RegistryLocation key = world.AddUninstallKey(RegistryHive.LocalMachine, RegistryView.Registry64, Product.NeoEE, null);

            Assert.That(Scanner().Scan(), Is.Empty);
            Assert.That(world.LogLinesAbout(key.ToString()), Has.Length.EqualTo(1));
        }

        [Test]
        public void UnreadableKey_IsDroppedWithOneLogLine()
        {
            RegistryLocation bad = world.AddUninstallKey(RegistryHive.LocalMachine, RegistryView.Registry64, Product.NeoEE, @"C:\Neo");
            world.AddUninstallKey(RegistryHive.LocalMachine, RegistryView.Registry64, Product.EE, @"C:\EE");
            world.Registry.SetFault(bad, RegistryStatus.AccessDenied);

            Assert.That(Scanner().Scan().Select(entry => entry.Root), Is.EqualTo(new[] { @"C:\EE" }));
            Assert.That(world.LogLinesAbout(bad.ToString()), Has.Length.EqualTo(1));
        }

        [Test]
        public void UnlistableUninstallFolder_IsLoggedOnce_AndTheOtherHivesAreRead()
        {
            world.AddUninstallKey(RegistryHive.CurrentUser, RegistryView.Default, Product.EE, @"C:\EE");
            world.AddUninstallKey(RegistryHive.LocalMachine, RegistryView.Registry64, Product.NeoEE, @"C:\Neo");
            RegistryLocation folder = InstallationWorld.Hklm64(ContractNames.UninstallKey);
            world.Registry.SetFault(folder, RegistryStatus.AccessDenied);

            Assert.That(Scanner().Scan().Select(entry => entry.Root), Is.EqualTo(new[] { @"C:\EE" }));
            Assert.That(world.LogLinesAbout(folder.ToString()), Has.Length.EqualTo(1));
        }

        [Test]
        public void ThirtyTwoBitWindows_ReadsTheKeyOnce()
        {
            world = new InstallationWorld(is32BitWindows: true);
            world.AddUninstallKey(RegistryHive.LocalMachine, RegistryView.Registry64, Product.EE, @"C:\Program Files\Empire Earth");

            Assert.That(Scanner().Scan(), Has.Count.EqualTo(1));
        }
    }
}

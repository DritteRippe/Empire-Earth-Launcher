using System.Collections.Generic;
using System.Linq;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Logging;
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

        /// <summary>A scanner that has the suite record the way the discovery reads it (null if there is none).</summary>
        private UninstallKeyScanner ScannerWithSuiteRecord()
        {
            var registry = new WriteForbiddingRegistry(world.Registry);
            return new UninstallKeyScanner(registry, world.Logger, new SuiteRecordReader(registry, world.Logger).Read());
        }

        /// <summary>A registry that cannot read one value of one key; the rest comes from the inner registry.</summary>
        private sealed class UnreadableValueRegistry : IRegistry
        {
            private readonly IRegistry inner;
            private readonly RegistryLocation unreadableKey;
            private readonly string valueName;

            public UnreadableValueRegistry(IRegistry inner, RegistryLocation unreadableKey, string valueName)
            {
                this.inner = inner;
                this.unreadableKey = unreadableKey;
                this.valueName = valueName;
            }

            public RegistryResult ProbeKey(RegistryLocation key) { return inner.ProbeKey(key); }

            public RegistryResult<bool> IsLink(RegistryLocation key) { return inner.IsLink(key); }

            public RegistryResult<RegistryValue> GetValue(RegistryLocation key, string name)
            {
                return name == valueName && key.ToString() == unreadableKey.ToString()
                    ? RegistryResult<RegistryValue>.Failure(RegistryStatus.AccessDenied, "Access to " + key + " is denied.")
                    : inner.GetValue(key, name);
            }

            public RegistryResult<IReadOnlyList<string>> GetValueNames(RegistryLocation key) { return inner.GetValueNames(key); }

            public RegistryResult<IReadOnlyList<string>> GetSubKeyNames(RegistryLocation key) { return inner.GetSubKeyNames(key); }

            public RegistryResult CreateSubKey(RegistryLocation key) { return inner.CreateSubKey(key); }

            public RegistryResult SetValue(RegistryLocation key, string name, RegistryValue value) { return inner.SetValue(key, name, value); }

            public RegistryResult DeleteValue(RegistryLocation key, string name) { return inner.DeleteValue(key, name); }

            public RegistryResult DeleteSubKeyTree(RegistryLocation key) { return inner.DeleteSubKeyTree(key); }
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

        // --- The uninstall key of the suite (contract 0 "Suite and launcher", 1.4 source 3, revision 5) -------------------

        [Test]
        public void SuiteKeyWithMarker_IsNoEntry_AndLoggedOnce()
        {
            RegistryLocation key = world.AddSuiteUninstallKey();

            Assert.That(Scanner().Scan(), Is.Empty);

            Assert.That(world.Logger.Entries, Has.Count.EqualTo(1));
            Assert.That(world.Logger.Entries[0].Level, Is.EqualTo(LogLevel.Info));
            Assert.That(world.Logger.Entries[0].Message, Does.Contain(key.ToString()).And.Contain("is the one of the suite"));
            Assert.That(world.Logger.Entries.Any(entry => entry.Level == LogLevel.Warning), Is.False);
        }

        [TestCase(0, null)]
        [TestCase(null, "1")]
        [TestCase(null, "")]
        public void SuiteMarker_OfAnyTypeOrData_Counts(int? dword, string text)
        {
            world.AddSuiteUninstallKey(markerValue: dword != null ? RegistryValue.FromDWord(dword.Value) : RegistryValue.FromString(text));

            Assert.That(Scanner().Scan(), Is.Empty);
        }

        [Test]
        public void SuiteKeyWithoutMarker_WhoseRootIsTheRecordInstallPath_IsNoEntry()
        {
            RegistryLocation key = world.AddSuiteUninstallKey(marker: false);
            RegistryLocation record = world.AddSuiteRecord();

            Assert.That(ScannerWithSuiteRecord().Scan(), Is.Empty);

            Assert.That(world.Logger.Entries, Has.Count.EqualTo(1));
            Assert.That(world.Logger.Entries[0].Level, Is.EqualTo(LogLevel.Info));
            Assert.That(world.Logger.Entries[0].Message,
                Does.Contain(key.ToString()).And.Contain("names the suite root").And.Contain(record.ToString()));
        }

        [TestCase(InstallationWorld.SuiteRoot + @"\")]
        [TestCase(@"c:\program files\EMPIRE EARTH COMMUNITY")]
        public void SuiteKeyWithoutMarker_TheRecordInstallPath_MayBeWrittenInAnotherForm(string installPath)
        {
            world.AddSuiteUninstallKey(marker: false);
            world.AddSuiteRecord(installPath);

            Assert.That(ScannerWithSuiteRecord().Scan(), Is.Empty);
            Assert.That(world.LogLinesAbout("names the suite root"), Has.Length.EqualTo(1));
        }

        [Test]
        public void SuiteKeyWithoutMarker_WithOnlyInstallLocation_IsNoEntry()
        {
            RegistryLocation key = world.AddSuiteUninstallKey(marker: false);
            world.Registry.DeleteValue(key, ContractNames.UninstallAppPathName);
            world.AddSuiteRecord();

            Assert.That(ScannerWithSuiteRecord().Scan(), Is.Empty);
            Assert.That(world.LogLinesAbout("names the suite root"), Has.Length.EqualTo(1));
        }

        /// <summary>The limit of the fallback (documented in contract 1.4): no marker and no record, the key counts as before.</summary>
        [Test]
        public void SuiteKeyWithoutMarkerAndWithoutRecord_CountsAsBefore()
        {
            world.AddSuiteUninstallKey(marker: false);

            UninstallEntry entry = ScannerWithSuiteRecord().Scan().Single();

            Assert.That(entry.Product, Is.SameAs(Product.EE));
            Assert.That(entry.AppId, Is.EqualTo(InstallationWorld.SuiteAppId));
            Assert.That(entry.Root, Is.EqualTo(InstallationWorld.SuiteRoot));
            Assert.That(world.Logger.Messages, Is.Empty);
        }

        [Test]
        public void SuiteKeyWithoutMarker_AndARecordForAnotherFolder_CountsAsBefore()
        {
            world.AddSuiteUninstallKey(marker: false);
            world.AddSuiteRecord(@"D:\Elsewhere");

            Assert.That(ScannerWithSuiteRecord().Scan().Single().Root, Is.EqualTo(InstallationWorld.SuiteRoot));
        }

        [Test]
        public void ProductKeyInTheSuiteRoot_WithAnEmbeddedAppId_StaysAnEntry()
        {
            world.AddUninstallKey(RegistryHive.LocalMachine, RegistryView.Registry64, Product.EE, InstallationWorld.SuiteRoot);
            world.AddSuiteRecord();

            UninstallEntry entry = ScannerWithSuiteRecord().Scan().Single();

            Assert.That(entry.AppId, Is.EqualTo(InstallationWorld.EEAppId));
            Assert.That(world.Logger.Messages, Is.Empty);
        }

        [Test]
        public void ProductKeyInTheSuiteRoot_WithTheNeoEeAppIdInOtherCase_StaysAnEntry()
        {
            world.AddUninstallKey(RegistryHive.LocalMachine, RegistryView.Registry64, Product.NeoEE, InstallationWorld.SuiteRoot,
                appId: InstallationWorld.NeoEEAppId.ToLowerInvariant());
            world.AddSuiteRecord(neoEeAppId: InstallationWorld.NeoEEAppId.ToUpperInvariant());

            Assert.That(ScannerWithSuiteRecord().Scan(), Has.Count.EqualTo(1));
        }

        [Test]
        public void RecordFallback_OnlyInHklm()
        {
            world.AddUninstallKey(RegistryHive.CurrentUser, RegistryView.Default, Product.EE, InstallationWorld.SuiteRoot,
                appId: "11111111-0000-0000-0000-000000000009");
            world.AddSuiteRecord();

            Assert.That(ScannerWithSuiteRecord().Scan().Single().InstallMode, Is.EqualTo(InstallMode.User));
        }

        [Test]
        public void RecordFallback_WithoutInstallPath_SkipsNothing()
        {
            world.AddSuiteUninstallKey(marker: false);
            RegistryLocation record = world.AddSuiteRecord();
            world.Registry.DeleteValue(record, ContractNames.InstallPathName);

            Assert.That(ScannerWithSuiteRecord().Scan(), Has.Count.EqualTo(1));
        }

        [Test]
        public void RealKeysNextToTheSuiteKey_AreReadExactlyAsBefore()
        {
            world.AddUninstallKey(RegistryHive.LocalMachine, RegistryView.Registry64, Product.EE, @"C:\Program Files (x86)\Empire Earth");
            world.AddUninstallKey(RegistryHive.LocalMachine, RegistryView.Registry64, Product.NeoEE,
                @"C:\Program Files (x86)\Neo Empire Earth", components: @"game,GAMEAOC,language\fr", tasks: "neoee_cdkeys",
                contractVersion: 1);
            world.AddSuiteUninstallKey();

            IReadOnlyList<UninstallEntry> entries = Scanner().Scan();

            Assert.That(entries.Select(entry => entry.Product.Id), Is.EqualTo(new[] { "NeoEE", "EE" }));
            UninstallEntry neo = entries[0];
            Assert.That(neo.AppId, Is.EqualTo(InstallationWorld.NeoEEAppId));
            Assert.That(neo.Root, Is.EqualTo(@"C:\Program Files (x86)\Neo Empire Earth"));
            Assert.That(neo.InstallMode, Is.EqualTo(InstallMode.Admin));
            Assert.That(neo.GameVersion, Is.EqualTo("1.7.2"));
            Assert.That(neo.SetupVersion, Is.EqualTo("1.7.2"));
            Assert.That(neo.Components.GameLanguage, Is.EqualTo("fr"));
            Assert.That(neo.Tasks.Contains("NEOEE_CDKEYS"), Is.True);
            Assert.That(neo.ContractVersion, Is.EqualTo(1));
            UninstallEntry ee = entries[1];
            Assert.That(ee.AppId, Is.EqualTo(InstallationWorld.EEAppId));
            Assert.That(ee.Root, Is.EqualTo(@"C:\Program Files (x86)\Empire Earth"));
            Assert.That(ee.ContractVersion, Is.Null, "a key of a setup up to 1.7.2 has no contract version");
            Assert.That(world.Logger.Messages, Has.Count.EqualTo(1), "only the line of the suite key");
            Assert.That(world.Logger.Messages[0], Does.Contain("is the one of the suite"));
        }

        [Test]
        public void TheMarkerUnreadable_DropsTheKeyWithOneWarning()
        {
            RegistryLocation key = world.AddSuiteUninstallKey();
            world.AddUninstallKey(RegistryHive.LocalMachine, RegistryView.Registry64, Product.EE, @"C:\EE");
            var registry = new UnreadableValueRegistry(world.Registry, key, ContractNames.UninstallSuiteMarkerName);

            var entries = new UninstallKeyScanner(registry, world.Logger).Scan();

            Assert.That(entries.Select(entry => entry.Root), Is.EqualTo(new[] { @"C:\EE" }), "only the key with the unreadable marker is dropped");
            Assert.That(world.Logger.Entries, Has.Count.EqualTo(1));
            Assert.That(world.Logger.Entries[0].Level, Is.EqualTo(LogLevel.Warning));
            Assert.That(world.Logger.Entries[0].Message, Does.Contain(key.ToString()));
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

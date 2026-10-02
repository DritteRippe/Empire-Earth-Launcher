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
    /// <summary>The registry records of contract 1.1 as source 2 of the discovery (<see cref="InstallRecordReader"/>).</summary>
    [TestFixture]
    public class InstallRecordReaderTests
    {
        private InstallationWorld world;

        [SetUp]
        public void SetUp()
        {
            world = new InstallationWorld();
        }

        private InstallRecordReader Reader()
        {
            return new InstallRecordReader(new WriteForbiddingRegistry(world.Registry), world.Logger);
        }

        [Test]
        public void Record_IsReadWithAllValues()
        {
            world.AddRecord(RegistryHive.LocalMachine, RegistryView.Registry64, Product.NeoEE,
                @"C:\Program Files (x86)\Neo Empire Earth\", 1, "admin");
            world.Registry.Seed(InstallationWorld.Hklm64(Product.NeoEE.InstallRecordKey), ContractNames.SetupBuildName,
                RegistryValue.FromString("a1b2c3d"));

            InstallRecord record = Reader().Read().Single();

            Assert.That(record.Product, Is.SameAs(Product.NeoEE));
            Assert.That(record.Key.ToString(), Is.EqualTo(@"HKLM64\Software\Empire Earth Community\Installations\NeoEE"));
            Assert.That(record.Root, Is.EqualTo(@"C:\Program Files (x86)\Neo Empire Earth"), "normal form, no trailing backslash");
            Assert.That(record.ContractVersion, Is.EqualTo(1));
            Assert.That(record.InstallMode, Is.EqualTo(InstallMode.Admin));
            Assert.That(record.AppId, Is.EqualTo(InstallationWorld.NeoEEAppId));
            Assert.That(record.GameVersion, Is.EqualTo("2.0.0.5"));
            Assert.That(record.SetupVersion, Is.EqualTo("2.0.0"));
            Assert.That(record.SetupBuild, Is.EqualTo("a1b2c3d"));
            Assert.That(world.Logger.Messages, Is.Empty);
        }

        [Test]
        public void Order_NeoEEBeforeEE_ThenHkcuHklm64Hklm32()
        {
            world.AddRecord(RegistryHive.LocalMachine, RegistryView.Registry32, Product.EE, @"C:\EE32");
            world.AddRecord(RegistryHive.LocalMachine, RegistryView.Registry64, Product.EE, @"C:\EE64");
            world.AddRecord(RegistryHive.CurrentUser, RegistryView.Default, Product.EE, @"C:\EECU", mode: "user");
            world.AddRecord(RegistryHive.LocalMachine, RegistryView.Registry32, Product.NeoEE, @"C:\Neo32");
            world.AddRecord(RegistryHive.CurrentUser, RegistryView.Default, Product.NeoEE, @"C:\NeoCU", mode: "user");

            string[] roots = Reader().Read().Select(record => record.Root).ToArray();

            Assert.That(roots, Is.EqualTo(new[] { @"C:\NeoCU", @"C:\Neo32", @"C:\EECU", @"C:\EE64", @"C:\EE32" }));
        }

        [Test]
        public void MissingKeys_AreNotLogged()
        {
            Assert.That(Reader().Read(), Is.Empty);
            Assert.That(world.Logger.Messages, Is.Empty);
        }

        [TestCase(null, TestName = "InstallPath missing")]
        [TestCase("", TestName = "InstallPath empty")]
        [TestCase(@"Games\EE", TestName = "InstallPath relative")]
        [TestCase(@"\Games\EE", TestName = "InstallPath without drive")]
        public void RecordWithoutAUsableRoot_IsDroppedWithOneLogLine(string installPath)
        {
            RegistryLocation key = world.AddRecord(RegistryHive.CurrentUser, RegistryView.Default, Product.EE, installPath);
            world.AddRecord(RegistryHive.LocalMachine, RegistryView.Registry64, Product.EE, @"C:\Good");

            Assert.That(Reader().Read().Select(record => record.Root), Is.EqualTo(new[] { @"C:\Good" }));
            Assert.That(world.LogLinesAbout(key.ToString()), Has.Length.EqualTo(1));
            Assert.That(world.Logger.Messages, Has.Count.EqualTo(1));
        }

        [Test]
        public void InstallPathOfAnotherType_IsDroppedWithOneLogLine()
        {
            RegistryLocation key = world.AddRecord(RegistryHive.CurrentUser, RegistryView.Default, Product.NeoEE, null);
            world.Registry.Seed(key, ContractNames.InstallPathName, RegistryValue.FromDWord(1));

            Assert.That(Reader().Read(), Is.Empty);
            Assert.That(world.LogLinesAbout(key.ToString()).Single(), Does.Contain("DWord").And.StartsWith("Warning"));
        }

        [TestCase(RegistryStatus.AccessDenied)]
        [TestCase(RegistryStatus.IoError)]
        public void UnreadableRecord_IsDroppedWithOneLogLine(RegistryStatus fault)
        {
            RegistryLocation key = world.AddRecord(RegistryHive.LocalMachine, RegistryView.Registry64, Product.NeoEE, @"C:\Neo");
            world.AddRecord(RegistryHive.LocalMachine, RegistryView.Registry64, Product.EE, @"C:\EE");
            world.Registry.SetFault(key, fault);

            Assert.That(Reader().Read().Select(record => record.Root), Is.EqualTo(new[] { @"C:\EE" }));
            Assert.That(world.LogLinesAbout(key.ToString()), Has.Length.EqualTo(1));
        }

        [Test]
        public void ContractVersionOfAnotherTypeOrMissing_IsZero()
        {
            RegistryLocation key = world.AddRecord(RegistryHive.CurrentUser, RegistryView.Default, Product.EE, @"C:\A",
                contractVersion: null, mode: null);
            world.Registry.Seed(key, ContractNames.ContractVersionName, RegistryValue.FromString("1"));

            InstallRecord record = Reader().Read().Single();

            Assert.That(record.ContractVersion, Is.EqualTo(0));
            Assert.That(record.InstallModeText, Is.Null);
            Assert.That(record.InstallMode, Is.EqualTo(InstallMode.Unknown));
        }

        [Test]
        public void ThirtyTwoBitWindows_ReadsTheRecordOnce()
        {
            world = new InstallationWorld(is32BitWindows: true);
            world.AddRecord(RegistryHive.LocalMachine, RegistryView.Registry64, Product.NeoEE, @"C:\Program Files\Neo Empire Earth");

            Assert.That(Reader().Read(), Has.Count.EqualTo(1), "HKLM64 and HKLM32 are one view on 32-bit Windows");
        }
    }
}

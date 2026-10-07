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
    /// <summary>
    /// The suite record of contract 1.6 (<see cref="SuiteRecordReader"/>, <see cref="SuiteRecord"/>): read-only, HKLM 64-bit view,
    /// tolerant of a missing key and of every missing or invalid value; no discovery source.
    /// </summary>
    [TestFixture]
    public class SuiteRecordReaderTests
    {
        private const string Suite = @"C:\Program Files\Empire Earth Community";
        private const string Source = @"C:\Users\Anna\Downloads\Empire Earth Community";

        private InstallationWorld world;

        [SetUp]
        public void SetUp()
        {
            world = new InstallationWorld();
        }

        private static RegistryLocation Key
        {
            get { return InstallationWorld.Hklm64(@"Software\Empire Earth Community\Suite"); }
        }

        private void Seed(string name, RegistryValue value)
        {
            world.Registry.Seed(Key, name, value);
        }

        private void SeedRecord(string products = "EE,NeoEE")
        {
            world.Registry.SeedKey(Key);
            Seed("ContractVersion", RegistryValue.FromDWord(1));
            Seed("SuiteVersion", RegistryValue.FromString("1.0.0"));
            Seed("InstallPath", RegistryValue.FromString(Suite));
            Seed("Products", RegistryValue.FromString(products));
            Seed("SourceDir", RegistryValue.FromString(Source));
            Seed("EEAppId", RegistryValue.FromString("00000000-0000-0000-0000-0000000000EE"));
            Seed("NeoEEAppId", RegistryValue.FromString("00000000-0000-0000-0000-000000000AEE"));
            Seed("Written", RegistryValue.FromString("2026-10-05 18:04:31"));
        }

        private SuiteRecordReader Reader()
        {
            return new SuiteRecordReader(new WriteForbiddingRegistry(world.Registry), world.Logger);
        }

        [Test]
        public void Contract_1_6_TheKey_IsInHklm64()
        {
            Assert.That(SuiteRecordReader.RecordKey.ToString(), Is.EqualTo(@"HKLM64\Software\Empire Earth Community\Suite"));
            Assert.That(ContractNames.SuiteRecordKey, Is.EqualTo(@"Software\Empire Earth Community\Suite"));
        }

        [Test]
        public void Contract_1_6_TheRecord_IsReadWithAllValues()
        {
            SeedRecord();

            SuiteRecord record = Reader().Read();

            Assert.That(record, Is.Not.Null);
            Assert.That(record.ContractVersion, Is.EqualTo(1));
            Assert.That(record.SuiteVersion, Is.EqualTo("1.0.0"));
            Assert.That(record.InstallPath, Is.EqualTo(Suite));
            Assert.That(record.Products, Is.EqualTo(new[] { Product.NeoEE, Product.EE }), "NeoEE before EE as everywhere");
            Assert.That(record.SourceDir, Is.EqualTo(Source));
            Assert.That(record.Lists(Product.EE), Is.True);
            Assert.That(record.Lists(Product.NeoEE), Is.True);
            Assert.That(record.Key.ToString(), Is.EqualTo(@"HKLM64\Software\Empire Earth Community\Suite"));
            Assert.That(world.Logger.Messages, Is.Empty);
        }

        [Test]
        public void AMissingKey_IsNoRecord_AndNotLogged()
        {
            Assert.That(Reader().Read(), Is.Null);
            Assert.That(world.Logger.Messages, Is.Empty, "the normal case without the suite");
        }

        [Test]
        public void TheRecord_IsReadOnlyFromTheView64()
        {
            // The same key in the 32-bit view and in HKCU is another record: not read.
            world.Registry.Seed(InstallationWorld.Hklm32(@"Software\Empire Earth Community\Suite"), "Products", RegistryValue.FromString("EE"));
            world.Registry.Seed(InstallationWorld.Hkcu(@"Software\Empire Earth Community\Suite"), "Products", RegistryValue.FromString("EE"));

            Assert.That(Reader().Read(), Is.Null);
        }

        [Test]
        public void AKeyWithoutValues_IsARecordWithoutProducts()
        {
            world.Registry.SeedKey(Key);

            SuiteRecord record = Reader().Read();

            Assert.That(record, Is.Not.Null);
            Assert.That(record.Products, Is.Empty);
            Assert.That(record.ContractVersion, Is.Zero);
            Assert.That(record.SuiteVersion, Is.Null);
            Assert.That(record.InstallPath, Is.Null);
            Assert.That(record.SourceDir, Is.Null);
            Assert.That(record.Lists(Product.EE), Is.False);
            Assert.That(world.Logger.Messages, Is.Empty);
        }

        [Test]
        public void AValueOfAnotherType_IsLeftOut_TheRestCounts()
        {
            SeedRecord();
            Seed("Products", RegistryValue.FromDWord(3));
            Seed("SourceDir", RegistryValue.FromDWord(4));
            Seed("ContractVersion", RegistryValue.FromString("1"));
            Seed("SuiteVersion", RegistryValue.FromDWord(1));

            SuiteRecord record = Reader().Read();

            Assert.That(record.Products, Is.Empty);
            Assert.That(record.SourceDir, Is.Null);
            Assert.That(record.ContractVersion, Is.Zero, "not a REG_DWORD");
            Assert.That(record.SuiteVersion, Is.Null);
            Assert.That(record.InstallPath, Is.EqualTo(Suite));
        }

        [TestCase("")]
        [TestCase("relative\\folder")]
        [TestCase("Downloads")]
        public void ASourceDirThatIsNoFullPath_IsLeftOut(string value)
        {
            SeedRecord();
            Seed("SourceDir", RegistryValue.FromString(value));

            Assert.That(Reader().Read().SourceDir, Is.Null);
        }

        [Test]
        public void TheSourceDir_IsInTheNormalFormOfAPath()
        {
            SeedRecord();
            Seed("SourceDir", RegistryValue.FromString(Source + @"\"));
            Seed("InstallPath", RegistryValue.FromString(Suite + @"\"));

            SuiteRecord record = Reader().Read();

            Assert.That(record.SourceDir, Is.EqualTo(Source));
            Assert.That(record.InstallPath, Is.EqualTo(Suite));
        }

        [TestCase("EE,NeoEE", new[] { "NeoEE", "EE" })]
        [TestCase("NeoEE,EE", new[] { "NeoEE", "EE" })]
        [TestCase("EE", new[] { "EE" })]
        [TestCase("NeoEE", new[] { "NeoEE" })]
        [TestCase(" EE , NeoEE ", new[] { "NeoEE", "EE" })]
        [TestCase("ee,neoee", new[] { "NeoEE", "EE" })]
        [TestCase("EE,EE,EE", new[] { "EE" })]
        [TestCase("EE,AoC,Foo,", new[] { "EE" })]
        [TestCase("EE;NeoEE", new string[0])]
        [TestCase("", new string[0])]
        [TestCase("   ", new string[0])]
        [TestCase(null, new string[0])]
        public void Products_AreParsedTolerantly(string value, string[] expected)
        {
            Assert.That(SuiteRecordReader.ParseProducts(value).Select(product => product.Id), Is.EqualTo(expected));
        }

        [Test]
        public void AnUnreadableKey_IsLoggedOnce_AndIsNoRecord()
        {
            SeedRecord();
            world.Registry.SetFault(Key, RegistryStatus.AccessDenied);

            Assert.That(Reader().Read(), Is.Null);

            Assert.That(world.Logger.MessagesOf(LogLevel.Warning).Single(), Does.Contain("cannot be read and is ignored"));
        }

        [Test]
        public void TheReader_NeverWrites()
        {
            SeedRecord();
            int before = world.Registry.Changes.Count;

            Reader().Read();

            Assert.That(world.Registry.Changes.Count, Is.EqualTo(before));
        }

        [Test]
        public void RepairFolderFor_NeedsTheProduct_AndAnExistingFolder()
        {
            SeedRecord("EE");
            SuiteRecord record = Reader().Read();
            world.FileSystem.AddDirectory(Source);

            Assert.That(record.RepairFolderFor(Product.EE, world.FileSystem), Is.EqualTo(Source));
            Assert.That(record.RepairFolderFor(Product.NeoEE, world.FileSystem), Is.Null, "the record does not list NeoEE");
        }

        [Test]
        public void RepairFolderFor_AFolderThatIsGone_IsNone()
        {
            SeedRecord();
            SuiteRecord record = Reader().Read();

            Assert.That(record.RepairFolderFor(Product.EE, world.FileSystem), Is.Null);
        }

        [Test]
        public void RepairFolderFor_ChecksItsArguments()
        {
            SeedRecord();
            SuiteRecord record = Reader().Read();

            Assert.That(() => record.RepairFolderFor(null, world.FileSystem), Throws.ArgumentNullException);
            Assert.That(() => record.RepairFolderFor(Product.EE, null), Throws.ArgumentNullException);
            Assert.That(() => new SuiteRecordReader(null, world.Logger), Throws.ArgumentNullException);
            Assert.That(() => new SuiteRecordReader(world.Registry, null), Throws.ArgumentNullException);
        }

        [Test]
        public void TheSuiteRecord_IsNoDiscoverySource()
        {
            // Contract 1.6: the record is no discovery source. A folder named in it is not an installation.
            SeedRecord();
            world.FileSystem.AddDirectory(Suite);
            world.FileSystem.AddDirectory(Source);

            DiscoveryResult result = world.Discover();

            Assert.That(result.Installations, Is.Empty);
        }
    }
}

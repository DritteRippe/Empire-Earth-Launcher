using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Empire_Earth_Launcher.Core.Backup;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Integrity;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Tests.Fakes;
using Empire_Earth_Launcher.Tests.TestSupport;
using Microsoft.Win32;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.Contract
{
    /// <summary>
    /// The readers of the launcher against the shared byte samples of <c>docs/contract-samples/</c> (ADR 0012 plan review,
    /// REV-03): <c>install.ini</c> of the three install modes (ASCII, CRLF; the user one with <c>[MissingAfterInstall]</c>),
    /// <c>files.sha256</c> (ASCII, LF) and the registry record as <c>.reg</c> (UTF-16 LE with BOM, CRLF), with exactly the
    /// bytes contract 1.1, 1.2, 2.2 and O3 describe. The samples are copied next to the test program
    /// (<c>ContractSamples\</c>), so these tests also run outside the source tree. The setup repository is to take the same
    /// folder over and test its writers against it (ARCHITECTURE 14).
    /// </summary>
    [TestFixture]
    public class ContractSampleTests
    {
        private const string Root = @"C:\Program Files (x86)\Neo Empire Earth";

        /// <summary>The files of the folder, as the setup repository is to keep them too.</summary>
        private static readonly string[] SampleNames =
        {
            "files.sha256", "install-admin.ini", "install-portable.ini", "install-user.ini", "record.reg"
        };

        private static byte[] Sample(string name)
        {
            return File.ReadAllBytes(Path.Combine(TestContext.CurrentContext.TestDirectory, "ContractSamples", name));
        }

        private static string Ascii(byte[] bytes)
        {
            Assert.That(bytes, Is.All.LessThan((byte)0x80), "pure ASCII (contract O3)");
            return Encoding.ASCII.GetString(bytes);
        }

        // --- Encoding of the samples (contract 1.2, 2.2, O3) -------------------------------------------------------------

        [TestCase("install-admin.ini")]
        [TestCase("install-user.ini")]
        [TestCase("install-portable.ini")]
        public void InstallIni_IsAsciiWithCrlf_WithoutBom(string name)
        {
            string text = Ascii(Sample(name));

            Assert.That(text.Replace("\r\n", string.Empty), Does.Not.Contain("\n").And.Not.Contain("\r"), "every line ends with CRLF");
            Assert.That(text, Does.EndWith("\r\n"));
            Assert.That(text, Does.StartWith("[Install]\r\nContractVersion=1\r\n"));
        }

        [Test]
        public void Manifest_IsAsciiWithLf_WithoutBom_SortedIgnoringCase()
        {
            string text = Ascii(Sample("files.sha256"));

            Assert.That(text, Does.Not.Contain("\r"));
            Assert.That(text, Does.EndWith("\n"));
            string[] paths = text.TrimEnd('\n').Split('\n').Select(line => line.Substring(66)).ToArray();
            Assert.That(paths, Is.Ordered.Using((IComparer<string>)StringComparer.OrdinalIgnoreCase), "contract 2.2 SHOULD");
        }

        [Test]
        public void Record_IsUtf16LeWithBomAndCrlf()
        {
            byte[] bytes = Sample("record.reg");
            Assert.That(bytes.Take(2), Is.EqualTo(new byte[] { 0xFF, 0xFE }));
            string text = Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);

            Assert.That(text.Replace("\r\n", string.Empty), Does.Not.Contain("\n").And.Not.Contain("\r"));
            Assert.That(text, Does.StartWith(RegFileWriter.Header + "\r\n\r\n"));
        }

        // --- The readers against the samples ---------------------------------------------------------------------------

        [Test]
        public void Section1_2_InstallIniOfTheAdminMode()
        {
            InstallInfoFile file = InstallInfoFile.Parse(Sample("install-admin.ini"));

            Assert.That(file.ContractVersion, Is.EqualTo(1));
            Assert.That(file.ProductId, Is.EqualTo("NeoEE"));
            Assert.That(file.AppId, Is.EqualTo("00000000-0000-0000-0000-000000000AEE"));
            Assert.That(file.InstallMode, Is.EqualTo(InstallMode.Admin));
            Assert.That(file.GameVersion, Is.EqualTo("2.0.0.5"));
            Assert.That(file.SetupVersion, Is.EqualTo("2.0.0"));
            Assert.That(file.SetupBuild, Is.EqualTo("a1b2c3d"));
            Assert.That(file.Components.HasArtOfConquest, Is.True);
            Assert.That(file.Components.HasDirectXWrapper, Is.True);
            Assert.That(file.Tasks.Contains("neoee_cdkeys"), Is.True);
            Assert.That(file.Written, Is.EqualTo("2026-10-02 18:04:31"));
            Assert.That(file.MissingAfterInstall, Is.Empty);
        }

        [Test]
        public void Section1_2_InstallIniOfTheUserMode_WithMissingAfterInstall()
        {
            InstallInfoFile file = InstallInfoFile.Parse(Sample("install-user.ini"));

            Assert.That(file.ProductId, Is.EqualTo("EE"));
            Assert.That(file.InstallMode, Is.EqualTo(InstallMode.User));
            Assert.That(file.MissingAfterInstall, Is.EqualTo(new[] { "Empire Earth/file0003.dll", "Empire Earth/Data/file0004.dat" }));
            Assert.That(file.MissingAfterInstall.Select(WinPath.CheckManifestPath), Is.All.EqualTo(ManifestPathError.None),
                "manifest paths (contract 1.2)");
        }

        [Test]
        public void Section1_2_InstallIniOfThePortableMode_WithoutTheOptionalSetupBuild()
        {
            InstallInfoFile file = InstallInfoFile.Parse(Sample("install-portable.ini"));

            Assert.That(file.InstallMode, Is.EqualTo(InstallMode.Portable));
            Assert.That(file.SetupBuild, Is.Null);
            Assert.That(file.Components.HasArtOfConquest, Is.False);
        }

        [Test]
        public void Section2_2_TheManifest()
        {
            ManifestParseResult manifest = ManifestReader.Parse(Sample("files.sha256"));

            Assert.That(manifest.IsValid, Is.True, manifest.ToString());
            Assert.That(manifest.Entries.Count, Is.EqualTo(10));
            Assert.That(manifest.Entries.Select(entry => entry.Hash), Is.EqualTo(Enumerable.Range(1, 10).Select(SampleHashes.Of)),
                "the n-th line holds the hash of sample-n");
            Assert.That(manifest.Entries.Select(entry => entry.Class).Distinct(),
                Is.EquivalentTo(new[] { FileClass.Code, FileClass.Mutable, FileClass.Data }));
        }

        [Test]
        public void Section1_1_TheRecordSample_IsTheExportOfTheDiscoverySeed()
        {
            // The record that the discovery tests seed (InstallationWorld.AddRecord) plus the optional SetupBuild, exported by
            // RegFileWriter as a backup would be.
            var world = new InstallationWorld();
            RegistryLocation key = world.AddRecord(RegistryHive.LocalMachine, RegistryView.Registry64, Product.NeoEE, Root);
            world.Registry.Seed(key, ContractNames.SetupBuildName, RegistryValue.FromString("a1b2c3d"));

            RegistryResult<IReadOnlyList<RegFileKey>> export = RegistryExport.ReadTree(world.Registry, key);

            Assert.That(export.IsOk, Is.True);
            byte[] written = RegFileWriter.ToBytes(export.Value);
            Assert.That(Encoding.Unicode.GetString(written), Is.EqualTo(Encoding.Unicode.GetString(Sample("record.reg"))));
            Assert.That(written, Is.EqualTo(Sample("record.reg")), "byte for byte");
        }

        [Test]
        public void Section1_1_TheRecordSample_AgreesWithInstallIniOfTheAdminMode()
        {
            InstallInfoFile file = InstallInfoFile.Parse(Sample("install-admin.ini"));
            var world = new InstallationWorld();
            RegFileImporter.Import(Sample("record.reg"), world.Registry);

            IReadOnlyList<InstallRecord> records = new InstallRecordReader(world.Registry, world.Logger).Read();

            InstallRecord record = records.Single();
            Assert.That(record.Key.ToString(), Does.Contain(@"Installations\NeoEE"));
            Assert.That(record.Product.Id, Is.EqualTo(file.ProductId));
            Assert.That(record.ContractVersion, Is.EqualTo(file.ContractVersion));
            Assert.That(record.AppId, Is.EqualTo(file.AppId));
            Assert.That(record.InstallMode, Is.EqualTo(file.InstallMode));
            Assert.That(record.GameVersion, Is.EqualTo(file.GameVersion));
            Assert.That(record.SetupVersion, Is.EqualTo(file.SetupVersion));
            Assert.That(record.SetupBuild, Is.EqualTo(file.SetupBuild));
            Assert.That(record.Root, Is.EqualTo(Root));
        }

        [Test]
        public void Section2_5_AComputerOfTheSamples_IsOk_AndTheUserSampleNamesItsMissingFiles()
        {
            var world = new InstallationWorld();
            RegFileImporter.Import(Sample("record.reg"), world.Registry);
            ManifestParseResult manifest = ManifestReader.Parse(Sample("files.sha256"));
            for (int n = 1; n <= manifest.Entries.Count; n++)
                world.FileSystem.AddFile(WinPath.Combine(Root, manifest.Entries[n - 1].Path), SampleHashes.Content(n));
            world.FileSystem.AddFile(Root + @"\_setupdata_NeoEE\install.ini", Sample("install-admin.ini"));
            world.FileSystem.AddFile(Root + @"\_setupdata_NeoEE\files.sha256", Sample("files.sha256"));
            Installation installation = world.Discover().Selected;
            var checker = new IntegrityChecker(world.FileSystem, world.Registry, new FakeMutexProbe(), world.Logger);

            IntegrityReport admin = checker.Check(installation, IntegrityCheckKind.Full);
            world.FileSystem.AddFile(Root + @"\_setupdata_NeoEE\install.ini", Sample("install-user.ini"));
            IntegrityReport withMissing = checker.Check(installation, IntegrityCheckKind.Full);

            Assert.That(installation.Kind, Is.EqualTo(InstallationKind.Community));
            Assert.That(admin.State, Is.EqualTo(IntegrityState.Ok), admin.ToString());
            Assert.That(admin.HashedFiles, Is.EqualTo(8), "code and data, not the two mutable files");
            Assert.That(withMissing.State, Is.EqualTo(IntegrityState.Damaged));
            Assert.That(withMissing.Findings.Select(finding => finding.Path),
                Is.EqualTo(new[] { "Empire Earth/file0003.dll", "Empire Earth/Data/file0004.dat" }));
        }

        // --- The folder in the source tree ----------------------------------------------------------------------------

        [Test]
        [Category(TestCategories.SourceTree)]
        public void TheFolder_HoldsExactlyTheSamples_AndTheTestsUseTheirBytes()
        {
            string folder = RepositoryRoot.GetFullPath("docs/contract-samples");
            string[] names = Directory.GetFiles(folder).Select(Path.GetFileName).OrderBy(name => name, StringComparer.Ordinal).ToArray();

            Assert.That(names, Is.EqualTo(SampleNames), "a new sample needs a link in the test project as well");
            foreach (string name in SampleNames)
                Assert.That(Sample(name), Is.EqualTo(File.ReadAllBytes(Path.Combine(folder, name))), name);
        }
    }
}

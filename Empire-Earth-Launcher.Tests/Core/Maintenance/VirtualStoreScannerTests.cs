using System.Linq;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Maintenance;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Core.Play;
using Empire_Earth_Launcher.Tests.Fakes;
using Empire_Earth_Launcher.Tests.TestSupport;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.Maintenance
{
    /// <summary>
    /// <see cref="VirtualStoreScanner"/> (R8, ADR 0016, forum report section 8 row 2 and test case 1, forum 4.12): only game
    /// folders below the virtualized folders are looked up; a shadowed manifest file and a program file are serious, every other
    /// copy is information; the scan only reads.
    /// </summary>
    [TestFixture]
    public class VirtualStoreScannerTests
    {
        private const string Root = @"C:\Program Files (x86)\Neo Empire Earth";
        private const string VirtualStore = @"C:\Users\Player\AppData\Local\VirtualStore";
        private const string EeCopy = VirtualStore + @"\Program Files (x86)\Neo Empire Earth\Empire Earth";
        private const string AocCopy = VirtualStore + @"\Program Files (x86)\Neo Empire Earth\Empire Earth - The Art of Conquest";

        private static readonly string[] Virtualized =
            { @"C:\Program Files", @"C:\Program Files (x86)", @"C:\ProgramData", @"C:\Windows" };

        private InstallationWorld world;
        private FakeMutexProbe mutexes;

        [SetUp]
        public void SetUp()
        {
            world = new InstallationWorld();
            mutexes = new FakeMutexProbe();
        }

        private VirtualStoreReport Scan(string root)
        {
            var scanner = new VirtualStoreScanner(new WriteForbiddingFileSystem(world.FileSystem),
                new EffectivePathResolver(world.FileSystem, VirtualStore, Virtualized), new MutationGuard(mutexes, world.Logger),
                world.Logger);
            return scanner.Scan(InstallationWorld.ByRoot(world.Discover(), root));
        }

        /// <summary>Contract 4.2: while a setup runs, the manifest of no installation is read (coverage review).</summary>
        [Test]
        public void WhileASetupRuns_TheManifestIsNotOpened()
        {
            world.AddCommunityInstallation(Root, Product.NeoEE);
            string manifest = Root + @"\_setupdata_NeoEE\files.sha256";
            world.FileSystem.AddFile(manifest, SampleHashes.Of(1) + "  Empire Earth/Data/Language.dll\n");
            world.FileSystem.AddFile(EeCopy + @"\neoee.log", "log");
            Installation installation = InstallationWorld.ByRoot(world.Discover(), Root);
            mutexes.With(Product.NeoEE.SetupMutexName);

            var scanner = new VirtualStoreScanner(world.FileSystem,
                new EffectivePathResolver(world.FileSystem, VirtualStore, Virtualized), new MutationGuard(mutexes, world.Logger),
                world.Logger);
            VirtualStoreReport report = scanner.Scan(installation);

            Assert.That(world.FileSystem.OpenCount(manifest), Is.EqualTo(0));
            Assert.That(report.ManifestUnusable, Is.True, "nothing is known about the files of the setup");
            Assert.That(ManifestFiles.Read(world.FileSystem, installation, new MutationGuard(mutexes, world.Logger)).Problem,
                Does.Contain("is not read while the NeoEE setup is running"));
            Assert.That(world.FileSystem.OpenCount(manifest), Is.EqualTo(0));
        }

        [Test]
        public void ShadowedManifestFiles_AreSerious_ProgramFilesToo_RuntimeFilesAreInformation()
        {
            world.AddCommunityInstallation(Root, Product.NeoEE);
            world.FileSystem.AddFile(Root + @"\_setupdata_NeoEE\files.sha256", SampleHashes.Of(1) + "  Empire Earth/Data/Language.dll\n" +
                                                                                SampleHashes.Of(2) + "  Empire Earth/Data/db/dbcivs.dat\n");
            world.FileSystem.AddFile(Root + @"\Empire Earth\Data\db\dbcivs.dat", SampleHashes.Content(2));
            world.FileSystem.AddFile(EeCopy + @"\Data\db\dbcivs.dat", "changed");
            world.FileSystem.AddFile(EeCopy + @"\neoee.dll", "updated");
            world.FileSystem.AddFile(EeCopy + @"\_wonlobbypersistent.dat", "profiles");
            world.FileSystem.AddFile(AocCopy + @"\Data\Saved Games\game.ees", "save");

            VirtualStoreReport report = Scan(Root);

            Assert.That(report.IsVirtualizable, Is.True);
            Assert.That(report.Folders, Is.EqualTo(new[] { EeCopy, AocCopy }));
            Assert.That(report.HasSerious, Is.True);
            Assert.That(report.Findings.Select(finding => finding.VirtualStorePath + " " + finding.Reason), Is.EqualTo(new[]
            {
                EeCopy + @"\Data\db\dbcivs.dat ManifestFile",
                EeCopy + @"\neoee.dll ProgramFile",
                AocCopy + @"\Data\Saved Games\game.ees RuntimeFile",
                EeCopy + @"\_wonlobbypersistent.dat RuntimeFile",
            }));
            VirtualStoreFinding civs = report.Findings[0];
            Assert.That(civs.GamePath, Is.EqualTo(Root + @"\Empire Earth\Data\db\dbcivs.dat"));
            Assert.That(civs.OriginalExists, Is.True);
            Assert.That(civs.Game, Is.SameAs(Game.EmpireEarth));
            Assert.That(report.Findings[1].OriginalExists, Is.True, "neoee.dll of the installation");
            Assert.That(report.Findings[2].Game, Is.SameAs(Game.ArtOfConquest));
            Assert.That(world.LogLinesAbout("VirtualStore: the game uses"), Has.Length.EqualTo(2));
        }

        /// <summary>A5: a copy of dgVoodoo.conf that differs from the file of the game folder is what the game reads.</summary>
        [Test]
        public void ADifferentCopyOfTheWrapperConfig_IsReportedAndLogged()
        {
            world.AddCommunityInstallation(Root, Product.NeoEE);
            world.FileSystem.AddFile(Root + @"\Empire Earth\dgVoodoo.conf", "[General]\nOutputAPI = bestavailable\n");
            world.FileSystem.AddFile(EeCopy + @"\dgVoodoo.conf", "[General]\nOutputAPI = d3d11_fl10_1\n");
            world.FileSystem.AddFile(Root + @"\Empire Earth - The Art of Conquest\dgVoodoo.conf", "same");
            world.FileSystem.AddFile(AocCopy + @"\dgVoodoo.conf", "same");

            VirtualStoreReport report = Scan(Root);

            Assert.That(report.ShadowingWrapperConfigs.Select(finding => finding.VirtualStorePath), Is.EqualTo(new[] { EeCopy + @"\dgVoodoo.conf" }));
            VirtualStoreFinding shadow = report.ShadowingWrapperConfigs[0];
            Assert.That(shadow.GamePath, Is.EqualTo(Root + @"\Empire Earth\dgVoodoo.conf"));
            Assert.That(shadow.DiffersFromOriginal, Is.True);
            Assert.That(report.Findings.Single(finding => finding.Game == Game.ArtOfConquest).DiffersFromOriginal, Is.False,
                "an identical copy is no hint");
            Assert.That(world.LogLinesAbout("differs from"), Has.Length.EqualTo(1));
        }

        [Test]
        public void ACopyOfTheWrapperConfigWithTheSameLength_IsComparedByContent()
        {
            world.AddCommunityInstallation(Root, Product.NeoEE);
            world.FileSystem.AddFile(Root + @"\Empire Earth\dgVoodoo.conf", "VRAM = 256");
            world.FileSystem.AddFile(EeCopy + @"\DGVOODOO.CONF", "VRAM = 512");

            VirtualStoreReport report = Scan(Root);

            Assert.That(report.ShadowingWrapperConfigs, Has.Count.EqualTo(1), "same length, other content; the name is not case sensitive");
        }

        [Test]
        public void NoCopyOfTheWrapperConfig_NoCopyWithoutOriginal_AndOtherFiles_GiveNoHint()
        {
            world.AddCommunityInstallation(Root, Product.NeoEE);
            world.FileSystem.AddFile(Root + @"\Empire Earth\dgVoodoo.conf", "real");
            world.FileSystem.AddFile(Root + @"\Empire Earth\other.conf", "real");
            world.FileSystem.AddFile(EeCopy + @"\other.conf", "changed");
            world.FileSystem.AddFile(EeCopy + @"\Data\dgVoodoo.conf", "changed");
            world.FileSystem.AddFile(AocCopy + @"\dgVoodoo.conf", "copy without a file in the game folder");

            VirtualStoreReport report = Scan(Root);

            Assert.That(report.Findings, Has.Count.EqualTo(3));
            Assert.That(report.ShadowingWrapperConfigs, Is.Empty);
            Assert.That(world.LogLinesAbout("differs from"), Is.Empty);
        }

        /// <summary>Outside Program Files, ProgramData and Windows the game is never virtualized (ADR 0016 amendment of L-WP4).</summary>
        [Test]
        public void AFolderOutsideTheVirtualizedFolders_IsNotLookedUp()
        {
            world.AddForeignInstallation(@"D:\Games\Empire Earth");
            world.FileSystem.AddFile(VirtualStore + @"\Games\Empire Earth\Empire Earth.exe", "other");

            VirtualStoreReport report = Scan(@"D:\Games");

            Assert.That(report.IsVirtualizable, Is.False);
            Assert.That(report.Findings, Is.Empty);
        }

        [Test]
        public void NoCopies_IsAnEmptyList()
        {
            world.AddCommunityInstallation(Root, Product.NeoEE);

            VirtualStoreReport report = Scan(Root);

            Assert.That(report.IsVirtualizable, Is.True);
            Assert.That(report.Findings, Is.Empty);
            Assert.That(report.HasSerious, Is.False);
        }

        [Test]
        public void ALongList_IsCut()
        {
            world.AddCommunityInstallation(Root, Product.NeoEE, artOfConquest: false);
            for (int i = 0; i <= VirtualStoreScanner.MaxFiles; i++)
                world.FileSystem.AddFile(EeCopy + @"\Logs\" + i.ToString("D5", System.Globalization.CultureInfo.InvariantCulture) + ".log", "x");

            VirtualStoreReport report = Scan(Root);

            Assert.That(report.Findings, Has.Count.EqualTo(VirtualStoreScanner.MaxFiles));
            Assert.That(report.Truncated, Is.True);
        }

        [Test]
        public void AnUnusableManifest_StillMarksProgramFiles()
        {
            world.AddCommunityInstallation(Root, Product.NeoEE);
            world.FileSystem.AddFile(Root + @"\_setupdata_NeoEE\files.sha256", "not a manifest\n");
            world.FileSystem.AddFile(EeCopy + @"\Empire Earth.exe", "patched");

            VirtualStoreReport report = Scan(Root);

            Assert.That(report.ManifestUnusable, Is.True);
            Assert.That(report.Findings.Single().Reason, Is.EqualTo(VirtualStoreReason.ProgramFile));
        }

        [Test]
        public void AFolderThatCannotBeListed_IsSkipped()
        {
            world.AddCommunityInstallation(Root, Product.NeoEE);
            world.FileSystem.AddFile(EeCopy + @"\Data\a.cfg", "x");
            world.FileSystem.AddFile(EeCopy + @"\b.log", "x");
            world.FileSystem.FailOn(EeCopy + @"\Data", FileSystemOperation.Enumerate, FileSystemStatus.AccessDenied);

            VirtualStoreReport report = Scan(Root);

            Assert.That(report.Findings.Select(finding => finding.VirtualStorePath), Is.EqualTo(new[] { EeCopy + @"\b.log" }));
        }
    }
}

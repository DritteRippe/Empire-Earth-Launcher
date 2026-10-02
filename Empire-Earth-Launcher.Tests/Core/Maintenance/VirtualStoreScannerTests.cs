using System.Linq;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Maintenance;
using Empire_Earth_Launcher.Core.Platform;
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

        [SetUp]
        public void SetUp()
        {
            world = new InstallationWorld();
        }

        private VirtualStoreReport Scan(string root)
        {
            var scanner = new VirtualStoreScanner(new WriteForbiddingFileSystem(world.FileSystem),
                new EffectivePathResolver(world.FileSystem, VirtualStore, Virtualized), world.Logger);
            return scanner.Scan(InstallationWorld.ByRoot(world.Discover(), root));
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

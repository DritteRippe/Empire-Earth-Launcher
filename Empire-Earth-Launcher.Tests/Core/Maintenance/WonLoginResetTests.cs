using System;
using System.IO;
using System.Linq;
using Empire_Earth_Launcher.Core.Backup;
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
    /// <see cref="WonLoginReset"/> (R6, forum t=43377 p=83519, ADR 0007 and 0016): <c>_wonkver.pub</c> and <c>_wonlogin.ks</c> of
    /// the EE and AoC folders and of their VirtualStore copies go into a dated backup folder, behind the mutation guard; a
    /// manifest file is never moved; denied access is reported. With the in-memory file system and once with real files.
    /// </summary>
    [TestFixture]
    public class WonLoginResetTests
    {
        private const string Root = @"C:\Program Files (x86)\Neo Empire Earth";
        private const string EeFolder = Root + @"\Empire Earth";
        private const string AocFolder = Root + @"\Empire Earth - The Art of Conquest";
        private const string VirtualStore = @"C:\Users\Player\AppData\Local\VirtualStore";
        private const string EeVirtualStore = VirtualStore + @"\Program Files (x86)\Neo Empire Earth\Empire Earth";

        private static readonly string[] Virtualized =
            { @"C:\Program Files", @"C:\Program Files (x86)", @"C:\ProgramData", @"C:\Windows" };

        private GameSettingsWorld w;

        [SetUp]
        public void SetUp()
        {
            w = new GameSettingsWorld();
            w.World.AddCommunityInstallation(Root, Product.NeoEE);
        }

        private WonLoginReset CreateReset(IFileSystem fileSystem = null)
        {
            IFileSystem files = fileSystem ?? w.FileSystem;
            var paths = new EffectivePathResolver(files, VirtualStore, Virtualized);
            return new WonLoginReset(files, paths, w.Guard, new FileBackup(files, w.Backups, w.Logger), w.Logger);
        }

        private Installation Installation()
        {
            return InstallationWorld.ByRoot(w.Discover(), Root);
        }

        private void AddLoginFiles()
        {
            w.FileSystem.AddFile(EeFolder + @"\_wonlogin.ks", SampleHashes.Content(1));
            w.FileSystem.AddFile(EeFolder + @"\_wonkver.pub", SampleHashes.Content(2));
            w.FileSystem.AddFile(AocFolder + @"\_wonlogin.ks", SampleHashes.Content(3));
            w.FileSystem.AddFile(EeVirtualStore + @"\_wonlogin.ks", SampleHashes.Content(4));
        }

        [Test]
        public void Find_ListsTheFilesOfBothGameFoldersAndTheirVirtualStoreCopies()
        {
            AddLoginFiles();
            w.FileSystem.AddFile(EeFolder + @"\_wonlobbypersistent.dat", "profiles");

            WonLoginFiles found = CreateReset(new WriteForbiddingFileSystem(w.FileSystem)).Find(Installation());

            Assert.That(found.ToMove.Select(file => file.Path), Is.EqualTo(new[]
            {
                EeFolder + @"\_wonkver.pub", EeFolder + @"\_wonlogin.ks", EeVirtualStore + @"\_wonlogin.ks", AocFolder + @"\_wonlogin.ks"
            }));
            Assert.That(found.ToMove.Select(file => file.IsVirtualStoreCopy), Is.EqualTo(new[] { false, false, true, false }));
            Assert.That(found.Kept, Is.Empty);
            Assert.That(found.Manifest.Status, Is.EqualTo(ManifestFilesStatus.None));
        }

        [Test]
        public void Reset_MovesTheFilesIntoTheBackup_ByGameAndPlace()
        {
            AddLoginFiles();

            WonResetResult result = CreateReset().Reset(Installation());

            Assert.That(result.Outcome, Is.EqualTo(WonResetOutcome.Done), result.ToString());
            Assert.That(WinPath.GetFileName(result.BackupFolder), Does.EndWith("_won-login-reset"));
            Assert.That(WinPath.GetParent(result.BackupFolder), Is.EqualTo(GameSettingsWorld.BackupsFolder));
            Assert.That(w.FileSystem.GetText(result.BackupFolder + @"\EE\_wonlogin.ks"), Is.EqualTo(SampleHashes.Content(1)));
            Assert.That(w.FileSystem.GetText(result.BackupFolder + @"\EE\_wonkver.pub"), Is.EqualTo(SampleHashes.Content(2)));
            Assert.That(w.FileSystem.GetText(result.BackupFolder + @"\AoC\_wonlogin.ks"), Is.EqualTo(SampleHashes.Content(3)));
            Assert.That(w.FileSystem.GetText(result.BackupFolder + @"\EE-VirtualStore\_wonlogin.ks"), Is.EqualTo(SampleHashes.Content(4)));
            Assert.That(w.FileSystem.AllFiles.Where(path => WonLoginReset.FileNames.Contains(WinPath.GetFileName(path)) &&
                                                            !path.StartsWith(GameSettingsWorld.BackupsFolder, StringComparison.Ordinal)),
                Is.Empty, "no login file is left in the game folders");
            Assert.That(w.FileSystem.FileExists(EeFolder + @"\Empire Earth.exe"), Is.True);
            Assert.That(w.Logger.Messages.Any(message => message.Contains("sample-")), Is.False, "no content in the log");
            Assert.That(w.Logger.Messages.Last(), Does.Contain("it contains login data"));
        }

        /// <summary>Files listed in the manifest are never moved, in the game folder and in the VirtualStore (ADR 0007).</summary>
        [Test]
        public void Reset_NeverMovesAManifestFile()
        {
            AddLoginFiles();
            w.FileSystem.AddFile(EeVirtualStore + @"\_wonkver.pub", SampleHashes.Content(5));
            string hash = SampleHashes.Of(2);
            w.FileSystem.AddFile(Root + @"\_setupdata_NeoEE\files.sha256", hash + "  Empire Earth/_wonkver.pub\n");

            WonResetResult result = CreateReset().Reset(Installation());

            Assert.That(result.Outcome, Is.EqualTo(WonResetOutcome.Done));
            Assert.That(w.FileSystem.GetText(EeFolder + @"\_wonkver.pub"), Is.EqualTo(SampleHashes.Content(2)));
            Assert.That(w.FileSystem.GetText(EeVirtualStore + @"\_wonkver.pub"), Is.EqualTo(SampleHashes.Content(5)));
            Assert.That(result.Kept.Select(file => file.Path), Is.EqualTo(new[] { EeFolder + @"\_wonkver.pub", EeVirtualStore + @"\_wonkver.pub" }));
            Assert.That(result.Files, Has.Count.EqualTo(3));
            Assert.That(w.FileSystem.FileExists(EeFolder + @"\_wonlogin.ks"), Is.False);
        }

        [TestCase("not a manifest line\n", TestName = "Reset_WithAnInvalidManifest_MovesNothing")]
        [TestCase(null, TestName = "Reset_WithAnUnreadableManifest_MovesNothing")]
        public void Reset_WithAManifestThatCannotBeUsed_MovesNothing(string manifest)
        {
            AddLoginFiles();
            string path = Root + @"\_setupdata_NeoEE\files.sha256";
            w.FileSystem.AddFile(path, manifest ?? SampleHashes.Of(2) + "  Empire Earth/_wonkver.pub\n");
            if (manifest == null)
                w.FileSystem.FailOn(path, FileSystemOperation.Read, FileSystemStatus.AccessDenied);

            WonResetResult result = CreateReset().Reset(Installation());

            Assert.That(result.Outcome, Is.EqualTo(WonResetOutcome.ManifestUnusable));
            Assert.That(w.FileSystem.FileExists(EeFolder + @"\_wonlogin.ks"), Is.True);
            Assert.That(w.FileSystem.DirectoryExists(GameSettingsWorld.BackupsFolder), Is.False);
        }

        /// <summary>ADR 0016: blocked by a setup and by a game, nothing is moved and no backup is made.</summary>
        [Test]
        public void Reset_IsBlockedBySetupAndGame(
            [Values("EE_Setup", "NeoEE_Setup", "StainlessSteelStudiosPresentsEmpireEarth", "MadDocSoftwarePresentsEmpireEarthExpansion")]
            string mutex)
        {
            AddLoginFiles();
            w.Mutexes.With(mutex);

            WonResetResult result = CreateReset().Reset(Installation());

            Assert.That(result.Outcome, Is.EqualTo(WonResetOutcome.Blocked));
            Assert.That(result.Block.Block, Is.EqualTo(mutex.EndsWith("_Setup", StringComparison.Ordinal)
                ? MutationBlock.SetupRunning : MutationBlock.GameRunning));
            Assert.That(w.FileSystem.FileExists(EeFolder + @"\_wonlogin.ks"), Is.True);
            Assert.That(w.FileSystem.DirectoryExists(GameSettingsWorld.BackupsFolder), Is.False);
        }

        /// <summary>A file Windows does not let the launcher remove (a protected folder, a running game) is reported.</summary>
        [Test]
        public void Reset_WhenAccessIsDenied_ReportsTheFile()
        {
            AddLoginFiles();
            w.FileSystem.SetReadOnly(EeFolder + @"\_wonlogin.ks", true);

            WonResetResult result = CreateReset().Reset(Installation());

            Assert.That(result.Outcome, Is.EqualTo(WonResetOutcome.Partial));
            MovedFile denied = result.Files.Single(file => file.Outcome != FileMoveOutcome.Moved);
            Assert.That(denied.Source, Is.EqualTo(EeFolder + @"\_wonlogin.ks"));
            Assert.That(denied.Outcome, Is.EqualTo(FileMoveOutcome.RemoveDenied));
            Assert.That(w.FileSystem.FileExists(denied.BackupPath), Is.True);
        }

        [Test]
        public void Reset_WithoutFiles_DoesNothing()
        {
            WonResetResult result = CreateReset().Reset(Installation());

            Assert.That(result.Outcome, Is.EqualTo(WonResetOutcome.NothingToReset));
            Assert.That(w.FileSystem.DirectoryExists(GameSettingsWorld.BackupsFolder), Is.False);
        }

        /// <summary>Outside the virtualized folders the VirtualStore is never looked at (ADR 0016 amendment of L-WP4).</summary>
        [Test]
        public void Find_OutsideTheVirtualizedFolders_IgnoresTheVirtualStore()
        {
            w.World.AddForeignInstallation(@"D:\Games\Empire Earth");
            w.FileSystem.AddFile(@"D:\Games\Empire Earth\_wonlogin.ks", "x");
            w.FileSystem.AddFile(VirtualStore + @"\Games\Empire Earth\_wonlogin.ks", "x");

            Installation foreign = w.Discover(@"D:\Games\Empire Earth").Selected;
            WonLoginFiles found = CreateReset().Find(foreign);

            Assert.That(found.All.Select(file => file.Path), Is.EqualTo(new[] { @"D:\Games\Empire Earth\_wonlogin.ks" }).IgnoreCase);
        }

        /// <summary>The reset with real files in a temporary folder (a mapped drive letter), the VirtualStore copy included.</summary>
        [Test]
        public void Reset_MovesRealFiles()
        {
            using (var directory = new TemporaryDirectory())
            {
                var real = new MappedFileSystem("T:", directory.Path);
                string ee = Path.Combine("Program Files (x86)", "Sierra", "Empire Earth");
                directory.CreateFile(Path.Combine(ee, "Empire Earth.exe"), "exe");
                directory.CreateFile(Path.Combine(ee, "_wonlogin.ks"), "login");
                directory.CreateFile(Path.Combine("VirtualStore", ee, "_wonkver.pub"), "key");
                Installation installation = new InstallationDiscovery(new InMemoryRegistry(), real, w.Logger)
                    .Discover(@"T:\Program Files (x86)\Sierra\Empire Earth", null).Selected;
                var paths = new EffectivePathResolver(real, @"T:\VirtualStore", new[] { @"T:\Program Files (x86)" });
                var reset = new WonLoginReset(real, paths, w.Guard,
                    new FileBackup(real, new BackupLocations(@"T:\Backups", real, w.World.Clock, w.Logger), w.Logger), w.Logger);

                WonResetResult result = reset.Reset(installation);

                Assert.That(result.Outcome, Is.EqualTo(WonResetOutcome.Done), result.ToString());
                Assert.That(File.Exists(directory.Combine(Path.Combine(ee, "_wonlogin.ks"))), Is.False);
                Assert.That(File.Exists(directory.Combine(Path.Combine("VirtualStore", ee, "_wonkver.pub"))), Is.False);
                Assert.That(File.Exists(directory.Combine(Path.Combine(ee, "Empire Earth.exe"))), Is.True);
                Assert.That(File.ReadAllText(real.ToHost(result.BackupFolder + @"\EE\_wonlogin.ks")), Is.EqualTo("login"));
                Assert.That(File.ReadAllText(real.ToHost(result.BackupFolder + @"\EE-VirtualStore\_wonkver.pub")), Is.EqualTo("key"));
            }
        }
    }
}

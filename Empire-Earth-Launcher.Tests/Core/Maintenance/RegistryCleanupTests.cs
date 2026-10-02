using System;
using System.Collections.Generic;
using System.Linq;
using Empire_Earth_Launcher.Core.Backup;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.GameSettings;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Logging;
using Empire_Earth_Launcher.Core.Maintenance;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Core.Play;
using Empire_Earth_Launcher.Tests.Fakes;
using Empire_Earth_Launcher.Tests.TestSupport;
using Microsoft.Win32;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.Maintenance
{
    /// <summary>
    /// <see cref="RegistryCleanup"/> (R5, contract 3.8, ADR 0007 and 0016): the scan judges every key of the list against the
    /// installations, the folder its "Installed From" values name and the kind of that drive; the deletion goes selection ->
    /// mutation guard -> check again -> <c>.reg</c> backup -> delete, through the launcher's write policy; a failed backup
    /// deletes nothing, and the CD keys are never touched. With the in-memory registry and file system; every key value is
    /// synthetic.
    /// </summary>
    [TestFixture]
    public class RegistryCleanupTests
    {
        private const string OldFolder = @"D:\Old Games\Empire Earth";
        private const string OldAocFolder = @"D:\Old Games\Empire Earth - The Art of Conquest";

        private GameSettingsWorld w;

        [SetUp]
        public void SetUp()
        {
            w = new GameSettingsWorld();
        }

        private RegistryCleanup CreateCleanup()
        {
            return new RegistryCleanup(w.Registry, w.FileSystem, w.Guard, w.Backups, w.Logger);
        }

        private static CleanupEntry Entry(string id)
        {
            return CleanupCandidates.All.Single(entry => entry.Id == id);
        }

        /// <summary>The key of <paramref name="id"/> with "Installed From" values naming <paramref name="folder"/> and a value below it.</summary>
        private RegistryLocation Seed(string id, string folder = OldFolder)
        {
            RegistryLocation key = Entry(id).Key;
            w.RawRegistry.SeedKey(key);
            if (folder != null)
                w.World.SetInstalledFrom(key, folder);
            w.RawRegistry.Seed(key.Child(ContractNames.GameOptionsSubKeyName), "Map Type", RegistryValue.FromDWord(3));
            return key;
        }

        /// <summary>The NeoEE CD keys in every place the setup and Windows put them (contract 3.8), with synthetic values.</summary>
        private void SeedCdKeys()
        {
            foreach (string location in new[]
                     {
                         @"HKCU\Software\Sierra\CDKeys", @"HKLM32\Software\Sierra\CDKeys", @"HKLM64\Software\Sierra\CDKeys",
                         @"HKCU\Software\Classes\VirtualStore\MACHINE\SOFTWARE\Sierra\CDKeys",
                         @"HKCU\Software\Classes\VirtualStore\MACHINE\SOFTWARE\WOW6432Node\Sierra\CDKeys"
                     })
            {
                w.RawRegistry.Seed(RegistryLocation.Parse(location), "Empire Earth", RegistryValue.FromString("NOT-A-KEY-0000"));
            }
        }

        private IReadOnlyList<string> CdKeyValues()
        {
            return w.ValuesBelow(RegistryLocation.Parse(@"HKCU\Software\Sierra"), RegistryLocation.Parse(@"HKLM32\Software\Sierra"),
                RegistryLocation.Parse(@"HKLM64\Software\Sierra"),
                RegistryLocation.Parse(@"HKCU\Software\Classes\VirtualStore\MACHINE\SOFTWARE\Sierra"),
                RegistryLocation.Parse(@"HKCU\Software\Classes\VirtualStore\MACHINE\SOFTWARE\WOW6432Node\Sierra"));
        }

        private static CleanupItem Item(CleanupScan scan, string id)
        {
            return scan.Items.Single(item => item.Entry.Id == id);
        }

        // --- Scan ------------------------------------------------------------------------------------------------------

        [Test]
        public void Scan_OfAComputerWithoutKeys_ShowsNothing()
        {
            CleanupScan scan = CreateCleanup().Scan(w.Discover());

            Assert.That(scan.Items, Has.Count.EqualTo(CleanupCandidates.All.Count));
            Assert.That(scan.Items.Select(item => item.State).Distinct(), Is.EqualTo(new[] { CleanupState.Missing }));
            Assert.That(scan.HasCandidates, Is.False);
            Assert.That(scan.ReadOnly, Is.Empty);
        }

        [TestCase("hkcu-ee-ee")]
        [TestCase("hkcu-ee-aoc")]
        [TestCase("hkcu-neoee-ee")]
        [TestCase("hkcu-neoee-aoc")]
        [TestCase("vs-sssi-ee")]
        [TestCase("vs-maddoc-aoc")]
        [TestCase("vs-sssi-ee-wow64")]
        [TestCase("vs-maddoc-aoc-wow64")]
        public void Scan_AStaleHkcuKey_IsOffered(string id)
        {
            Seed(id);

            CleanupItem item = Item(CreateCleanup().Scan(w.Discover()), id);

            Assert.That(item.State, Is.EqualTo(CleanupState.Stale));
            Assert.That(item.IsOffered, Is.True);
            Assert.That(item.Folder, Is.EqualTo(OldFolder).IgnoreCase);
            Assert.That(item.DriveKind, Is.EqualTo(DriveKind.Fixed));
            Assert.That(item.Advice.Code, Is.EqualTo(CleanupAdviceCode.LauncherCanDelete));
        }

        /// <summary>
        /// Security review: Windows reports a folder the player may not look at as missing. Only a parent that can be listed (or
        /// is missing itself) proves that the folder is gone; access denied keeps the key.
        /// </summary>
        [TestCase(true, TestName = "Scan_AFolderWhoseParentCannotBeListed_IsKept")]
        [TestCase(false, TestName = "Scan_AFolderWhoseMissingParentIsInAFolderThatCannotBeListed_IsKept")]
        public void Scan_AFolderThatMayNotBeLookedAt_IsKept(bool parentExists)
        {
            Seed("vs-sssi-ee");
            if (parentExists)
            {
                w.FileSystem.AddDirectory(@"D:\Old Games\Other");
                w.FileSystem.FailOn(@"D:\Old Games", FileSystemOperation.Enumerate, FileSystemStatus.AccessDenied);
            }
            else
                w.FileSystem.FailOn(@"D:\", FileSystemOperation.Enumerate, FileSystemStatus.AccessDenied);

            CleanupItem item = Item(CreateCleanup().Scan(w.Discover()), "vs-sssi-ee");

            Assert.That(item.State, Is.EqualTo(CleanupState.FolderUnknown));
            Assert.That(item.IsOffered, Is.False);
            Assert.That(item.Advice.Code, Is.EqualTo(CleanupAdviceCode.KeepFolderUnknown));
            Assert.That(item.Advice.DeletionTarget, Is.Null);
        }

        [Test]
        public void Scan_AFolderMissingFromAParentThatCanBeListed_IsStale()
        {
            Seed("vs-sssi-ee");
            w.FileSystem.AddDirectory(@"D:\Old Games\Other");

            CleanupItem item = Item(CreateCleanup().Scan(w.Discover()), "vs-sssi-ee");

            Assert.That(item.State, Is.EqualTo(CleanupState.Stale));
            Assert.That(item.IsOffered, Is.True);
        }

        /// <summary>
        /// ADR 0007 amendment: a key of a product that has an installation is never offered; the game settings keys are then
        /// the player's settings and not shown, their VirtualStore copies are shown read-only.
        /// </summary>
        [Test]
        public void Scan_TheKeysOfAnInstalledProduct_AreKept()
        {
            w.World.AddForeignInstallation(@"C:\Games\Empire Earth");
            Seed("vs-sssi-ee");
            Seed("hkcu-neoee-ee");

            CleanupScan scan = CreateCleanup().Scan(w.Discover());

            Assert.That(Item(scan, "hkcu-ee-ee").State, Is.EqualTo(CleanupState.InstallationFound));
            Assert.That(Item(scan, "hkcu-ee-ee").IsShown, Is.False, "the active settings are no leftover");
            Assert.That(Item(scan, "vs-sssi-ee").State, Is.EqualTo(CleanupState.InstallationFound));
            Assert.That(Item(scan, "vs-sssi-ee").IsShown, Is.True);
            Assert.That(Item(scan, "vs-sssi-ee").Advice.Code, Is.EqualTo(CleanupAdviceCode.KeepInstallationFound));
            Assert.That(Item(scan, "hkcu-neoee-ee").State, Is.EqualTo(CleanupState.Stale), "no NeoEE installation");
            Assert.That(scan.Offered.Select(item => item.Entry.Id), Is.EqualTo(new[] { "hkcu-neoee-ee" }));
        }

        [Test]
        public void Scan_TheNeoKeysOfANeoEEInstallation_AreKept()
        {
            w.World.AddCommunityInstallation(GameSettingsWorld.NeoRoot, Product.NeoEE);
            Seed("hkcu-neoee-aoc");
            Seed("hkcu-ee-ee");

            CleanupScan scan = CreateCleanup().Scan(w.Discover());

            Assert.That(Item(scan, "hkcu-neoee-ee").State, Is.EqualTo(CleanupState.InstallationFound));
            Assert.That(Item(scan, "hkcu-neoee-aoc").State, Is.EqualTo(CleanupState.InstallationFound));
            Assert.That(Item(scan, "hkcu-ee-ee").State, Is.EqualTo(CleanupState.Stale));
        }

        [Test]
        public void Scan_AKeyWhoseFolderExists_IsKept()
        {
            Seed("vs-maddoc-aoc", OldAocFolder);
            w.FileSystem.AddDirectory(OldAocFolder);

            CleanupItem item = Item(CreateCleanup().Scan(w.Discover()), "vs-maddoc-aoc");

            Assert.That(item.State, Is.EqualTo(CleanupState.FolderExists));
            Assert.That(item.IsOffered, Is.False);
            Assert.That(item.Advice.Code, Is.EqualTo(CleanupAdviceCode.KeepFolderExists));
            Assert.That(item.Advice.Folder, Is.EqualTo(OldAocFolder).IgnoreCase);
        }

        /// <summary>A missing USB stick or network drive is not a removed installation (ADR 0007 amendment of the design review).</summary>
        [TestCase(@"E:\Empire Earth", DriveKind.Removable)]
        [TestCase(@"N:\Games\Empire Earth", DriveKind.Network)]
        [TestCase(@"Q:\Empire Earth", DriveKind.NotFound)]
        [TestCase(@"\\server\games\Empire Earth", DriveKind.Network)]
        public void Scan_AFolderOnADriveThatIsNotPresentFixedAndLocal_IsKept(string folder, DriveKind kind)
        {
            w.FileSystem.AddDrive("E:", DriveKind.Removable);
            w.FileSystem.AddDrive("N:", DriveKind.Network);
            RegistryLocation key = Seed("hkcu-ee-ee", null);
            w.RawRegistry.Seed(key, ContractNames.InstalledFromVolumeName,
                RegistryValue.FromString(folder.StartsWith(@"\\", StringComparison.Ordinal) ? @"\\server" : folder.Substring(0, 2)));
            w.RawRegistry.Seed(key, ContractNames.InstalledFromDirectoryName,
                RegistryValue.FromString(folder.StartsWith(@"\\", StringComparison.Ordinal) ? @"\games\Empire Earth\" : folder.Substring(2) + @"\"));

            CleanupItem item = Item(CreateCleanup().Scan(w.Discover()), "hkcu-ee-ee");

            Assert.That(item.State, Is.EqualTo(CleanupState.DriveNotFixed));
            Assert.That(item.DriveKind, Is.EqualTo(kind));
            Assert.That(item.IsShown, Is.False);
            Assert.That(item.Advice.Code, Is.EqualTo(CleanupAdviceCode.KeepDriveNotFixed));
        }

        [Test]
        public void Scan_AKeyThatNamesNoFolder_IsKept()
        {
            Seed("vs-sssi-ee", null);
            RegistryLocation onlyVolume = Seed("vs-maddoc-aoc", null);
            w.RawRegistry.Seed(onlyVolume, ContractNames.InstalledFromVolumeName, RegistryValue.FromString("D:"));

            CleanupScan scan = CreateCleanup().Scan(w.Discover());

            Assert.That(Item(scan, "vs-sssi-ee").State, Is.EqualTo(CleanupState.NoFolderNamed));
            Assert.That(Item(scan, "vs-maddoc-aoc").State, Is.EqualTo(CleanupState.NoFolderNamed));
            Assert.That(scan.HasCandidates, Is.False);
            Assert.That(scan.ReadOnly.Select(item => item.Advice.Code).Distinct(), Is.EqualTo(new[] { CleanupAdviceCode.KeepNoFolderNamed }));
        }

        [Test]
        public void Scan_AnUnreadableKey_IsKept()
        {
            RegistryLocation key = Seed("vs-sssi-ee");
            RegistryLocation values = Seed("vs-maddoc-aoc");
            w.RawRegistry.SetFault(key, RegistryStatus.AccessDenied);

            CleanupScan scan = CreateCleanup().Scan(w.Discover());

            Assert.That(Item(scan, "vs-sssi-ee").State, Is.EqualTo(CleanupState.Unreadable));
            Assert.That(Item(scan, "vs-sssi-ee").Advice.Code, Is.EqualTo(CleanupAdviceCode.KeepUnreadable));
            Assert.That(Item(scan, "vs-maddoc-aoc").State, Is.EqualTo(CleanupState.Stale), "one bad key does not stop the scan: " + values);
        }

        /// <summary>HKLM keys are only advice (contract 4.1: no elevation): with no HKCU candidate the page has nothing to delete.</summary>
        [Test]
        public void Scan_StaleHklmKeys_AreAdviceOnly()
        {
            Seed("hklm32-sssi-ee");
            Seed("hklm32-maddoc-aoc", OldAocFolder);

            CleanupScan scan = CreateCleanup().Scan(w.Discover());

            Assert.That(scan.HasCandidates, Is.False);
            Assert.That(scan.ReadOnly.Select(item => item.Entry.Id), Is.EqualTo(new[] { "hklm32-sssi-ee", "hklm32-maddoc-aoc" }));
            Assert.That(scan.ReadOnly.Select(item => item.Advice.Code).Distinct(),
                Is.EqualTo(new[] { CleanupAdviceCode.ExportThenDeleteAsAdministrator }));
        }

        [Test]
        public void Scan_TheHklmKeyOfAForeignInstallationThatWasFound_IsKept()
        {
            w.World.AddForeignInstallation(@"C:\Sierra\Empire Earth", RegistryHive.LocalMachine, RegistryView.Registry32);

            CleanupItem item = Item(CreateCleanup().Scan(w.Discover()), "hklm32-sssi-ee");

            Assert.That(item.State, Is.EqualTo(CleanupState.InstallationFound));
            Assert.That(item.Advice.Code, Is.EqualTo(CleanupAdviceCode.KeepInstallationFound));
            Assert.That(item.Folder, Is.EqualTo(@"C:\SIERRA\Empire Earth").IgnoreCase);
        }

        /// <summary>Contract 3.8, O8: <c>Software\Sierra</c> is "do not delete"; only the existence of <c>CDKeys</c> is read.</summary>
        [Test]
        public void Scan_SoftwareSierra_IsProtected_AndSaysWhetherTheCdKeysExist()
        {
            w.RawRegistry.Seed(RegistryLocation.Parse(@"HKLM32\Software\Sierra\CDKeys"), "Empire Earth", RegistryValue.FromString("NOT-A-KEY-0000"));
            w.RawRegistry.SeedKey(RegistryLocation.Parse(@"HKCU\Software\Sierra\Other"));

            CleanupScan scan = CreateCleanup().Scan(w.Discover());

            Assert.That(Item(scan, "hklm32-sierra").State, Is.EqualTo(CleanupState.Protected));
            Assert.That(Item(scan, "hklm32-sierra").Advice.CdKeysExist, Is.True);
            Assert.That(Item(scan, "hkcu-sierra").Advice.CdKeysExist, Is.False);
            Assert.That(Item(scan, "hklm64-sierra").State, Is.EqualTo(CleanupState.Missing));
            Assert.That(scan.ReadOnly.Select(item => item.Advice.Code).Distinct(), Is.EqualTo(new[] { CleanupAdviceCode.DoNotDeleteContainsCdKeys }));
            Assert.That(w.Logger.Messages.Any(message => message.Contains("NOT-A-KEY")), Is.False, "never a value of the CD keys");
        }

        [Test]
        public void Scan_On32BitWindows_ListsTheOneHklmKeyOnce()
        {
            var world = new InstallationWorld(is32BitWindows: true);
            world.Registry.Seed(RegistryLocation.LocalMachine32(@"Software\Sierra\CDKeys"), "Empire Earth", RegistryValue.FromString("NOT-A-KEY-0000"));
            world.Registry.SeedKey(RegistryLocation.LocalMachine32(@"Software\SSSI\Empire Earth"));
            world.SetInstalledFrom(RegistryLocation.LocalMachine32(@"Software\SSSI\Empire Earth"), OldFolder);
            var cleanup = new RegistryCleanup(world.Registry, world.FileSystem, new MutationGuard(new FakeMutexProbe(), world.Logger),
                new BackupLocations(GameSettingsWorld.BackupsFolder, world.FileSystem, world.Clock, world.Logger), world.Logger);

            CleanupScan scan = cleanup.Scan(world.Discover());

            Assert.That(scan.ReadOnly.Select(item => item.Entry.Id), Is.EqualTo(new[] { "hklm32-sssi-ee", "hklm32-sierra" }));
        }

        [Test]
        public void Scan_IsReadOnly()
        {
            Seed("hkcu-ee-ee");
            Seed("hklm32-sssi-ee");
            SeedCdKeys();
            var cleanup = new RegistryCleanup(new WriteForbiddingRegistry(w.RawRegistry), new WriteForbiddingFileSystem(w.FileSystem),
                w.Guard, w.Backups, w.Logger);

            Assert.That(cleanup.Scan(w.Discover()).HasCandidates, Is.True);
        }

        // --- Delete ----------------------------------------------------------------------------------------------------

        /// <summary>
        /// Every key the launcher may delete, stale, with the CD keys next to them: the selected keys go, a backup restores
        /// them exactly, and the CD keys are unchanged (contract 3.8; test plan WP8-01 checks the same on Windows).
        /// </summary>
        [Test]
        public void Delete_BacksUpThenDeletesTheSelectedKeys_AndNeverTheCdKeys()
        {
            List<RegistryLocation> keys = CleanupCandidates.All.Where(entry => entry.Scope == CleanupScope.LauncherDeletes)
                                                           .Select(entry => Seed(entry.Id)).ToList();
            SeedCdKeys();
            IReadOnlyList<string> before = w.ValuesBelow(keys.ToArray());
            IReadOnlyList<string> cdKeys = CdKeyValues();
            RegistryCleanup cleanup = CreateCleanup();
            CleanupScan scan = cleanup.Scan(w.Discover());
            Assert.That(scan.Offered, Has.Count.EqualTo(8), string.Join("; ", scan.Items));

            CleanupResult result = cleanup.Delete(scan, scan.Offered);

            Assert.That(result.Outcome, Is.EqualTo(CleanupOutcome.Done), result.ToString());
            Assert.That(result.Deleted, Is.EqualTo(keys));
            foreach (RegistryLocation key in keys)
                Assert.That(w.RawRegistry.ProbeKey(key).Status, Is.EqualTo(RegistryStatus.Missing), key.ToString());
            Assert.That(CdKeyValues(), Is.EqualTo(cdKeys), "the CD keys are untouched");
            Assert.That(CdKeyValues(), Is.Not.Empty);
            Assert.That(result.BackupFile, Does.StartWith(GameSettingsWorld.BackupsFolder + @"\").And.EndWith("_registry-cleanup.reg"));
            Assert.That(WinPath.GetFileName(result.BackupFolder), Does.EndWith("_registry-cleanup"));

            RegFileImporter.Import(w.FileSystem.GetContent(result.BackupFile), w.RawRegistry);
            Assert.That(w.ValuesBelow(keys.ToArray()), Is.EqualTo(before), "double-clicking the backup restores the keys");
            Assert.That(w.Logger.MessagesOf(LogLevel.Info).Count(message => message.StartsWith("Registry cleanup: deleted ", StringComparison.Ordinal)),
                Is.EqualTo(8));
            Assert.That(w.Logger.MessagesOf(LogLevel.Error), Is.Empty);
            Assert.That(w.Logger.Messages.Any(message => message.Contains("NOT-A-KEY")), Is.False);
        }

        [Test]
        public void Delete_OnlyTheSelection()
        {
            Seed("hkcu-ee-ee");
            Seed("vs-sssi-ee");
            RegistryCleanup cleanup = CreateCleanup();
            CleanupScan scan = cleanup.Scan(w.Discover());

            CleanupResult result = cleanup.Delete(scan, new[] { Item(scan, "vs-sssi-ee") });

            Assert.That(result.Outcome, Is.EqualTo(CleanupOutcome.Done));
            Assert.That(w.RawRegistry.ProbeKey(Entry("vs-sssi-ee").Key).Status, Is.EqualTo(RegistryStatus.Missing));
            Assert.That(w.RawRegistry.ProbeKey(Entry("hkcu-ee-ee").Key).IsOk, Is.True);
        }

        [TestCase(true, TestName = "Delete_BackupFileFails_NothingIsDeleted")]
        [TestCase(false, TestName = "Delete_BackupFolderFails_NothingIsDeleted")]
        public void Delete_WhenTheBackupFails_NothingIsDeleted(bool fileFails)
        {
            Seed("hkcu-ee-ee");
            RegistryCleanup cleanup = CreateCleanup();
            CleanupScan scan = cleanup.Scan(w.Discover());
            int changes = w.Changes.Count;
            w.FileSystem.FailOn(GameSettingsWorld.BackupsFolder,
                fileFails ? FileSystemOperation.Write : FileSystemOperation.CreateDirectory, FileSystemStatus.IoError);

            CleanupResult result = cleanup.Delete(scan, scan.Offered);

            Assert.That(result.Outcome, Is.EqualTo(CleanupOutcome.BackupFailed));
            Assert.That(result.Deleted, Is.Empty);
            Assert.That(w.Changes, Has.Count.EqualTo(changes));
            Assert.That(w.RawRegistry.ProbeKey(Entry("hkcu-ee-ee").Key).IsOk, Is.True);
        }

        [Test]
        public void Delete_WhenAKeyCannotBeExported_NothingIsDeleted()
        {
            Seed("hkcu-ee-ee");
            Seed("vs-sssi-ee");
            RegistryCleanup cleanup = CreateCleanup();
            CleanupScan scan = cleanup.Scan(w.Discover());
            w.RawRegistry.SetFault(Entry("vs-sssi-ee").Key.Child(ContractNames.GameOptionsSubKeyName), RegistryStatus.AccessDenied);

            CleanupResult result = cleanup.Delete(scan, scan.Offered);

            Assert.That(result.Outcome, Is.EqualTo(CleanupOutcome.BackupFailed));
            Assert.That(w.RawRegistry.ProbeKey(Entry("hkcu-ee-ee").Key).IsOk, Is.True);
        }

        /// <summary>
        /// Security review: a value name with a line break in a key to delete cannot be backed up without injecting lines into
        /// the .reg file, so the backup fails and nothing is deleted.
        /// </summary>
        [Test]
        public void Delete_WhenANameCannotBeWrittenToTheBackup_NothingIsDeleted()
        {
            RegistryLocation key = Seed("hkcu-ee-ee");
            w.RawRegistry.Seed(key, "x\"=\"1\"\r\n[-HKEY_LOCAL_MACHINE\\SOFTWARE\\Sierra\\CDKeys]", RegistryValue.FromDWord(1));
            RegistryCleanup cleanup = CreateCleanup();
            CleanupScan scan = cleanup.Scan(w.Discover());
            int changes = w.Changes.Count;

            CleanupResult result = cleanup.Delete(scan, scan.Offered);

            Assert.That(result.Outcome, Is.EqualTo(CleanupOutcome.BackupFailed));
            Assert.That(result.Deleted, Is.Empty);
            Assert.That(w.Changes, Has.Count.EqualTo(changes));
            Assert.That(w.RawRegistry.ProbeKey(key).IsOk, Is.True);
        }

        /// <summary>
        /// Security review (ADR 0007, D6): a symbolic registry link below a key to delete could point at
        /// <c>Software\Sierra\CDKeys</c>; the cleanup never follows it, so the backup fails and nothing is deleted.
        /// </summary>
        [Test]
        public void Delete_WhenTheTreeHoldsASymbolicLink_NothingIsDeleted()
        {
            SeedCdKeys();
            RegistryLocation key = Seed("hkcu-ee-ee");
            w.RawRegistry.SeedLink(key.Child("Sierra"));
            RegistryCleanup cleanup = CreateCleanup();
            CleanupScan scan = cleanup.Scan(w.Discover());
            int changes = w.Changes.Count;

            CleanupResult result = cleanup.Delete(scan, scan.Offered);

            Assert.That(result.Outcome, Is.EqualTo(CleanupOutcome.BackupFailed));
            Assert.That(result.Deleted, Is.Empty);
            Assert.That(w.Changes, Has.Count.EqualTo(changes));
            Assert.That(w.RawRegistry.ProbeKey(key).IsOk, Is.True);
            Assert.That(w.RawRegistry.ProbeKey(RegistryLocation.CurrentUser(@"Software\Sierra\CDKeys")).IsOk, Is.True);
        }

        /// <summary>A link that appears after the backup (between backup and deletion) stops the deletion too.</summary>
        [Test]
        public void Delete_WhenASymbolicLinkAppearsAfterTheBackup_TheKeyIsNotDeleted()
        {
            RegistryLocation key = Seed("hkcu-ee-ee");
            RegistryCleanup cleanup = CreateCleanup();
            CleanupScan scan = cleanup.Scan(w.Discover());
            w.FileSystem.OnRead = path =>
            {
                // The read-back of the backup file: the backup is written, the deletion comes next.
                if (path.EndsWith(".reg", StringComparison.OrdinalIgnoreCase))
                    w.RawRegistry.SeedLink(key.Child("Sierra"));
            };

            CleanupResult result = cleanup.Delete(scan, scan.Offered);

            Assert.That(result.Outcome, Is.EqualTo(CleanupOutcome.Failed));
            Assert.That(result.Deleted, Is.Empty);
            Assert.That(result.BackupFile, Is.Not.Null);
            Assert.That(w.RawRegistry.ProbeKey(key).IsOk, Is.True);
        }

        /// <summary>ADR 0016: the deletion is blocked by a setup and by a game and changes nothing, no backup either.</summary>
        [Test]
        public void Delete_IsBlockedBySetupAndGame(
            [Values("EE_Setup", "NeoEE_Setup", "StainlessSteelStudiosPresentsEmpireEarth", "MadDocSoftwarePresentsEmpireEarthExpansion")]
            string mutex)
        {
            Seed("hkcu-ee-ee");
            RegistryCleanup cleanup = CreateCleanup();
            CleanupScan scan = cleanup.Scan(w.Discover());
            int changes = w.Changes.Count;
            w.Mutexes.With(mutex);

            CleanupResult result = cleanup.Delete(scan, scan.Offered);

            Assert.That(result.Outcome, Is.EqualTo(CleanupOutcome.Blocked));
            Assert.That(result.Block.Block, Is.EqualTo(mutex.EndsWith("_Setup", StringComparison.Ordinal)
                ? MutationBlock.SetupRunning : MutationBlock.GameRunning));
            Assert.That(w.Changes, Has.Count.EqualTo(changes));
            Assert.That(w.FileSystem.DirectoryExists(GameSettingsWorld.BackupsFolder), Is.False, "no backup either");
        }

        /// <summary>The scan may be old: a folder that came back (a drive plugged in) keeps the key, and nothing is changed.</summary>
        [Test]
        public void Delete_WhenAKeyIsNotStaleAnyMore_NothingIsChanged()
        {
            Seed("hkcu-ee-ee");
            Seed("vs-sssi-ee", @"D:\Other\Empire Earth");
            RegistryCleanup cleanup = CreateCleanup();
            CleanupScan scan = cleanup.Scan(w.Discover());
            w.FileSystem.AddDirectory(@"D:\Other\Empire Earth");
            int changes = w.Changes.Count;

            CleanupResult result = cleanup.Delete(scan, scan.Offered);

            Assert.That(result.Outcome, Is.EqualTo(CleanupOutcome.NoLongerStale));
            Assert.That(w.Changes, Has.Count.EqualTo(changes));
            Assert.That(w.FileSystem.DirectoryExists(GameSettingsWorld.BackupsFolder), Is.False);
        }

        /// <summary>Only offered items of the scan can be deleted: nothing else reaches the registry.</summary>
        [Test]
        public void Delete_RefusesASelectionThatIsNotOffered()
        {
            Seed("hkcu-ee-ee");
            Seed("hklm32-sssi-ee");
            SeedCdKeys();
            RegistryCleanup cleanup = CreateCleanup();
            CleanupScan scan = cleanup.Scan(w.Discover());
            CleanupScan other = cleanup.Scan(w.Discover());
            int changes = w.Changes.Count;

            Assert.That(() => cleanup.Delete(scan, new CleanupItem[0]), Throws.ArgumentException);
            Assert.That(() => cleanup.Delete(scan, other.Offered), Throws.ArgumentException, "an item of another scan");
            Assert.That(() => cleanup.Delete(scan, new[] { Item(scan, "hklm32-sssi-ee") }), Throws.ArgumentException, "advice only");
            Assert.That(() => cleanup.Delete(scan, new[] { Item(scan, "hklm32-sierra") }), Throws.ArgumentException, "protected");
            Assert.That(() => cleanup.Delete(scan, new[] { Item(scan, "hkcu-neoee-ee") }), Throws.ArgumentException, "missing");
            Assert.That(w.Changes, Has.Count.EqualTo(changes));
        }

        [Test]
        public void Delete_WhenAKeyCannotBeDeleted_StopsWithTheBackup()
        {
            Seed("hkcu-ee-ee");
            Seed("vs-sssi-ee");
            RegistryCleanup cleanup = CreateCleanup();
            CleanupScan scan = cleanup.Scan(w.Discover());
            w.RawRegistry.SetFault(Entry("vs-sssi-ee").Key, RegistryStatus.AccessDenied, writesOnly: true);

            CleanupResult result = cleanup.Delete(scan, scan.Offered);

            Assert.That(result.Outcome, Is.EqualTo(CleanupOutcome.Failed));
            Assert.That(result.Deleted, Is.EqualTo(new[] { Entry("hkcu-ee-ee").Key }));
            Assert.That(result.BackupFile, Is.Not.Null);
            Assert.That(w.FileSystem.FileExists(result.BackupFile), Is.True);
        }

        /// <summary>On Windows 7 (no compatibility switches) the policy still allows the cleanup of the listed keys.</summary>
        [Test]
        public void Delete_WorksWithThePolicyOfWindows7()
        {
            w = new GameSettingsWorld(FakeSystemInfo.Windows7());
            Seed("vs-maddoc-aoc-wow64", OldAocFolder);
            RegistryCleanup cleanup = CreateCleanup();
            CleanupScan scan = cleanup.Scan(w.Discover());

            Assert.That(cleanup.Delete(scan, scan.Offered).Outcome, Is.EqualTo(CleanupOutcome.Done));
            Assert.That(LauncherWritePolicy.For(w.SystemInfo), Is.SameAs(LauncherWritePolicy.WithoutLayerEntries));
        }
    }
}

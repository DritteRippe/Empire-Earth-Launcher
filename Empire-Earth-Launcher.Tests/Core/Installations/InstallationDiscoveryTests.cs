using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
    /// The discovery beyond the table of <see cref="DiscoveryContractTests"/>: the real game folders of foreign
    /// installations (ADR 0015), every form of the user choice, newer contract versions, the shared game settings keys,
    /// errors, read-only, 32-bit Windows and the asynchronous start.
    /// </summary>
    [TestFixture]
    public class InstallationDiscoveryTests
    {
        private const string NeoRoot = @"C:\Program Files (x86)\Neo Empire Earth";
        private const string EERoot = @"C:\Program Files (x86)\Empire Earth";

        private InstallationWorld world;

        [SetUp]
        public void SetUp()
        {
            world = new InstallationWorld();
        }

        // --- Real game folders (ADR 0015) ---------------------------------------------------------------------------

        [Test]
        public void ForeignInstallation_WithAnotherFolderName_KeepsItsRealFolders()
        {
            world.AddForeignInstallation(@"C:\Games\EE", aocFolder: @"C:\Games\AoC");

            Installation installation = world.Discover().Selected;

            Assert.That(installation.EeFolder, Is.EqualTo(@"C:\Games\EE").IgnoreCase);
            Assert.That(installation.AocFolder, Is.EqualTo(@"C:\Games\AoC").IgnoreCase);
            Assert.That(installation.Root, Is.EqualTo(@"C:\Games").IgnoreCase);
            Assert.That(installation.GetGameFolder(Game.EmpireEarth), Is.SameAs(installation.EeFolder));
            Assert.That(installation.GetGameFolder(Game.ArtOfConquest), Is.SameAs(installation.AocFolder));
            Assert.That(installation.GetGameSettingsKey(Game.EmpireEarth), Is.EqualTo(@"Software\SSSI\Empire Earth"));
            Assert.That(installation.State, Is.EqualTo(InstallationState.Ok));
        }

        [Test]
        public void ForeignInstallation_OnADriveRoot_HasTheDriveAsRoot()
        {
            world.AddForeignInstallation(@"D:\Empire Earth");

            Installation installation = world.Discover().Selected;

            Assert.That(installation.EeFolder, Is.EqualTo(@"D:\Empire Earth"));
            Assert.That(installation.Root, Is.EqualTo(@"D:\"));
            Assert.That(WinPath.IsSamePath(installation.Root, "D:"), Is.True, "the root D: of the work package");
            Assert.That(installation.Kind, Is.EqualTo(InstallationKind.Foreign));
        }

        [Test]
        public void CommunityInstallation_HasTheFoldersOfTheContract()
        {
            world.AddCommunityInstallation(NeoRoot, Product.NeoEE);

            Installation installation = world.Discover().Selected;

            Assert.That(installation.EeFolder, Is.EqualTo(NeoRoot + @"\Empire Earth"));
            Assert.That(installation.AocFolder, Is.EqualTo(NeoRoot + @"\Empire Earth - The Art of Conquest"));
            Assert.That(installation.HasArtOfConquest, Is.True);
            Assert.That(installation.GetGameSettingsKey(Game.ArtOfConquest), Is.EqualTo(@"Software\Neo\Art of Conquest"));
            Assert.That(installation.HasUnreliableIntegrity, Is.False);
        }

        // --- User choice: root, EE folder, AoC folder, missing ------------------------------------------------------

        [Test]
        public void UserChoice_TheRootOfAForeignInstallation_SelectsIt()
        {
            world.AddForeignInstallation(@"C:\Games\EE", aocFolder: @"C:\Games\AoC");
            world.AddCommunityInstallation(NeoRoot, Product.NeoEE);

            DiscoveryResult result = world.Discover(@"C:\Games");

            Assert.That(result.IsSelectedByUser, Is.True);
            Assert.That(result.Selected.EeFolder, Is.EqualTo(@"C:\Games\EE").IgnoreCase);
            Assert.That(result.Installations, Has.Count.EqualTo(2));
            Assert.That(result.Installations[0], Is.SameAs(result.Selected), "the chosen installation comes first");
        }

        [Test]
        public void UserChoice_TheEEFolderOfAForeignInstallation_SelectsIt()
        {
            world.AddForeignInstallation(@"C:\Games\EE");

            DiscoveryResult result = world.Discover(@"C:\Games\EE\");

            Assert.That(result.Installations, Has.Count.EqualTo(1));
            Assert.That(result.Selected.Sources, Is.EqualTo(new[] { InstallationSource.UserChoice, InstallationSource.InstalledFrom }));
        }

        [Test]
        public void UserChoice_TheAocFolder_SelectsTheInstallation_AndNamesItsAocFolder()
        {
            // No AoC "Installed From" values: only the choice names the AoC folder.
            world.AddForeignInstallation(@"C:\Games\EE");
            world.AddArtOfConquest(@"C:\Games\AoC");

            DiscoveryResult result = world.Discover(@"C:\Games\AoC");

            Assert.That(result.Installations, Has.Count.EqualTo(1));
            Assert.That(result.Selected.EeFolder, Is.EqualTo(@"C:\Games\EE").IgnoreCase, "the data of source 4 wins");
            Assert.That(result.Selected.AocFolder, Is.EqualTo(@"C:\Games\AoC"));
            Assert.That(result.IsSelectedByUser, Is.True);
        }

        [Test]
        public void UserChoice_TheAocFolderOfACommunityInstallation_SelectsIt()
        {
            world.AddCommunityInstallation(NeoRoot, Product.NeoEE);

            DiscoveryResult result = world.Discover(NeoRoot + @"\Empire Earth - The Art of Conquest");

            Assert.That(result.Installations, Has.Count.EqualTo(1));
            Assert.That(result.IsSelectedByUser, Is.True);
        }

        [Test]
        public void UserChoice_AFolderNoSourceKnows_IsAnInstallationOfItsOwn()
        {
            world.AddEmpireEarth(@"D:\Backup\EE Copy");
            world.AddArtOfConquest(@"D:\Backup\Empire Earth - The Art of Conquest");

            Installation installation = world.Discover(@"D:\Backup\EE Copy").Selected;

            Assert.That(installation.EeFolder, Is.EqualTo(@"D:\Backup\EE Copy"));
            Assert.That(installation.Root, Is.EqualTo(@"D:\Backup"));
            Assert.That(installation.AocFolder, Is.EqualTo(@"D:\Backup\Empire Earth - The Art of Conquest"));
            Assert.That(installation.Kind, Is.EqualTo(InstallationKind.Foreign));
            Assert.That(installation.Sources, Is.EqualTo(new[] { InstallationSource.UserChoice }));
            Assert.That(installation.State, Is.EqualTo(InstallationState.Ok));
        }

        [Test]
        public void UserChoice_ADriveRootThatHoldsTheEEFolder_IsTheRoot()
        {
            world.AddEmpireEarth(@"D:\Empire Earth");

            Installation installation = world.Discover(@"D:\").Selected;

            Assert.That(installation.Root, Is.EqualTo(@"D:\"));
            Assert.That(installation.EeFolder, Is.EqualTo(@"D:\Empire Earth"));
        }

        [TestCase(@"D:\Old Games\Empire Earth", TestName = "UserChoice_MissingEEFolder_StaysSelected")]
        [TestCase(@"D:\Old Games", TestName = "UserChoice_MissingRoot_StaysSelected")]
        [TestCase(@"D:\Old Games\Empire Earth - The Art of Conquest", TestName = "UserChoice_MissingAocFolder_StaysSelected")]
        public void UserChoice_ThatDoesNotExist_StaysSelected(string choice)
        {
            world.AddCommunityInstallation(NeoRoot, Product.NeoEE);

            DiscoveryResult result = world.Discover(choice);

            Assert.That(result.IsSelectedByUser, Is.True);
            Assert.That(result.Selected.State, Is.EqualTo(InstallationState.FolderMissing));
            Assert.That(result.Selected.HasFolder(choice), Is.True, "the user sees the folder they chose");
            Assert.That(result.Installations, Has.Count.EqualTo(2));
            Assert.That(world.Logger.MessagesOf(LogLevel.Warning).Single(), Does.Contain(choice));
        }

        [Test]
        public void UserChoice_AFolderWithoutTheProgram_IsDamaged()
        {
            world.FileSystem.AddDirectory(@"D:\Empty");

            Installation installation = world.Discover(@"D:\Empty").Selected;

            Assert.That(installation.State, Is.EqualTo(InstallationState.Damaged));
            Assert.That(installation.MissingPrograms, Is.EqualTo(new[] { Game.EmpireEarth }));
        }

        // --- Contract version, modes, versions ----------------------------------------------------------------------

        [Test]
        public void HigherContractVersion_IsFlagged()
        {
            world.AddCommunityFiles(NeoRoot, Product.NeoEE);
            world.AddInstallInfo(NeoRoot, Product.NeoEE, ContractNames.ContractVersion + 1);

            Installation installation = world.Discover(NeoRoot).Selected;

            Assert.That(installation.Kind, Is.EqualTo(InstallationKind.Community));
            Assert.That(installation.ContractVersion, Is.EqualTo(ContractNames.ContractVersion + 1));
            Assert.That(installation.HasNewerContract, Is.True);
            Assert.That(installation.AppId, Is.EqualTo(InstallationWorld.NeoEEAppId), "root, product and AppId are still used");
        }

        [Test]
        public void CurrentContractVersion_IsNotFlagged()
        {
            world.AddCommunityInstallation(NeoRoot, Product.NeoEE);

            Assert.That(world.Discover().Selected.HasNewerContract, Is.False);
        }

        [Test]
        public void ValuesOfInstallIni_WinOverTheRecord()
        {
            world.AddCommunityInstallation(NeoRoot, Product.NeoEE);
            world.Registry.Seed(InstallationWorld.Hklm64(Product.NeoEE.InstallRecordKey), ContractNames.GameVersionName,
                RegistryValue.FromString("9.9"));

            Installation installation = world.Discover().Selected;

            Assert.That(installation.GameVersion, Is.EqualTo("2.0.0.5"), "contract 1.1: install.ini wins");
            Assert.That(installation.SetupVersion, Is.EqualTo("2.0.0"));
            Assert.That(installation.SetupBuild, Is.EqualTo("a1b2c3d"));
            Assert.That(installation.Components.HasArtOfConquest, Is.True);
            Assert.That(installation.Tasks.Contains("compatibility"), Is.True);
        }

        [Test]
        public void ModeOfARecordWithoutInstallMode_FollowsItsHive()
        {
            world.AddCommunityFiles(@"C:\Users\Player\Games\EE", Product.EE);
            world.AddRecord(RegistryHive.CurrentUser, RegistryView.Default, Product.EE, @"C:\Users\Player\Games\EE", mode: null);

            Assert.That(world.Discover().Selected.Mode, Is.EqualTo(InstallMode.User));
        }

        [Test]
        public void ARecordWithoutInstallIni_IsCommunity_TheLastRunDidNotFinish()
        {
            // install.ini is deleted at the start of the installation step (contract 1.2); a run that broke off leaves the
            // record of the previous run. The integrity check then gives the repair advice (contract 2.5).
            world.AddCommunityInstallation(NeoRoot, Product.NeoEE);
            world.FileSystem.DeleteFile(InstallationWorld.InstallInfoPath(NeoRoot, Product.NeoEE));

            Installation installation = world.Discover().Selected;

            Assert.That(installation.Kind, Is.EqualTo(InstallationKind.Community));
            Assert.That(installation.InstallInfo, Is.Null);
            Assert.That(installation.ContractVersion, Is.EqualTo(1));
            Assert.That(installation.Components.HasArtOfConquest, Is.True, "the components of the uninstall key");
        }

        [Test]
        public void InstallIniOfTheOtherProduct_IsLoggedAndTheFolderDecides()
        {
            world.AddCommunityFiles(NeoRoot, Product.NeoEE);
            world.AddInstallInfo(NeoRoot, Product.NeoEE, text: "[Install]\r\nContractVersion=1\r\nProduct=EE\r\n");

            Installation installation = world.Discover(NeoRoot).Selected;

            Assert.That(installation.Product, Is.SameAs(Product.NeoEE));
            Assert.That(world.LogLinesAbout("names the product"), Has.Length.EqualTo(1));
        }

        [Test]
        public void InstallIniLargerThanTheLimit_IsIgnoredWithOneLogLine()
        {
            world.AddCommunityFiles(@"D:\EE", Product.EE);
            world.FileSystem.AddFile(InstallationWorld.InstallInfoPath(@"D:\EE", Product.EE),
                new byte[InstallInfoFile.MaxFileBytes + 1]);

            Installation installation = world.Discover(@"D:\EE").Selected;

            Assert.That(installation.InstallInfo, Is.Null);
            Assert.That(installation.Kind, Is.EqualTo(InstallationKind.Foreign));
            Assert.That(world.LogLinesAbout(ContractNames.InstallInfoFileName), Has.Length.EqualTo(1));
        }

        [Test]
        public void InvalidContractVersionInInstallIni_IsLogged()
        {
            world.AddCommunityFiles(@"D:\EE", Product.EE);
            world.AddInstallInfo(@"D:\EE", Product.EE, text: "[Install]\r\nContractVersion=one\r\n");

            Assert.That(world.Discover(@"D:\EE").Selected.Kind, Is.EqualTo(InstallationKind.Foreign));
            Assert.That(world.LogLinesAbout("invalid ContractVersion"), Has.Length.EqualTo(1));
        }

        // --- Shared game settings keys (ADR 0015) -------------------------------------------------------------------

        [Test]
        public void SeveralInstallationsOfAProduct_ShareItsGameSettings()
        {
            world.AddLegacyInstallation(EERoot, Product.EE);
            world.AddForeignInstallation(@"C:\Retail\Empire Earth", RegistryHive.LocalMachine, RegistryView.Registry32);
            world.AddCommunityInstallation(NeoRoot, Product.NeoEE);

            DiscoveryResult result = world.Discover();
            Installation legacy = InstallationWorld.ByRoot(result, EERoot);
            Installation retail = InstallationWorld.ByRoot(result, @"C:\Retail");
            Installation neo = InstallationWorld.ByRoot(result, NeoRoot);

            Assert.That(result.ProductsWithSharedSettings, Is.EqualTo(new[] { Product.EE }));
            Assert.That(result.SharingSettingsWith(legacy), Is.EqualTo(new[] { retail }));
            Assert.That(result.SharingSettingsWith(neo), Is.Empty);
            Assert.That(result.IsUnambiguous(neo), Is.True);
            Assert.That(result.IsUnambiguous(legacy), Is.False, "first run only at the first Play or when the user chose it");
        }

        [Test]
        public void TheChosenInstallation_IsUnambiguousEvenIfItSharesItsKey()
        {
            world.AddLegacyInstallation(EERoot, Product.EE);
            world.AddForeignInstallation(@"C:\Retail\Empire Earth", RegistryHive.LocalMachine, RegistryView.Registry32);

            DiscoveryResult result = world.Discover(EERoot);

            Assert.That(result.IsUnambiguous(result.Selected), Is.True);
            Assert.That(result.IsUnambiguous(InstallationWorld.ByRoot(result, @"C:\Retail")), Is.False);
        }

        // --- Errors, read-only, logging ------------------------------------------------------------------------------

        [Test]
        public void EveryDroppedCandidate_HasExactlyOneLogLine()
        {
            world.AddCommunityInstallation(NeoRoot, Product.NeoEE);
            RegistryLocation missingRoot = world.AddRecord(RegistryHive.CurrentUser, RegistryView.Default, Product.EE, @"C:\Gone");
            RegistryLocation noRoot = world.AddUninstallKey(RegistryHive.CurrentUser, RegistryView.Default, Product.EE, null);
            RegistryLocation denied = world.AddUninstallKey(RegistryHive.LocalMachine, RegistryView.Registry32, Product.EE, @"C:\X",
                appId: "22222222-0000-0000-0000-000000000000");
            world.Registry.SetFault(denied, RegistryStatus.IoError);
            world.SetInstalledFrom(InstallationWorld.Hklm32(@"Software\SSSI\Empire Earth"), @"C:\Not There\Empire Earth");

            DiscoveryResult result = world.Discover();

            Assert.That(result.Installations.Select(installation => installation.Root), Is.EqualTo(new[] { NeoRoot }));
            foreach (RegistryLocation key in new[] { missingRoot, noRoot, denied, InstallationWorld.Hklm32(@"Software\SSSI\Empire Earth") })
            {
                string[] lines = world.LogLinesAbout(key.ToString());
                Assert.That(lines, Has.Length.EqualTo(1), key.ToString());
                Assert.That(lines[0], Does.StartWith("Warning"));
            }
            Assert.That(world.Logger.MessagesOf(LogLevel.Error), Is.Empty);
        }

        [Test]
        public void Discovery_CallsNothingThatWrites()
        {
            // The world's discovery uses registry and file system wrappers that fail the test at the first write.
            world.AddCommunityInstallation(NeoRoot, Product.NeoEE);
            world.AddLegacyInstallation(EERoot, Product.EE, admin: false);
            world.AddForeignInstallation(@"D:\Empire Earth", RegistryHive.LocalMachine, RegistryView.Registry32, aocFolder: @"D:\AoC");
            world.AddEmpireEarth(@"D:\Launcher Copy\Empire Earth");
            string[] filesBefore = world.FileSystem.AllFiles.ToArray();

            DiscoveryResult result = world.Discover(@"D:\Missing", @"D:\Launcher Copy\Empire Earth");

            Assert.That(result.Installations, Has.Count.EqualTo(5));
            Assert.That(world.Registry.Changes, Is.Empty);
            Assert.That(world.FileSystem.AllFiles, Is.EqualTo(filesBefore));
        }

        [Test]
        public void EveryInstallation_IsLoggedOnce_WithItsFolders()
        {
            world.AddCommunityInstallation(NeoRoot, Product.NeoEE);

            world.Discover();

            string line = world.Logger.Messages.Single(message => message.StartsWith("Info: Discovery: installation", StringComparison.Ordinal));
            Assert.That(line, Does.Contain("NeoEE community (admin), root " + NeoRoot + ", EE folder " + NeoRoot + @"\Empire Earth"));
            Assert.That(line, Does.Contain("state ok").And.Contain("sources 2,3,4"));
            Assert.That(world.LogLinesAbout("selected " + NeoRoot), Has.Length.EqualTo(1));
        }

        [Test]
        public void ThirtyTwoBitWindows_GivesNoDuplicates()
        {
            world = new InstallationWorld(is32BitWindows: true);
            // An admin installation (HKLM is one view) and a retail installation registered in HKLM.
            world.AddCommunityInstallation(@"C:\Program Files\Neo Empire Earth", Product.NeoEE);
            world.AddLegacyInstallation(@"C:\Program Files\Empire Earth", Product.EE);
            world.AddForeignInstallation(@"C:\Program Files\Sierra\Empire Earth", RegistryHive.LocalMachine, RegistryView.Registry64);

            DiscoveryResult result = world.Discover();

            Assert.That(result.Installations.Select(installation => installation.Root),
                Is.EquivalentTo(new[] { @"C:\Program Files\Neo Empire Earth", @"C:\Program Files\Empire Earth", @"C:\PROGRAM FILES\SIERRA" }));
            Assert.That(world.Logger.Messages.Count(message => message.Contains("Discovery: installation")), Is.EqualTo(3));
            // One candidate per source and folder: NeoEE record, uninstall key and HKCU values; EE uninstall key and HKCU
            // values; the retail values, read once although both HKLM views show them.
            Assert.That(world.Logger.Messages.Count(message => message.Contains("Discovery: found the install root")), Is.EqualTo(6));
        }

        // --- Threading -----------------------------------------------------------------------------------------------

        [Test]
        public void DiscoverAsync_WithABlockingRegistry_ReturnsAnUnfinishedTaskAtOnce()
        {
            world.AddCommunityInstallation(NeoRoot, Product.NeoEE);
            using (var blocking = new BlockingRegistry(world.Registry))
            {
                var discovery = new InstallationDiscovery(blocking, world.FileSystem, world.Logger);

                Task<DiscoveryResult> task = discovery.DiscoverAsync(null, null);

                Assert.That(task.IsCompleted, Is.False, "the caller (the UI thread) must not wait for the registry");
                Assert.That(blocking.Entered.WaitOne(TimeSpan.FromSeconds(10)), Is.True, "the discovery runs elsewhere");
                Assert.That(task.IsCompleted, Is.False);
                blocking.Release();
                Assert.That(task.Wait(TimeSpan.FromSeconds(10)), Is.True);
                Assert.That(task.Result.Selected.Root, Is.EqualTo(NeoRoot));
            }
        }

        [Test]
        public void DiscoverAsync_CanBeCanceled()
        {
            world.AddCommunityInstallation(NeoRoot, Product.NeoEE);
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();

                Task<DiscoveryResult> task = world.CreateDiscovery().DiscoverAsync(null, null, cancellation.Token);

                Assert.That(() => task.Wait(TimeSpan.FromSeconds(10)), Throws.InstanceOf<AggregateException>()
                    .With.InnerException.InstanceOf<OperationCanceledException>());
                Assert.That(task.IsCanceled, Is.True);
            }
        }

        [Test]
        public void Discover_StopsWhenCanceledBetweenCandidates()
        {
            world.AddCommunityInstallation(NeoRoot, Product.NeoEE);
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();

                Assert.That(() => world.CreateDiscovery().Discover(null, null, cancellation.Token),
                    Throws.InstanceOf<OperationCanceledException>());
            }
        }

        [Test]
        public void Arguments_AreChecked()
        {
            Assert.That(() => new InstallationDiscovery(null, world.FileSystem, world.Logger), Throws.ArgumentNullException);
            Assert.That(() => new InstallationDiscovery(world.Registry, null, world.Logger), Throws.ArgumentNullException);
            Assert.That(() => new InstallationDiscovery(world.Registry, world.FileSystem, null), Throws.ArgumentNullException);
        }
    }
}

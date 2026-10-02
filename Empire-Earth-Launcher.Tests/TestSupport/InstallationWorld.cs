using System;
using System.Globalization;
using System.Linq;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Tests.Fakes;
using Microsoft.Win32;

namespace Empire_Earth_Launcher.Tests.TestSupport
{
    /// <summary>
    /// A computer for the discovery tests: an in-memory registry and file system with helpers that leave behind what the
    /// setups (contract 1.1 to 1.3, 1.5), retail, GOG and the games leave behind. Nothing is real; drives C: and D: exist.
    /// </summary>
    internal sealed class InstallationWorld
    {
        /// <summary>An AppId as the build switches give it (contract 1.1 example).</summary>
        public const string NeoEEAppId = "00000000-0000-0000-0000-000000000AEE";

        /// <summary>A second AppId, for EE.</summary>
        public const string EEAppId = "11111111-2222-3333-4444-555555555555";

        public InstallationWorld(bool is32BitWindows = false)
        {
            Clock = new FakeClock();
            Registry = new InMemoryRegistry(is32BitWindows);
            FileSystem = new InMemoryFileSystem(Clock);
            FileSystem.AddDrive("D:");
            Logger = new RecordingLogger();
        }

        public FakeClock Clock { get; }

        public InMemoryRegistry Registry { get; }

        public InMemoryFileSystem FileSystem { get; }

        public RecordingLogger Logger { get; }

        /// <summary>A discovery on this world that fails the test if it writes anything.</summary>
        public InstallationDiscovery CreateDiscovery()
        {
            return new InstallationDiscovery(new WriteForbiddingRegistry(Registry), new WriteForbiddingFileSystem(FileSystem),
                Logger);
        }

        public DiscoveryResult Discover(string userChoice = null, string launcherFolder = null)
        {
            return CreateDiscovery().Discover(userChoice, launcherFolder);
        }

        // --- Hives -------------------------------------------------------------------------------------------------

        public static RegistryLocation Hkcu(string path)
        {
            return RegistryLocation.CurrentUser(path);
        }

        public static RegistryLocation Hklm64(string path)
        {
            return RegistryLocation.LocalMachine64(path);
        }

        public static RegistryLocation Hklm32(string path)
        {
            return RegistryLocation.LocalMachine32(path);
        }

        /// <summary>The same path in another hive and view.</summary>
        public static RegistryLocation In(RegistryHive hive, RegistryView view, string path)
        {
            return new RegistryLocation(hive, view, path);
        }

        // --- Files -------------------------------------------------------------------------------------------------

        /// <summary><c>Empire Earth.exe</c> in <paramref name="eeFolder"/>; with <paramref name="neoee"/> also <c>neoee.dll</c>.</summary>
        public void AddEmpireEarth(string eeFolder, bool neoee = false)
        {
            FileSystem.AddFile(WinPath.Combine(eeFolder, Game.EmpireEarth.ProgramName), "exe");
            if (neoee)
                FileSystem.AddFile(WinPath.Combine(eeFolder, Product.NeoEE.ProductOnlyFileName), "dll");
        }

        /// <summary><c>EE-AOC.exe</c> in <paramref name="aocFolder"/>.</summary>
        public void AddArtOfConquest(string aocFolder)
        {
            FileSystem.AddFile(WinPath.Combine(aocFolder, Game.ArtOfConquest.ProgramName), "exe");
        }

        /// <summary>The standard game folders of a community installation below <paramref name="root"/>.</summary>
        public void AddCommunityFiles(string root, Product product, bool artOfConquest = true)
        {
            AddEmpireEarth(WinPath.Combine(root, Game.EmpireEarth.FolderName), product == Product.NeoEE);
            if (artOfConquest)
                AddArtOfConquest(WinPath.Combine(root, Game.ArtOfConquest.FolderName));
        }

        /// <summary>The path of <c>install.ini</c> of <paramref name="product"/> below <paramref name="root"/>.</summary>
        public static string InstallInfoPath(string root, Product product)
        {
            return WinPath.Combine(root, product.SetupDataFolderName + @"\" + ContractNames.InstallInfoFileName);
        }

        /// <summary>Writes an <c>install.ini</c> as the setup does (ASCII, CRLF).</summary>
        public void AddInstallInfo(string root, Product product, int contractVersion = 1, string mode = "admin",
            string components = "game,gameaoc", string tasks = "compatibility", string appId = null, string text = null)
        {
            string content = text ?? string.Join("\r\n",
                "[Install]",
                "ContractVersion=" + contractVersion.ToString(CultureInfo.InvariantCulture),
                "Product=" + product.Id,
                "AppId=" + (appId ?? AppIdOf(product)),
                "InstallMode=" + mode,
                "GameVersion=2.0.0.5",
                "SetupVersion=2.0.0",
                "SetupBuild=a1b2c3d",
                "Components=" + components,
                "Tasks=" + tasks,
                "Written=2026-10-02 18:04:31",
                "");
            FileSystem.AddFile(InstallInfoPath(root, product), content);
        }

        public static string AppIdOf(Product product)
        {
            return product == Product.NeoEE ? NeoEEAppId : EEAppId;
        }

        // --- Registry ----------------------------------------------------------------------------------------------

        /// <summary>
        /// The registry record of contract 1.1 in <paramref name="hive"/>/<paramref name="view"/>; values that are null
        /// are not written.
        /// </summary>
        public RegistryLocation AddRecord(RegistryHive hive, RegistryView view, Product product, string root,
            int? contractVersion = 1, string mode = "admin", string appId = null)
        {
            RegistryLocation key = In(hive, view, product.InstallRecordKey);
            Registry.SeedKey(key);
            if (contractVersion != null)
                Registry.Seed(key, ContractNames.ContractVersionName, RegistryValue.FromDWord(contractVersion.Value));
            if (root != null)
                Registry.Seed(key, ContractNames.InstallPathName, RegistryValue.FromString(root));
            if (mode != null)
                Registry.Seed(key, ContractNames.InstallModeName, RegistryValue.FromString(mode));
            Registry.Seed(key, ContractNames.AppIdName, RegistryValue.FromString(appId ?? AppIdOf(product)));
            Registry.Seed(key, ContractNames.GameVersionName, RegistryValue.FromString("2.0.0.5"));
            Registry.Seed(key, ContractNames.SetupVersionName, RegistryValue.FromString("2.0.0"));
            return key;
        }

        /// <summary>The uninstall key of Inno Setup (contract 1.3) with the publisher of <paramref name="product"/>.</summary>
        public RegistryLocation AddUninstallKey(RegistryHive hive, RegistryView view, Product product, string root,
            string appId = null, string components = "game,gameaoc", string tasks = "compatibility",
            string publisher = null, bool writeAppPath = true, string keyName = null, int? contractVersion = null)
        {
            RegistryLocation key = In(hive, view, ContractNames.UninstallKey + @"\" + (keyName ?? "{" + (appId ?? AppIdOf(product)) + "}_is1"));
            Registry.SeedKey(key);
            Registry.Seed(key, ContractNames.UninstallPublisherName, RegistryValue.FromString(publisher ?? product.Publisher));
            if (root != null)
            {
                if (writeAppPath)
                    Registry.Seed(key, ContractNames.UninstallAppPathName, RegistryValue.FromString(root));
                Registry.Seed(key, ContractNames.UninstallInstallLocationName, RegistryValue.FromString(root.TrimEnd('\\') + @"\"));
            }
            Registry.Seed(key, ContractNames.UninstallDisplayNameName,
                RegistryValue.FromString(product.AppName + " v1.7.2 - Setup v1.7.2"));
            Registry.Seed(key, ContractNames.UninstallDisplayVersionName, RegistryValue.FromString("1.7.2"));
            if (components != null)
                Registry.Seed(key, ContractNames.UninstallComponentsName, RegistryValue.FromString(components));
            if (tasks != null)
                Registry.Seed(key, ContractNames.UninstallTasksName, RegistryValue.FromString(tasks));
            if (contractVersion != null)
                Registry.Seed(key, ContractNames.UninstallContractVersionName, RegistryValue.FromDWord(contractVersion.Value));
            return key;
        }

        /// <summary>
        /// The "Installed From" values naming <paramref name="gameFolder"/> in <paramref name="settingsKey"/>, written as the
        /// setup writes them (contract 3.3): the first two characters, then the parent upper-cased, a backslash, the folder
        /// name and a backslash.
        /// </summary>
        public void SetInstalledFrom(RegistryLocation settingsKey, string gameFolder)
        {
            string parent = WinPath.GetParent(gameFolder);
            string name = WinPath.GetFileName(gameFolder);
            string parentWithoutDrive = parent.Substring(2).TrimEnd('\\');
            Registry.Seed(settingsKey, ContractNames.InstalledFromVolumeName, RegistryValue.FromString(gameFolder.Substring(0, 2)));
            Registry.Seed(settingsKey, ContractNames.InstalledFromDirectoryName,
                RegistryValue.FromString(parentWithoutDrive.ToUpperInvariant() + @"\" + name + @"\"));
        }

        /// <summary>The Empire Earth settings key of <paramref name="product"/> in a hive.</summary>
        public static RegistryLocation EmpireEarthKey(Product product, RegistryHive hive, RegistryView view)
        {
            return In(hive, view, product.GetGameSettingsKey(Game.EmpireEarth));
        }

        /// <summary>The AoC settings key of <paramref name="product"/> in a hive.</summary>
        public static RegistryLocation ArtOfConquestKey(Product product, RegistryHive hive, RegistryView view)
        {
            return In(hive, view, product.GetGameSettingsKey(Game.ArtOfConquest));
        }

        // --- Whole installations -----------------------------------------------------------------------------------

        /// <summary>
        /// A community setup since v2 in <paramref name="mode"/> (contract 1.1 to 1.3): files, <c>install.ini</c>, record and
        /// uninstall key (not in portable mode), and the HKCU "Installed From" values of the account that ran it.
        /// </summary>
        public void AddCommunityInstallation(string root, Product product, string mode = "admin", bool artOfConquest = true)
        {
            AddCommunityFiles(root, product, artOfConquest);
            AddInstallInfo(root, product, 1, mode, artOfConquest ? "game,gameaoc" : "game");
            if (mode != "portable")
            {
                RegistryHive hive = mode == "admin" ? RegistryHive.LocalMachine : RegistryHive.CurrentUser;
                RegistryView view = mode == "admin" ? RegistryView.Registry64 : RegistryView.Default;
                AddRecord(hive, view, product, root, 1, mode);
                AddUninstallKey(hive, view, product, root, components: artOfConquest ? "game,gameaoc" : "game",
                    contractVersion: 1);
            }
            SetInstalledFrom(EmpireEarthKey(product, RegistryHive.CurrentUser, RegistryView.Default),
                WinPath.Combine(root, Game.EmpireEarth.FolderName));
        }

        /// <summary>
        /// A community setup up to 1.7.2 (contract 1.5): files, the uninstall key (HKLM64 for admin, HKCU for user) and the
        /// HKCU "Installed From" values.
        /// </summary>
        public void AddLegacyInstallation(string root, Product product, bool admin = true, bool artOfConquest = true)
        {
            AddCommunityFiles(root, product, artOfConquest);
            AddUninstallKey(admin ? RegistryHive.LocalMachine : RegistryHive.CurrentUser,
                admin ? RegistryView.Registry64 : RegistryView.Default, product, root,
                components: artOfConquest ? "game,gameaoc" : "game");
            SetInstalledFrom(EmpireEarthKey(product, RegistryHive.CurrentUser, RegistryView.Default),
                WinPath.Combine(root, Game.EmpireEarth.FolderName));
        }

        /// <summary>
        /// A foreign installation (retail, GOG, a copy): <c>Empire Earth.exe</c> in <paramref name="eeFolder"/> and the
        /// "Installed From" values of the SSSI key (or of the Neo key with <paramref name="neoKey"/>) in a hive.
        /// </summary>
        public void AddForeignInstallation(string eeFolder, RegistryHive hive = RegistryHive.CurrentUser,
            RegistryView view = RegistryView.Default, bool neoKey = false, string aocFolder = null)
        {
            Product keyProduct = neoKey ? Product.NeoEE : Product.EE;
            AddEmpireEarth(eeFolder);
            SetInstalledFrom(EmpireEarthKey(keyProduct, hive, view), eeFolder);
            if (aocFolder != null)
            {
                AddArtOfConquest(aocFolder);
                SetInstalledFrom(ArtOfConquestKey(keyProduct, hive, view), aocFolder);
            }
        }

        /// <summary>The installation with the root <paramref name="root"/>, or null.</summary>
        public static Installation ByRoot(DiscoveryResult result, string root)
        {
            return result.Installations.SingleOrDefault(installation => WinPath.IsSamePath(installation.Root, root));
        }

        /// <summary>The log lines that mention <paramref name="text"/>.</summary>
        public string[] LogLinesAbout(string text)
        {
            return Logger.Messages.Where(message => message.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Empire_Earth_Launcher.Core.Backup;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.GameSettings;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Core.Play;
using Empire_Earth_Launcher.Tests.Fakes;
using Microsoft.Win32;

namespace Empire_Earth_Launcher.Tests.TestSupport
{
    /// <summary>
    /// A computer for the game settings tests: an <see cref="InstallationWorld"/> plus the mutexes, the system information,
    /// the launcher's write policy around the registry and the backup folder below the player's profile.
    /// </summary>
    internal sealed class GameSettingsWorld
    {
        public const string NeoRoot = @"C:\Program Files (x86)\Neo Empire Earth";
        public const string EERoot = @"C:\Program Files (x86)\Empire Earth";
        public const string BackupsFolder = @"C:\Users\Player\AppData\Local\Empire Earth Launcher\Backups";

        public GameSettingsWorld(FakeSystemInfo systemInfo = null)
        {
            World = new InstallationWorld();
            Mutexes = new FakeMutexProbe();
            SystemInfo = systemInfo ?? new FakeSystemInfo();
            Registry = new PolicyCheckedRegistry(World.Registry, LauncherWritePolicy.For(SystemInfo));
            Guard = new MutationGuard(Mutexes, World.Logger);
            Backups = new BackupLocations(BackupsFolder, World.FileSystem, World.Clock, World.Logger);
        }

        public InstallationWorld World { get; }

        public InMemoryRegistry RawRegistry
        {
            get { return World.Registry; }
        }

        public InMemoryFileSystem FileSystem
        {
            get { return World.FileSystem; }
        }

        public RecordingLogger Logger
        {
            get { return World.Logger; }
        }

        public FakeMutexProbe Mutexes { get; }

        public FakeSystemInfo SystemInfo { get; }

        /// <summary>The registry as the launcher uses it: every change passes <see cref="LauncherWritePolicy"/>.</summary>
        public IRegistry Registry { get; }

        public MutationGuard Guard { get; }

        public BackupLocations Backups { get; }

        public GameDefaultsService CreateDefaultsService()
        {
            return new GameDefaultsService(Registry, FileSystem, SystemInfo, Guard, Backups, Logger);
        }

        public DiscoveryResult Discover(string userChoice = null)
        {
            return World.Discover(userChoice);
        }

        /// <summary>
        /// A community installation "for all users" (contract 1.1 to 1.3) as another account sees it: files, install.ini,
        /// record and uninstall key in HKLM, but no HKCU values of this account (forum report section 8, test case 1).
        /// </summary>
        public void AddAdminInstallationOfAnotherAccount(string root, Product product, string components = "game,gameaoc",
            string tasks = "compatibility,compatibility_windows", int contractVersion = 1)
        {
            World.AddCommunityFiles(root, product, components.Contains("gameaoc"));
            World.AddInstallInfo(root, product, contractVersion, "admin", components, tasks);
            World.AddRecord(RegistryHive.LocalMachine, RegistryView.Registry64, product, root, contractVersion);
            World.AddUninstallKey(RegistryHive.LocalMachine, RegistryView.Registry64, product, root, components: components,
                tasks: tasks, contractVersion: contractVersion);
        }

        public static RegistryLocation Settings(Product product, Game game)
        {
            return RegistryLocation.CurrentUser(product.GetGameSettingsKey(game));
        }

        public static RegistryLocation GameOptions(Product product, Game game)
        {
            return Settings(product, game).Child(ContractNames.GameOptionsSubKeyName);
        }

        public static RegistryLocation Marker(Product product)
        {
            return RegistryLocation.CurrentUser(product.DefaultsMarkerKey);
        }

        public static RegistryLocation GpuPreferences
        {
            get { return RegistryLocation.CurrentUser(ContractNames.GpuPreferencesKey); }
        }

        public static RegistryLocation Layers
        {
            get { return RegistryLocation.CurrentUser(ContractNames.CompatibilityLayersKey); }
        }

        /// <summary>The value, or null if it is missing.</summary>
        public RegistryValue Get(RegistryLocation key, string valueName)
        {
            RegistryResult<RegistryValue> value = RawRegistry.GetValue(key, valueName);
            return value.IsOk ? value.Value : null;
        }

        /// <summary>
        /// Every value below the keys (recursively), as <c>key @"name" = value</c> lines, sorted: the state a backup must
        /// restore. Empty keys do not appear (an import never deletes a key).
        /// </summary>
        public IReadOnlyList<string> ValuesBelow(params RegistryLocation[] keys)
        {
            var lines = new List<string>();
            foreach (RegistryLocation key in keys)
            {
                RegistryResult<IReadOnlyList<RegFileKey>> tree = RegistryExport.ReadTree(RawRegistry, key);
                foreach (RegFileKey exported in tree.Value)
                    lines.AddRange(exported.Values.Select(value => exported.Key + " @\"" + value.Key + "\" = " + value.Value));
            }
            return lines.OrderBy(line => line, StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>The changes of the registry that were not refused, as the fake records them.</summary>
        public IReadOnlyList<string> Changes
        {
            get { return RawRegistry.Changes; }
        }
    }
}

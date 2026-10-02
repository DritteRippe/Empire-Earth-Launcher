namespace Empire_Earth_Launcher.Core.Contract
{
    /// <summary>
    /// Fixed names of the contract between the Empire Earth Community Setup and the launcher
    /// (<c>docs/CONTRACT.md</c>) that belong to neither a product nor a game. The per-product names are in
    /// <see cref="Product"/>, the per-game names in <see cref="Game"/>.
    /// </summary>
    /// <remarks>
    /// Registry key paths are relative to a hive (HKCU, HKLM64 or HKLM32, contract 0 "Registry views") and use
    /// <c>\</c> as separator. A change of a name here is a change of the contract and needs the same change in
    /// <c>docs/CONTRACT.md</c> of both repositories (contract 5); <c>ContractNamesTests</c> compares them.
    /// </remarks>
    public static class ContractNames
    {
        /// <summary>
        /// The contract version this launcher implements (contract 5): written by the setup as
        /// <c>ContractVersion</c> and stored in the defaults marker.
        /// </summary>
        public const int ContractVersion = 1;

        /// <summary>Root key of everything the community setup records (contract 1.1, 3.5).</summary>
        public const string CommunityKey = @"Software\Empire Earth Community";

        /// <summary>Parent of the registry records, one subkey per product id (contract 1.1). Read-only.</summary>
        public const string InstallRecordsKey = CommunityKey + @"\Installations";

        /// <summary>Parent of the defaults markers, one subkey per product id (contract 3.5).</summary>
        public const string DefaultsMarkersKey = CommunityKey + @"\GameDefaults";

        /// <summary>Uninstall keys of Windows; community setups add <c>{&lt;AppId&gt;}_is1</c> (contract 1.3). Read-only.</summary>
        public const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall";

        /// <summary>
        /// The NeoEE CD keys registered by <c>authtools.dll</c> during the setup (contract 3.8). Never changed or
        /// deleted, in any hive or view; only its existence may be checked.
        /// </summary>
        public const string CdKeysKey = @"Software\Sierra\CDKeys";

        /// <summary>Subkey of a game settings key with the default game options (contract 3.2).</summary>
        public const string GameOptionsSubKeyName = "Game Options";

        /// <summary>HKCU key of the GPU preference per program path (contract 3.4).</summary>
        public const string GpuPreferencesKey = @"Software\Microsoft\DirectX\UserGpuPreferences";

        /// <summary>Key of the compatibility layers per program path (contract 3.7).</summary>
        public const string CompatibilityLayersKey = @"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers";

        /// <summary>Install info file in the setup data folder (contract 1.2). Read-only.</summary>
        public const string InstallInfoFileName = "install.ini";

        /// <summary>Integrity manifest in the setup data folder (contract 2.1). Read-only.</summary>
        public const string ManifestFileName = "files.sha256";

        // Registry record (contract 1.1); the same names are the keys of the section [Install] of install.ini (1.2).

        /// <summary>Contract version of the setup run, REG_DWORD in the record (contract 1.1, 1.2, 5).</summary>
        public const string ContractVersionName = "ContractVersion";

        /// <summary>Install root, REG_SZ, only in the registry record (contract 1.1).</summary>
        public const string InstallPathName = "InstallPath";

        /// <summary><c>admin</c>, <c>user</c> or <c>portable</c> (contract 1.1, 1.2).</summary>
        public const string InstallModeName = "InstallMode";

        /// <summary>AppId without braces (contract 1.1, 1.2).</summary>
        public const string AppIdName = "AppId";

        /// <summary><c>MyAppVersion</c> of the setup (contract 1.1, 1.2).</summary>
        public const string GameVersionName = "GameVersion";

        /// <summary><c>MySetupVersion</c> of the setup (contract 1.1, 1.2).</summary>
        public const string SetupVersionName = "SetupVersion";

        /// <summary>Optional build identifier of the setup (contract 1.1, 1.2).</summary>
        public const string SetupBuildName = "SetupBuild";

        // Install info file (contract 1.2).

        /// <summary>Section of <c>install.ini</c> with the values of the setup run.</summary>
        public const string InstallInfoSectionName = "Install";

        /// <summary>Section of <c>install.ini</c> with the files that were gone after the installation.</summary>
        public const string MissingAfterInstallSectionName = "MissingAfterInstall";

        /// <summary>Product id in <c>install.ini</c>.</summary>
        public const string ProductName = "Product";

        /// <summary>Selected components in <c>install.ini</c>, comma separated.</summary>
        public const string ComponentsName = "Components";

        /// <summary>Selected tasks in <c>install.ini</c>, comma separated.</summary>
        public const string TasksName = "Tasks";

        /// <summary>Local time of the setup run in <c>install.ini</c>, informative only.</summary>
        public const string WrittenName = "Written";

        /// <summary>Install mode of an administrative installation (HKLM, contract 0).</summary>
        public const string AdminInstallMode = "admin";

        /// <summary>Install mode of a non-administrative installation (HKCU, contract 0).</summary>
        public const string UserInstallMode = "user";

        /// <summary>Install mode of a portable installation (no registry record, contract 0).</summary>
        public const string PortableInstallMode = "portable";

        /// <summary>Component of The Art of Conquest (contract 1.2).</summary>
        public const string ArtOfConquestComponent = "gameaoc";

        /// <summary>
        /// Component of the DirectX wrappers; the names of its subcomponents start with it and a backslash
        /// (contract 1.2).
        /// </summary>
        public const string DirectXWrapperComponent = @"additional\directx_wrapper";

        /// <summary>Prefix of the component of the game language, e.g. <c>language\pt_BR</c> (contract 1.2).</summary>
        public const string LanguageComponentPrefix = @"language\";

        // Uninstall key of Inno Setup (contract 1.3). Read-only.

        /// <summary>Install root without trailing backslash.</summary>
        public const string UninstallAppPathName = "Inno Setup: App Path";

        /// <summary>Install root with trailing backslash; used when the App Path is missing (contract 1.4).</summary>
        public const string UninstallInstallLocationName = "InstallLocation";

        /// <summary>Publisher; identifies community setups and their product (contract 0, 1.4).</summary>
        public const string UninstallPublisherName = "Publisher";

        /// <summary><c>&lt;AppName&gt; v&lt;game version&gt; - Setup v&lt;setup version&gt;</c>.</summary>
        public const string UninstallDisplayNameName = "DisplayName";

        /// <summary>The game version.</summary>
        public const string UninstallDisplayVersionName = "DisplayVersion";

        /// <summary>Selected components, as <see cref="ComponentsName"/> in <c>install.ini</c>.</summary>
        public const string UninstallComponentsName = "Inno Setup: Selected Components";

        /// <summary>Selected tasks, as <see cref="TasksName"/> in <c>install.ini</c>.</summary>
        public const string UninstallTasksName = "Inno Setup: Selected Tasks";

        /// <summary>REG_DWORD written by setups since v2 after the manifest (contract 1.3, 2.5).</summary>
        public const string UninstallContractVersionName = "Empire Earth Community: ContractVersion";

        // "Installed From" values of the game settings keys (contract 3.2, 3.3; source 4 of contract 1.4).

        /// <summary>Drive of the game folder, e.g. <c>C:</c>.</summary>
        public const string InstalledFromVolumeName = "Installed From Volume";

        /// <summary>Game folder without its drive, with a backslash at both ends.</summary>
        public const string InstalledFromDirectoryName = "Installed From Directory";
    }
}

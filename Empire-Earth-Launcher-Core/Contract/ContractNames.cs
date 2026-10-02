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
    }
}

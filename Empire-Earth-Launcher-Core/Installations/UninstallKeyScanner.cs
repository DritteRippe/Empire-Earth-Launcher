using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Logging;
using Empire_Earth_Launcher.Core.Platform;
using Microsoft.Win32;

namespace Empire_Earth_Launcher.Core.Installations
{
    /// <summary>The uninstall key of a community setup (contract 1.3), source 3 of the discovery.</summary>
    public sealed class UninstallEntry
    {
        internal UninstallEntry(Product product, RegistryLocation key, string appId, string root, string gameVersion,
            string setupVersion, SetupNameList components, SetupNameList tasks, int? contractVersion)
        {
            Product = product;
            Key = key;
            AppId = appId;
            Root = root;
            GameVersion = gameVersion;
            SetupVersion = setupVersion;
            Components = components;
            Tasks = tasks;
            ContractVersion = contractVersion;
        }

        /// <summary>The product its <c>Publisher</c> names.</summary>
        public Product Product { get; }

        /// <summary>The key <c>...\Uninstall\{&lt;AppId&gt;}_is1</c> with hive and view.</summary>
        public RegistryLocation Key { get; }

        /// <summary>The GUID of the key name, without braces, as written.</summary>
        public string AppId { get; }

        /// <summary><c>Inno Setup: App Path</c>, else <c>InstallLocation</c>, in the normal form of <see cref="WinPath"/>.</summary>
        public string Root { get; }

        /// <summary>
        /// <c>admin</c> for a key in HKLM, <c>user</c> for one in HKCU (contract 1.3: portable setups have no key).
        /// </summary>
        public InstallMode InstallMode
        {
            get { return RegistryReads.IsCurrentUser(Key) ? InstallMode.User : InstallMode.Admin; }
        }

        /// <summary><c>DisplayVersion</c>; null if missing.</summary>
        public string GameVersion { get; }

        /// <summary>The setup version at the end of <c>DisplayName</c> (<c>... - Setup v&lt;version&gt;</c>); null if absent.</summary>
        public string SetupVersion { get; }

        /// <summary><c>Inno Setup: Selected Components</c>; empty if missing.</summary>
        public SetupNameList Components { get; }

        /// <summary><c>Inno Setup: Selected Tasks</c>; empty if missing.</summary>
        public SetupNameList Tasks { get; }

        /// <summary>
        /// <c>Empire Earth Community: ContractVersion</c> (setups since v2, contract 1.3); null if missing, as after a later
        /// run of a setup up to 1.7.2 (contract 2.5).
        /// </summary>
        public int? ContractVersion { get; }

        public override string ToString()
        {
            return Key.ToString();
        }
    }

    /// <summary>
    /// Finds the uninstall keys of community setups (contract 1.4, source 3): the subkeys of
    /// <c>Software\Microsoft\Windows\CurrentVersion\Uninstall</c> in HKCU, HKLM64 and HKLM32 whose name has the form
    /// <c>{&lt;GUID&gt;}_is1</c> and whose <c>Publisher</c> is exactly one of the two publishers of the contract, which
    /// also gives the product. NeoEE before EE, then the order of the hives. Read-only.
    /// </summary>
    /// <remarks>
    /// Keys of other programs are skipped without a log line. A community key without a usable root, a key whose
    /// <c>Publisher</c> cannot be read, and an uninstall folder that cannot be listed each give exactly one log line.
    /// The uninstall key of the suite has the <c>Publisher</c> of EE but is no installation (contract 0 "Suite and
    /// launcher", revision 5): a key with the value <see cref="ContractNames.UninstallSuiteMarkerName"/> is skipped, and so
    /// is, for a suite built before revision 5, a key in HKLM whose root is the <c>InstallPath</c> of the suite record and
    /// whose AppId the record does not embed. Each skipped suite key gives one Info line.
    /// On 32-bit Windows both HKLM views are one; a key with the name and root of an earlier one is left out.
    /// </remarks>
    public sealed class UninstallKeyScanner
    {
        /// <summary>The name of an uninstall key of Inno Setup: <c>{&lt;GUID&gt;}_is1</c>.</summary>
        private static readonly Regex InnoSetupKeyName = new Regex(
            @"^\{(?<guid>[0-9A-F]{8}-[0-9A-F]{4}-[0-9A-F]{4}-[0-9A-F]{4}-[0-9A-F]{12})\}_is1$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>The end of <c>DisplayName</c>: <c> - Setup v&lt;setup version&gt;</c>.</summary>
        private static readonly Regex SetupVersionInDisplayName = new Regex(@" - Setup v(?<version>\S+)\s*$",
            RegexOptions.CultureInvariant);

        private readonly IRegistry registry;
        private readonly ILogger logger;
        private readonly SuiteRecord suiteRecord;

        /// <param name="registry">The registry to read.</param>
        /// <param name="logger">The log.</param>
        /// <param name="suiteRecord">The suite record (contract 1.6), or null; it is only used to recognise the uninstall key
        /// of a suite built before contract revision 5, which has no marker.</param>
        public UninstallKeyScanner(IRegistry registry, ILogger logger, SuiteRecord suiteRecord = null)
        {
            this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.suiteRecord = suiteRecord;
        }

        /// <summary>The folders of uninstall keys in the order of the contract: HKCU, HKLM64, HKLM32.</summary>
        public static IEnumerable<RegistryLocation> UninstallFolders()
        {
            yield return RegistryLocation.CurrentUser(ContractNames.UninstallKey);
            foreach (RegistryView view in RegistryReads.LocalMachine64Then32)
                yield return new RegistryLocation(RegistryHive.LocalMachine, view, ContractNames.UninstallKey);
        }

        /// <summary>True if <paramref name="keyName"/> has the form <c>{&lt;GUID&gt;}_is1</c>.</summary>
        public static bool IsInnoSetupKeyName(string keyName, out string appId)
        {
            Match match = InnoSetupKeyName.Match(keyName ?? string.Empty);
            appId = match.Success ? match.Groups["guid"].Value : null;
            return match.Success;
        }

        /// <summary>The uninstall keys of community setups, NeoEE before EE, then in the order of the hives.</summary>
        public IReadOnlyList<UninstallEntry> Scan(CancellationToken cancellationToken = default)
        {
            var entries = new List<UninstallEntry>();
            foreach (RegistryLocation folder in UninstallFolders())
            {
                RegistryResult<IReadOnlyList<string>> names = registry.GetSubKeyNames(folder);
                if (names.Status == RegistryStatus.Missing)
                    continue;
                if (!names.IsOk)
                {
                    logger.Warning("Discovery: the uninstall keys in " + folder + " cannot be listed and are ignored: " + names + ".");
                    continue;
                }

                foreach (string name in names.Value)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!IsInnoSetupKeyName(name, out string appId))
                        continue;
                    UninstallEntry entry = ReadEntry(folder.Child(name), appId);
                    if (entry == null)
                        continue;
                    if (entries.Any(earlier => string.Equals(earlier.Key.Path, entry.Key.Path, StringComparison.OrdinalIgnoreCase) &&
                                               earlier.Key.Hive == entry.Key.Hive &&
                                               WinPath.IsSamePath(earlier.Root, entry.Root)))
                        continue; // the same key through the other HKLM view (32-bit Windows)
                    entries.Add(entry);
                }
            }

            // OrderBy is stable: within a product the order of the hives stays.
            return entries.OrderBy(entry => Product.All.ToList().IndexOf(entry.Product)).ToList();
        }

        private UninstallEntry ReadEntry(RegistryLocation key, string appId)
        {
            RegistryResult<RegistryValue> publisher = registry.GetValue(key, ContractNames.UninstallPublisherName);
            if (publisher.Status == RegistryStatus.Missing)
                return null;
            if (!publisher.IsOk)
            {
                logger.Warning("Discovery: the uninstall key " + key + " cannot be read and is ignored: " + publisher + ".");
                return null;
            }

            Product product = publisher.Value.IsString
                ? Product.All.FirstOrDefault(candidate =>
                    string.Equals(candidate.Publisher, publisher.Value.StringValue, StringComparison.Ordinal))
                : null;
            if (product == null)
                return null; // another program

            if (IsMarkedSuiteKey(key))
                return null;

            string appPath = RegistryReads.GetStringOrNull(registry, key, ContractNames.UninstallAppPathName);
            string installLocation = RegistryReads.GetStringOrNull(registry, key, ContractNames.UninstallInstallLocationName);
            string written = !string.IsNullOrWhiteSpace(appPath) ? appPath : installLocation;
            string root = RegistryReads.ToRoot(written);
            if (root == null)
            {
                logger.Warning("Discovery: the uninstall key " + key + " of " + product.Id + " is ignored, it names no full " +
                               "install root (" + ContractNames.UninstallAppPathName + ": " + Quote(appPath) + ", " +
                               ContractNames.UninstallInstallLocationName + ": " + Quote(installLocation) + ").");
                return null;
            }

            if (IsSuiteRootWithoutMarker(key, appId, root))
                return null;

            string displayName = RegistryReads.GetStringOrNull(registry, key, ContractNames.UninstallDisplayNameName);
            Match setupVersion = SetupVersionInDisplayName.Match(displayName ?? string.Empty);
            return new UninstallEntry(product, key, appId, root,
                RegistryReads.GetStringOrNull(registry, key, ContractNames.UninstallDisplayVersionName),
                setupVersion.Success ? setupVersion.Groups["version"].Value : null,
                SetupNameList.Parse(RegistryReads.GetStringOrNull(registry, key, ContractNames.UninstallComponentsName)),
                SetupNameList.Parse(RegistryReads.GetStringOrNull(registry, key, ContractNames.UninstallTasksName)),
                RegistryReads.GetDWordOrNull(registry, key, ContractNames.UninstallContractVersionName));
        }

        /// <summary>
        /// True if the key has the marker of the suite (any type and data): it is no installation. A marker that cannot be
        /// read drops the key with one warning, as an unreadable <c>Publisher</c> does.
        /// </summary>
        private bool IsMarkedSuiteKey(RegistryLocation key)
        {
            RegistryResult<RegistryValue> marker = registry.GetValue(key, ContractNames.UninstallSuiteMarkerName);
            if (marker.Status == RegistryStatus.Missing)
                return false;
            if (marker.IsOk)
            {
                logger.Info("Discovery: the uninstall key " + key + " is the one of the suite \"" + ContractNames.SuiteAppName +
                            "\" (" + ContractNames.UninstallSuiteMarkerName + "); it is no installation and is ignored.");
                return true;
            }

            logger.Warning("Discovery: the uninstall key " + key + " cannot be read and is ignored: " + marker + ".");
            return true;
        }

        /// <summary>
        /// True for the key of a suite built before revision 5 (no marker): in HKLM, with the suite root of the record as
        /// root and an AppId that is neither of the two the record embeds (contract 1.4, source 3).
        /// </summary>
        private bool IsSuiteRootWithoutMarker(RegistryLocation key, string appId, string root)
        {
            if (suiteRecord?.InstallPath == null || RegistryReads.IsCurrentUser(key))
                return false;
            if (!WinPath.IsSamePath(root, suiteRecord.InstallPath))
                return false;
            if (string.Equals(appId, suiteRecord.EeAppId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(appId, suiteRecord.NeoEeAppId, StringComparison.OrdinalIgnoreCase))
                return false;
            logger.Info("Discovery: the uninstall key " + key + " names the suite root " + root + " of the suite record " +
                        suiteRecord.Key + " and no AppId the record embeds; it is the key of the suite \"" +
                        ContractNames.SuiteAppName + "\" without " + ContractNames.UninstallSuiteMarkerName + " and is ignored.");
            return true;
        }

        private static string Quote(string value)
        {
            return value == null ? "missing" : "\"" + value + "\"";
        }
    }
}

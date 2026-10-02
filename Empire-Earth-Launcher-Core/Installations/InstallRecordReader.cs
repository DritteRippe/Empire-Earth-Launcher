using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Logging;
using Empire_Earth_Launcher.Core.Platform;
using Microsoft.Win32;

namespace Empire_Earth_Launcher.Core.Installations
{
    /// <summary>A registry record of a setup since v2 (contract 1.1), source 2 of the discovery.</summary>
    public sealed class InstallRecord
    {
        internal InstallRecord(Product product, RegistryLocation key, string root, int contractVersion,
            string installModeText, string appId, string gameVersion, string setupVersion, string setupBuild)
        {
            Product = product;
            Key = key;
            Root = root;
            ContractVersion = contractVersion;
            InstallModeText = installModeText;
            InstallMode = InstallModes.Parse(installModeText);
            AppId = appId;
            GameVersion = gameVersion;
            SetupVersion = setupVersion;
            SetupBuild = setupBuild;
        }

        /// <summary>The product of the record (its key name).</summary>
        public Product Product { get; }

        /// <summary>The key of the record, with hive and view.</summary>
        public RegistryLocation Key { get; }

        /// <summary><c>InstallPath</c> in the normal form of <see cref="WinPath"/>.</summary>
        public string Root { get; }

        /// <summary><c>ContractVersion</c>; 0 if missing or not a REG_DWORD.</summary>
        public int ContractVersion { get; }

        /// <summary><c>InstallMode</c> as written; null if missing.</summary>
        public string InstallModeText { get; }

        /// <summary><see cref="InstallModeText"/> as a mode.</summary>
        public InstallMode InstallMode { get; }

        /// <summary><c>AppId</c>; null if missing.</summary>
        public string AppId { get; }

        /// <summary><c>GameVersion</c>; null if missing.</summary>
        public string GameVersion { get; }

        /// <summary><c>SetupVersion</c>; null if missing.</summary>
        public string SetupVersion { get; }

        /// <summary><c>SetupBuild</c>; null if missing.</summary>
        public string SetupBuild { get; }

        public override string ToString()
        {
            return Key.ToString();
        }
    }

    /// <summary>
    /// Reads the registry records <c>Software\Empire Earth Community\Installations\&lt;Product&gt;</c> (contract 1.1):
    /// product NeoEE before EE, per product HKCU, then HKLM64, then HKLM32 (contract 1.4, source 2). Read-only.
    /// </summary>
    /// <remarks>
    /// A record that cannot be used (no or an invalid <c>InstallPath</c>, access denied) is left out with exactly one
    /// log line; a missing key is no candidate and is not logged. On 32-bit Windows both HKLM views are one, so the
    /// same record would be read twice: a record with the product and root of an earlier one is left out.
    /// </remarks>
    public sealed class InstallRecordReader
    {
        private readonly IRegistry registry;
        private readonly ILogger logger;

        public InstallRecordReader(IRegistry registry, ILogger logger)
        {
            this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>The keys of the records of <paramref name="product"/>: HKCU, HKLM64, HKLM32.</summary>
        public static IEnumerable<RegistryLocation> RecordKeys(Product product)
        {
            if (product == null)
                throw new ArgumentNullException(nameof(product));
            yield return RegistryLocation.CurrentUser(product.InstallRecordKey);
            foreach (RegistryView view in RegistryReads.LocalMachine64Then32)
                yield return new RegistryLocation(RegistryHive.LocalMachine, view, product.InstallRecordKey);
        }

        /// <summary>The usable records, in the order of the contract.</summary>
        public IReadOnlyList<InstallRecord> Read(CancellationToken cancellationToken = default)
        {
            var records = new List<InstallRecord>();
            foreach (Product product in Product.All)
            {
                foreach (RegistryLocation key in RecordKeys(product))
                    Add(records, product, key, cancellationToken);
            }
            return records;
        }

        private void Add(List<InstallRecord> records, Product product, RegistryLocation key, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            InstallRecord record = ReadRecord(product, key);
            if (record == null)
                return;
            if (records.Any(earlier => earlier.Product == product && WinPath.IsSamePath(earlier.Root, record.Root)))
                return; // the same record through the other HKLM view (32-bit Windows)
            records.Add(record);
        }

        private InstallRecord ReadRecord(Product product, RegistryLocation key)
        {
            RegistryResult probe = registry.ProbeKey(key);
            if (probe.Status == RegistryStatus.Missing)
                return null;
            if (!probe.IsOk)
            {
                logger.Warning("Discovery: the install record " + key + " cannot be read and is ignored: " + probe + ".");
                return null;
            }

            RegistryResult<string> installPath = RegistryReads.GetString(registry, key, ContractNames.InstallPathName);
            if (!installPath.IsOk)
            {
                logger.Warning("Discovery: the install record " + key + " is ignored, its " + ContractNames.InstallPathName +
                               " cannot be read: " + installPath + ".");
                return null;
            }

            string root = RegistryReads.ToRoot(installPath.Value);
            if (root == null)
            {
                logger.Warning("Discovery: the install record " + key + " is ignored, its " + ContractNames.InstallPathName +
                               " \"" + installPath.Value + "\" is not a full path.");
                return null;
            }

            return new InstallRecord(product, key, root,
                RegistryReads.GetDWordOrNull(registry, key, ContractNames.ContractVersionName) ?? 0,
                RegistryReads.GetStringOrNull(registry, key, ContractNames.InstallModeName),
                RegistryReads.GetStringOrNull(registry, key, ContractNames.AppIdName),
                RegistryReads.GetStringOrNull(registry, key, ContractNames.GameVersionName),
                RegistryReads.GetStringOrNull(registry, key, ContractNames.SetupVersionName),
                RegistryReads.GetStringOrNull(registry, key, ContractNames.SetupBuildName));
        }
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Logging;
using Empire_Earth_Launcher.Core.Platform;
using Microsoft.Win32;

namespace Empire_Earth_Launcher.Core.Installations
{
    /// <summary>
    /// The record of the suite "Empire Earth Community" (contract 1.6, since revision 4): which products its setup runs
    /// installed and from which folder it was started. Only used for the repair advice (contract 4.4); it is no source of
    /// the discovery, and every value may be missing.
    /// </summary>
    public sealed class SuiteRecord
    {
        internal SuiteRecord(RegistryLocation key, int contractVersion, string suiteVersion, string installPath,
            IEnumerable<Product> products, string sourceDir, string eeAppId = null, string neoEeAppId = null)
        {
            Key = key;
            ContractVersion = contractVersion;
            SuiteVersion = suiteVersion;
            InstallPath = installPath;
            Products = new ReadOnlyCollection<Product>(products.ToList());
            SourceDir = sourceDir;
            EeAppId = eeAppId;
            NeoEeAppId = neoEeAppId;
        }

        /// <summary>The key of the record: HKLM, 64-bit view.</summary>
        public RegistryLocation Key { get; }

        /// <summary><c>ContractVersion</c>; 0 if missing or not a REG_DWORD.</summary>
        public int ContractVersion { get; }

        /// <summary><c>SuiteVersion</c>; null if missing.</summary>
        public string SuiteVersion { get; }

        /// <summary><c>InstallPath</c>, the suite root in the normal form of <see cref="WinPath"/>; null if missing or no full path.</summary>
        public string InstallPath { get; }

        /// <summary>The products of <c>Products</c> (NeoEE before EE); a name that is no product is left out.</summary>
        public IReadOnlyList<Product> Products { get; }

        /// <summary>
        /// <c>SourceDir</c> in the normal form of <see cref="WinPath"/>: the folder with <c>Empire Earth Community Setup.exe</c>
        /// the suite ran from last; null if missing or no full path. The folder may be gone.
        /// </summary>
        public string SourceDir { get; }

        /// <summary><c>EEAppId</c>, without braces; null if missing.</summary>
        public string EeAppId { get; }

        /// <summary><c>NeoEEAppId</c>, without braces; null if missing.</summary>
        public string NeoEeAppId { get; }

        /// <summary>The AppId the suite embeds for <paramref name="product"/>; null if the record has none.</summary>
        public string AppIdFor(Product product)
        {
            if (product == null)
                throw new ArgumentNullException(nameof(product));
            return product == Product.NeoEE ? NeoEeAppId : EeAppId;
        }

        /// <summary>True if the setup run of the suite succeeded for <paramref name="product"/> (contract 1.6, <c>Products</c>).</summary>
        public bool Lists(Product product)
        {
            if (product == null)
                throw new ArgumentNullException(nameof(product));
            return Products.Contains(product);
        }

        /// <summary>
        /// The folder to run the suite from again for <paramref name="product"/> (contract 4.4): <see cref="SourceDir"/> if
        /// the record lists the product and the folder exists; null otherwise, then the advice is the download of the
        /// product setup.
        /// </summary>
        public string RepairFolderFor(Product product, IFileSystem fileSystem)
        {
            if (fileSystem == null)
                throw new ArgumentNullException(nameof(fileSystem));
            if (!Lists(product) || SourceDir == null)
                return null;
            // A network share may not answer for seconds, and the check runs on the UI thread: it gives no suite step.
            if (SourceDir.StartsWith(@"\\", StringComparison.Ordinal))
                return null;
            return fileSystem.DirectoryExists(SourceDir) ? SourceDir : null;
        }

        public override string ToString()
        {
            return Key + " (" + (Products.Count == 0 ? "no product" : string.Join(", ", Products.Select(p => p.Id))) + ")";
        }
    }

    /// <summary>
    /// Reads the suite record <c>Software\Empire Earth Community\Suite</c> in HKLM, 64-bit view (contract 1.6, "Suite
    /// record"). Read-only, with an explicit view; the launcher works without it.
    /// </summary>
    /// <remarks>
    /// A missing key is the normal case (no suite was run, or the launcher was not installed by it) and is not logged. A key
    /// that cannot be read is logged once and means no record. A value that is missing or has another type is left out, so
    /// the rest of the record still counts.
    /// </remarks>
    public sealed class SuiteRecordReader
    {
        private static readonly char[] ProductSeparators = { ',' };

        private readonly IRegistry registry;
        private readonly ILogger logger;

        public SuiteRecordReader(IRegistry registry, ILogger logger)
        {
            this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>The key of the record: HKLM, 64-bit view (the suite runs in 64-bit install mode, contract 1.6).</summary>
        public static RegistryLocation RecordKey
        {
            get { return new RegistryLocation(RegistryHive.LocalMachine, RegistryView.Registry64, ContractNames.SuiteRecordKey); }
        }

        /// <summary>The record; null if there is none (no key, or it cannot be read).</summary>
        public SuiteRecord Read()
        {
            RegistryLocation key = RecordKey;
            RegistryResult probe = registry.ProbeKey(key);
            if (probe.Status == RegistryStatus.Missing)
                return null;
            if (!probe.IsOk)
            {
                logger.Warning("The suite record " + key + " cannot be read and is ignored: " + probe + ".");
                return null;
            }

            return new SuiteRecord(key,
                RegistryReads.GetDWordOrNull(registry, key, ContractNames.ContractVersionName) ?? 0,
                RegistryReads.GetStringOrNull(registry, key, ContractNames.SuiteVersionName),
                RegistryReads.ToRoot(RegistryReads.GetStringOrNull(registry, key, ContractNames.InstallPathName)),
                ParseProducts(RegistryReads.GetStringOrNull(registry, key, ContractNames.SuiteProductsName)),
                RegistryReads.ToRoot(RegistryReads.GetStringOrNull(registry, key, ContractNames.SuiteSourceDirName)),
                CleanAppId(RegistryReads.GetStringOrNull(registry, key, ContractNames.SuiteEeAppIdName)),
                CleanAppId(RegistryReads.GetStringOrNull(registry, key, ContractNames.SuiteNeoEeAppIdName)));
        }

        /// <summary>An AppId without braces and blanks; null if the value is missing or empty.</summary>
        private static string CleanAppId(string value)
        {
            string cleaned = value?.Trim().Trim('{', '}').Trim();
            return string.IsNullOrEmpty(cleaned) ? null : cleaned;
        }

        /// <summary>
        /// The products of a <c>Products</c> value (<c>EE,NeoEE</c>): comma separated, blanks around a name ignored, the
        /// case of a name ignored, a name that is no product and a repeated name left out; NeoEE before EE as everywhere.
        /// </summary>
        public static IReadOnlyList<Product> ParseProducts(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return new Product[0];
            var named = new HashSet<Product>();
            foreach (string part in value.Split(ProductSeparators))
            {
                Product product = Product.FromId(part.Trim());
                if (product != null)
                    named.Add(product);
            }
            return Product.All.Where(named.Contains).ToList();
        }
    }
}

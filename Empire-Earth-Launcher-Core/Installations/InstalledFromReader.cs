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
    /// <summary>
    /// The "Installed From" values of an Empire Earth settings key (contract 3.3), source 4 of the discovery. They name
    /// the EE folder; the install root is its parent (contract 1.4).
    /// </summary>
    public sealed class InstalledFromEntry
    {
        internal InstalledFromEntry(Product keyProduct, RegistryLocation key, string eeFolder, string aocFolder,
            RegistryLocation aocKey)
        {
            KeyProduct = keyProduct;
            Key = key;
            EeFolder = eeFolder;
            Root = WinPath.GetParent(eeFolder);
            AocFolder = aocFolder;
            AocKey = aocKey;
        }

        /// <summary>
        /// The product whose settings key holds the values: NeoEE for <c>Software\Neo\Empire Earth</c>, EE for
        /// <c>Software\SSSI\Empire Earth</c>, which retail, GOG and older installations also use. It is not the product
        /// of the installation: that comes from the installation itself (contract 1.4, "Kind").
        /// </summary>
        public Product KeyProduct { get; }

        /// <summary>The settings key of Empire Earth with hive and view.</summary>
        public RegistryLocation Key { get; }

        /// <summary>The EE folder the values name (normal form of <see cref="WinPath"/>).</summary>
        public string EeFolder { get; }

        /// <summary>The parent of <see cref="EeFolder"/>: the install root.</summary>
        public string Root { get; }

        /// <summary>
        /// The folder the "Installed From" values of the AoC settings key of the same product name, in the same hive and
        /// view (contract 1.4, "AoC folder" of foreign installations); null if they are missing or invalid. Whether it
        /// holds <c>EE-AOC.exe</c> is checked by the discovery.
        /// </summary>
        public string AocFolder { get; }

        /// <summary>The AoC settings key that named <see cref="AocFolder"/>; null without one.</summary>
        public RegistryLocation AocKey { get; }

        public override string ToString()
        {
            return Key.ToString();
        }
    }

    /// <summary>
    /// Reads the "Installed From" values (contract 1.4, source 4). The order is key before hive:
    /// <c>Software\Neo\Empire Earth</c> in HKCU, HKLM32, HKLM64, then <c>Software\SSSI\Empire Earth</c> in the same
    /// order, like the product order of sources 2 and 3 (ADR 0015; contract 1.4 says so since revision 3). The old
    /// locator let the hive win, so an old SSSI value in HKCU beat a Neo value in HKLM (forum report section 8, test
    /// case 8). Read-only.
    /// </summary>
    /// <remarks>
    /// A settings key without the values, with only one of them, or with values that give no folder below a drive is
    /// left out with exactly one log line; a missing key is not logged. An EE folder read before through another view
    /// of the same key (32-bit Windows has one HKLM) is left out.
    /// </remarks>
    public sealed class InstalledFromReader
    {
        /// <summary>The hives and views of source 4, in their order: HKCU, HKLM32, HKLM64.</summary>
        private static readonly Tuple<RegistryHive, RegistryView>[] Hives =
        {
            Tuple.Create(RegistryHive.CurrentUser, RegistryView.Default),
            Tuple.Create(RegistryHive.LocalMachine, RegistryView.Registry32),
            Tuple.Create(RegistryHive.LocalMachine, RegistryView.Registry64)
        };

        private readonly IRegistry registry;
        private readonly ILogger logger;

        public InstalledFromReader(IRegistry registry, ILogger logger)
        {
            this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>The Empire Earth settings keys in the order of source 4 (key before hive).</summary>
        public static IEnumerable<RegistryLocation> SettingsKeys()
        {
            return Product.All.SelectMany(product => Hives.Select(hive =>
                new RegistryLocation(hive.Item1, hive.Item2, product.GetGameSettingsKey(Game.EmpireEarth))));
        }

        /// <summary>
        /// Joins the two values into a full path, e.g. <c>C:</c> and <c>\GAMES\Empire Earth\</c> into
        /// <c>C:\GAMES\Empire Earth</c>, with the rules of <see cref="WinPath"/> (the same on every platform).
        /// <see cref="System.IO.Path.Combine(string, string)"/> cannot be used: it drops the drive of a rooted folder.
        /// </summary>
        /// <returns>null if a value is missing or white space, or the result is not a fully qualified path.</returns>
        public static string CombineInstallLocation(string volume, string directory)
        {
            if (string.IsNullOrWhiteSpace(volume) || string.IsNullOrWhiteSpace(directory))
                return null;
            string combined = volume.Trim().TrimEnd('\\', '/') + WinPath.Separator + directory.Trim().TrimStart('\\', '/');
            return WinPath.IsFullyQualified(combined) ? WinPath.Normalize(combined) : null;
        }

        /// <summary>The usable values, key before hive.</summary>
        public IReadOnlyList<InstalledFromEntry> Read(CancellationToken cancellationToken = default)
        {
            var entries = new List<InstalledFromEntry>();
            foreach (Product product in Product.All)
            {
                foreach (Tuple<RegistryHive, RegistryView> hive in Hives)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var key = new RegistryLocation(hive.Item1, hive.Item2, product.GetGameSettingsKey(Game.EmpireEarth));
                    InstalledFromEntry entry = ReadEntry(product, key);
                    if (entry == null)
                        continue;
                    if (entries.Any(earlier => earlier.KeyProduct == product && WinPath.IsSamePath(earlier.EeFolder, entry.EeFolder)))
                        continue; // the same values through the other HKLM view (32-bit Windows), or HKCU and HKLM agree
                    entries.Add(entry);
                }
            }
            return entries;
        }

        private InstalledFromEntry ReadEntry(Product product, RegistryLocation key)
        {
            RegistryResult probe = registry.ProbeKey(key);
            if (probe.Status == RegistryStatus.Missing)
                return null;
            if (!probe.IsOk)
            {
                logger.Warning("Discovery: the game settings key " + key + " cannot be read and is ignored: " + probe + ".");
                return null;
            }

            RegistryResult<string> volume = RegistryReads.GetString(registry, key, ContractNames.InstalledFromVolumeName);
            RegistryResult<string> directory = RegistryReads.GetString(registry, key, ContractNames.InstalledFromDirectoryName);
            if (volume.Status == RegistryStatus.Missing && directory.Status == RegistryStatus.Missing)
            {
                logger.Info("Discovery: the game settings key " + key + " has no \"Installed From\" values and names no folder.");
                return null;
            }
            if (!volume.IsOk || !directory.IsOk)
            {
                logger.Warning("Discovery: the \"Installed From\" values of " + key + " are ignored, one cannot be read (" +
                               ContractNames.InstalledFromVolumeName + ": " + volume + ", " +
                               ContractNames.InstalledFromDirectoryName + ": " + directory + ").");
                return null;
            }

            string eeFolder = CombineInstallLocation(volume.Value, directory.Value);
            if (eeFolder == null || WinPath.GetParent(eeFolder) == null)
            {
                logger.Warning("Discovery: the \"Installed From\" values of " + key + " (\"" + volume.Value + "\", \"" +
                               directory.Value + "\") name no game folder below a drive and are ignored.");
                return null;
            }

            var aocKey = new RegistryLocation(key.Hive, key.View, product.GetGameSettingsKey(Game.ArtOfConquest));
            string aocFolder = CombineInstallLocation(
                RegistryReads.GetStringOrNull(registry, aocKey, ContractNames.InstalledFromVolumeName),
                RegistryReads.GetStringOrNull(registry, aocKey, ContractNames.InstalledFromDirectoryName));
            if (aocFolder != null && WinPath.GetParent(aocFolder) == null)
                aocFolder = null;
            return new InstalledFromEntry(product, key, eeFolder, aocFolder, aocFolder == null ? null : aocKey);
        }
    }
}

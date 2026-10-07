using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Empire_Earth_Launcher.Core.Contract
{
    /// <summary>
    /// A product of the community setup, EE or NeoEE, with its fixed names (contract 0, "Products"). There are
    /// exactly two instances.
    /// </summary>
    public sealed class Product
    {
        /// <summary>NeoEE: Empire Earth with the NeoEE online lobby.</summary>
        public static readonly Product NeoEE = new Product("NeoEE", "NeoEE", "Empire Earth Community & NeoEE",
            "Neo Empire Earth", @"Software\Neo\Empire Earth", @"Software\Neo\Art of Conquest", "neoee.dll");

        /// <summary>EE: Empire Earth without NeoEE.</summary>
        public static readonly Product EE = new Product("EE", "Empire Earth", "Empire Earth Community",
            "Empire Earth", @"Software\SSSI\Empire Earth", @"Software\Mad Doc Software\EE-AOC", null);

        /// <summary>Both products in the order of preference of the discovery: NeoEE before EE (contract 1.4).</summary>
        public static readonly IReadOnlyList<Product> All = new ReadOnlyCollection<Product>(new[] { NeoEE, EE });

        private readonly string empireEarthSettingsKey;
        private readonly string artOfConquestSettingsKey;

        private Product(string id, string appName, string publisher, string defaultInstallFolderName,
            string empireEarthSettingsKey, string artOfConquestSettingsKey, string productOnlyFileName)
        {
            Id = id;
            AppName = appName;
            Publisher = publisher;
            DefaultInstallFolderName = defaultInstallFolderName;
            this.empireEarthSettingsKey = empireEarthSettingsKey;
            this.artOfConquestSettingsKey = artOfConquestSettingsKey;
            ProductOnlyFileName = productOnlyFileName;
        }

        /// <summary>Product id, the <c>InstallType</c> of the setup: <c>EE</c> or <c>NeoEE</c>.</summary>
        public string Id { get; }

        /// <summary><c>AppName</c> of the setup.</summary>
        public string AppName { get; }

        /// <summary>Exact <c>Publisher</c> of the uninstall key; it identifies community setups (contract 1.4).</summary>
        public string Publisher { get; }

        /// <summary>Folder name of the default install root below <c>{autopf32}</c>.</summary>
        public string DefaultInstallFolderName { get; }

        /// <summary>Hidden setup data folder below the install root (contract 1.2, 2.1).</summary>
        public string SetupDataFolderName
        {
            get { return "_setupdata_" + Id; }
        }

        /// <summary>
        /// Name of the mutex a running setup of this product holds (<c>SetupMutex</c>, session namespace, contract
        /// 4.2).
        /// </summary>
        public string SetupMutexName
        {
            get { return Id + "_Setup"; }
        }

        /// <summary>File that only this product installs into the game folders, or null (EE has none).</summary>
        public string ProductOnlyFileName { get; }

        /// <summary>Registry record of this product (contract 1.1), relative to HKCU or HKLM64. Read-only.</summary>
        public string InstallRecordKey
        {
            get { return ContractNames.InstallRecordsKey + "\\" + Id; }
        }

        /// <summary>HKCU key of the defaults marker of this product (contract 3.5).</summary>
        public string DefaultsMarkerKey
        {
            get { return ContractNames.DefaultsMarkersKey + "\\" + Id; }
        }

        /// <summary>The HKCU game settings key of <paramref name="game"/> for this product (contract 3.1).</summary>
        public string GetGameSettingsKey(Game game)
        {
            if (game == null)
                throw new ArgumentNullException(nameof(game));
            return game == Game.EmpireEarth ? empireEarthSettingsKey : artOfConquestSettingsKey;
        }

        /// <summary>
        /// The product with the id <paramref name="id"/> (compared ignoring case, as the setup writes it into
        /// <c>install.ini</c>), or null if it is unknown.
        /// </summary>
        public static Product FromId(string id)
        {
            return All.FirstOrDefault(product => string.Equals(product.Id, id, StringComparison.OrdinalIgnoreCase));
        }

        public override string ToString()
        {
            return Id;
        }
    }
}

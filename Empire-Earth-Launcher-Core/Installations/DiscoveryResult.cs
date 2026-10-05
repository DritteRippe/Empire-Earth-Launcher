using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Empire_Earth_Launcher.Core.Contract;

namespace Empire_Earth_Launcher.Core.Installations
{
    /// <summary>What the discovery found (contract 1.4): every installation and the selected one.</summary>
    public sealed class DiscoveryResult
    {
        private readonly ReadOnlyCollection<Installation> installations;

        internal DiscoveryResult(IEnumerable<Installation> installations, Installation selected, bool isSelectedByUser,
            string userChoice)
        {
            this.installations = new ReadOnlyCollection<Installation>(
                (installations ?? throw new ArgumentNullException(nameof(installations))).ToList());
            if (selected != null && !this.installations.Contains(selected))
                throw new ArgumentException("The selected installation must be one of the installations.", nameof(selected));
            Selected = selected;
            IsSelectedByUser = isSelectedByUser;
            UserChoice = userChoice;
        }

        /// <summary>
        /// Every installation found, one per install root, ordered by their sources (contract 1.4: user choice, record,
        /// uninstall key, "Installed From", launcher folder), so the selected installation is the first.
        /// </summary>
        public IReadOnlyList<Installation> Installations
        {
            get { return installations; }
        }

        /// <summary>
        /// The installation the launcher works with: the one the user chose, else the first one found (contract 1.4,
        /// "Default selection"); null if there is none.
        /// </summary>
        public Installation Selected { get; }

        /// <summary>True if <see cref="Selected"/> is the user's choice (source 1).</summary>
        public bool IsSelectedByUser { get; }

        /// <summary>The folder chosen in the launcher settings (trimmed), or null for automatic detection.</summary>
        public string UserChoice { get; }

        /// <summary>
        /// The result for a session that was started with <c>--product=&lt;product&gt;</c> (contract 1.4, "Default selection"):
        /// the selected installation is the first installation of that product in the order of the sources, the user's
        /// choice first if it is of that product. Nothing else changes: the order, the user choice and the sources stay, and
        /// nothing is saved.
        /// </summary>
        /// <returns>
        /// This result if the selected installation is of that product already or there is none of that product (the rule of
        /// "Default selection" applies then); otherwise a result with the other selection, which is not the user's choice.
        /// </returns>
        public DiscoveryResult ForSessionProduct(Product product)
        {
            if (product == null)
                throw new ArgumentNullException(nameof(product));
            if (Selected != null && Selected.Product == product)
                return this;
            Installation first = installations.FirstOrDefault(installation => installation.Product == product);
            return first == null ? this : new DiscoveryResult(installations, first, false, UserChoice);
        }

        /// <summary>
        /// The other installations that use the same game settings keys as <paramref name="installation"/>: those of the
        /// same product (contract 3.1). Retail, GOG and older installations use the SSSI key of EE (contract 1.4).
        /// </summary>
        public IReadOnlyList<Installation> SharingSettingsWith(Installation installation)
        {
            if (installation == null)
                throw new ArgumentNullException(nameof(installation));
            return installations.Where(other => other != installation && other.Product == installation.Product).ToList();
        }

        /// <summary>
        /// The products whose game settings keys more than one installation uses, NeoEE before EE (the Launcher page
        /// shows a hint for each, ADR 0015).
        /// </summary>
        public IReadOnlyList<Product> ProductsWithSharedSettings
        {
            get
            {
                return Product.All.Where(product => installations.Count(installation => installation.Product == product) > 1)
                              .ToList();
            }
        }

        /// <summary>
        /// True if the launcher may apply the first-run defaults of <paramref name="installation"/> at its start
        /// (ADR 0015): the user chose it, or no other installation found uses its game settings keys.
        /// </summary>
        public bool IsUnambiguous(Installation installation)
        {
            if (installation == null)
                throw new ArgumentNullException(nameof(installation));
            return (IsSelectedByUser && installation == Selected) || SharingSettingsWith(installation).Count == 0;
        }
    }
}

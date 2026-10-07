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
            string userChoice, IEnumerable<ResolvedChoice> choices = null)
        {
            this.installations = new ReadOnlyCollection<Installation>(
                (installations ?? throw new ArgumentNullException(nameof(installations))).ToList());
            if (selected != null && !this.installations.Contains(selected))
                throw new ArgumentException("The selected installation must be one of the installations.", nameof(selected));
            Selected = selected;
            IsSelectedByUser = isSelectedByUser;
            UserChoice = userChoice;
            Choices = new ReadOnlyCollection<ResolvedChoice>((choices ?? new ResolvedChoice[0]).ToList());
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

        /// <summary>The first folder chosen in the launcher settings (trimmed), or null for automatic detection.</summary>
        public string UserChoice { get; }

        /// <summary>
        /// Every folder the user chose (contract 1.4 source 1, one per product since revision 6) with the installation it
        /// resolved to, the first choice first; empty for automatic detection.
        /// </summary>
        public IReadOnlyList<ResolvedChoice> Choices { get; }

        /// <summary>True if the discovery found an installation of <paramref name="product"/> (a chosen folder that is missing counts).</summary>
        public bool Has(Product product)
        {
            return FirstOf(product) != null;
        }

        /// <summary>The first installation of <paramref name="product"/> in the order of the sources; null if there is none.</summary>
        public Installation FirstOf(Product product)
        {
            if (product == null)
                throw new ArgumentNullException(nameof(product));
            return installations.FirstOrDefault(installation => installation.Product == product);
        }

        /// <summary>
        /// The installation the user's choice for <paramref name="product"/> selects (contract 1.4, "Default selection", revision 6):
        /// the first choice that was made for that product, or for none (the choice of an older launcher), whose installation is of
        /// that product; null if there is none. A folder that was chosen for the other product, and one that now belongs to the
        /// other product, is no choice for this product.
        /// </summary>
        public Installation ChosenFor(Product product)
        {
            if (product == null)
                throw new ArgumentNullException(nameof(product));
            foreach (ResolvedChoice choice in Choices)
            {
                if ((choice.Choice.Product == null || choice.Choice.Product == product) && choice.Installation.Product == product)
                    return choice.Installation;
            }
            return null;
        }

        /// <summary>The installation to work with for <paramref name="product"/>: <see cref="ChosenFor"/>, else <see cref="FirstOf"/>.</summary>
        public Installation SelectionFor(Product product)
        {
            return ChosenFor(product) ?? FirstOf(product);
        }

        /// <summary>
        /// The result with the selection of <paramref name="product"/> (contract 1.4, "Default selection", revision 6): the folder the
        /// user chose for it, else its first installation in the order of the sources. The list, its order and the choices stay;
        /// <see cref="IsSelectedByUser"/> tells whether the selection is a choice of the user. Used for the product of the game the
        /// player chose, and for <c>--product=</c> (for this session only; nothing is saved).
        /// </summary>
        /// <returns>
        /// This result if it selects that installation already, or if there is none of that product (the rule of "Default selection"
        /// applies then); otherwise a result with the other selection.
        /// </returns>
        public DiscoveryResult ForProduct(Product product)
        {
            if (product == null)
                throw new ArgumentNullException(nameof(product));
            Installation chosen = ChosenFor(product);
            Installation selection = chosen ?? FirstOf(product);
            if (selection == null || (selection == Selected && (chosen != null) == IsSelectedByUser))
                return this;
            return new DiscoveryResult(installations, selection, chosen != null, UserChoice, Choices);
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

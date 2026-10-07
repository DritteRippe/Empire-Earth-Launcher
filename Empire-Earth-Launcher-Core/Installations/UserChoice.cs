using System;
using Empire_Earth_Launcher.Core.Contract;

namespace Empire_Earth_Launcher.Core.Installations
{
    /// <summary>
    /// A folder the player chose (contract 1.4 source 1) and the product it was chosen for. Since revision 6 the launcher saves one
    /// chosen folder per product, so the discovery takes a list of them; every one only selects.
    /// </summary>
    public sealed class UserChoice
    {
        /// <param name="folder">The folder chosen (install root, EE folder or AoC folder), trimmed; not empty.</param>
        /// <param name="product">The product it was chosen for; null if that is not known (the choice of launcher 1.0.0).</param>
        public UserChoice(string folder, Product product)
        {
            if (string.IsNullOrWhiteSpace(folder))
                throw new ArgumentException("A chosen folder is required.", nameof(folder));
            Folder = folder.Trim();
            Product = product;
        }

        /// <summary>The folder chosen.</summary>
        public string Folder { get; }

        /// <summary>
        /// The product the folder was chosen for; null if that is not known. A folder that does not exist (any more) is listed
        /// as an installation of this product (null: EE); an installation of the other product that the folder now belongs to is
        /// no choice for this product (<see cref="DiscoveryResult.ChosenFor"/>).
        /// </summary>
        public Product Product { get; }

        public override string ToString()
        {
            return Folder + " (" + (Product == null ? "any product" : Product.Id) + ")";
        }
    }

    /// <summary>A <see cref="UserChoice"/> and the installation the discovery resolved it to.</summary>
    public sealed class ResolvedChoice
    {
        internal ResolvedChoice(UserChoice choice, Installation installation)
        {
            Choice = choice;
            Installation = installation;
        }

        public UserChoice Choice { get; }

        /// <summary>The installation the folder selects, or the one the discovery made of a folder that no other source found.</summary>
        public Installation Installation { get; }
    }
}

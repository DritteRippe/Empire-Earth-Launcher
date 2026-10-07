using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Installations;

namespace Empire_Earth_Launcher.Core.Play
{
    /// <summary>
    /// One of the four games of the Play page (launcher 1.1.0, contract 1.4 revision 6): Empire Earth and The Art of Conquest of
    /// each product. The page lists them in the order of <see cref="All"/>; the one the player chooses selects the installation of
    /// its product for every page and is remembered (<c>LauncherSettings.LastProduct</c> and <c>LastGame</c>). There are exactly
    /// four instances.
    /// </summary>
    public sealed class PlayEntry
    {
        /// <summary>Empire Earth.</summary>
        public static readonly PlayEntry EmpireEarth = new PlayEntry(Product.EE, Game.EmpireEarth);

        /// <summary>Empire Earth - The Art of Conquest.</summary>
        public static readonly PlayEntry EmpireEarthArtOfConquest = new PlayEntry(Product.EE, Game.ArtOfConquest);

        /// <summary>Neo Empire Earth.</summary>
        public static readonly PlayEntry NeoEmpireEarth = new PlayEntry(Product.NeoEE, Game.EmpireEarth);

        /// <summary>Neo Empire Earth - The Art of Conquest.</summary>
        public static readonly PlayEntry NeoEmpireEarthArtOfConquest = new PlayEntry(Product.NeoEE, Game.ArtOfConquest);

        /// <summary>The four games in the order of the Play page.</summary>
        public static readonly IReadOnlyList<PlayEntry> All = new ReadOnlyCollection<PlayEntry>(new[]
        {
            EmpireEarth, EmpireEarthArtOfConquest, NeoEmpireEarth, NeoEmpireEarthArtOfConquest
        });

        private PlayEntry(Product product, Game game)
        {
            Product = product;
            Game = game;
        }

        /// <summary>The product whose installation the game belongs to.</summary>
        public Product Product { get; }

        /// <summary>The game: Empire Earth or The Art of Conquest.</summary>
        public Game Game { get; }

        /// <summary>The entry of <paramref name="game"/> of <paramref name="product"/>.</summary>
        public static PlayEntry For(Product product, Game game)
        {
            if (product == null)
                throw new ArgumentNullException(nameof(product));
            if (game == null)
                throw new ArgumentNullException(nameof(game));
            foreach (PlayEntry entry in All)
            {
                if (entry.Product == product && entry.Game == game)
                    return entry;
            }
            throw new ArgumentException("There is no entry for " + product.Id + " " + game.Id + ".");
        }

        /// <summary>
        /// True if the player can choose <paramref name="entry"/>: the discovery has an installation to work with for its product
        /// (<see cref="DiscoveryResult.SelectionFor"/>, the folder chosen for the product, else its first installation) and, for
        /// The Art of Conquest, that installation has the AoC folder. A chosen folder that does not exist counts: Play then says so.
        /// </summary>
        public static bool IsAvailable(DiscoveryResult result, PlayEntry entry)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));
            Installation installation = result?.SelectionFor(entry.Product);
            return installation != null && (entry.Game == Game.EmpireEarth || installation.HasArtOfConquest);
        }

        public override string ToString()
        {
            return Product.Id + " " + Game.Id;
        }
    }
}

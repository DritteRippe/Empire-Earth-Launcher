using System;
using System.Collections.Generic;
using System.Linq;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Core.Settings;

namespace Empire_Earth_Launcher.Core.Installations
{
    /// <summary>
    /// The choices of the player in <see cref="LauncherSettings"/> since contract 1.4 revision 6 (launcher 1.1.0): the folder
    /// chosen for each product (<see cref="LauncherSettings.ProductFolders"/>), the product chosen last
    /// (<see cref="LauncherSettings.LastProduct"/>), and <see cref="LauncherSettings.GameDirectory"/> as a mirror of the folder of
    /// that product, so that launcher 1.0.0 reads the same installation from the same file (ADR 0005 amendment). Pure functions
    /// on the settings object; nothing is saved here.
    /// </summary>
    public static class ProductChoices
    {
        /// <summary>The product chosen last; null for none or an id this launcher does not know.</summary>
        public static Product LastProduct(LauncherSettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));
            return Product.FromId(settings.LastProduct);
        }

        /// <summary>The folder chosen for <paramref name="product"/>; empty if none (automatic detection).</summary>
        public static string FolderOf(LauncherSettings settings, Product product)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));
            if (product == null)
                throw new ArgumentNullException(nameof(product));
            ProductFolder entry = settings.ProductFolders.FirstOrDefault(candidate => IsEntryOf(candidate, product));
            return entry?.Folder?.Trim() ?? string.Empty;
        }

        /// <summary>
        /// Saves <paramref name="folder"/> as the folder chosen for <paramref name="product"/> and makes it the product chosen
        /// last; the mirror <see cref="LauncherSettings.GameDirectory"/> follows. An empty folder removes the choice of the
        /// product (automatic detection).
        /// </summary>
        public static void Choose(LauncherSettings settings, Product product, string folder)
        {
            SetFolder(settings, product, folder);
            settings.LastProduct = product.Id;
            Mirror(settings);
        }

        /// <summary>The player chose a game of <paramref name="product"/>: it becomes the product chosen last; the folders stay.</summary>
        public static void ChooseProduct(LauncherSettings settings, Product product)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));
            if (product == null)
                throw new ArgumentNullException(nameof(product));
            settings.LastProduct = product.Id;
            Mirror(settings);
        }

        /// <summary>Removes the folder chosen for <paramref name="product"/> (automatic detection); the product chosen last stays.</summary>
        public static void ClearFolder(LauncherSettings settings, Product product)
        {
            SetFolder(settings, product, string.Empty);
            Mirror(settings);
        }

        /// <summary>
        /// True if <see cref="LauncherSettings.GameDirectory"/> is a choice of an older launcher or of the "Browse" button, which does not
        /// say what product it belongs to: it is not empty and not the folder chosen for the product chosen last (or there is no
        /// such product). The discovery then resolves it without a product, and the product of its installation becomes the product
        /// chosen last (<see cref="Choose"/>).
        /// </summary>
        public static bool HasChoiceOfOlderLauncher(LauncherSettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));
            string folder = (settings.GameDirectory ?? string.Empty).Trim();
            if (folder.Length == 0)
                return false;
            Product last = LastProduct(settings);
            if (last == null)
                return true;
            string chosen = FolderOf(settings, last);
            return chosen.Length == 0 || !WinPath.IsSamePath(folder, chosen);
        }

        /// <summary>
        /// The folders to hand to the discovery as source 1, in this order: <see cref="LauncherSettings.GameDirectory"/> first (for
        /// the product chosen last, or without a product if it is the choice of an older launcher), then the folder of every other
        /// product. A folder named twice (the same path) is named once.
        /// </summary>
        public static IReadOnlyList<UserChoice> UserChoices(LauncherSettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));
            var choices = new List<UserChoice>();
            string mirror = (settings.GameDirectory ?? string.Empty).Trim();
            if (mirror.Length > 0)
                choices.Add(new UserChoice(mirror, HasChoiceOfOlderLauncher(settings) ? null : LastProduct(settings)));
            foreach (ProductFolder entry in settings.ProductFolders)
            {
                Product product = Product.FromId(entry.Product);
                string folder = entry.Folder?.Trim() ?? string.Empty;
                if (product == null || folder.Length == 0 || choices.Any(choice => WinPath.IsSamePath(choice.Folder, folder)))
                    continue;
                choices.Add(new UserChoice(folder, product));
            }
            return choices;
        }

        private static void SetFolder(LauncherSettings settings, Product product, string folder)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));
            if (product == null)
                throw new ArgumentNullException(nameof(product));
            string trimmed = string.IsNullOrWhiteSpace(folder) ? string.Empty : folder.Trim();
            ProductFolder entry = settings.ProductFolders.FirstOrDefault(candidate => IsEntryOf(candidate, product));
            if (trimmed.Length == 0)
            {
                settings.ProductFolders.RemoveAll(candidate => IsEntryOf(candidate, product));
                return;
            }
            if (entry == null)
                settings.ProductFolders.Add(new ProductFolder { Product = product.Id, Folder = trimmed });
            else
                entry.Folder = trimmed;
        }

        /// <summary>The mirror: <see cref="LauncherSettings.GameDirectory"/> is the folder of the product chosen last.</summary>
        private static void Mirror(LauncherSettings settings)
        {
            Product last = LastProduct(settings);
            settings.GameDirectory = last == null ? string.Empty : FolderOf(settings, last);
        }

        private static bool IsEntryOf(ProductFolder entry, Product product)
        {
            return string.Equals(entry.Product, product.Id, StringComparison.OrdinalIgnoreCase);
        }
    }
}

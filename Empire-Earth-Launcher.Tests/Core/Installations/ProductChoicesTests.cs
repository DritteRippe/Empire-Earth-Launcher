using System;
using System.Linq;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Settings;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.Installations
{
    /// <summary>
    /// <see cref="ProductChoices"/> (contract 1.4 revision 6, ADR 0005 amendment): one chosen folder per product, the product
    /// chosen last, and <c>GameDirectory</c> as the mirror of the folder of that product, which launcher 1.0.0 reads. The
    /// choice of an older launcher is a <c>GameDirectory</c> that is not the folder of the product chosen last.
    /// </summary>
    [TestFixture]
    public class ProductChoicesTests
    {
        private const string EEFolder = @"D:\GOG Games\Empire Earth Gold\Empire Earth";
        private const string NeoRoot = @"C:\Program Files (x86)\Neo Empire Earth";

        private LauncherSettings settings;

        [SetUp]
        public void SetUp()
        {
            settings = new LauncherSettings();
        }

        [Test]
        public void ANewFile_HasNoChoices()
        {
            Assert.That(ProductChoices.LastProduct(settings), Is.Null);
            Assert.That(ProductChoices.FolderOf(settings, Product.EE), Is.Empty);
            Assert.That(ProductChoices.HasChoiceOfOlderLauncher(settings), Is.False);
            Assert.That(ProductChoices.UserChoices(settings), Is.Empty);
        }

        [Test]
        public void Choose_SavesTheFolderOfTheProduct_AndMirrorsItIntoGameDirectory()
        {
            ProductChoices.Choose(settings, Product.EE, "  " + EEFolder + "  ");

            Assert.That(ProductChoices.FolderOf(settings, Product.EE), Is.EqualTo(EEFolder), "trimmed");
            Assert.That(settings.LastProduct, Is.EqualTo("EE"));
            Assert.That(settings.GameDirectory, Is.EqualTo(EEFolder), "launcher 1.0.0 selects the same installation");
            Assert.That(ProductChoices.LastProduct(settings), Is.SameAs(Product.EE));
            Assert.That(ProductChoices.HasChoiceOfOlderLauncher(settings), Is.False);
        }

        [Test]
        public void Choose_ForASecondProduct_KeepsTheFirst_AndTheMirrorFollowsTheProductChosenLast()
        {
            ProductChoices.Choose(settings, Product.EE, EEFolder);
            ProductChoices.Choose(settings, Product.NeoEE, NeoRoot);

            Assert.That(settings.GameDirectory, Is.EqualTo(NeoRoot));
            Assert.That(ProductChoices.FolderOf(settings, Product.EE), Is.EqualTo(EEFolder));
            Assert.That(settings.ProductFolders, Has.Count.EqualTo(2));
        }

        [Test]
        public void Choose_ForTheSameProductAgain_ReplacesTheFolder()
        {
            ProductChoices.Choose(settings, Product.EE, EEFolder);
            ProductChoices.Choose(settings, Product.EE, @"E:\Other\Empire Earth");

            Assert.That(settings.ProductFolders, Has.Count.EqualTo(1));
            Assert.That(ProductChoices.FolderOf(settings, Product.EE), Is.EqualTo(@"E:\Other\Empire Earth"));
        }

        [Test]
        public void ChooseProduct_MirrorsTheFolderOfThatProduct_OrNothing()
        {
            ProductChoices.Choose(settings, Product.EE, EEFolder);

            ProductChoices.ChooseProduct(settings, Product.NeoEE);

            Assert.That(settings.LastProduct, Is.EqualTo("NeoEE"));
            Assert.That(settings.GameDirectory, Is.Empty, "NeoEE is found automatically: nothing for launcher 1.0.0 to select");
            Assert.That(ProductChoices.FolderOf(settings, Product.EE), Is.EqualTo(EEFolder), "the folder of EE stays");

            ProductChoices.ChooseProduct(settings, Product.EE);

            Assert.That(settings.GameDirectory, Is.EqualTo(EEFolder));
        }

        [Test]
        public void ClearFolder_RemovesTheChoiceOfOneProduct_AndKeepsTheProductChosenLast()
        {
            ProductChoices.Choose(settings, Product.EE, EEFolder);
            ProductChoices.Choose(settings, Product.NeoEE, NeoRoot);

            ProductChoices.ClearFolder(settings, Product.NeoEE);

            Assert.That(ProductChoices.FolderOf(settings, Product.NeoEE), Is.Empty);
            Assert.That(settings.GameDirectory, Is.Empty);
            Assert.That(settings.LastProduct, Is.EqualTo("NeoEE"));
            Assert.That(ProductChoices.FolderOf(settings, Product.EE), Is.EqualTo(EEFolder));
            Assert.That(settings.ProductFolders.Select(entry => entry.Product), Is.EqualTo(new[] { "EE" }), "no empty entry is kept");
        }

        [Test]
        public void ClearFolder_WithoutAProductChosenLast_ClearsTheChoiceOfAnOlderLauncher()
        {
            settings.GameDirectory = EEFolder;

            ProductChoices.ClearFolder(settings, Product.EE);

            Assert.That(settings.GameDirectory, Is.Empty);
            Assert.That(ProductChoices.UserChoices(settings), Is.Empty);
        }

        [Test]
        public void AnEntryOfAnUnknownProduct_IsKept_AndNeverAChoice()
        {
            settings.ProductFolders.Add(new ProductFolder { Product = "FutureEE", Folder = @"C:\Future" });

            ProductChoices.Choose(settings, Product.EE, EEFolder);
            ProductChoices.ClearFolder(settings, Product.EE);

            Assert.That(settings.ProductFolders.Select(entry => entry.Product), Is.EqualTo(new[] { "FutureEE" }));
            Assert.That(ProductChoices.UserChoices(settings), Is.Empty);
        }

        [Test]
        public void ALastProductThatIsNotKnown_IsNoProduct()
        {
            settings.LastProduct = "FutureEE";

            Assert.That(ProductChoices.LastProduct(settings), Is.Null);
            Assert.That(settings.LastProduct, Is.EqualTo("FutureEE"), "kept as written");
        }

        // --- The choice of an older launcher ----------------------------------------------------------------------------

        [Test]
        public void AGameDirectoryWithoutAProductChosenLast_IsTheChoiceOfAnOlderLauncher()
        {
            settings.GameDirectory = EEFolder;

            Assert.That(ProductChoices.HasChoiceOfOlderLauncher(settings), Is.True);
            UserChoice choice = ProductChoices.UserChoices(settings).Single();
            Assert.That(choice.Folder, Is.EqualTo(EEFolder));
            Assert.That(choice.Product, Is.Null, "the product is not known: the installation the folder selects says it");
        }

        [Test]
        public void AGameDirectoryThatIsNotTheFolderOfTheProductChosenLast_IsTheChoiceOfAnOlderLauncher()
        {
            ProductChoices.Choose(settings, Product.EE, EEFolder);
            settings.GameDirectory = NeoRoot;

            Assert.That(ProductChoices.HasChoiceOfOlderLauncher(settings), Is.True);
            Assert.That(ProductChoices.UserChoices(settings).Select(choice => choice.Folder + "|" + choice.Product?.Id),
                Is.EqualTo(new[] { NeoRoot + "|", EEFolder + "|EE" }), "the mirror first, then the folder of the product");
        }

        [Test]
        public void TheSamePath_InAnotherCaseOrWithATrailingBackslash_IsNoChoiceOfAnOlderLauncher()
        {
            ProductChoices.Choose(settings, Product.EE, EEFolder);

            settings.GameDirectory = EEFolder.ToUpperInvariant() + @"\";

            Assert.That(ProductChoices.HasChoiceOfOlderLauncher(settings), Is.False);
            Assert.That(ProductChoices.UserChoices(settings), Has.Count.EqualTo(1), "named once");
        }

        [Test]
        public void AProductChosenLastWithoutAFolder_AndAGameDirectory_IsTheChoiceOfAnOlderLauncher()
        {
            // The player chose a folder with the browse button while NeoEE (found automatically) was the product chosen last.
            settings.LastProduct = "NeoEE";
            settings.GameDirectory = EEFolder;

            Assert.That(ProductChoices.HasChoiceOfOlderLauncher(settings), Is.True);
        }

        // --- The choices for the discovery ------------------------------------------------------------------------------

        [Test]
        public void UserChoices_NameTheMirrorFirst_ThenTheFoldersOfTheOtherProducts_WithTheirProducts()
        {
            ProductChoices.Choose(settings, Product.EE, EEFolder);
            ProductChoices.Choose(settings, Product.NeoEE, NeoRoot);

            Assert.That(ProductChoices.UserChoices(settings).Select(choice => choice.Folder + "|" + choice.Product.Id),
                Is.EqualTo(new[] { NeoRoot + "|NeoEE", EEFolder + "|EE" }));
        }

        [Test]
        public void UserChoices_SkipEmptyEntries_AndTwoProductsWithTheSameFolderAreNamedOnce()
        {
            settings.ProductFolders.Add(new ProductFolder { Product = "EE", Folder = "  " });
            settings.ProductFolders.Add(new ProductFolder { Product = "NeoEE", Folder = null });
            settings.ProductFolders.Add(new ProductFolder { Product = "EE", Folder = @"C:\Shared Root" });
            settings.ProductFolders.Add(new ProductFolder { Product = "NeoEE", Folder = @"c:\shared root\" });

            UserChoice choice = ProductChoices.UserChoices(settings).Single();

            Assert.That(choice.Folder, Is.EqualTo(@"C:\Shared Root"));
            Assert.That(choice.Product, Is.SameAs(Product.EE), "the first entry wins");
        }

        [Test]
        public void Arguments_AreChecked()
        {
            Assert.That(() => ProductChoices.LastProduct(null), Throws.ArgumentNullException);
            Assert.That(() => ProductChoices.FolderOf(null, Product.EE), Throws.ArgumentNullException);
            Assert.That(() => ProductChoices.FolderOf(settings, null), Throws.ArgumentNullException);
            Assert.That(() => ProductChoices.Choose(settings, null, EEFolder), Throws.ArgumentNullException);
            Assert.That(() => ProductChoices.ChooseProduct(settings, null), Throws.ArgumentNullException);
            Assert.That(() => ProductChoices.UserChoices(null), Throws.ArgumentNullException);
            Assert.That(() => new UserChoice(" ", Product.EE), Throws.InstanceOf<ArgumentException>());
        }
    }
}

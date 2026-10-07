using System.Linq;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Tests.TestSupport;
using Microsoft.Win32;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.Installations
{
    /// <summary>
    /// <see cref="DiscoveryResult.ForProduct"/>, <see cref="DiscoveryResult.ChosenFor"/> and their helpers: the selection of
    /// <c>--product=EE|NeoEE</c> (contract 1.4, "Default selection", revision 4) and of the product of the game the player chose
    /// (revision 6): the folder chosen for that product, else its first installation in the order of the sources; the list, its
    /// order and the choices stay.
    /// </summary>
    [TestFixture]
    public class DiscoveryResultSessionProductTests
    {
        private const string NeoRoot = @"C:\Program Files (x86)\Neo Empire Earth";
        private const string EERoot = @"C:\Program Files (x86)\Empire Earth Community";
        private const string EERoot2 = @"D:\Games\EE2";

        private InstallationWorld world;

        [SetUp]
        public void SetUp()
        {
            world = new InstallationWorld();
            world.AddCommunityInstallation(NeoRoot, Product.NeoEE);
            world.AddCommunityInstallation(EERoot, Product.EE);
        }

        [Test]
        public void TheSelectedProduct_ReturnsTheSameResult()
        {
            DiscoveryResult result = world.Discover();
            Assert.That(result.Selected.Product, Is.SameAs(Product.NeoEE), "NeoEE is found first");

            Assert.That(result.ForProduct(Product.NeoEE), Is.SameAs(result));
        }

        [Test]
        public void AnotherProduct_SelectsItsInstallation_AndKeepsEverythingElse()
        {
            DiscoveryResult result = world.Discover();

            DiscoveryResult session = result.ForProduct(Product.EE);

            Assert.That(session, Is.Not.SameAs(result));
            Assert.That(session.Selected.Root, Is.EqualTo(EERoot));
            Assert.That(session.Selected.Product, Is.SameAs(Product.EE));
            Assert.That(session.IsSelectedByUser, Is.False);
            Assert.That(session.Installations, Is.EqualTo(result.Installations), "the order is the order of the sources");
            Assert.That(session.UserChoice, Is.EqualTo(result.UserChoice));
            Assert.That(result.Selected.Product, Is.SameAs(Product.NeoEE), "the original result is unchanged");
        }

        [Test]
        public void WithoutAnInstallationOfTheProduct_TheResultStays()
        {
            world = new InstallationWorld();
            world.AddCommunityInstallation(NeoRoot, Product.NeoEE);
            DiscoveryResult result = world.Discover();

            Assert.That(result.ForProduct(Product.EE), Is.SameAs(result), "ignored: the rule of the default selection applies");
        }

        [Test]
        public void WithoutAnyInstallation_TheResultStays()
        {
            DiscoveryResult result = new InstallationWorld().Discover();

            Assert.That(result.ForProduct(Product.EE), Is.SameAs(result));
            Assert.That(result.Selected, Is.Null);
        }

        [Test]
        public void TheFirstInstallationOfTheProduct_InTheOrderOfTheSources_IsTaken()
        {
            // Two EE installations: the one of the registry record (source 2) comes before the one of "Installed From" (source 4).
            world.AddForeignInstallation(EERoot2);
            DiscoveryResult result = world.Discover();
            Installation firstEE = result.Installations.First(installation => installation.Product == Product.EE);

            DiscoveryResult session = result.ForProduct(Product.EE);

            Assert.That(session.Selected, Is.SameAs(firstEE));
            Assert.That(firstEE.Root, Is.EqualTo(EERoot), "source 2 before source 4");
        }

        [Test]
        public void TheUsersChoiceOfThatProduct_IsKept_AsTheUsersChoice()
        {
            world.AddForeignInstallation(EERoot2);
            DiscoveryResult result = world.Discover(userChoice: EERoot2);
            Assert.That(result.IsSelectedByUser, Is.True);

            DiscoveryResult session = result.ForProduct(Product.EE);

            Assert.That(session, Is.SameAs(result), "the user choice first, if it is of that product");
            Assert.That(session.Selected.Root, Is.EqualTo(EERoot2).IgnoreCase.Or.EqualTo(@"D:\Games").IgnoreCase);
            Assert.That(session.IsSelectedByUser, Is.True);
        }

        [Test]
        public void TheUsersChoiceOfAnotherProduct_GivesWayToTheArgument()
        {
            DiscoveryResult result = world.Discover(userChoice: NeoRoot);
            Assert.That(result.IsSelectedByUser, Is.True);

            DiscoveryResult session = result.ForProduct(Product.EE);

            Assert.That(session.Selected.Root, Is.EqualTo(EERoot));
            Assert.That(session.IsSelectedByUser, Is.False);
            Assert.That(session.UserChoice, Is.EqualTo(NeoRoot), "the choice itself is not touched");
        }

        [Test]
        public void ForProduct_ChecksItsArgument()
        {
            Assert.That(() => world.Discover().ForProduct(null), Throws.ArgumentNullException);
            Assert.That(() => world.Discover().FirstOf(null), Throws.ArgumentNullException);
            Assert.That(() => world.Discover().ChosenFor(null), Throws.ArgumentNullException);
            Assert.That(() => world.Discover().SelectionFor(null), Throws.ArgumentNullException);
        }

        // --- One folder per product (contract 1.4 revision 6) ---------------------------------------------------------------

        private const string GogFolder = @"D:\GOG Games\Empire Earth Gold\Empire Earth";

        private DiscoveryResult Choose(params UserChoice[] choices)
        {
            return world.CreateDiscovery().DiscoverChoices(choices, null);
        }

        [Test]
        public void FirstOf_IsTheFirstInstallationOfTheProduct_InTheOrderOfTheSources()
        {
            world.AddForeignInstallation(EERoot2);
            DiscoveryResult result = world.Discover();

            Assert.That(result.FirstOf(Product.EE).Root, Is.EqualTo(EERoot), "source 2 before source 4");
            Assert.That(result.FirstOf(Product.NeoEE).Root, Is.EqualTo(NeoRoot));
            Assert.That(result.Has(Product.EE), Is.True);
            Assert.That(new InstallationWorld().Discover().FirstOf(Product.EE), Is.Null);
            Assert.That(new InstallationWorld().Discover().Has(Product.NeoEE), Is.False);
        }

        [Test]
        public void WithoutAChoice_NothingIsChosenFor_AProduct_AndTheSelectionIsTheFirstOne()
        {
            DiscoveryResult result = world.Discover();

            Assert.That(result.ChosenFor(Product.EE), Is.Null);
            Assert.That(result.ChosenFor(Product.NeoEE), Is.Null);
            Assert.That(result.Choices, Is.Empty);
            Assert.That(result.SelectionFor(Product.EE), Is.SameAs(result.FirstOf(Product.EE)));
        }

        [Test]
        public void TwoChoices_ForTwoProducts_AreBothChosen_AndBothSource1()
        {
            world.AddForeignInstallation(GogFolder);
            DiscoveryResult result = Choose(new UserChoice(NeoRoot, Product.NeoEE), new UserChoice(GogFolder, Product.EE));

            Assert.That(result.ChosenFor(Product.NeoEE).Root, Is.EqualTo(NeoRoot));
            Assert.That(result.ChosenFor(Product.EE).EeFolder, Is.EqualTo(GogFolder).IgnoreCase);
            Assert.That(result.SelectionFor(Product.EE).EeFolder, Is.EqualTo(GogFolder).IgnoreCase, "the chosen copy, not the first EE found");
            Assert.That(result.FirstOf(Product.EE).Root, Is.EqualTo(@"D:\GOG Games\Empire Earth Gold").IgnoreCase, "source 1 comes first in the list");
            Assert.That(result.Selected.Root, Is.EqualTo(NeoRoot), "the selection of the discovery is the first choice");
            Assert.That(result.UserChoice, Is.EqualTo(NeoRoot));
            Assert.That(result.Choices.Select(choice => choice.Installation), Is.EqualTo(new[] { result.ChosenFor(Product.NeoEE), result.ChosenFor(Product.EE) }));
            Assert.That(result.Installations.Count(installation => installation.Sources.Contains(InstallationSource.UserChoice)),
                Is.EqualTo(2), "both are source 1");
        }

        [Test]
        public void ForProduct_SelectsTheFolderChosenForThatProduct_AndFlagsItAsTheUsersChoice()
        {
            world.AddForeignInstallation(GogFolder);
            DiscoveryResult result = Choose(new UserChoice(NeoRoot, Product.NeoEE), new UserChoice(GogFolder, Product.EE));

            DiscoveryResult ee = result.ForProduct(Product.EE);

            Assert.That(ee.Selected.EeFolder, Is.EqualTo(GogFolder).IgnoreCase);
            Assert.That(ee.IsSelectedByUser, Is.True);
            Assert.That(ee.Installations, Is.EqualTo(result.Installations));
            Assert.That(ee.Choices, Is.EqualTo(result.Choices));
            Assert.That(ee.UserChoice, Is.EqualTo(result.UserChoice));
            DiscoveryResult neo = ee.ForProduct(Product.NeoEE);
            Assert.That(neo.Selected, Is.SameAs(result.Selected), "back to the first choice: the same selection as the discovery made");
            Assert.That(neo.IsSelectedByUser, Is.True);
            Assert.That(ee.ForProduct(Product.EE), Is.SameAs(ee));
        }

        [Test]
        public void AFolderChosenForAnotherProduct_IsNoChoiceForThisOne()
        {
            // The folder of NeoEE was chosen for EE (a stale entry): it is an installation of NeoEE, so no choice for EE.
            DiscoveryResult result = Choose(new UserChoice(NeoRoot, Product.EE));

            Assert.That(result.ChosenFor(Product.EE), Is.Null, "its installation is of NeoEE now");
            Assert.That(result.ChosenFor(Product.NeoEE), Is.Null, "and it was not chosen for NeoEE");
            Assert.That(result.Installations.Any(installation => installation.Root == NeoRoot), Is.True, "it stays listed");
            DiscoveryResult ee = result.ForProduct(Product.EE);
            Assert.That(ee.Selected.Root, Is.EqualTo(EERoot), "the first EE installation");
            Assert.That(ee.IsSelectedByUser, Is.False);
        }

        [Test]
        public void AChoiceWithoutAProduct_IsAChoiceForTheProductOfItsInstallation()
        {
            // The choice of launcher 1.0.0: the product is what the installation says.
            DiscoveryResult result = Choose(new UserChoice(EERoot, null));

            Assert.That(result.ChosenFor(Product.EE).Root, Is.EqualTo(EERoot));
            Assert.That(result.ChosenFor(Product.NeoEE), Is.Null);
            Assert.That(result.ForProduct(Product.NeoEE).Selected.Root, Is.EqualTo(NeoRoot));
            Assert.That(result.ForProduct(Product.NeoEE).IsSelectedByUser, Is.False);
        }

        [Test]
        public void ForProduct_WithoutAnInstallationOfIt_KeepsTheResult_EvenWithAChoice()
        {
            world = new InstallationWorld();
            world.AddCommunityInstallation(NeoRoot, Product.NeoEE);
            DiscoveryResult result = Choose(new UserChoice(NeoRoot, Product.NeoEE));

            Assert.That(result.ForProduct(Product.EE), Is.SameAs(result));
            Assert.That(result.SelectionFor(Product.EE), Is.Null);
        }

        [Test]
        public void TheFirstChoiceThatMatches_Wins()
        {
            world.AddForeignInstallation(GogFolder);
            DiscoveryResult result = Choose(new UserChoice(EERoot, null), new UserChoice(GogFolder, Product.EE));

            Assert.That(result.ChosenFor(Product.EE).Root, Is.EqualTo(EERoot));
        }
    }
}

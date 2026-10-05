using System.Linq;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Tests.TestSupport;
using Microsoft.Win32;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.Installations
{
    /// <summary>
    /// <see cref="DiscoveryResult.ForSessionProduct"/>: the selection of <c>--product=EE|NeoEE</c> (contract 1.4, "Default
    /// selection", revision 4): the first installation of that product in the order of the sources, the user's choice first if it
    /// is of that product; the list, its order and the user choice stay.
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

            Assert.That(result.ForSessionProduct(Product.NeoEE), Is.SameAs(result));
        }

        [Test]
        public void AnotherProduct_SelectsItsInstallation_AndKeepsEverythingElse()
        {
            DiscoveryResult result = world.Discover();

            DiscoveryResult session = result.ForSessionProduct(Product.EE);

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

            Assert.That(result.ForSessionProduct(Product.EE), Is.SameAs(result), "ignored: the rule of the default selection applies");
        }

        [Test]
        public void WithoutAnyInstallation_TheResultStays()
        {
            DiscoveryResult result = new InstallationWorld().Discover();

            Assert.That(result.ForSessionProduct(Product.EE), Is.SameAs(result));
            Assert.That(result.Selected, Is.Null);
        }

        [Test]
        public void TheFirstInstallationOfTheProduct_InTheOrderOfTheSources_IsTaken()
        {
            // Two EE installations: the one of the registry record (source 2) comes before the one of "Installed From" (source 4).
            world.AddForeignInstallation(EERoot2);
            DiscoveryResult result = world.Discover();
            Installation firstEE = result.Installations.First(installation => installation.Product == Product.EE);

            DiscoveryResult session = result.ForSessionProduct(Product.EE);

            Assert.That(session.Selected, Is.SameAs(firstEE));
            Assert.That(firstEE.Root, Is.EqualTo(EERoot), "source 2 before source 4");
        }

        [Test]
        public void TheUsersChoiceOfThatProduct_IsKept_AsTheUsersChoice()
        {
            world.AddForeignInstallation(EERoot2);
            DiscoveryResult result = world.Discover(userChoice: EERoot2);
            Assert.That(result.IsSelectedByUser, Is.True);

            DiscoveryResult session = result.ForSessionProduct(Product.EE);

            Assert.That(session, Is.SameAs(result), "the user choice first, if it is of that product");
            Assert.That(session.Selected.Root, Is.EqualTo(EERoot2).IgnoreCase.Or.EqualTo(@"D:\Games").IgnoreCase);
            Assert.That(session.IsSelectedByUser, Is.True);
        }

        [Test]
        public void TheUsersChoiceOfAnotherProduct_GivesWayToTheArgument()
        {
            DiscoveryResult result = world.Discover(userChoice: NeoRoot);
            Assert.That(result.IsSelectedByUser, Is.True);

            DiscoveryResult session = result.ForSessionProduct(Product.EE);

            Assert.That(session.Selected.Root, Is.EqualTo(EERoot));
            Assert.That(session.IsSelectedByUser, Is.False);
            Assert.That(session.UserChoice, Is.EqualTo(NeoRoot), "the choice itself is not touched");
        }

        [Test]
        public void ForSessionProduct_ChecksItsArgument()
        {
            Assert.That(() => world.Discover().ForSessionProduct(null), Throws.ArgumentNullException);
        }
    }
}

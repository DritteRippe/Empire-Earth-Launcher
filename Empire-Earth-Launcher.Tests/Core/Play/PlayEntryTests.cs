using System;
using System.Linq;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Play;
using Empire_Earth_Launcher.Tests.TestSupport;
using Microsoft.Win32;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.Play
{
    /// <summary>
    /// <see cref="PlayEntry"/>, the four games of the Play page (launcher 1.1.0, contract 1.4 revision 6): their order and
    /// which of them the discovery has an installation for (the others are shown disabled).
    /// </summary>
    [TestFixture]
    public class PlayEntryTests
    {
        private const string NeoRoot = @"C:\Program Files (x86)\Neo Empire Earth";
        private const string EERoot = @"C:\Program Files (x86)\Empire Earth Community";
        private const string GogFolder = @"D:\GOG Games\Empire Earth Gold\Empire Earth";
        private const string GogAocFolder = @"D:\GOG Games\Empire Earth Gold\Empire Earth - The Art of Conquest";

        private static bool[] Available(DiscoveryResult result)
        {
            return PlayEntry.All.Select(entry => PlayEntry.IsAvailable(result, entry)).ToArray();
        }

        [Test]
        public void TheFourGames_AreInTheOrderOfThePage()
        {
            Assert.That(PlayEntry.All.Select(entry => entry.Product.Id + " " + entry.Game.Id),
                Is.EqualTo(new[] { "EE EE", "EE AoC", "NeoEE EE", "NeoEE AoC" }), "Empire Earth, its Art of Conquest, then the same of Neo");
            Assert.That(PlayEntry.All[0], Is.SameAs(PlayEntry.EmpireEarth));
            Assert.That(PlayEntry.All[1], Is.SameAs(PlayEntry.EmpireEarthArtOfConquest));
            Assert.That(PlayEntry.All[2], Is.SameAs(PlayEntry.NeoEmpireEarth));
            Assert.That(PlayEntry.All[3], Is.SameAs(PlayEntry.NeoEmpireEarthArtOfConquest));
            Assert.That(PlayEntry.All.Distinct().Count(), Is.EqualTo(4));
        }

        [Test]
        public void For_FindsTheEntryOfAProductAndAGame()
        {
            foreach (PlayEntry entry in PlayEntry.All)
                Assert.That(PlayEntry.For(entry.Product, entry.Game), Is.SameAs(entry));
            Assert.That(() => PlayEntry.For(null, Game.EmpireEarth), Throws.ArgumentNullException);
            Assert.That(() => PlayEntry.For(Product.EE, null), Throws.ArgumentNullException);
        }

        [Test]
        public void BothProductsWithTheArtOfConquest_AllFourAreAvailable()
        {
            var world = new InstallationWorld();
            world.AddCommunityInstallation(NeoRoot, Product.NeoEE);
            world.AddCommunityInstallation(EERoot, Product.EE);

            Assert.That(Available(world.Discover()), Is.EqualTo(new[] { true, true, true, true }));
        }

        [Test]
        public void EEWithoutTheArtOfConquest_ItsEntryIsDisabled()
        {
            var world = new InstallationWorld();
            world.AddCommunityInstallation(NeoRoot, Product.NeoEE);
            world.AddCommunityInstallation(EERoot, Product.EE, artOfConquest: false);

            Assert.That(Available(world.Discover()), Is.EqualTo(new[] { true, false, true, true }));
        }

        [Test]
        public void OnlyNeoEE_TheEntriesOfEEAreDisabled()
        {
            var world = new InstallationWorld();
            world.AddCommunityInstallation(NeoRoot, Product.NeoEE);

            Assert.That(Available(world.Discover()), Is.EqualTo(new[] { false, false, true, true }));
        }

        [Test]
        public void OnlyEE_TheEntriesOfNeoEEAreDisabled()
        {
            var world = new InstallationWorld();
            world.AddCommunityInstallation(EERoot, Product.EE);

            Assert.That(Available(world.Discover()), Is.EqualTo(new[] { true, true, false, false }));
        }

        [Test]
        public void AForeignGogCopy_FeedsTheEntriesOfEE_TheArtOfConquestOnlyWithItsFolder()
        {
            var withoutAoc = new InstallationWorld();
            withoutAoc.AddForeignInstallation(GogFolder);
            var withAoc = new InstallationWorld();
            withAoc.AddForeignInstallation(GogFolder, aocFolder: GogAocFolder);

            Assert.That(Available(withoutAoc.Discover()), Is.EqualTo(new[] { true, false, false, false }));
            Assert.That(Available(withAoc.Discover()), Is.EqualTo(new[] { true, true, false, false }));
        }

        [Test]
        public void AForeignFolderWithNeoeeDll_FeedsTheEntriesOfNeoEE()
        {
            var world = new InstallationWorld();
            world.AddEmpireEarth(@"C:\NeoCopy\Empire Earth", neoee: true);
            world.SetInstalledFrom(InstallationWorld.Hkcu(@"Software\SSSI\Empire Earth"), @"C:\NeoCopy\Empire Earth");

            Assert.That(Available(world.Discover()), Is.EqualTo(new[] { false, false, true, false }));
        }

        [Test]
        public void Nothing_IsAvailable_AndNoResultIsNoChoice()
        {
            Assert.That(Available(new InstallationWorld().Discover()), Is.EqualTo(new[] { false, false, false, false }));
            Assert.That(Available(null), Is.EqualTo(new[] { false, false, false, false }), "before the first search");
        }

        [Test]
        public void AChosenFolderThatDoesNotExist_CountsAsAvailable_Play_ThenSaysSo()
        {
            var world = new InstallationWorld();
            world.AddCommunityInstallation(EERoot, Product.EE);
            DiscoveryResult result = world.CreateDiscovery().DiscoverChoices(
                new[] { new UserChoice(@"D:\Removed\Neo Empire Earth", Product.NeoEE) }, null);

            Assert.That(result.SelectionFor(Product.NeoEE).State, Is.EqualTo(InstallationState.FolderMissing));
            Assert.That(Available(result), Is.EqualTo(new[] { true, true, true, false }),
                "the Art of Conquest of a folder that is not there has no AoC folder");
        }

        [Test]
        public void TheFolderChosenForAProduct_DecidesTheArtOfConquestEntry()
        {
            // Two EE installations: the community one has the Art of Conquest, the GOG copy chosen for EE has not.
            var world = new InstallationWorld();
            world.AddCommunityInstallation(EERoot, Product.EE);
            world.AddForeignInstallation(GogFolder);
            DiscoveryResult result = world.CreateDiscovery().DiscoverChoices(new[] { new UserChoice(GogFolder, Product.EE) }, null);

            Assert.That(Available(result), Is.EqualTo(new[] { true, false, false, false }), "the chosen copy decides, not the first one found");
        }

        [Test]
        public void IsAvailable_ChecksItsEntry()
        {
            Assert.That(() => PlayEntry.IsAvailable(new InstallationWorld().Discover(), null), Throws.ArgumentNullException);
        }
    }
}

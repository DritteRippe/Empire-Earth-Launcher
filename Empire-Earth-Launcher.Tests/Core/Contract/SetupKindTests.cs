using System.Linq;
using Empire_Earth_Launcher.Core.Contract;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.Contract
{
    /// <summary><see cref="SetupKind"/>: the three setups whose mutex the launcher respects (contract 0, 4.2, revision 4).</summary>
    [TestFixture]
    public class SetupKindTests
    {
        [Test]
        public void Contract_4_2_TheMutexNames()
        {
            Assert.That(SetupKind.All.Select(kind => kind.MutexName),
                Is.EqualTo(new[] { "NeoEE_Setup", "EE_Setup", "EmpireEarthCommunity_Suite" }));
            Assert.That(SetupKind.All.Select(kind => kind.Id), Is.EqualTo(new[] { "NeoEE", "EE", "Suite" }));
        }

        [Test]
        public void TheProductSetups_AreTheMutexesOfTheProducts()
        {
            foreach (Product product in Product.All)
            {
                SetupKind kind = SetupKind.For(product);
                Assert.That(kind.Product, Is.SameAs(product));
                Assert.That(kind.MutexName, Is.EqualTo(product.SetupMutexName));
                Assert.That(kind.AppName, Is.EqualTo(product.AppName));
            }
        }

        [Test]
        public void TheSuite_HasNoProduct_AndTheNameOfTheContract()
        {
            Assert.That(SetupKind.Suite.Product, Is.Null);
            Assert.That(SetupKind.Suite.AppName, Is.EqualTo("Empire Earth Community"));
            Assert.That(SetupKind.Suite.MutexName, Is.EqualTo(ContractNames.SuiteSetupMutexName));
            Assert.That(SetupKind.Suite.ToString(), Is.EqualTo("Suite"));
        }

        [Test]
        public void TheLauncherMutex_IsNoSetupMutex()
        {
            Assert.That(SetupKind.All.Select(kind => kind.MutexName), Does.Not.Contain("EmpireEarthCommunityLauncher"));
        }

        [Test]
        public void For_ChecksItsArgument()
        {
            Assert.That(() => SetupKind.For(null), Throws.ArgumentNullException);
        }
    }
}

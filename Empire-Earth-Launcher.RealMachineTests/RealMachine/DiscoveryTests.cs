using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.RealMachineTests.Checks;
using Empire_Earth_Launcher.RealMachineTests.Harness;
using NUnit.Framework;

namespace Empire_Earth_Launcher.RealMachineTests.RealMachine
{
    /// <summary>
    /// Contract 1.4 on the runner: the discovery of the launcher finds exactly the expected installations with product, kind,
    /// mode, AppId, contract version, state, sources and folders (catalogue L of the setup's end-to-end workflow).
    /// </summary>
    [TestFixture]
    [Explicit(RealMachineRun.ExplicitReason)]
    [Category(RealMachineCategories.RealMachine)]
    [Order(1)]
    public class DiscoveryTests
    {
        private HarnessSession session;

        [OneTimeSetUp]
        public void OpenSession()
        {
            session = RealMachineRun.Open();
        }

        [Test]
        public void Discovery_FindsTheExpectedInstallations()
        {
            DiscoveryResult result = session.Discover();
            TestContext.Out.WriteLine(SafeText.Redact("Found: " + DiscoveryCheck.Describe(result)));

            Assert.That(session.CheckDiscovery(result), Is.Empty);
        }
    }
}

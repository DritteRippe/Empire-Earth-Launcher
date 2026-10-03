using System.Collections.Generic;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Integrity;
using Empire_Earth_Launcher.RealMachineTests.Harness;
using NUnit.Framework;

namespace Empire_Earth_Launcher.RealMachineTests.RealMachine
{
    /// <summary>
    /// Contract 2.5 on the runner: the quick and the full check of the real installed files against the manifest the setup
    /// wrote, with states, reasons and findings (<c>path|kind</c>, never a hash). Read-only.
    /// </summary>
    [TestFixture]
    [Explicit(RealMachineRun.ExplicitReason)]
    [Category(RealMachineCategories.RealMachine)]
    [Order(2)]
    public class IntegrityTests
    {
        private HarnessSession session;
        private DiscoveryResult discovery;

        [OneTimeSetUp]
        public void OpenSession()
        {
            session = RealMachineRun.Open();
            discovery = session.Discover();
        }

        [TestCase(IntegrityCheckKind.Quick)]
        [TestCase(IntegrityCheckKind.Full)]
        public void Check_GivesTheExpectedReport(IntegrityCheckKind kind)
        {
            var report = new List<string>();
            IReadOnlyList<string> problems = session.CheckIntegrity(discovery, kind, out int checkedInstallations, report);
            foreach (string line in report)
                TestContext.Out.WriteLine(SafeText.Redact(line));
            if (checkedInstallations == 0)
                Assert.Pass("No " + kind + " check is expected in this step.");

            Assert.That(problems, Is.Empty);
        }
    }
}

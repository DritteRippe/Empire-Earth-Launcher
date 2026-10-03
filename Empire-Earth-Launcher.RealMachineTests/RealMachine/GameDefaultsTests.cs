using System.Collections.Generic;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.RealMachineTests.Harness;
using NUnit.Framework;

namespace Empire_Earth_Launcher.RealMachineTests.RealMachine
{
    /// <summary>
    /// Contract 3.4 to 3.6 and R1/R4 on the runner, only in a step whose expectation has <c>defaults</c>: the defaults of the
    /// launcher start for the current Windows account (nothing to write after a setup since v2; after the job removed the game
    /// settings, a first run with the recommended values, equal to what the setup wrote), a second start that changes nothing,
    /// and the reset with its <c>.reg</c> backup. These are the only checks that write: HKCU through the launcher's write
    /// policy, files only below the work folder. The tests run in this order.
    /// </summary>
    [TestFixture]
    [Explicit(RealMachineRun.ExplicitReason)]
    [Category(RealMachineCategories.RealMachine)]
    [Order(4)]
    public class GameDefaultsTests
    {
        private HarnessSession session;
        private DiscoveryResult discovery;

        [OneTimeSetUp]
        public void OpenSession()
        {
            session = RealMachineRun.Open();
            discovery = session.Discover();
        }

        private void RequireDefaults()
        {
            if (session.Expectation.Defaults == null)
                Assert.Pass("The defaults are not applied in this step (no \"defaults\" in the expectation).");
        }

        [Test]
        [Order(1)]
        public void SetupValues_AreSavedWhenAsked()
        {
            RequireDefaults();
            if (session.Expectation.Defaults.RecordSetupValuesTo == null)
                Assert.Pass("No recordSetupValuesTo in this step.");

            Assert.That(session.RecordSetupValues(discovery), Is.Empty);
        }

        [Test]
        [Order(2)]
        public void LauncherStart_AppliesTheExpectedDefaults()
        {
            RequireDefaults();
            var report = new List<string>();
            IReadOnlyList<string> problems = session.ApplyDefaultsAtStart(discovery, report);
            foreach (string line in report)
                TestContext.Out.WriteLine(SafeText.Redact(line));

            Assert.That(problems, Is.Empty);
        }

        [Test]
        [Order(3)]
        public void SecondLauncherStart_ChangesNothing()
        {
            RequireDefaults();
            if (!session.Expectation.Defaults.SecondStartChangesNothing)
                Assert.Pass("secondStartChangesNothing is false in this step.");

            Assert.That(session.CheckSecondStart(discovery), Is.Empty);
        }

        [Test]
        [Order(4)]
        public void Reset_RestoresTheRecommendedValues()
        {
            RequireDefaults();
            if (!session.Expectation.Defaults.Reset)
                Assert.Pass("No reset in this step.");

            Assert.That(session.CheckReset(discovery), Is.Empty);
        }
    }
}

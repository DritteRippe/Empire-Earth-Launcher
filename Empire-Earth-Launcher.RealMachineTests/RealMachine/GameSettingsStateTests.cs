using System.Collections.Generic;
using Empire_Earth_Launcher.RealMachineTests.Harness;
using NUnit.Framework;

namespace Empire_Earth_Launcher.RealMachineTests.RealMachine
{
    /// <summary>
    /// Contract 3.3, 3.5 and 3.7 on the runner, before the launcher changes anything: the status of the defaults per game
    /// (<c>Applied</c> after a setup since v2 for the account that ran it, <c>Pending</c> after 1.7.2) and the consistency
    /// findings of the game settings the setup wrote. Read-only.
    /// </summary>
    [TestFixture]
    [Explicit(RealMachineRun.ExplicitReason)]
    [Category(RealMachineCategories.RealMachine)]
    [Order(3)]
    public class GameSettingsStateTests
    {
        private HarnessSession session;

        [OneTimeSetUp]
        public void OpenSession()
        {
            session = RealMachineRun.Open();
        }

        [Test]
        public void StatusAndConsistency_AreTheExpectedOnes()
        {
            IReadOnlyList<string> problems = session.CheckGameSettingsState(session.Discover(), out int checkedInstallations);
            if (checkedInstallations == 0)
                Assert.Pass("No defaults status or consistency findings are expected in this step.");

            Assert.That(problems, Is.Empty);
        }
    }
}

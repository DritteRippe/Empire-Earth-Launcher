using Empire_Earth_Launcher.RealMachineTests.Harness;
using NUnit.Framework;

namespace Empire_Earth_Launcher.RealMachineTests.RealMachine
{
    /// <summary>
    /// Last of every run: the launcher core left alone what it must (briefing D6, contract 1.4, 2.5): the CD keys of every view,
    /// the install records, the uninstall keys of Inno Setup, the compatibility layers, the HKLM game settings and every file
    /// below the watched roots are as before the first check; no change was refused on the way; no game runs.
    /// </summary>
    [TestFixture]
    [Explicit(RealMachineRun.ExplicitReason)]
    [Category(RealMachineCategories.RealMachine)]
    [Order(100)]
    public class MachineStateTests
    {
        private HarnessSession session;

        [OneTimeSetUp]
        public void OpenSession()
        {
            session = RealMachineRun.Open();
        }

        [Test]
        public void TheChecks_LeftEverythingElseAsItWas()
        {
            Assert.That(session.CheckMachineState(), Is.Empty);
        }
    }
}

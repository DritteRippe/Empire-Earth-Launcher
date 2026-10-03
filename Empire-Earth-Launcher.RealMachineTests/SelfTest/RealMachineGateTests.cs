using System.Collections.Generic;
using Empire_Earth_Launcher.RealMachineTests.Harness;
using NUnit.Framework;

namespace Empire_Earth_Launcher.RealMachineTests.SelfTest
{
    /// <summary>The second lock of the real-machine checks: only the switch 1, on Windows, on a GitHub-hosted runner.</summary>
    [TestFixture]
    [Category(RealMachineCategories.SelfTest)]
    public class RealMachineGateTests
    {
        private static GateDecision Evaluate(string switchValue, string runner, bool isWindows = true)
        {
            var variables = new Dictionary<string, string>
            {
                { RealMachineGate.SwitchVariable, switchValue },
                { RealMachineGate.RunnerEnvironmentVariable, runner }
            };
            return RealMachineGate.Evaluate(name => variables.TryGetValue(name, out string value) ? value : null, isWindows);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("0")]
        [TestCase("true")]
        [TestCase(" 1")]
        public void WithoutTheSwitch_TheChecksAreOff(string switchValue)
        {
            GateDecision decision = Evaluate(switchValue, RealMachineGate.GitHubHostedRunner);

            Assert.That(decision.State, Is.EqualTo(GateState.Disabled));
            Assert.That(decision.Reason, Does.Contain("switched off"));
        }

        [Test]
        public void TheSwitchOutsideWindows_IsRefused()
        {
            GateDecision decision = Evaluate("1", RealMachineGate.GitHubHostedRunner, isWindows: false);

            Assert.That(decision.State, Is.EqualTo(GateState.Refused));
            Assert.That(decision.Reason, Does.Contain("need Windows"));
        }

        [TestCase(null)]
        [TestCase("self-hosted")]
        [TestCase("GitHub-Hosted")]
        public void TheSwitchOutsideAGitHubHostedRunner_IsRefused(string runner)
        {
            GateDecision decision = Evaluate("1", runner);

            Assert.That(decision.State, Is.EqualTo(GateState.Refused));
            Assert.That(decision.Reason, Does.Contain("only on a GitHub-hosted runner"));
        }

        [Test]
        public void TheSwitchOnAGitHubHostedWindowsRunner_OpensTheGate()
        {
            Assert.That(Evaluate("1", "github-hosted").State, Is.EqualTo(GateState.Enabled));
        }
    }
}

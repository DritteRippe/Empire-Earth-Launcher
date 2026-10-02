using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Play;
using Empire_Earth_Launcher.Tests.Fakes;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.Play
{
    /// <summary><see cref="RunningGameDetector"/>: setups and games by their mutexes (contract 0, 4.2), processes by name.</summary>
    [TestFixture]
    public class RunningGameDetectorTests
    {
        [Test]
        public void NothingRuns()
        {
            var detector = new RunningGameDetector(new FakeMutexProbe(), new FakeProcessList());

            Assert.That(detector.FindRunningSetup(), Is.Null);
            Assert.That(detector.IsRunning(Game.EmpireEarth), Is.False);
            Assert.That(detector.IsRunning(Game.ArtOfConquest), Is.False);
            Assert.That(detector.HasProcess(Game.EmpireEarth), Is.False);
        }

        [Test]
        public void Contract_4_2_TheSetups_NeoEEFirst()
        {
            var probe = new FakeMutexProbe().With("EE_Setup", "NeoEE_Setup");

            Assert.That(new RunningGameDetector(probe, new FakeProcessList()).FindRunningSetup(), Is.SameAs(Product.NeoEE));
            probe.Remove("NeoEE_Setup");
            Assert.That(RunningGameDetector.FindRunningSetup(probe), Is.SameAs(Product.EE));
        }

        [TestCase("ee_setup")]
        [TestCase("EmpireEarthCommunityLauncher")]
        [TestCase("StainlessSteelStudiosPresentsEmpireEarth")]
        public void OtherMutexes_AreNoSetup(string name)
        {
            Assert.That(RunningGameDetector.FindRunningSetup(new FakeMutexProbe().With(name)), Is.Null);
        }

        [Test]
        public void Contract_0_TheGames_ByTheirMutexes()
        {
            var detector = new RunningGameDetector(new FakeMutexProbe().With("MadDocSoftwarePresentsEmpireEarthExpansion"),
                new FakeProcessList());

            Assert.That(detector.IsRunning(Game.ArtOfConquest), Is.True);
            Assert.That(detector.IsRunning(Game.EmpireEarth), Is.False);
        }

        [Test]
        public void AProcess_IsFoundByTheProgramName()
        {
            var detector = new RunningGameDetector(new FakeMutexProbe(), new FakeProcessList().With("EE-AOC.exe"));

            Assert.That(detector.HasProcess(Game.ArtOfConquest), Is.True);
            Assert.That(detector.HasProcess(Game.EmpireEarth), Is.False);
        }

        [Test]
        public void Other_IsTheOtherGame()
        {
            Assert.That(RunningGameDetector.Other(Game.EmpireEarth), Is.SameAs(Game.ArtOfConquest));
            Assert.That(RunningGameDetector.Other(Game.ArtOfConquest), Is.SameAs(Game.EmpireEarth));
        }
    }
}

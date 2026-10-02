using System.Linq;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Logging;
using Empire_Earth_Launcher.Core.Play;
using Empire_Earth_Launcher.Tests.Fakes;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.Play
{
    /// <summary>
    /// No change while a setup or a game runs (<see cref="MutationGuard"/>, ADR 0016, contract 4.2). Every action
    /// that writes gets its own blocked tests with the package that adds it.
    /// </summary>
    [TestFixture]
    public class MutationGuardTests
    {
        private RecordingLogger logger;

        [SetUp]
        public void SetUp()
        {
            logger = new RecordingLogger();
        }

        private MutationCheck Check(FakeMutexProbe probe)
        {
            return new MutationGuard(probe, logger).Check("reset the game settings");
        }

        [Test]
        public void NothingRuns_Allowed()
        {
            var probe = new FakeMutexProbe();

            MutationCheck check = Check(probe);

            Assert.That(check.IsAllowed, Is.True);
            Assert.That(check.Block, Is.EqualTo(MutationBlock.None));
            Assert.That(check, Is.SameAs(MutationCheck.Allowed));
            Assert.That(logger.Messages, Is.Empty);
        }

        [Test]
        public void NothingRuns_EveryContractMutexIsProbed()
        {
            var probe = new FakeMutexProbe();

            Check(probe);

            Assert.That(probe.Probed, Is.EqualTo(new[]
            {
                "NeoEE_Setup", "EE_Setup",
                "StainlessSteelStudiosPresentsEmpireEarth", "MadDocSoftwarePresentsEmpireEarthExpansion"
            }));
        }

        [TestCase("NeoEE_Setup", "NeoEE")]
        [TestCase("EE_Setup", "EE")]
        public void SetupRuns_BlockedSetupRunning(string mutex, string product)
        {
            MutationCheck check = Check(new FakeMutexProbe().With(mutex));

            Assert.That(check.IsAllowed, Is.False);
            Assert.That(check.Block, Is.EqualTo(MutationBlock.SetupRunning));
            Assert.That(check.Setup, Is.SameAs(Product.FromId(product)));
            Assert.That(check.Game, Is.Null);
            Assert.That(check.MutexName, Is.EqualTo(mutex));
            Assert.That(check.ToString(), Is.EqualTo("Blocked(SetupRunning, " + mutex + ")"));
            Assert.That(logger.MessagesOf(LogLevel.Info).Single(),
                Is.EqualTo("Not allowed to reset the game settings now: the " + product + " setup is running (mutex " + mutex + ")."));
        }

        [TestCase("StainlessSteelStudiosPresentsEmpireEarth", "EE")]
        [TestCase("MadDocSoftwarePresentsEmpireEarthExpansion", "AoC")]
        public void GameRuns_BlockedGameRunning(string mutex, string game)
        {
            MutationCheck check = Check(new FakeMutexProbe().With(mutex));

            Assert.That(check.IsAllowed, Is.False);
            Assert.That(check.Block, Is.EqualTo(MutationBlock.GameRunning));
            Assert.That(check.Game, Is.SameAs(Game.All.Single(g => g.Id == game)));
            Assert.That(check.Setup, Is.Null);
            Assert.That(check.MutexName, Is.EqualTo(mutex));
            Assert.That(logger.MessagesOf(LogLevel.Info).Single(), Does.Contain(check.Game.ProgramName + " is running"));
        }

        [Test]
        public void SetupAndGameRun_TheSetupIsReported()
        {
            MutationCheck check = Check(new FakeMutexProbe().With("StainlessSteelStudiosPresentsEmpireEarth", "EE_Setup"));

            Assert.That(check.Block, Is.EqualTo(MutationBlock.SetupRunning));
            Assert.That(check.Setup, Is.SameAs(Product.EE));
        }

        [Test]
        public void SetupEnded_AllowedAgain()
        {
            var probe = new FakeMutexProbe().With("NeoEE_Setup");
            var guard = new MutationGuard(probe, logger);
            Assert.That(guard.Check("apply the defaults").Block, Is.EqualTo(MutationBlock.SetupRunning));

            probe.Remove("NeoEE_Setup");

            Assert.That(guard.Check("apply the defaults").IsAllowed, Is.True);
        }

        [Test]
        public void OtherNames_DoNotBlock()
        {
            // Mutex names are case-sensitive on Windows; the launcher's own single-instance mutex is not a setup.
            MutationCheck check = Check(new FakeMutexProbe().With("neoee_setup", "EmpireEarthCommunityLauncher", @"Global\Other"));

            Assert.That(check.IsAllowed, Is.True);
        }

        [Test]
        public void Action_IsRequiredForTheLog()
        {
            Assert.That(() => new MutationGuard(new FakeMutexProbe(), logger).Check(""), Throws.ArgumentException);
        }
    }
}

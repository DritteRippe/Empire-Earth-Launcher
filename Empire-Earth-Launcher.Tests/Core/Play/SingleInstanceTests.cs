using System;
using Empire_Earth_Launcher.Core.Play;
using Empire_Earth_Launcher.Tests.Fakes;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.Play
{
    /// <summary><see cref="SingleInstance"/> with <see cref="FakeMutexProbe"/> (ADR 0010, contract O10).</summary>
    [TestFixture]
    public class SingleInstanceTests
    {
        [Test]
        public void TheMutexName_IsReservedForTheSetup()
        {
            Assert.That(SingleInstance.MutexName, Is.EqualTo("EmpireEarthCommunityLauncher"));
        }

        [Test]
        public void TheFirstLauncher_HoldsTheMutex_AndTheSecondEnds()
        {
            var mutexes = new FakeMutexProbe();
            var logger = new RecordingLogger();

            using (IDisposable first = SingleInstance.TryClaim(mutexes, logger))
            {
                Assert.That(first, Is.Not.Null);
                Assert.That(mutexes.Exists("EmpireEarthCommunityLauncher"), Is.True, "a setup's AppMutex would see it");
                Assert.That(logger.Entries, Is.Empty);

                Assert.That(SingleInstance.TryClaim(mutexes, logger), Is.Null);
                Assert.That(logger.Messages, Is.EqualTo(new[]
                {
                    "Info: Another Empire Earth Launcher is already running (mutex EmpireEarthCommunityLauncher); this one ends."
                }));
            }

            Assert.That(mutexes.Exists("EmpireEarthCommunityLauncher"), Is.False, "the mutex ends with the first launcher");
            using (IDisposable again = SingleInstance.TryClaim(mutexes, logger))
                Assert.That(again, Is.Not.Null);
        }

        [Test]
        public void TheLauncherMutex_BlocksNeitherPlayNorAChange()
        {
            var mutexes = new FakeMutexProbe();
            using (SingleInstance.TryClaim(mutexes, new RecordingLogger()))
            {
                Assert.That(new MutationGuard(mutexes, new RecordingLogger()).Check("test").IsAllowed, Is.True);
                Assert.That(RunningGameDetector.FindRunningSetup(mutexes), Is.Null);
            }
        }

        [Test]
        public void Arguments_AreChecked()
        {
            Assert.That(() => SingleInstance.TryClaim(null, new RecordingLogger()), Throws.ArgumentNullException);
            Assert.That(() => SingleInstance.TryClaim(new FakeMutexProbe(), null), Throws.ArgumentNullException);
        }
    }
}

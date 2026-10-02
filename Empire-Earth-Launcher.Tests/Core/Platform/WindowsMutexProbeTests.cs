using System;
using System.Threading;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Tests.Fakes;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.Platform
{
    /// <summary>
    /// The real mutex probe (<see cref="WindowsMutexProbe"/>) with mutexes this test creates under random names (never
    /// the names of the setups or games). Named mutexes work within one process under Mono as well; the cases with
    /// another account or an elevated setup are in the test plan.
    /// </summary>
    [TestFixture]
    public class WindowsMutexProbeTests
    {
        private static string RandomName()
        {
            return "EmpireEarthLauncherTests-" + Guid.NewGuid().ToString("N");
        }

        [Test]
        public void ExistingMutex_IsFound_AndNotAcquired()
        {
            string name = RandomName();
            var probe = new WindowsMutexProbe(new RecordingLogger());

            using (var mutex = new Mutex(true, name))
            {
                Assert.That(probe.Exists(name), Is.True);
                Assert.That(probe.Exists(name), Is.True, "probing twice works: the probe holds nothing");
                mutex.ReleaseMutex();
            }
        }

        [Test]
        public void MissingMutex_IsNotFound()
        {
            Assert.That(new WindowsMutexProbe(new RecordingLogger()).Exists(RandomName()), Is.False);
        }

        [Test]
        public void GlobalMutex_IsFoundByItsName()
        {
            string name = RandomName();

            using (new Mutex(false, WindowsMutexProbe.GlobalPrefix + name))
            {
                Assert.That(new WindowsMutexProbe(new RecordingLogger()).Exists(name), Is.True);
            }
        }

        [Test]
        public void ClosedMutex_IsGone()
        {
            string name = RandomName();
            var probe = new WindowsMutexProbe(new RecordingLogger());
            new Mutex(false, name).Dispose();

            Assert.That(probe.Exists(name), Is.False);
        }

        [Test]
        public void EmptyName_IsAProgrammingError()
        {
            Assert.That(() => new WindowsMutexProbe(new RecordingLogger()).Exists(""), Throws.ArgumentException);
        }
    }
}

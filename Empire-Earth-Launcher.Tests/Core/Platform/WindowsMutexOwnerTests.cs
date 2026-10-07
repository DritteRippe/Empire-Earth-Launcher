using System;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Tests.Fakes;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.Platform
{
    /// <summary>
    /// <see cref="WindowsMutexOwner"/> with real named mutexes of random names (also under Mono): created once, refused while
    /// it exists, free again after dispose, and seen by <see cref="WindowsMutexProbe"/>.
    /// </summary>
    [TestFixture]
    public class WindowsMutexOwnerTests
    {
        private static string RandomName()
        {
            return "EmpireEarthLauncherTests-" + Guid.NewGuid().ToString("N");
        }

        [Test]
        public void AMutex_IsCreatedOnce_AndFreeAfterDispose()
        {
            var logger = new RecordingLogger();
            var owner = new WindowsMutexOwner(logger);
            string name = RandomName();

            using (IDisposable first = owner.TryCreate(name))
            {
                Assert.That(first, Is.Not.Null);
                Assert.That(new WindowsMutexProbe(logger).Exists(name), Is.True);
                Assert.That(owner.TryCreate(name), Is.Null, "a second launcher");
            }

            Assert.That(new WindowsMutexProbe(logger).Exists(name), Is.False);
            using (IDisposable again = owner.TryCreate(name))
                Assert.That(again, Is.Not.Null);
            Assert.That(logger.Entries, Is.Empty);
        }

        [Test]
        public void AnEmptyName_IsAProgrammingError()
        {
            Assert.That(() => new WindowsMutexOwner(new RecordingLogger()).TryCreate(""), Throws.ArgumentException);
        }
    }
}

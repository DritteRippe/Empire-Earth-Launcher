using System;
using System.Threading;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Tests.Fakes;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.Platform
{
    /// <summary>
    /// <see cref="IClock.Elapsed"/>, the source of durations (the quiet time and the deadline of the activation signal): it only
    /// goes forward, and setting the wall clock does not move it, in the real clock and in the fake one that the tests use.
    /// </summary>
    [TestFixture]
    public class ClockTests
    {
        [Test]
        public void TheElapsedTimeOfTheSystemClock_NeverGoesBackwards_AndGrowsWithRealTime()
        {
            IClock clock = SystemClock.Instance;
            TimeSpan first = clock.Elapsed;

            Thread.Sleep(30);
            TimeSpan second = clock.Elapsed;

            Assert.That(second, Is.GreaterThanOrEqualTo(first + TimeSpan.FromMilliseconds(20)));
            Assert.That(clock.Elapsed, Is.GreaterThanOrEqualTo(second));
        }

        [Test]
        public void Advance_MovesTheWallClockAndTheElapsedTime()
        {
            var clock = new FakeClock();
            DateTime utc = clock.UtcNow;
            TimeSpan elapsed = clock.Elapsed;

            clock.Advance(TimeSpan.FromSeconds(7));

            Assert.That(clock.UtcNow - utc, Is.EqualTo(TimeSpan.FromSeconds(7)));
            Assert.That(clock.Elapsed - elapsed, Is.EqualTo(TimeSpan.FromSeconds(7)));
        }

        [Test]
        public void SettingTheWallClock_LeavesTheElapsedTimeWhereItIs()
        {
            var clock = new FakeClock();
            DateTime utc = clock.UtcNow;
            TimeSpan elapsed = clock.Elapsed;

            clock.SetWallClock(TimeSpan.FromHours(1));
            clock.SetWallClock(TimeSpan.FromHours(-3));

            Assert.That(clock.UtcNow - utc, Is.EqualTo(TimeSpan.FromHours(-2)));
            Assert.That(clock.Elapsed, Is.EqualTo(elapsed));
        }
    }
}

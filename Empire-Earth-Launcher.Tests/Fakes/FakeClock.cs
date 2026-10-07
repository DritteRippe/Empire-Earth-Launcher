using System;
using Empire_Earth_Launcher.Core.Platform;

namespace Empire_Earth_Launcher.Tests.Fakes
{
    /// <summary>
    /// <see cref="IClock"/> that stands still until a test moves it. Local time and UTC differ by a fixed offset
    /// (two hours, like Central European Summer Time), so that a mix-up of the two is visible in a test.
    /// </summary>
    internal sealed class FakeClock : IClock
    {
        /// <summary>Offset of the fake local time to UTC.</summary>
        public static readonly TimeSpan LocalOffset = TimeSpan.FromHours(2);

        public FakeClock()
            : this(new DateTime(2026, 10, 2, 18, 4, 31, DateTimeKind.Utc))
        {
        }

        public FakeClock(DateTime utcNow)
        {
            if (utcNow.Kind != DateTimeKind.Utc)
                throw new ArgumentException("UTC time expected.", nameof(utcNow));
            UtcNow = utcNow;
        }

        public DateTime UtcNow { get; private set; }

        public DateTime Now
        {
            get { return DateTime.SpecifyKind(UtcNow + LocalOffset, DateTimeKind.Local); }
        }

        /// <summary>
        /// <see cref="IClock.Elapsed"/>: moves with <see cref="Advance"/> (time that passes) and stays where it is for
        /// <see cref="SetWallClock"/> (the clock of the computer is set).
        /// </summary>
        public TimeSpan Elapsed { get; private set; }

        /// <summary>Lets time pass: the wall clock and <see cref="Elapsed"/> move forward (or the wall clock back, with a negative value).</summary>
        public void Advance(TimeSpan time)
        {
            UtcNow += time;
            Elapsed += time;
        }

        /// <summary>Sets the wall clock of the computer by <paramref name="time"/> (a time server, daylight saving); no time passes: <see cref="Elapsed"/> stays.</summary>
        public void SetWallClock(TimeSpan time)
        {
            UtcNow += time;
        }
    }
}

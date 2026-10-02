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

        /// <summary>Moves the clock forward (or back, with a negative value).</summary>
        public void Advance(TimeSpan time)
        {
            UtcNow += time;
        }
    }
}

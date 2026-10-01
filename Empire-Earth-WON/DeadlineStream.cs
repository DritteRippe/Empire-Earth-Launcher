using System;
using System.IO;

namespace Empire_Earth_WON
{
    /// <summary>
    /// Gives a whole request/reply exchange one time budget. A read or write timeout alone only limits each
    /// call: a server (or anyone on the plain-TCP path) sending a 64 KiB reply one byte just before every
    /// timeout could block a request for days. Before each call the remaining time becomes the timeout of
    /// the inner stream, and once it is used up a <see cref="TimeoutException"/> is thrown.
    /// </summary>
    internal sealed class DeadlineStream : Stream
    {
        private readonly Stream inner;
        private readonly int budgetMilliseconds;
        private readonly Func<int> clock;
        private readonly int start;

        /// <param name="inner">Stream of the exchange; not closed by this wrapper.</param>
        /// <param name="budgetMilliseconds">Time allowed for all reads and writes together.</param>
        /// <param name="clock">Milliseconds clock, for tests; <see cref="Environment.TickCount"/> by default.</param>
        public DeadlineStream(Stream inner, int budgetMilliseconds, Func<int> clock = null)
        {
            if (inner == null)
                throw new ArgumentNullException(nameof(inner));
            if (budgetMilliseconds <= 0)
                throw new ArgumentOutOfRangeException(nameof(budgetMilliseconds));
            this.inner = inner;
            this.budgetMilliseconds = budgetMilliseconds;
            this.clock = clock ?? (() => Environment.TickCount);
            start = this.clock();
        }

        /// <summary>Remaining time; throws when the budget is used up.</summary>
        private int RemainingMilliseconds()
        {
            // Unchecked: Environment.TickCount wraps around after 24.9 days, the difference stays right.
            long elapsed = unchecked(clock() - start);
            long remaining = budgetMilliseconds - elapsed;
            if (remaining <= 0)
                throw new TimeoutException("The Neo server did not complete its reply within " + budgetMilliseconds + " ms.");
            return (int)remaining;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int remaining = RemainingMilliseconds();
            if (inner.CanTimeout)
                inner.ReadTimeout = remaining;
            return inner.Read(buffer, offset, count);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            int remaining = RemainingMilliseconds();
            if (inner.CanTimeout)
                inner.WriteTimeout = remaining;
            inner.Write(buffer, offset, count);
        }

        public override void Flush()
        {
            inner.Flush();
        }

        public override bool CanRead
        {
            get { return inner.CanRead; }
        }

        public override bool CanWrite
        {
            get { return inner.CanWrite; }
        }

        public override bool CanSeek
        {
            get { return false; }
        }

        public override long Length
        {
            get { throw new NotSupportedException(); }
        }

        public override long Position
        {
            get { throw new NotSupportedException(); }
            set { throw new NotSupportedException(); }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }
    }
}

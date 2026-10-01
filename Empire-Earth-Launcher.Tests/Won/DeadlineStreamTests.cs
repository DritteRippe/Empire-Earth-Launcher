using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Empire_Earth_WON;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Won
{
    /// <summary>
    /// One time budget for a whole Neo exchange (<see cref="DeadlineStream"/>). Before, the timeout only
    /// limited each read, so a server sending one byte just before every timeout could block a request for
    /// hours. The clock is simulated: nothing waits and no connection is opened.
    /// </summary>
    [TestFixture]
    public class DeadlineStreamTests
    {
        private static byte[] ReplyPacket(string text)
        {
            byte[] payload = Encoding.UTF8.GetBytes(text);
            int size = payload.Length + NeoApiClient.HeaderSize;
            return new[] { (byte)(size & 0xFF), (byte)(size >> 8) }.Concat(payload).ToArray();
        }

        [Test]
        public void TrickledReply_TimesOutWhenTheBudgetIsUsedUp()
        {
            // 1 byte every 50 ms, well below the per-read timeout of 1000 ms: 102 reads would take 5.1 s.
            var clock = new FakeClock();
            var server = new TricklingStream(ReplyPacket(new string('x', 100)), clock, 50);
            var stream = new DeadlineStream(server, 1000, clock.Now);

            Assert.That(() => NeoApiClient.ReadReply(stream, ' '), Throws.TypeOf<TimeoutException>());
            Assert.That(server.ReadTimeouts.Count, Is.EqualTo(20), "reads within the budget");
        }

        [Test]
        public void EveryRead_GetsTheRemainingTimeAsTimeout()
        {
            var clock = new FakeClock();
            var server = new TricklingStream(ReplyPacket("a b"), clock, 100);
            var stream = new DeadlineStream(server, 1000, clock.Now);

            Assert.That(NeoApiClient.ReadReply(stream, ' '), Is.EqualTo(new[] { "a", "b" }));
            Assert.That(server.ReadTimeouts, Is.EqualTo(new[] { 1000, 900, 800, 700, 600 }));
        }

        [Test]
        public void Write_GetsTheRemainingTimeAsTimeout()
        {
            var clock = new FakeClock();
            var server = new TricklingStream(new byte[0], clock, 0);
            var stream = new DeadlineStream(server, 1000, clock.Now);
            clock.Advance(300);

            NeoApiClient.WriteRequest(stream, NeoApiClient.RequestType.Info);

            Assert.That(server.WriteTimeouts, Is.EqualTo(new[] { 700 }));
            Assert.That(server.Written, Is.EqualTo(new byte[] { 3, 0, 7 }));
        }

        [Test]
        public void ClockWrapAround_IsHandled()
        {
            // Environment.TickCount wraps from int.MaxValue to int.MinValue after 24.9 days of uptime.
            var clock = new FakeClock(int.MaxValue - 150);
            var server = new TricklingStream(ReplyPacket("ok"), clock, 100);
            var stream = new DeadlineStream(server, 1000, clock.Now);

            Assert.That(NeoApiClient.ReadReply(stream, ' '), Is.EqualTo(new[] { "ok" }));
            Assert.That(server.ReadTimeouts, Is.EqualTo(new[] { 1000, 900, 800, 700 }));
        }

        [Test]
        public void InvalidArguments_Throw()
        {
            Assert.That(() => new DeadlineStream(null, 1000), Throws.TypeOf<ArgumentNullException>());
            Assert.That(() => new DeadlineStream(new MemoryStream(), 0), Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        private sealed class FakeClock
        {
            private int now;

            public FakeClock(int start = 0)
            {
                now = start;
            }

            public int Now()
            {
                return now;
            }

            public void Advance(int milliseconds)
            {
                now = unchecked(now + milliseconds);
            }
        }

        /// <summary>
        /// Returns one byte per read and lets <paramref name="millisecondsPerRead"/> pass on the fake clock for
        /// each, like a server that trickles its reply. Records the timeouts set by the caller.
        /// </summary>
        private sealed class TricklingStream : Stream
        {
            private readonly byte[] data;
            private readonly FakeClock clock;
            private readonly int millisecondsPerRead;
            private readonly MemoryStream written = new MemoryStream();
            private int position;

            public TricklingStream(byte[] data, FakeClock clock, int millisecondsPerRead)
            {
                this.data = data;
                this.clock = clock;
                this.millisecondsPerRead = millisecondsPerRead;
            }

            public List<int> ReadTimeouts { get; } = new List<int>();

            public List<int> WriteTimeouts { get; } = new List<int>();

            public byte[] Written
            {
                get { return written.ToArray(); }
            }

            public override bool CanTimeout
            {
                get { return true; }
            }

            public override int ReadTimeout
            {
                get { return ReadTimeouts.LastOrDefault(); }
                set { ReadTimeouts.Add(value); }
            }

            public override int WriteTimeout
            {
                get { return WriteTimeouts.LastOrDefault(); }
                set { WriteTimeouts.Add(value); }
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                clock.Advance(millisecondsPerRead);
                if (position >= data.Length || count == 0)
                    return 0;
                buffer[offset] = data[position++];
                return 1;
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                written.Write(buffer, offset, count);
            }

            public override void Flush()
            {
            }

            public override bool CanRead
            {
                get { return true; }
            }

            public override bool CanWrite
            {
                get { return true; }
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
}

using System;
using System.IO;

namespace Empire_Earth_Launcher.Tests.TestSupport
{
    /// <summary>
    /// Read-only, non-seekable stream over a byte array that returns at most <see cref="MaxBytesPerRead"/>
    /// bytes per <see cref="Read"/>, like a network stream that receives a reply in several TCP segments.
    /// </summary>
    internal sealed class ChunkedReadStream : Stream
    {
        private readonly byte[] data;
        private int position;

        public ChunkedReadStream(byte[] data, int maxBytesPerRead)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            if (maxBytesPerRead < 1)
                throw new ArgumentOutOfRangeException(nameof(maxBytesPerRead));
            this.data = data;
            MaxBytesPerRead = maxBytesPerRead;
        }

        public int MaxBytesPerRead { get; }

        /// <summary>Number of <see cref="Read"/> calls so far.</summary>
        public int ReadCalls { get; private set; }

        /// <summary>Number of bytes not read yet.</summary>
        public int RemainingBytes
        {
            get { return data.Length - position; }
        }

        public override bool CanRead
        {
            get { return true; }
        }

        public override bool CanSeek
        {
            get { return false; }
        }

        public override bool CanWrite
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

        public override int Read(byte[] buffer, int offset, int count)
        {
            ReadCalls++;
            int read = Math.Min(Math.Min(count, MaxBytesPerRead), RemainingBytes);
            Array.Copy(data, position, buffer, offset, read);
            position += read;
            return read;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }
    }
}

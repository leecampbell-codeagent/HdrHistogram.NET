using System;
using System.IO;
using FluentAssertions;
using HdrHistogram.Utilities;
using Xunit;

namespace HdrHistogram.UnitTests.Utilities
{
    public class ByteBufferTests
    {
        /// <summary>
        /// Reproduces Issue #99.
        /// <see cref="ByteBuffer.ReadFrom"/> must loop until all requested bytes
        /// have been read, because <see cref="Stream.Read"/> is permitted to
        /// return fewer bytes than requested — even when more data is available.
        /// <see cref="System.IO.Compression.DeflateStream"/> does exactly this
        /// at DEFLATE block boundaries.
        /// </summary>
        [Theory]
        [InlineData(1024, 100)]
        [InlineData(4096, 511)]
        [InlineData(8192, 1000)]
        public void ReadFrom_returns_all_bytes_when_stream_returns_partial_reads(
            int totalBytes, int maxBytesPerRead)
        {
            var data = new byte[totalBytes];
            new Random(42).NextBytes(data);
            var stream = new PartialReadStream(new MemoryStream(data), maxBytesPerRead);
            var buffer = ByteBuffer.Allocate(totalBytes);

            int bytesRead = buffer.ReadFrom(stream, totalBytes);

            Assert.Equal(totalBytes, bytesRead);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(-1)]
        [InlineData(int.MaxValue)]
        [InlineData(int.MinValue)]
        public void PutInt_and_GetInt_round_trip_returns_original_value(int expected)
        {
            var buffer = ByteBuffer.Allocate(4);
            buffer.PutInt(expected);
            buffer.Position = 0;
            var result = buffer.GetInt();
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(-1)]
        [InlineData(int.MaxValue)]
        [InlineData(int.MinValue)]
        public void PutInt_at_index_and_GetInt_round_trip_returns_original_value(int expected)
        {
            var buffer = ByteBuffer.Allocate(8);
            buffer.PutInt(0, expected);
            buffer.Position.Should().Be(0);
            var result = buffer.GetInt();
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData(0L)]
        [InlineData(1L)]
        [InlineData(-1L)]
        [InlineData(long.MaxValue)]
        [InlineData(long.MinValue)]
        public void PutLong_and_GetLong_round_trip_returns_original_value(long expected)
        {
            var buffer = ByteBuffer.Allocate(8);
            buffer.PutLong(expected);
            buffer.Position = 0;
            var result = buffer.GetLong();
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(1.0)]
        [InlineData(-1.0)]
        [InlineData(double.MaxValue)]
        [InlineData(double.Epsilon)]
        public void PutDouble_and_GetDouble_round_trip_returns_original_value(double expected)
        {
            var buffer = ByteBuffer.Allocate(8);
            buffer.PutDouble(expected);
            buffer.Position = 0;
            var result = buffer.GetDouble();
            result.Should().Be(expected);
        }

        /// <summary>
        /// A stream wrapper that returns at most <c>maxBytesPerRead</c> bytes
        /// per <see cref="Read"/> call, simulating the behaviour of
        /// <see cref="System.IO.Compression.DeflateStream"/> at compression
        /// block boundaries.
        /// </summary>
        private sealed class PartialReadStream : Stream
        {
            private readonly Stream _inner;
            private readonly int _maxBytesPerRead;

            public PartialReadStream(Stream inner, int maxBytesPerRead)
            {
                _inner = inner;
                _maxBytesPerRead = maxBytesPerRead;
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                return _inner.Read(buffer, offset, Math.Min(count, _maxBytesPerRead));
            }

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }
            public override void Flush() { }
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }
    }
}

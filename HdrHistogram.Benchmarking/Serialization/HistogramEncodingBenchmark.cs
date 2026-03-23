using BenchmarkDotNet.Attributes;
using HdrHistogram.Encoding;
using HdrHistogram.Utilities;

namespace HdrHistogram.Benchmarking.Serialization
{
    [MemoryDiagnoser]
    public class HistogramEncodingBenchmark
    {
        private LongHistogram _histogram;
        private ByteBuffer _encodeTargetBuffer;
        private ByteBuffer _decodeSourceBuffer;
        private ByteBuffer _encodeCompressedTargetBuffer;
        private ByteBuffer _decodeCompressedSourceBuffer;

        [GlobalSetup]
        public void Setup()
        {
            const long highestTrackableValue = 3600L * 1000 * 1000 * 1000;
            _histogram = new LongHistogram(highestTrackableValue, 3);
            for (long i = 0; i < 10000L; i++)
            {
                _histogram.RecordValue(1000L * i);
            }

            int capacity = _histogram.GetNeededByteBufferCapacity();
            _encodeTargetBuffer = ByteBuffer.Allocate(capacity);

            _decodeSourceBuffer = ByteBuffer.Allocate(capacity);
            _histogram.Encode(_decodeSourceBuffer, HistogramEncoderV2.Instance);

            _encodeCompressedTargetBuffer = ByteBuffer.Allocate(capacity + 64);
            _decodeCompressedSourceBuffer = ByteBuffer.Allocate(capacity + 64);
            _histogram.EncodeIntoCompressedByteBuffer(_decodeCompressedSourceBuffer);
        }

        [Benchmark]
        public void Encode()
        {
            _encodeTargetBuffer.Position = 0;
            _histogram.Encode(_encodeTargetBuffer, HistogramEncoderV2.Instance);
        }

        [Benchmark]
        public HistogramBase Decode()
        {
            _decodeSourceBuffer.Position = 0;
            return HistogramEncoding.DecodeFromByteBuffer(_decodeSourceBuffer, 0);
        }

        [Benchmark]
        public void EncodeCompressed()
        {
            _encodeCompressedTargetBuffer.Position = 0;
            _histogram.EncodeIntoCompressedByteBuffer(_encodeCompressedTargetBuffer);
        }

        [Benchmark]
        public HistogramBase DecodeCompressed()
        {
            _decodeCompressedSourceBuffer.Position = 0;
            return HistogramEncoding.DecodeFromCompressedByteBuffer(_decodeCompressedSourceBuffer, 0);
        }
    }
}

using BenchmarkDotNet.Attributes;
using HdrHistogram.Utilities;

namespace HdrHistogram.Benchmarking.Serialization
{
    [MemoryDiagnoser]
    public class ByteBufferBenchmark
    {
        private ByteBuffer _buffer;

        [GlobalSetup]
        public void Setup()
        {
            _buffer = ByteBuffer.Allocate(8);
            _buffer.PutLong(long.MaxValue);
        }

        [Benchmark]
        public void PutInt()
        {
            _buffer.Position = 0;
            _buffer.PutInt(42);
        }

        [Benchmark]
        public int GetInt()
        {
            _buffer.Position = 0;
            return _buffer.GetInt();
        }

        [Benchmark]
        public void PutLong()
        {
            _buffer.Position = 0;
            _buffer.PutLong(42L);
        }

        [Benchmark]
        public long GetLong()
        {
            _buffer.Position = 0;
            return _buffer.GetLong();
        }

        [Benchmark]
        public void PutDouble()
        {
            _buffer.Position = 0;
            _buffer.PutDouble(42.0);
        }

        [Benchmark]
        public double GetDouble()
        {
            _buffer.Position = 0;
            return _buffer.GetDouble();
        }
    }
}

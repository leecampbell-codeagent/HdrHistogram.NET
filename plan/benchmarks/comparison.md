# Benchmark Comparison: Baseline vs Post-Change (Issue #141)

This document compares benchmark results before and after refactoring `ByteBuffer` to use `BinaryPrimitives` for Issue #141 (ByteBuffer: reduce allocations for serial read/write).
All measurements were captured on 2026-03-23 using BenchmarkDotNet v0.15.8 on an Intel Core i5-14400 running Linux Ubuntu 24.04.4 LTS.
Runtimes tested: .NET 8.0, .NET 9.0, .NET 10.0.
A negative Mean Delta % indicates the post-change result is faster than baseline.

## ByteBufferBenchmark

| Benchmark | Runtime | Baseline Mean | Post-Change Mean | Mean Delta | Mean Delta % | Baseline Allocated | Post-Change Allocated |
|-----------|---------|---------------|------------------|------------|--------------|--------------------|-----------------------|
| PutInt    | .NET 10.0 | 14.916 ns | 5.582 ns | -9.334 ns | -62.6% | 32 B | 0 B |
| GetInt    | .NET 10.0 | 2.316 ns  | 2.317 ns | +0.001 ns | +0.0% | 0 B  | 0 B |
| PutLong   | .NET 10.0 | 14.751 ns | 5.715 ns | -9.036 ns | -61.3% | 32 B | 0 B |
| GetLong   | .NET 10.0 | 2.344 ns  | 2.272 ns | -0.072 ns | -3.1%  | 0 B  | 0 B |
| PutDouble | .NET 10.0 | 15.335 ns | 5.808 ns | -9.527 ns | -62.1% | 32 B | 0 B |
| GetDouble | .NET 10.0 | 4.066 ns  | 2.288 ns | -1.778 ns | -43.7% | 0 B  | 0 B |
| PutInt    | .NET 8.0  | 16.733 ns | 5.647 ns | -11.086 ns | -66.3% | 32 B | 0 B |
| GetInt    | .NET 8.0  | 3.707 ns  | 2.575 ns | -1.132 ns  | -30.5% | 0 B  | 0 B |
| PutLong   | .NET 8.0  | 16.183 ns | 6.015 ns | -10.168 ns | -62.8% | 32 B | 0 B |
| GetLong   | .NET 8.0  | 3.770 ns  | 2.748 ns | -1.022 ns  | -27.1% | 0 B  | 0 B |
| PutDouble | .NET 8.0  | 16.320 ns | 5.612 ns | -10.708 ns | -65.6% | 32 B | 0 B |
| GetDouble | .NET 8.0  | 5.567 ns  | 2.553 ns | -3.014 ns  | -54.1% | 0 B  | 0 B |
| PutInt    | .NET 9.0  | 15.703 ns | 6.160 ns | -9.543 ns  | -60.8% | 32 B | 0 B |
| GetInt    | .NET 9.0  | 3.678 ns  | 2.613 ns | -1.065 ns  | -29.0% | 0 B  | 0 B |
| PutLong   | .NET 9.0  | 15.332 ns | 5.726 ns | -9.606 ns  | -62.7% | 32 B | 0 B |
| GetLong   | .NET 9.0  | 4.192 ns  | 2.735 ns | -1.457 ns  | -34.8% | 0 B  | 0 B |
| PutDouble | .NET 9.0  | 15.889 ns | 5.843 ns | -10.046 ns | -63.2% | 32 B | 0 B |
| GetDouble | .NET 9.0  | 4.461 ns  | 2.713 ns | -1.748 ns  | -39.2% | 0 B  | 0 B |

## HistogramEncodingBenchmark

| Benchmark        | Runtime | Baseline Mean | Post-Change Mean | Mean Delta | Mean Delta % | Baseline Allocated | Post-Change Allocated |
|------------------|---------|---------------|------------------|------------|--------------|--------------------|-----------------------|
| Encode           | .NET 10.0 | 138.98 μs | 139.43 μs | +0.45 μs  | +0.3%  | 114.35 KB | 114.15 KB |
| Decode           | .NET 10.0 | 49.74 μs  | 52.38 μs  | +2.64 μs  | +5.3%  | 265.16 KB | 265.16 KB |
| EncodeCompressed | .NET 10.0 | 203.47 μs | 200.64 μs | -2.83 μs  | -1.4%  | 644.62 KB | 644.29 KB |
| DecodeCompressed | .NET 10.0 | 87.73 μs  | 87.46 μs  | -0.27 μs  | -0.3%  | 534.98 KB | 534.98 KB |
| Encode           | .NET 8.0  | 137.98 μs | 138.44 μs | +0.46 μs  | +0.3%  | 114.41 KB | 114.19 KB |
| Decode           | .NET 8.0  | 50.97 μs  | 51.42 μs  | +0.45 μs  | +0.9%  | 265.29 KB | 265.28 KB |
| EncodeCompressed | .NET 8.0  | 211.12 μs | 213.56 μs | +2.44 μs  | +1.2%  | 644.62 KB | 644.24 KB |
| DecodeCompressed | .NET 8.0  | 92.05 μs  | 93.26 μs  | +1.21 μs  | +1.3%  | 535.10 KB | 535.09 KB |
| Encode           | .NET 9.0  | 135.49 μs | 137.84 μs | +2.35 μs  | +1.7%  | 114.40 KB | 114.18 KB |
| Decode           | .NET 9.0  | 53.37 μs  | 53.43 μs  | +0.06 μs  | +0.1%  | 265.25 KB | 265.25 KB |
| EncodeCompressed | .NET 9.0  | 209.48 μs | 207.26 μs | -2.22 μs  | -1.1%  | 644.52 KB | 644.19 KB |
| DecodeCompressed | .NET 9.0  | 94.12 μs  | 95.70 μs  | +1.58 μs  | +1.7%  | 535.05 KB | 535.04 KB |

## Summary

### ByteBufferBenchmark

**Allocations — eliminated entirely:**
All six `Put*` operations (`PutInt`, `PutLong`, `PutDouble`) across all three runtimes previously allocated 32 B per call.
Post-change, all allocations are gone (0 B).

**Mean latency — improved across the board:**

- All `Put*` operations are 61–66% faster across all runtimes.
  The per-call cost dropped from the 14–17 ns range down to the 5–6 ns range.
- All `Get*` operations are also faster or essentially unchanged.
  `GetDouble` shows the largest improvement at 39–54% faster.
  `GetInt` on .NET 10.0 is the only result effectively unchanged (+0.001 ns, within noise).

**No regressions detected** in ByteBufferBenchmark.

### HistogramEncodingBenchmark

**Allocations — marginal improvements:**
Encode and EncodeCompressed operations show small reductions of 0.20–0.38 KB per call across all runtimes.
Decode and DecodeCompressed allocations are virtually unchanged (≤0.01 KB difference).

**Mean latency — mixed, all within noise margins:**

- Most differences are under 2 μs on operations taking 50–210 μs — well within run-to-run variation.
- The largest observed difference is `Decode` on .NET 10.0 (+2.64 μs, +5.3%), which is likely noise; the same operation on .NET 8.0 and .NET 9.0 shows only +0.45 μs (+0.9%) and +0.06 μs (+0.1%) respectively, with no consistent directional trend across runtimes.
- No consistent regression pattern exists across runtimes for any single method.

**No meaningful regressions detected** in HistogramEncodingBenchmark.

## Verdict

The data strongly supports the change.

The primary goal of Issue #141 was to eliminate the 32 B per-call heap allocations on `ByteBuffer` write operations.
That goal is fully achieved: all six `Put*` variants across all three runtimes now allocate 0 B.

As a secondary benefit, all `ByteBuffer` operations are substantially faster — write operations by roughly 61–66% and read operations by up to 54%.
This is consistent with the removal of object allocation overhead and the use of `BinaryPrimitives`, which the JIT can optimise to direct register-width memory operations.

The higher-level `HistogramEncodingBenchmark` results show no meaningful change in throughput or allocations, which is the expected outcome: the encoding path calls `ByteBuffer` heavily, but the dominant cost at that level is GZip compression and array manipulation, not individual `ByteBuffer` writes.
The marginal allocation reductions (~0.20–0.38 KB) in `Encode` and `EncodeCompressed` reflect the eliminated per-call 32 B allocations accumulating over many writes within a single encode pass.

The change is a clear improvement with no regressions.

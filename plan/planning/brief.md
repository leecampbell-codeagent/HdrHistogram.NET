# Issue #141: ByteBuffer — Reduce Allocations for Serialisation Path

## Summary

`ByteBuffer.PutInt`, `ByteBuffer.PutInt(index, value)`, and `ByteBuffer.PutLong` each call `BitConverter.GetBytes(...)` which allocates a new `byte[]` on every invocation, then immediately copies it into the internal buffer via `Array.Copy`.
This allocation is unnecessary: `System.Buffers.Binary.BinaryPrimitives` can write the big-endian bytes directly into a `Span<byte>` slice of the existing internal buffer with zero allocation.

The same pattern applies to `GetInt` and `GetLong`, which call `BitConverter.ToInt32/ToInt64` followed by `IPAddress.NetworkToHostOrder` to reverse byte order.
`BinaryPrimitives.ReadInt32BigEndian` / `ReadInt64BigEndian` replaces both calls in one step.

`PutDouble` also allocates via `BitConverter.GetBytes` and reverses the array.
`BinaryPrimitives.WriteDoubleBigEndian` eliminates both allocations.

On serialisation-heavy workloads (encoding many histograms), every histogram write triggers six or more `Put*` calls, making this a high-impact, low-risk change.

## Affected Files (confirmed)

| File | Change needed |
|------|---------------|
| `HdrHistogram/Utilities/ByteBuffer.cs` | Replace `PutInt`, `PutInt(index,value)`, `PutLong`, `PutDouble`, `GetInt`, `GetLong` implementations |
| `HdrHistogram.UnitTests/Utilities/ByteBufferTests.cs` | Add round-trip unit tests for all changed methods |
| `HdrHistogram.Benchmarking/Serialization/ByteBufferBenchmark.cs` (new) | Micro-benchmarks for Put/Get operations |
| `HdrHistogram.Benchmarking/Serialization/HistogramEncodingBenchmark.cs` (new) | End-to-end encode/decode round-trip benchmark |
| `HdrHistogram.Benchmarking/Program.cs` | Register the two new benchmark classes in `BenchmarkSwitcher` |

### Indirectly affected (callers, no change required)

- `HdrHistogram/Encoding/HistogramEncoderV2.cs`
- `HdrHistogram/HistogramEncoding.cs`
- `HdrHistogram/Encoding/V0Header.cs`, `V1Header.cs`
- `HdrHistogram/Encoding/IntCountsDecoder.cs`, `LongCountsDecoder.cs`

## Acceptance Criteria

- `PutInt(int value)` writes big-endian bytes directly to `_internalBuffer` with no intermediate allocation.
- `PutInt(int index, int value)` writes big-endian bytes to the specified index with no intermediate allocation.
- `PutLong(long value)` writes big-endian bytes directly to `_internalBuffer` with no intermediate allocation.
- `PutDouble(double value)` writes big-endian bytes directly to `_internalBuffer` with no intermediate allocation.
- `GetInt()` reads big-endian bytes without an intermediate allocation.
- `GetLong()` reads big-endian bytes without an intermediate allocation.
- All existing encoding round-trip tests continue to pass unmodified (`LongHistogramEncodingTests`, `IntHistogramEncodingTests`, `ShortHistogramEncodingTests`).
- New unit tests cover `PutInt`/`GetInt` and `PutLong`/`GetLong` round-trips, including the indexed `PutInt(index, value)` overload.
- Benchmark results show zero `Allocated` bytes for the `Put*`/`Get*` micro-benchmarks after the change.
- End-to-end benchmark shows a measurable reduction in allocated bytes per encode/decode operation.
- The `System.Net` using directive (used only for `IPAddress`) is removed from `ByteBuffer.cs` after refactoring.

## Test Strategy

### Unit tests to add (`HdrHistogram.UnitTests/Utilities/ByteBufferTests.cs`)

Add `[Fact]` and `[Theory]` tests:

- `PutInt_and_GetInt_round_trip_returns_original_value` — writes then reads several representative values (0, 1, −1, `int.MaxValue`, `int.MinValue`).
- `PutInt_at_index_and_GetInt_round_trip_returns_original_value` — uses the index overload to overwrite a position without advancing `Position`.
- `PutLong_and_GetLong_round_trip_returns_original_value` — same coverage for `long`.
- `PutDouble_and_GetDouble_round_trip_returns_original_value` — covers the double path.

Use FluentAssertions (`result.Should().Be(expected)`).

### Existing tests to verify (no changes expected)

- All tests in `HdrHistogram.UnitTests/` — run `dotnet test` after the change to confirm nothing regressed.

## Benchmark Strategy

### Category: `performance`

### Relevant existing benchmarks

- `HdrHistogram.Benchmarking/Recording/Recording32BitBenchmark.cs` — not directly relevant, but confirms the BDN project structure and `Program.cs` registration pattern.
- No existing ByteBuffer or serialisation benchmarks exist.

### New micro-benchmarks — `Serialization/ByteBufferBenchmark.cs`

Isolate each Put/Get method:

```
[MemoryDiagnoser]
ByteBufferBenchmark
  PutInt      — calls PutInt in a loop, resetting Position each iteration
  GetInt      — calls GetInt in a loop, resetting Position each iteration
  PutLong     — calls PutLong in a loop, resetting Position each iteration
  GetLong     — calls GetLong in a loop, resetting Position each iteration
```

Pre-allocate the `ByteBuffer` instance in `[GlobalSetup]`.
Reset `Position` inside each benchmark method (not in setup) per BDN iteration model.

### New end-to-end benchmarks — `Serialization/HistogramEncodingBenchmark.cs`

Exercise the realistic encode/decode workflow:

```
[MemoryDiagnoser]
HistogramEncodingBenchmark
  Encode              — encodes a pre-populated LongHistogram to a MemoryStream
  Decode              — decodes a pre-encoded byte array back to a LongHistogram
  EncodeCompressed    — encodes with DEFLATE compression
  DecodeCompressed    — decodes compressed payload
```

Pre-populate the histogram and pre-encode the byte array in `[GlobalSetup]`.

### Metrics that matter

| Metric | Why |
|--------|-----|
| `Allocated` (bytes) | Primary goal — must drop to 0 for micro-benchmarks |
| `Mean` (ns) | Should improve or stay flat |
| `Gen0` collections | Should drop alongside allocations |

### Benchmark-driven development phases

1. **Phase 1 (before implementation):** Create benchmark files, register in `Program.cs`, run against unmodified code to record baseline (Mean, Allocated, Gen0).
2. **Phase 2 (implementation):** Replace `Put*`/`Get*` bodies with `BinaryPrimitives`, add unit tests.
3. **Phase 3 (validation):** Re-run benchmarks with identical configuration, produce a side-by-side comparison table, document in the PR.

## Risks and Open Questions

- `BinaryPrimitives` is in `System.Buffers` which is available on all target frameworks (`net8.0`, `net9.0`, `net10.0`, `netstandard2.0` ≥ 2.1 via NuGet; `netstandard2.0` ships it as part of `System.Memory`).
  Confirm the `HdrHistogram` project already references `System.Memory` or that `BinaryPrimitives` is available on `netstandard2.0` without an additional package reference.
- `GetShort` also uses `BitConverter` + `IPAddress.HostToNetworkOrder` and could be updated for consistency, but is not called on the hot serialisation path; treat as optional.
- The `ToInt64`, `FromBytes`, `CheckedFromBytes`, `CheckByteArgument` private helpers exist solely to support the old big-endian `GetDouble` logic via `GetLong`.
  After replacing `GetLong` with `BinaryPrimitives.ReadInt64BigEndian`, the `GetDouble` method should be updated to use `BinaryPrimitives.ReadDoubleBigEndian` directly, making those helpers dead code that can be removed.
- Confirm that `IPAddress.NetworkToHostOrder` and `IPAddress.HostToNetworkOrder` are byte-swap operations on little-endian hosts (which they are), so the `BinaryPrimitives` big-endian equivalents are semantically identical.

## Category

`performance`

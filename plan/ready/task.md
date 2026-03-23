# Task List — Issue #141: ByteBuffer — Reduce Allocations for Serialisation Path

## Category: `performance`

Follows the benchmark-driven development process from `spec/tech-standards/testing-standards.md`.

---

## Phase 1 — Benchmark Scaffolding (before any implementation changes)

- [x] **Create `HdrHistogram.Benchmarking/Serialization/ByteBufferBenchmark.cs`**
  - File: `HdrHistogram.Benchmarking/Serialization/ByteBufferBenchmark.cs` (new file, new directory)
  - Why: Establishes baseline allocation measurements for each `Put*`/`Get*` method before optimisation
  - Add `[MemoryDiagnoser]` to the class
  - Allocate a `ByteBuffer` in `[GlobalSetup]`; reset `Position` inside each benchmark method (not in setup)
  - Benchmark methods: `PutInt`, `GetInt`, `PutLong`, `GetLong`, `PutDouble`, `GetDouble`
  - Verify: Class compiles; each method is decorated with `[Benchmark]`

- [x] **Create `HdrHistogram.Benchmarking/Serialization/HistogramEncodingBenchmark.cs`**
  - File: `HdrHistogram.Benchmarking/Serialization/HistogramEncodingBenchmark.cs` (new file)
  - Why: Measures end-to-end allocation impact across a realistic encode/decode workflow
  - Add `[MemoryDiagnoser]` to the class
  - Pre-populate a `LongHistogram` and pre-encode a byte array in `[GlobalSetup]`
  - Benchmark methods: `Encode`, `Decode`, `EncodeCompressed`, `DecodeCompressed`
  - Verify: Class compiles; all four benchmark methods present

- [x] **Register both new benchmark classes in `HdrHistogram.Benchmarking/Program.cs`**
  - File: `HdrHistogram.Benchmarking/Program.cs`
  - Why: `BenchmarkSwitcher` must know about the classes for `--filter` to resolve them
  - Add `typeof(Serialization.ByteBufferBenchmark)` and `typeof(Serialization.HistogramEncodingBenchmark)` to the `new[]` array passed to `BenchmarkSwitcher`
  - Verify: Array contains both new types alongside existing three types

- [x] **Build benchmarks in Release mode against unmodified code**
  - Command: `dotnet build HdrHistogram.Benchmarking/ -c Release`
  - Why: Confirms scaffolding compiles before touching production code
  - Verify: Build succeeds with zero errors and zero warnings

- [x] **Run baseline benchmarks and save results to `plan/benchmarks/baseline.md`**
  - Commands (run separately for each benchmark class):
    ```
    dotnet run -c Release --project HdrHistogram.Benchmarking/ -- --filter '*ByteBufferBenchmark*' --exporters json
    dotnet run -c Release --project HdrHistogram.Benchmarking/ -- --filter '*HistogramEncodingBenchmark*' --exporters json
    ```
  - File: `plan/benchmarks/baseline.md` (new file)
  - Why: Establishes the pre-change allocation and timing baseline for comparison in Phase 3
  - Content: Formatted table with columns — Benchmark | Mean | StdDev | Allocated | Op/s — for every benchmark method on every target framework
  - Verify: File exists and contains non-zero `Allocated` values for `Put*`/`Get*` methods

---

## Phase 2 — Implementation

- [x] **Add conditional `System.Memory` package reference to `HdrHistogram/HdrHistogram.csproj`**
  - File: `HdrHistogram/HdrHistogram.csproj`
  - Why: `BinaryPrimitives` (in `System.Buffers.Binary`) is part of `System.Memory` on `netstandard2.0`; it is inbox on `net8.0`+
  - Add: `<PackageReference Include="System.Memory" Version="4.5.5" Condition="'$(TargetFramework)' == 'netstandard2.0'" />`
  - Verify: `dotnet build HdrHistogram/ -c Release` succeeds on all four target frameworks (`net10.0`, `net9.0`, `net8.0`, `netstandard2.0`)

- [x] **Replace `PutInt(int value)` in `HdrHistogram/Utilities/ByteBuffer.cs`**
  - File: `HdrHistogram/Utilities/ByteBuffer.cs`
  - Why: Eliminates the `BitConverter.GetBytes()` allocation and the `Array.Copy` that follows it
  - Change: Replace body with `BinaryPrimitives.WriteInt32BigEndian(new Span<byte>(_internalBuffer, Position, 4), value); Position += 4;`
  - Verify: Method has no `BitConverter` or `IPAddress` calls; `dotnet build` passes

- [x] **Replace `PutInt(int index, int value)` in `HdrHistogram/Utilities/ByteBuffer.cs`**
  - File: `HdrHistogram/Utilities/ByteBuffer.cs`
  - Why: Eliminates the `BitConverter.GetBytes()` allocation in the indexed overload without advancing `Position`
  - Change: Replace body with `BinaryPrimitives.WriteInt32BigEndian(new Span<byte>(_internalBuffer, index, 4), value);`
  - Verify: Method has no `BitConverter` or `IPAddress` calls; `Position` is unchanged after the call

- [x] **Replace `PutLong(long value)` in `HdrHistogram/Utilities/ByteBuffer.cs`**
  - File: `HdrHistogram/Utilities/ByteBuffer.cs`
  - Why: Eliminates the `BitConverter.GetBytes()` allocation on the hot serialisation path (called once per histogram header field)
  - Change: Replace body with `BinaryPrimitives.WriteInt64BigEndian(new Span<byte>(_internalBuffer, Position, 8), value); Position += 8;`
  - Verify: Method has no `BitConverter` or `IPAddress` calls; `dotnet build` passes

- [x] **Replace `PutDouble(double value)` in `HdrHistogram/Utilities/ByteBuffer.cs`**
  - File: `HdrHistogram/Utilities/ByteBuffer.cs`
  - Why: Eliminates the `BitConverter.GetBytes()` allocation and the `Array.Reverse()` call
  - Change: Replace body with `BinaryPrimitives.WriteDoubleBigEndian(new Span<byte>(_internalBuffer, Position, 8), value); Position += 8;`
  - Verify: Method has no `BitConverter`, `Array.Reverse`, or `IPAddress` calls

- [x] **Replace `GetInt()` in `HdrHistogram/Utilities/ByteBuffer.cs`**
  - File: `HdrHistogram/Utilities/ByteBuffer.cs`
  - Why: Eliminates the `BitConverter.ToInt32` allocation and the `IPAddress.HostToNetworkOrder` call
  - Change: Replace body with `var value = BinaryPrimitives.ReadInt32BigEndian(new ReadOnlySpan<byte>(_internalBuffer, Position, 4)); Position += 4; return value;`
  - Verify: Method has no `BitConverter` or `IPAddress` calls

- [x] **Replace `GetLong()` in `HdrHistogram/Utilities/ByteBuffer.cs`**
  - File: `HdrHistogram/Utilities/ByteBuffer.cs`
  - Why: Eliminates the `BitConverter.ToInt64` allocation and the `IPAddress.HostToNetworkOrder` call; also makes the private helpers dead code
  - Change: Replace body with `var value = BinaryPrimitives.ReadInt64BigEndian(new ReadOnlySpan<byte>(_internalBuffer, Position, 8)); Position += 8; return value;`
  - Verify: Method has no `BitConverter` or `IPAddress` calls

- [x] **Replace `GetDouble()` in `HdrHistogram/Utilities/ByteBuffer.cs`**
  - File: `HdrHistogram/Utilities/ByteBuffer.cs`
  - Why: `GetDouble` currently delegates to `ToInt64` → `CheckedFromBytes` → `FromBytes` → `Int64BitsToDouble`; replacing it with `BinaryPrimitives.ReadDoubleBigEndian` eliminates all that indirection
  - Change: Replace body with `var value = BinaryPrimitives.ReadDoubleBigEndian(new ReadOnlySpan<byte>(_internalBuffer, Position, 8)); Position += 8; return value;`
  - Verify: Method has no `BitConverter`, `ToInt64`, or `Int64BitsToDouble` calls

- [x] **Remove private helper methods from `HdrHistogram/Utilities/ByteBuffer.cs`**
  - File: `HdrHistogram/Utilities/ByteBuffer.cs`
  - Why: `ToInt64`, `CheckedFromBytes`, `CheckByteArgument`, `FromBytes`, and `Int64BitsToDouble` exist solely to support the old `GetDouble` big-endian logic; they are dead code after the refactor
  - Change: Delete all five private methods
  - Verify: `dotnet build` succeeds with no "unreachable code" or "unused method" warnings; no references to these methods remain anywhere in the file

- [x] **Remove `using System.Net;` from `HdrHistogram/Utilities/ByteBuffer.cs`**
  - File: `HdrHistogram/Utilities/ByteBuffer.cs`
  - Why: `IPAddress` (from `System.Net`) is the only consumer of that namespace; after the refactor it is unused
  - Change: Delete the `using System.Net;` directive; add `using System.Buffers.Binary;` in its place
  - Verify: `dotnet build` succeeds; no CS0246 or CS0234 errors; `System.Net` does not appear in the file

- [x] **Add round-trip unit tests to `HdrHistogram.UnitTests/Utilities/ByteBufferTests.cs`**
  - File: `HdrHistogram.UnitTests/Utilities/ByteBufferTests.cs`
  - Why: The brief acceptance criteria require new tests for every refactored method; existing tests only cover `ReadFrom`
  - Add the following tests using `[Theory]`/`[InlineData]` and FluentAssertions:
    - `PutInt_and_GetInt_round_trip_returns_original_value` — values: `0`, `1`, `-1`, `int.MaxValue`, `int.MinValue`
    - `PutInt_at_index_and_GetInt_round_trip_returns_original_value` — uses `PutInt(int index, int value)` overload; asserts `Position` is unchanged after the indexed write, then reads back using `GetInt()` from the same index
    - `PutLong_and_GetLong_round_trip_returns_original_value` — values: `0L`, `1L`, `-1L`, `long.MaxValue`, `long.MinValue`
    - `PutDouble_and_GetDouble_round_trip_returns_original_value` — values: `0.0`, `1.0`, `-1.0`, `double.MaxValue`, `double.Epsilon`
  - Verify: `dotnet test --filter "FullyQualifiedName~ByteBufferTests"` passes all new tests

- [x] **Verify all existing tests pass**
  - Command: `dotnet test`
  - Why: Confirms the `BinaryPrimitives` replacements are semantically identical to the old `BitConverter` + `IPAddress` implementations across all callers (`HistogramEncoderV2`, `IntCountsDecoder`, `LongCountsDecoder`, `V0Header`, `V1Header`, etc.)
  - Verify: Zero test failures; encoding round-trip tests (`LongHistogramEncodingTests`, `IntHistogramEncodingTests`, `ShortHistogramEncodingTests`) all pass

---

## Phase 3 — Benchmark Validation

- [x] **Run post-change benchmarks and save results to `plan/benchmarks/post-change.md`**
  - Commands (identical configuration to baseline):
    ```
    dotnet run -c Release --project HdrHistogram.Benchmarking/ -- --filter '*ByteBufferBenchmark*' --exporters json
    dotnet run -c Release --project HdrHistogram.Benchmarking/ -- --filter '*HistogramEncodingBenchmark*' --exporters json
    ```
  - File: `plan/benchmarks/post-change.md` (new file)
  - Why: Provides post-change measurements to compare against the Phase 1 baseline
  - Content: Same table format as `baseline.md`
  - Verify: File exists; `Allocated` column for `Put*`/`Get*` micro-benchmarks shows `0 B`

- [x] **Generate comparison in `plan/benchmarks/comparison.md`**
  - File: `plan/benchmarks/comparison.md` (new file)
  - Why: Required by the brief and testing standards to document whether the change achieved its non-functional goals
  - Content must include:
    - Side-by-side table: Benchmark | Baseline (Mean / Allocated) | Post-Change (Mean / Allocated) | Delta | Delta %
    - Summary: which metrics improved, which regressed (if any), which are unchanged
    - Verdict: does the data support the change? (expected: yes — zero allocation for micro-benchmarks, reduced allocation for end-to-end)
  - Verify: File exists and contains a verdict section

---

## Phase 4 — Code Review Fixes

- [ ] **Replace magic numbers with `sizeof()` in `HdrHistogram/Utilities/ByteBuffer.cs`**
  - File: `HdrHistogram/Utilities/ByteBuffer.cs`
  - Why: The existing `GetShort()` method uses `Position += sizeof(short)` (consistent with the codebase style); the new `Get*`/`Put*` methods use bare literals `4` and `8`, creating an inconsistency
  - Change: Replace `Position += 4` with `Position += sizeof(int)` in `PutInt(int value)` and `GetInt()`; replace `Position += 8` with `Position += sizeof(long)` in `PutLong`, `GetLong`, `PutDouble`, `GetDouble`; the `Span<byte>` slice size arguments should also use the `sizeof()` equivalent
  - Verify: `dotnet build` passes; no bare `+= 4` or `+= 8` remain in the refactored methods

- [ ] **Add missing `double.MinValue` (and special value) test cases in `ByteBufferTests.cs`**
  - File: `HdrHistogram.UnitTests/Utilities/ByteBufferTests.cs`
  - Why: Every other round-trip test includes `MinValue`; omitting it from the double test is a coverage gap; `double.NaN` is also used in histograms and must survive a round-trip
  - Change: Add `[InlineData(double.MinValue)]`, `[InlineData(double.NaN)]`, `[InlineData(double.PositiveInfinity)]`, `[InlineData(double.NegativeInfinity)]` to `PutDouble_and_GetDouble_round_trip_returns_original_value`; use `value.Should().Be(expected)` — for NaN use `double.IsNaN(result).Should().BeTrue()`
  - Verify: `dotnet test --filter "FullyQualifiedName~ByteBufferTests"` passes all tests including the new cases

---

## Acceptance Criteria Cross-Reference

| Acceptance criterion (from brief) | Covered by task |
|-----------------------------------|-----------------|
| `PutInt(int value)` writes big-endian bytes with no intermediate allocation | Replace `PutInt(int value)` |
| `PutInt(int index, int value)` writes big-endian bytes at index with no allocation | Replace `PutInt(int index, int value)` |
| `PutLong(long value)` writes big-endian bytes with no intermediate allocation | Replace `PutLong(long value)` |
| `PutDouble(double value)` writes big-endian bytes with no intermediate allocation | Replace `PutDouble(double value)` |
| `GetInt()` reads big-endian bytes without intermediate allocation | Replace `GetInt()` |
| `GetLong()` reads big-endian bytes without intermediate allocation | Replace `GetLong()` |
| `GetDouble()` reads big-endian double using `BinaryPrimitives.ReadDoubleBigEndian` | Replace `GetDouble()` |
| All existing encoding round-trip tests pass unmodified | Verify all existing tests pass |
| New unit tests for `PutInt`/`GetInt`, `PutLong`/`GetLong`, indexed `PutInt` round-trips | Add round-trip unit tests |
| Benchmark results show zero `Allocated` bytes for micro-benchmarks | Run post-change benchmarks |
| End-to-end benchmark shows measurable reduction in allocated bytes | Run post-change benchmarks + comparison |
| `using System.Net;` removed from `ByteBuffer.cs` | Remove `using System.Net;` |
| Private helpers `ToInt64`, `CheckedFromBytes`, `CheckByteArgument`, `FromBytes`, `Int64BitsToDouble` removed | Remove private helper methods |
| Conditional `System.Memory` package reference added for `netstandard2.0`; project builds on all targets | Add conditional `System.Memory` package reference |

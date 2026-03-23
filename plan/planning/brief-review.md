# Brief Review — Issue #141: ByteBuffer Allocations

## Verdict: Changes Required

The brief is well-structured and the core analysis is correct.
All claimed code patterns are confirmed in `ByteBuffer.cs`.
Four issues need resolving before the brief is implementation-ready.

---

## Issues

### 1. Missing affected file: `HdrHistogram.csproj` (Critical)

The brief lists `System.Memory` / `BinaryPrimitives` availability on `netstandard2.0` as an open risk, but leaves it unresolved.
After checking the project file, `HdrHistogram.csproj` has **no** `System.Memory` reference and no `System.Buffers` reference.
On `netstandard2.0`, `System.Buffers.Binary.BinaryPrimitives` is not available without the `System.Memory` NuGet package.

**Action:** Add `HdrHistogram/HdrHistogram.csproj` to the affected files table with the required change:

```xml
<ItemGroup Condition="'$(TargetFramework)' == 'netstandard2.0'">
  <PackageReference Include="System.Memory" Version="4.5.5" />
</ItemGroup>
```

Also add an acceptance criterion:

> A conditional `System.Memory` package reference is added to `HdrHistogram.csproj` for `netstandard2.0`, and the project builds successfully on all four target frameworks.

---

### 2. Wrong file paths for two indirectly-affected files (Minor)

The brief lists:

```
HdrHistogram/Encoding/IntCountsDecoder.cs
HdrHistogram/Encoding/LongCountsDecoder.cs
```

Both files live in `HdrHistogram/Persistence/`, not `HdrHistogram/Encoding/`.

**Action:** Correct the paths in the "Indirectly affected" table to:

```
HdrHistogram/Persistence/IntCountsDecoder.cs
HdrHistogram/Persistence/LongCountsDecoder.cs
```

---

### 3. Missing acceptance criterion for `GetDouble` (Minor)

The brief correctly identifies in "Risks and Open Questions" that `GetDouble` uses a helper chain (`ToInt64` → `CheckedFromBytes` → `CheckByteArgument` → `FromBytes`) and that it should be replaced with `BinaryPrimitives.ReadDoubleBigEndian`.
However, this is not reflected in the acceptance criteria.

**Action:** Add two acceptance criteria:

> - `GetDouble()` reads the big-endian double directly using `BinaryPrimitives.ReadDoubleBigEndian` with no intermediate allocation.
> - The private helper methods `ToInt64`, `CheckedFromBytes`, `CheckByteArgument`, `FromBytes`, and `Int64BitsToDouble` are removed from `ByteBuffer.cs` as dead code after the refactor.

---

### 4. `GetDouble` missing from micro-benchmark (Minor)

The micro-benchmark table lists `PutInt`, `GetInt`, `PutLong`, `GetLong` but omits `GetDouble`, even though `GetDouble` is being changed and its existing implementation (byte-loop) allocates nothing — making it a useful before/after data point.

**Action:** Add `GetDouble` and `PutDouble` methods to `ByteBufferBenchmark` to give complete coverage of all changed methods:

```
PutDouble   — calls PutDouble in a loop, resetting Position each iteration
GetDouble   — calls GetDouble in a loop, resetting Position each iteration
```

---

## What Is Already Good

- Code analysis is accurate — all `BitConverter` + `IPAddress` patterns confirmed in the actual file.
- Benchmark strategy correctly follows the three-phase BDD workflow from `testing-standards.md`.
- Both micro and end-to-end benchmark levels are identified.
- Acceptance criteria are measurable and verifiable.
- Scope is appropriate for a single PR.
- Category `performance` is correct.
- Test naming follows project conventions.
- The `System.Net` using-directive removal is captured as an acceptance criterion.

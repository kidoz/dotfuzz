# API reference

[Documentation](../README.md) / Reference

The shipping library targets `net10.0`, with no runtime package dependencies.
All matching is case-sensitive and uses UTF-16 code units. No scorer or extraction
method preprocesses inputs implicitly. See the [compatibility contract](compatibility.md)
for the distance formula, rounding, and FuzzySharp differences.

## Scorers

Namespace: `DotFuzz`. Type: `public static partial class Fuzz`.

Each method below has both overload shapes, with the same parameter names:

```csharp
public static int Ratio(ReadOnlySpan<char> left, ReadOnlySpan<char> right, int scoreCutoff = 0);
public static int Ratio(string left, string right, int scoreCutoff = 0);
```

| Method | Scoring rule |
|---|---|
| `Ratio` | Normalized insertion/deletion similarity |
| `PartialRatio` | Best similarity across candidate alignments of the shorter input within the longer |
| `TokenSortRatio` | `Ratio` of ordinally sorted, single-space-joined tokens |
| `PartialTokenSortRatio` | `PartialRatio` of sorted token joins |
| `TokenSetRatio` | Best `Ratio` across unique-token intersection/remainder combinations |
| `PartialTokenSetRatio` | Partial scoring of unique-token combinations |
| `WeightedRatio` | FuzzySharp-style composite of base, partial, and token scores |

All return an integer in 0–100. Empty input returns 0; token scorers also return 0
if an input has no tokens. Tokens are separated by Unicode whitespace and sorted
ordinally. String overloads reject null arguments with `ArgumentNullException`.

`scoreCutoff` is an inclusive minimum score. Results below it return 0. Negative
cutoffs behave as 0; cutoffs greater than 100 return 0. A returned 0 can mean an
actual zero score or a rejected score. A cutoff is not a different scoring scale.
`WeightedRatio` applies its public cutoff after combining sub-scores.

## Indel distance

Namespace: `DotFuzz.Distance`. Type: `public static class Indel`.

```csharp
public static int Distance(ReadOnlySpan<char> left, ReadOnlySpan<char> right,
    int scoreCutoff = int.MaxValue);
public static int Distance(string left, string right, int scoreCutoff = int.MaxValue);
```

Returns the minimum count of insertions and deletions. A substitution costs two
operations. Empty inputs are valid: the distance from empty to non-empty is the
non-empty input's length; two empty inputs have distance 0.

Despite the parameter name, `scoreCutoff` is a maximum **distance**, measured in
edit operations. The result is exact within that limit; otherwise it is
`scoreCutoff + 1`. The default requests the exact distance. A negative cutoff
throws `ArgumentOutOfRangeException`; null string arguments throw
`ArgumentNullException`.

## Preprocessing

Namespace: `DotFuzz`. Type: `public static class Preprocess`.

```csharp
public static string Compatibility(string source);
public static int Compatibility(ReadOnlySpan<char> source, Span<char> destination);
public static string Unicode(string source);
public static int Unicode(ReadOnlySpan<char> source, Span<char> destination);
```

| Method | Transformation |
|---|---|
| `Compatibility` | Keeps ASCII letters and digits, lowercases ASCII letters, replaces other characters with spaces, trims outer spaces |
| `Unicode` | Keeps letter/digit runes, applies invariant simple lowercasing, replaces other runes with spaces, trims outer spaces |

Interior space runs are preserved. `Unicode` handles supplementary-plane letters;
invalid surrogate sequences become spaces. It does not make subsequent scoring
grapheme-aware or apply Unicode normalization.

String overloads return the original instance if the transformed content is
unchanged and throw `ArgumentNullException` for null. Span overloads return the
written length. Only `destination[..written]` is output. The destination must have
at least `source.Length` code units, even if the eventual output is shorter;
otherwise an `ArgumentException` is thrown.

## Cached ratio

Namespace: `DotFuzz.Cached`. Type: `public sealed class CachedRatio`.

```csharp
public CachedRatio(string query);
public CachedRatio(ReadOnlySpan<char> query);
public int QueryLength { get; }
public int Score(ReadOnlySpan<char> candidate, int scoreCutoff = 0);
public int Score(string candidate, int scoreCutoff = 0);
```

`Score` has the same score and cutoff contracts as `Fuzz.Ratio`. `QueryLength`
counts UTF-16 code units. String construction retains the query string; span
construction copies it. Construction compiles masks and allocates owned state.
The scorer is immutable, supports concurrent readers, and requires no disposal.
Null query or candidate strings throw `ArgumentNullException`.

Ordinary scoring after construction and warmup performs no managed allocations.
Large inputs may use pooled workspace; see [Architecture](../explanation/architecture.md)
for memory tradeoffs and limits.

## Extraction

Namespace: `DotFuzz`. Type: `public static class Process`.

All methods score with direct `Ratio` and accept `ReadOnlySpan<string> choices`.
String arrays can be passed directly. Query overloads compile a `CachedRatio`
once per scan; supplying a cached scorer reuses compiled state.

```csharp
public static ExtractOneResult ExtractOne(string query,
    ReadOnlySpan<string> choices, int scoreCutoff = 0);
public static ExtractOneResult ExtractOne(ReadOnlySpan<char> query,
    ReadOnlySpan<string> choices, int scoreCutoff = 0);
public static ExtractOneResult ExtractOne(CachedRatio scorer,
    ReadOnlySpan<string> choices, int scoreCutoff = 0);

public static ExtractResult[] ExtractTop(string query,
    ReadOnlySpan<string> choices, int limit, int scoreCutoff = 0);
public static ExtractResult[] ExtractTop(ReadOnlySpan<char> query,
    ReadOnlySpan<string> choices, int limit, int scoreCutoff = 0);
public static ExtractResult[] ExtractTop(CachedRatio scorer,
    ReadOnlySpan<string> choices, int limit, int scoreCutoff = 0);
public static int ExtractTop(CachedRatio scorer, ReadOnlySpan<string> choices,
    Span<ExtractResult> destination, int scoreCutoff = 0);

public static ExtractResult[] ExtractAll(string query,
    ReadOnlySpan<string> choices, int scoreCutoff = 0);
public static ExtractResult[] ExtractAll(ReadOnlySpan<char> query,
    ReadOnlySpan<string> choices, int scoreCutoff = 0);
public static ExtractResult[] ExtractAll(CachedRatio scorer,
    ReadOnlySpan<string> choices, int scoreCutoff = 0);
public static int ExtractAll(CachedRatio scorer, ReadOnlySpan<string> choices,
    Span<ExtractResult> destination, int scoreCutoff = 0);
```

| Method | Results and ordering | No qualifying choice |
|---|---|---|
| `ExtractOne` | First highest-scoring candidate | `ExtractOneResult.None` |
| `ExtractTop` | Up to `limit` entries, descending score, earlier indexes first on ties | Empty array or count 0 |
| `ExtractAll` | Every candidate meeting the cutoff, in input order | Empty array or count 0 |

Cutoffs are inclusive, default to 0, and follow the scorer's bounds. At cutoff 0,
zero-scoring and empty-string candidates can qualify. Empty choices yield no
results. Cutoffs greater than 100 yield no results for otherwise valid arguments.

For span destinations, the returned count identifies valid entries in
`destination[..count]`; remaining entries are unspecified. `ExtractTop` uses
destination length as its limit. `ExtractAll` requires a destination at least as
long as `choices`, regardless of cutoff, and throws `ArgumentException` if it is
too short. A negative array-result `limit` throws `ArgumentOutOfRangeException`;
a zero limit or empty top-result destination yields no results.

Null query strings or scorers throw `ArgumentNullException`. A null candidate
encountered during a scan throws `ArgumentException`. Scans can return early, so
they do not guarantee validation of every candidate.

With an existing scorer, `ExtractOne` and the destination overloads perform no
managed allocations for ordinary input sizes. Array-result overloads allocate
results; `ExtractTop` may also copy into a smaller array, and `ExtractAll` uses a
pooled intermediate buffer. Destination buffers contain managed references and
cannot be allocated with `stackalloc`.

`Process` conflicts with `System.Diagnostics.Process` if both namespaces are
imported; qualify it as `DotFuzz.Process` or use an alias.

### Result types

```csharp
public readonly record struct ExtractOneResult(string? Value, int Score, int Index);
public readonly record struct ExtractResult(string Value, int Score, int Index);
```

`Index` refers to the supplied choices and `Value` is that choice's string.
`ExtractOneResult.Found` is true when `Index >= 0`. `ExtractOneResult.None` has
`Value = null`, `Score = 0`, and `Index = -1`. The default struct value has index 0;
use `None` to represent no match explicitly. `ExtractResult` has no `Found`
property: only entries returned by a scan are results.

For executable usage, see [Match and rank choices](../how-to/match-and-rank.md).

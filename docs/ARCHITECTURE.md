# Architecture

## Design goals

The core engine validates four claims that the wider scorer surface builds on:

1. FuzzySharp's direct `Ratio` semantics can be expressed as normalized Indel
   similarity.
2. A span-first core can make ordinary scoring hot paths allocation-free.
3. Score cutoffs can be translated to exact distance bounds and propagated into
   the algorithm.
4. Precompiled query masks materially improve one-to-many matching without
   reflection, dynamic code, or NativeAOT-hostile features.

The shipping project targets only `net10.0`, explicitly selects C# 14, enables
nullable annotations, trim/AOT analysis, deterministic builds, and treats
warnings as errors.

## Data flow

```text
ReadOnlySpan<char>
       |
       +-- common prefix/suffix removal (pairwise path)
       |
       +-- length/cutoff lower-bound rejection
       |
       +-- <= 64 code units: one ulong LCS state
       |
       +-- > 64 code units: blockwise ulong LCS state
       |          |
       |          +-- bounded stack workspace
       |          +-- ArrayPool fallback for unusually large inputs
       |          +-- DP fallback for pathological mask cardinality
       |
       +-- Indel distance = len(left) + len(right) - 2 * LCS
       |
       +-- exact ties-to-even integer normalization to 0..100
```

## Core algorithm

The engine uses the bitset LCS recurrence:

```text
x = matches(character) | state
y = (state << 1) | 1
state = x & ~(x - y)
```

`PopCount(state)` is the LCS length. The blockwise form propagates shift carry
and subtraction borrow across 64-bit words. Replacements cost one deletion plus
one insertion, which is the distance model used by FuzzySharp's direct ratio.

The pairwise short path builds a small open-addressed character-to-mask table on
the stack. The blockwise path uses stack storage for ordinary inputs and pooled
arrays beyond conservative thresholds. A dynamic-programming fallback avoids
quadratic bit-mask storage for very long, very high-cardinality patterns.

## Cutoff model

`Fuzz.Ratio` uses exact integer arithmetic for ties-to-even rounding. A requested
score cutoff is inverted to the largest Indel distance that could still round to
that score. Length difference and partial-LCS upper bounds then reject impossible
matches before the full comparison completes. No floating-point boundary guess
can discard a valid result.

`Indel.Distance` exposes the lower-level distance cutoff contract: exact distance
when it is within the limit, otherwise `cutoff + 1`.

## Cached scorer

`CachedRatio` owns an immutable `CompiledIndelPattern`. Construction sorts the
query's distinct UTF-16 code units once, emits block masks, and builds a compact
open-addressed lookup table. Patterns up to 64 code units use a specialized
single-word scorer. Longer patterns reuse the same masks with per-call state on
the stack or in a rented buffer.

All fields are immutable after construction. Per-call mutation is confined to
local workspace, so one cached scorer may be used concurrently by multiple
threads. Scoring does not require `IDisposable` because the cached state uses
ordinary owned arrays rather than retaining pooled buffers.

## Partial ratio engine

`Fuzz.PartialRatio` scores the shorter input against aligned windows of the
longer input. Candidate window offsets are derived from Levenshtein edit
operations exactly as FuzzySharp 2.0.2 derives them: the cost matrix is built
after common affix trimming, and the backtrace ports python-Levenshtein's
direction state and tie-breaking order so the same alignments are selected.
Each window is then scored with the ordinary bit-parallel Indel engine, with
the running best score converted to a distance bound that prunes hopeless
windows. The cost matrix and edit-op storage live on the stack for short
inputs and in pooled buffers otherwise; working memory grows with the product
of the input lengths, matching the reference algorithm.

## Token scorers

`TokenSortRatio` and `TokenSetRatio` (and their partial variants) tokenize on
Unicode whitespace, sort token ranges ordinally with a stable insertion sort,
and build single-space joins in stack or pooled character buffers. The set
variant deduplicates sorted tokens, classifies them into intersection and
one-sided remainders with a linear merge, and takes the best of the three
FuzzySharp pairings, raising the internal cutoff as better pairings are found.
No token strings are materialized; ordinary inputs score without managed
allocations.

`WeightedRatio` ports FuzzySharp's composite exactly, including its
double-precision scaling of integer sub-scores, the 1.5 and 8.0 length-ratio
thresholds, and final ties-to-even rounding.

## Preprocessors

Matching APIs never preprocess implicitly. `Preprocess.Compatibility`
reproduces FuzzySharp's default mode (non-`[ a-zA-Z0-9]` to space, lowercase,
trim) with a culture-invariant character loop instead of a regular expression.
`Preprocess.Unicode` walks scalar values, keeps letter and digit runes with
invariant simple lowercasing, and turns everything else into spaces. Both
offer span-to-span overloads that write into caller buffers and string
overloads that return the original instance when nothing changes.

## ExtractOne

`Process.ExtractOne(CachedRatio, ReadOnlySpan<string>, int)`:

1. keeps only the current best value, score, and index;
2. raises the next comparison's cutoff to `best + 1`, so ties are skipped and the
   first best choice is stable;
3. exits at 100 because no later candidate can win;
4. returns a record struct and performs no sorting, LINQ, iterator creation, or
   result allocation.

The string and query-span convenience overloads compile the query once per scan.
Supplying an existing `CachedRatio` keeps the complete timed scan allocation-free
for the ordinary input sizes exercised by the tests and benchmarks.

## ExtractTop and ExtractAll

`Process.ExtractTop` maintains a bounded, score-descending insertion buffer.
Once the buffer is full, the scan cutoff rises to one above the current
worst kept score, so ties keep the earliest choice and the scan exits early
when the buffer is saturated at 100. The span-destination overload uses the
destination length as the limit and allocates nothing; the array overloads
allocate only the returned results. `Process.ExtractAll` scores every choice
against the fixed cutoff and preserves choice order.

## NativeAOT posture

The shipping assembly uses no reflection, expression compilation, runtime code
generation, regular expressions, culture-sensitive preprocessing, or plugin
discovery. The `DotFuzz.AotSmoke` project publishes the core APIs as a
self-contained native executable. Benchmark and test dependencies remain outside
the shipping library.

## Deliberate limits

- Matching is by UTF-16 code unit, not Unicode scalar value or grapheme cluster
  (`Preprocess.Unicode` is rune-aware, but scoring itself is not).
- Scorers never preprocess implicitly; callers opt in through `Preprocess`.
- Token ordering is ordinal, not culture-sensitive, and uses a stable insertion
  sort: ordinary token counts are fast, but pathological many-token inputs
  degrade quadratically.
- There is no FuzzySharp API facade; extraction scans `ReadOnlySpan<string>`
  with direct `Ratio`, not `IEnumerable<T>` with extractor delegates.
- `PartialRatio` alignment discovery uses working memory proportional to the
  product of the input lengths, like the reference implementation.
- Very large inputs may rent from `ArrayPool<T>`; "zero allocation" describes
  the measured ordinary hot paths after scorer construction, not an unconditional
  promise for every possible span length.


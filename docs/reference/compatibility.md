# Compatibility contract

[Documentation](../README.md) / Reference

The compatibility oracle is FuzzySharp 2.0.2. This contract covers the implemented
scorers; [API reference](api.md) describes their signatures and result types.

## Ratio semantics

For non-empty inputs, DotFuzz computes:

```text
distance = len(left) + len(right) - 2 * LCS(left, right)
raw similarity = 100 * (len(left) + len(right) - distance)
                 / (len(left) + len(right))
score = round raw similarity to nearest integer, ties to even
```

This matches FuzzySharp 2.0.2's direct, unprocessed `Fuzz.Ratio` behavior in the
golden and deterministic differential corpus. The test suite covers empty,
identical, case-different, punctuation, ASCII, Cyrillic, CJK, emoji/surrogate,
64-bit boundary, blockwise, and 1,024-character cases, plus 2,000 generated
pairs. No score differences were observed.

The following behaviors are explicit:

- if either input is empty, `Ratio` returns 0, matching FuzzySharp 2.x;
- direct matching is case-sensitive and does not normalize punctuation or space;
- strings are compared as UTF-16 code units, matching .NET string indexing;
- exact midpoint scores use ties-to-even rounding;
- null string overload arguments throw `ArgumentNullException`;
- score cutoffs below 0 behave as 0; cutoffs above 100 produce no match.

One divergence class is known and deliberate: when the exact score is a `.5`
midpoint, FuzzySharp rounds a `double` quotient whose representation error can
land one step away from the true midpoint, while DotFuzz's integer arithmetic
applies exact ties-to-even (always the even neighbor). The committed
[token compatibility tests](../../tests/DotFuzz.Tests/TokenRatioCompatibilityTests.cs)
cover 1,500 generated pairs across four scorers (6,000 comparisons) and 1,000
preprocessed pairs across two scorers (2,000 comparisons). The preprocessed
comparisons permit a one-point difference only when DotFuzz's score is even.
The [direct `Ratio` corpus](../../tests/DotFuzz.Tests/RatioCompatibilityTests.cs)
requires exact equality.

## Partial, token, and weighted scorers

- `PartialRatio` ports FuzzySharp 2.0.2's algorithm: candidate windows come
  from the same Levenshtein edit-op matching blocks, including the reference
  backtrace tie-breaking. Its committed
  [differential corpus](../../tests/DotFuzz.Tests/PartialRatioCompatibilityTests.cs)
  checks 2,000 generated pairs for exact equality, in addition to golden cases.
- `TokenSortRatio`, `PartialTokenSortRatio`, `TokenSetRatio`, and
  `PartialTokenSetRatio` follow FuzzySharp's constructions (sorted joins;
  intersection and remainder combinations over unique tokens) with whitespace
  tokenization equivalent to FuzzySharp's `\s+` split.
- DotFuzz sorts tokens ordinally; FuzzySharp uses the current culture's string
  comparer. For FuzzySharp's own fully processed input (lowercase ASCII
  letters, digits, spaces) the orders agree; on unprocessed culture-sensitive
  data the sorted joins can differ.
- `WeightedRatio` reproduces FuzzySharp's weights (0.95 unbase scale, 0.9/0.6
  partial scale), its 1.5 and 8.0 length-ratio thresholds, its use of the
  non-partial token scorers in the partial branch, and its final
  ties-to-even `Math.Round` over double-scaled integer sub-scores.
- Like FuzzySharp's parameterless overloads, none of these scorers preprocess
  input. FuzzySharp's `PreprocessMode.Full` is reproduced by applying
  `Preprocess.Compatibility` to both inputs first; the preprocessor matches
  the reference regex (`[^ a-zA-Z0-9]` to space, lowercase, trim) except that
  lowercasing is culture-invariant rather than current-culture.

## API differences

DotFuzz targets compatible scores for the implemented scorers, subject to the
differences above. It does not provide source compatibility with FuzzySharp:

- `ReadOnlySpan<char>` is the primary API; strings are convenience overloads.
- Preprocessing is always explicit through `Preprocess`; there is no
  `PreprocessMode` parameter on the scorers.
- `Process.ExtractOne`, `ExtractTop`, and `ExtractAll` use direct `Ratio` with
  no preprocessing. FuzzySharp's default `Process` operations use full
  preprocessing and `WeightedRatio`, so callers must not compare those defaults
  as equivalent operations.
- `ExtractOneResult` is a local record struct with `Value`, `Score`, `Index`, and
  `Found`; `ExtractTop`/`ExtractAll` return `ExtractResult` values rather than
  FuzzySharp's result type. `ExtractTop` sorts by descending score with earlier
  indexes winning ties.
- Candidate collections are currently `ReadOnlySpan<string>`; general
  `IEnumerable<T>` and extractor delegates are not included.
- The `Process` class name mirrors FuzzySharp; code that also imports
  `System.Diagnostics` must qualify or alias one of the two.
- A distance cutoff returns `cutoff + 1` when exceeded. A ratio cutoff returns 0
  when the requested score cannot be reached, on every scorer.
- Cached scorer construction from `ReadOnlySpan<char>` copies the query because
  the cached object must own stable data. Construction from `string` retains that
  string.

## Resource limits

Partial alignment uses a Levenshtein matrix. After common prefix and suffix
trimming, `(shorterLength + 1) * (longerLength + 1)` must not exceed 1,048,576
cells (4 MiB of `int` storage). Requests above this limit throw
`ArgumentOutOfRangeException` before renting a matrix. Edit-operation buffers
are additional workspace, and their size arithmetic is checked.

This limit also applies when `PartialTokenSortRatio`, `PartialTokenSetRatio`,
or the partial branch of `WeightedRatio` invokes partial alignment. For token
scorers, it applies to each transformed pair actually scored. Equal inputs,
common affixes, empty-input returns, and cutoffs above 100 can avoid matrix work.
With no common affixes, two 1,023-code-unit inputs fit exactly; two 1,024-code-unit
inputs exceed the limit. Long inputs with small differing middles can still fit.
This is a deliberate resource bound beyond FuzzySharp's scoring contract.

Distance and ratio calculations require combined lengths representable by an
`int`; checked arithmetic throws `OverflowException` when that sum exceeds
`int.MaxValue`. Token workspace sizes are also checked. These limits are separate
from score cutoffs: a resource-limit exception does not mean a non-match.

Pooled buffers containing preprocessed text, joined tokens, or character lookup
keys are cleared when returned, including on exceptional exits. This does not
erase the caller's strings or owned cached query state.

## Not yet implemented

Compatibility facades over FuzzySharp's API shapes, `IEnumerable<T>`/extractor
extraction, `WeightedRatio`-based extraction defaults, and grapheme-aware
Unicode matching are not implemented. Their absence is an API compatibility
gap, not a silent change in the implemented scorers.


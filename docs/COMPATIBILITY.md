# Compatibility contract

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
applies exact ties-to-even (always the even neighbor). Differential sweeps of
40,000 token-scorer pairs surfaced three such one-off differences and no other
mismatch; direct `Ratio` sweeps have surfaced none.

## Partial, token, and weighted scorers

- `PartialRatio` ports FuzzySharp 2.0.2's algorithm: candidate windows come
  from the same Levenshtein edit-op matching blocks, including the reference
  backtrace tie-breaking. A 20,000-pair differential sweep found no
  differences.
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

DotFuzz is source-compatible in score meaning, not API-compatible with
the entire FuzzySharp package:

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

## Not yet implemented

Compatibility facades over FuzzySharp's API shapes, `IEnumerable<T>`/extractor
extraction, `WeightedRatio`-based extraction defaults, and grapheme-aware
Unicode matching are not implemented. Their absence is an API compatibility
gap, not a silent change in the implemented scorers.


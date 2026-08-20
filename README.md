# DotFuzz

[![Language](https://img.shields.io/badge/language-C%23-512BD4)](https://learn.microsoft.com/dotnet/csharp/)
[![.NET SDK](https://img.shields.io/badge/.NET%20SDK-10.0.400-512BD4)](https://github.com/kidoz/dotfuzz/blob/main/global.json)
[![License](https://img.shields.io/badge/license-MIT-blue)](https://github.com/kidoz/dotfuzz/blob/main/LICENSE)

**DotFuzz** — a span-first, NativeAOT-friendly fuzzy string matching engine for
.NET 10, with FuzzySharp-compatible scoring. The current release implements:

- span-first `Indel.Distance` and `Fuzz.Ratio` APIs;
- the full direct scorer family: `PartialRatio`, `TokenSortRatio`,
  `PartialTokenSortRatio`, `TokenSetRatio`, `PartialTokenSetRatio`, and
  `WeightedRatio`;
- exact integer score cutoffs with early rejection on every scorer;
- explicit `Preprocess.Compatibility` (FuzzySharp-equivalent) and
  `Preprocess.Unicode` input preprocessors;
- an immutable, reusable, thread-safe `CachedRatio` scorer;
- allocation-free `ExtractOne`, top-k `ExtractTop`, and `ExtractAll` scans over
  `ReadOnlySpan<string>`;
- differential tests against FuzzySharp 2.0.2;
- BenchmarkDotNet comparisons with FuzzySharp 2.0.2 and
  Raffinert.FuzzySharp 5.0.3;
- a NativeAOT publish smoke test.

DotFuzz keeps score meaning compatible with FuzzySharp while staying span-first
and preprocessing-free by default; it does not reproduce FuzzySharp's API
surface one-to-one.

## Quick start

```csharp
using DotFuzz;
using DotFuzz.Cached;
using DotFuzz.Distance;

int distance = Indel.Distance("kitten".AsSpan(), "sitting".AsSpan());
int score = Fuzz.Ratio("mysmilarstring".AsSpan(), "mysimilarstring".AsSpan());
int pruned = Fuzz.Ratio("query".AsSpan(), "candidate".AsSpan(), scoreCutoff: 80);

int partial = Fuzz.PartialRatio("yankees".AsSpan(), "new york yankees".AsSpan());
int sorted = Fuzz.TokenSortRatio("fuzzy wuzzy was a bear", "wuzzy fuzzy was a bear");
int set = Fuzz.TokenSetRatio("fuzzy was a bear", "fuzzy fuzzy was a bear");
int weighted = Fuzz.WeightedRatio("this is a test", "this is a test!");

string processed = Preprocess.Compatibility("New York Mets!"); // "new york mets"

var cached = new CachedRatio("new york mets");
int cachedScore = cached.Score("new york mets tickets".AsSpan());

string[] choices = ["Atlanta Falcons", "New York Jets", "Dallas Cowboys"];
ExtractOneResult best = Process.ExtractOne(new CachedRatio("cowboys"), choices);
ExtractResult[] top = Process.ExtractTop("New York Jets", choices, limit: 2);
ExtractResult[] all = Process.ExtractAll("New York Jets", choices, scoreCutoff: 50);
```

Every scorer is case-sensitive and performs no implicit preprocessing. Scoring
operates over UTF-16 code units, uses normalized Indel similarity, and returns
an integer from 0 to 100. Passing a cutoff returns zero when the score cannot
reach the threshold. To reproduce FuzzySharp's fully processed modes, run
inputs through `Preprocess.Compatibility` (or the Unicode-aware
`Preprocess.Unicode`) first.

`Indel.Distance` returns the exact insertion/deletion distance by default. With
a cutoff, it returns `cutoff + 1` as a sentinel when the exact result is larger.

## Requirements

- [.NET SDK 10.0.400](https://dotnet.microsoft.com/download/dotnet/10.0) or
  later (see [global.json](global.json))
- No runtime dependencies; the library is trim- and NativeAOT-compatible

## Build and verify

```text
dotnet restore DotFuzz.slnx
dotnet build DotFuzz.slnx -c Release --no-restore
dotnet test DotFuzz.slnx -c Release --no-restore
dotnet publish tests/DotFuzz.AotSmoke/DotFuzz.AotSmoke.csproj \
  -c Release -r <your-runtime-identifier> --self-contained
```

Formatting is enforced with [CSharpier](https://csharpier.com/), pinned as a
local tool:

```text
dotnet tool restore
dotnet csharpier format .
```

Run every benchmark or select one suite:

```text
dotnet run -c Release \
  --project benchmarks/DotFuzz.Benchmarks/DotFuzz.Benchmarks.csproj \
  -- --filter '*PairwiseRatioBenchmarks*' --job short
```

## Repository map

```text
src/DotFuzz/                   shipping library
  Distance/                    Indel and bit-parallel LCS engine
  Cached/                      compiled query state
  Internal/                    score/cutoff math, partial alignment, token ops
  Process/                     ExtractOne/ExtractTop/ExtractAll and result types
tests/DotFuzz.Tests/           golden, differential, property, allocation tests
tests/DotFuzz.AotSmoke/        NativeAOT console smoke test
benchmarks/DotFuzz.Benchmarks/ BenchmarkDotNet comparison suites
docs/                          architecture, compatibility, methodology, findings
```

## Documentation

- [Architecture](docs/ARCHITECTURE.md) — design contract and engine internals
- [Compatibility](docs/COMPATIBILITY.md) — FuzzySharp behavioral compatibility
- [Benchmarks](docs/BENCHMARKS.md) — methodology and measured results

## License

DotFuzz is licensed under the [MIT License](LICENSE).

Copyright (c) 2026 Aleksandr Pavlov.

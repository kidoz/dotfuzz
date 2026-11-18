# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.0.2] - 2026-11-18

### Added

- .NET 11 support alongside .NET 10, with unit tests and NativeAOT smoke checks
  for both target frameworks.

### Changed

- Build the repository with the .NET 11 SDK.

### Fixed

- Support overlapping source and destination spans in both preprocessors.
- Bound the number of comparisons when sorting large token lists and document
  how culture affects compatibility with FuzzySharp's token scores.
- Keep NativeAOT publish locks separate from the portable solution restore.
- Isolate NuGet publishing permissions in the release job and verify the
  package and symbol versions against the canonical release tag.

## [1.0.1] - 2026-10-25

### Changed

- Partial alignment now rejects matrices above 1,048,576 cells after common
  affix trimming with `ArgumentOutOfRangeException`. This includes calls through
  partial token scorers and the partial branch of `WeightedRatio`.

### Fixed

- Skip partial-alignment matrices when affix trimming empties either side,
  preserving exact prefix/suffix matches with long unmatched tails and scores
  for internal insertions. Matrix-limit exceptions no longer blame one parameter.
- Clear pooled preprocessing, token-join, and character-key buffers on return.
- Reject overflowing combined distance lengths and token workspace sizes, check
  edit-operation buffer sizing, and widen mask-size calculations before fallback.
- Align documented compatibility corpus counts with committed tests.
- Ignore the repository-local package cache and scope formatting commands to
  `src`, `tests`, and `benchmarks`; document locked restore consistently.

## [1.0.0] - 2026-08-20

### Added

- Span-first `Indel.Distance` and `Fuzz.Ratio` with exact integer score cutoffs
  and early rejection.
- The full direct scorer family with FuzzySharp 2.0.2-compatible scoring:
  `PartialRatio`, `TokenSortRatio`, `PartialTokenSortRatio`, `TokenSetRatio`,
  `PartialTokenSetRatio`, and `WeightedRatio`.
- Explicit `Preprocess.Compatibility` (FuzzySharp-equivalent) and
  `Preprocess.Unicode` input preprocessors.
- Immutable, reusable, thread-safe `CachedRatio` compiled query scorer.
- Allocation-conscious `Process.ExtractOne`, top-k `ExtractTop`, and
  `ExtractAll` scans over `ReadOnlySpan<string>`.
- Differential test suites against FuzzySharp 2.0.2, BenchmarkDotNet
  comparison suites, and a NativeAOT publish smoke test.
- Trim- and NativeAOT-compatible, deterministic packaging with SourceLink and
  symbol packages.

[Unreleased]: https://github.com/kidoz/dotfuzz/compare/v1.0.2...HEAD
[1.0.2]: https://github.com/kidoz/dotfuzz/releases/tag/v1.0.2
[1.0.1]: https://github.com/kidoz/dotfuzz/releases/tag/v1.0.1
[1.0.0]: https://github.com/kidoz/dotfuzz/releases/tag/v1.0.0

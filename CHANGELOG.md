# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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

[Unreleased]: https://github.com/kidoz/dotfuzz/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/kidoz/dotfuzz/releases/tag/v1.0.0

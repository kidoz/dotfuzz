# DotFuzz documentation

DotFuzz is a span-first fuzzy string matching library for .NET 10. Start with the
tutorial to build a small matching application, or choose a page for your task.

The documentation follows [Diátaxis](https://diataxis.fr/): tutorials guide
learning, how-to guides solve tasks, reference describes the library, and
explanation develops understanding.

| Your need | Documentation |
|---|---|
| Learn by building a working example | [Tutorial: Find your first fuzzy match](tutorials/first-match.md) |
| Normalize text and search a collection | [How to match and rank choices](how-to/match-and-rank.md) |
| Validate a local checkout or native executable | [How to build and verify](how-to/build-and-verify.md) |
| Measure performance | [How to run benchmarks](how-to/run-benchmarks.md) |
| Look up methods, parameters, and results | [API reference](reference/api.md) |
| Check score semantics and FuzzySharp differences | [Compatibility contract](reference/compatibility.md) |
| Consult recorded performance measurements | [Benchmark results](reference/benchmark-results.md) |
| Understand the engine and its tradeoffs | [Architecture](explanation/architecture.md) |

## Tutorials

[Find your first fuzzy match](tutorials/first-match.md) walks through creating a
console application, comparing two strings, and finding a candidate. It includes
complete code and expected output.

## How-to guides

- [Match and rank choices](how-to/match-and-rank.md): preprocess inputs, reuse a
  cached query, apply a cutoff, and write results into a caller-owned buffer.
- [Build and verify](how-to/build-and-verify.md): restore, check formatting, build,
  test, and publish and run the NativeAOT smoke application.
- [Run benchmarks](how-to/run-benchmarks.md): select suites and jobs, export
  results, and record the measurement environment.

## Reference

- [API](reference/api.md): scorers, distance, preprocessing, caching, extraction,
  and result types.
- [Compatibility](reference/compatibility.md): score contracts, deliberate
  divergences, and unsupported FuzzySharp API shapes.
- [Benchmark results](reference/benchmark-results.md): existing measurement data
  and the conditions under which it was collected.

## Explanation

[Architecture](explanation/architecture.md) connects the Indel/LCS algorithm,
exact cutoff arithmetic, cached masks, allocation strategy, and NativeAOT design.

## Maintaining these docs

Keep each page focused on one reader need. Put a guided learning exercise in
`tutorials/`, a task solution in `how-to/`, a factual contract in `reference/`,
and design reasoning in `explanation/`. Link to supporting pages when another
kind of information is needed. Add new pages to this index.

Check examples against the current public API and run them when changing their
behavior. Preserve measurement dates and environments; new benchmark claims need
new measurements. Repository agent guidance remains under [`.agents/`](../.agents/).

See also the [project overview](../README.md) and [changelog](../CHANGELOG.md).

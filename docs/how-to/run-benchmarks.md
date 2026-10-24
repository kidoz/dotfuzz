# Run benchmarks

[Documentation](../README.md) / How-to guides

Run from the repository root using the SDK selected by
[`global.json`](../../global.json). Restore the solution first:

```sh
dotnet restore DotFuzz.slnx --locked-mode
```

## Select a suite

Use a short job for an initial comparison:

```sh
dotnet run -c Release \
  --project benchmarks/DotFuzz.Benchmarks/DotFuzz.Benchmarks.csproj \
  -- --filter '*PairwiseRatioBenchmarks*' --job short --exporters json
```

Replace the quoted filter to measure another operation:

| Filter | Operation |
|---|---|
| `*PairwiseRatioBenchmarks*` | Direct `Ratio` comparisons |
| `*CachedRatioBenchmarks*` | Repeated scoring with compiled queries |
| `*ExtractOneBenchmarks*` | Best-match scans across 512 choices |
| `*CutoffRatioBenchmarks*` | Direct scoring with a score cutoff |

Keep filters quoted so the shell does not expand `*`. To run every suite, use
`--filter '*'`.

## Collect a longer measurement

Omit `--job short` to use the default BenchmarkDotNet job:

```sh
dotnet run -c Release \
  --project benchmarks/DotFuzz.Benchmarks/DotFuzz.Benchmarks.csproj \
  -- --filter '*CachedRatioBenchmarks*' --exporters json
```

Run on an otherwise idle machine. Keep the same job, runtime, and hardware when
comparing changes.

## Inspect and record results

Open the reports in `BenchmarkDotNet.Artifacts/results/`. Inspect means,
uncertainty, and allocated bytes. Keep the raw reports with the comparison and
record the commit, date, OS, CPU, SDK/runtime, command, and job.

Cached query construction happens in setup and is excluded from timed scoring.
Treat extraction as a complete scan strategy: the DotFuzz implementation raises
cutoffs as better matches are found. The cutoff suite compares different public
API capabilities. Consult [Benchmark results](../reference/benchmark-results.md)
for the existing measurements, exact input definitions, and comparison limits.

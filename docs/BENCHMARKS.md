# Benchmark methodology and findings

## Environment

Measurements were taken on 2026-08-20 with:

- macOS Tahoe 26.6.2, Apple Silicon arm64 (BenchmarkDotNet reported the processor
  model as unknown in the sandbox);
- .NET SDK 10.0.400 and .NET runtime 10.0.11, Arm64 RyuJIT;
- concurrent workstation GC;
- BenchmarkDotNet 0.15.8 `ShortRun`: one launch, three warmups, three measured
  iterations;
- `MemoryDiagnoser` enabled;
- Release builds, with FuzzySharp 2.0.2 and Raffinert.FuzzySharp 5.0.3 pinned by
  central package management.

The raw CSV, JSON, HTML, and Markdown reports are generated in
`BenchmarkDotNet.Artifacts/results/` by the commands below.

## Inputs and fairness

Pairwise scenarios cross three lengths (16, 128, and 1,024 UTF-16 code units),
similar/dissimilar data, and ASCII/Unicode alphabets. Similar inputs replace one
code unit every eleven positions. Dissimilar inputs use disjoint alphabets.
Unicode data uses BMP Cyrillic, CJK, and Greek characters so the benchmark tests
non-ASCII lookup without conflating the result with grapheme segmentation.

All pairwise methods call direct, case-sensitive `Ratio` with no preprocessing.
Cached scorer construction occurs in `GlobalSetup` and is excluded from timing.
Extraction scans 512 choices with the near match at index 510; it compares the
original FuzzySharp ratio loop, Raffinert's cached scorer loop, and DotFuzz's
cached `ExtractOne`. The latter also raises the distance cutoff as better results
are found, so this suite measures the intended extraction strategy, not merely
512 isolated calls.

The cutoff suite compares DotFuzz's public cutoff to Raffinert's public
static `Ratio`, which has no score-cutoff parameter. It demonstrates end-to-end
API value, not a claim that Raffinert lacks internal pruning in other APIs.

## Pairwise Ratio results

All DotFuzz pairwise cases measured 0 B allocated. Means and speedups are:

| Scenario | DotFuzz mean | vs FuzzySharp | vs Raffinert |
|---|---:|---:|---:|
| Short similar ASCII | 15.80 ns | 1.77x | 3.67x |
| Short dissimilar ASCII | 34.20 ns | 4.84x | 2.56x |
| Short similar Unicode | 15.63 ns | 1.74x | 4.61x |
| Short dissimilar Unicode | 38.72 ns | 4.58x | 6.90x |
| Medium similar ASCII | 581.71 ns | 12.80x | 1.06x |
| Medium dissimilar ASCII | 616.95 ns | 16.02x | 1.27x |
| Medium similar Unicode | 569.63 ns | 16.57x | 2.07x |
| Medium dissimilar Unicode | 601.21 ns | 18.50x | 2.05x |
| Long similar ASCII | 17.72 us | 39.06x | 1.16x |
| Long dissimilar ASCII | 15.26 us | 39.06x | 1.34x |
| Long similar Unicode | 17.39 us | 40.53x | 1.57x |
| Long dissimilar Unicode | 15.68 us | 35.51x | 1.51x |

FuzzySharp allocated 200-8,360 B per pair, Raffinert allocated 144-146 B, and
DotFuzz allocated 0 B in all twelve pairwise cases.

## Cached Ratio results

The final precompiled hash lookup removed the per-character binary-search
hotspot. `CachedRatio` allocated 0 B in all cases and:

- beat Raffinert in all four short scenarios (15.59-23.10 ns versus
  23.43-47.23 ns);
- was 3% slower for medium similar ASCII, but 1.20x-1.56x faster in the other
  three medium scenarios;
- was 1.32x-1.50x faster in all four long scenarios, at 12.38-15.22 us.

FuzzySharp 2.0.2 has no query-compiling direct Ratio API; its ordinary scorer is
retained in this table as the compatibility baseline and allocated 200-8,360 B.

## ExtractOne results

The table reports a scan over 512 choices. DotFuzz allocated 0 B in every
scan.

| Length | Unicode | DotFuzz | vs FuzzySharp loop | vs Raffinert cached loop |
|---:|:---:|---:|---:|---:|
| 16 | no | 12.50 us | 7.00x | 1.06x |
| 16 | yes | 13.94 us | 7.11x | 1.52x |
| 128 | no | 201.25 us | 29.04x | 1.22x |
| 128 | yes | 246.02 us | 23.08x | 1.14x |
| 1,024 | no | 7.71 ms | 41.03x | 1.33x |
| 1,024 | yes | 7.71 ms | 38.46x | 1.37x |

FuzzySharp allocated about 151 KB, 610 KB, and 4.28 MB per short, medium, and
long scan respectively. Raffinert's loop allocated about 32 KB per 512-choice
scan in this harness.

## Score-cutoff results

On dissimilar inputs with cutoff 85, DotFuzz returned zero and allocated
0 B. Relative to Raffinert's public no-cutoff call it was 3.0x/9.2x faster for
short ASCII/Unicode, 2.2x/3.7x for medium, and 4.0x/4.8x for long inputs.

## Conclusions

The results validate the central architecture: direct scoring is
allocation-free in the measured range, exact compatibility is maintained in the
test corpus, cached lookup is competitive across every size, rising cutoffs make
extraction faster, and the advantage over the original implementation grows
substantially with input length. The `ShortRun` job is suitable for
direction-setting, not a release-grade performance guarantee; repeat the default
job on target production hardware before setting service-level targets.

## Reproduce

```text
dotnet run -c Release \
  --project benchmarks/DotFuzz.Benchmarks/DotFuzz.Benchmarks.csproj \
  -- --filter '*PairwiseRatioBenchmarks*' --job short --exporters json

dotnet run -c Release \
  --project benchmarks/DotFuzz.Benchmarks/DotFuzz.Benchmarks.csproj \
  -- --filter '*CachedRatioBenchmarks*' --job short --exporters json

dotnet run -c Release \
  --project benchmarks/DotFuzz.Benchmarks/DotFuzz.Benchmarks.csproj \
  -- --filter '*ExtractOneBenchmarks*' --job short --exporters json

dotnet run -c Release \
  --project benchmarks/DotFuzz.Benchmarks/DotFuzz.Benchmarks.csproj \
  -- --filter '*CutoffRatioBenchmarks*' --job short --exporters json
```


using System.Diagnostics.CodeAnalysis;
using BenchmarkDotNet.Attributes;
using DotFuzz.Cached;
using OriginalFuzz = FuzzySharp.Fuzz;
using RaffinertCachedRatio = Raffinert.FuzzySharp.SimilarityRatio.Scorer.StrategySensitive.CachedDefaultRatioScorer;
using RaffinertPreprocessor = Raffinert.FuzzySharp.PreProcess.StringPreprocessor;

namespace DotFuzz.Benchmarks;

[MemoryDiagnoser]
[SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "BenchmarkDotNet owns lifecycle and invokes GlobalCleanup."
)]
public class CachedRatioBenchmarks
{
    private string _left = null!;
    private string _right = null!;
    private CachedRatio _cached = null!;
    private RaffinertCachedRatio _raffinertCached = null!;

    [ParamsAllValues]
    public RatioScenario Scenario { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        (_left, _right) = ScenarioFactory.Create(Scenario);
        _cached = new CachedRatio(_left);
        _raffinertCached = new RaffinertCachedRatio(_left, RaffinertPreprocessor.None);
    }

    [GlobalCleanup]
    public void Cleanup() => _raffinertCached.Dispose();

    [Benchmark(Baseline = true, Description = "FuzzySharp 2.0.2 (no query cache)")]
    public int FuzzySharp202() => OriginalFuzz.Ratio(_left, _right);

    [Benchmark(Description = "Raffinert 5.0.3 CachedDefaultRatioScorer")]
    public int Raffinert503Cached() => _raffinertCached.Score(_right);

    [Benchmark(Description = "DotFuzz CachedRatio")]
    public int DotFuzzCached() => _cached.Score(_right.AsSpan());
}

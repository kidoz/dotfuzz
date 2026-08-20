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
public class ExtractOneBenchmarks
{
    private string _query = null!;
    private string[] _choices = null!;
    private CachedRatio _cached = null!;
    private RaffinertCachedRatio _raffinertCached = null!;

    [Params(16, 128, 1_024)]
    public int Length { get; set; }

    [Params(false, true)]
    public bool Unicode { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var scenario = (Length, Unicode) switch
        {
            (16, false) => RatioScenario.ShortSimilarAscii,
            (16, true) => RatioScenario.ShortSimilarUnicode,
            (128, false) => RatioScenario.MediumSimilarAscii,
            (128, true) => RatioScenario.MediumSimilarUnicode,
            (1_024, false) => RatioScenario.LongSimilarAscii,
            _ => RatioScenario.LongSimilarUnicode,
        };
        (_query, _) = ScenarioFactory.Create(scenario);
        _choices = ScenarioFactory.CreateChoices(_query, 512, Unicode);
        _cached = new CachedRatio(_query);
        _raffinertCached = new RaffinertCachedRatio(_query, RaffinertPreprocessor.None);
    }

    [GlobalCleanup]
    public void Cleanup() => _raffinertCached.Dispose();

    [Benchmark(Baseline = true, Description = "FuzzySharp 2.0.2 Ratio loop")]
    public int FuzzySharp202Loop() => ExtractWithOriginal(_query, _choices);

    [Benchmark(Description = "Raffinert 5.0.3 cached Ratio loop")]
    public int Raffinert503CachedLoop() => ExtractWithRaffinert(_raffinertCached, _choices);

    [Benchmark(Description = "DotFuzz cached ExtractOne")]
    public int DotFuzzCachedExtractOne() => Process.ExtractOne(_cached, _choices).Score;

    private static int ExtractWithOriginal(string query, string[] choices)
    {
        var best = 0;
        foreach (var choice in choices)
        {
            best = Math.Max(best, OriginalFuzz.Ratio(query, choice));
        }

        return best;
    }

    private static int ExtractWithRaffinert(RaffinertCachedRatio scorer, string[] choices)
    {
        var best = 0;
        foreach (var choice in choices)
        {
            best = Math.Max(best, scorer.Score(choice));
        }

        return best;
    }
}

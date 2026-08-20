using BenchmarkDotNet.Attributes;
using RaffinertFuzz = Raffinert.FuzzySharp.Fuzz;

namespace DotFuzz.Benchmarks;

[MemoryDiagnoser]
public class CutoffRatioBenchmarks
{
    private string _left = null!;
    private string _right = null!;

    [Params(
        RatioScenario.ShortDissimilarAscii,
        RatioScenario.MediumDissimilarAscii,
        RatioScenario.LongDissimilarAscii,
        RatioScenario.ShortDissimilarUnicode,
        RatioScenario.MediumDissimilarUnicode,
        RatioScenario.LongDissimilarUnicode
    )]
    public RatioScenario Scenario { get; set; }

    [Params(85)]
    public int ScoreCutoff { get; set; }

    [GlobalSetup]
    public void Setup() => (_left, _right) = ScenarioFactory.Create(Scenario);

    [Benchmark(Baseline = true, Description = "Raffinert 5.0.3 (no public cutoff)")]
    public int RaffinertWithoutPublicCutoff() => RaffinertFuzz.Ratio(_left, _right);

    [Benchmark(Description = "DotFuzz (cutoff)")]
    public int DotFuzzWithCutoff() => Fuzz.Ratio(_left.AsSpan(), _right.AsSpan(), ScoreCutoff);
}

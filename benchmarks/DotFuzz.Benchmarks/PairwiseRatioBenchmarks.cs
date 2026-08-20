using BenchmarkDotNet.Attributes;
using OriginalFuzz = FuzzySharp.Fuzz;
using RaffinertFuzz = Raffinert.FuzzySharp.Fuzz;

namespace DotFuzz.Benchmarks;

[MemoryDiagnoser]
public class PairwiseRatioBenchmarks
{
    private string _left = null!;
    private string _right = null!;

    [ParamsAllValues]
    public RatioScenario Scenario { get; set; }

    [GlobalSetup]
    public void Setup() => (_left, _right) = ScenarioFactory.Create(Scenario);

    [Benchmark(Baseline = true, Description = "FuzzySharp 2.0.2")]
    public int FuzzySharp202() => OriginalFuzz.Ratio(_left, _right);

    [Benchmark(Description = "Raffinert 5.0.3")]
    public int Raffinert503() => RaffinertFuzz.Ratio(_left, _right);

    [Benchmark]
    public int DotFuzz() => Fuzz.Ratio(_left.AsSpan(), _right.AsSpan());
}

using BenchmarkDotNet.Attributes;
using DotFuzz.Cached;

namespace DotFuzz.Benchmarks;

[MemoryDiagnoser]
public class CachedConstructionBenchmarks
{
    private string _query = null!;

    [Params(16, 63, 64, 65, 80, 81, 1_024)]
    public int Length { get; set; }

    [Params(true, false)]
    public bool AsciiOnly { get; set; }

    [Params(true, false)]
    public bool VariedAlphabet { get; set; }

    [GlobalSetup]
    public void Setup() => _query = CreateQuery(Length, AsciiOnly, VariedAlphabet);

    [Benchmark]
    public CachedRatio Compile() => new(_query);

    internal static string CreateQuery(int length, bool asciiOnly, bool variedAlphabet)
    {
        var symbols = asciiOnly ? "cab17" : "a東Ж7λ";
        var chars = new char[length];
        for (var i = 0; i < chars.Length; i++)
        {
            if (!variedAlphabet)
            {
                chars[i] = symbols[i % symbols.Length];
                continue;
            }

            // Repeat every eighth symbol and permute the others across the alphabet.
            var index = i - (i / 8);
            chars[i] =
                !asciiOnly && (index & 1) != 0
                    ? (char)(0x4000 + ((53 * index) % 0x2700))
                    : (char)(' ' + ((37 * index) % 95));
        }

        return new string(chars);
    }
}

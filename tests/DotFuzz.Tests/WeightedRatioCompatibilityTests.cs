using FuzzySharp.PreProcess;
using OriginalFuzz = FuzzySharp.Fuzz;

namespace DotFuzz.Tests;

public sealed class WeightedRatioCompatibilityTests
{
    public static TheoryData<string, string> GoldenPairs =>
        new()
        {
            { "", "" },
            { "", "nonempty" },
            { "this is a test", "this is a test!" },
            { "fuzzy wuzzy was a bear", "wuzzy fuzzy was a bear" },
            { "yankees", "new york yankees" },
            { "tea", "a cup of the finest green tea in the entire world" },
            { "mysmilarstring", "myawfullysimilarstirng" },
            { "a b c", "c b a" },
        };

    [Theory]
    [MemberData(nameof(GoldenPairs))]
    public void WeightedRatioMatchesFuzzySharp202(string left, string right)
    {
        var expected = OriginalFuzz.WeightedRatio(left, right, PreprocessMode.None);
        Assert.Equal(expected, Fuzz.WeightedRatio(left.AsSpan(), right.AsSpan()));
    }

    [Fact]
    public void WeightedRatioMatchesFuzzySharpOnDeterministicRandomCorpus()
    {
        // Lowercase alphanumeric tokens keep FuzzySharp's culture-sensitive token
        // ordering identical to DotFuzz's ordinal ordering.
        var random = new Random(0xF00D);
        for (var iteration = 0; iteration < 1_000; iteration++)
        {
            var left = RandomTokenString(random, random.Next(0, 90));
            var right = RandomTokenString(random, random.Next(0, 90));
            var expected = OriginalFuzz.WeightedRatio(left, right, PreprocessMode.None);

            Assert.Equal(expected, Fuzz.WeightedRatio(left.AsSpan(), right.AsSpan()));
        }
    }

    [Fact]
    public void PreprocessedWeightedRatioMatchesFuzzySharpFullMode()
    {
        var random = new Random(0xDECAF);
        for (var iteration = 0; iteration < 600; iteration++)
        {
            var left = RandomRawString(random, random.Next(0, 90));
            var right = RandomRawString(random, random.Next(0, 90));
            var expected = OriginalFuzz.WeightedRatio(left, right, PreprocessMode.Full);

            Assert.Equal(
                expected,
                Fuzz.WeightedRatio(Preprocess.Compatibility(left), Preprocess.Compatibility(right))
            );
        }
    }

    [Fact]
    public void ScoreCutoffKeepsExactScoresAndRejectsBelowThreshold()
    {
        var random = new Random(0xACE);
        for (var iteration = 0; iteration < 300; iteration++)
        {
            var left = RandomTokenString(random, random.Next(0, 50));
            var right = RandomTokenString(random, random.Next(0, 50));
            var cutoff = random.Next(0, 102);
            var uncut = Fuzz.WeightedRatio(left, right);
            var expected = uncut >= cutoff && cutoff <= 100 ? uncut : 0;

            Assert.Equal(expected, Fuzz.WeightedRatio(left.AsSpan(), right.AsSpan(), cutoff));
        }
    }

    [Fact]
    public void NullStringsThrow()
    {
        Assert.Throws<ArgumentNullException>(() => Fuzz.WeightedRatio(null!, "x"));
        Assert.Throws<ArgumentNullException>(() => Fuzz.WeightedRatio("x", null!));
    }

    private static string RandomTokenString(Random random, int length) =>
        string.Create(
            length,
            random,
            static (span, state) =>
            {
                const string alphabet = "abcdxyz019   ";
                for (var index = 0; index < span.Length; index++)
                {
                    span[index] = alphabet[state.Next(alphabet.Length)];
                }
            }
        );

    private static string RandomRawString(Random random, int length) =>
        string.Create(
            length,
            random,
            static (span, state) =>
            {
                const string alphabet = "abC XY-z!01,9 Ж中 \t";
                for (var index = 0; index < span.Length; index++)
                {
                    span[index] = alphabet[state.Next(alphabet.Length)];
                }
            }
        );
}

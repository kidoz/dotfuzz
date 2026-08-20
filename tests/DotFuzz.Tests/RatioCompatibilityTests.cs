using DotFuzz.Cached;
using OriginalFuzz = FuzzySharp.Fuzz;

namespace DotFuzz.Tests;

public sealed class RatioCompatibilityTests
{
    public static TheoryData<string, string> GoldenPairs =>
        new()
        {
            { "", "" },
            { "", "nonempty" },
            { "abc", "abc" },
            { "abc", "ABC" },
            { "mysmilarstring", "myawfullysimilarstirng" },
            { "mysmilarstring", "mysimilarstring" },
            { "fuzzy wuzzy was a bear", "wuzzy fuzzy was a bear" },
            { "this is a test", "this is a test!" },
            { "New York Mets", "new york mets" },
            { "Straße", "Strasse" },
            { "Москва", "Масква" },
            { "東京駅", "東京" },
            { "👩‍💻", "👨‍💻" },
            { new string('a', 63) + "b", new string('a', 63) + "c" },
            { new string('a', 128) + "xyz", new string('a', 128) + "xzy" },
            { new string('a', 1_024), new string('b', 1_024) },
        };

    [Theory]
    [MemberData(nameof(GoldenPairs))]
    public void RatioMatchesFuzzySharp202(string left, string right)
    {
        var expected = OriginalFuzz.Ratio(left, right);

        Assert.Equal(expected, Fuzz.Ratio(left.AsSpan(), right.AsSpan()));
        Assert.Equal(expected, new CachedRatio(left).Score(right.AsSpan()));
    }

    [Fact]
    public void RatioMatchesFuzzySharpOnDeterministicRandomCorpus()
    {
        var random = new Random(0xC0FFEE);
        for (var iteration = 0; iteration < 2_000; iteration++)
        {
            var left = RandomString(random, random.Next(0, 180));
            var right = RandomString(random, random.Next(0, 180));
            var expected = OriginalFuzz.Ratio(left, right);

            Assert.Equal(expected, Fuzz.Ratio(left.AsSpan(), right.AsSpan()));
            Assert.Equal(expected, new CachedRatio(left).Score(right.AsSpan()));
        }
    }

    [Theory]
    [InlineData("kitten", "sitting", 63, 0)]
    [InlineData("kitten", "sitting", 62, 62)]
    [InlineData("identical", "identical", 100, 100)]
    [InlineData("abc", "xyz", 1, 0)]
    public void ScoreCutoffReturnsZeroBelowThreshold(
        string left,
        string right,
        int cutoff,
        int expected
    )
    {
        Assert.Equal(expected, Fuzz.Ratio(left.AsSpan(), right.AsSpan(), cutoff));
        Assert.Equal(expected, new CachedRatio(left).Score(right.AsSpan(), cutoff));
    }

    [Fact]
    public void OrdinaryShortHotPathsAllocateZeroBytesAfterWarmup()
    {
        const string left = "high performance fuzzy search";
        const string right = "high performnce fuzzy search";
        var cached = new CachedRatio(left);
        _ = Fuzz.Ratio(left.AsSpan(), right.AsSpan());
        _ = cached.Score(right.AsSpan());

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < 10_000; iteration++)
        {
            _ = Fuzz.Ratio(left.AsSpan(), right.AsSpan());
            _ = cached.Score(right.AsSpan());
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void CachedLongHotPathAllocatesZeroBytesAfterWarmup()
    {
        var left = new string('a', 1_024) + new string('b', 128);
        var right = new string('a', 1_024) + new string('c', 128);
        var cached = new CachedRatio(left);
        _ = cached.Score(right.AsSpan());

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < 1_000; iteration++)
        {
            _ = cached.Score(right.AsSpan());
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    private static string RandomString(Random random, int length) =>
        string.Create(
            length,
            random,
            static (span, state) =>
            {
                const string alphabet = "abcXYZ019 -_Ж中λ";
                for (var index = 0; index < span.Length; index++)
                {
                    span[index] = alphabet[state.Next(alphabet.Length)];
                }
            }
        );
}

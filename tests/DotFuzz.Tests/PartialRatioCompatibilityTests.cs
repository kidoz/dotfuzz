using OriginalFuzz = FuzzySharp.Fuzz;

namespace DotFuzz.Tests;

public sealed class PartialRatioCompatibilityTests
{
    public static TheoryData<string, string> GoldenPairs =>
        new()
        {
            { "", "" },
            { "", "nonempty" },
            { "abc", "abc" },
            { "similar", "somewhat similar" },
            { "mysmilarstring", "myawfullysimilarstirng" },
            { "fuzzy wuzzy was a bear", "wuzzy fuzzy was a bear" },
            { "this is a test", "this is a test!" },
            { "New York Mets", "new york mets" },
            { "yankees", "new york yankees" },
            { "new york mets vs atlanta braves", "atlanta braves vs new york mets" },
            { "Москва", "город Масква" },
            { "東京駅", "東京" },
            { "aaaa", "bbbb" },
            { new string('a', 63) + "b", "xx" + new string('a', 63) + "c" },
            { new string('a', 128) + "xyz", new string('a', 200) + "xzy" },
            { new string('a', 300), new string('b', 100) + new string('a', 300) },
        };

    [Theory]
    [MemberData(nameof(GoldenPairs))]
    public void PartialRatioMatchesFuzzySharp202(string left, string right)
    {
        var expected = OriginalFuzz.PartialRatio(left, right);
        Assert.Equal(expected, Fuzz.PartialRatio(left.AsSpan(), right.AsSpan()));
    }

    [Fact]
    public void PartialRatioMatchesFuzzySharpOnDeterministicRandomCorpus()
    {
        var random = new Random(0xBADF00D);
        for (var iteration = 0; iteration < 2_000; iteration++)
        {
            var left = RandomString(random, random.Next(0, 160));
            var right = RandomString(random, random.Next(0, 160));
            var expected = OriginalFuzz.PartialRatio(left, right);

            Assert.Equal(expected, Fuzz.PartialRatio(left.AsSpan(), right.AsSpan()));
        }
    }

    [Fact]
    public void ScoreCutoffKeepsExactScoresAndRejectsBelowThreshold()
    {
        var random = new Random(0x5EED);
        for (var iteration = 0; iteration < 500; iteration++)
        {
            var left = RandomString(random, random.Next(1, 60));
            var right = RandomString(random, random.Next(1, 60));
            var cutoff = random.Next(0, 102);
            var uncut = Fuzz.PartialRatio(left.AsSpan(), right.AsSpan());
            var expected = uncut >= cutoff && cutoff <= 100 ? uncut : 0;

            Assert.Equal(expected, Fuzz.PartialRatio(left.AsSpan(), right.AsSpan(), cutoff));
        }
    }

    [Fact]
    public void NullStringsThrow()
    {
        Assert.Throws<ArgumentNullException>(() => Fuzz.PartialRatio(null!, "x"));
        Assert.Throws<ArgumentNullException>(() => Fuzz.PartialRatio("x", null!));
    }

    [Fact]
    public void ShortHotPathAllocatesZeroBytesAfterWarmup()
    {
        const string left = "fuzzy search";
        const string right = "high performance fuzzy search";
        _ = Fuzz.PartialRatio(left.AsSpan(), right.AsSpan());

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < 10_000; iteration++)
        {
            _ = Fuzz.PartialRatio(left.AsSpan(), right.AsSpan());
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void PooledMediumPathAllocatesZeroBytesAfterWarmup()
    {
        var left = new string('a', 100) + new string('b', 28);
        var right = new string('a', 140) + new string('c', 30);
        _ = Fuzz.PartialRatio(left.AsSpan(), right.AsSpan());

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < 1_000; iteration++)
        {
            _ = Fuzz.PartialRatio(left.AsSpan(), right.AsSpan());
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

using FuzzySharp.PreProcess;
using OriginalFuzz = FuzzySharp.Fuzz;

namespace DotFuzz.Tests;

public sealed class TokenRatioCompatibilityTests
{
    public static TheoryData<string, string> GoldenPairs =>
        new()
        {
            { "", "" },
            { "", "nonempty" },
            { "   ", "\t\n" },
            { "fuzzy wuzzy was a bear", "wuzzy fuzzy was a bear" },
            { "fuzzy was a bear", "fuzzy fuzzy was a bear" },
            { "new york mets vs atlanta braves", "atlanta braves vs new york mets" },
            { "the quick brown fox", "quick fox brown the the" },
            { "a b c", "c b a" },
            { "1 2 33 4", "4 33 2 1 0" },
            { "single", "single token" },
            { "token  double  spaces", "double token spaces" },
        };

    [Theory]
    [MemberData(nameof(GoldenPairs))]
    public void TokenScorersMatchFuzzySharp202(string left, string right)
    {
        Assert.Equal(
            OriginalFuzz.TokenSortRatio(left, right),
            Fuzz.TokenSortRatio(left.AsSpan(), right.AsSpan())
        );
        Assert.Equal(
            OriginalFuzz.PartialTokenSortRatio(left, right),
            Fuzz.PartialTokenSortRatio(left.AsSpan(), right.AsSpan())
        );
        Assert.Equal(
            OriginalFuzz.TokenSetRatio(left, right),
            Fuzz.TokenSetRatio(left.AsSpan(), right.AsSpan())
        );
        Assert.Equal(
            OriginalFuzz.PartialTokenSetRatio(left, right),
            Fuzz.PartialTokenSetRatio(left.AsSpan(), right.AsSpan())
        );
    }

    [Fact]
    public void TokenScorersMatchFuzzySharpOnDeterministicRandomCorpus()
    {
        // Lowercase alphanumeric tokens keep FuzzySharp's culture-sensitive token
        // ordering identical to DotFuzz's ordinal ordering.
        var random = new Random(0x70CE1);
        for (var iteration = 0; iteration < 1_500; iteration++)
        {
            var left = RandomTokenString(random, random.Next(0, 90));
            var right = RandomTokenString(random, random.Next(0, 90));

            Assert.Equal(
                OriginalFuzz.TokenSortRatio(left, right),
                Fuzz.TokenSortRatio(left.AsSpan(), right.AsSpan())
            );
            Assert.Equal(
                OriginalFuzz.PartialTokenSortRatio(left, right),
                Fuzz.PartialTokenSortRatio(left.AsSpan(), right.AsSpan())
            );
            Assert.Equal(
                OriginalFuzz.TokenSetRatio(left, right),
                Fuzz.TokenSetRatio(left.AsSpan(), right.AsSpan())
            );
            Assert.Equal(
                OriginalFuzz.PartialTokenSetRatio(left, right),
                Fuzz.PartialTokenSetRatio(left.AsSpan(), right.AsSpan())
            );
        }
    }

    [Fact]
    public void PreprocessedTokenScoringMatchesFuzzySharpFullMode()
    {
        var random = new Random(0xFACADE);
        for (var iteration = 0; iteration < 1_000; iteration++)
        {
            var left = RandomRawString(random, random.Next(0, 90));
            var right = RandomRawString(random, random.Next(0, 90));
            var processedLeft = Preprocess.Compatibility(left);
            var processedRight = Preprocess.Compatibility(right);

            AssertScoreCompatible(
                OriginalFuzz.TokenSortRatio(left, right, PreprocessMode.Full),
                Fuzz.TokenSortRatio(processedLeft, processedRight)
            );
            AssertScoreCompatible(
                OriginalFuzz.TokenSetRatio(left, right, PreprocessMode.Full),
                Fuzz.TokenSetRatio(processedLeft, processedRight)
            );
        }
    }

    /// <summary>
    /// FuzzySharp rounds a double quotient, so an exact .5 midpoint can land one
    /// step away from DotFuzz's exact ties-to-even score, which is always even at
    /// a midpoint. Any other difference is a real incompatibility.
    /// </summary>
    private static void AssertScoreCompatible(int fuzzySharpScore, int dotFuzzScore)
    {
        if (fuzzySharpScore == dotFuzzScore)
        {
            return;
        }

        Assert.True(
            Math.Abs(fuzzySharpScore - dotFuzzScore) == 1 && dotFuzzScore % 2 == 0,
            $"FuzzySharp scored {fuzzySharpScore} but DotFuzz scored {dotFuzzScore}."
        );
    }

    [Fact]
    public void ScoreCutoffKeepsExactScoresAndRejectsBelowThreshold()
    {
        var random = new Random(0xCAB);
        for (var iteration = 0; iteration < 400; iteration++)
        {
            var left = RandomTokenString(random, random.Next(0, 50));
            var right = RandomTokenString(random, random.Next(0, 50));
            var cutoff = random.Next(0, 102);

            AssertCutoffContract(
                Fuzz.TokenSortRatio(left, right),
                cutoff,
                () => Fuzz.TokenSortRatio(left.AsSpan(), right.AsSpan(), cutoff)
            );
            AssertCutoffContract(
                Fuzz.PartialTokenSortRatio(left, right),
                cutoff,
                () => Fuzz.PartialTokenSortRatio(left.AsSpan(), right.AsSpan(), cutoff)
            );
            AssertCutoffContract(
                Fuzz.TokenSetRatio(left, right),
                cutoff,
                () => Fuzz.TokenSetRatio(left.AsSpan(), right.AsSpan(), cutoff)
            );
            AssertCutoffContract(
                Fuzz.PartialTokenSetRatio(left, right),
                cutoff,
                () => Fuzz.PartialTokenSetRatio(left.AsSpan(), right.AsSpan(), cutoff)
            );
        }
    }

    [Fact]
    public void NullStringsThrow()
    {
        Assert.Throws<ArgumentNullException>(() => Fuzz.TokenSortRatio(null!, "x"));
        Assert.Throws<ArgumentNullException>(() => Fuzz.PartialTokenSortRatio("x", null!));
        Assert.Throws<ArgumentNullException>(() => Fuzz.TokenSetRatio(null!, "x"));
        Assert.Throws<ArgumentNullException>(() => Fuzz.PartialTokenSetRatio("x", null!));
    }

    [Fact]
    public void ShortTokenHotPathsAllocateZeroBytesAfterWarmup()
    {
        const string left = "fuzzy wuzzy was a bear";
        const string right = "wuzzy fuzzy hugged a bear";
        _ = Fuzz.TokenSortRatio(left.AsSpan(), right.AsSpan());
        _ = Fuzz.TokenSetRatio(left.AsSpan(), right.AsSpan());

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < 10_000; iteration++)
        {
            _ = Fuzz.TokenSortRatio(left.AsSpan(), right.AsSpan());
            _ = Fuzz.TokenSetRatio(left.AsSpan(), right.AsSpan());
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    private static void AssertCutoffContract(int uncut, int cutoff, Func<int> scoreWithCutoff)
    {
        var expected = uncut >= cutoff && cutoff <= 100 ? uncut : 0;
        Assert.Equal(expected, scoreWithCutoff());
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

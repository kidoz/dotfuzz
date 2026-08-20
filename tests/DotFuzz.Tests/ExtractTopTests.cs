using DotFuzz.Cached;

namespace DotFuzz.Tests;

public sealed class ExtractTopTests
{
    private static readonly string[] Teams =
    [
        "Atlanta Falcons",
        "New York Jets",
        "New York Giants",
        "Dallas Cowboys",
        "New York Mets",
    ];

    [Fact]
    public void ReturnsResultsSortedByDescendingScore()
    {
        var results = Process.ExtractTop("New York Jets", Teams, limit: 3);

        Assert.Equal(3, results.Length);
        Assert.Equal(new ExtractResult("New York Jets", 100, 1), results[0]);
        Assert.True(results[0].Score >= results[1].Score);
        Assert.True(results[1].Score >= results[2].Score);
    }

    [Fact]
    public void EqualScoresKeepEarlierChoiceFirst()
    {
        string[] choices = ["same", "same", "same"];
        var results = Process.ExtractTop("same", choices, limit: 2);

        Assert.Equal(2, results.Length);
        Assert.Equal(new ExtractResult("same", 100, 0), results[0]);
        Assert.Equal(new ExtractResult("same", 100, 1), results[1]);
    }

    [Fact]
    public void CutoffExcludesLowScores()
    {
        var results = Process.ExtractTop("New York Jets", Teams, limit: 5, scoreCutoff: 90);

        Assert.Equal(2, results.Length);
        Assert.Equal("New York Jets", results[0].Value);
        Assert.Equal("New York Mets", results[1].Value);
    }

    [Fact]
    public void LimitLargerThanChoicesReturnsAllQualifying()
    {
        var results = Process.ExtractTop("new york jets", Teams, limit: 100);

        Assert.Equal(Teams.Length, results.Length);
    }

    [Fact]
    public void ZeroLimitReturnsEmptyAndNegativeLimitThrows()
    {
        Assert.Empty(Process.ExtractTop("query", Teams, limit: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Process.ExtractTop("query", Teams, limit: -1)
        );
    }

    [Fact]
    public void MatchesStableSortOfAllScoresOnDeterministicRandomCorpus()
    {
        var random = new Random(0x7E57);
        for (var iteration = 0; iteration < 200; iteration++)
        {
            var query = RandomString(random, random.Next(1, 20));
            var choices = new string[random.Next(1, 30)];
            for (var index = 0; index < choices.Length; index++)
            {
                choices[index] = RandomString(random, random.Next(0, 20));
            }

            var cutoff = random.Next(0, 60);
            var limit = random.Next(1, 8);
            var scorer = new CachedRatio(query);

            var expected = choices
                .Select(
                    (choice, index) =>
                        new ExtractResult(choice, scorer.Score(choice.AsSpan()), index)
                )
                .Where(result => result.Score >= cutoff)
                .OrderByDescending(result => result.Score)
                .ThenBy(result => result.Index)
                .Take(limit)
                .ToArray();

            var actual = Process.ExtractTop(scorer, choices, limit, cutoff);

            Assert.Equal(expected, actual);
        }
    }

    [Fact]
    public void SpanDestinationScanAllocatesZeroBytesAfterWarmup()
    {
        var scorer = new CachedRatio("new york jets");
        Span<ExtractResult> destination = new ExtractResult[3];
        _ = Process.ExtractTop(scorer, Teams, destination);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < 5_000; iteration++)
        {
            _ = Process.ExtractTop(scorer, Teams, destination);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void ExtractAllReturnsQualifyingChoicesInChoiceOrder()
    {
        var results = Process.ExtractAll("new york jets", Teams, scoreCutoff: 50);

        Assert.Equal(3, results.Length);
        Assert.Equal(
            ["New York Jets", "New York Giants", "New York Mets"],
            results.Select(result => result.Value).ToArray()
        );
        Assert.True(results[0].Index < results[1].Index);
        Assert.True(results[1].Index < results[2].Index);
    }

    [Fact]
    public void ExtractAllWithZeroCutoffReturnsEveryChoice()
    {
        var results = Process.ExtractAll("query", Teams);

        Assert.Equal(Teams.Length, results.Length);
    }

    [Fact]
    public void ExtractAllSpanDestinationTooSmallThrows()
    {
        var scorer = new CachedRatio("query");
        Assert.Throws<ArgumentException>(() =>
        {
            Span<ExtractResult> destination = new ExtractResult[2];
            return Process.ExtractAll(scorer, Teams, destination);
        });
    }

    [Fact]
    public void NullChoicesThrow()
    {
        var scorer = new CachedRatio("query");
        string[] withNull = ["ok", null!];

        Assert.Throws<ArgumentException>(() => Process.ExtractTop(scorer, withNull, limit: 2));
        Assert.Throws<ArgumentException>(() => Process.ExtractAll(scorer, withNull));
    }

    private static string RandomString(Random random, int length) =>
        string.Create(
            length,
            random,
            static (span, state) =>
            {
                const string alphabet = "abcde ";
                for (var index = 0; index < span.Length; index++)
                {
                    span[index] = alphabet[state.Next(alphabet.Length)];
                }
            }
        );
}

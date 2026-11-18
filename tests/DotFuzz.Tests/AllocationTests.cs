using DotFuzz.Cached;
using DotFuzz.Internal;

namespace DotFuzz.Tests;

// Parallel suite runs can move the per-thread byte counter even when the
// profiler observes no object allocation inside the measured call.
[CollectionDefinition(DisableParallelization = true)]
[Collection<AllocationTests>]
public sealed class AllocationTests
{
    [Fact]
    public void ShortRatioHotPathsAllocateZeroBytesAfterWarmup()
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

    [Fact]
    public void ShortPartialRatioAllocatesZeroBytesAfterWarmup()
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
    public void PooledPartialRatioAllocatesZeroBytesAfterWarmup()
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

    [Fact]
    public void EmptyTrimmedMiddleScoringAllocatesNothingAfterWarmup()
    {
        const string shorter = "hello";
        var tail = new string('x', 1_048_576);
        var prefixMatch = shorter + tail;
        var suffixMatch = tail + shorter;
        _ = Fuzz.PartialRatio(shorter, prefixMatch);
        _ = Fuzz.PartialRatio(suffixMatch, shorter);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < 1_000; iteration++)
        {
            _ = Fuzz.PartialRatio(shorter, prefixMatch);
            _ = Fuzz.PartialRatio(suffixMatch, shorter);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Theory]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(1024)]
    public void TokenSortOrdersRangesOrdinallyWithoutAllocations(int count)
    {
        string[] vocabulary = ["z", "zeta", "東京", "Alpha", "al", "a", "𐐀"];
        var tokens = Enumerable.Range(0, count).Select(i => vocabulary[i % vocabulary.Length]);
        var descending = tokens.OrderDescending(StringComparer.Ordinal).ToArray();
        var text = string.Join(' ', descending);
        var expected = string.Join(' ', descending.Reverse());
        var starts = new int[count];
        var lengths = new int[count];
        var output = new char[text.Length];

        _ = TokenOps.WriteSortedJoined(text, starts, lengths, output);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var written = TokenOps.WriteSortedJoined(text, starts, lengths, output);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Equal(expected, new string(output, 0, written));
        Assert.Equal(100, Fuzz.TokenSortRatio(text, expected));
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

    [Fact]
    public void PreprocessSpanHotPathsAllocateZeroBytesAfterWarmup()
    {
        const string input = "  Fast, Allocation-Free Preprocessing 101!  ";
        var buffer = new char[input.Length];
        _ = Preprocess.Compatibility(input.AsSpan(), buffer);
        _ = Preprocess.Unicode(input.AsSpan(), buffer);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < 10_000; iteration++)
        {
            _ = Preprocess.Compatibility(input.AsSpan(), buffer);
            _ = Preprocess.Unicode(input.AsSpan(), buffer);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void CachedExtractOneScanAllocatesZeroBytesAfterWarmup()
    {
        var choices = ExtractOneTests.Choices;
        var scorer = new CachedRatio("New York Giants");
        _ = Process.ExtractOne(scorer, choices);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < 10_000; iteration++)
        {
            _ = Process.ExtractOne(scorer, choices);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void ImpossibleCutoffReturnsEmptyWithoutAllocating()
    {
        var scorer = new CachedRatio("query");
        var choices = new string[1024];
        Array.Fill(choices, "query");
        _ = Process.ExtractTop(scorer, choices, choices.Length, scoreCutoff: 101);

        var before = GC.GetAllocatedBytesForCurrentThread();
        var results = Process.ExtractTop(scorer, choices, choices.Length, scoreCutoff: 101);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Empty(results);
        Assert.Equal(0, allocated);
    }

    [Fact]
    public void ExtractTopSpanDestinationAllocatesZeroBytesAfterWarmup()
    {
        var choices = ExtractTopTests.Teams;
        var scorer = new CachedRatio("new york jets");
        Span<ExtractResult> destination = new ExtractResult[3];
        _ = Process.ExtractTop(scorer, choices, destination);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < 5_000; iteration++)
        {
            _ = Process.ExtractTop(scorer, choices, destination);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
}

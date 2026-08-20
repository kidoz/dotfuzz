using DotFuzz.Cached;

namespace DotFuzz.Tests;

public sealed class ExtractOneTests
{
    private static readonly string[] Choices =
    [
        "Atlanta Falcons",
        "New York Jets",
        "New York Giants",
        "Dallas Cowboys",
    ];

    [Fact]
    public void ExtractOneReturnsFirstBestRatioMatch()
    {
        var result = Process.ExtractOne("cowboys", Choices);

        Assert.True(result.Found);
        Assert.Equal("Dallas Cowboys", result.Value);
        Assert.Equal(3, result.Index);
        Assert.Equal(Fuzz.Ratio("cowboys", "Dallas Cowboys"), result.Score);
    }

    [Fact]
    public void ExtractOnePreservesFirstChoiceOnTie()
    {
        string[] choices = ["same", "same", "different"];

        Assert.Equal(0, Process.ExtractOne("same", choices).Index);
    }

    [Fact]
    public void ExtractOneHonorsCutoffAndNoneShape()
    {
        var result = Process.ExtractOne("unrelated", Choices, 95);

        Assert.False(result.Found);
        Assert.Equal(-1, result.Index);
        Assert.Null(result.Value);
        Assert.Equal(0, result.Score);
    }

    [Fact]
    public void CachedExtractOneScanAllocatesZeroBytesAfterWarmup()
    {
        var scorer = new CachedRatio("New York Giants");
        _ = Process.ExtractOne(scorer, Choices);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < 10_000; iteration++)
        {
            _ = Process.ExtractOne(scorer, Choices);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
}

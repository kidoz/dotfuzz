using DotFuzz.Internal;

namespace DotFuzz.Tests;

public sealed class InputLimitTests
{
    [Theory]
    [InlineData(1_024, 1_024)]
    [InlineData(10_000, 10_000)]
    [InlineData(46_340, 46_340)]
    [InlineData(1, 1_048_576)]
    public void PartialRatioRejectsOversizedMatricesBeforeAllocation(
        int leftLength,
        int rightLength
    )
    {
        var left = new string('a', leftLength);
        var right = new string('b', rightLength);

        Assert.Throws<ArgumentOutOfRangeException>(() => Fuzz.PartialRatio(left, right));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Fuzz.PartialRatio(right.AsSpan(), left.AsSpan())
        );
    }

    [Fact]
    public void MatrixLimitAlsoAppliesToCompositeScorers()
    {
        var left = new string('a', 1_024);
        var right = new string('b', 1_536);

        Assert.Throws<ArgumentOutOfRangeException>(() => Fuzz.PartialTokenSortRatio(left, right));
        Assert.Throws<ArgumentOutOfRangeException>(() => Fuzz.PartialTokenSetRatio(left, right));
        Assert.Throws<ArgumentOutOfRangeException>(() => Fuzz.WeightedRatio(left, right));

        // Ordinary Ratio and the non-partial weighted branch have no matrix limit.
        Assert.Equal(0, Fuzz.Ratio(left, right));
        Assert.Equal(0, Fuzz.WeightedRatio(left, new string('b', left.Length)));
    }

    [Fact]
    public void PartialRatioAcceptsTheMatrixCellBoundary()
    {
        // (1,023 + 1)^2 is exactly the 1,048,576-cell limit.
        var left = new string('a', 1_023);
        var right = new string('b', 1_023);
        Assert.Equal(FuzzySharp.Fuzz.PartialRatio(left, right), Fuzz.PartialRatio(left, right));
    }

    [Fact]
    public void CommonAffixesAreTrimmedBeforeApplyingTheMatrixLimit()
    {
        var affix = new string('x', 10_000);
        var left = affix + "a" + affix;
        var right = affix + "b" + affix;

        Assert.Equal(100, Fuzz.PartialRatio(left, right));
        Assert.Equal(100, Fuzz.PartialRatio(left, left));
    }

    [Fact]
    public void EmptyInputsAndImpossibleCutoffsDoNotNeedAnAlignmentMatrix()
    {
        var text = new string('a', 10_000);
        Assert.Equal(0, Fuzz.PartialRatio(text, ""));
        Assert.Equal(0, Fuzz.PartialRatio(text, new string('b', text.Length), 101));
    }

    [Theory]
    [InlineData(int.MaxValue, 0)]
    [InlineData(int.MaxValue - 1, 1)]
    [InlineData(1_073_741_824, 1_073_741_823)]
    public void CombinedLengthsFitAtTheIntegerBoundary(int leftLength, int rightLength)
    {
        Assert.Equal(int.MaxValue, ScoreMath.TotalLength(leftLength, rightLength));
    }

    [Theory]
    [InlineData(int.MaxValue, 1)]
    [InlineData(1_073_741_824, 1_073_741_824)]
    public void CombinedLengthsCannotWrap(int leftLength, int rightLength)
    {
        // Exercise length arithmetic without allocating multi-gigabyte strings.
        Assert.Throws<OverflowException>(() => ScoreMath.TotalLength(leftLength, rightLength));
    }

    [Fact]
    public void EditOperationBufferLengthCannotWrap()
    {
        Assert.Equal(2_147_483_646, PartialRatioCore.EditOperationBufferLength(int.MaxValue / 3));
        Assert.Throws<OverflowException>(() =>
            PartialRatioCore.EditOperationBufferLength((int.MaxValue / 3) + 1)
        );
    }

    [Fact]
    public void MaximumTokenCountCannotWrap()
    {
        Assert.Equal(1_073_741_824, TokenOps.MaximumTokenCount(int.MaxValue));
        Assert.Equal(1_073_741_823, TokenOps.MaximumTokenCount(int.MaxValue - 1));
    }
}

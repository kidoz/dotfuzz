using System.Buffers;

namespace DotFuzz.Tests;

public sealed class PooledTextBufferTests
{
    [Fact]
    public void PreprocessorsClearReturnedTextBuffers()
    {
        var input = new string('A', 600);

        Assert.Equal(new string('a', input.Length), Preprocess.Compatibility(input));
        AssertReturnedBufferIsClear(input.Length);

        Assert.Equal(new string('a', input.Length), Preprocess.Unicode(input));
        AssertReturnedBufferIsClear(input.Length);
    }

    [Fact]
    public void TokenSortClearsBothReturnedTextBuffers()
    {
        // Different pool buckets allow each returned buffer to be checked on
        // this thread, without racing another renter for a shared bucket slot.
        var left = new string('a', 600);
        var right = new string('b', 1_500);

        Assert.Equal(0, Fuzz.TokenSortRatio(left, right));
        AssertReturnedBufferIsClear(left.Length);
        AssertReturnedBufferIsClear(right.Length);
    }

    [Fact]
    public void TokenSetClearsItsReturnedTextBuffer()
    {
        var left = new string('a', 600);
        var right = new string('b', 1_500);

        Assert.Equal(0, Fuzz.TokenSetRatio(left, right));
        AssertReturnedBufferIsClear(
            Math.Min(left.Length, right.Length) + left.Length + right.Length
        );
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PartialTokenScorersClearTextBuffersWhenAlignmentThrows(bool tokenSet)
    {
        var left = new string('a', 1_024);
        var right = new string('b', 1_536);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            tokenSet
                ? Fuzz.PartialTokenSetRatio(left, right)
                : Fuzz.PartialTokenSortRatio(left, right)
        );

        if (tokenSet)
        {
            AssertReturnedBufferIsClear(2 * left.Length + right.Length);
        }
        else
        {
            AssertReturnedBufferIsClear(left.Length);
            AssertReturnedBufferIsClear(right.Length);
        }
    }

    private static void AssertReturnedBufferIsClear(int minimumLength)
    {
        // SharedArrayPool keeps the last returned array per size bucket in
        // thread-local storage. Rent synchronously on the returning thread.
        var buffer = ArrayPool<char>.Shared.Rent(minimumLength);
        try
        {
            Assert.True(buffer.AsSpan().IndexOfAnyExcept('\0') < 0);
        }
        finally
        {
            ArrayPool<char>.Shared.Return(buffer, clearArray: true);
        }
    }
}

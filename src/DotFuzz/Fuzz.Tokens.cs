using System.Buffers;
using DotFuzz.Internal;

namespace DotFuzz;

public static partial class Fuzz
{
    /// <summary>
    /// Sorts each input's whitespace-separated tokens ordinally, joins them with
    /// single spaces, and returns the <see cref="Ratio(ReadOnlySpan{char}, ReadOnlySpan{char}, int)"/>
    /// of the joined strings. Values below <paramref name="scoreCutoff"/> return zero.
    /// </summary>
    /// <remarks>
    /// No preprocessing is applied; pair with <see cref="Preprocess"/> to reproduce
    /// FuzzySharp's default token matching. Token order is ordinal, which matches
    /// FuzzySharp's culture-sensitive ordering for preprocessed lowercase
    /// alphanumeric input. An input with no tokens scores zero.
    /// </remarks>
    public static int TokenSortRatio(
        ReadOnlySpan<char> left,
        ReadOnlySpan<char> right,
        int scoreCutoff = 0
    ) => TokenSortCore(left, right, scoreCutoff, partial: false);

    /// <inheritdoc cref="TokenSortRatio(ReadOnlySpan{char}, ReadOnlySpan{char}, int)"/>
    public static int TokenSortRatio(string left, string right, int scoreCutoff = 0)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        return TokenSortRatio(left.AsSpan(), right.AsSpan(), scoreCutoff);
    }

    /// <summary>
    /// Token-sort variant of <see cref="PartialRatio(ReadOnlySpan{char}, ReadOnlySpan{char}, int)"/>:
    /// scores the sorted token joins with best-partial alignment.
    /// </summary>
    /// <inheritdoc cref="TokenSortRatio(ReadOnlySpan{char}, ReadOnlySpan{char}, int)" path="/remarks"/>
    public static int PartialTokenSortRatio(
        ReadOnlySpan<char> left,
        ReadOnlySpan<char> right,
        int scoreCutoff = 0
    ) => TokenSortCore(left, right, scoreCutoff, partial: true);

    /// <inheritdoc cref="PartialTokenSortRatio(ReadOnlySpan{char}, ReadOnlySpan{char}, int)"/>
    public static int PartialTokenSortRatio(string left, string right, int scoreCutoff = 0)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        return PartialTokenSortRatio(left.AsSpan(), right.AsSpan(), scoreCutoff);
    }

    /// <summary>
    /// Splits both inputs into unique whitespace-separated tokens and returns the
    /// best <see cref="Ratio(ReadOnlySpan{char}, ReadOnlySpan{char}, int)"/> among the
    /// sorted intersection and the intersection-plus-remainder combinations.
    /// Values below <paramref name="scoreCutoff"/> return zero.
    /// </summary>
    /// <inheritdoc cref="TokenSortRatio(ReadOnlySpan{char}, ReadOnlySpan{char}, int)" path="/remarks"/>
    public static int TokenSetRatio(
        ReadOnlySpan<char> left,
        ReadOnlySpan<char> right,
        int scoreCutoff = 0
    ) => TokenSetCore(left, right, scoreCutoff, partial: false);

    /// <inheritdoc cref="TokenSetRatio(ReadOnlySpan{char}, ReadOnlySpan{char}, int)"/>
    public static int TokenSetRatio(string left, string right, int scoreCutoff = 0)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        return TokenSetRatio(left.AsSpan(), right.AsSpan(), scoreCutoff);
    }

    /// <summary>
    /// Token-set variant of <see cref="PartialRatio(ReadOnlySpan{char}, ReadOnlySpan{char}, int)"/>:
    /// scores the token-set combinations with best-partial alignment.
    /// </summary>
    /// <inheritdoc cref="TokenSortRatio(ReadOnlySpan{char}, ReadOnlySpan{char}, int)" path="/remarks"/>
    public static int PartialTokenSetRatio(
        ReadOnlySpan<char> left,
        ReadOnlySpan<char> right,
        int scoreCutoff = 0
    ) => TokenSetCore(left, right, scoreCutoff, partial: true);

    /// <inheritdoc cref="PartialTokenSetRatio(ReadOnlySpan{char}, ReadOnlySpan{char}, int)"/>
    public static int PartialTokenSetRatio(string left, string right, int scoreCutoff = 0)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        return PartialTokenSetRatio(left.AsSpan(), right.AsSpan(), scoreCutoff);
    }

    private static int TokenSortCore(
        ReadOnlySpan<char> left,
        ReadOnlySpan<char> right,
        int scoreCutoff,
        bool partial
    )
    {
        if (scoreCutoff > 100)
        {
            return 0;
        }

        int[]? rentedRanges = null;
        char[]? rentedLeftBuffer = null;
        char[]? rentedRightBuffer = null;
        try
        {
            var leftCapacity = TokenOps.MaximumTokenCount(left.Length);
            var rightCapacity = TokenOps.MaximumTokenCount(right.Length);
            var rangeInts = 2 * (leftCapacity + rightCapacity);
            Span<int> ranges =
                rangeInts <= 2 * TokenOps.StackTextLength
                    ? stackalloc int[2 * TokenOps.StackTextLength]
                    : (rentedRanges = ArrayPool<int>.Shared.Rent(rangeInts)).AsSpan();

            Span<char> leftBuffer =
                left.Length <= TokenOps.StackTextLength
                    ? stackalloc char[TokenOps.StackTextLength]
                    : (rentedLeftBuffer = ArrayPool<char>.Shared.Rent(left.Length)).AsSpan();
            Span<char> rightBuffer =
                right.Length <= TokenOps.StackTextLength
                    ? stackalloc char[TokenOps.StackTextLength]
                    : (rentedRightBuffer = ArrayPool<char>.Shared.Rent(right.Length)).AsSpan();

            var leftLength = TokenOps.WriteSortedJoined(
                left,
                ranges[..leftCapacity],
                ranges.Slice(leftCapacity, leftCapacity),
                leftBuffer
            );
            var offset = 2 * leftCapacity;
            var rightLength = TokenOps.WriteSortedJoined(
                right,
                ranges.Slice(offset, rightCapacity),
                ranges.Slice(offset + rightCapacity, rightCapacity),
                rightBuffer
            );

            var sortedLeft = (ReadOnlySpan<char>)leftBuffer[..leftLength];
            var sortedRight = (ReadOnlySpan<char>)rightBuffer[..rightLength];
            return partial
                ? PartialRatio(sortedLeft, sortedRight, scoreCutoff)
                : Ratio(sortedLeft, sortedRight, scoreCutoff);
        }
        finally
        {
            if (rentedRanges is not null)
            {
                ArrayPool<int>.Shared.Return(rentedRanges);
            }

            if (rentedLeftBuffer is not null)
            {
                ArrayPool<char>.Shared.Return(rentedLeftBuffer);
            }

            if (rentedRightBuffer is not null)
            {
                ArrayPool<char>.Shared.Return(rentedRightBuffer);
            }
        }
    }

    private static int TokenSetCore(
        ReadOnlySpan<char> left,
        ReadOnlySpan<char> right,
        int scoreCutoff,
        bool partial
    )
    {
        if (scoreCutoff > 100)
        {
            return 0;
        }

        int[]? rentedInts = null;
        char[]? rentedChars = null;
        try
        {
            var leftCapacity = TokenOps.MaximumTokenCount(left.Length);
            var rightCapacity = TokenOps.MaximumTokenCount(right.Length);

            // Ranges for both sides plus the intersection/remainder index lists.
            var rangeInts = 2 * (leftCapacity + rightCapacity);
            var listInts = 2 * (leftCapacity + rightCapacity);
            Span<int> ints =
                rangeInts + listInts <= 4 * TokenOps.StackTextLength
                    ? stackalloc int[4 * TokenOps.StackTextLength]
                    : (rentedInts = ArrayPool<int>.Shared.Rent(rangeInts + listInts)).AsSpan();

            var leftStarts = ints[..leftCapacity];
            var leftLengths = ints.Slice(leftCapacity, leftCapacity);
            var rightStarts = ints.Slice(2 * leftCapacity, rightCapacity);
            var rightLengths = ints.Slice((2 * leftCapacity) + rightCapacity, rightCapacity);
            var intersection = ints.Slice(rangeInts, Math.Min(leftCapacity, rightCapacity));
            var leftOnly = ints.Slice(
                rangeInts + Math.Min(leftCapacity, rightCapacity),
                leftCapacity
            );
            var rightOnly = ints.Slice(
                rangeInts + Math.Min(leftCapacity, rightCapacity) + leftCapacity,
                rightCapacity
            );

            var leftCount = TokenOps.Tokenize(left, leftStarts, leftLengths);
            TokenOps.SortOrdinal(left, leftStarts, leftLengths, leftCount);
            leftCount = TokenOps.DedupeSorted(left, leftStarts, leftLengths, leftCount);

            var rightCount = TokenOps.Tokenize(right, rightStarts, rightLengths);
            TokenOps.SortOrdinal(right, rightStarts, rightLengths, rightCount);
            rightCount = TokenOps.DedupeSorted(right, rightStarts, rightLengths, rightCount);

            TokenOps.Classify(
                left,
                leftStarts,
                leftLengths,
                leftCount,
                right,
                rightStarts,
                rightLengths,
                rightCount,
                intersection,
                out var intersectionCount,
                leftOnly,
                out var leftOnlyCount,
                rightOnly,
                out var rightOnlyCount
            );

            // sect | sect + leftOnly | sect + rightOnly, all single-space joins.
            var sectCapacity = Math.Min(left.Length, right.Length);
            var charCount = sectCapacity + left.Length + right.Length;
            Span<char> chars =
                charCount <= 3 * TokenOps.StackTextLength
                    ? stackalloc char[3 * TokenOps.StackTextLength]
                    : (rentedChars = ArrayPool<char>.Shared.Rent(charCount)).AsSpan();

            var sectBuffer = chars[..sectCapacity];
            var combinedLeftBuffer = chars.Slice(sectCapacity, left.Length);
            var combinedRightBuffer = chars.Slice(sectCapacity + left.Length, right.Length);

            var sectLength = TokenOps.AppendJoined(
                left,
                leftStarts,
                leftLengths,
                intersection,
                intersectionCount,
                sectBuffer,
                0
            );
            var combinedLeftLength = TokenOps.AppendJoined(
                left,
                leftStarts,
                leftLengths,
                leftOnly,
                leftOnlyCount,
                combinedLeftBuffer,
                TokenOps.AppendJoined(
                    left,
                    leftStarts,
                    leftLengths,
                    intersection,
                    intersectionCount,
                    combinedLeftBuffer,
                    0
                )
            );
            var combinedRightLength = TokenOps.AppendJoined(
                right,
                rightStarts,
                rightLengths,
                rightOnly,
                rightOnlyCount,
                combinedRightBuffer,
                TokenOps.AppendJoined(
                    left,
                    leftStarts,
                    leftLengths,
                    intersection,
                    intersectionCount,
                    combinedRightBuffer,
                    0
                )
            );

            var sect = (ReadOnlySpan<char>)sectBuffer[..sectLength];
            var combinedLeft = (ReadOnlySpan<char>)combinedLeftBuffer[..combinedLeftLength];
            var combinedRight = (ReadOnlySpan<char>)combinedRightBuffer[..combinedRightLength];

            var best = 0;
            ScorePair(sect, combinedLeft, scoreCutoff, partial, ref best);
            if (best < 100)
            {
                ScorePair(sect, combinedRight, scoreCutoff, partial, ref best);
            }

            if (best < 100)
            {
                ScorePair(combinedLeft, combinedRight, scoreCutoff, partial, ref best);
            }

            return best >= Math.Max(0, scoreCutoff) ? best : 0;
        }
        finally
        {
            if (rentedInts is not null)
            {
                ArrayPool<int>.Shared.Return(rentedInts);
            }

            if (rentedChars is not null)
            {
                ArrayPool<char>.Shared.Return(rentedChars);
            }
        }
    }

    private static void ScorePair(
        ReadOnlySpan<char> left,
        ReadOnlySpan<char> right,
        int scoreCutoff,
        bool partial,
        ref int best
    )
    {
        var needed = Math.Max(scoreCutoff, best + 1);
        var score = partial ? PartialRatio(left, right, needed) : Ratio(left, right, needed);
        if (score > best)
        {
            best = score;
        }
    }
}

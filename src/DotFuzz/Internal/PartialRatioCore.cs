using System.Buffers;
using DotFuzz.Distance;

namespace DotFuzz.Internal;

/// <summary>
/// Best-partial alignment scoring. Candidate windows come from the same
/// Levenshtein edit-op matching blocks that FuzzySharp 2.0.2 uses, so the
/// selected alignments and scores stay behaviorally compatible.
/// </summary>
internal static class PartialRatioCore
{
    private const int StackMatrixCells = 4_096;
    private const int StackEditOps = 512;
    private const int MaximumMatrixCells = 1_048_576;

    private const int Insert = 0;
    private const int Delete = 1;
    private const int Replace = 2;

    public static int Score(ReadOnlySpan<char> left, ReadOnlySpan<char> right, int scoreCutoff)
    {
        _ = ScoreMath.TotalLength(left.Length, right.Length);

        ReadOnlySpan<char> shorter;
        ReadOnlySpan<char> longer;
        if (left.Length < right.Length)
        {
            shorter = left;
            longer = right;
        }
        else
        {
            shorter = right;
            longer = left;
        }

        if (shorter.SequenceEqual(longer))
        {
            return 100;
        }

        var prefixLength = shorter.CommonPrefixLength(longer);
        var middleShorter = shorter[prefixLength..];
        var middleLonger = longer[prefixLength..];

        var suffixLength = 0;
        var maximumSuffixLength = Math.Min(middleShorter.Length, middleLonger.Length);
        while (
            suffixLength < maximumSuffixLength
            && middleShorter[^(suffixLength + 1)] == middleLonger[^(suffixLength + 1)]
        )
        {
            suffixLength++;
        }
        middleShorter = middleShorter[..^suffixLength];
        middleLonger = middleLonger[..^suffixLength];

        if (middleShorter.IsEmpty || middleLonger.IsEmpty)
        {
            // A single insertion needs only the prefix/suffix matching-block
            // windows. Split affixes around an internal insertion need not score 100.
            var prefixScore = Fuzz.Ratio(shorter, longer[..shorter.Length], scoreCutoff);
            return prefixScore == 100
                ? 100
                : Math.Max(
                    prefixScore,
                    Fuzz.Ratio(
                        shorter,
                        longer[^shorter.Length..],
                        Math.Max(scoreCutoff, prefixScore + 1)
                    )
                );
        }

        // Widen before adding or multiplying, and reject before renting workspace.
        var cellCount = ((long)middleShorter.Length + 1) * ((long)middleLonger.Length + 1);
        if (cellCount > MaximumMatrixCells)
        {
            throw new ArgumentOutOfRangeException(
                paramName: null,
                message: "Partial alignment requires more than 1,048,576 matrix cells after common affix trimming."
            );
        }

        var columns = middleLonger.Length + 1;
        var cells = (int)cellCount;

        int[]? rentedMatrix = null;
        int[]? rentedOps = null;
        try
        {
            Span<int> matrix =
                cells <= StackMatrixCells
                    ? stackalloc int[StackMatrixCells]
                    : (rentedMatrix = ArrayPool<int>.Shared.Rent(cells)).AsSpan();
            matrix = matrix[..cells];

            FillCostMatrix(middleShorter, middleLonger, matrix, columns);

            var operationCount = matrix[cells - 1];
            var operationBufferLength = EditOperationBufferLength(operationCount);
            Span<int> ops =
                operationCount <= StackEditOps
                    ? stackalloc int[3 * StackEditOps]
                    : (rentedOps = ArrayPool<int>.Shared.Rent(operationBufferLength)).AsSpan();
            var opTypes = ops[..operationCount];
            var opSources = ops.Slice(operationCount, operationCount);
            var opDestinations = ops.Slice(2 * operationCount, operationCount);

            Backtrace(
                middleShorter,
                middleLonger,
                matrix,
                columns,
                prefixLength,
                opTypes,
                opSources,
                opDestinations
            );

            return ScoreMatchingBlocks(
                shorter,
                longer,
                opTypes,
                opSources,
                opDestinations,
                scoreCutoff
            );
        }
        finally
        {
            if (rentedMatrix is not null)
            {
                ArrayPool<int>.Shared.Return(rentedMatrix);
            }

            if (rentedOps is not null)
            {
                ArrayPool<int>.Shared.Return(rentedOps);
            }
        }
    }

    internal static int EditOperationBufferLength(int operationCount) =>
        checked((int)(3L * operationCount));

    private static void FillCostMatrix(
        ReadOnlySpan<char> pattern,
        ReadOnlySpan<char> text,
        Span<int> matrix,
        int columns
    )
    {
        var rows = pattern.Length + 1;
        for (var column = 0; column < columns; column++)
        {
            matrix[column] = column;
        }

        for (var row = 1; row < rows; row++)
        {
            matrix[row * columns] = row;
        }

        for (var row = 1; row < rows; row++)
        {
            var previous = (row - 1) * columns;
            var current = (row * columns) + 1;
            var character = pattern[row - 1];
            var value = row;

            for (var column = 1; column < columns; column++)
            {
                var candidate = matrix[previous] + (character != text[column - 1] ? 1 : 0);
                previous++;
                value++;
                if (value > candidate)
                {
                    value = candidate;
                }

                candidate = matrix[previous] + 1;
                if (value > candidate)
                {
                    value = candidate;
                }

                matrix[current++] = value;
            }
        }
    }

    /// <remarks>
    /// Ports python-Levenshtein's edit-op backtrace, including its direction state
    /// and tie-breaking order, so the chosen alignment matches FuzzySharp exactly.
    /// </remarks>
    private static void Backtrace(
        ReadOnlySpan<char> pattern,
        ReadOnlySpan<char> text,
        ReadOnlySpan<int> matrix,
        int columns,
        int offset,
        Span<int> opTypes,
        Span<int> opSources,
        Span<int> opDestinations
    )
    {
        var row = pattern.Length;
        var column = text.Length;
        var pointer = ((row + 1) * columns) - 1;
        var direction = 0;
        var position = opTypes.Length;

        while (row > 0 || column > 0)
        {
            if (
                row != 0
                && column != 0
                && matrix[pointer] == matrix[pointer - columns - 1]
                && pattern[row - 1] == text[column - 1]
            )
            {
                row--;
                column--;
                pointer -= columns + 1;
                direction = 0;
                continue;
            }

            if (direction < 0 && column != 0 && matrix[pointer] == matrix[pointer - 1] + 1)
            {
                position--;
                opTypes[position] = Insert;
                opSources[position] = row + offset;
                opDestinations[position] = --column + offset;
                pointer--;
                continue;
            }

            if (direction > 0 && row != 0 && matrix[pointer] == matrix[pointer - columns] + 1)
            {
                position--;
                opTypes[position] = Delete;
                opSources[position] = --row + offset;
                opDestinations[position] = column + offset;
                pointer -= columns;
                continue;
            }

            if (row != 0 && column != 0 && matrix[pointer] == matrix[pointer - columns - 1] + 1)
            {
                position--;
                opTypes[position] = Replace;
                opSources[position] = --row + offset;
                opDestinations[position] = --column + offset;
                pointer -= columns + 1;
                direction = 0;
                continue;
            }

            if (direction == 0 && column != 0 && matrix[pointer] == matrix[pointer - 1] + 1)
            {
                position--;
                opTypes[position] = Insert;
                opSources[position] = row + offset;
                opDestinations[position] = --column + offset;
                pointer--;
                direction = -1;
                continue;
            }

            if (direction == 0 && row != 0 && matrix[pointer] == matrix[pointer - columns] + 1)
            {
                position--;
                opTypes[position] = Delete;
                opSources[position] = --row + offset;
                opDestinations[position] = column + offset;
                pointer -= columns;
                direction = 1;
                continue;
            }

            throw new InvalidOperationException("Cannot trace edit operations.");
        }

        System.Diagnostics.Debug.Assert(position == 0);
    }

    private static int ScoreMatchingBlocks(
        ReadOnlySpan<char> shorter,
        ReadOnlySpan<char> longer,
        ReadOnlySpan<int> opTypes,
        ReadOnlySpan<int> opSources,
        ReadOnlySpan<int> opDestinations,
        int scoreCutoff
    )
    {
        var best = 0;
        var previousStart = -1;
        var sourcePosition = 0;
        var destinationPosition = 0;
        var operation = 0;
        var remaining = opTypes.Length;

        while (remaining != 0)
        {
            if (
                sourcePosition < opSources[operation]
                || destinationPosition < opDestinations[operation]
            )
            {
                if (
                    TryScoreBlock(
                        shorter,
                        longer,
                        sourcePosition,
                        destinationPosition,
                        scoreCutoff,
                        ref best,
                        ref previousStart
                    )
                )
                {
                    return best;
                }

                sourcePosition = opSources[operation];
                destinationPosition = opDestinations[operation];
            }

            var type = opTypes[operation];
            do
            {
                if (type != Insert)
                {
                    sourcePosition++;
                }

                if (type != Delete)
                {
                    destinationPosition++;
                }

                remaining--;
                operation++;
            } while (
                remaining != 0
                && opTypes[operation] == type
                && sourcePosition == opSources[operation]
                && destinationPosition == opDestinations[operation]
            );
        }

        if (sourcePosition < shorter.Length || destinationPosition < longer.Length)
        {
            if (
                TryScoreBlock(
                    shorter,
                    longer,
                    sourcePosition,
                    destinationPosition,
                    scoreCutoff,
                    ref best,
                    ref previousStart
                )
            )
            {
                return best;
            }
        }

        _ = TryScoreBlock(
            shorter,
            longer,
            shorter.Length,
            longer.Length,
            scoreCutoff,
            ref best,
            ref previousStart
        );
        return best;
    }

    private static bool TryScoreBlock(
        ReadOnlySpan<char> shorter,
        ReadOnlySpan<char> longer,
        int sourcePosition,
        int destinationPosition,
        int scoreCutoff,
        ref int best,
        ref int previousStart
    )
    {
        var start = Math.Max(destinationPosition - sourcePosition, 0);
        if (start == previousStart)
        {
            return false;
        }

        previousStart = start;
        var end = start + Math.Min(shorter.Length, longer.Length - start);
        var window = longer[start..end];
        var totalLength = ScoreMath.TotalLength(shorter.Length, window.Length);

        var needed = Math.Max(scoreCutoff, best + 1);
        var maximumDistance = ScoreMath.MaximumDistanceForScore(totalLength, needed);
        if (maximumDistance < 0)
        {
            return false;
        }

        var distance = Indel.Distance(shorter, window, maximumDistance);
        if (distance > maximumDistance)
        {
            return false;
        }

        var score = ScoreMath.RatioScore(totalLength, distance);
        if (score > best)
        {
            best = score;
        }

        return best == 100;
    }
}

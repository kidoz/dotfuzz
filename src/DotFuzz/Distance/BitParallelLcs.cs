using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace DotFuzz.Distance;

/// <summary>
/// Hyyrö-style bitset LCS. The LCS length yields Indel distance as
/// <c>left.Length + right.Length - 2 * lcs</c>.
/// </summary>
internal static class BitParallelLcs
{
    // Worst-case combined stackalloc in one frame is roughly 49 KB
    // (hash keys/indices + mask words + state words), which assumes the
    // default 1 MB thread stack; lower these bounds for smaller custom stacks.
    private const int StackHashCapacity = 2_048;
    private const int StackMaskWords = 4_096;
    private const int StackStateWords = 128;
    private const int MaximumBitMaskWords = 1_048_576;

    public static int Distance(
        ReadOnlySpan<char> pattern,
        ReadOnlySpan<char> text,
        int maximumDistance
    )
    {
        if (pattern.Length <= 64)
        {
            return Distance64(pattern, text, maximumDistance);
        }

        return DistanceBlocks(pattern, text, maximumDistance);
    }

    private static int Distance64(
        ReadOnlySpan<char> pattern,
        ReadOnlySpan<char> text,
        int maximumDistance
    )
    {
        var capacity = HashCapacity(pattern.Length);
        Span<int> keys = stackalloc int[capacity];
        Span<ulong> masks = stackalloc ulong[capacity];
        keys.Clear();
        masks.Clear();

        var hashMask = capacity - 1;
        for (var index = 0; index < pattern.Length; index++)
        {
            var key = pattern[index] + 1;
            var slot = FindSlot(keys, hashMask, key);
            keys[slot] = key;
            masks[slot] |= 1UL << index;
        }

        ulong state = 0;
        for (var index = 0; index < text.Length; index++)
        {
            var key = text[index] + 1;
            var slot = FindSlot(keys, hashMask, key);
            var matches = keys[slot] == key ? masks[slot] : 0UL;
            var x = matches | state;
            var y = (state << 1) | 1UL;
            state = x & ~(x - y);

            if (maximumDistance != int.MaxValue)
            {
                var lcsSoFar = BitOperations.PopCount(state);
                if (
                    CannotReachCutoff(
                        pattern.Length,
                        text.Length,
                        index + 1,
                        lcsSoFar,
                        maximumDistance
                    )
                )
                {
                    return Indel.Exceeded(maximumDistance);
                }
            }
        }

        var lcs = BitOperations.PopCount(state);
        var distance = pattern.Length + text.Length - (2 * lcs);
        return distance <= maximumDistance ? distance : Indel.Exceeded(maximumDistance);
    }

    private static int DistanceBlocks(
        ReadOnlySpan<char> pattern,
        ReadOnlySpan<char> text,
        int maximumDistance
    )
    {
        var blockCount = (pattern.Length + 63) >>> 6;
        var capacity = HashCapacity(pattern.Length);

        int[]? rentedKeys = null;
        int[]? rentedIndices = null;
        ulong[]? rentedMasks = null;
        ulong[]? rentedState = null;

        try
        {
            Span<int> keys =
                capacity <= StackHashCapacity
                    ? stackalloc int[capacity]
                    : (rentedKeys = ArrayPool<int>.Shared.Rent(capacity)).AsSpan(0, capacity);
            Span<int> indices =
                capacity <= StackHashCapacity
                    ? stackalloc int[capacity]
                    : (rentedIndices = ArrayPool<int>.Shared.Rent(capacity)).AsSpan(0, capacity);
            keys.Clear();

            var hashMask = capacity - 1;
            var uniqueCount = 0;
            foreach (var character in pattern)
            {
                var key = character + 1;
                var slot = FindSlot(keys, hashMask, key);
                if (keys[slot] == 0)
                {
                    keys[slot] = key;
                    indices[slot] = uniqueCount++;
                }
            }

            var maskWordCount = checked(uniqueCount * blockCount);
            if (maskWordCount > MaximumBitMaskWords)
            {
                return DynamicProgrammingDistance(pattern, text, maximumDistance);
            }

            Span<ulong> characterMasks =
                maskWordCount <= StackMaskWords
                    ? stackalloc ulong[maskWordCount]
                    : (rentedMasks = ArrayPool<ulong>.Shared.Rent(maskWordCount)).AsSpan(
                        0,
                        maskWordCount
                    );
            characterMasks.Clear();

            for (var index = 0; index < pattern.Length; index++)
            {
                var key = pattern[index] + 1;
                var slot = FindSlot(keys, hashMask, key);
                var characterIndex = indices[slot];
                characterMasks[(characterIndex * blockCount) + (index >>> 6)] |=
                    1UL << (index & 63);
            }

            Span<ulong> state =
                blockCount <= StackStateWords
                    ? stackalloc ulong[blockCount]
                    : (rentedState = ArrayPool<ulong>.Shared.Rent(blockCount)).AsSpan(
                        0,
                        blockCount
                    );
            state.Clear();

            for (var textIndex = 0; textIndex < text.Length; textIndex++)
            {
                var key = text[textIndex] + 1;
                var slot = FindSlot(keys, hashMask, key);
                var characterIndex = keys[slot] == key ? indices[slot] : -1;
                UpdateState(state, characterMasks, characterIndex, blockCount);

                if (
                    maximumDistance != int.MaxValue
                    && ((textIndex & 7) == 7 || textIndex == text.Length - 1)
                )
                {
                    var lcsSoFar = PopCount(state);
                    if (
                        CannotReachCutoff(
                            pattern.Length,
                            text.Length,
                            textIndex + 1,
                            lcsSoFar,
                            maximumDistance
                        )
                    )
                    {
                        return Indel.Exceeded(maximumDistance);
                    }
                }
            }

            var lcs = PopCount(state);
            var distance = pattern.Length + text.Length - (2 * lcs);
            return distance <= maximumDistance ? distance : Indel.Exceeded(maximumDistance);
        }
        finally
        {
            if (rentedKeys is not null)
            {
                ArrayPool<int>.Shared.Return(rentedKeys);
            }

            if (rentedIndices is not null)
            {
                ArrayPool<int>.Shared.Return(rentedIndices);
            }

            if (rentedMasks is not null)
            {
                ArrayPool<ulong>.Shared.Return(rentedMasks);
            }

            if (rentedState is not null)
            {
                ArrayPool<ulong>.Shared.Return(rentedState);
            }
        }
    }

    internal static int Distance(
        int patternLength,
        ReadOnlySpan<char> text,
        ReadOnlySpan<int> lookupKeys,
        ReadOnlySpan<int> lookupIndices,
        ReadOnlySpan<ulong> characterMasks,
        int maximumDistance
    )
    {
        if (Math.Abs(patternLength - text.Length) > maximumDistance)
        {
            return Indel.Exceeded(maximumDistance);
        }

        if (patternLength == 0 || text.IsEmpty)
        {
            var distance = patternLength + text.Length;
            return distance <= maximumDistance ? distance : Indel.Exceeded(maximumDistance);
        }

        if (patternLength <= 64)
        {
            return Distance64(
                patternLength,
                text,
                lookupKeys,
                lookupIndices,
                characterMasks,
                maximumDistance
            );
        }

        var blockCount = (patternLength + 63) >>> 6;
        var hashMask = lookupKeys.Length - 1;
        ulong[]? rentedState = null;

        try
        {
            Span<ulong> state =
                blockCount <= StackStateWords
                    ? stackalloc ulong[blockCount]
                    : (rentedState = ArrayPool<ulong>.Shared.Rent(blockCount)).AsSpan(
                        0,
                        blockCount
                    );
            state.Clear();

            for (var textIndex = 0; textIndex < text.Length; textIndex++)
            {
                var key = text[textIndex] + 1;
                var slot = FindSlot(lookupKeys, hashMask, key);
                var characterIndex = lookupKeys[slot] == key ? lookupIndices[slot] : -1;
                UpdateState(state, characterMasks, characterIndex, blockCount);

                if (
                    maximumDistance != int.MaxValue
                    && ((textIndex & 7) == 7 || textIndex == text.Length - 1)
                )
                {
                    var lcsSoFar = PopCount(state);
                    if (
                        CannotReachCutoff(
                            patternLength,
                            text.Length,
                            textIndex + 1,
                            lcsSoFar,
                            maximumDistance
                        )
                    )
                    {
                        return Indel.Exceeded(maximumDistance);
                    }
                }
            }

            var lcs = PopCount(state);
            var distance = patternLength + text.Length - (2 * lcs);
            return distance <= maximumDistance ? distance : Indel.Exceeded(maximumDistance);
        }
        finally
        {
            if (rentedState is not null)
            {
                ArrayPool<ulong>.Shared.Return(rentedState);
            }
        }
    }

    private static int Distance64(
        int patternLength,
        ReadOnlySpan<char> text,
        ReadOnlySpan<int> lookupKeys,
        ReadOnlySpan<int> lookupIndices,
        ReadOnlySpan<ulong> characterMasks,
        int maximumDistance
    )
    {
        var hashMask = lookupKeys.Length - 1;
        ulong state = 0;

        for (var textIndex = 0; textIndex < text.Length; textIndex++)
        {
            var key = text[textIndex] + 1;
            var slot = FindSlot(lookupKeys, hashMask, key);
            var characterIndex = lookupKeys[slot] == key ? lookupIndices[slot] : -1;
            var matches = characterIndex >= 0 ? characterMasks[characterIndex] : 0UL;
            var x = matches | state;
            var y = (state << 1) | 1UL;
            state = x & ~(x - y);

            if (maximumDistance != int.MaxValue)
            {
                var lcsSoFar = BitOperations.PopCount(state);
                if (
                    CannotReachCutoff(
                        patternLength,
                        text.Length,
                        textIndex + 1,
                        lcsSoFar,
                        maximumDistance
                    )
                )
                {
                    return Indel.Exceeded(maximumDistance);
                }
            }
        }

        var lcs = BitOperations.PopCount(state);
        var distance = patternLength + text.Length - (2 * lcs);
        return distance <= maximumDistance ? distance : Indel.Exceeded(maximumDistance);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void UpdateState(
        Span<ulong> state,
        ReadOnlySpan<ulong> characterMasks,
        int characterIndex,
        int blockCount
    )
    {
        ulong shiftCarry = 1;
        ulong subtractBorrow = 0;
        var maskOffset = characterIndex * blockCount;

        for (var block = 0; block < blockCount; block++)
        {
            var oldState = state[block];
            var matches = characterIndex >= 0 ? characterMasks[maskOffset + block] : 0UL;
            var x = matches | oldState;
            var y = (oldState << 1) | shiftCarry;
            shiftCarry = oldState >>> 63;

            var difference = x - y;
            var borrow = x < y ? 1UL : 0UL;
            var differenceWithBorrow = difference - subtractBorrow;
            if (difference < subtractBorrow)
            {
                borrow = 1UL;
            }

            state[block] = x & ~differenceWithBorrow;
            subtractBorrow = borrow;
        }
    }

    private static int DynamicProgrammingDistance(
        ReadOnlySpan<char> pattern,
        ReadOnlySpan<char> text,
        int maximumDistance
    )
    {
        int[]? rentedRow = null;
        try
        {
            Span<int> row =
                pattern.Length <= 1_024
                    ? stackalloc int[pattern.Length + 1]
                    : (rentedRow = ArrayPool<int>.Shared.Rent(pattern.Length + 1)).AsSpan(
                        0,
                        pattern.Length + 1
                    );

            for (var column = 0; column <= pattern.Length; column++)
            {
                row[column] = column;
            }

            for (var textIndex = 1; textIndex <= text.Length; textIndex++)
            {
                var diagonal = row[0];
                row[0] = textIndex;
                var lowerBound = row[0] + Math.Abs(pattern.Length - (text.Length - textIndex));

                for (var column = 1; column <= pattern.Length; column++)
                {
                    var above = row[column];
                    var value =
                        pattern[column - 1] == text[textIndex - 1]
                            ? diagonal
                            : Math.Min(above + 1, row[column - 1] + 1);
                    diagonal = above;
                    row[column] = value;

                    var remainingDifference = Math.Abs(
                        (pattern.Length - column) - (text.Length - textIndex)
                    );
                    lowerBound = Math.Min(lowerBound, value + remainingDifference);
                }

                if (lowerBound > maximumDistance)
                {
                    return Indel.Exceeded(maximumDistance);
                }
            }

            var distance = row[pattern.Length];
            return distance <= maximumDistance ? distance : Indel.Exceeded(maximumDistance);
        }
        finally
        {
            if (rentedRow is not null)
            {
                ArrayPool<int>.Shared.Return(rentedRow);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int FindSlot(Span<int> keys, int hashMask, int key)
    {
        var slot = (int)(((uint)key * 2_654_435_761U) & (uint)hashMask);
        while (keys[slot] != 0 && keys[slot] != key)
        {
            slot = (slot + 1) & hashMask;
        }

        return slot;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int FindSlot(ReadOnlySpan<int> keys, int hashMask, int key)
    {
        var slot = (int)(((uint)key * 2_654_435_761U) & (uint)hashMask);
        while (keys[slot] != 0 && keys[slot] != key)
        {
            slot = (slot + 1) & hashMask;
        }

        return slot;
    }

    private static int HashCapacity(int patternLength)
    {
        var target = patternLength >= 65_536 ? 131_072 : Math.Max(4, patternLength * 2);
        var capacity = 4;
        while (capacity < target)
        {
            capacity <<= 1;
        }

        return capacity;
    }

    private static int PopCount(ReadOnlySpan<ulong> values)
    {
        var count = 0;
        foreach (var value in values)
        {
            count += BitOperations.PopCount(value);
        }

        return count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool CannotReachCutoff(
        int patternLength,
        int textLength,
        int processedTextLength,
        int lcsSoFar,
        int maximumDistance
    )
    {
        var remainingText = textLength - processedTextLength;
        var maximumLcs = lcsSoFar + Math.Min(remainingText, patternLength - lcsSoFar);
        var bestPossibleDistance = patternLength + textLength - (2 * maximumLcs);
        return bestPossibleDistance > maximumDistance;
    }
}

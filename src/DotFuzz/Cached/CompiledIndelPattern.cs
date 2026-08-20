using System.Buffers;
using DotFuzz.Distance;

namespace DotFuzz.Cached;

internal sealed class CompiledIndelPattern
{
    private const int MaximumBitMaskWords = 1_048_576;
    private readonly string _pattern;
    private readonly int[] _lookupKeys;
    private readonly int[] _lookupIndices;
    private readonly ulong[] _characterMasks;

    public CompiledIndelPattern(string pattern)
    {
        _pattern = pattern;
        Length = pattern.Length;
        _lookupKeys = [];
        _lookupIndices = [];
        _characterMasks = [];

        if (pattern.Length == 0)
        {
            return;
        }

        var sorted = pattern.ToCharArray();
        Array.Sort(sorted);

        var uniqueCount = 1;
        for (var index = 1; index < sorted.Length; index++)
        {
            if (sorted[index] != sorted[index - 1])
            {
                uniqueCount++;
            }
        }

        var destination = 1;
        for (var index = 1; index < sorted.Length; index++)
        {
            if (sorted[index] != sorted[index - 1])
            {
                sorted[destination++] = sorted[index];
            }
        }

        var blockCount = (pattern.Length + 63) >>> 6;
        var maskWordCount = checked(uniqueCount * blockCount);
        if (maskWordCount > MaximumBitMaskWords)
        {
            return;
        }

        var lookupCapacity = HashCapacity(uniqueCount);
        _lookupKeys = new int[lookupCapacity];
        _lookupIndices = new int[lookupCapacity];
        var lookupMask = lookupCapacity - 1;
        for (var index = 0; index < uniqueCount; index++)
        {
            var key = sorted[index] + 1;
            var slot = FindSlot(_lookupKeys, lookupMask, key);
            _lookupKeys[slot] = key;
            _lookupIndices[slot] = index;
        }

        _characterMasks = new ulong[maskWordCount];
        for (var index = 0; index < pattern.Length; index++)
        {
            var characterIndex = Array.BinarySearch(sorted, 0, uniqueCount, pattern[index]);
            _characterMasks[(characterIndex * blockCount) + (index >>> 6)] |= 1UL << (index & 63);
        }
    }

    public int Length { get; }

    public int Distance(ReadOnlySpan<char> text, int maximumDistance)
    {
        if (_pattern.AsSpan().SequenceEqual(text))
        {
            return 0;
        }

        if (_characterMasks.Length != 0)
        {
            return BitParallelLcs.Distance(
                Length,
                text,
                _lookupKeys,
                _lookupIndices,
                _characterMasks,
                maximumDistance
            );
        }

        // This protects pathological huge/high-cardinality inputs from quadratic
        // mask storage while retaining pooled, allocation-conscious execution.
        return DynamicProgrammingDistance(text, maximumDistance);
    }

    private static int HashCapacity(int uniqueCount)
    {
        var target = Math.Max(4, uniqueCount * 2);
        var capacity = 4;
        while (capacity < target)
        {
            capacity <<= 1;
        }

        return capacity;
    }

    private static int FindSlot(int[] keys, int hashMask, int key)
    {
        var slot = (int)(((uint)key * 2_654_435_761U) & (uint)hashMask);
        while (keys[slot] != 0 && keys[slot] != key)
        {
            slot = (slot + 1) & hashMask;
        }

        return slot;
    }

    private int DynamicProgrammingDistance(ReadOnlySpan<char> text, int maximumDistance)
    {
        if (Math.Abs(Length - text.Length) > maximumDistance)
        {
            return Indel.Exceeded(maximumDistance);
        }

        int[]? rented = null;
        try
        {
            Span<int> row =
                Length <= 1_024
                    ? stackalloc int[Length + 1]
                    : (rented = ArrayPool<int>.Shared.Rent(Length + 1)).AsSpan(0, Length + 1);

            for (var column = 0; column <= Length; column++)
            {
                row[column] = column;
            }

            for (var textIndex = 1; textIndex <= text.Length; textIndex++)
            {
                var diagonal = row[0];
                row[0] = textIndex;
                var lowerBound = row[0] + Math.Abs(Length - (text.Length - textIndex));

                for (var column = 1; column <= Length; column++)
                {
                    var above = row[column];
                    var value =
                        _pattern[column - 1] == text[textIndex - 1]
                            ? diagonal
                            : Math.Min(above + 1, row[column - 1] + 1);
                    diagonal = above;
                    row[column] = value;

                    var remainingDifference = Math.Abs(
                        (Length - column) - (text.Length - textIndex)
                    );
                    lowerBound = Math.Min(lowerBound, value + remainingDifference);
                }

                if (lowerBound > maximumDistance)
                {
                    return Indel.Exceeded(maximumDistance);
                }
            }

            var distance = row[Length];
            return distance <= maximumDistance ? distance : Indel.Exceeded(maximumDistance);
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<int>.Shared.Return(rented);
            }
        }
    }
}

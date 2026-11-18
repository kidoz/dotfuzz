using System.Buffers;
using System.Text;
using DotFuzz.Distance;

namespace DotFuzz.Cached;

internal sealed class CompiledIndelPattern
{
    private const int MaximumBitMaskWords = 1_048_576;
    private const int MaxLinearCountLength = 80;
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

        var uniqueCount = CountDistinct(pattern);

        var blockCount = (pattern.Length + 63) >>> 6;
        var requiredMaskWords = (long)uniqueCount * blockCount;
        if (requiredMaskWords > MaximumBitMaskWords)
        {
            return;
        }

        var maskWordCount = (int)requiredMaskWords;

        var lookupCapacity = HashCapacity(uniqueCount);
        _lookupKeys = new int[lookupCapacity];
        _lookupIndices = new int[lookupCapacity];
        var lookupMask = lookupCapacity - 1;
        var nextIndex = 0;
        _characterMasks = new ulong[maskWordCount];
        for (var index = 0; index < pattern.Length; index++)
        {
            var key = pattern[index] + 1;
            var slot = FindSlot(_lookupKeys, lookupMask, key);
            if (_lookupKeys[slot] == 0)
            {
                _lookupKeys[slot] = key;
                _lookupIndices[slot] = nextIndex++;
            }

            var characterIndex = _lookupIndices[slot];
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

    private static int CountDistinct(ReadOnlySpan<char> pattern)
    {
        var ascii = Ascii.IsValid(pattern);
        if (!ascii && pattern.Length <= MaxLinearCountLength)
        {
            Span<char> distinct = stackalloc char[pattern.Length];
            var count = 0;
            foreach (var character in pattern)
            {
                if (!distinct[..count].Contains(character))
                {
                    distinct[count++] = character;
                }
            }

            return count;
        }

        // One bit per possible code unit. ASCII needs only the first two words.
        Span<ulong> seen = stackalloc ulong[ascii ? 2 : (char.MaxValue / 64) + 1];
        seen.Clear();
        var uniqueCount = 0;
        foreach (var character in pattern)
        {
            ref var word = ref seen[character >> 6];
            var bit = 1UL << (character & 63);
            if ((word & bit) == 0)
            {
                word |= bit;
                uniqueCount++;
            }
        }

        return uniqueCount;
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

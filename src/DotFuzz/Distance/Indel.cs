namespace DotFuzz.Distance;

/// <summary>
/// Computes insertion/deletion edit distance over UTF-16 code units.
/// A replacement therefore costs two operations.
/// </summary>
public static class Indel
{
    /// <summary>
    /// Returns the Indel distance, or <paramref name="scoreCutoff"/> + 1 when the
    /// exact distance is known to exceed the cutoff.
    /// </summary>
    public static int Distance(
        ReadOnlySpan<char> left,
        ReadOnlySpan<char> right,
        int scoreCutoff = int.MaxValue
    )
    {
        ArgumentOutOfRangeException.ThrowIfNegative(scoreCutoff);

        if (left.SequenceEqual(right))
        {
            return 0;
        }

        if (Math.Abs(left.Length - right.Length) > scoreCutoff)
        {
            return Exceeded(scoreCutoff);
        }

        TrimCommonAffixes(ref left, ref right);

        if (left.IsEmpty || right.IsEmpty)
        {
            var distance = left.Length + right.Length;
            return distance <= scoreCutoff ? distance : Exceeded(scoreCutoff);
        }

        // Building the match vector for the shorter input minimizes bitset width.
        if (left.Length > right.Length)
        {
            var temporary = left;
            left = right;
            right = temporary;
        }

        var result = BitParallelLcs.Distance(left, right, scoreCutoff);
        System.Diagnostics.Debug.Assert(
            result <= left.Length + right.Length || result == Exceeded(scoreCutoff)
        );
        return result;
    }

    /// <inheritdoc cref="Distance(ReadOnlySpan{char}, ReadOnlySpan{char}, int)"/>
    public static int Distance(string left, string right, int scoreCutoff = int.MaxValue)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        return Distance(left.AsSpan(), right.AsSpan(), scoreCutoff);
    }

    internal static int Exceeded(int scoreCutoff) =>
        scoreCutoff == int.MaxValue ? int.MaxValue : scoreCutoff + 1;

    private static void TrimCommonAffixes(ref ReadOnlySpan<char> left, ref ReadOnlySpan<char> right)
    {
        var prefixLength = left.CommonPrefixLength(right);
        left = left[prefixLength..];
        right = right[prefixLength..];

        var suffixLength = 0;
        var maximumSuffixLength = Math.Min(left.Length, right.Length);
        while (
            suffixLength < maximumSuffixLength
            && left[^(suffixLength + 1)] == right[^(suffixLength + 1)]
        )
        {
            suffixLength++;
        }
        if (suffixLength != 0)
        {
            left = left[..^suffixLength];
            right = right[..^suffixLength];
        }
    }
}

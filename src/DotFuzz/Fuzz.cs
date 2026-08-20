using DotFuzz.Distance;
using DotFuzz.Internal;

namespace DotFuzz;

/// <summary>Span-first fuzzy string scoring APIs.</summary>
public static partial class Fuzz
{
    /// <summary>
    /// Returns normalized Indel similarity in the integer range 0..100.
    /// Values below <paramref name="scoreCutoff"/> return zero.
    /// </summary>
    /// <remarks>
    /// Scoring is case-sensitive and operates on UTF-16 code units. An empty input
    /// returns zero to match FuzzySharp 2.x. Midpoints use ties-to-even rounding.
    /// </remarks>
    public static int Ratio(ReadOnlySpan<char> left, ReadOnlySpan<char> right, int scoreCutoff = 0)
    {
        if (left.IsEmpty || right.IsEmpty || scoreCutoff > 100)
        {
            return 0;
        }

        if (left.SequenceEqual(right))
        {
            return 100;
        }

        var totalLength = checked(left.Length + right.Length);
        var maximumDistance = ScoreMath.MaximumDistanceForScore(totalLength, scoreCutoff);
        if (maximumDistance < 0)
        {
            return 0;
        }

        var distance = Indel.Distance(left, right, maximumDistance);
        if (distance > maximumDistance)
        {
            return 0;
        }

        var score = ScoreMath.RatioScore(totalLength, distance);
        return score >= Math.Max(0, scoreCutoff) ? score : 0;
    }

    /// <inheritdoc cref="Ratio(ReadOnlySpan{char}, ReadOnlySpan{char}, int)"/>
    public static int Ratio(string left, string right, int scoreCutoff = 0)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        return Ratio(left.AsSpan(), right.AsSpan(), scoreCutoff);
    }
}

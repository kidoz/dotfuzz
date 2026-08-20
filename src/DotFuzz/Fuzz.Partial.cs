using DotFuzz.Internal;

namespace DotFuzz;

public static partial class Fuzz
{
    /// <summary>
    /// Returns the best score of the shorter input against aligned windows of the
    /// longer input, in the integer range 0..100. Values below
    /// <paramref name="scoreCutoff"/> return zero.
    /// </summary>
    /// <remarks>
    /// Candidate windows follow FuzzySharp 2.0.2's Levenshtein matching blocks, so
    /// results stay behaviorally compatible with its <c>PartialRatio</c>. Scoring is
    /// case-sensitive, performs no preprocessing, and operates on UTF-16 code
    /// units. An empty input returns zero. Alignment discovery uses working memory
    /// proportional to the product of the input lengths; ordinary short inputs stay
    /// on the stack and larger inputs use pooled buffers.
    /// </remarks>
    public static int PartialRatio(
        ReadOnlySpan<char> left,
        ReadOnlySpan<char> right,
        int scoreCutoff = 0
    )
    {
        if (left.IsEmpty || right.IsEmpty || scoreCutoff > 100)
        {
            return 0;
        }

        var score = PartialRatioCore.Score(left, right, scoreCutoff);
        return score >= Math.Max(0, scoreCutoff) ? score : 0;
    }

    /// <inheritdoc cref="PartialRatio(ReadOnlySpan{char}, ReadOnlySpan{char}, int)"/>
    public static int PartialRatio(string left, string right, int scoreCutoff = 0)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        return PartialRatio(left.AsSpan(), right.AsSpan(), scoreCutoff);
    }
}

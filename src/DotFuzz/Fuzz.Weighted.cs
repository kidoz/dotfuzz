namespace DotFuzz;

public static partial class Fuzz
{
    private const double UnbaseScale = 0.95;

    /// <summary>
    /// Combines <see cref="Ratio(ReadOnlySpan{char}, ReadOnlySpan{char}, int)"/>,
    /// <see cref="PartialRatio(ReadOnlySpan{char}, ReadOnlySpan{char}, int)"/>, and the
    /// token scorers with FuzzySharp 2.0.2's weighting rules. Values below
    /// <paramref name="scoreCutoff"/> return zero.
    /// </summary>
    /// <remarks>
    /// The weights, length-ratio thresholds, and final ties-to-even rounding follow
    /// FuzzySharp's <c>WeightedRatio</c> exactly, including its double-precision
    /// scaling of the integer sub-scores. No preprocessing is applied; pair with
    /// <see cref="Preprocess"/> to reproduce FuzzySharp's fully processed defaults.
    /// </remarks>
    public static int WeightedRatio(
        ReadOnlySpan<char> left,
        ReadOnlySpan<char> right,
        int scoreCutoff = 0
    )
    {
        if (left.IsEmpty || right.IsEmpty || scoreCutoff > 100)
        {
            return 0;
        }

        double baseRatio = Ratio(left, right);
        var lengthRatio =
            (double)Math.Max(left.Length, right.Length) / Math.Min(left.Length, right.Length);

        double combined;
        if (lengthRatio < 1.5)
        {
            var tokenSort = TokenSortRatio(left, right) * UnbaseScale;
            var tokenSet = TokenSetRatio(left, right) * UnbaseScale;
            combined = Math.Max(baseRatio, Math.Max(tokenSort, tokenSet));
        }
        else
        {
            var partialScale = lengthRatio > 8 ? 0.6 : 0.9;
            var partial = PartialRatio(left, right) * partialScale;
            var partialSort = TokenSortRatio(left, right) * UnbaseScale * partialScale;
            var partialSet = TokenSetRatio(left, right) * UnbaseScale * partialScale;
            combined = Math.Max(Math.Max(baseRatio, partial), Math.Max(partialSort, partialSet));
        }

        var score = (int)Math.Round(combined);
        return score >= Math.Max(0, scoreCutoff) ? score : 0;
    }

    /// <inheritdoc cref="WeightedRatio(ReadOnlySpan{char}, ReadOnlySpan{char}, int)"/>
    public static int WeightedRatio(string left, string right, int scoreCutoff = 0)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        return WeightedRatio(left.AsSpan(), right.AsSpan(), scoreCutoff);
    }
}

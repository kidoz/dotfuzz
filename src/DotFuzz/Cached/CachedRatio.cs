using DotFuzz.Internal;

namespace DotFuzz.Cached;

/// <summary>
/// Immutable, thread-safe Ratio scorer that compiles a query's bit masks once.
/// </summary>
public sealed class CachedRatio
{
    private readonly CompiledIndelPattern _pattern;

    /// <summary>Creates a cached scorer while retaining the supplied string.</summary>
    public CachedRatio(string query)
    {
        ArgumentNullException.ThrowIfNull(query);
        _pattern = new CompiledIndelPattern(query);
    }

    /// <summary>
    /// Creates a cached scorer from a span. The span is copied because cached state
    /// must outlive the caller's buffer.
    /// </summary>
    public CachedRatio(ReadOnlySpan<char> query)
        : this(query.ToString()) { }

    /// <summary>Gets the number of UTF-16 code units in the cached query.</summary>
    public int QueryLength => _pattern.Length;

    /// <inheritdoc cref="Fuzz.Ratio(ReadOnlySpan{char}, ReadOnlySpan{char}, int)"/>
    public int Score(ReadOnlySpan<char> candidate, int scoreCutoff = 0)
    {
        if (_pattern.Length == 0 || candidate.IsEmpty || scoreCutoff > 100)
        {
            return 0;
        }

        var totalLength = checked(_pattern.Length + candidate.Length);
        var maximumDistance = ScoreMath.MaximumDistanceForScore(totalLength, scoreCutoff);
        if (maximumDistance < 0)
        {
            return 0;
        }

        var distance = _pattern.Distance(candidate, maximumDistance);
        if (distance > maximumDistance)
        {
            return 0;
        }

        var score = ScoreMath.RatioScore(totalLength, distance);
        return score >= Math.Max(0, scoreCutoff) ? score : 0;
    }

    /// <inheritdoc cref="Score(ReadOnlySpan{char}, int)"/>
    public int Score(string candidate, int scoreCutoff = 0)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        return Score(candidate.AsSpan(), scoreCutoff);
    }
}

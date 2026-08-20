using System.Buffers;
using DotFuzz.Cached;

namespace DotFuzz;

/// <summary>Allocation-conscious one-to-many matching operations.</summary>
public static class Process
{
    /// <summary>
    /// Finds the first highest-scoring choice. Query compilation is performed once
    /// for the whole scan.
    /// </summary>
    public static ExtractOneResult ExtractOne(
        string query,
        ReadOnlySpan<string> choices,
        int scoreCutoff = 0
    )
    {
        ArgumentNullException.ThrowIfNull(query);
        return ExtractOne(new CachedRatio(query), choices, scoreCutoff);
    }

    /// <summary>
    /// Finds the first highest-scoring choice. The query span is copied once so the
    /// compiled scorer can own stable query data during the scan.
    /// </summary>
    public static ExtractOneResult ExtractOne(
        ReadOnlySpan<char> query,
        ReadOnlySpan<string> choices,
        int scoreCutoff = 0
    ) => ExtractOne(new CachedRatio(query), choices, scoreCutoff);

    /// <summary>
    /// Finds the first highest-scoring choice using precompiled query state. The
    /// hot scan itself performs no managed allocations for ordinary input sizes.
    /// </summary>
    public static ExtractOneResult ExtractOne(
        CachedRatio scorer,
        ReadOnlySpan<string> choices,
        int scoreCutoff = 0
    )
    {
        ArgumentNullException.ThrowIfNull(scorer);
        if (scoreCutoff > 100 || choices.IsEmpty)
        {
            return ExtractOneResult.None;
        }

        scoreCutoff = Math.Max(0, scoreCutoff);
        var found = false;
        var bestIndex = -1;
        var bestScore = 0;

        for (var index = 0; index < choices.Length; index++)
        {
            var choice = choices[index];
            if (choice is null)
            {
                throw new ArgumentException("Choices cannot contain null values.", nameof(choices));
            }

            var candidateCutoff = found ? Math.Max(scoreCutoff, bestScore + 1) : scoreCutoff;
            var score = scorer.Score(choice.AsSpan(), candidateCutoff);

            if (!found || score > bestScore)
            {
                if (score >= scoreCutoff)
                {
                    found = true;
                    bestIndex = index;
                    bestScore = score;
                    if (score == 100)
                    {
                        break;
                    }
                }
            }
        }

        return found
            ? new ExtractOneResult(choices[bestIndex], bestScore, bestIndex)
            : ExtractOneResult.None;
    }

    /// <summary>
    /// Writes the highest-scoring choices into <paramref name="destination"/>,
    /// sorted by descending score with earlier indexes winning ties, and returns
    /// the number of results. The destination length is the result limit, and the
    /// scan itself performs no managed allocations for ordinary input sizes.
    /// </summary>
    public static int ExtractTop(
        CachedRatio scorer,
        ReadOnlySpan<string> choices,
        Span<ExtractResult> destination,
        int scoreCutoff = 0
    )
    {
        ArgumentNullException.ThrowIfNull(scorer);
        if (destination.IsEmpty || scoreCutoff > 100 || choices.IsEmpty)
        {
            return 0;
        }

        scoreCutoff = Math.Max(0, scoreCutoff);
        var count = 0;

        for (var index = 0; index < choices.Length; index++)
        {
            var choice = choices[index];
            if (choice is null)
            {
                throw new ArgumentException("Choices cannot contain null values.", nameof(choices));
            }

            var full = count == destination.Length;
            var candidateCutoff = full
                ? Math.Max(scoreCutoff, destination[count - 1].Score + 1)
                : scoreCutoff;
            if (candidateCutoff > 100)
            {
                break;
            }

            var score = scorer.Score(choice.AsSpan(), candidateCutoff);
            if (score < candidateCutoff)
            {
                continue;
            }

            var position = full ? count - 1 : count;
            while (position > 0 && destination[position - 1].Score < score)
            {
                position--;
            }

            for (var shift = full ? count - 1 : count; shift > position; shift--)
            {
                destination[shift] = destination[shift - 1];
            }

            destination[position] = new ExtractResult(choice, score, index);
            if (!full)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// Returns up to <paramref name="limit"/> highest-scoring choices, sorted by
    /// descending score with earlier indexes winning ties. Query compilation is
    /// performed once for the whole scan.
    /// </summary>
    public static ExtractResult[] ExtractTop(
        CachedRatio scorer,
        ReadOnlySpan<string> choices,
        int limit,
        int scoreCutoff = 0
    )
    {
        ArgumentNullException.ThrowIfNull(scorer);
        ArgumentOutOfRangeException.ThrowIfNegative(limit);
        var capacity = Math.Min(limit, choices.Length);
        if (capacity == 0)
        {
            return [];
        }

        var results = new ExtractResult[capacity];
        var count = ExtractTop(scorer, choices, results.AsSpan(), scoreCutoff);
        return count == capacity ? results : results[..count];
    }

    /// <inheritdoc cref="ExtractTop(CachedRatio, ReadOnlySpan{string}, int, int)"/>
    public static ExtractResult[] ExtractTop(
        string query,
        ReadOnlySpan<string> choices,
        int limit,
        int scoreCutoff = 0
    )
    {
        ArgumentNullException.ThrowIfNull(query);
        return ExtractTop(new CachedRatio(query), choices, limit, scoreCutoff);
    }

    /// <inheritdoc cref="ExtractTop(CachedRatio, ReadOnlySpan{string}, int, int)"/>
    public static ExtractResult[] ExtractTop(
        ReadOnlySpan<char> query,
        ReadOnlySpan<string> choices,
        int limit,
        int scoreCutoff = 0
    ) => ExtractTop(new CachedRatio(query), choices, limit, scoreCutoff);

    /// <summary>
    /// Writes every choice that meets the cutoff into
    /// <paramref name="destination"/> in choice order and returns the count.
    /// The destination must be at least as long as <paramref name="choices"/>.
    /// The scan performs no managed allocations for ordinary input sizes.
    /// </summary>
    public static int ExtractAll(
        CachedRatio scorer,
        ReadOnlySpan<string> choices,
        Span<ExtractResult> destination,
        int scoreCutoff = 0
    )
    {
        ArgumentNullException.ThrowIfNull(scorer);
        if (destination.Length < choices.Length)
        {
            throw new ArgumentException(
                "Destination must be at least as long as the choices.",
                nameof(destination)
            );
        }

        if (scoreCutoff > 100)
        {
            return 0;
        }

        scoreCutoff = Math.Max(0, scoreCutoff);
        var count = 0;

        for (var index = 0; index < choices.Length; index++)
        {
            var choice = choices[index];
            if (choice is null)
            {
                throw new ArgumentException("Choices cannot contain null values.", nameof(choices));
            }

            var score = scorer.Score(choice.AsSpan(), scoreCutoff);
            if (score >= scoreCutoff)
            {
                destination[count++] = new ExtractResult(choice, score, index);
            }
        }

        return count;
    }

    /// <summary>
    /// Returns every choice that meets the cutoff, in choice order. Query
    /// compilation is performed once for the whole scan.
    /// </summary>
    public static ExtractResult[] ExtractAll(
        CachedRatio scorer,
        ReadOnlySpan<string> choices,
        int scoreCutoff = 0
    )
    {
        ArgumentNullException.ThrowIfNull(scorer);
        if (choices.IsEmpty || scoreCutoff > 100)
        {
            return [];
        }

        var rented = ArrayPool<ExtractResult>.Shared.Rent(choices.Length);
        try
        {
            var count = ExtractAll(scorer, choices, rented.AsSpan(), scoreCutoff);
            return rented.AsSpan(0, count).ToArray();
        }
        finally
        {
            ArrayPool<ExtractResult>.Shared.Return(rented, clearArray: true);
        }
    }

    /// <inheritdoc cref="ExtractAll(CachedRatio, ReadOnlySpan{string}, int)"/>
    public static ExtractResult[] ExtractAll(
        string query,
        ReadOnlySpan<string> choices,
        int scoreCutoff = 0
    )
    {
        ArgumentNullException.ThrowIfNull(query);
        return ExtractAll(new CachedRatio(query), choices, scoreCutoff);
    }

    /// <inheritdoc cref="ExtractAll(CachedRatio, ReadOnlySpan{string}, int)"/>
    public static ExtractResult[] ExtractAll(
        ReadOnlySpan<char> query,
        ReadOnlySpan<string> choices,
        int scoreCutoff = 0
    ) => ExtractAll(new CachedRatio(query), choices, scoreCutoff);
}

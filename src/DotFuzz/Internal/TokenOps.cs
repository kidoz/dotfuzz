namespace DotFuzz.Internal;

/// <summary>
/// Span-based whitespace tokenization, ordinal token sorting, and single-space
/// joins used by the token scorers. All state lives in caller-provided buffers.
/// </summary>
internal static class TokenOps
{
    /// <summary>Text length at or below which callers may use stack buffers.</summary>
    public const int StackTextLength = 512;

    /// <summary>Upper bound of the token count for a text of the given length.</summary>
    public static int MaximumTokenCount(int textLength) => (textLength / 2) + (textLength & 1);

    /// <summary>Splits on Unicode whitespace, dropping empty tokens.</summary>
    public static int Tokenize(ReadOnlySpan<char> text, Span<int> starts, Span<int> lengths)
    {
        var count = 0;
        var index = 0;
        while (index < text.Length)
        {
            if (char.IsWhiteSpace(text[index]))
            {
                index++;
                continue;
            }

            var start = index;
            while (index < text.Length && !char.IsWhiteSpace(text[index]))
            {
                index++;
            }

            starts[count] = start;
            lengths[count] = index - start;
            count++;
        }

        return count;
    }

    /// <summary>Sorts token ranges in place by ordinal comparison.</summary>
    public static void SortOrdinal(
        ReadOnlySpan<char> text,
        Span<int> starts,
        Span<int> lengths,
        int count
    )
    {
        if (count > 16)
        {
            // Heapsort bounds the number of token comparisons for large lists.
            for (var root = (count / 2) - 1; root >= 0; root--)
            {
                SiftDown(text, starts, lengths, root, count);
            }

            for (var end = count - 1; end > 0; end--)
            {
                (starts[0], starts[end]) = (starts[end], starts[0]);
                (lengths[0], lengths[end]) = (lengths[end], lengths[0]);
                SiftDown(text, starts, lengths, 0, end);
            }

            return;
        }

        for (var index = 1; index < count; index++)
        {
            var start = starts[index];
            var length = lengths[index];
            var token = text.Slice(start, length);

            var insert = index - 1;
            while (
                insert >= 0
                && text.Slice(starts[insert], lengths[insert])
                    .CompareTo(token, StringComparison.Ordinal) > 0
            )
            {
                starts[insert + 1] = starts[insert];
                lengths[insert + 1] = lengths[insert];
                insert--;
            }

            starts[insert + 1] = start;
            lengths[insert + 1] = length;
        }
    }

    private static void SiftDown(
        ReadOnlySpan<char> text,
        Span<int> starts,
        Span<int> lengths,
        int root,
        int count
    )
    {
        var start = starts[root];
        var length = lengths[root];
        var token = text.Slice(start, length);
        while (root < count / 2)
        {
            var child = (2 * root) + 1;
            var larger = text.Slice(starts[child], lengths[child]);
            if (child + 1 < count)
            {
                var sibling = text.Slice(starts[child + 1], lengths[child + 1]);
                if (sibling.SequenceCompareTo(larger) > 0)
                {
                    child++;
                    larger = sibling;
                }
            }

            if (token.SequenceCompareTo(larger) >= 0)
            {
                break;
            }

            starts[root] = starts[child];
            lengths[root] = lengths[child];
            root = child;
        }

        starts[root] = start;
        lengths[root] = length;
    }

    /// <summary>Removes adjacent duplicates from sorted token ranges.</summary>
    public static int DedupeSorted(
        ReadOnlySpan<char> text,
        Span<int> starts,
        Span<int> lengths,
        int count
    )
    {
        if (count == 0)
        {
            return 0;
        }

        var kept = 1;
        for (var index = 1; index < count; index++)
        {
            if (
                text.Slice(starts[index], lengths[index])
                    .SequenceEqual(text.Slice(starts[kept - 1], lengths[kept - 1]))
            )
            {
                continue;
            }

            starts[kept] = starts[index];
            lengths[kept] = lengths[index];
            kept++;
        }

        return kept;
    }

    /// <summary>Tokenizes, sorts ordinally, and writes the single-space join.</summary>
    public static int WriteSortedJoined(
        ReadOnlySpan<char> text,
        Span<int> starts,
        Span<int> lengths,
        Span<char> destination
    )
    {
        var count = Tokenize(text, starts, lengths);
        SortOrdinal(text, starts, lengths, count);

        var written = 0;
        for (var index = 0; index < count; index++)
        {
            if (written != 0)
            {
                destination[written++] = ' ';
            }

            text.Slice(starts[index], lengths[index]).CopyTo(destination[written..]);
            written += lengths[index];
        }

        return written;
    }

    /// <summary>
    /// Splits sorted, deduplicated token lists into intersection and one-sided
    /// remainders. Intersection entries reference the left side's ranges.
    /// </summary>
    public static void Classify(
        ReadOnlySpan<char> leftText,
        ReadOnlySpan<int> leftStarts,
        ReadOnlySpan<int> leftLengths,
        int leftCount,
        ReadOnlySpan<char> rightText,
        ReadOnlySpan<int> rightStarts,
        ReadOnlySpan<int> rightLengths,
        int rightCount,
        Span<int> intersection,
        out int intersectionCount,
        Span<int> leftOnly,
        out int leftOnlyCount,
        Span<int> rightOnly,
        out int rightOnlyCount
    )
    {
        intersectionCount = 0;
        leftOnlyCount = 0;
        rightOnlyCount = 0;

        var leftIndex = 0;
        var rightIndex = 0;
        while (leftIndex < leftCount && rightIndex < rightCount)
        {
            var comparison = leftText
                .Slice(leftStarts[leftIndex], leftLengths[leftIndex])
                .CompareTo(
                    rightText.Slice(rightStarts[rightIndex], rightLengths[rightIndex]),
                    StringComparison.Ordinal
                );
            if (comparison < 0)
            {
                leftOnly[leftOnlyCount++] = leftIndex++;
            }
            else if (comparison > 0)
            {
                rightOnly[rightOnlyCount++] = rightIndex++;
            }
            else
            {
                intersection[intersectionCount++] = leftIndex++;
                rightIndex++;
            }
        }

        while (leftIndex < leftCount)
        {
            leftOnly[leftOnlyCount++] = leftIndex++;
        }

        while (rightIndex < rightCount)
        {
            rightOnly[rightOnlyCount++] = rightIndex++;
        }
    }

    /// <summary>Appends the selected tokens to a single-space join in progress.</summary>
    public static int AppendJoined(
        ReadOnlySpan<char> text,
        ReadOnlySpan<int> starts,
        ReadOnlySpan<int> lengths,
        ReadOnlySpan<int> selected,
        int selectedCount,
        Span<char> destination,
        int written
    )
    {
        for (var index = 0; index < selectedCount; index++)
        {
            if (written != 0)
            {
                destination[written++] = ' ';
            }

            var token = selected[index];
            text.Slice(starts[token], lengths[token]).CopyTo(destination[written..]);
            written += lengths[token];
        }

        return written;
    }
}

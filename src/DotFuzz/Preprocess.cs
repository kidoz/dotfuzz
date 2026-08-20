using System.Buffers;
using System.Text;

namespace DotFuzz;

/// <summary>
/// Deterministic, culture-invariant input preprocessors. Matching APIs never
/// preprocess implicitly; apply these first to reproduce FuzzySharp's processed
/// modes.
/// </summary>
public static class Preprocess
{
    /// <summary>
    /// FuzzySharp-compatible full preprocessing: every character outside
    /// <c>[ a-zA-Z0-9]</c> becomes a space, ASCII letters are lowercased, and
    /// leading and trailing spaces are trimmed. Returns the number of characters
    /// written to <paramref name="destination"/>.
    /// </summary>
    /// <remarks>
    /// Matches FuzzySharp 2.0.2's default preprocessor under ordinary cultures;
    /// DotFuzz lowercases invariantly rather than with the current culture.
    /// <paramref name="destination"/> must be at least as long as
    /// <paramref name="source"/>. Interior space runs are preserved.
    /// </remarks>
    public static int Compatibility(ReadOnlySpan<char> source, Span<char> destination)
    {
        if (destination.Length < source.Length)
        {
            throw new ArgumentException(
                "Destination must be at least as long as the source.",
                nameof(destination)
            );
        }

        var first = 0;
        while (first < source.Length && !IsCompatibilityToken(source[first]))
        {
            first++;
        }

        var last = source.Length - 1;
        while (last >= first && !IsCompatibilityToken(source[last]))
        {
            last--;
        }

        var written = 0;
        for (var index = first; index <= last; index++)
        {
            var character = source[index];
            destination[written++] = IsCompatibilityToken(character)
                ? ToLowerAscii(character)
                : ' ';
        }

        return written;
    }

    /// <inheritdoc cref="Compatibility(ReadOnlySpan{char}, Span{char})"/>
    public static string Compatibility(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return Transform(source, static (input, output) => Compatibility(input, output));
    }

    /// <summary>
    /// Unicode-aware preprocessing: letter and digit runes are lowercased with the
    /// invariant simple case mapping, every other rune becomes a single space, and
    /// leading and trailing spaces are trimmed. Returns the number of characters
    /// written to <paramref name="destination"/>.
    /// </summary>
    /// <remarks>
    /// Operates on Unicode scalar values, so astral-plane letters are preserved.
    /// Invalid surrogate sequences become spaces. <paramref name="destination"/>
    /// must be at least as long as <paramref name="source"/>. Interior space runs
    /// are preserved.
    /// </remarks>
    public static int Unicode(ReadOnlySpan<char> source, Span<char> destination)
    {
        if (destination.Length < source.Length)
        {
            throw new ArgumentException(
                "Destination must be at least as long as the source.",
                nameof(destination)
            );
        }

        var written = 0;
        var trimmedLength = 0;
        foreach (var rune in source.EnumerateRunes())
        {
            if (Rune.IsLetterOrDigit(rune))
            {
                written += Rune.ToLowerInvariant(rune).EncodeToUtf16(destination[written..]);
                trimmedLength = written;
            }
            else if (written != 0)
            {
                destination[written++] = ' ';
            }
        }

        return trimmedLength;
    }

    /// <inheritdoc cref="Unicode(ReadOnlySpan{char}, Span{char})"/>
    public static string Unicode(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return Transform(source, static (input, output) => Unicode(input, output));
    }

    private static string Transform(string source, Func<string, char[], int> transformation)
    {
        if (source.Length == 0)
        {
            return source;
        }

        var rented = ArrayPool<char>.Shared.Rent(source.Length);
        try
        {
            var written = transformation(source, rented);
            var result = rented.AsSpan(0, written);
            return result.SequenceEqual(source) ? source : new string(result);
        }
        finally
        {
            ArrayPool<char>.Shared.Return(rented);
        }
    }

    private static bool IsCompatibilityToken(char character) =>
        (uint)(character - '0') <= 9
        || (uint)(character - 'a') <= 25
        || (uint)(character - 'A') <= 25;

    private static char ToLowerAscii(char character) =>
        (uint)(character - 'A') <= 25 ? (char)(character + 32) : character;
}

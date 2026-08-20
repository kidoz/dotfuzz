namespace DotFuzz.Benchmarks;

public enum RatioScenario
{
    ShortSimilarAscii,
    ShortDissimilarAscii,
    ShortSimilarUnicode,
    ShortDissimilarUnicode,
    MediumSimilarAscii,
    MediumDissimilarAscii,
    MediumSimilarUnicode,
    MediumDissimilarUnicode,
    LongSimilarAscii,
    LongDissimilarAscii,
    LongSimilarUnicode,
    LongDissimilarUnicode,
}

internal static class ScenarioFactory
{
    public static (string Left, string Right) Create(RatioScenario scenario)
    {
        var name = scenario.ToString();
        var length =
            name.StartsWith("Short", StringComparison.Ordinal) ? 16
            : name.StartsWith("Medium", StringComparison.Ordinal) ? 128
            : 1_024;
        var unicode = name.EndsWith("Unicode", StringComparison.Ordinal);
        var similar =
            name.Contains("Similar", StringComparison.Ordinal)
            && !name.Contains("Dissimilar", StringComparison.Ordinal);

        var firstAlphabet = unicode ? "Москва東京ΑθήναЖ中λ" : "abcdefghijklmnop0123456789";
        var secondAlphabet = unicode ? "Київ大阪βήταЮ文δ" : "QRSTUVWXYZ9876543210";
        var left = Repeat(firstAlphabet, length);
        var right = similar ? Mutate(left, unicode ? '界' : 'x') : Repeat(secondAlphabet, length);
        return (left, right);
    }

    public static string[] CreateChoices(string query, int count, bool unicode)
    {
        var choices = new string[count];
        var alphabet = unicode ? "Київ大阪βήταЮ文δ" : "QRSTUVWXYZ9876543210";
        for (var index = 0; index < choices.Length; index++)
        {
            var offset = index % alphabet.Length;
            choices[index] = Repeat(alphabet[offset..] + alphabet[..offset], query.Length);
        }

        choices[^2] = Mutate(query, unicode ? '界' : 'x');
        return choices;
    }

    private static string Repeat(string alphabet, int length) =>
        string.Create(
            length,
            alphabet,
            static (destination, source) =>
            {
                for (var index = 0; index < destination.Length; index++)
                {
                    destination[index] = source[index % source.Length];
                }
            }
        );

    private static string Mutate(string value, char replacement) =>
        string.Create(
            value.Length,
            (value, replacement),
            static (destination, state) =>
            {
                state.value.AsSpan().CopyTo(destination);
                for (var index = 7; index < destination.Length; index += 11)
                {
                    destination[index] = state.replacement;
                }
            }
        );
}

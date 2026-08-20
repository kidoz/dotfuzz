using System.Text.RegularExpressions;

namespace DotFuzz.Tests;

public sealed class PreprocessTests
{
    [Theory]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData("!!!", "")]
    [InlineData("New York Mets!", "new york mets")]
    [InlineData("  C# is--great  ", "c  is  great")]
    [InlineData("ABC123", "abc123")]
    [InlineData("Straße", "stra e")]
    [InlineData("Москва 2024", "2024")]
    [InlineData("a\tb\nc", "a b c")]
    public void CompatibilityMatchesFuzzySharpDefaultPreprocessor(string input, string expected)
    {
        Assert.Equal(expected, Preprocess.Compatibility(input));
    }

    [Fact]
    public void CompatibilityMatchesReferenceRegexOnDeterministicRandomCorpus()
    {
        var random = new Random(0xFEED);
        for (var iteration = 0; iteration < 2_000; iteration++)
        {
            var input = RandomString(random, random.Next(0, 120));
#pragma warning disable CA1308 // The FuzzySharp-compatible contract is lowercasing.
            var expected = Regex.Replace(input, "[^ a-zA-Z0-9]", " ").ToLowerInvariant().Trim();
#pragma warning restore CA1308

            Assert.Equal(expected, Preprocess.Compatibility(input));
        }
    }

    [Fact]
    public void CompatibilitySpanWritesTrimmedOutput()
    {
        Span<char> destination = stackalloc char[32];
        var written = Preprocess.Compatibility("  Hello, World!  ".AsSpan(), destination);

        Assert.Equal("hello  world", destination[..written].ToString());
    }

    [Fact]
    public void CompatibilityReturnsSameInstanceWhenAlreadyNormalized()
    {
        const string normalized = "already normalized 42";
        Assert.Same(normalized, Preprocess.Compatibility(normalized));
    }

    [Theory]
    [InlineData("", "")]
    [InlineData(" \t ", "")]
    [InlineData("New York Mets!", "new york mets")]
    [InlineData("Тёплый Stanley", "тёплый stanley")]
    [InlineData("Straße", "straße")]
    [InlineData("東京駅 123", "東京駅 123")]
    [InlineData("émigré", "émigré")]
    [InlineData("a👍b", "a b")]
    [InlineData("𝔘nicode", "𝔘nicode")]
    public void UnicodeKeepsLetterAndDigitRunesLowercased(string input, string expected)
    {
        Assert.Equal(expected, Preprocess.Unicode(input));
    }

    [Fact]
    public void UnicodeSpanTrimsAndPreservesInteriorSpacing()
    {
        Span<char> destination = stackalloc char[32];
        var written = Preprocess.Unicode("--Ab!  cd--".AsSpan(), destination);

        Assert.Equal("ab   cd", destination[..written].ToString());
    }

    [Fact]
    public void DestinationShorterThanSourceThrows()
    {
        Assert.Throws<ArgumentException>(() =>
        {
            Span<char> destination = stackalloc char[3];
            return Preprocess.Compatibility("abcdef".AsSpan(), destination);
        });
        Assert.Throws<ArgumentException>(() =>
        {
            Span<char> destination = stackalloc char[3];
            return Preprocess.Unicode("abcdef".AsSpan(), destination);
        });
    }

    [Fact]
    public void NullStringsThrow()
    {
        Assert.Throws<ArgumentNullException>(() => Preprocess.Compatibility(null!));
        Assert.Throws<ArgumentNullException>(() => Preprocess.Unicode(null!));
    }

    [Fact]
    public void SpanHotPathsAllocateZeroBytesAfterWarmup()
    {
        const string input = "  Fast, Allocation-Free Preprocessing 101!  ";
        var buffer = new char[input.Length];
        _ = Preprocess.Compatibility(input.AsSpan(), buffer);
        _ = Preprocess.Unicode(input.AsSpan(), buffer);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < 10_000; iteration++)
        {
            _ = Preprocess.Compatibility(input.AsSpan(), buffer);
            _ = Preprocess.Unicode(input.AsSpan(), buffer);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    private static string RandomString(Random random, int length) =>
        string.Create(
            length,
            random,
            static (span, state) =>
            {
                const string alphabet = "abC XY-z!01,9 Ж中 \tλ_.";
                for (var index = 0; index < span.Length; index++)
                {
                    span[index] = alphabet[state.Next(alphabet.Length)];
                }
            }
        );
}

using DotFuzz.Distance;

namespace DotFuzz.Tests;

public sealed class IndelTests
{
    [Theory]
    [InlineData("", "", 0)]
    [InlineData("abc", "", 3)]
    [InlineData("", "abc", 3)]
    [InlineData("abc", "abc", 0)]
    [InlineData("kitten", "sitting", 5)]
    [InlineData("fuzzy", "wuzzy", 2)]
    [InlineData("Saturday", "Sunday", 4)]
    [InlineData("Москва", "Масква", 2)]
    public void DistanceHasExpectedIndelSemantics(string left, string right, int expected)
    {
        Assert.Equal(expected, Indel.Distance(left.AsSpan(), right.AsSpan()));
        Assert.Equal(expected, Indel.Distance(right.AsSpan(), left.AsSpan()));
    }

    [Fact]
    public void DistanceReturnsCutoffPlusOneWhenUnreachable()
    {
        Assert.Equal(3, Indel.Distance("abcdef".AsSpan(), "uvwxyz".AsSpan(), 2));
        Assert.Equal(2, Indel.Distance("abc".AsSpan(), "axc".AsSpan(), 2));
    }

    [Fact]
    public void BlockwiseAlgorithmMatchesReferenceDynamicProgramming()
    {
        var random = new Random(0x5EED);
        for (var iteration = 0; iteration < 300; iteration++)
        {
            var left = RandomString(random, random.Next(65, 260));
            var right = RandomString(random, random.Next(65, 260));
            var expected = ReferenceIndel(left, right);

            Assert.Equal(expected, Indel.Distance(left, right));
            foreach (var cutoff in new[] { 0, 5, 20, 80, int.MaxValue })
            {
                var expectedWithCutoff =
                    expected <= cutoff ? expected
                    : cutoff == int.MaxValue ? int.MaxValue
                    : cutoff + 1;
                Assert.Equal(expectedWithCutoff, Indel.Distance(left, right, cutoff));
            }
        }
    }

    private static string RandomString(Random random, int length)
    {
        return string.Create(
            length,
            random,
            static (span, state) =>
            {
                const string characters = "abcdef0123Ж中";
                for (var index = 0; index < span.Length; index++)
                {
                    span[index] = characters[state.Next(characters.Length)];
                }
            }
        );
    }

    private static int ReferenceIndel(string left, string right)
    {
        var row = new int[right.Length + 1];
        for (var column = 0; column <= right.Length; column++)
        {
            row[column] = column;
        }

        for (var rowIndex = 1; rowIndex <= left.Length; rowIndex++)
        {
            var diagonal = row[0];
            row[0] = rowIndex;
            for (var column = 1; column <= right.Length; column++)
            {
                var above = row[column];
                row[column] =
                    left[rowIndex - 1] == right[column - 1]
                        ? diagonal
                        : Math.Min(above + 1, row[column - 1] + 1);
                diagonal = above;
            }
        }

        return row[^1];
    }
}

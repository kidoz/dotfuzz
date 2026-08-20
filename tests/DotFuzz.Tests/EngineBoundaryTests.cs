using DotFuzz.Cached;
using DotFuzz.Distance;

namespace DotFuzz.Tests;

/// <summary>
/// Boundary coverage for engine paths the compatibility suites do not reach:
/// the dynamic-programming fallback, tight blockwise cutoff edges, and the
/// cached scorer's concurrency contract.
/// </summary>
public sealed class EngineBoundaryTests
{
    // 8,400 distinct code units push uniqueCount * blockCount past the
    // 1,048,576 mask-word ceiling, forcing the DP fallback in both the
    // pairwise and the cached path.
    private const int FallbackLength = 8_400;

    [Fact]
    public void DynamicProgrammingFallbackComputesExactDistanceAndSentinel()
    {
        var left = HighCardinalityString();
        var right = WithReplacements(left, out var replacedCount);

        var expectedDistance = 2 * replacedCount;
        Assert.Equal(expectedDistance, Indel.Distance(left.AsSpan(), right.AsSpan()));
        Assert.Equal(31, Indel.Distance(left.AsSpan(), right.AsSpan(), 30));
    }

    [Fact]
    public void DynamicProgrammingFallbackKeepsCachedAndPairwiseParity()
    {
        var left = HighCardinalityString();
        var right = WithReplacements(left, out _);
        var dissimilar = string.Create(
            FallbackLength,
            0,
            static (span, _) =>
            {
                for (var index = 0; index < span.Length; index++)
                {
                    span[index] = (char)(0x5000 + index);
                }
            }
        );

        var cached = new CachedRatio(left);
        Assert.Equal(Fuzz.Ratio(left.AsSpan(), right.AsSpan()), cached.Score(right.AsSpan()));

        // The dissimilar pair cannot reach the cutoff; both paths must prune to zero.
        Assert.Equal(0, Fuzz.Ratio(left.AsSpan(), dissimilar.AsSpan(), 90));
        Assert.Equal(0, cached.Score(dissimilar.AsSpan(), 90));
    }

    [Fact]
    public void BlockwiseDistancesMatchReferenceWithTightCutoffs()
    {
        var random = new Random(0xB10C);
        for (var iteration = 0; iteration < 60; iteration++)
        {
            var left = RandomString(random, random.Next(190, 601));
            var right = RandomString(random, random.Next(190, 601));
            var expected = ReferenceIndelDistance(left, right);

            Assert.Equal(expected, Indel.Distance(left.AsSpan(), right.AsSpan()));
            for (var delta = -2; delta <= 2; delta++)
            {
                var cutoff = expected + delta;
                if (cutoff < 0)
                {
                    continue;
                }

                var bounded = expected <= cutoff ? expected : cutoff + 1;
                Assert.Equal(bounded, Indel.Distance(left.AsSpan(), right.AsSpan(), cutoff));
            }

            var score = Fuzz.Ratio(left.AsSpan(), right.AsSpan());
            Assert.Equal(score, new CachedRatio(left).Score(right.AsSpan()));
            Assert.Equal(score, Fuzz.Ratio(left.AsSpan(), right.AsSpan(), score));
            Assert.Equal(0, Fuzz.Ratio(left.AsSpan(), right.AsSpan(), score + 1));
        }
    }

    [Fact]
    public void CachedRatioIsSafeForConcurrentScoring()
    {
        var random = new Random(0xC0C0);
        var query = RandomString(random, 100);
        var cached = new CachedRatio(query);

        var candidates = new string[64];
        var expected = new int[64];
        for (var index = 0; index < candidates.Length; index++)
        {
            candidates[index] = RandomString(random, random.Next(80, 121));
            expected[index] = cached.Score(candidates[index].AsSpan());
        }

        var mismatches = 0;
        Parallel.For(
            0,
            10_000,
            new ParallelOptions { MaxDegreeOfParallelism = 8 },
            iteration =>
            {
                var index = iteration % candidates.Length;
                if (cached.Score(candidates[index].AsSpan()) != expected[index])
                {
                    Interlocked.Increment(ref mismatches);
                }
            }
        );

        Assert.Equal(0, mismatches);
    }

    private static string HighCardinalityString() =>
        string.Create(
            FallbackLength,
            0,
            static (span, _) =>
            {
                for (var index = 0; index < span.Length; index++)
                {
                    span[index] = (char)(0x1000 + index);
                }
            }
        );

    /// <summary>
    /// Replaces spaced-out positions, including both ends so affix trimming
    /// cannot shrink the pattern below the fallback threshold. Each replacement
    /// uses a code unit absent from the base string, so the exact Indel distance
    /// is twice the replacement count.
    /// </summary>
    private static string WithReplacements(string source, out int replacedCount)
    {
        var characters = source.ToCharArray();
        var count = 0;
        for (var index = 0; index < characters.Length; index += 350)
        {
            characters[index] = (char)(0x4000 + count);
            count++;
        }

        characters[^1] = (char)(0x4000 + count);
        count++;

        replacedCount = count;
        return new string(characters);
    }

    private static int ReferenceIndelDistance(string left, string right)
    {
        var row = new int[right.Length + 1];
        for (var column = 0; column <= right.Length; column++)
        {
            row[column] = column;
        }

        for (var leftIndex = 1; leftIndex <= left.Length; leftIndex++)
        {
            var diagonal = row[0];
            row[0] = leftIndex;
            for (var column = 1; column <= right.Length; column++)
            {
                var above = row[column];
                row[column] =
                    left[leftIndex - 1] == right[column - 1]
                        ? diagonal
                        : Math.Min(above, row[column - 1]) + 1;
                diagonal = above;
            }
        }

        return row[right.Length];
    }

    private static string RandomString(Random random, int length) =>
        string.Create(
            length,
            random,
            static (span, state) =>
            {
                const string alphabet = "abcXYZ019 -_Ж中λ";
                for (var index = 0; index < span.Length; index++)
                {
                    span[index] = alphabet[state.Next(alphabet.Length)];
                }
            }
        );
}

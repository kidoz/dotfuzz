using System.Runtime.CompilerServices;

namespace DotFuzz.Internal;

internal static class ScoreMath
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int RatioScore(int totalLength, int distance)
    {
        if (totalLength == 0)
        {
            return 0;
        }

        var numerator = (long)(totalLength - distance) * 100;
        var quotient = (int)(numerator / totalLength);
        var remainder = numerator % totalLength;
        var twiceRemainder = remainder * 2;

        if (twiceRemainder > totalLength || (twiceRemainder == totalLength && (quotient & 1) != 0))
        {
            quotient++;
        }

        return quotient;
    }

    public static int MaximumDistanceForScore(int totalLength, int scoreCutoff)
    {
        if (scoreCutoff <= 0)
        {
            return int.MaxValue;
        }

        if (scoreCutoff > 100)
        {
            return -1;
        }

        // Start at the half-point rounding boundary, then correct for ties-to-even.
        var maximumDistance = (int)
            Math.Min(totalLength, ((long)totalLength * (201 - (2 * scoreCutoff))) / 200);

        while (maximumDistance >= 0 && RatioScore(totalLength, maximumDistance) < scoreCutoff)
        {
            maximumDistance--;
        }

        return maximumDistance;
    }
}

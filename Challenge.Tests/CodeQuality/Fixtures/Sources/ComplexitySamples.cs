namespace Challenge.Tests.CodeQuality.Fixtures.Sources;

public static class ComplexitySamples
{
    public static int LegacyHighComplexity(int value)
    {
        var result = 0;

        if (value > 0) result++;
        if (value > 1) result++;
        if (value > 2) result++;
        if (value > 3) result++;
        if (value > 4) result++;
        if (value > 5) result++;
        if (value > 6) result++;
        if (value > 7) result++;
        if (value > 8) result++;
        if (value > 9) result++;
        if (value > 10) result++;
        if (value > 11) result++;
        if (value > 12) result++;
        if (value > 13) result++;
        if (value > 14) result++;
        if (value > 15) result++;

        return result;
    }

    public static int NewHighComplexity(int value)
    {
        var result = 0;

        if (value < 0) result--;
        if (value < -1) result--;
        if (value < -2) result--;
        if (value < -3) result--;
        if (value < -4) result--;
        if (value < -5) result--;
        if (value < -6) result--;
        if (value < -7) result--;
        if (value < -8) result--;
        if (value < -9) result--;
        if (value < -10) result--;
        if (value < -11) result--;
        if (value < -12) result--;
        if (value < -13) result--;
        if (value < -14) result--;
        if (value < -15) result--;

        return result;
    }
}
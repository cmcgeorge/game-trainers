using System;
using System.Collections.Generic;

namespace MightAndMagic1Trainer.Game;

/// <summary>
/// The probability model for one attribute generated as the sum of three six-sided dice
/// (3d6, range 3–18). The roller uses it two ways: to estimate the odds that a fresh roll
/// meets the user's per-stat minimums (the "what are my chances of this hero" number), and
/// as the yardstick the observed rolls are compared against to judge whether the game is
/// really rolling fair dice or quietly constraining the result.
/// </summary>
public static class ThreeD6
{
    /// <summary>Lowest value 3d6 can produce (1+1+1).</summary>
    public const int Min = 3;
    /// <summary>Highest value 3d6 can produce (6+6+6).</summary>
    public const int Max = 18;

    /// <summary>Mean of one 3d6 attribute (3 × 3.5 = 10.5).</summary>
    public const double Mean = 10.5;

    /// <summary>Variance of one 3d6 attribute (3 × 35/12 = 8.75).</summary>
    public const double Variance = 3 * 35.0 / 12.0;

    /// <summary>Standard deviation of one 3d6 attribute (≈ 2.9580).</summary>
    public static readonly double StdDev = Math.Sqrt(Variance);

    // _pAtLeast[v] = P(3d6 ≥ v) for v in 0..18, computed once by enumerating all 216 outcomes.
    private static readonly double[] _pAtLeast = BuildAtLeast();

    private static double[] BuildAtLeast()
    {
        var ways = new int[Max + 1];                 // ways[s] = #combinations summing to s (s in 3..18)
        for (int a = 1; a <= 6; a++)
            for (int b = 1; b <= 6; b++)
                for (int c = 1; c <= 6; c++)
                    ways[a + b + c]++;

        var atLeast = new double[Max + 1];
        for (int v = 0; v <= Max; v++)
        {
            int n = 0;
            for (int s = Math.Max(v, Min); s <= Max; s++) n += ways[s];
            atLeast[v] = n / 216.0;
        }
        return atLeast;
    }

    /// <summary>
    /// Probability that one 3d6 attribute is at least <paramref name="min"/>:
    /// 1.0 when <paramref name="min"/> ≤ 3 (every roll clears it) and 0.0 when it exceeds 18
    /// (no roll can).
    /// </summary>
    public static double PAtLeast(int min)
    {
        if (min <= Min) return 1.0;
        if (min > Max) return 0.0;
        return _pAtLeast[min];
    }

    /// <summary>
    /// Probability that a single fresh roll satisfies <em>every</em> per-stat minimum, assuming
    /// each stat is an independent 3d6. A minimum ≤ 3 counts as "no requirement"; if any minimum
    /// exceeds 18 the result is 0 (the target is unreachable under 3d6).
    /// </summary>
    public static double PMeetsAll(IEnumerable<int> mins)
    {
        double p = 1.0;
        foreach (var m in mins)
        {
            p *= PAtLeast(m);
            if (p == 0) break;
        }
        return p;
    }
}

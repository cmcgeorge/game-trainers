namespace PoolOfRadianceTrainer.Game;

/// <summary>
/// Probability model for the Pool of Radiance create-screen roll, used to estimate the odds of
/// hitting a target on any one re-roll. Each of the six primary abilities (STR, INT, WIS, DEX,
/// CON, CHA) is modelled as an independent <b>3d6</b> (range 3–18, mean 10.5) — the classic
/// AD&amp;D method the Gold Box engine rolls with. It's an approximation (some groups roll 4d6-
/// drop-lowest or re-roll for high stats; the game's own method is undocumented), so callers
/// phrase the result as "modelling each ability as fair 3d6". Exceptional strength (STR%) and
/// hit points (HP) are not modelled here — STR% only applies to fighters with STR 18, and HP
/// depends on the class hit die, so neither follows 3d6.
///
/// The key method, <see cref="PMeetsTarget"/>, convolves each ability's 3d6 distribution
/// <i>floored at that ability's minimum</i>. The convolution's total mass is the chance every
/// per-stat minimum is met, and its upper tail from the total minimum is the chance the six
/// abilities also sum high enough — so per-stat and total targets (which are correlated, being
/// the same dice) are handled together, exactly. Pure and unit-testable.
/// </summary>
public static class RollOdds
{
    /// <summary>Lowest value one 3d6 ability can roll (1+1+1).</summary>
    public const int Min = 3;
    /// <summary>Highest value one 3d6 ability can roll (6+6+6).</summary>
    public const int Max = 18;
    /// <summary>Number of rolled abilities (STR, INT, WIS, DEX, CON, CHA).</summary>
    public const int Attributes = PorFormat.StatCount;

    // _pmf[v] = P(one 3d6 ability == v) for v in 0..18 (0 for v < 3), from all 216 outcomes.
    private static readonly double[] _pmf = BuildPmf();

    private static double[] BuildPmf()
    {
        var ways = new int[Max + 1];
        for (int a = 1; a <= 6; a++)
            for (int b = 1; b <= 6; b++)
                for (int c = 1; c <= 6; c++)
                    ways[a + b + c]++;
        var pmf = new double[Max + 1];
        for (int s = Min; s <= Max; s++) pmf[s] = ways[s] / 216.0;
        return pmf;
    }

    /// <summary>Probability that one 3d6 ability is at least <paramref name="min"/>: 1.0 when
    /// <paramref name="min"/> ≤ 3 (every roll clears it) and 0.0 when it exceeds 18 (no roll can).</summary>
    public static double PAtLeast(int min)
    {
        if (min <= Min) return 1.0;
        if (min > Max) return 0.0;
        double p = 0;
        for (int v = min; v <= Max; v++) p += _pmf[v];
        return p;
    }

    /// <summary>
    /// Probability that a single fresh roll satisfies every per-stat minimum in <paramref name="mins"/>
    /// (index-aligned with the abilities; 0 = no requirement) <em>and</em> that the six abilities
    /// sum to at least <paramref name="totalMin"/> (0 = no total requirement). Returns 1.0 when nothing
    /// is constrained and 0.0 when the target is unreachable under 3d6 (a per-stat min above 18, or a
    /// total above 108). Abilities are treated as independent 3d6.
    /// </summary>
    public static double PMeetsTarget(IReadOnlyList<int> mins, int totalMin)
    {
        // dist[s] = probability the running ability sum equals s. Seed with sum 0 at probability 1,
        // then convolve in each ability's minimum-floored 3d6 distribution.
        double[] dist = { 1.0 };
        for (int k = 0; k < Attributes; k++)
        {
            int floor = (mins != null && k < mins.Count) ? mins[k] : 0;
            var next = new double[dist.Length + Max];
            for (int s = 0; s < dist.Length; s++)
            {
                double ps = dist[s];
                if (ps == 0) continue;
                for (int v = Math.Max(Min, floor); v <= Max; v++)
                {
                    double pv = _pmf[v];
                    if (pv != 0) next[s + v] += ps * pv;
                }
            }
            dist = next;
        }

        double p = 0;
        for (int s = Math.Max(totalMin, 0); s < dist.Length; s++) p += dist[s];
        return p;
    }
}

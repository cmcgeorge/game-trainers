namespace DarkDesigns1Trainer.Game;

/// <summary>
/// Exact odds that one create-screen roll clears a target, used to tell the player up front whether
/// "every attribute at least 17" is a two-second wait or a hopeless one.
///
/// Each of the five rolled values is an independent <c>10 + random(5) + random(5)</c> — the
/// distribution measured off the running game and documented on <see cref="CreationFormat"/>: a
/// symmetric triangle over 10..18 with weights 1:2:3:4:5:4:3:2:1 out of 25, mean 14.
///
/// Unlike a per-slot target, an attribute minimum here is a question about the pool as a multiset,
/// because the player arranges the five values freely (<see cref="CreationFormat.Arrange"/>). A
/// pool clears the target exactly when its values, sorted highest-first, are each at least the
/// correspondingly-ranked minimum. That makes the answer depend only on the <em>sorted</em> pool,
/// so <see cref="PMeetsTarget"/> enumerates the 1,287 non-increasing five-value combinations
/// rather than all 59,049 ordered ones, weighting each by how many orderings it stands for. The
/// result is exact — no sampling and no approximation. Pure and unit-testable.
/// </summary>
public static class RollOdds
{
    /// <summary>Lowest value one rolled stat can take (10).</summary>
    public const int Min = CreationFormat.MinRoll;

    /// <summary>Highest value one rolled stat can take (18).</summary>
    public const int Max = CreationFormat.MaxRoll;

    /// <summary>How many values one roll produces (5).</summary>
    public const int Rolled = CreationFormat.RolledCount;

    // _pmf[v] = P(one rolled value == v), zero outside Min..Max.
    private static readonly double[] _pmf = BuildPmf();

    // _factorial[n] = n!, for the multinomial weighting of a sorted combination.
    private static readonly double[] _factorial = BuildFactorials();

    private static double[] BuildPmf()
    {
        // Convolve DiceCount independent uniform 0..DieSides-1 draws, then shift by RollBase.
        var ways = new double[] { 1.0 };
        for (int d = 0; d < CreationFormat.DiceCount; d++)
        {
            var next = new double[ways.Length + CreationFormat.DieSides - 1];
            for (int s = 0; s < ways.Length; s++)
                for (int face = 0; face < CreationFormat.DieSides; face++)
                    next[s + face] += ways[s];
            ways = next;
        }

        double total = 0;
        foreach (var w in ways) total += w;

        var pmf = new double[Max + 1];
        for (int s = 0; s < ways.Length; s++)
            pmf[CreationFormat.RollBase + s] = ways[s] / total;
        return pmf;
    }

    private static double[] BuildFactorials()
    {
        var f = new double[Rolled + 1];
        f[0] = 1;
        for (int i = 1; i <= Rolled; i++) f[i] = f[i - 1] * i;
        return f;
    }

    /// <summary>Probability that one rolled value is <paramref name="v"/>.</summary>
    public static double P(int v) => v >= 0 && v < _pmf.Length ? _pmf[v] : 0.0;

    /// <summary>Probability that one rolled value is at least <paramref name="min"/>: 1.0 when
    /// <paramref name="min"/> ≤ <see cref="Min"/> and 0.0 when it exceeds <see cref="Max"/>.</summary>
    public static double PAtLeast(int min)
    {
        if (min <= Min) return 1.0;
        if (min > Max) return 0.0;
        double p = 0;
        for (int v = min; v <= Max; v++) p += _pmf[v];
        return p;
    }

    /// <summary>
    /// Probability that a single fresh roll can be arranged so every attribute meets its minimum in
    /// <paramref name="mins"/> (index-aligned with the attributes; 0 or null = no requirement)
    /// <em>and</em> the five values sum to at least <paramref name="totalMin"/> (0 = no total
    /// requirement). Returns 1.0 when nothing is constrained and 0.0 when the target is out of
    /// reach (a minimum above <see cref="Max"/>, or a total above <see cref="CreationFormat.MaxTotal"/>).
    /// </summary>
    public static double PMeetsTarget(IReadOnlyList<int>? mins, int totalMin)
    {
        var need = CreationFormat.SortedDescendingMins(mins);
        if (need[0] > Max) return 0.0;                       // no die can reach it
        if (totalMin > CreationFormat.MaxTotal) return 0.0;  // even all 18s fall short

        return Walk(0, Max, new int[Rolled], need, totalMin);
    }

    // Enumerates non-increasing value combinations (pick[0] >= pick[1] >= ... ), which is exactly
    // the sorted pool the target is judged on. Because pick is non-increasing, a value below the
    // rank's minimum means every later value is too — so the branch is abandoned immediately.
    private static double Walk(int k, int maxValue, int[] pick, int[] need, int totalMin)
    {
        if (k == Rolled)
        {
            int total = 0;
            foreach (var v in pick) total += v;
            return total >= totalMin ? Weight(pick) : 0.0;
        }

        double p = 0;
        for (int v = Math.Min(maxValue, Max); v >= Min; v--)
        {
            if (v < need[k]) break;          // this rank (and every later one) can't be satisfied
            pick[k] = v;
            p += Walk(k + 1, v, pick, need, totalMin);
        }
        return p;
    }

    // Probability of rolling this exact multiset in any order: the product of the per-value
    // probabilities times the number of distinct orderings (5! / the factorials of the run lengths).
    private static double Weight(int[] pick)
    {
        double p = 1;
        foreach (var v in pick) p *= _pmf[v];

        double orderings = _factorial[Rolled];
        int run = 1;
        for (int i = 1; i <= pick.Length; i++)
        {
            if (i < pick.Length && pick[i] == pick[i - 1]) { run++; continue; }
            orderings /= _factorial[run];
            run = 1;
        }
        return p * orderings;
    }
}

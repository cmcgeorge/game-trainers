using System;

namespace MightAndMagic1Trainer.Game;

/// <summary>How the observed rolls compare to fair 3d6.</summary>
public enum RollFairness
{
    /// <summary>Not enough samples yet for a verdict.</summary>
    NeedMoreData,
    /// <summary>A stat fell outside 3–18, so it can't be plain 3d6 at all.</summary>
    OutOfRange,
    /// <summary>Totals vary far less than 3d6 predicts — the game looks like it caps/normalises them.</summary>
    LikelyConstrained,
    /// <summary>The average total is significantly off from 3d6's expectation.</summary>
    LikelyBiased,
    /// <summary>Mean and spread both line up with fair 3d6.</summary>
    ConsistentWith3d6,
}

/// <summary>An immutable view of the accumulated stats, safe to hand to the UI thread.</summary>
public readonly record struct RollStatsSnapshot(
    int Count,
    double[] StatMean, int[] StatMin, int[] StatMax,
    double TotalMean, int TotalMin, int TotalMax, double TotalStdDev,
    double ExpectedTotalMean, double ExpectedTotalStdDev,
    RollFairness Fairness, double TotalMeanZ, double TotalStdDevRatio);

/// <summary>
/// A running tally of the rolls the roller has read this lock session: per-stat and total-sum
/// averages and extremes, and a verdict on whether the spread looks like fair 3d6 or like the
/// game constraining the outcome. Pure logic with no UI dependency — the view model owns one
/// instance, feeds it each fresh roll on the roll-loop thread, and posts <see cref="Snapshot"/>s
/// to the UI. Not thread-safe; all access is expected on a single owner thread at a time.
/// </summary>
public sealed class RollHistory
{
    /// <summary>Below this many samples the fairness check stays at <see cref="RollFairness.NeedMoreData"/>.</summary>
    public const int MinSamplesForVerdict = 40;

    // A spread this much below 3d6's predicted σ reads as "the game is constraining the total".
    private const double ConstrainedSdRatio = 0.6;
    // |z| beyond this on the average total reads as a real bias rather than sampling noise.
    private const double BiasedMeanZ = 4.0;

    private readonly int _statCount;
    private readonly long[] _statSum;
    private readonly int[] _statMin;
    private readonly int[] _statMax;
    private long _totalSum;
    private double _totalSumSq;
    private int _totalMin = int.MaxValue;
    private int _totalMax = int.MinValue;
    private bool _sawOutOfRange;
    private int[]? _last;

    public RollHistory(int statCount)
    {
        _statCount = statCount;
        _statSum = new long[statCount];
        _statMin = new int[statCount];
        _statMax = new int[statCount];
        for (int i = 0; i < statCount; i++) { _statMin[i] = int.MaxValue; _statMax[i] = int.MinValue; }
    }

    public int Count { get; private set; }

    /// <summary>
    /// Records one roll. Returns false (and changes nothing) when <paramref name="v"/> repeats the
    /// immediately preceding sample: a genuine re-roll never reproduces all seven stats, so a
    /// back-to-back duplicate is almost always a stale read that would skew the averages.
    /// </summary>
    public bool Add(int[] v)
    {
        if (v.Length < _statCount) return false;
        if (_last != null && Same(v, _last)) return false;

        int total = 0;
        for (int i = 0; i < _statCount; i++)
        {
            int x = v[i];
            _statSum[i] += x;
            if (x < _statMin[i]) _statMin[i] = x;
            if (x > _statMax[i]) _statMax[i] = x;
            if (x < ThreeD6.Min || x > ThreeD6.Max) _sawOutOfRange = true;
            total += x;
        }
        _totalSum += total;
        _totalSumSq += (double)total * total;
        if (total < _totalMin) _totalMin = total;
        if (total > _totalMax) _totalMax = total;

        _last = (int[])v.Clone();
        Count++;
        return true;
    }

    private double TotalMean => Count == 0 ? 0 : (double)_totalSum / Count;

    // Sample (Bessel-corrected) standard deviation of the total; 0 with fewer than two samples.
    private double TotalStdDev
    {
        get
        {
            if (Count < 2) return 0;
            double mean = TotalMean;
            double variance = (_totalSumSq - Count * mean * mean) / (Count - 1);
            return variance > 0 ? Math.Sqrt(variance) : 0;
        }
    }

    /// <summary>Builds an immutable snapshot of the current tally (cheap; clones the small per-stat arrays).</summary>
    public RollStatsSnapshot Snapshot()
    {
        var mean = new double[_statCount];
        var min = new int[_statCount];
        var max = new int[_statCount];
        for (int i = 0; i < _statCount; i++)
        {
            mean[i] = Count == 0 ? 0 : (double)_statSum[i] / Count;
            min[i] = Count == 0 ? 0 : _statMin[i];
            max[i] = Count == 0 ? 0 : _statMax[i];
        }

        double expMean = _statCount * ThreeD6.Mean;
        double expSd = Math.Sqrt(_statCount * ThreeD6.Variance);
        var fairness = Assess(expMean, expSd, out double z, out double sdRatio);

        return new RollStatsSnapshot(
            Count, mean, min, max,
            TotalMean, Count == 0 ? 0 : _totalMin, Count == 0 ? 0 : _totalMax, TotalStdDev,
            expMean, expSd, fairness, z, sdRatio);
    }

    // Compares the observed total against fair 3d6: z is how many standard errors the mean is off,
    // sdRatio is observed spread ÷ predicted spread (well below 1 ⇒ the totals are being pinned).
    private RollFairness Assess(double expMean, double expSd, out double z, out double sdRatio)
    {
        z = 0;
        sdRatio = 0;
        if (_sawOutOfRange) return RollFairness.OutOfRange;
        if (Count < MinSamplesForVerdict) return RollFairness.NeedMoreData;

        double se = expSd / Math.Sqrt(Count);          // standard error of the mean under fair 3d6
        z = (TotalMean - expMean) / se;
        sdRatio = expSd > 0 ? TotalStdDev / expSd : 0;

        if (sdRatio < ConstrainedSdRatio) return RollFairness.LikelyConstrained;
        if (Math.Abs(z) > BiasedMeanZ) return RollFairness.LikelyBiased;
        return RollFairness.ConsistentWith3d6;
    }

    private static bool Same(int[] a, int[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
        return true;
    }
}

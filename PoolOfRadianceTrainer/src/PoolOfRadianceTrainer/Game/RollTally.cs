namespace PoolOfRadianceTrainer.Game;

/// <summary>An immutable view of the accumulated roll statistics, safe to hand to the UI thread.</summary>
public readonly record struct RollTallySnapshot(
    int Count,
    double[] StatMean, int[] StatMin, int[] StatMax,
    double TotalMean, int TotalMin, int TotalMax);

/// <summary>
/// A running tally of the create-screen rolls read this lock session: per-stat and total-sum
/// averages and extremes for the six primary abilities. PoR's create roll is modelled as 3d6
/// (see <see cref="RollOdds"/>), but this tally deliberately keeps only the neutral averages/
/// extremes and makes no fairness judgement — the live distribution may differ from 3d6. Pure
/// logic with no UI dependency — the view model owns one instance, feeds it each fresh roll on
/// the roll-loop thread, and posts <see cref="Snapshot"/>s to the UI. Not thread-safe; all access
/// is expected on a single owner thread at a time.
/// </summary>
public sealed class RollTally
{
    private readonly int _statCount;
    private readonly long[] _statSum;
    private readonly int[] _statMin;
    private readonly int[] _statMax;
    private long _totalSum;
    private int _totalMin = int.MaxValue;
    private int _totalMax = int.MinValue;
    private int[]? _last;

    public RollTally(int statCount)
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
    /// immediately preceding sample: a genuine re-roll almost never reproduces all the stats, so a
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
            total += x;
        }
        _totalSum += total;
        if (total < _totalMin) _totalMin = total;
        if (total > _totalMax) _totalMax = total;

        _last = (int[])v.Clone();
        Count++;
        return true;
    }

    /// <summary>Builds an immutable snapshot of the current tally (cheap; clones the small per-stat arrays).</summary>
    public RollTallySnapshot Snapshot()
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
        return new RollTallySnapshot(
            Count, mean, min, max,
            Count == 0 ? 0 : (double)_totalSum / Count,
            Count == 0 ? 0 : _totalMin,
            Count == 0 ? 0 : _totalMax);
    }

    private static bool Same(int[] a, int[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
        return true;
    }
}

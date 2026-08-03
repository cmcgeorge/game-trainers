namespace DarkDesigns1Trainer.Game;

/// <summary>An immutable view of the accumulated roll statistics, safe to hand to the UI thread.</summary>
public readonly record struct RollTallySnapshot(
    int Count,
    double[] RankMean, int[] RankMin, int[] RankMax,
    double TotalMean, int TotalMin, int TotalMax);

/// <summary>
/// A running tally of the create-screen rolls read this lock session.
///
/// The five rolled values are interchangeable — the player arranges them freely — so tallying them
/// by slot would only ever show five copies of the same distribution. This tallies them by
/// <b>rank</b> instead (best, 2nd, 3rd, 4th, worst), which is what actually informs a target: the
/// best value of a roll averages well above 14, the worst well below, and seeing those bands makes
/// "is STR ≥ 17 realistic?" answerable at a glance. The overall total is tallied alongside.
///
/// Pure logic with no UI dependency — the view model owns one instance, feeds it each fresh roll on
/// the roll-loop thread, and posts <see cref="Snapshot"/>s to the UI. Not thread-safe; all access is
/// expected on a single owner thread at a time.
/// </summary>
public sealed class RollTally
{
    // The rank count is fixed by the roll itself: Add ranks through CreationFormat.SortedDescending,
    // which always returns exactly RolledCount entries. Taking it as a constructor parameter would
    // only invite a caller to pass something else and get an out-of-range throw or silently dropped
    // ranks, so it is a constant here instead.
    private const int RankCount = CreationFormat.RolledCount;

    private readonly long[] _rankSum = new long[RankCount];
    private readonly int[] _rankMin = new int[RankCount];
    private readonly int[] _rankMax = new int[RankCount];
    private long _totalSum;
    private int _totalMin = int.MaxValue;
    private int _totalMax = int.MinValue;
    private int[]? _last;

    public RollTally()
    {
        for (int i = 0; i < RankCount; i++) { _rankMin[i] = int.MaxValue; _rankMax[i] = int.MinValue; }
    }

    public int Count { get; private set; }

    /// <summary>
    /// Records one roll (given in slot order; ranked internally). Returns false — and changes
    /// nothing — when <paramref name="v"/> repeats the immediately preceding sample: a genuine
    /// re-roll reproduces all five values only about once in twenty thousand, so a back-to-back
    /// duplicate is almost always a stale read that would skew the averages.
    /// </summary>
    public bool Add(int[] v)
    {
        if (v.Length < RankCount) return false;
        if (_last != null && Same(v, _last)) return false;

        var ranked = CreationFormat.SortedDescending(v);
        int total = 0;
        for (int i = 0; i < RankCount; i++)
        {
            int x = ranked[i];
            _rankSum[i] += x;
            if (x < _rankMin[i]) _rankMin[i] = x;
            if (x > _rankMax[i]) _rankMax[i] = x;
            total += x;
        }
        _totalSum += total;
        if (total < _totalMin) _totalMin = total;
        if (total > _totalMax) _totalMax = total;

        _last = (int[])v.Clone();
        Count++;
        return true;
    }

    /// <summary>Builds an immutable snapshot of the current tally (cheap; clones the small per-rank arrays).</summary>
    public RollTallySnapshot Snapshot()
    {
        var mean = new double[RankCount];
        var min = new int[RankCount];
        var max = new int[RankCount];
        for (int i = 0; i < RankCount; i++)
        {
            mean[i] = Count == 0 ? 0 : (double)_rankSum[i] / Count;
            min[i] = Count == 0 ? 0 : _rankMin[i];
            max[i] = Count == 0 ? 0 : _rankMax[i];
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

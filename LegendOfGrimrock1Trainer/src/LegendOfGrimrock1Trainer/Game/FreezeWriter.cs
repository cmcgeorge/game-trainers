namespace LegendOfGrimrock1Trainer.Game;

/// <summary>
/// Re-applies frozen stats, once per refresh.
///
/// Separate from the session view-model, and static, purely so it can be tested: the write side of a
/// freeze is the part that actually touches the game, and it used to live where nothing without a
/// WPF dispatcher could reach it. The display side of a freeze is <c>StatRowViewModel</c>'s business.
/// </summary>
public static class FreezeWriter
{
    /// <summary>
    /// How far a stat may drift before the freeze re-writes it. Grimrock's numbers are whole, so half
    /// a point is "the same value" and skipping the write keeps a freeze from issuing four pointless
    /// <c>WriteProcessMemory</c> calls a second per frozen stat.
    /// </summary>
    public const double Tolerance = 0.5;

    /// <summary>
    /// Holds one champion's frozen stats at their targets against a snapshot read this tick, and
    /// reports how many were written.
    /// </summary>
    public static int Apply(TrainerActions actions, PartySnapshot party, int championIndex,
                            IEnumerable<(string Name, double Value)> frozen)
    {
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(party);
        ArgumentNullException.ThrowIfNull(frozen);

        var champion = party.Champions.FirstOrDefault(c => c.Index == championIndex);
        if (champion is null) return 0;

        int written = 0;
        foreach (var (name, value) in frozen)
        {
            var stat = champion.Stat(name);
            if (stat is null) continue;

            // The cap is checked as well as the value: a bar frozen above its own maximum needs the
            // maximum raised too, and it would otherwise be re-written every tick without ever
            // reaching the target.
            if (Math.Abs(stat.Value - value) < Tolerance && stat.Max >= value) continue;
            if (actions.SetStat(champion, name, value).Applied > 0) written++;
        }
        return written;
    }
}

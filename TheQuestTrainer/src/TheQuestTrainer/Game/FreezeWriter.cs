namespace TheQuestTrainer.Game;

/// <summary>A field the trainer can hold at a value.</summary>
public enum FrozenField
{
    /// <summary>Current health — the closest thing to god mode this game has.</summary>
    Health,

    /// <summary>Current mana — spells stop costing anything in practice.</summary>
    Mana,

    /// <summary>Gold — purchases still deduct, they are just put back four times a second.</summary>
    Gold,

    /// <summary>Outstanding crime — guards stop having a reason to arrest you.</summary>
    Crime,

    /// <summary>
    /// Poison, disease, curse and paralysis, kept off the character.
    ///
    /// The odd one out, and deliberately so. There is no value to latch: a condition is a list of
    /// effect objects rather than a number, so this entry re-runs the cure on every tick instead of
    /// rewriting a field, and <see cref="FreezeWriter.TargetOf"/> means nothing for it. That also
    /// makes it a *cure* rather than an immunity — the game still inflicts the condition and the
    /// trainer still takes it away, so a poison inflicted between two ticks costs its turn of health
    /// before it goes. Anything the game does not consider curable is left alone, exactly as when
    /// the button is pressed.
    /// </summary>
    Conditions,
}

/// <summary>
/// What the trainer re-applies on every tick: fields held at a latched value, and — for
/// <see cref="FrozenField.Conditions"/>, which has no value to hold — a cure re-run.
///
/// The target is captured when the box is ticked and never re-derived from what is on screen: the
/// refresh overwrites the displayed value with whatever the game currently holds, so a derived
/// target would follow the damage down and the freeze would oscillate between two numbers four
/// times a second.
///
/// This lives apart from the view model so it can be exercised without a WPF dispatcher — the
/// harness drives <see cref="Tick"/> against a fake process and checks what was written.
/// </summary>
public sealed class FreezeWriter
{
    private readonly Dictionary<FrozenField, long> _targets = new();

    /// <summary>Fields currently held, in no particular order.</summary>
    public IReadOnlyCollection<FrozenField> Frozen => _targets.Keys;

    /// <summary>Whether anything is being held.</summary>
    public bool Any => _targets.Count > 0;

    /// <summary>Starts holding <paramref name="field"/> at <paramref name="value"/>.</summary>
    public void Freeze(FrozenField field, long value) => _targets[field] = value;

    /// <summary>Stops holding <paramref name="field"/>.</summary>
    public void Thaw(FrozenField field) => _targets.Remove(field);

    /// <summary>Stops holding everything. Called on detach so a re-attach starts clean.</summary>
    public void ThawAll() => _targets.Clear();

    /// <summary>Whether <paramref name="field"/> is held.</summary>
    public bool IsFrozen(FrozenField field) => _targets.ContainsKey(field);

    /// <summary>The latched target for <paramref name="field"/>, or null when it is not held.</summary>
    public long? TargetOf(FrozenField field) => _targets.TryGetValue(field, out long v) ? v : null;

    /// <summary>
    /// Rewrites every held field. Returns the number of writes that succeeded; a failure is not
    /// fatal — the record may have moved, and the next refresh will notice and say so.
    /// </summary>
    public int Tick(TrainerActions actions, uint record)
    {
        ArgumentNullException.ThrowIfNull(actions);
        if (record == 0 || _targets.Count == 0) return 0;

        int written = 0;
        foreach (var (field, value) in _targets)
        {
            var result = field switch
            {
                FrozenField.Health => actions.SetHealth(record, (int)value),
                FrozenField.Mana => actions.SetMana(record, (int)value),
                FrozenField.Gold => actions.SetGold(record, value),
                FrozenField.Crime => actions.SetCrime(record, value),
                // The one entry that ignores its latched value: there is nothing to hold a
                // condition at, so the cure is simply run again.
                FrozenField.Conditions => actions.CureConditions(record),
                _ => ActionResult.Failure($"Unknown field {field}."),
            };
            if (result.Ok) written++;
        }
        return written;
    }
}

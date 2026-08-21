using DarksypreTrainer.Memory;

namespace DarksypreTrainer.ViewModels;

/// <summary>
/// Editable view over the located character. DarkSpyre splits the state across three
/// structures (see <see cref="CharacterFormat"/>), so each setter writes to whichever one the
/// game actually plays out of:
///
/// <list type="bullet">
/// <item>current HP and SP go to the <b>player actor</b> — the status block is a per-frame copy,
/// so writing there would be undone on the next tick;</item>
/// <item>maximum HP, SP and encumbrance, and the six attributes, go to the <b>character
/// record</b>;</item>
/// <item>current encumbrance is shown read-only: the game recomputes it from what you carry.</item>
/// </list>
/// </summary>
public sealed class CharacterViewModel : ObservableObject
{
    private readonly ICharacterHost _host;
    private readonly byte[] _status;
    private readonly byte[] _record;
    private readonly byte[] _actor = new byte[4];   // current HP and SP, read as one pair
    private readonly byte[] _previousAttributes = new byte[CharacterFormat.AttributeCount];

    /// <summary>Address of the status block the on-screen bars read from.</summary>
    public nuint StatusAddress { get; }

    /// <summary>Address of the character record (attributes and maxima).</summary>
    public nuint RecordAddress { get; }

    /// <summary>Address of the player actor (current HP and SP).</summary>
    public nuint ActorAddress { get; }

    public ObservableCollection<NamedValueViewModel> Attributes { get; } = new();

    public CharacterViewModel(ICharacterHost host, LocatedCharacter located)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(located);
        _host = host;
        _status = located.Status;
        _record = located.Record;
        StatusAddress = located.StatusAddress;
        RecordAddress = located.RecordAddress;
        ActorAddress = located.ActorAddress;

        Array.Copy(located.ActorVitals, _actor, _actor.Length);

        for (int i = 0; i < CharacterFormat.AttributeCount; i++)
        {
            int index = i;
            Attributes.Add(new NamedValueViewModel(
                CharacterFormat.AttributeNames[index],
                () => _record[CharacterFormat.RecordAttributes + index],
                v => SetAttribute(index, v)));
        }

        _freezeHpValue = CurrentHp;
        _freezeSpValue = CurrentSp;
    }

    // ---- vitals -------------------------------------------------------------
    /// <summary>Current hit points. Written to the player actor, which the game plays out of.</summary>
    public int CurrentHp
    {
        get => CharacterFormat.ReadU16(_actor, 0);
        set
        {
            int v = Clamp(value, 0, CharacterFormat.LocatorMaxVital);
            CharacterFormat.WriteU16(_actor, 0, v);
            WriteActorVitals(0);
            _freezeHpValue = v;
            OnPropertyChanged();
        }
    }

    /// <summary>Current spell points. Written to the player actor.</summary>
    public int CurrentSp
    {
        get => CharacterFormat.ReadU16(_actor, 2);
        set
        {
            int v = Clamp(value, 0, CharacterFormat.LocatorMaxVital);
            CharacterFormat.WriteU16(_actor, 2, v);
            WriteActorVitals(2);
            _freezeSpValue = v;
            OnPropertyChanged();
        }
    }

    /// <summary>Weight carried right now. Read-only — the game recomputes it from your inventory.</summary>
    public int CurrentEncumbrance => CharacterFormat.ReadU16(_status, CharacterFormat.StatusCurrentEnc);

    public int MaxHp
    {
        get => CharacterFormat.ReadU16(_record, CharacterFormat.RecordMaxHp);
        set => SetRecordWord(CharacterFormat.RecordMaxHp, value);
    }

    public int MaxSp
    {
        get => CharacterFormat.ReadU16(_record, CharacterFormat.RecordMaxSp);
        set => SetRecordWord(CharacterFormat.RecordMaxSp, value);
    }

    public int MaxEncumbrance
    {
        get => CharacterFormat.ReadU16(_record, CharacterFormat.RecordMaxEnc);
        set => SetRecordWord(CharacterFormat.RecordMaxEnc, value);
    }

    // ---- freezes ------------------------------------------------------------
    private int _freezeHpValue;
    private int _freezeSpValue;

    private bool _freezeHp;
    /// <summary>Re-writes current HP every poll tick, so nothing can damage you below it.</summary>
    public bool FreezeHp
    {
        get => _freezeHp;
        set { if (SetField(ref _freezeHp, value) && value) _freezeHpValue = CurrentHp; }
    }

    private bool _freezeSp;
    /// <summary>Re-writes current SP every poll tick, so casting never runs you dry.</summary>
    public bool FreezeSp
    {
        get => _freezeSp;
        set { if (SetField(ref _freezeSp, value) && value) _freezeSpValue = CurrentSp; }
    }

    /// <summary>Re-applies whichever freezes are on. Called from the poll loop.</summary>
    public void ApplyFreezes()
    {
        if (_freezeHp)
        {
            CharacterFormat.WriteU16(_actor, 0, _freezeHpValue);
            WriteActorVitals(0);
        }
        if (_freezeSp)
        {
            CharacterFormat.WriteU16(_actor, 2, _freezeSpValue);
            WriteActorVitals(2);
        }
    }

    // ---- quick actions ------------------------------------------------------
    /// <summary>Sets current HP and SP to their maxima.</summary>
    public void Refill()
    {
        CurrentHp = MaxHp;
        CurrentSp = MaxSp;
    }

    /// <summary>Raises every attribute to the game's cap of <see cref="GameFacts.MaxAttribute"/>.</summary>
    public void MaxAttributes()
    {
        for (int i = 0; i < CharacterFormat.AttributeCount; i++)
            SetAttribute(i, GameFacts.MaxAttribute);
        foreach (var a in Attributes) a.Refresh();
    }

    // ---- refresh ------------------------------------------------------------
    /// <summary>
    /// Pushes freshly read bytes into the view. Returns false when the structures no longer look
    /// like a live character, which is the signal to locate again (a level change moves the
    /// creature table, and quitting to the menu tears it down).
    /// </summary>
    public bool Refresh(byte[] status, byte[] record, byte[] actorVitals)
    {
        if (status.Length < CharacterFormat.StatusSize || record.Length < CharacterFormat.RecordSize
            || actorVitals.Length < 4)
            return false;

        // Only the fields that actually moved raise PropertyChanged. This runs five times a
        // second against boxes the user may be typing in, and WPF pushes the source value back
        // into a focused TextBox on every notification — so notifying unconditionally would make
        // the maxima and attributes impossible to edit.
        int hp = CurrentHp, sp = CurrentSp, enc = CurrentEncumbrance;
        int maxHp = MaxHp, maxSp = MaxSp, maxEnc = MaxEncumbrance;
        Array.Copy(_record, CharacterFormat.RecordAttributes, _previousAttributes, 0, _previousAttributes.Length);

        Array.Copy(status, _status, CharacterFormat.StatusSize);
        Array.Copy(record, _record, CharacterFormat.RecordSize);
        Array.Copy(actorVitals, _actor, 4);

        bool changed = false;
        if (CurrentHp != hp) { OnPropertyChanged(nameof(CurrentHp)); changed = true; }
        if (CurrentSp != sp) { OnPropertyChanged(nameof(CurrentSp)); changed = true; }
        if (CurrentEncumbrance != enc) { OnPropertyChanged(nameof(CurrentEncumbrance)); changed = true; }
        if (MaxHp != maxHp) { OnPropertyChanged(nameof(MaxHp)); changed = true; }
        if (MaxSp != maxSp) { OnPropertyChanged(nameof(MaxSp)); changed = true; }
        if (MaxEncumbrance != maxEnc) { OnPropertyChanged(nameof(MaxEncumbrance)); changed = true; }
        for (int i = 0; i < _previousAttributes.Length; i++)
            if (_record[CharacterFormat.RecordAttributes + i] != _previousAttributes[i]) Attributes[i].Refresh();
        if (changed) OnPropertyChanged(nameof(Summary));

        return CharacterFormat.IsCharacterRecord(_record, 0, MaxHp, MaxSp, MaxEncumbrance);
    }

    /// <summary>One-line status for the header, mirroring the game's own bars.</summary>
    public string Summary =>
        $"HP {CurrentHp}/{MaxHp}    SP {CurrentSp}/{MaxSp}    ENC {CurrentEncumbrance}/{MaxEncumbrance}";

    /// <summary>Where each structure was found, for the status line and bug reports.</summary>
    public string AddressSummary =>
        $"actor 0x{(ulong)ActorAddress:X}   record 0x{(ulong)RecordAddress:X}   status 0x{(ulong)StatusAddress:X}";

    // ---- internals ----------------------------------------------------------
    private void SetAttribute(int index, int value)
    {
        _record[CharacterFormat.RecordAttributes + index] = (byte)Clamp(value, 1, GameFacts.MaxAttribute);
        _host.WriteBytes(RecordAddress, _record, CharacterFormat.RecordAttributes + index, 1);
    }

    private void SetRecordWord(int offset, int value, [System.Runtime.CompilerServices.CallerMemberName] string? property = null)
    {
        CharacterFormat.WriteU16(_record, offset, Clamp(value, 0, CharacterFormat.LocatorMaxVital));
        _host.WriteBytes(RecordAddress, _record, offset, 2);
        OnPropertyChanged(property);
        OnPropertyChanged(nameof(Summary));
    }

    /// <summary>
    /// Writes one of the two live vitals. <c>_actor</c> mirrors the four bytes at the actor's HP
    /// field, so the field offset is 0 for hit points and 2 for spell points — and because the host
    /// applies that offset to the address as well, the actor's SP field lands correctly at
    /// <see cref="CharacterFormat.ActorCurrentSp"/>.
    /// </summary>
    private void WriteActorVitals(int offset) =>
        _host.WriteBytes(ActorAddress + CharacterFormat.ActorCurrentHp, _actor, offset, 2);

    private static int Clamp(int value, int min, int max) => value < min ? min : value > max ? max : value;
}

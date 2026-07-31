namespace AirborneRangerTrainer.Game;

/// <summary>
/// A typed, mutable view over the live mission-state window read out of the game.
///
/// <para>Every setter clamps to what the game can represent and then calls back through
/// <c>flush</c> with the exact <c>DGROUP</c> offset and byte range it touched, so the shell writes
/// one or two bytes rather than the whole window. That keeps a write from clobbering a neighbouring
/// field the game changed a millisecond ago.</para>
/// </summary>
public sealed class MissionState
{
    private readonly byte[] _buffer;
    private readonly Action<int, int>? _flush;   // (dgroupOffset, length)

    /// <summary>Wraps <paramref name="buffer"/>, a read of <see cref="MissionFormat.WindowLength"/> bytes.</summary>
    /// <param name="buffer">The window, starting at <see cref="MissionFormat.WindowStart"/>.</param>
    /// <param name="flush">Called with the <c>DGROUP</c> offset and length of every write, or null for a detached view.</param>
    public MissionState(byte[] buffer, Action<int, int>? flush = null)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        if (buffer.Length < MissionFormat.WindowLength)
            throw new ArgumentException(
                $"Mission-state window must be at least {MissionFormat.WindowLength} bytes.", nameof(buffer));
        _buffer = buffer;
        _flush = flush;
    }

    /// <summary>The underlying window, so the poll loop can refresh it in place.</summary>
    public byte[] Buffer => _buffer;

    private static int Rel(int dgroupOffset) => dgroupOffset - MissionFormat.WindowStart;

    private byte GetU8(int dgroupOffset) => _buffer[Rel(dgroupOffset)];

    private void SetU8(int dgroupOffset, int value, int min, int max)
    {
        byte v = (byte)Math.Clamp(value, min, max);
        int i = Rel(dgroupOffset);
        if (_buffer[i] == v) return;
        _buffer[i] = v;
        _flush?.Invoke(dgroupOffset, 1);
    }

    private ushort GetU16(int dgroupOffset) => MissionFormat.ReadU16(_buffer, Rel(dgroupOffset));

    private void SetU16(int dgroupOffset, int value, int min, int max)
    {
        ushort v = (ushort)Math.Clamp(value, min, max);
        int i = Rel(dgroupOffset);
        if (MissionFormat.ReadU16(_buffer, i) == v) return;
        MissionFormat.WriteU16(_buffer, i, v);
        _flush?.Invoke(dgroupOffset, 2);
    }

    // --- condition -----------------------------------------------------------

    /// <summary>Wounds taken. <see cref="MissionFormat.FatalWounds"/> is death.</summary>
    public int Wounds
    {
        get => GetU8(MissionFormat.OffWounds);
        set => SetU8(MissionFormat.OffWounds, value, 0, MissionFormat.FatalWounds + 1);
    }

    /// <summary>First-aid kits carried. One kit removes one wound.</summary>
    public int FirstAidKits
    {
        get => GetU8(MissionFormat.OffFirstAidKits);
        set => SetU8(MissionFormat.OffFirstAidKits, value, 0, MissionFormat.SupplyCeiling);
    }

    // --- ammunition ----------------------------------------------------------

    /// <summary>Rounds left in the loaded magazine; negative means no magazine is loaded.</summary>
    public int RoundsInMagazine
    {
        get => MissionFormat.ReadI8(_buffer, Rel(MissionFormat.OffRoundsInMagazine));
        set
        {
            sbyte v = (sbyte)Math.Clamp(value, sbyte.MinValue, MissionFormat.FullMagazine);
            int i = Rel(MissionFormat.OffRoundsInMagazine);
            if (unchecked((sbyte)_buffer[i]) == v) return;
            _buffer[i] = unchecked((byte)v);
            _flush?.Invoke(MissionFormat.OffRoundsInMagazine, 1);
        }
    }

    /// <summary>Spare carbine magazines. The game's panel shows this plus the loaded one.</summary>
    public int SpareMagazines
    {
        get => GetU8(MissionFormat.OffSpareMagazines);
        set => SetU8(MissionFormat.OffSpareMagazines, value, 0, MissionFormat.SupplyCeiling);
    }

    /// <summary>Hand grenades.</summary>
    public int Grenades
    {
        get => GetU8(MissionFormat.OffGrenades);
        set => SetU8(MissionFormat.OffGrenades, value, 0, MissionFormat.SupplyCeiling);
    }

    /// <summary>LAW rockets.</summary>
    public int LawRockets
    {
        get => GetU8(MissionFormat.OffLawRockets);
        set => SetU8(MissionFormat.OffLawRockets, value, 0, MissionFormat.SupplyCeiling);
    }

    /// <summary>Time bombs.</summary>
    public int TimeBombs
    {
        get => GetU8(MissionFormat.OffTimeBombs);
        set => SetU8(MissionFormat.OffTimeBombs, value, 0, MissionFormat.SupplyCeiling);
    }

    /// <summary>Magazine count as the game's own status panel prints it.</summary>
    public int DisplayedMagazines =>
        MissionFormat.DisplayedMagazines(SpareMagazines, RoundsInMagazine);

    // --- read-only readouts --------------------------------------------------

    /// <summary>Carried weight as the panel prints it — the stored total plus the loaded magazine.</summary>
    public int CarriedWeight =>
        GetU16(MissionFormat.OffCarriedWeight) + GetU16(MissionFormat.OffMagazineLoaded);

    /// <summary>Selected weapon code; see <see cref="WeaponBook"/>.</summary>
    public int SelectedWeapon => GetU8(MissionFormat.OffSelectedWeapon);

    /// <summary>Name of the selected weapon, or a placeholder for an unknown code.</summary>
    public string SelectedWeaponName => WeaponBook.Name(SelectedWeapon);

    // --- clock ---------------------------------------------------------------

    /// <summary>Mission countdown, composed from the three digit bytes the game stores.</summary>
    public int Clock
    {
        get => MissionFormat.ComposeClock(
            GetU8(MissionFormat.OffClockHundreds),
            GetU8(MissionFormat.OffClockTens),
            GetU8(MissionFormat.OffClockUnits));
        set
        {
            var (h, t, u) = MissionFormat.SplitClock(value);
            // The three digits are adjacent, so write them as one range rather than three.
            int i = Rel(MissionFormat.OffClockHundreds);
            if (_buffer[i] == h && _buffer[i + 1] == t && _buffer[i + 2] == u) return;
            _buffer[i] = h;
            _buffer[i + 1] = t;
            _buffer[i + 2] = u;
            _flush?.Invoke(MissionFormat.OffClockHundreds, 3);
        }
    }

    // --- score ---------------------------------------------------------------

    /// <summary>Merit points earned so far this mission.</summary>
    public int MeritPoints
    {
        get => GetU16(MissionFormat.OffMeritPoints);
        set => SetU16(MissionFormat.OffMeritPoints, value, 0, MissionFormat.MeritCeiling);
    }

    /// <summary>Enemy soldiers eliminated this mission.</summary>
    public int SoldiersKilled
    {
        get => GetU8(MissionFormat.OffSoldiersKilled);
        set => SetU8(MissionFormat.OffSoldiersKilled, value, 0, MissionFormat.SupplyCeiling);
    }

    /// <summary>Military targets destroyed this mission.</summary>
    public int TargetsDestroyed
    {
        get => GetU8(MissionFormat.OffTargetsDestroyed);
        set => SetU8(MissionFormat.OffTargetsDestroyed, value, 0, MissionFormat.SupplyCeiling);
    }

    // --- bulk actions --------------------------------------------------------

    /// <summary>Clears every wound.</summary>
    public void Heal() => Wounds = 0;

    /// <summary>
    /// Tops up every ammunition counter and reloads the magazine. Spare magazines stop one short of
    /// the other counters — see <see cref="MissionFormat.MaxSpareMagazines"/>.
    /// </summary>
    public void Resupply()
    {
        RoundsInMagazine = MissionFormat.FullMagazine;
        SpareMagazines = MissionFormat.MaxSpareMagazines;
        Grenades = MissionFormat.MaxSupply;
        LawRockets = MissionFormat.MaxSupply;
        TimeBombs = MissionFormat.MaxSupply;
        FirstAidKits = MissionFormat.MaxSupply;
    }

    /// <summary>Sets the countdown to the largest value the three-digit display can show.</summary>
    public void MaxClock() => Clock = MissionFormat.MaxClock;

    /// <summary>Heals, resupplies and refills the clock.</summary>
    public void MaxEverything()
    {
        Heal();
        Resupply();
        MaxClock();
    }
}

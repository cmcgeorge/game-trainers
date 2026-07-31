using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Input;
using AirborneRangerTrainer.Game;
using AirborneRangerTrainer.Memory;

namespace AirborneRangerTrainer.ViewModels;

/// <summary>
/// The located mission: editable fields on one side, a live read-only mirror on the other.
///
/// <para>Editable properties write straight into the game. The <c>Live*</c> properties come from the
/// poll loop, so the panel showing what the game currently holds never fights a half-typed value in
/// a text box — which matters here more than in most trainers, because the game rewrites these
/// counters continuously and never pauses.</para>
///
/// <para>The editable buffer is a <i>shadow</i> of what the trainer last wrote, not of what the game
/// holds. Every path that compares against it therefore refreshes the field from the live mirror
/// first (<see cref="SyncFromLive"/>); without that, setting a field back to a value the trainer had
/// already written would look like a no-op and never reach the game.</para>
/// </summary>
public sealed class MissionViewModel : ObservableObject
{
    private readonly IMissionHost _host;
    private readonly byte[] _buffer;
    private readonly byte[] _liveBuffer = new byte[MissionFormat.WindowLength];
    private readonly MissionState _state;
    private readonly MissionState _live;

    /// <summary>Live address of <c>DGROUP:0000</c> in the attached process.</summary>
    public nuint DgroupAddress { get; }

    /// <summary>How the data segment was found, for the status bar.</summary>
    public string LocateMethod { get; }

    /// <summary>How many corroborating literals matched.</summary>
    public int ValidatorsMatched { get; }

    /// <summary>Builds a view-model over a successful locate.</summary>
    public MissionViewModel(IMissionHost host, LocateResult located)
    {
        ArgumentNullException.ThrowIfNull(host);
        if (!located.Found) throw new ArgumentException("Locate result holds no mission state.", nameof(located));

        _host = host;
        DgroupAddress = located.DgroupAddress;
        LocateMethod = located.Method;
        ValidatorsMatched = located.ValidatorsMatched;

        _buffer = new byte[MissionFormat.WindowLength];
        Array.Copy(located.Window, _buffer, Math.Min(located.Window.Length, _buffer.Length));

        // Seed the live mirror from the same read. Leaving it zeroed until the first poll tick would
        // mean a freeze armed in that window — or while a later Reread is failing — pinned zero and
        // then wrote zero into the game on the next tick, emptying the loadout and running the
        // countdown to nothing.
        Array.Copy(located.Window, _liveBuffer, Math.Min(located.Window.Length, _liveBuffer.Length));

        _state = new MissionState(_buffer, Flush);
        _live = new MissionState(_liveBuffer);

        HealCommand = new RelayCommand(() => Bulk(_state.Heal, Pins.Wounds, "Wounds cleared."));
        ResupplyCommand = new RelayCommand(() => Bulk(_state.Resupply, Pins.Ammo,
            $"Ammunition topped up ({MissionFormat.MaxSpareMagazines} spare magazines, " +
            $"{MissionFormat.MaxSupply} of everything else) and the magazine reloaded."));
        MaxClockCommand = new RelayCommand(() => Bulk(_state.MaxClock, Pins.Clock,
            $"Mission clock set to {MissionFormat.MaxClock}."));
        MaxEverythingCommand = new RelayCommand(() => Bulk(_state.MaxEverything, Pins.All,
            "Healed, resupplied, and the clock refilled."));
        ReloadCommand = new RelayCommand(() => _host.ReportStatus(
            ReloadFromGame()
                ? "Reloaded the editable fields from the game."
                : "Nothing polled yet — give it a moment and try again."));
    }

    // --- commands ------------------------------------------------------------

    /// <summary>Clears every wound.</summary>
    public ICommand HealCommand { get; }

    /// <summary>Fills every ammunition counter and reloads the magazine.</summary>
    public ICommand ResupplyCommand { get; }

    /// <summary>Sets the countdown to its three-digit maximum.</summary>
    public ICommand MaxClockCommand { get; }

    /// <summary>Heal, resupply and refill the clock in one go.</summary>
    public ICommand MaxEverythingCommand { get; }

    /// <summary>Copies the live values back into the editable fields.</summary>
    public ICommand ReloadCommand { get; }

    // --- editable fields -----------------------------------------------------
    //
    // Every setter refreshes its own field from the live mirror, clamps, writes only when the value
    // actually moves, and then notifies if *either* the value moved or the caller's input had to be
    // clamped — the second case is what makes a text box that was handed an out-of-range number snap
    // back to what was really written instead of sitting there showing a value the game never got.

    /// <summary>Wounds taken; three is death.</summary>
    public int Wounds
    {
        get => _state.Wounds;
        set => Edit(MissionFormat.OffWounds, 1, value, 0, MissionFormat.FatalWounds + 1,
                    () => _state.Wounds, v => _state.Wounds = v, () => PinWounds(explicitly: true));
    }

    /// <summary>Rounds left in the loaded magazine; negative means no magazine.</summary>
    public int RoundsInMagazine
    {
        get => _state.RoundsInMagazine;
        set => Edit(MissionFormat.OffRoundsInMagazine, 1, value, sbyte.MinValue, MissionFormat.FullMagazine,
                    () => _state.RoundsInMagazine, v => _state.RoundsInMagazine = v,
                    () => PinAmmoField(() => _pinnedRounds = _state.RoundsInMagazine), derived: true);
    }

    /// <summary>Spare carbine magazines.</summary>
    public int SpareMagazines
    {
        get => _state.SpareMagazines;
        set => Edit(MissionFormat.OffSpareMagazines, 1, value, 0, MissionFormat.SupplyCeiling,
                    () => _state.SpareMagazines, v => _state.SpareMagazines = v,
                    () => PinAmmoField(() => _pinnedMagazines = _state.SpareMagazines), derived: true);
    }

    /// <summary>Hand grenades.</summary>
    public int Grenades
    {
        get => _state.Grenades;
        set => Edit(MissionFormat.OffGrenades, 1, value, 0, MissionFormat.SupplyCeiling,
                    () => _state.Grenades, v => _state.Grenades = v,
                    () => PinAmmoField(() => _pinnedGrenades = _state.Grenades));
    }

    /// <summary>LAW rockets.</summary>
    public int LawRockets
    {
        get => _state.LawRockets;
        set => Edit(MissionFormat.OffLawRockets, 1, value, 0, MissionFormat.SupplyCeiling,
                    () => _state.LawRockets, v => _state.LawRockets = v,
                    () => PinAmmoField(() => _pinnedRockets = _state.LawRockets));
    }

    /// <summary>Time bombs.</summary>
    public int TimeBombs
    {
        get => _state.TimeBombs;
        set => Edit(MissionFormat.OffTimeBombs, 1, value, 0, MissionFormat.SupplyCeiling,
                    () => _state.TimeBombs, v => _state.TimeBombs = v,
                    () => PinAmmoField(() => _pinnedBombs = _state.TimeBombs));
    }

    /// <summary>First-aid kits.</summary>
    public int FirstAidKits
    {
        get => _state.FirstAidKits;
        set => Edit(MissionFormat.OffFirstAidKits, 1, value, 0, MissionFormat.SupplyCeiling,
                    () => _state.FirstAidKits, v => _state.FirstAidKits = v,
                    () => PinAmmoField(() => _pinnedKits = _state.FirstAidKits));
    }

    /// <summary>Mission countdown.</summary>
    public int Clock
    {
        get => _state.Clock;
        set => Edit(MissionFormat.OffClockHundreds, 3, value, 0, MissionFormat.MaxClock,
                    () => _state.Clock, v => _state.Clock = v, () => PinClock(explicitly: true));
    }

    /// <summary>Merit points earned this mission.</summary>
    public int MeritPoints
    {
        get => _state.MeritPoints;
        set => Edit(MissionFormat.OffMeritPoints, 2, value, 0, MissionFormat.MeritCeiling,
                    () => _state.MeritPoints, v => _state.MeritPoints = v, null);
    }

    /// <summary>Enemy soldiers eliminated this mission.</summary>
    public int SoldiersKilled
    {
        get => _state.SoldiersKilled;
        set => Edit(MissionFormat.OffSoldiersKilled, 1, value, 0, MissionFormat.SupplyCeiling,
                    () => _state.SoldiersKilled, v => _state.SoldiersKilled = v, null);
    }

    /// <summary>Military targets destroyed this mission.</summary>
    public int TargetsDestroyed
    {
        get => _state.TargetsDestroyed;
        set => Edit(MissionFormat.OffTargetsDestroyed, 1, value, 0, MissionFormat.SupplyCeiling,
                    () => _state.TargetsDestroyed, v => _state.TargetsDestroyed = v, null);
    }

    /// <summary>Magazine count as the game's own panel prints it, from the editable values.</summary>
    public int DisplayedMagazines => _state.DisplayedMagazines;

    // --- freeze toggles ------------------------------------------------------

    private bool _freezeWounds;
    private bool _freezeAmmo;
    private bool _freezeClock;

    // A freeze armed outside a mission has nothing meaningful to pin, so it records that its pin is
    // provisional and re-takes it on the first tick of a running mission.
    private bool _woundsPinProvisional;
    private bool _ammoPinProvisional;
    private bool _clockPinProvisional;

    private int _pinnedWounds;
    private int _pinnedRounds;
    private int _pinnedMagazines;
    private int _pinnedGrenades;
    private int _pinnedRockets;
    private int _pinnedBombs;
    private int _pinnedKits;
    private int _pinnedClock;

    /// <summary>
    /// True while a mission is actually under way, which the running countdown marks. Outside one the
    /// mission block holds zeros (nothing has been played yet) or the previous mission's leftovers,
    /// and neither is a value worth pinning or restoring.
    /// </summary>
    public bool MissionIsRunning => _live.Clock > 0;

    /// <summary>
    /// Holds the wound counter where it is on every poll tick. Pinned at zero this is effectively
    /// invulnerability — but only as fast as the poll interval, so an instant-death event can still
    /// end the mission in the gap between ticks.
    /// </summary>
    public bool FreezeWounds
    {
        get => _freezeWounds;
        set { if (SetField(ref _freezeWounds, value) && value) PinWounds(); }
    }

    /// <summary>Holds every ammunition counter, including the rounds in the loaded magazine.</summary>
    public bool FreezeAmmo
    {
        get => _freezeAmmo;
        set { if (SetField(ref _freezeAmmo, value) && value) PinAmmo(); }
    }

    /// <summary>Holds the mission countdown where it is.</summary>
    public bool FreezeClock
    {
        get => _freezeClock;
        set { if (SetField(ref _freezeClock, value) && value) PinClock(); }
    }

    /// <summary>
    /// Captures the wound pin. <paramref name="explicitly"/> takes it from the editor (the user
    /// chose the value) rather than from the live mirror.
    ///
    /// <para>A pin taken while no mission is running is always <i>provisional</i>, whoever asked for
    /// it: outside a mission the block holds zeros or the last mission's leftovers, so there is
    /// nothing there worth holding. A provisional pin is re-taken on the first tick of the mission
    /// that starts next.</para>
    /// </summary>
    private void PinWounds(bool explicitly = false)
    {
        _pinnedWounds = explicitly ? _state.Wounds : _live.Wounds;
        _woundsPinProvisional = !MissionIsRunning;
    }

    private void PinAmmo(bool explicitly = false)
    {
        var from = explicitly ? _state : _live;
        _pinnedRounds = from.RoundsInMagazine;
        _pinnedMagazines = from.SpareMagazines;
        _pinnedGrenades = from.Grenades;
        _pinnedRockets = from.LawRockets;
        _pinnedBombs = from.TimeBombs;
        _pinnedKits = from.FirstAidKits;
        _ammoPinProvisional = !MissionIsRunning;
    }

    private void PinClock(bool explicitly = false)
    {
        _pinnedClock = explicitly ? _state.Clock : _live.Clock;
        _clockPinProvisional = !MissionIsRunning;
    }

    /// <summary>
    /// Re-pins one ammunition field the user has just edited, leaving the other five where they are.
    /// The group's provisional flag is refreshed too, so a single edit cannot leave the group in a
    /// state where the next tick discards it.
    /// </summary>
    private void PinAmmoField(Action pinEdited)
    {
        pinEdited();
        _ammoPinProvisional = !MissionIsRunning;
    }

    // --- live mirror ---------------------------------------------------------

    /// <summary>The buffer the poll loop refreshes.</summary>
    public byte[] LiveBuffer => _liveBuffer;

    /// <summary>One-line summary of the ranger's condition, as the game currently holds it.</summary>
    public string LiveCondition =>
        $"Wounds {_live.Wounds}/{MissionFormat.FatalWounds}   First aid {_live.FirstAidKits}   " +
        $"Weight {_live.CarriedWeight}   Weapon: {_live.SelectedWeaponName}";

    /// <summary>One-line summary of the ranger's ammunition, as the game currently holds it.</summary>
    public string LiveAmmo =>
        $"Magazines {_live.DisplayedMagazines} ({_live.SpareMagazines} spare + {_live.RoundsInMagazine} rounds loaded)   " +
        $"Grenades {_live.Grenades}   LAW rockets {_live.LawRockets}   Time bombs {_live.TimeBombs}";

    /// <summary>One-line summary of the clock and score, as the game currently holds it.</summary>
    public string LiveProgress =>
        $"Time {_live.Clock}   Merit points {_live.MeritPoints:N0}   " +
        $"Soldiers {_live.SoldiersKilled}   Targets {_live.TargetsDestroyed}" +
        (MissionIsRunning ? string.Empty : "   (no mission running)");

    private string _statusPanel = string.Empty;

    /// <summary>
    /// The game's own status panel, rendered from its text template. This is what the map screen
    /// shows, so it lags the live values until the game next redraws the panel.
    /// </summary>
    public string StatusPanel
    {
        get => _statusPanel;
        private set => SetField(ref _statusPanel, value);
    }

    /// <summary>Called by the poll loop after it has refreshed <see cref="LiveBuffer"/>.</summary>
    public void OnPolled()
    {
        _polled = true;
        ApplyFreezes();
        OnPropertyChanged(nameof(LiveCondition));
        OnPropertyChanged(nameof(LiveAmmo));
        OnPropertyChanged(nameof(LiveProgress));
        OnPropertyChanged(nameof(MissionIsRunning));
    }

    /// <summary>Called by the poll loop with the raw status-panel template.</summary>
    public void OnStatusPanel(byte[]? panel) => StatusPanel = RenderPanel(panel);

    /// <summary>
    /// Turns the game's byte-coded panel template into readable text: printable ASCII is kept, the
    /// newline control byte becomes a line break, and every other control byte becomes a space.
    /// </summary>
    public static string RenderPanel(byte[]? panel)
    {
        if (panel == null || panel.Length == 0) return string.Empty;
        var sb = new StringBuilder(panel.Length);
        foreach (byte b in panel)
        {
            if (b == 0x0D) sb.Append('\n');
            else if (b == 0xFF) break;                       // end of message
            else if (b is >= 0x20 and < 0x7F) sb.Append((char)b);
            else sb.Append(' ');
        }
        return string.Join("\n", sb.ToString().Split('\n').Select(l => l.TrimEnd())).Trim('\n');
    }

    // --- plumbing ------------------------------------------------------------

    [Flags]
    private enum Pins
    {
        None = 0,
        Wounds = 1,
        Ammo = 2,
        Clock = 4,
        All = Wounds | Ammo | Clock,
    }

    /// <summary>
    /// Writes one field's bytes into the game. <paramref name="length"/> is the byte range the
    /// setter touched, starting at <paramref name="dgroupOffset"/>.
    /// </summary>
    private void Flush(int dgroupOffset, int length)
    {
        int i = dgroupOffset - MissionFormat.WindowStart;
        var bytes = new byte[length];
        Array.Copy(_buffer, i, bytes, 0, length);
        if (_host.WriteBytes(dgroupOffset, bytes))
        {
            _writeFailed = false;
            return;
        }

        // Report the first failure only. When the game has gone away an armed freeze retries eight
        // fields several times a second, and repeating the message would bury the poll loop's more
        // useful "Lost the game — click Locate game" under a flicker of write errors.
        if (_writeFailed) return;
        _writeFailed = true;
        _host.ReportStatus($"Write to DGROUP:0x{dgroupOffset:X4} failed — is the game still running?");
    }

    private bool _writeFailed;

    /// <summary>
    /// Copies one field's current bytes from the live mirror into the editable shadow.
    ///
    /// <para>Both the edit path and the freeze path need this for the same reason: the shadow holds
    /// what the trainer last <i>wrote</i>, and the game moves these counters constantly. Comparing a
    /// new value against the shadow would suppress the write exactly when the game has drifted away
    /// from it — which is the case both paths exist to correct.</para>
    /// </summary>
    private void SyncFromLive(int dgroupOffset, int length)
    {
        int i = dgroupOffset - MissionFormat.WindowStart;
        Array.Copy(_liveBuffer, i, _buffer, i, length);
    }

    private void Edit(int dgroupOffset, int length, int requested, int min, int max,
                      Func<int> read, Action<int> apply, Action? repin,
                      bool derived = false, [CallerMemberName] string? name = null)
    {
        SyncFromLive(dgroupOffset, length);

        int clamped = Math.Clamp(requested, min, max);
        bool moved = clamped != read();
        if (moved) apply(clamped);

        // Re-pin whether or not the value moved. Asking a frozen field for the value the game
        // already holds is a perfectly sensible "hold it here" — and it is exactly the case where
        // nothing moves, so a repin guarded on `moved` would leave the old pin in force and the
        // next tick would undo the instruction the user just gave.
        repin?.Invoke();

        if (moved || clamped != requested)
        {
            OnPropertyChanged(name);
            if (derived) OnPropertyChanged(nameof(DisplayedMagazines));
        }
    }

    /// <summary>
    /// Runs a bulk action against the game's current values rather than whatever the editor last
    /// showed, then re-pins whichever freezes the action's fields belong to so a frozen field does
    /// not immediately undo the action.
    /// </summary>
    private void Bulk(Action action, Pins pins, string message)
    {
        ReloadFromGame();
        action();
        if ((pins & Pins.Wounds) != 0) PinWounds(explicitly: true);
        if ((pins & Pins.Ammo) != 0) PinAmmo(explicitly: true);
        if ((pins & Pins.Clock) != 0) PinClock(explicitly: true);
        RefreshAll();
        _host.ReportStatus(message);
    }

    /// <summary>
    /// Copies the live values into the editable buffer. False before the first successful poll —
    /// which is tracked with a flag rather than by testing the buffer for zeros, because a genuinely
    /// all-zero mission state is what a game sitting on its title screen actually holds.
    /// </summary>
    public bool ReloadFromGame()
    {
        if (!_polled) return false;
        Array.Copy(_liveBuffer, _buffer, _buffer.Length);
        RefreshAll();
        return true;
    }

    private bool _polled;

    private void ApplyFreezes()
    {
        // A freeze must never fire outside a mission: the block then holds zeros or the last
        // mission's leftovers, and restoring either into a mission that has just begun would hand
        // the player an empty loadout or a countdown already at zero.
        //
        // Marking the pins provisional here — rather than only when a freeze is armed — is what
        // makes a freeze hold values *for one mission*. Without it, a freeze armed midway through a
        // mission keeps that mission's pin across the boundary and forces it onto the next one:
        // the fresh 600-second clock clamped back to where the last mission ended, or a loadout
        // reset to whatever was left when the previous ranger died.
        if (!MissionIsRunning)
        {
            _woundsPinProvisional = true;
            _ammoPinProvisional = true;
            _clockPinProvisional = true;
            return;
        }

        if (_freezeWounds)
        {
            if (_woundsPinProvisional) PinWounds();
            else if (_live.Wounds != _pinnedWounds)
                Restore(MissionFormat.OffWounds, 1, () => _state.Wounds = _pinnedWounds);
        }

        if (_freezeAmmo)
        {
            if (_ammoPinProvisional)
            {
                PinAmmo();
            }
            else
            {
                if (_live.RoundsInMagazine != _pinnedRounds)
                    Restore(MissionFormat.OffRoundsInMagazine, 1, () => _state.RoundsInMagazine = _pinnedRounds);
                if (_live.SpareMagazines != _pinnedMagazines)
                    Restore(MissionFormat.OffSpareMagazines, 1, () => _state.SpareMagazines = _pinnedMagazines);
                if (_live.Grenades != _pinnedGrenades)
                    Restore(MissionFormat.OffGrenades, 1, () => _state.Grenades = _pinnedGrenades);
                if (_live.LawRockets != _pinnedRockets)
                    Restore(MissionFormat.OffLawRockets, 1, () => _state.LawRockets = _pinnedRockets);
                if (_live.TimeBombs != _pinnedBombs)
                    Restore(MissionFormat.OffTimeBombs, 1, () => _state.TimeBombs = _pinnedBombs);
                if (_live.FirstAidKits != _pinnedKits)
                    Restore(MissionFormat.OffFirstAidKits, 1, () => _state.FirstAidKits = _pinnedKits);
            }
        }

        if (_freezeClock)
        {
            if (_clockPinProvisional) PinClock();
            else if (_live.Clock != _pinnedClock)
                Restore(MissionFormat.OffClockHundreds, 3, () => _state.Clock = _pinnedClock);
        }
    }

    private void Restore(int dgroupOffset, int length, Action apply)
    {
        SyncFromLive(dgroupOffset, length);
        apply();
    }

    private void RefreshAll()
    {
        OnPropertyChanged(nameof(Wounds));
        OnPropertyChanged(nameof(RoundsInMagazine));
        OnPropertyChanged(nameof(SpareMagazines));
        OnPropertyChanged(nameof(Grenades));
        OnPropertyChanged(nameof(LawRockets));
        OnPropertyChanged(nameof(TimeBombs));
        OnPropertyChanged(nameof(FirstAidKits));
        OnPropertyChanged(nameof(Clock));
        OnPropertyChanged(nameof(MeritPoints));
        OnPropertyChanged(nameof(SoldiersKilled));
        OnPropertyChanged(nameof(TargetsDestroyed));
        OnPropertyChanged(nameof(DisplayedMagazines));
    }
}

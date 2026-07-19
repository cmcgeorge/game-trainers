using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Threading;
using MoriaTrainer.Game;

namespace MoriaTrainer.ViewModels;

// ---------------------------------------------------------------------------
// One directly-addressed character field: live read, editable target, freeze.
// ---------------------------------------------------------------------------

/// <summary>
/// A single located character field (HP, mana, gold, stat…) with a live display, an editable target
/// value, and an optional freeze that re-writes the target every poll tick.
/// </summary>
public sealed class CharPin : ObservableObject
{
    private readonly IScanHost _host;

    public string Label { get; }
    public nuint Address { get; }
    public ScanWidth Width { get; }

    private long _live;
    public long Live { get => _live; internal set => SetField(ref _live, value); }

    private string _target = "";
    /// <summary>User-editable target; on commit, pokes the value into RAM immediately.</summary>
    public string Target
    {
        get => _target;
        set
        {
            if (!SetField(ref _target, value)) return;
            if (ScanValue.TryParse(value, out long parsed) && ScanValue.FitsWidth(parsed, Width))
                if (!_host.Write(Address, parsed, Width)) _host.ReportWriteFailure(Address);
        }
    }

    private bool _frozen;
    public bool Frozen { get => _frozen; set => SetField(ref _frozen, value); }

    public CharPin(IScanHost host, string label, nuint address, ScanWidth width, long live)
    {
        _host = host; Label = label; Address = address; Width = width; _live = live;
        _target = live.ToString();
    }

    internal void ApplyFreeze()
    {
        if (!_frozen || !ScanValue.TryParse(_target, out long t)) return;
        if (!ScanValue.FitsWidth(t, Width)) return;
        _host.Write(Address, t, Width);
    }
}

// ---------------------------------------------------------------------------
// CharacterViewModel
// ---------------------------------------------------------------------------

/// <summary>
/// Backs the Stats tab. Auto-detects the <c>player_type.misc</c> struct base in DOSBox guest RAM by
/// scanning for the user-supplied Gold value (int32) then validating MaxHP, CurrentHP, Level, and
/// other fields at the <see cref="PlayerFormat"/> confirmed offsets. Once located, all confirmed
/// character fields are readable/writable directly and can be frozen.
///
/// <para>
/// The scan uses Gold as the primary anchor because it tends to be a distinctive value at game start
/// (e.g. 536 on the screenshot). The MaxHP hint is a required secondary discriminator.
/// </para>
/// </summary>
public sealed class CharacterViewModel : ObservableObject, IScanHost, IDisposable
{
    private ProcessMemory? _mem;
    private MemorySearcher? _searcher;
    private CancellationTokenSource? _cts;
    private readonly DispatcherTimer _poll;

    // --- locate hints (user fills these in from what they see on screen) ----
    private string _hintMaxHp = "";
    public string HintMaxHp { get => _hintMaxHp; set => SetField(ref _hintMaxHp, value); }

    private string _hintGold = "";
    public string HintGold { get => _hintGold; set => SetField(ref _hintGold, value); }

    // --- state --------------------------------------------------------------
    private bool _isLocated;
    public bool IsLocated { get => _isLocated; private set { if (SetField(ref _isLocated, value)) RaiseCommands(); } }

    private bool _isSearching;
    public bool IsSearching { get => _isSearching; private set { if (SetField(ref _isSearching, value)) RaiseCommands(); } }

    public bool IsAttached => _mem is { IsOpen: true };

    private string _status =
        "Attach on the Character tab, then enter the values you see on the C screen and click Auto-Detect.";
    public string Status { get => _status; set => SetField(ref _status, value); }

    // --- located pins -------------------------------------------------------
    public ObservableCollection<CharPin> Pins { get; } = new();

    // Convenience freeze-all shortcuts (write current HP = max HP, etc.)
    private bool _freezeHp;
    public bool FreezeHp
    {
        get => _freezeHp;
        set
        {
            if (!SetField(ref _freezeHp, value)) return;
            if (value)
            {
                // Sync Cur HP target to the live Max HP value so the freeze holds at the current ceiling.
                var maxPin = Pins.FirstOrDefault(p => p.Label == "Max HP");
                var chpPin = Pins.FirstOrDefault(p => p.Label == "Cur HP");
                if (chpPin != null && maxPin != null)
                    chpPin.Target = maxPin.Live.ToString();
            }
            ApplyNamedFreeze("Cur HP", value);
        }
    }

    private bool _freezeMana;
    public bool FreezeMana
    {
        get => _freezeMana;
        set
        {
            if (!SetField(ref _freezeMana, value)) return;
            if (value)
            {
                var mhpPin   = Pins.FirstOrDefault(p => p.Label == "Max Mana");
                var cmanaPin = Pins.FirstOrDefault(p => p.Label == "Cur Mana");
                if (cmanaPin != null && mhpPin != null)
                    cmanaPin.Target = mhpPin.Live.ToString();
            }
            ApplyNamedFreeze("Cur Mana", value);
        }
    }

    private bool _freezeFood;
    public bool FreezeFood
    {
        get => _freezeFood;
        set
        {
            if (!SetField(ref _freezeFood, value)) return;
            if (value)
            {
                var foodPin = Pins.FirstOrDefault(p => p.Label == "Food");
                if (foodPin != null) foodPin.Target = "15000";
            }
            ApplyNamedFreeze("Food", value);
        }
    }

    // --- commands -----------------------------------------------------------
    public ICommand AutoDetectCommand { get; }
    public ICommand ResetCommand { get; }

    public CharacterViewModel()
    {
        AutoDetectCommand = new RelayCommand(_ => _ = AutoDetectAsync(), _ => IsAttached && !IsSearching);
        ResetCommand = new RelayCommand(_ => Reset(), _ => IsLocated);

        _poll = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _poll.Tick += (_, _) => PollTick();
    }

    // --- attach/detach (called by MainViewModel) ----------------------------
    public void OnAttached(ProcessMemory mem)
    {
        _mem = mem;
        _searcher = new MemorySearcher(mem, ScanWidth.Int32);
        _poll.Start();
        OnPropertyChanged(nameof(IsAttached));
        RaiseCommands();
        Status = "Attached. Open the 'C' character screen in-game, enter Max HP and Gold, then Auto-Detect.";
    }

    public void OnDetached()
    {
        _poll.Stop();
        _cts?.Cancel();
        _mem = null;
        _searcher = null;
        Pins.Clear();
        IsLocated = false;
        _freezeHp = false; _freezeMana = false; _freezeFood = false;
        OnPropertyChanged(nameof(FreezeHp)); OnPropertyChanged(nameof(FreezeMana)); OnPropertyChanged(nameof(FreezeFood));
        OnPropertyChanged(nameof(IsAttached));
        RaiseCommands();
        Status = "Detached.";
    }

    // --- auto-detect --------------------------------------------------------

    /// <summary>
    /// Scans for the Gold int32 then validates MaxHP and other fields at confirmed struct offsets to
    /// locate the <c>py.misc</c> base. On success, populates <see cref="Pins"/> with all confirmed fields.
    /// </summary>
    private async Task AutoDetectAsync()
    {
        if (_mem is not { IsOpen: true } mem || _searcher == null) return;

        if (!ScanValue.TryParse(HintGold, out long gold) || gold < 0)
        { Status = "Enter the Gold value shown on the C screen (e.g. 536)."; return; }

        if (!ScanValue.TryParse(HintMaxHp, out long maxHp) || maxHp < 1 || maxHp > 9999)
        { Status = "Enter Max HP shown on the C screen (e.g. 10)."; return; }

        IsSearching = true;
        Status = "Scanning for Gold value…";
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        List<nuint> candidates = new();
        try
        {
            var searcher = _searcher;
            // Run the first scan and the candidate validation entirely on a background thread to avoid
            // blocking the UI thread with potentially thousands of ReadProcessMemory P/Invoke calls.
            await Task.Run(() =>
            {
                searcher.FirstScanExact(gold, ct);
                ct.ThrowIfCancellationRequested();
                foreach (var m in searcher.Take(5000))
                {
                    ct.ThrowIfCancellationRequested();
                    nuint miscBase = m.Address - (nuint)PlayerFormat.MiscAuOff;
                    if (ValidateMiscBase(mem, miscBase, (int)maxHp, (int)gold))
                        lock (candidates) candidates.Add(miscBase);
                }
            }, ct);
        }
        catch (OperationCanceledException) { Status = "Cancelled."; return; }
        catch (Exception ex) { Status = "Scan error: " + ex.Message; return; }
        finally { IsSearching = false; }

        // Staleness guard: discard if the user detached/re-attached while the scan ran.
        if (mem != _mem) return;

        switch (candidates.Count)
        {
            case 0:
                Status = "Not found. Make sure DOSBox is running UMoria and both values are correct. " +
                         "Try after a Gold change (buy/sell) to get a unique value.";
                return;
            case 1:
                PopulatePins(mem, candidates[0]);
                Status = $"Character located at 0x{(ulong)candidates[0]:X}. All stats are now live — edit a value or tick Freeze.";
                IsLocated = true;
                break;
            default:
                Status = $"{candidates.Count} candidates found. Change your Gold (buy or sell something) then Auto-Detect again to narrow to one.";
                return;
        }
    }

    private static bool ValidateMiscBase(ProcessMemory mem, nuint miscBase, int maxHp, int gold)
    {
        // maxhp (int32) at +0 must equal the hint
        if (!ReadInt32(mem, miscBase + (nuint)PlayerFormat.MiscMaxHpOff, out int mh) || mh != maxHp) return false;
        // chp (int32) at +4 must be in [0, maxhp]
        if (!ReadInt32(mem, miscBase + (nuint)PlayerFormat.MiscChpOff, out int chp) || chp < 0 || chp > mh) return false;
        // mhp (int32) at +12 must be in [0, 9999]
        if (!ReadInt32(mem, miscBase + (nuint)PlayerFormat.MiscMhpOff, out int mhp) || mhp < 0 || mhp > 9999) return false;
        // cmana (int32) at +16 must be in [0, mhp]
        if (!ReadInt32(mem, miscBase + (nuint)PlayerFormat.MiscCmanaOff, out int cm) || cm < 0 || cm > mhp) return false;
        // lev (int16) at +30 must be in [1, 40]
        if (!ReadInt16(mem, miscBase + (nuint)PlayerFormat.MiscLevOff, out short lev) || lev < 1 || lev > PlayerFormat.MaxLevel) return false;
        // au (int32) at +44 must equal gold hint
        if (!ReadInt32(mem, miscBase + (nuint)PlayerFormat.MiscAuOff, out int au) || au != gold) return false;
        // food (int16) at +84 must be in [0, 20000]
        if (!ReadInt16(mem, miscBase + (nuint)PlayerFormat.MiscFoodOff, out short food) || food < 0 || food > 20000) return false;
        return true;
    }

    private void PopulatePins(ProcessMemory mem, nuint miscBase)
    {
        Pins.Clear();

        void AddPin(string label, nuint address, ScanWidth w)
        {
            ReadAny(mem, address, w, out long v);
            Pins.Add(new CharPin(this, label, address, w, v));
        }

        // --- core stats (confirmed offsets) ---------------------------------
        AddPin("Max HP",    miscBase + (nuint)PlayerFormat.MiscMaxHpOff,  ScanWidth.Int32);
        AddPin("Cur HP",    miscBase + (nuint)PlayerFormat.MiscChpOff,    ScanWidth.Int32);
        AddPin("Max Mana",  miscBase + (nuint)PlayerFormat.MiscMhpOff,    ScanWidth.Int32);
        AddPin("Cur Mana",  miscBase + (nuint)PlayerFormat.MiscCmanaOff,  ScanWidth.Int32);
        AddPin("Level",     miscBase + (nuint)PlayerFormat.MiscLevOff,    ScanWidth.Int16);
        AddPin("Exp",       miscBase + (nuint)PlayerFormat.MiscExpOff,    ScanWidth.Int32);
        AddPin("Max Exp",   miscBase + (nuint)PlayerFormat.MiscMaxExpOff, ScanWidth.Int32);
        AddPin("Gold",      miscBase + (nuint)PlayerFormat.MiscAuOff,     ScanWidth.Int32);
        AddPin("Food",      miscBase + (nuint)PlayerFormat.MiscFoodOff,   ScanWidth.Int16);

        // --- stats (confirmed offsets from StatSubOff) ----------------------
        AddPin("STR",       miscBase + (nuint)PlayerFormat.StatStrOff,    ScanWidth.Int16);
        AddPin("INT",       miscBase + (nuint)PlayerFormat.StatIntOff,    ScanWidth.Int16);
        AddPin("WIS",       miscBase + (nuint)PlayerFormat.StatWisOff,    ScanWidth.Int16);
        AddPin("DEX",       miscBase + (nuint)PlayerFormat.StatDexOff,    ScanWidth.Int16);
        AddPin("CON",       miscBase + (nuint)PlayerFormat.StatConOff,    ScanWidth.Int16);
        AddPin("CHR",       miscBase + (nuint)PlayerFormat.StatChrOff,    ScanWidth.Int16);
    }

    private void Reset()
    {
        Pins.Clear();
        IsLocated = false;
        _freezeHp = false; _freezeMana = false; _freezeFood = false;
        OnPropertyChanged(nameof(FreezeHp)); OnPropertyChanged(nameof(FreezeMana)); OnPropertyChanged(nameof(FreezeFood));
        Status = "Reset. Enter updated values and Auto-Detect again.";
    }

    // --- poll loop ----------------------------------------------------------
    private void PollTick()
    {
        if (_mem is not { IsOpen: true }) return;
        if (!IsLocated) return;
        foreach (var pin in Pins)
        {
            pin.ApplyFreeze();
            if (ReadAny(_mem, pin.Address, pin.Width, out long live)) pin.Live = live;
        }
    }

    // --- IScanHost ----------------------------------------------------------
    bool IScanHost.Write(nuint address, long value, ScanWidth width) => ScanIo.WriteAt(_mem, address, value, width);
    bool IScanHost.Read(nuint address, ScanWidth width, out long value) => ScanIo.ReadAt(_mem, address, width, out value);
    void IScanHost.ReportWriteFailure(nuint address) => Status = $"Write failed at 0x{(ulong)address:X}.";

    // --- helpers ------------------------------------------------------------
    private static bool ReadInt32(ProcessMemory mem, nuint address, out int value)
    {
        var buf = mem.Read(address, 4);
        if (buf.Length < 4) { value = 0; return false; }
        value = buf[0] | (buf[1] << 8) | (buf[2] << 16) | (buf[3] << 24);
        return true;
    }

    private static bool ReadInt16(ProcessMemory mem, nuint address, out short value)
    {
        var buf = mem.Read(address, 2);
        if (buf.Length < 2) { value = 0; return false; }
        value = (short)(buf[0] | (buf[1] << 8));
        return true;
    }

    private static bool ReadAny(ProcessMemory? mem, nuint address, ScanWidth width, out long value) =>
        ScanIo.ReadAt(mem, address, width, out value);

    private void ApplyNamedFreeze(string label, bool frozen)
    {
        foreach (var p in Pins)
            if (p.Label == label) p.Frozen = frozen;
    }

    private void RaiseCommands()
    {
        (AutoDetectCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ResetCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    public void Dispose()
    {
        _poll.Stop();
        _cts?.Cancel();
        _cts?.Dispose();
    }
}

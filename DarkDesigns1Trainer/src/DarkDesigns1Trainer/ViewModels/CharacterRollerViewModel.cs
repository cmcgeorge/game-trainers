using System.Collections.ObjectModel;
using DarkDesigns1Trainer.Game;
using DarkDesigns1Trainer.Memory;

namespace DarkDesigns1Trainer.ViewModels;

/// <summary>
/// One row in the roller. Depending on which list it belongs to it carries a captured/live value
/// (the five rolled slots), a target minimum and the value an arrangement would give it (the five
/// attributes), or a running average (the five ranks) — the unused parts simply stay blank.
/// </summary>
public sealed class RollRowViewModel : ObservableObject
{
    private readonly int _minCap;

    public RollRowViewModel(string name, int minCap, string description = "")
    {
        Name = name;
        _minCap = minCap;
        Description = description;
    }

    public string Name { get; }

    /// <summary>Tooltip text explaining what this row is; empty when there's no description.</summary>
    public string Description { get; }

    private int? _captured;
    /// <summary>An on-screen rolled value the player types so the pool can be located; null until
    /// entered. The UI edits it through <see cref="CapturedText"/>.</summary>
    public int? Captured
    {
        get => _captured;
        set { if (SetField(ref _captured, value)) OnPropertyChanged(nameof(CapturedText)); }
    }

    /// <summary>The captured value as shown in / edited from the text box. A string binding (not a
    /// nullable int) so clearing the box reliably clears the value to null — see
    /// <see cref="MinimumText"/> for the nullable-int TextBox pitfall this avoids. Not clamped: the
    /// value must match the on-screen number exactly for the signature scan. Blank or non-numeric
    /// text reads as "not entered".</summary>
    public string CapturedText
    {
        get => _captured?.ToString() ?? "";
        set => Captured = int.TryParse(value, out int n) ? n : null;
    }

    private int? _minimum;
    /// <summary>The target floor for this attribute; null or 0 means "no requirement". This typed
    /// value is what the roller reads; the UI edits it through <see cref="MinimumText"/>.</summary>
    public int? Minimum
    {
        get => _minimum;
        set
        {
            int? clamped = value is null ? null : Math.Clamp(value.Value, 0, _minCap);
            if (SetField(ref _minimum, clamped)) OnPropertyChanged(nameof(MinimumText));
        }
    }

    /// <summary>The minimum as shown in / edited from the text box. Binding to a string (rather than
    /// a nullable int) makes an emptied box reliably clear the target to null — a nullable-int
    /// TextBox binding leaves the old value in place when the text is deleted, so the target would
    /// linger. Blank or non-numeric text reads as "no requirement".</summary>
    public string MinimumText
    {
        get => _minimum?.ToString() ?? "";
        set => Minimum = int.TryParse(value, out int n) ? n : null;
    }

    private int _live;
    private bool _hasLive;

    /// <summary>The value last read from the located pool (the current roll).</summary>
    public int Live
    {
        get => _live;
        set { _hasLive = true; SetField(ref _live, value); OnPropertyChanged(nameof(LiveText)); }
    }

    public string LiveText => _hasLive ? _live.ToString() : "—";

    /// <summary>True once a value has been read back into <see cref="Live"/>.</summary>
    public bool HasLive => _hasLive;

    public void ClearLive() { _hasLive = false; _live = 0; OnPropertyChanged(nameof(LiveText)); }

    private string _assignedText = "—";
    /// <summary>For an attribute row: which rolled value the suggested arrangement would give it,
    /// e.g. "18  (from #4)". "—" until a roll has been read.</summary>
    public string AssignedText { get => _assignedText; set => SetField(ref _assignedText, value); }

    private string _avgText = "—";
    /// <summary>For a rank row: this rank's running average and observed range over the session's
    /// rolls, e.g. "16.1  (13–18)"; "—" until any roll has been sampled.</summary>
    public string AvgText { get => _avgText; private set => SetField(ref _avgText, value); }

    /// <summary>Updates the average/range readout from a stats snapshot (UI thread).</summary>
    public void SetAverage(double avg, int min, int max) => AvgText = $"{avg:0.0}  ({min}–{max})";

    /// <summary>Clears the average/range readout when the history is reset.</summary>
    public void ClearStats() => AvgText = "—";
}

/// <summary>
/// "Create a character": automates the town (C)reate screen's re-roll. The roll lives in a
/// temporary five-value pool, not a roster slot, so the normal party scan can't see it — the player
/// first types the numbers showing on the create screen and the trainer signature-scans for them
/// (<see cref="CreationScanner"/>) to lock the address. From then on it taps <c>R</c>
/// (<see cref="KeyboardSender"/>), reads each fresh roll straight from memory, and stops once the
/// five values can be arranged to meet every attribute minimum.
///
/// Because the player arranges the pool freely, a target is a question about the whole set rather
/// than any one slot: the roller stops when <em>some</em> arrangement satisfies the minimums, and
/// then spells that arrangement out (<see cref="CreationFormat.Arrange"/>) so it can be typed in.
///
/// It can also write the pool outright (<see cref="SetRollCommand"/>) for a roll no amount of
/// re-rolling would produce — the game reads the written values back and the created character
/// keeps them, confirmed against the running game.
/// </summary>
public sealed class CharacterRollerViewModel : ObservableObject
{
    private readonly Func<ProcessMemory?> _getMem;
    private readonly Func<int?> _getPid;
    private readonly Action<string> _setStatus;
    private readonly SynchronizationContext _ui;

    /// <summary>The key the create screen uses to throw the roll away and roll again.</summary>
    private const string ReRollKey = "r";

    /// <summary>Re-rolls used while disambiguating multiple signature matches.</summary>
    private const int MaxNarrowRolls = 8;

    /// <summary>
    /// How many consecutive unchanged reads mean the game has stopped re-rolling (the create screen
    /// was closed) rather than that one read came back stale.
    ///
    /// Kept deliberately small. Each unchanged read costs another <c>R</c> tap, and if the create
    /// screen really has closed those taps land on whatever screen replaced it — in the town menu
    /// <c>R</c> is "Remove a character". A genuine repeat of all five values happens about once in
    /// twenty thousand rolls, so needing three in a row before giving up is already far beyond
    /// coincidence while keeping the stray keystrokes down to two.
    /// </summary>
    private const int MaxStaleRolls = 3;

    private CancellationTokenSource? _cts;
    private nuint _lockAddr;

    // The target the running roll loop froze at Start(); null when no run is in flight. Only ever
    // read and written on the UI thread (Start() sets it before the loop starts, the loop's own
    // continuation clears it, and RefreshArrangement runs via OnUi), so it needs no synchronisation.
    private int[]? _activeMins;
    private int _activeTotalMin;

    public CharacterRollerViewModel(Func<ProcessMemory?> getMem, Func<int?> getPid, Action<string> setStatus)
    {
        _getMem = getMem;
        _getPid = getPid;
        _setStatus = setStatus;
        _ui = SynchronizationContext.Current ?? new SynchronizationContext();

        foreach (var name in CreationFormat.SlotNames)
        {
            var slot = new RollRowViewModel(name, CreationFormat.MaxPlausible,
                "One of the five values the create screen rolled. Type them in any order — the "
                + "trainer matches the set, not the sequence.");
            slot.PropertyChanged += OnSlotChanged;
            Slots.Add(slot);
        }

        for (int i = 0; i < CharacterFormat.AttributeNames.Length; i++)
        {
            var row = new RollRowViewModel(CharacterFormat.AttributeNames[i], CreationFormat.MaxTargetValue,
                AttributeBook.DescriptionOf(i));
            row.PropertyChanged += OnTargetChanged;
            Targets.Add(row);
        }

        foreach (var name in CreationFormat.RankNames)
            Ranks.Add(new RollRowViewModel(name, CreationFormat.MaxRoll,
                "The rolled values of each roll, ranked highest to lowest."));

        _tally = new RollTally();

        LockCommand = new RelayCommand(_ => Lock(), _ => Attached && !IsBusy && !IsRolling);
        ReadOnceCommand = new RelayCommand(_ => ReadOnce(), _ => IsLocked && Attached && !IsBusy && !IsRolling);
        ResetLockCommand = new RelayCommand(_ => ResetLock(), _ => IsLocked && !IsBusy && !IsRolling);
        StartCommand = new RelayCommand(_ => Start(), _ => IsLocked && Attached && _getPid() != null && !IsBusy && !IsRolling);
        StopCommand = new RelayCommand(_ => Stop(), _ => IsRolling);
        ClearStatsCommand = new RelayCommand(_ => ClearStats(), _ => _tally.Count > 0 && !IsRolling);
        ClearMinimumsCommand = new RelayCommand(_ => ClearMinimums(), _ => HasAnyMinimum() && !IsRolling);
        SetRollCommand = new RelayCommand(_ => SetRoll(), _ => IsLocked && Attached && !IsBusy && !IsRolling);
    }

    // The on-screen total is derived from the five capture boxes, so it has to follow them.
    private void OnSlotChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RollRowViewModel.Captured))
            OnPropertyChanged(nameof(CapturedTotalText));
    }

    private void OnTargetChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(RollRowViewModel.Minimum)) return;
        OnPropertyChanged(nameof(CriteriaText));
        OnPropertyChanged(nameof(OddsText));
        ClearMinimumsCommand?.RaiseCanExecuteChanged();   // null only during ctor, before this fires
        RefreshArrangement();
    }

    /// <summary>
    /// Session tally of every fresh roll, for the statistics panel. Mutated by whichever thread is
    /// currently reading rolls — the roll loop, or the UI thread for one-shot reads, which never
    /// overlap because the read commands are disabled while rolling. <c>volatile</c> so a reset's
    /// reassignment on the UI thread is promptly visible to the background roll loop.
    ///
    /// One exception, worth being honest about: <see cref="Reset"/> (from detach/dispose) only
    /// <em>requests</em> cancellation and then clears the tally, so a roll loop still unwinding can
    /// add a sample to the instance being replaced or snapshot one mid-update. That costs nothing
    /// but statistics which are being thrown away in the same breath, so it isn't worth blocking the
    /// UI thread on the loop to prevent.
    /// </summary>
    private volatile RollTally _tally;

    // --- the three row lists ----------------------------------------------------
    /// <summary>The five rolled values, in the order the create screen shows them.</summary>
    public ObservableCollection<RollRowViewModel> Slots { get; } = new();

    /// <summary>The five attributes, each with an optional target minimum.</summary>
    public ObservableCollection<RollRowViewModel> Targets { get; } = new();

    /// <summary>The five rolled values ranked best-to-worst, for the statistics panel.</summary>
    public ObservableCollection<RollRowViewModel> Ranks { get; } = new();

    // --- totals -----------------------------------------------------------------
    /// <summary>Sum of the five live rolled values; "—" until the pool is locked.</summary>
    public string LiveTotalText => Slots.All(s => s.HasLive) ? Slots.Sum(s => s.Live).ToString() : "—";

    /// <summary>Sum of the five captured (on-screen) values; "—" until all five are entered.</summary>
    public string CapturedTotalText =>
        Slots.All(s => s.Captured.HasValue) ? Slots.Sum(s => s.Captured!.Value).ToString() : "—";

    // --- target criteria --------------------------------------------------------
    private int? _totalMinimum;
    /// <summary>Optional target floor for the sum of the five rolled values. null or 0 means "no
    /// requirement". Edited from the UI through <see cref="TotalMinimumText"/>.</summary>
    public int? TotalMinimum
    {
        get => _totalMinimum;
        set
        {
            int? clamped = value is null ? null : Math.Clamp(value.Value, 0, CreationFormat.MaxTargetTotal);
            if (SetField(ref _totalMinimum, clamped))
            {
                OnPropertyChanged(nameof(TotalMinimumText));
                OnPropertyChanged(nameof(CriteriaText));
                OnPropertyChanged(nameof(OddsText));
                ClearMinimumsCommand?.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>The total minimum as shown in / edited from its text box; a string binding so
    /// clearing the box reliably drops the target to null (see <see cref="RollRowViewModel.MinimumText"/>).</summary>
    public string TotalMinimumText
    {
        get => _totalMinimum?.ToString() ?? "";
        set => TotalMinimum = int.TryParse(value, out int n) ? n : null;
    }

    public string CriteriaText
    {
        get
        {
            var active = Targets.Where(t => t.Minimum > 0)          // null and 0 both mean "no requirement"
                                .Select(t => $"{t.Name} ≥ {t.Minimum!.Value}")
                                .ToList();
            if (TotalMinimum > 0) active.Add($"total ≥ {TotalMinimum.Value}");
            return active.Count == 0
                ? "No minimums set — the first roll will be accepted. Set a minimum on the attributes you care about."
                : "Stop when the five values can be arranged so " + string.Join(", ", active) + ".";
        }
    }

    /// <summary>Exact odds of hitting the current target on any one roll, from the measured
    /// <c>10 + random(5) + random(5)</c> distribution. Recomputed whenever a minimum changes.</summary>
    public string OddsText
    {
        get
        {
            var mins = Targets.Select(t => t.Minimum ?? 0).ToArray();
            int totalMin = TotalMinimum ?? 0;

            var overMax = Targets.Where(t => (t.Minimum ?? 0) > RollOdds.Max).Select(t => t.Name).ToArray();
            if (overMax.Length > 0)
                return $"Out of reach: {string.Join(", ", overMax)} would need more than {RollOdds.Max}, "
                     + "the highest the game rolls. (You can still write the roll directly below.)";
            if (totalMin > CreationFormat.MaxTotal)
                return $"Out of reach: the five values can't total more than {CreationFormat.MaxTotal} "
                     + $"(all {RollOdds.Max}s). (You can still write the roll directly below.)";

            // A minimum at or below the game's floor constrains nothing, so say why rather than
            // quoting "1 in 1" at someone who thinks they've set a target.
            bool anyStat = mins.Any(m => m > RollOdds.Min);
            bool anyTotal = totalMin > CreationFormat.MinTotal;
            if (!anyStat && !anyTotal)
                return mins.Any(m => m > 0) || totalMin > 0
                    ? $"Every roll qualifies (1 in 1): the game never rolls below {RollOdds.Min}, or "
                      + $"totals below {CreationFormat.MinTotal}, so those minimums ask for nothing."
                    : "Every roll qualifies (1 in 1) — no minimums are set.";

            double p = RollOdds.PMeetsTarget(mins, totalMin);
            if (p <= 0) return "Out of reach for the game's dice. (You can still write the roll directly below.)";

            double expected = 1.0 / p;
            double rolls95 = Math.Max(1, Math.Ceiling(Math.Log(0.05) / Math.Log(1 - p)));   // ~95% chance of >=1 hit
            string time = Humanize(expected * PerRollSeconds);
            return $"Odds: about 1 in {expected:N0} rolls (p = {Percent(p)}). At ~{PerRollSeconds:0.##}s per roll "
                 + $"that's roughly {time}; allow about {rolls95:N0} rolls for a 95% chance.";
        }
    }

    // --- suggested arrangement --------------------------------------------------
    private string _arrangementText = "Lock onto the roll to see how to arrange it.";
    /// <summary>How to lay the current roll onto the five attributes so every minimum is met — the
    /// payoff of a successful roll, since the player has to type the arrangement in themselves.</summary>
    public string ArrangementText { get => _arrangementText; private set => SetField(ref _arrangementText, value); }

    // --- session statistics (fed from the roll loop) ----------------------------
    private string _samplesText = "No rolls sampled yet.";
    public string SamplesText { get => _samplesText; private set => SetField(ref _samplesText, value); }

    private string _totalAvgText = "—";
    /// <summary>Running average and observed range of the five-value total across the session's
    /// rolls; "—" until any roll has been sampled. A fair roll averages 70.</summary>
    public string TotalAvgText { get => _totalAvgText; private set => SetField(ref _totalAvgText, value); }

    // --- writing the roll directly ----------------------------------------------
    private string _setValuesText = "18 18 18 18 18";
    /// <summary>The values <see cref="SetRollCommand"/> writes over the pool. Five numbers, or one
    /// number to use for all five.</summary>
    public string SetValuesText { get => _setValuesText; set => SetField(ref _setValuesText, value); }

    // --- tuning -----------------------------------------------------------------
    private int _maxAttempts = 1000;
    public int MaxAttempts { get => _maxAttempts; set => SetField(ref _maxAttempts, Math.Clamp(value, 1, 1_000_000)); }

    // Settle is the pause between the R tap and the next read: it must outlast the game writing the
    // fresh roll to memory, or a stale read wastes the roll. 120ms held up over a 400-roll run.
    private int _settleDelayMs = 120;
    public int SettleDelayMs
    {
        get => _settleDelayMs;
        set { if (SetField(ref _settleDelayMs, Math.Clamp(value, 0, 2000))) OnPropertyChanged(nameof(OddsText)); }
    }

    private int _focusDelayMs = 50;
    public int FocusDelayMs { get => _focusDelayMs; set => SetField(ref _focusDelayMs, Math.Clamp(value, 0, 2000)); }

    // Rough wall-clock cost of one roll: the post-tap settle plus a little tap/loop overhead. Used
    // only to turn "expected rolls" into a rough time in the odds readout.
    private double PerRollSeconds => (_settleDelayMs + 30) / 1000.0;

    // --- state ------------------------------------------------------------------
    private bool _isBusy;
    public bool IsBusy { get => _isBusy; private set { if (SetField(ref _isBusy, value)) { OnPropertyChanged(nameof(CanEditTargets)); RaiseAll(); } } }

    private bool _isRolling;
    public bool IsRolling { get => _isRolling; private set { if (SetField(ref _isRolling, value)) { OnPropertyChanged(nameof(CanEditTargets)); RaiseAll(); } } }

    /// <summary>False while a roll or scan is in flight, so the target boxes are greyed out rather
    /// than accepting edits the running loop has already frozen past.</summary>
    public bool CanEditTargets => !_isRolling && !_isBusy;

    private bool _isLocked;
    public bool IsLocked { get => _isLocked; private set { if (SetField(ref _isLocked, value)) { OnPropertyChanged(nameof(LockInfo)); RaiseAll(); } } }

    public string LockInfo => _isLocked
        ? $"Locked onto the roll at 0x{(ulong)_lockAddr:X}."
        : "Not locked. Type the five numbers showing on the create screen, then Lock onto roll.";

    private int _attempts;
    public int Attempts { get => _attempts; private set { if (SetField(ref _attempts, value)) OnPropertyChanged(nameof(AttemptsText)); } }
    public string AttemptsText => _attempts == 0 ? "" : $"Rolls tried: {_attempts}";

    private string _bestText = "";
    public string BestText { get => _bestText; private set => SetField(ref _bestText, value); }

    private string _resultText = "";
    public string ResultText { get => _resultText; private set => SetField(ref _resultText, value); }

    private bool Attached => _getMem() != null;

    // --- commands ---------------------------------------------------------------
    public RelayCommand LockCommand { get; }
    public RelayCommand ReadOnceCommand { get; }
    public RelayCommand ResetLockCommand { get; }
    public RelayCommand StartCommand { get; }
    public RelayCommand StopCommand { get; }
    public RelayCommand ClearStatsCommand { get; }
    public RelayCommand ClearMinimumsCommand { get; }
    public RelayCommand SetRollCommand { get; }

    /// <summary>Re-evaluates command availability — call when attach state changes.</summary>
    public void RefreshCommands() => RaiseAll();

    /// <summary>Stops any roll and forgets the locked address (called on detach: the address belongs
    /// to a process we're no longer attached to).</summary>
    public void Reset()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        _activeMins = null;  // the cancelled loop's finally won't clear it: _cts no longer matches
        IsRolling = false;   // via the setters so bindings + command state refresh
        IsBusy = false;
        ResetLock();
    }

    // --- locating the roll pool -------------------------------------------------
    private async void Lock()
    {
        var mem = _getMem();
        if (mem == null) { _setStatus("Attach to the game first."); return; }

        var captured = Slots.Select(s => s.Captured ?? 0).ToArray();   // blank reads as 0, which fails InRange below
        if (!CreationScanner.InRange(captured))
        {
            _setStatus($"Type the five numbers from the create screen first (each between "
                     + $"{CreationFormat.MinPlausible} and {CreationFormat.MaxPlausible}).");
            return;
        }

        var cts = ResetCts();
        var ct = cts.Token;
        int? pid = _getPid();
        int settle = _settleDelayMs, focus = _focusDelayMs;   // snapshot UI-owned knobs for the bg thread
        IsBusy = true;
        _setStatus("Searching memory for the roll on the create screen… (if several spots match, the "
                 + "trainer re-rolls a few times to pin down the right one — so the on-screen roll may change).");

        try
        {
            var locked = await Task.Run(() =>
            {
                var matches = CreationScanner.Find(mem, captured, ct);
                if (matches.Count == 0) return (nuint?)null;
                if (matches.Count == 1 || pid == null) return matches[0];
                return Narrow(mem, pid.Value, matches, settle, focus, ct);
            }, ct);

            // A detach (+ reattach) may have replaced the handle while we scanned; don't publish
            // results against a stale/disposed ProcessMemory (mirrors MainViewModel.Scan).
            if (ct.IsCancellationRequested || !ReferenceEquals(mem, _getMem())) return;

            if (locked == null)
            {
                _setStatus("Couldn't find those numbers in the game's memory. Make sure you're on the "
                         + "town (C)reate screen and the five values match what's on it, then try again.");
                return;
            }

            // Only claim the lock once the address actually reads back as a roll. Declaring success
            // on an address we can't read would leave the Live column blank and, worse, point the
            // write-the-roll-directly button at memory we know nothing about.
            var probe = new int[CreationFormat.RolledCount];
            if (!CreationScanner.TryReadRoll(mem, locked.Value, probe) || !CreationScanner.InRange(probe))
            {
                _setStatus("Found those numbers, but the address stopped reading back as a roll. "
                         + "Make sure the create screen is still open, then try Lock again.");
                return;
            }

            _lockAddr = locked.Value;
            IsLocked = true;
            ClearStats();   // a fresh lock starts a fresh tally
            ReadInto(mem, _lockAddr);
            _setStatus(LockInfo + " Set your target below, then Roll.");
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { _setStatus("Lock failed: " + ex.Message); }
        finally { if (ReferenceEquals(_cts, cts)) IsBusy = false; }
    }

    // Disambiguate multiple signature hits: re-roll a few times and keep candidates whose five values
    // stay in a plausible range AND actually change (the live pool does; a coincidental static match
    // doesn't). Runs on the scan thread.
    private nuint? Narrow(ProcessMemory mem, int pid, List<nuint> matches,
                          int settleMs, int focusMs, CancellationToken ct)
    {
        var cands = new List<Cand>();
        foreach (var addr in matches)
        {
            var v = new int[CreationFormat.RolledCount];
            if (CreationScanner.TryReadRoll(mem, addr, v) && CreationScanner.InRange(v))
                cands.Add(new Cand(addr, v));
        }
        // Every hit turned out to be unreadable or to hold something that isn't a roll any more.
        // Report no match rather than handing back an address we just disproved — Lock would
        // otherwise declare success on it, and the write path would happily poke ten bytes into it.
        if (cands.Count == 0) return null;

        for (int r = 0; r < MaxNarrowRolls && cands.Count > 1; r++)
        {
            ct.ThrowIfCancellationRequested();
            if (!KeyboardSender.Send(pid, ReRollKey, settleMs, focusMs, out _)) break;

            var keep = new List<Cand>();
            foreach (var c in cands)
            {
                var v = new int[CreationFormat.RolledCount];
                if (!CreationScanner.TryReadRoll(mem, c.Address, v)) continue;
                if (!CreationScanner.InRange(v)) continue;
                if (!Same(v, c.Last)) c.ChangedEver = true;
                c.Last = v;
                keep.Add(c);
            }
            cands = keep;
        }

        // Prefer a candidate that proved it's live (changed during the re-rolls).
        var live = cands.Where(c => c.ChangedEver).ToList();
        var pool = live.Count > 0 ? live : cands;
        return pool.Count > 0 ? pool[0].Address : null;
    }

    private sealed class Cand
    {
        public Cand(nuint address, int[] last) { Address = address; Last = last; }
        public nuint Address { get; }
        public int[] Last { get; set; }
        public bool ChangedEver { get; set; }
    }

    private void ReadOnce()
    {
        var mem = _getMem();
        if (mem == null || !_isLocked) return;
        if (ReadInto(mem, _lockAddr))
            _setStatus("Read the current roll from memory.");
        else
            _setStatus("Couldn't read the locked address — Reset lock and capture again (did the screen change?).");
    }

    private void ResetLock()
    {
        _isLocked = false;
        _lockAddr = 0;
        Attempts = 0;
        BestText = "";
        ResultText = "";
        foreach (var s in Slots) s.ClearLive();
        foreach (var t in Targets) t.AssignedText = "—";
        ArrangementText = "Lock onto the roll to see how to arrange it.";
        OnPropertyChanged(nameof(LiveTotalText));
        ClearStats();   // the tally belonged to the pool we're releasing
        OnPropertyChanged(nameof(IsLocked));
        OnPropertyChanged(nameof(LockInfo));
        OnPropertyChanged(nameof(CriteriaText));
        RaiseAll();
    }

    private bool HasAnyMinimum() => Targets.Any(t => t.Minimum is > 0) || TotalMinimum is > 0;

    /// <summary>Clears every target minimum (per-attribute and total) back to blank ("no requirement").</summary>
    private void ClearMinimums()
    {
        foreach (var t in Targets) t.Minimum = null;   // setters fire PropertyChanged → Criteria/command refresh
        TotalMinimum = null;
    }

    // --- writing the roll directly ----------------------------------------------
    /// <summary>
    /// Writes <see cref="SetValuesText"/> straight over the pool, for a roll the dice would never
    /// produce. The game reads the pool as it hands each value out, so the created character keeps
    /// what's written here — but the row of numbers already painted on the create screen is not
    /// repainted, so it keeps showing the old roll until the next R.
    /// </summary>
    private void SetRoll()
    {
        var mem = _getMem();
        if (mem == null || !_isLocked) return;

        if (!CreationFormat.TryParseValues(_setValuesText, out var values, out string error))
        {
            _setStatus(error);
            return;
        }

        if (!CreationScanner.WriteRoll(mem, _lockAddr, values))
        {
            _setStatus("Couldn't write the roll — Reset lock and capture again.");
            return;
        }

        // Don't tally the read-back: the statistics panel reports what the game's dice do, and a
        // roll we wrote ourselves is not evidence about that.
        string written = $"Roll set to {string.Join(", ", values)}. The create screen still shows the "
                       + "old numbers — it isn't repainted — but these are the values it will hand "
                       + "out as you arrange the character.";
        _setStatus(ReadInto(mem, _lockAddr, addToTally: false)
            ? written + " " + ArrangementText
            : written + " (Couldn't read the pool back to check it — Reset lock and capture again.)");
    }

    // --- statistics -------------------------------------------------------------
    /// <summary>Empties the session tally and the statistics readouts.</summary>
    private void ClearStats()
    {
        _tally = new RollTally();
        foreach (var r in Ranks) r.ClearStats();
        SamplesText = "No rolls sampled yet.";
        TotalAvgText = "—";
        RaiseAll();
    }

    /// <summary>Pushes a stats snapshot into the readouts (UI thread).</summary>
    private void ApplyStatsSnapshot(RollTallySnapshot s)
    {
        if (!_isLocked) return;   // a detach/reset may have run before this queued update fired
        for (int k = 0; k < Ranks.Count && k < s.RankMean.Length; k++)
            Ranks[k].SetAverage(s.RankMean[k], s.RankMin[k], s.RankMax[k]);

        SamplesText = $"Rolls sampled: {s.Count:N0}";
        TotalAvgText = s.Count == 0 ? "—" : $"{s.TotalMean:0.0}  ({s.TotalMin}–{s.TotalMax})";
        ClearStatsCommand.RaiseCanExecuteChanged();
    }

    // --- the roll loop ----------------------------------------------------------
    private async void Start()
    {
        var mem = _getMem();
        int? pid = _getPid();
        if (mem == null || pid == null || !_isLocked) return;

        var cts = ResetCts();
        var ct = cts.Token;
        int count = CreationFormat.RolledCount;

        // Snapshot everything the background loop needs so it never reads UI-owned state (the lock
        // address, the target, the delays) across threads.
        nuint lockAddr = _lockAddr;
        int[] mins = Targets.Select(t => t.Minimum ?? 0).ToArray();
        int totalMinTarget = TotalMinimum ?? 0;
        int maxAttempts = _maxAttempts;
        int settle = _settleDelayMs, focus = _focusDelayMs;

        // The arrangement panel must describe the target the loop is actually stopping on, not
        // whatever is in the boxes right now — otherwise editing a minimum mid-run would leave the
        // green "arrange it as …" line contradicting the "✔ Found it" line directly below it.
        _activeMins = mins;
        _activeTotalMin = totalMinTarget;

        OnUi(() => { Attempts = 0; BestText = ""; ResultText = ""; });
        IsRolling = true;
        _setStatus("Rolling… (the game window comes forward for each re-roll; click Stop here to halt).");

        int tried = 0;
        bool met = false;
        string failure = "";
        int[]? winning = null;

        try
        {
            await Task.Run(() =>
            {
                var v = new int[count];
                int[]? best = null;     // best-so-far is owned solely by this loop thread
                int[]? previous = null;
                int bestAttempt = 0, staleReads = 0;

                while (!ct.IsCancellationRequested && tried < maxAttempts)
                {
                    // A short read or out-of-range values mean the pool no longer holds a roll (the
                    // create screen closed, or the game reused the memory). Bail rather than treat
                    // unrelated bytes as a winning roll.
                    if (!CreationScanner.TryReadRoll(mem, lockAddr, v) || !CreationScanner.InRange(v))
                    { failure = "lost the locked roll (did the create screen close?)"; break; }

                    // Identical to the last roll almost always means we read before the game had
                    // written the fresh one. Give it another settle and look again rather than
                    // judging — and stop grading a roll the player can no longer see.
                    if (previous != null && Same(v, previous))
                    {
                        Thread.Sleep(settle);
                        if (!CreationScanner.TryReadRoll(mem, lockAddr, v) || !CreationScanner.InRange(v))
                        { failure = "lost the locked roll (did the create screen close?)"; break; }

                        if (Same(v, previous))
                        {
                            // Still unchanged: the game isn't re-rolling for us any more.
                            if (++staleReads >= MaxStaleRolls)
                            { failure = "the roll stopped changing — is the create screen still open and the game window responding?"; break; }
                            if (ct.IsCancellationRequested) break;
                            if (!KeyboardSender.Send(pid.Value, ReRollKey, settle, focus, out var staleErr))
                            { failure = staleErr; break; }
                            continue;
                        }
                    }
                    staleReads = 0;

                    tried++;
                    if (best == null || IsBetter(v, best, mins, totalMinTarget))
                    { best = (int[])v.Clone(); bestAttempt = tried; }

                    // Hand the UI immutable snapshots: the loop keeps overwriting `v`, and `best` is
                    // replaced (never edited) so its reference is safe to read later.
                    var rollSnap = (int[])v.Clone();
                    int[] bestSnap = best;
                    int bestAttemptSnap = bestAttempt, triedSnap = tried;
                    OnUi(() => PublishRoll(rollSnap, bestSnap, bestAttemptSnap, mins, totalMinTarget, triedSnap));

                    // Tally the roll for the statistics panel; a duplicate is dropped, so only post a
                    // fresh snapshot when it actually changed the numbers.
                    if (_tally.Add(v))
                    {
                        var statsSnap = _tally.Snapshot();
                        OnUi(() => ApplyStatsSnapshot(statsSnap));
                    }

                    if (CreationFormat.MeetsTarget(v, mins, totalMinTarget))
                    { winning = rollSnap; met = true; break; }

                    previous = rollSnap;
                    if (ct.IsCancellationRequested) break;
                    if (!KeyboardSender.Send(pid.Value, ReRollKey, settle, focus, out var err))
                    { failure = err; break; }
                }
            }, ct);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { failure = ex.Message; }
        finally
        {
            // Only release the frozen target if this run still owns it — a superseding Start() has
            // already installed its own, and clearing that would unfreeze the newer run.
            if (ReferenceEquals(_cts, cts)) { _activeMins = null; IsRolling = false; }
        }

        // If a detach/reset (or a fresh Start) superseded this run while it was unwinding, don't
        // publish its result — Reset() already set the status/lock state, and surfacing the game or
        // clobbering the status here would fight it. Mirrors the stale-scan guard in Lock().
        if (!ReferenceEquals(_cts, cts)) return;

        if (met && winning != null)
        {
            KeyboardSender.BringToFront(pid.Value);   // surface the game so the player can arrange the roll
            ResultText = $"✔ Found it after {tried} roll(s): {Describe(winning, mins, totalMinTarget)}. "
                       + "The game is in front — " + ArrangeSentence(winning, mins);
            _setStatus(ResultText);
        }
        else if (failure.Length > 0)
        {
            ResultText = $"Stopped after {tried} roll(s): {failure}";
            _setStatus(ResultText);
        }
        else if (ct.IsCancellationRequested)
        {
            ResultText = $"Stopped by you after {tried} roll(s). Best so far: {BestText}";
            _setStatus("Roller stopped.");
        }
        else
        {
            ResultText = $"Hit the {maxAttempts}-roll limit without matching. Best seen: {BestText}. "
                       + "Loosen the target, raise the roll limit, or write the roll directly.";
            _setStatus(ResultText);
        }
    }

    private void Stop()
    {
        _cts?.Cancel();
        _setStatus("Stopping the roller…");
    }

    // --- evaluation helpers -----------------------------------------------------
    // Ranks one roll above another: closer to meeting the target first (less shortfall), then the
    // higher total.
    private static bool IsBetter(int[] cand, int[] best, int[] mins, int totalMin)
    {
        int cs = CreationFormat.Shortfall(cand, mins, totalMin);
        int bs = CreationFormat.Shortfall(best, mins, totalMin);
        if (cs != bs) return cs < bs;
        return CreationFormat.Total(cand) > CreationFormat.Total(best);
    }

    // "14, 18, 12, 16, 11 · total 71 (short 3)" — the shared roll summary.
    private static string Describe(int[] v, int[] mins, int totalMin)
    {
        string parts = string.Join(", ", v);
        parts += $" · total {CreationFormat.Total(v)}";
        int sf = CreationFormat.Shortfall(v, mins, totalMin);
        return sf == 0 ? parts : $"{parts} (short {sf})";
    }

    // "arrange it as Strength ← #2 (18), Dexterity ← #1 (16), …" — the instruction the player follows.
    private static string ArrangeSentence(int[] roll, int[] mins)
    {
        var slots = CreationFormat.Arrange(roll, mins);
        if (slots == null) return "arrange the five values as you like.";
        var parts = slots.Select((slot, attr) =>
            $"{CharacterFormat.AttributeNames[attr]} ← #{slot + 1} ({roll[slot]})");
        return "arrange it as " + string.Join(", ", parts) + ".";
    }

    // Runs on the UI thread (via OnUi) with immutable snapshots taken in the roll loop: the live
    // readbacks, attempt count, and the best-so-far line.
    private void PublishRoll(int[] roll, int[] best, int bestAttempt, int[] mins, int totalMin, int attempt)
    {
        if (!_isLocked) return;   // a detach/reset may have run before this queued update fired
        for (int k = 0; k < Slots.Count && k < roll.Length; k++) Slots[k].Live = roll[k];
        OnPropertyChanged(nameof(LiveTotalText));
        Attempts = attempt;
        BestText = $"{Describe(best, mins, totalMin)} (roll #{bestAttempt})";
        RefreshArrangement();
    }

    /// <summary>Reads the pool at <paramref name="addr"/> into the Live column and refreshes the
    /// arrangement. <paramref name="addToTally"/> is false for the read-back after a write, whose
    /// values came from the trainer rather than the game's dice. Returns false when the address no
    /// longer holds a plausible roll.</summary>
    private bool ReadInto(ProcessMemory mem, nuint addr, bool addToTally = true)
    {
        var v = new int[CreationFormat.RolledCount];
        // Reject a short read OR bytes that aren't a plausible roll: the pool is ephemeral, so if the
        // screen has closed and the address now holds unrelated bytes the read still succeeds — the
        // in-range gate keeps that garbage out of the Live readout and the tally (mirrors Narrow()).
        if (!CreationScanner.TryReadRoll(mem, addr, v) || !CreationScanner.InRange(v)) return false;
        for (int k = 0; k < Slots.Count; k++) Slots[k].Live = v[k];
        OnPropertyChanged(nameof(LiveTotalText));
        RefreshArrangement();

        // A one-shot read (lock / Read current roll) counts as a sample too; a repeat of the same
        // static roll is dropped by the tally's dedup. Runs on the UI thread, and never while the
        // roll loop is active (the commands are disabled then), so there's no race.
        if (addToTally && _tally.Add(v)) ApplyStatsSnapshot(_tally.Snapshot());
        return true;
    }

    /// <summary>
    /// Recomputes the suggested arrangement from the live roll, filling in each attribute's
    /// "Arranged" cell and the summary line. While a run is in flight this uses the target the roll
    /// loop froze at <see cref="Start"/>, not whatever is in the boxes now, so the panel can never
    /// describe a different target from the one the roller is actually stopping on.
    /// </summary>
    private void RefreshArrangement()
    {
        if (!Slots.All(s => s.HasLive))
        {
            foreach (var t in Targets) t.AssignedText = "—";
            return;
        }

        var roll = Slots.Select(s => s.Live).ToArray();
        var mins = _activeMins ?? Targets.Select(t => t.Minimum ?? 0).ToArray();
        int totalMin = _activeMins != null ? _activeTotalMin : TotalMinimum ?? 0;
        var slots = CreationFormat.Arrange(roll, mins);

        if (slots == null)
        {
            foreach (var t in Targets) t.AssignedText = "—";
            int sf = CreationFormat.Shortfall(roll, mins, totalMin);
            ArrangementText = $"This roll can't meet the target — it's {sf} short in total. Roll again, "
                            + "loosen the minimums, or write the roll directly.";
            return;
        }

        for (int attr = 0; attr < Targets.Count && attr < slots.Length; attr++)
            Targets[attr].AssignedText = $"{roll[slots[attr]]}  (#{slots[attr] + 1})";

        int total = CreationFormat.Total(roll);
        ArrangementText = total < totalMin
            ? $"The values fit the attribute minimums, but they only total {total} (you asked for {totalMin})."
            : "On the create screen, " + ArrangeSentence(roll, mins);
    }

    private static bool Same(int[] a, int[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
        return true;
    }

    private CancellationTokenSource ResetCts()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        return _cts;
    }

    private void OnUi(Action action) => _ui.Post(_ => action(), null);

    // "under a second" / "1.2 seconds" / "3.4 minutes" / "2.1 hours" / "5.3 days" / "1.4 years".
    private static string Humanize(double seconds)
    {
        if (seconds < 1) return "under a second";
        if (seconds < 90) return $"{seconds:0.#} seconds";
        double minutes = seconds / 60;
        if (minutes < 90) return $"{minutes:0.#} minutes";
        double hours = minutes / 60;
        if (hours < 48) return $"{hours:0.#} hours";
        double days = hours / 24;
        if (days < 730) return $"{days:0.#} days";
        return $"{days / 365:0.#} years";
    }

    // Percentage down to 0.01%, then scientific for the genuinely tiny (e.g. all-18 targets, where
    // p bottoms out around 1e-7). Both branches are percentages and both carry the % sign — the
    // custom "%" format specifier scales by 100 itself, the scientific one has to be told to.
    private static string Percent(double p) =>
        p >= 0.0001 ? p.ToString("0.####%") : (p * 100).ToString("0.0E+0") + "%";

    private void RaiseAll()
    {
        LockCommand.RaiseCanExecuteChanged();
        ReadOnceCommand.RaiseCanExecuteChanged();
        ResetLockCommand.RaiseCanExecuteChanged();
        StartCommand.RaiseCanExecuteChanged();
        StopCommand.RaiseCanExecuteChanged();
        ClearStatsCommand.RaiseCanExecuteChanged();
        ClearMinimumsCommand.RaiseCanExecuteChanged();
        SetRollCommand.RaiseCanExecuteChanged();
    }
}

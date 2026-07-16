using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using WastelandTrainer.Game;
using WastelandTrainer.Memory;

namespace WastelandTrainer.ViewModels;

/// <summary>One stat row in the roller: the value the user reads off the create screen (to locate the
/// buffer), the target floor, the value last read back from memory, and a running average/range.</summary>
public sealed class RollStatViewModel : ObservableObject
{
    private readonly int _minCap;

    public RollStatViewModel(string name, int minCap, string description = "")
    {
        Name = name;
        _minCap = minCap;
        Description = description;
    }

    public string Name { get; }

    /// <summary>Tooltip text explaining what this stat does; empty when there's no description.</summary>
    public string Description { get; }

    private int? _captured;
    /// <summary>The on-screen value the user types so the buffer can be located; null until entered.
    /// The UI edits it through <see cref="CapturedText"/>.</summary>
    public int? Captured
    {
        get => _captured;
        set { if (SetField(ref _captured, value)) OnPropertyChanged(nameof(CapturedText)); }
    }

    /// <summary>The captured value as shown in / edited from the text box. A string binding (not a
    /// nullable int) so clearing the box reliably clears the value to null — see <see cref="MinimumText"/>
    /// for the nullable-int TextBox pitfall this avoids. Not clamped: the value must match the on-screen
    /// number exactly for the signature scan. Blank or non-numeric text reads as "not entered".</summary>
    public string CapturedText
    {
        get => _captured?.ToString() ?? "";
        set => Captured = int.TryParse(value, out int n) ? n : null;
    }

    private int? _minimum;
    /// <summary>The target floor for this stat; null or 0 means "no requirement". This typed value is
    /// what the roller reads; the UI edits it through <see cref="MinimumText"/>.</summary>
    public int? Minimum
    {
        get => _minimum;
        set
        {
            int? clamped = value is null ? null : Math.Clamp(value.Value, 0, _minCap);
            if (SetField(ref _minimum, clamped)) OnPropertyChanged(nameof(MinimumText));
        }
    }

    /// <summary>The minimum as shown in / edited from the text box. Binding to a string (rather than a
    /// nullable int) makes an emptied box reliably clear the target to null — a nullable-int TextBox
    /// binding leaves the old value in place when the text is deleted, so the target would linger.
    /// Blank or non-numeric text reads as "no requirement".</summary>
    public string MinimumText
    {
        get => _minimum?.ToString() ?? "";
        set => Minimum = int.TryParse(value, out int n) ? n : null;
    }

    private int _live;
    private bool _hasLive;

    /// <summary>The value last read from the located buffer (the current/best roll).</summary>
    public int Live
    {
        get => _live;
        set { _hasLive = true; SetField(ref _live, value); OnPropertyChanged(nameof(LiveText)); }
    }

    public string LiveText => _hasLive ? _live.ToString() : "—";

    /// <summary>True once a value has been read back into <see cref="Live"/> (i.e. the roll is locked).</summary>
    public bool HasLive => _hasLive;

    public void ClearLive() { _hasLive = false; _live = 0; OnPropertyChanged(nameof(LiveText)); }

    private string _avgText = "—";
    /// <summary>This stat's running average and observed range over the session's rolls, e.g.
    /// "10.4  (3–18)"; "—" until any roll has been sampled.</summary>
    public string AvgText { get => _avgText; private set => SetField(ref _avgText, value); }

    /// <summary>Updates the average/range readout from a stats snapshot (UI thread).</summary>
    public void SetAverage(double avg, int min, int max) => AvgText = $"{avg:0.0}  ({min}–{max})";

    /// <summary>Clears the average/range readout when the history is reset.</summary>
    public void ClearStats() => AvgText = "—";
}

/// <summary>
/// "Roll a ranger": automates the Ranger Center CREATE re-roll. The roll lives in a temporary scratch
/// record (not a roster slot), so the normal party scan can't see it — the user first captures the
/// numbers on the create screen and the trainer signature-scans for them (<see cref="CreationScanner"/>)
/// to lock the address. From then on it taps the spacebar to re-roll (<see cref="KeyboardSender"/>),
/// reads the new stats straight from memory, and stops once every stat meets its own minimum — exactly
/// the "keep re-rolling until IQ is high and MAXCON is at least 30 and …" workflow, hands-free. It only
/// reads the game's memory and taps the spacebar; it never writes to the game.
/// </summary>
public sealed class CharacterRollerViewModel : ObservableObject
{
    private readonly Func<ProcessMemory?> _getMem;
    private readonly Func<int?> _getPid;
    private readonly Action<string> _setStatus;
    private readonly SynchronizationContext _ui;

    /// <summary>Re-rolls used while disambiguating multiple signature matches.</summary>
    private const int MaxNarrowRolls = 8;

    private CancellationTokenSource? _cts;
    private nuint _lockAddr;
    private bool _maxConTracked;

    // MAXCON running stats, kept in step with the 7-attribute tally (a MAXCON sample is counted only when
    // the attribute roll it came with was accepted as fresh, so a stale read never skews it). Held in a
    // volatile reference reset by reassignment — exactly like _tally — so a reset on the UI thread is
    // promptly visible to the background roll loop and can't tear a shared counter mid-update.
    private volatile MaxConTally _maxConTally = new();

    /// <summary>Running mean/min/max of MAXCON over the session's fresh rolls. Not thread-safe on its own;
    /// like <see cref="RollTally"/> it is only mutated by whichever single thread is currently reading
    /// rolls, and a reset swaps in a new instance rather than clearing this one in place.</summary>
    private sealed class MaxConTally
    {
        private long _sum;
        private int _count, _min = int.MaxValue, _max = int.MinValue;
        public void Add(int v) { _sum += v; _count++; if (v < _min) _min = v; if (v > _max) _max = v; }
        public int Count => _count;
        public double Mean => _count == 0 ? 0 : (double)_sum / _count;
        public int Min => _count == 0 ? 0 : _min;
        public int Max => _count == 0 ? 0 : _max;
    }

    public CharacterRollerViewModel(Func<ProcessMemory?> getMem, Func<int?> getPid, Action<string> setStatus)
    {
        _getMem = getMem;
        _getPid = getPid;
        _setStatus = setStatus;
        _ui = SynchronizationContext.Current ?? new SynchronizationContext();

        for (int i = 0; i < CharacterFormat.AttributeNames.Length; i++)
        {
            var stat = new RollStatViewModel(CharacterFormat.AttributeNames[i], CreationScanner.MaxAttr,
                AttributeBook.DescriptionOf(i));
            stat.PropertyChanged += OnStatChanged;
            Stats.Add(stat);
        }
        MaxCon = new RollStatViewModel("MAXCON", CharacterFormat.MaxPlausibleCon,
            "Maximum Constitution (MAXCON) — the character's hit points; the higher it is, the more "
            + "damage they can take before going down. Rolled alongside the attributes, so you can "
            + "re-roll for a tougher ranger too.");
        MaxCon.PropertyChanged += OnStatChanged;

        _tally = new RollTally(Stats.Count);

        LockCommand = new RelayCommand(_ => Lock(), _ => Attached && !IsBusy && !IsRolling);
        ReadOnceCommand = new RelayCommand(_ => ReadOnce(), _ => IsLocked && Attached && !IsBusy && !IsRolling);
        ResetLockCommand = new RelayCommand(_ => ResetLock(), _ => IsLocked && !IsBusy && !IsRolling);
        StartCommand = new RelayCommand(_ => Start(), _ => IsLocked && Attached && _getPid() != null && !IsBusy && !IsRolling);
        StopCommand = new RelayCommand(_ => Stop(), _ => IsRolling);
        ClearStatsCommand = new RelayCommand(_ => ClearStats(), _ => _tally.Count > 0 && !IsRolling);
        ClearMinimumsCommand = new RelayCommand(_ => ClearMinimums(), _ => HasAnyMinimum() && !IsRolling);
    }

    private void OnStatChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RollStatViewModel.Minimum))
        {
            OnPropertyChanged(nameof(CriteriaText));
            OnPropertyChanged(nameof(OddsText));
            ClearMinimumsCommand?.RaiseCanExecuteChanged();   // null only during ctor, before this fires
        }
        else if (e.PropertyName == nameof(RollStatViewModel.Captured))
        {
            OnPropertyChanged(nameof(CapturedTotalText));     // the on-screen attribute total follows the boxes
        }
    }

    /// <summary>Session tally of every fresh attribute roll, for the statistics panel. Mutated only on
    /// whatever thread is currently reading rolls (the roll loop, or the UI thread for one-shot reads,
    /// which never overlap), then snapshotted to the UI. <c>volatile</c> so a reset's reassignment on
    /// the UI thread is promptly visible to the background roll loop.</summary>
    private volatile RollTally _tally;

    // --- captured / live stats --------------------------------------------------
    public ObservableCollection<RollStatViewModel> Stats { get; } = new();

    /// <summary>MAXCON, tracked and targetable only when the create buffer's full record shape was
    /// confirmed at lock time (the on-screen MAXCON and SKP landed at their record offsets).</summary>
    public RollStatViewModel MaxCon { get; }

    /// <summary>The seven attribute names, in record order (used to label rolls in summaries).</summary>
    public string[] StatNames => CharacterFormat.AttributeNames;

    // --- attribute totals (the seven attributes only — excludes SKP and MAXCON) -
    /// <summary>Sum of the seven live attribute values on the current roll; "—" until the roll is locked.</summary>
    public string LiveTotalText => Stats.All(s => s.HasLive) ? Stats.Sum(s => s.Live).ToString() : "—";

    /// <summary>Sum of the seven captured (on-screen) attribute values; "—" until all seven are entered.</summary>
    public string CapturedTotalText =>
        Stats.All(s => s.Captured.HasValue) ? Stats.Sum(s => s.Captured!.Value).ToString() : "—";

    private bool _maxConAvailable;
    /// <summary>True once a lock confirmed the record shape, so the MAXCON row is live and targetable.</summary>
    public bool MaxConAvailable
    {
        get => _maxConAvailable;
        private set { if (SetField(ref _maxConAvailable, value)) { OnPropertyChanged(nameof(MaxConNote)); OnPropertyChanged(nameof(OddsText)); } }
    }

    public string MaxConNote => _maxConAvailable
        ? "MAXCON confirmed — set a minimum to re-roll for hit points too."
        : "MAXCON tracking needs the on-screen MAXCON and SKP entered above so the record can be confirmed; "
          + "until then the roller targets the seven attributes only. (SKP always equals IQ on a fresh roll.)";

    // --- target criteria --------------------------------------------------------
    private int? _totalMinimum;
    /// <summary>Optional target floor for the attribute total (sum of the seven attributes; excludes SKP
    /// and MAXCON). null or 0 means "no requirement". Edited from the UI through
    /// <see cref="TotalMinimumText"/>.</summary>
    public int? TotalMinimum
    {
        get => _totalMinimum;
        set
        {
            int? clamped = value is null ? null : Math.Clamp(value.Value, 0, Stats.Count * CreationScanner.MaxAttr);
            if (SetField(ref _totalMinimum, clamped))
            {
                OnPropertyChanged(nameof(TotalMinimumText));
                OnPropertyChanged(nameof(CriteriaText));
                OnPropertyChanged(nameof(OddsText));
                ClearMinimumsCommand?.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>The total minimum as shown in / edited from its text box; a string binding so clearing the
    /// box reliably drops the target to null (see <see cref="RollStatViewModel.MinimumText"/>).</summary>
    public string TotalMinimumText
    {
        get => _totalMinimum?.ToString() ?? "";
        set => TotalMinimum = int.TryParse(value, out int n) ? n : null;
    }

    public string CriteriaText
    {
        get
        {
            var active = Stats.Where(s => s.Minimum > 0)         // null and 0 both mean "no requirement"
                              .Select(s => $"{s.Name} ≥ {s.Minimum!.Value}")
                              .ToList();
            if (TotalMinimum > 0)
                active.Add($"total ≥ {TotalMinimum.Value}");
            if (_maxConTracked && MaxCon.Minimum > 0)
                active.Add($"MAXCON ≥ {MaxCon.Minimum!.Value}");
            return active.Count == 0
                ? "No minimums set — the first roll will be accepted. Set a minimum on the stats you care about."
                : "Stop when " + string.Join(", ", active) + ".";
        }
    }

    /// <summary>Estimated odds of hitting the current target on any one roll, modelling each of the seven
    /// attributes as fair 3d6 (per-stat and total minimums together; MAXCON is not modelled). Recomputed
    /// whenever a minimum changes.</summary>
    public string OddsText
    {
        get
        {
            var mins = Stats.Select(s => s.Minimum ?? 0).ToArray();
            int totalMin = TotalMinimum ?? 0;

            // Targets 3d6 can never reach.
            var overMax = Stats.Where(s => (s.Minimum ?? 0) > RollOdds.Max).Select(s => s.Name).ToArray();
            if (overMax.Length > 0)
                return $"Out of reach: {string.Join(", ", overMax)} would need more than {RollOdds.Max}, "
                     + "the most three dice can total." + MaxConOddsCaveat();
            if (totalMin > RollOdds.Attributes * RollOdds.Max)
                return $"Out of reach: the attribute total can't exceed {RollOdds.Attributes * RollOdds.Max} "
                     + $"(all {RollOdds.Max}s)." + MaxConOddsCaveat();

            bool anyStat = mins.Any(m => m > RollOdds.Min);
            bool anyTotal = totalMin > RollOdds.Attributes * RollOdds.Min;   // > 21, the lowest possible sum
            if (!anyStat && !anyTotal)
                return "No effective attribute minimums — every roll qualifies (1 in 1)." + MaxConOddsCaveat();

            double p = RollOdds.PMeetsTarget(mins, totalMin);
            if (p <= 0) return "Out of reach as fair 3d6." + MaxConOddsCaveat();

            double expected = 1.0 / p;
            double rolls95 = Math.Max(1, Math.Ceiling(Math.Log(0.05) / Math.Log(1 - p)));   // ~95% chance of ≥1 hit
            string time = Humanize(expected * PerRollSeconds);
            return $"Odds (modelling each attribute as fair 3d6): about 1 in {expected:N0} rolls "
                 + $"(p = {Percent(p)}). At ~{PerRollSeconds:0.##}s per roll that's roughly {time}; "
                 + $"allow about {rolls95:N0} rolls for a 95% chance." + MaxConOddsCaveat();
        }
    }

    // Appended to the odds when a MAXCON minimum is also set: MAXCON isn't part of the 3d6 model, so the
    // real odds are a little longer than this attribute-only estimate.
    private string MaxConOddsCaveat() =>
        _maxConTracked && MaxCon.Minimum > 0
            ? " (Excludes the MAXCON minimum, which isn't modelled — the real odds are a little longer.)"
            : "";

    // --- session statistics (fed from the roll loop) ----------------------------
    private string _samplesText = "No rolls sampled yet.";
    public string SamplesText { get => _samplesText; private set => SetField(ref _samplesText, value); }

    private string _totalAvgText = "—";
    /// <summary>Running average and observed range of the attribute total (the seven attributes, excluding
    /// SKP and MAXCON) across the session's rolls; "—" until any roll has been sampled.</summary>
    public string TotalAvgText { get => _totalAvgText; private set => SetField(ref _totalAvgText, value); }

    // --- tuning -----------------------------------------------------------------
    private int _maxAttempts = 1000;
    public int MaxAttempts { get => _maxAttempts; set => SetField(ref _maxAttempts, Math.Clamp(value, 1, 1_000_000)); }

    // Settle is the pause between {SPACE} and the next read: it must outlast the game writing the fresh
    // roll to memory, or a stale read can skip a winning roll. Kept at the proven 130ms.
    private int _settleDelayMs = 130;
    public int SettleDelayMs
    {
        get => _settleDelayMs;
        set { if (SetField(ref _settleDelayMs, Math.Clamp(value, 0, 2000))) OnPropertyChanged(nameof(OddsText)); }
    }

    private int _focusDelayMs = 50;
    public int FocusDelayMs { get => _focusDelayMs; set => SetField(ref _focusDelayMs, Math.Clamp(value, 0, 2000)); }

    // Rough wall-clock cost of one roll: the post-space settle plus a little tap/loop overhead. Used only
    // to turn "expected rolls" into a rough time in the odds readout.
    private double PerRollSeconds => (_settleDelayMs + 30) / 1000.0;

    // --- state ------------------------------------------------------------------
    private bool _isBusy;
    public bool IsBusy { get => _isBusy; private set { if (SetField(ref _isBusy, value)) RaiseAll(); } }

    private bool _isRolling;
    public bool IsRolling { get => _isRolling; private set { if (SetField(ref _isRolling, value)) RaiseAll(); } }

    private bool _isLocked;
    public bool IsLocked { get => _isLocked; private set { if (SetField(ref _isLocked, value)) { OnPropertyChanged(nameof(LockInfo)); RaiseAll(); } } }

    public string LockInfo => _isLocked
        ? $"Locked onto the roll at 0x{(ulong)_lockAddr:X}" + (_maxConTracked ? " (record shape confirmed — MAXCON tracked)." : " (attributes only).")
        : "Not locked. Enter the numbers showing on the create screen, then Lock onto roll.";

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

    /// <summary>Re-evaluates command availability — call when attach state changes.</summary>
    public void RefreshCommands() => RaiseAll();

    /// <summary>Stops any roll and forgets the locked address (called on detach: the address belongs to
    /// a process we're no longer attached to).</summary>
    public void Reset()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        IsRolling = false;   // via the setters so bindings + command state refresh
        IsBusy = false;
        ResetLock();
    }

    // --- locating the roll buffer -----------------------------------------------
    private async void Lock()
    {
        var mem = _getMem();
        if (mem == null) { _setStatus("Attach to the game first."); return; }

        var captured = Stats.Select(s => s.Captured ?? 0).ToArray();   // blank reads as 0, which fails InRange below
        if (!CreationScanner.InRange(captured))
        {
            _setStatus($"Enter the seven attribute numbers from the create screen first (each between "
                     + $"{CreationScanner.MinAttr} and {CreationScanner.MaxAttr}).");
            return;
        }

        int capMaxCon = MaxCon.Captured ?? 0;
        int capSkp = _capturedSkp ?? -1;   // -1 disables the record-shape confirmation (MAXCON stays untracked)

        var cts = ResetCts();
        var ct = cts.Token;
        int? pid = _getPid();
        int settle = _settleDelayMs, focus = _focusDelayMs;   // snapshot UI-owned knobs for the bg thread
        IsBusy = true;
        _setStatus("Searching memory for the roll on the create screen… (if several spots match, the trainer "
                 + "re-rolls a few times to pin down the right one — so the on-screen roll may change).");

        try
        {
            var locked = await Task.Run(() =>
            {
                var matches = CreationScanner.Find(mem, captured, capMaxCon, capSkp, ct);
                if (matches.Count == 0) return (CreationMatch?)null;

                // Prefer confirmed record-shaped hits; fall back to plain attribute-only hits.
                var pool = matches.Where(m => m.Structural).ToList();
                if (pool.Count == 0) pool = matches;
                if (pool.Count == 1 || pid == null) return pool[0];
                return Narrow(mem, pid.Value, pool, settle, focus, ct);
            }, ct);

            // A detach (+ reattach) may have replaced the handle while we scanned; don't publish results
            // against a stale/disposed ProcessMemory (mirrors MainViewModel.Scan).
            if (ct.IsCancellationRequested || !ReferenceEquals(mem, _getMem())) return;

            if (locked == null)
            {
                _setStatus("Couldn't find those numbers in the game's memory. Make sure you're on the "
                         + "Ranger Center create screen and the values match exactly, then try again.");
                return;
            }

            _lockAddr = locked.Value.Address;
            _maxConTracked = locked.Value.Structural;
            MaxConAvailable = _maxConTracked;
            IsLocked = true;
            ClearStats();   // a fresh lock starts a fresh tally
            ReadInto(mem, _lockAddr);
            _setStatus(LockInfo + " Set your target below, then Roll.");
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { _setStatus("Lock failed: " + ex.Message); }
        finally { if (ReferenceEquals(_cts, cts)) IsBusy = false; }
    }

    // Disambiguate multiple signature hits: re-roll a few times and keep candidates whose seven bytes
    // stay in a plausible range AND actually change (the live buffer does; a coincidental static match
    // doesn't). Runs on the scan thread.
    private CreationMatch? Narrow(ProcessMemory mem, int pid, List<CreationMatch> matches,
                                  int settleMs, int focusMs, CancellationToken ct)
    {
        var cands = new List<Cand>();
        foreach (var m in matches)
        {
            var v = new int[CreationScanner.AttributeCount];
            if (CreationScanner.TryReadAttributes(mem, m.Address, v) && CreationScanner.InRange(v))
                cands.Add(new Cand(m, v));
        }
        if (cands.Count == 0) return matches[0];

        for (int r = 0; r < MaxNarrowRolls && cands.Count > 1; r++)
        {
            ct.ThrowIfCancellationRequested();
            if (!KeyboardSender.Send(pid, "{SPACE}", settleMs, focusMs, out _)) break;

            var keep = new List<Cand>();
            foreach (var c in cands)
            {
                var v = new int[CreationScanner.AttributeCount];
                if (!CreationScanner.TryReadAttributes(mem, c.Match.Address, v)) continue;
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
        return pool.Count > 0 ? pool[0].Match : null;
    }

    private sealed class Cand
    {
        public Cand(CreationMatch m, int[] last) { Match = m; Last = last; }
        public CreationMatch Match { get; }
        public int[] Last { get; set; }
        public bool ChangedEver { get; set; }
    }

    // The on-screen SKP value. SKP always equals IQ on a fresh roll, so it isn't a separate target — it's
    // captured only to confirm the record shape (which unlocks MAXCON tracking).
    private int? _capturedSkp;
    public int? CapturedSkp
    {
        get => _capturedSkp;
        set { if (SetField(ref _capturedSkp, value)) OnPropertyChanged(nameof(CapturedSkpText)); }
    }

    /// <summary>SKP as shown in / edited from its text box; a string binding so clearing the box reliably
    /// clears it to null (see <see cref="RollStatViewModel.MinimumText"/>).</summary>
    public string CapturedSkpText
    {
        get => _capturedSkp?.ToString() ?? "";
        set => CapturedSkp = int.TryParse(value, out int n) ? n : null;
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
        _maxConTracked = false;
        MaxConAvailable = false;
        Attempts = 0;
        BestText = "";
        ResultText = "";
        foreach (var s in Stats) s.ClearLive();
        MaxCon.ClearLive();
        OnPropertyChanged(nameof(LiveTotalText));
        ClearStats();   // the tally belonged to the buffer we're releasing
        OnPropertyChanged(nameof(IsLocked));
        OnPropertyChanged(nameof(LockInfo));
        OnPropertyChanged(nameof(CriteriaText));
        RaiseAll();
    }

    // Counts a set MAXCON minimum even when it isn't currently tracked: like the per-stat and total
    // minimums it persists across a reset/re-lock (and can only have been set while the record was
    // confirmed and the box enabled), so "Clear all targets" stays able to remove it. It's still only
    // *applied* (and shown in the criteria/odds) when _maxConTracked — see Start()/CriteriaText.
    private bool HasAnyMinimum() =>
        Stats.Any(s => s.Minimum is > 0) || TotalMinimum is > 0 || MaxCon.Minimum is > 0;

    /// <summary>Clears every target minimum (per-stat, total, and MAXCON) back to blank ("no requirement").</summary>
    private void ClearMinimums()
    {
        foreach (var s in Stats) s.Minimum = null;   // setters fire PropertyChanged → Criteria/command refresh
        MaxCon.Minimum = null;
        TotalMinimum = null;
    }

    // --- statistics -------------------------------------------------------------
    /// <summary>Empties the session tally and the statistics readouts.</summary>
    private void ClearStats()
    {
        _tally = new RollTally(Stats.Count);
        _maxConTally = new MaxConTally();
        foreach (var s in Stats) s.ClearStats();
        MaxCon.ClearStats();
        SamplesText = "No rolls sampled yet.";
        TotalAvgText = "—";
        RaiseAll();
    }

    /// <summary>Pushes a stats snapshot into the readouts (UI thread).</summary>
    private void ApplyStatsSnapshot(RollTallySnapshot s, double maxConMean, int maxConMin, int maxConMax, int maxConCount)
    {
        if (!_isLocked) return;   // a detach/reset may have run before this queued update fired
        for (int k = 0; k < Stats.Count && k < s.StatMean.Length; k++)
            Stats[k].SetAverage(s.StatMean[k], s.StatMin[k], s.StatMax[k]);
        if (_maxConTracked && maxConCount > 0)
            MaxCon.SetAverage(maxConMean, maxConMin, maxConMax);

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
        int count = Stats.Count;

        // Snapshot everything the background loop needs so it never reads UI-owned state (the lock
        // address, the target, the delays) across threads.
        nuint lockAddr = _lockAddr;
        bool trackMaxCon = _maxConTracked;
        int[] mins = Stats.Select(s => s.Minimum ?? 0).ToArray();   // per-stat floors (blank = 0 = unconstrained)
        int maxConMinTarget = trackMaxCon ? (MaxCon.Minimum ?? 0) : 0;
        int totalMinTarget = TotalMinimum ?? 0;                     // attribute-total floor (blank = 0 = unconstrained)
        int maxAttempts = _maxAttempts;
        int settle = _settleDelayMs, focus = _focusDelayMs;

        OnUi(() => { Attempts = 0; BestText = ""; ResultText = ""; });
        IsRolling = true;
        _setStatus("Rolling… (the game window comes forward for each re-roll; click Stop here to halt).");

        int tried = 0;
        bool met = false;
        string failure = "";
        int[]? winning = null;
        int winningMaxCon = 0;

        try
        {
            await Task.Run(() =>
            {
                var v = new int[count];
                int[]? best = null;       // best-so-far is owned solely by this loop thread
                int bestMaxCon = 0, bestAttempt = 0;

                while (!ct.IsCancellationRequested && tried < maxAttempts)
                {
                    // A short read, out-of-range attributes, or an implausible MAXCON all mean the buffer no
                    // longer holds a roll (the create screen closed, or the game reused the memory). Bail
                    // rather than treat unrelated bytes as a winning roll — the in-range gate is what stops a
                    // coincidental "match" from surfacing the game and claiming success on garbage.
                    if (!CreationScanner.TryReadAttributes(mem, lockAddr, v) || !CreationScanner.InRange(v))
                    { failure = "lost the locked roll (did the create screen close?)"; break; }

                    int curMaxCon = 0;
                    if (trackMaxCon && (!CreationScanner.TryReadMaxCon(mem, lockAddr, out curMaxCon) || !IsPlausibleMaxCon(curMaxCon)))
                    { failure = "lost the locked roll (did the create screen close?)"; break; }

                    tried++;

                    if (best == null || IsBetter(v, curMaxCon, best, bestMaxCon, mins, maxConMinTarget, totalMinTarget, trackMaxCon))
                    { best = (int[])v.Clone(); bestMaxCon = curMaxCon; bestAttempt = tried; }

                    // Hand the UI immutable snapshots: the loop keeps overwriting `v`, and `best` is
                    // replaced (never edited) so its reference is safe to read later.
                    var rollSnap = (int[])v.Clone();
                    int[] bestSnap = best;
                    int bestMaxConSnap = bestMaxCon, curMaxConSnap = curMaxCon;
                    int bestAttemptSnap = bestAttempt, triedSnap = tried;
                    OnUi(() => PublishRoll(rollSnap, curMaxConSnap, bestSnap, bestMaxConSnap, bestAttemptSnap, mins, maxConMinTarget, totalMinTarget, triedSnap));

                    // Tally the roll for the statistics panel; a duplicate (stale read) is dropped, so
                    // only post a fresh snapshot when it actually changed the numbers.
                    if (_tally.Add(v))
                    {
                        var mc = _maxConTally;
                        if (trackMaxCon) mc.Add(curMaxCon);
                        var statsSnap = _tally.Snapshot();
                        double mcMean = mc.Mean;
                        int mcMin = mc.Min, mcMax = mc.Max, mcCount = mc.Count;
                        OnUi(() => ApplyStatsSnapshot(statsSnap, mcMean, mcMin, mcMax, mcCount));
                    }

                    if (Shortfall(v, curMaxCon, mins, maxConMinTarget, totalMinTarget, trackMaxCon) == 0)
                    { winning = rollSnap; winningMaxCon = curMaxCon; met = true; break; }
                    if (ct.IsCancellationRequested) break;
                    if (!KeyboardSender.Send(pid.Value, "{SPACE}", settle, focus, out var err))
                    { failure = err; break; }
                }
            }, ct);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { failure = ex.Message; }
        finally { if (ReferenceEquals(_cts, cts)) IsRolling = false; }

        // If a detach/reset (or a fresh Start) superseded this run while it was unwinding, don't publish
        // its result — Reset() already set the status/lock state, and surfacing the game or clobbering the
        // status here would fight it. Mirrors the stale-scan guard in Lock().
        if (!ReferenceEquals(_cts, cts)) return;

        if (met && winning != null)
        {
            KeyboardSender.BringToFront(pid.Value);   // surface the game so the user can accept the roll
            ResultText = $"✔ Found it after {tried} roll(s): {Describe(winning, winningMaxCon, mins, maxConMinTarget, totalMinTarget, trackMaxCon)}. "
                       + "The game is in front — press Return to accept this roll, then name the ranger.";
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
                       + "Loosen the target or raise the roll limit.";
            _setStatus(ResultText);
        }
    }

    private void Stop()
    {
        _cts?.Cancel();
        _setStatus("Stopping the roller…");
    }

    // --- evaluation helpers -----------------------------------------------------
    // Total amount by which a roll falls below its minimums — per-stat, the attribute total (sum of the
    // seven attributes), and MAXCON; 0 means every target is met.
    private static int Shortfall(int[] v, int maxCon, int[] mins, int maxConMin, int totalMin, bool trackMaxCon)
    {
        int sum = 0, attrTotal = 0;
        for (int k = 0; k < v.Length; k++)
        {
            attrTotal += v[k];
            if (k < mins.Length) { int d = mins[k] - v[k]; if (d > 0) sum += d; }
        }
        if (totalMin > 0) { int d = totalMin - attrTotal; if (d > 0) sum += d; }
        if (trackMaxCon && maxConMin > 0) { int d = maxConMin - maxCon; if (d > 0) sum += d; }
        return sum;
    }

    private static int Sum(int[] v, int maxCon, bool trackMaxCon)
    {
        int sum = 0;
        foreach (var x in v) sum += x;
        if (trackMaxCon) sum += maxCon;
        return sum;
    }

    // Ranks one roll above another: closer to meeting every minimum first (less shortfall), then the
    // higher overall total.
    private static bool IsBetter(int[] cand, int candMaxCon, int[] best, int bestMaxCon, int[] mins, int maxConMin, int totalMin, bool trackMaxCon)
    {
        int cs = Shortfall(cand, candMaxCon, mins, maxConMin, totalMin, trackMaxCon);
        int bs = Shortfall(best, bestMaxCon, mins, maxConMin, totalMin, trackMaxCon);
        if (cs != bs) return cs < bs;
        return Sum(cand, candMaxCon, trackMaxCon) > Sum(best, bestMaxCon, trackMaxCon);
    }

    // "STR 12, IQ 14, …, MAXCON 30 · total 88 (short 3)" — the shared roll summary.
    private string Describe(int[] v, int maxCon, int[] mins, int maxConMin, int totalMin, bool trackMaxCon)
    {
        var parts = string.Join(", ", v.Select((x, k) => $"{StatNames[k]} {x}"));
        if (trackMaxCon) parts += $", MAXCON {maxCon}";
        if (totalMin > 0) parts += $" · total {v.Sum()}";
        int sf = Shortfall(v, maxCon, mins, maxConMin, totalMin, trackMaxCon);
        return sf == 0 ? parts : $"{parts} (short {sf})";
    }

    // Runs on the UI thread (via OnUi) with immutable snapshots taken in the roll loop: the live
    // readbacks, attempt count, and the best-so-far line.
    private void PublishRoll(int[] roll, int maxCon, int[] best, int bestMaxCon, int bestAttempt, int[] mins, int maxConMin, int totalMin, int attempt)
    {
        if (!_isLocked) return;   // a detach/reset may have run before this queued update fired
        for (int k = 0; k < Stats.Count && k < roll.Length; k++) Stats[k].Live = roll[k];
        if (_maxConTracked) MaxCon.Live = maxCon;
        OnPropertyChanged(nameof(LiveTotalText));
        Attempts = attempt;
        BestText = $"{Describe(best, bestMaxCon, mins, maxConMin, totalMin, _maxConTracked)} (roll #{bestAttempt})";
    }

    private bool ReadInto(ProcessMemory mem, nuint addr)
    {
        var v = new int[Stats.Count];
        // Reject a short read OR bytes that aren't a plausible roll: the create buffer is ephemeral, so if
        // the screen has closed and the address now holds unrelated bytes the read still succeeds — the
        // in-range gate keeps that garbage out of the Live readout and the tally (mirrors Narrow()).
        if (!CreationScanner.TryReadAttributes(mem, addr, v) || !CreationScanner.InRange(v)) return false;
        for (int k = 0; k < Stats.Count; k++) Stats[k].Live = v[k];
        OnPropertyChanged(nameof(LiveTotalText));

        int maxCon = 0;
        bool haveMaxCon = _maxConTracked && CreationScanner.TryReadMaxCon(mem, addr, out maxCon) && IsPlausibleMaxCon(maxCon);
        if (haveMaxCon) MaxCon.Live = maxCon;

        // A one-shot read (lock / Read current roll) counts as a sample too; a repeat of the same static
        // roll is dropped by the tally's dedup. Runs on the UI thread, and never while the roll loop is
        // active (the commands are disabled then), so there's no race.
        if (_tally.Add(v))
        {
            var mc = _maxConTally;
            if (haveMaxCon) mc.Add(maxCon);
            ApplyStatsSnapshot(_tally.Snapshot(), mc.Mean, mc.Min, mc.Max, mc.Count);
        }
        return true;
    }

    /// <summary>A MAXCON value read from the create buffer looks real: within 1..<see cref="CharacterFormat.MaxPlausibleCon"/>.
    /// Guards the tracked-MAXCON readback the same way <see cref="CreationScanner.InRange"/> guards the attributes.</summary>
    private static bool IsPlausibleMaxCon(int maxCon) => maxCon >= 1 && maxCon <= CharacterFormat.MaxPlausibleCon;

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

    // Percentage down to 0.01%, then scientific for the genuinely tiny (e.g. all-18 targets), where the
    // "1 in N rolls" figure alongside it already conveys the magnitude.
    private static string Percent(double p) =>
        p >= 0.0001 ? p.ToString("0.####%") : p.ToString("0.0E+0");

    private void RaiseAll()
    {
        LockCommand.RaiseCanExecuteChanged();
        ReadOnceCommand.RaiseCanExecuteChanged();
        ResetLockCommand.RaiseCanExecuteChanged();
        StartCommand.RaiseCanExecuteChanged();
        StopCommand.RaiseCanExecuteChanged();
        ClearStatsCommand.RaiseCanExecuteChanged();
        ClearMinimumsCommand.RaiseCanExecuteChanged();
    }
}

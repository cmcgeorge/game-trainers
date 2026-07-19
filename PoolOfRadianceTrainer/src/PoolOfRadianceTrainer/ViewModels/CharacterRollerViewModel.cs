using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using PoolOfRadianceTrainer.Game;
using PoolOfRadianceTrainer.Memory;
using PoolOfRadianceTrainer.Mvvm;

namespace PoolOfRadianceTrainer.ViewModels;

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
        set { if (SetProperty(ref _captured, value)) OnPropertyChanged(nameof(CapturedText)); }
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
            if (SetProperty(ref _minimum, clamped)) OnPropertyChanged(nameof(MinimumText));
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
        set { _hasLive = true; SetProperty(ref _live, value); OnPropertyChanged(nameof(LiveText)); }
    }

    public string LiveText => _hasLive ? _live.ToString() : "—";

    /// <summary>True once a value has been read back into <see cref="Live"/> (i.e. the roll is locked).</summary>
    public bool HasLive => _hasLive;

    public void ClearLive() { _hasLive = false; _live = 0; OnPropertyChanged(nameof(LiveText)); }

    private string _avgText = "—";
    /// <summary>This stat's running average and observed range over the session's rolls, e.g.
    /// "10.4  (3–18)"; "—" until any roll has been sampled.</summary>
    public string AvgText { get => _avgText; private set => SetProperty(ref _avgText, value); }

    /// <summary>Updates the average/range readout from a stats snapshot (UI thread).</summary>
    public void SetAverage(double avg, int min, int max) => AvgText = $"{avg:0.0}  ({min}–{max})";

    /// <summary>Clears the average/range readout when the history is reset.</summary>
    public void ClearStats() => AvgText = "—";
}

/// <summary>
/// "Roll a hero": automates the Pool of Radiance create-a-character re-roll. The roll lives in a
/// temporary scratch record (not a party member yet), so the normal party scan can't see it — the
/// user first captures the numbers on the create screen and the trainer signature-scans for them
/// (<see cref="CreationScanner"/>) to lock the address. From then on it taps the <c>n</c> key to
/// re-roll (answering "no" to "keep character (y/n)?" via <see cref="KeyboardSender"/>), reads the
/// new stats straight from memory, and stops once every stat meets its own minimum — exactly the
/// "keep re-rolling until Strength and Constitution are high and …" workflow, hands-free. It only
/// reads the game's memory and taps the <c>n</c> key; it never writes to the game.
/// </summary>
public sealed class CharacterRollerViewModel : ObservableObject
{
    private readonly Func<ProcessMemory?> _getMem;
    private readonly Func<int?> _getPid;
    private readonly Action<string> _setStatus;
    private readonly SynchronizationContext _ui;

    /// <summary>Re-rolls used while disambiguating multiple signature matches.</summary>
    private const int MaxNarrowRolls = 8;

    // The key the game answers "no" to "keep character (y/n)?" with, re-rolling the stats.
    private const string RerollKey = "n";

    private CancellationTokenSource? _cts;
    private nuint _lockAddr;
    private bool _hpTracked;

    // Running stats for the two extras (STR% and HP), kept in step with the six-ability tally (a
    // sample is counted only when the ability roll it came with was accepted as fresh, so a stale
    // read never skews it). Held in a volatile reference reset by reassignment — exactly like
    // _tally — so a reset on the UI thread is promptly visible to the background roll loop and
    // can't tear a shared counter mid-update.
    private volatile ExtraTally _strPercentTally = new();
    private volatile ExtraTally _hpTally = new();

    /// <summary>Running mean/min/max of one extra (STR% or HP) over the session's fresh rolls. Not
    /// thread-safe on its own; like <see cref="RollTally"/> it is only mutated by whichever single
    /// thread is currently reading rolls, and a reset swaps in a new instance rather than clearing
    /// this one in place.</summary>
    private sealed class ExtraTally
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

        for (int i = 0; i < PorFormat.StatCount; i++)
        {
            var stat = new RollStatViewModel(PorFormat.StatsShort[i], CreationScanner.MaxStat,
                AttributeBook.DescriptionOf(i));
            stat.PropertyChanged += OnStatChanged;
            Stats.Add(stat);
        }
        StrPercent = new RollStatViewModel("STR%", CreationScanner.MaxStrPercent,
            AttributeBook.StrPercent.Description);
        StrPercent.PropertyChanged += OnStatChanged;

        Hp = new RollStatViewModel("HP", byte.MaxValue,
            AttributeBook.HitPoints.Description);
        Hp.PropertyChanged += OnStatChanged;

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
            OnPropertyChanged(nameof(CapturedTotalText));     // the on-screen ability total follows the boxes
        }
    }

    /// <summary>Session tally of every fresh ability roll, for the statistics panel. Mutated only on
    /// whatever thread is currently reading rolls (the roll loop, or the UI thread for one-shot reads,
    /// which never overlap), then snapshotted to the UI. <c>volatile</c> so a reset's reassignment on
    /// the UI thread is promptly visible to the background roll loop.</summary>
    private volatile RollTally _tally;

    // --- captured / live stats --------------------------------------------------
    public ObservableCollection<RollStatViewModel> Stats { get; } = new();

    /// <summary>Exceptional strength percentile (STR%), tracked and targetable always — it is part of
    /// the seven-byte signature, so it is read on every roll. Not part of the 3d6 odds model (it only
    /// applies to fighters with Strength 18).</summary>
    public RollStatViewModel StrPercent { get; }

    /// <summary>Hit points (HP), tracked and targetable only when the create buffer's full record shape
    /// was confirmed at lock time (the on-screen HP landed at its record offset).</summary>
    public RollStatViewModel Hp { get; }

    /// <summary>The six ability abbreviations, in record order (used to label rolls in summaries).</summary>
    public string[] StatNames => PorFormat.StatsShort;

    // --- ability totals (the six abilities only — excludes STR% and HP) --------
    /// <summary>Sum of the six live ability values on the current roll; "—" until the roll is locked.</summary>
    public string LiveTotalText => Stats.All(s => s.HasLive) ? Stats.Sum(s => s.Live).ToString() : "—";

    /// <summary>Sum of the six captured (on-screen) ability values; "—" until all six are entered.</summary>
    public string CapturedTotalText =>
        Stats.All(s => s.Captured.HasValue) ? Stats.Sum(s => s.Captured!.Value).ToString() : "—";

    private bool _hpAvailable;
    /// <summary>True once a lock confirmed the record shape, so the HP row is live and targetable.</summary>
    public bool HpAvailable
    {
        get => _hpAvailable;
        private set { if (SetProperty(ref _hpAvailable, value)) { OnPropertyChanged(nameof(HpNote)); OnPropertyChanged(nameof(OddsText)); } }
    }

    public string HpNote => _hpAvailable
        ? "HP confirmed — set a minimum to re-roll for hit points too."
        : "HP tracking needs the on-screen HP entered above so the record can be confirmed; until then "
          + "the roller targets the six abilities and STR% only.";

    // --- target criteria --------------------------------------------------------
    private int? _totalMinimum;
    /// <summary>Optional target floor for the ability total (sum of the six abilities; excludes STR%
    /// and HP). null or 0 means "no requirement". Edited from the UI through
    /// <see cref="TotalMinimumText"/>.</summary>
    public int? TotalMinimum
    {
        get => _totalMinimum;
        set
        {
            int? clamped = value is null ? null : Math.Clamp(value.Value, 0, Stats.Count * CreationScanner.MaxStat);
            if (SetProperty(ref _totalMinimum, clamped))
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
            if (StrPercent.Minimum > 0)
                active.Add($"STR% ≥ {StrPercent.Minimum!.Value}");
            if (TotalMinimum > 0)
                active.Add($"total ≥ {TotalMinimum.Value}");
            if (_hpTracked && Hp.Minimum > 0)
                active.Add($"HP ≥ {Hp.Minimum!.Value}");
            return active.Count == 0
                ? "No minimums set — the first roll will be accepted. Set a minimum on the abilities you care about."
                : "Stop when " + string.Join(", ", active) + ".";
        }
    }

    /// <summary>Estimated odds of hitting the current target on any one roll, modelling each of the six
    /// abilities as fair 3d6 (per-stat and total minimums together; STR% and HP are not modelled).
    /// Recomputed whenever a minimum changes.</summary>
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
                     + "the most three dice can total." + ExtrasOddsCaveat();
            if (totalMin > RollOdds.Attributes * RollOdds.Max)
                return $"Out of reach: the ability total can't exceed {RollOdds.Attributes * RollOdds.Max} "
                     + $"(all {RollOdds.Max}s)." + ExtrasOddsCaveat();

            bool anyStat = mins.Any(m => m > RollOdds.Min);
            bool anyTotal = totalMin > RollOdds.Attributes * RollOdds.Min;   // > 18, the lowest possible sum
            if (!anyStat && !anyTotal)
                return "No effective ability minimums — every roll qualifies (1 in 1)." + ExtrasOddsCaveat();

            double p = RollOdds.PMeetsTarget(mins, totalMin);
            if (p <= 0) return "Out of reach as fair 3d6." + ExtrasOddsCaveat();

            double expected = 1.0 / p;
            double rolls95 = Math.Max(1, Math.Ceiling(Math.Log(0.05) / Math.Log(1 - p)));   // ~95% chance of ≥1 hit
            string time = Humanize(expected * PerRollSeconds);
            return $"Odds (modelling each ability as fair 3d6): about 1 in {expected:N0} rolls "
                 + $"(p = {Percent(p)}). At ~{PerRollSeconds:0.##}s per roll that's roughly {time}; "
                 + $"allow about {rolls95:N0} rolls for a 95% chance." + ExtrasOddsCaveat();
        }
    }

    // Appended to the odds when a STR% or HP minimum is also set: those aren't part of the 3d6 model,
    // so the real odds are a little longer than this ability-only estimate.
    private string ExtrasOddsCaveat()
    {
        bool strPct = StrPercent.Minimum > 0;
        bool hp = _hpTracked && Hp.Minimum > 0;
        if (!strPct && !hp) return "";
        var names = new List<string>(2);
        if (strPct) names.Add("STR%");
        if (hp) names.Add("HP");
        return $" (Excludes the {string.Join(" and ", names)} minimum{(names.Count > 1 ? "s" : "")}, "
             + $"which {(names.Count > 1 ? "aren't" : "isn't")} modelled — the real odds are a little longer.)";
    }

    // --- session statistics (fed from the roll loop) ----------------------------
    private string _samplesText = "No rolls sampled yet.";
    public string SamplesText { get => _samplesText; private set => SetProperty(ref _samplesText, value); }

    private string _totalAvgText = "—";
    /// <summary>Running average and observed range of the ability total (the six abilities, excluding
    /// STR% and HP) across the session's rolls; "—" until any roll has been sampled.</summary>
    public string TotalAvgText { get => _totalAvgText; private set => SetProperty(ref _totalAvgText, value); }

    // --- tuning -----------------------------------------------------------------
    private int _maxAttempts = 1000;
    public int MaxAttempts { get => _maxAttempts; set => SetProperty(ref _maxAttempts, Math.Clamp(value, 1, 1_000_000)); }

    // Settle is the pause between the 'n' key and the next read: it must outlast the game writing the
    // fresh roll to memory, or a stale read can skip a winning roll.
    private int _settleDelayMs = 130;
    public int SettleDelayMs
    {
        get => _settleDelayMs;
        set { if (SetProperty(ref _settleDelayMs, Math.Clamp(value, 0, 2000))) OnPropertyChanged(nameof(OddsText)); }
    }

    private int _focusDelayMs = 50;
    public int FocusDelayMs { get => _focusDelayMs; set => SetProperty(ref _focusDelayMs, Math.Clamp(value, 0, 2000)); }

    // Rough wall-clock cost of one roll: the post-key settle plus a little tap/loop overhead. Used only
    // to turn "expected rolls" into a rough time in the odds readout.
    private double PerRollSeconds => (_settleDelayMs + 30) / 1000.0;

    // --- state ------------------------------------------------------------------
    private bool _isBusy;
    public bool IsBusy { get => _isBusy; private set { if (SetProperty(ref _isBusy, value)) RaiseAll(); } }

    private bool _isRolling;
    public bool IsRolling { get => _isRolling; private set { if (SetProperty(ref _isRolling, value)) RaiseAll(); } }

    private bool _isLocked;
    public bool IsLocked { get => _isLocked; private set { if (SetProperty(ref _isLocked, value)) { OnPropertyChanged(nameof(LockInfo)); RaiseAll(); } } }

    public string LockInfo => _isLocked
        ? $"Locked onto the roll at 0x{(ulong)_lockAddr:X}" + (_hpTracked ? " (record shape confirmed — HP tracked)." : " (abilities and STR% only).")
        : "Not locked. Enter the numbers showing on the create screen, then Lock onto roll.";

    private int _attempts;
    public int Attempts { get => _attempts; private set { if (SetProperty(ref _attempts, value)) OnPropertyChanged(nameof(AttemptsText)); } }
    public string AttemptsText => _attempts == 0 ? "" : $"Rolls tried: {_attempts}";

    private string _bestText = "";
    public string BestText { get => _bestText; private set => SetProperty(ref _bestText, value); }

    private string _resultText = "";
    public string ResultText { get => _resultText; private set => SetProperty(ref _resultText, value); }

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

        // Build the seven-byte signature: six abilities + STR%. Blank reads as 0, which fails InRange below.
        var captured = new int[CreationScanner.SignatureCount];
        for (int i = 0; i < PorFormat.StatCount; i++) captured[i] = Stats[i].Captured ?? 0;
        captured[PorFormat.StatCount] = StrPercent.Captured ?? 0;
        if (!CreationScanner.InRange(captured))
        {
            _setStatus($"Enter the six ability numbers (and STR% — 0 for non-fighters) from the create "
                     + $"screen first (abilities {CreationScanner.MinStat}–{CreationScanner.MaxStat}, "
                     + $"STR% 0–{CreationScanner.MaxStrPercent}).");
            return;
        }

        int capHp = Hp.Captured ?? 0;   // 0 disables the record-shape confirmation (HP stays untracked)

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
                var matches = CreationScanner.Find(mem, captured, capHp, ct);
                if (matches.Count == 0) return (CreationMatch?)null;

                // Prefer confirmed record-shaped hits; fall back to plain signature-only hits.
                var pool = matches.Where(m => m.Structural).ToList();
                if (pool.Count == 0) pool = matches;
                if (pool.Count == 1 || pid == null) return pool[0];
                return Narrow(mem, pid.Value, pool, settle, focus, ct);
            }, ct);

            // A detach (+ reattach) may have replaced the handle while we scanned; don't publish results
            // against a stale/disposed ProcessMemory.
            if (ct.IsCancellationRequested || !ReferenceEquals(mem, _getMem())) return;

            if (locked == null)
            {
                _setStatus("Couldn't find those numbers in the game's memory. Make sure you're on the "
                         + "create-a-character screen and the values match exactly, then try again.");
                return;
            }

            _lockAddr = locked.Value.Address;
            _hpTracked = locked.Value.Structural;
            HpAvailable = _hpTracked;
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
            var v = new int[CreationScanner.SignatureCount];
            if (CreationScanner.TryReadSignature(mem, m.Address, v) && CreationScanner.InRange(v))
                cands.Add(new Cand(m, v));
        }
        if (cands.Count == 0) return matches[0];

        for (int r = 0; r < MaxNarrowRolls && cands.Count > 1; r++)
        {
            ct.ThrowIfCancellationRequested();
            if (!KeyboardSender.Send(pid, RerollKey, settleMs, focusMs, out _)) break;

            var keep = new List<Cand>();
            foreach (var c in cands)
            {
                var v = new int[CreationScanner.SignatureCount];
                if (!CreationScanner.TryReadSignature(mem, c.Match.Address, v)) continue;
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
        _hpTracked = false;
        HpAvailable = false;
        Attempts = 0;
        BestText = "";
        ResultText = "";
        foreach (var s in Stats) s.ClearLive();
        StrPercent.ClearLive();
        Hp.ClearLive();
        OnPropertyChanged(nameof(LiveTotalText));
        ClearStats();   // the tally belonged to the buffer we're releasing
        OnPropertyChanged(nameof(IsLocked));
        OnPropertyChanged(nameof(LockInfo));
        OnPropertyChanged(nameof(CriteriaText));
        RaiseAll();
    }

    // Counts a set HP minimum even when it isn't currently tracked: like the per-stat and total
    // minimums it persists across a reset/re-lock (and can only have been set while the record was
    // confirmed and the box enabled), so "Clear all targets" stays able to remove it. It's still only
    // *applied* (and shown in the criteria/odds) when _hpTracked — see Start()/CriteriaText.
    private bool HasAnyMinimum() =>
        Stats.Any(s => s.Minimum is > 0) || StrPercent.Minimum is > 0 || TotalMinimum is > 0 || Hp.Minimum is > 0;

    /// <summary>Clears every target minimum (per-stat, STR%, total, and HP) back to blank ("no requirement").</summary>
    private void ClearMinimums()
    {
        foreach (var s in Stats) s.Minimum = null;   // setters fire PropertyChanged → Criteria/command refresh
        StrPercent.Minimum = null;
        Hp.Minimum = null;
        TotalMinimum = null;
    }

    // --- statistics -------------------------------------------------------------
    /// <summary>Empties the session tally and the statistics readouts.</summary>
    private void ClearStats()
    {
        _tally = new RollTally(Stats.Count);
        _strPercentTally = new ExtraTally();
        _hpTally = new ExtraTally();
        foreach (var s in Stats) s.ClearStats();
        StrPercent.ClearStats();
        Hp.ClearStats();
        SamplesText = "No rolls sampled yet.";
        TotalAvgText = "—";
        RaiseAll();
    }

    /// <summary>Pushes a stats snapshot into the readouts (UI thread).</summary>
    private void ApplyStatsSnapshot(RollTallySnapshot s, double strPctMean, int strPctMin, int strPctMax, int strPctCount,
                                    double hpMean, int hpMin, int hpMax, int hpCount)
    {
        if (!_isLocked) return;   // a detach/reset may have run before this queued update fired
        for (int k = 0; k < Stats.Count && k < s.StatMean.Length; k++)
            Stats[k].SetAverage(s.StatMean[k], s.StatMin[k], s.StatMax[k]);
        if (strPctCount > 0)
            StrPercent.SetAverage(strPctMean, strPctMin, strPctMax);
        if (_hpTracked && hpCount > 0)
            Hp.SetAverage(hpMean, hpMin, hpMax);

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
        bool trackHp = _hpTracked;
        int[] mins = Stats.Select(s => s.Minimum ?? 0).ToArray();   // per-stat floors (blank = 0 = unconstrained)
        int strPctMinTarget = StrPercent.Minimum ?? 0;
        int hpMinTarget = trackHp ? (Hp.Minimum ?? 0) : 0;
        int totalMinTarget = TotalMinimum ?? 0;                     // ability-total floor (blank = 0 = unconstrained)
        int maxAttempts = _maxAttempts;
        int settle = _settleDelayMs, focus = _focusDelayMs;

        OnUi(() => { Attempts = 0; BestText = ""; ResultText = ""; });
        IsRolling = true;
        _setStatus("Rolling… (the game window comes forward for each re-roll; click Stop here to halt).");

        int tried = 0;
        bool met = false;
        string failure = "";
        int[]? winning = null;
        int winningStrPct = 0, winningHp = 0;

        try
        {
            await Task.Run(() =>
            {
                var sig = new int[CreationScanner.SignatureCount];
                var v = new int[count];       // the six abilities only, for the tally / shortfall / best
                int[]? best = null;       // best-so-far is owned solely by this loop thread
                int bestStrPct = 0, bestHp = 0, bestAttempt = 0;

                while (!ct.IsCancellationRequested && tried < maxAttempts)
                {
                    // A short read, out-of-range bytes, or an implausible HP all mean the buffer no
                    // longer holds a roll (the create screen closed, or the game reused the memory). Bail
                    // rather than treat unrelated bytes as a winning roll — the in-range gate is what stops a
                    // coincidental "match" from surfacing the game and claiming success on garbage.
                    if (!CreationScanner.TryReadSignature(mem, lockAddr, sig) || !CreationScanner.InRange(sig))
                    { failure = "lost the locked roll (did the create screen close?)"; break; }

                    for (int k = 0; k < count; k++) v[k] = sig[k];
                    int curStrPct = sig[count];
                    int curHp = 0;
                    if (trackHp && (!CreationScanner.TryReadHp(mem, lockAddr, out curHp) || !IsPlausibleHp(curHp)))
                    { failure = "lost the locked roll (did the create screen close?)"; break; }

                    tried++;

                    if (best == null || IsBetter(v, curStrPct, curHp, best, bestStrPct, bestHp, mins, strPctMinTarget, hpMinTarget, totalMinTarget, trackHp))
                    { best = (int[])v.Clone(); bestStrPct = curStrPct; bestHp = curHp; bestAttempt = tried; }

                    // Hand the UI immutable snapshots: the loop keeps overwriting `v`, and `best` is
                    // replaced (never edited) so its reference is safe to read later.
                    var rollSnap = (int[])v.Clone();
                    int[] bestSnap = best;
                    int curStrPctSnap = curStrPct, curHpSnap = curHp;
                    int bestStrPctSnap = bestStrPct, bestHpSnap = bestHp;
                    int bestAttemptSnap = bestAttempt, triedSnap = tried;
                    OnUi(() => PublishRoll(rollSnap, curStrPctSnap, curHpSnap, bestSnap, bestStrPctSnap, bestHpSnap, bestAttemptSnap, mins, strPctMinTarget, hpMinTarget, totalMinTarget, triedSnap));

                    // Tally the roll for the statistics panel; a duplicate (stale read) is dropped, so
                    // only post a fresh snapshot when it actually changed the numbers.
                    if (_tally.Add(v))
                    {
                        var sp = _strPercentTally; sp.Add(curStrPct);
                        var hpT = _hpTally;
                        if (trackHp) hpT.Add(curHp);
                        var statsSnap = _tally.Snapshot();
                        double spMean = sp.Mean; int spMin = sp.Min, spMax = sp.Max, spCount = sp.Count;
                        double hpMean = hpT.Mean; int hpMin = hpT.Min, hpMax = hpT.Max, hpCount = hpT.Count;
                        OnUi(() => ApplyStatsSnapshot(statsSnap, spMean, spMin, spMax, spCount, hpMean, hpMin, hpMax, hpCount));
                    }

                    if (Shortfall(v, curStrPct, curHp, mins, strPctMinTarget, hpMinTarget, totalMinTarget, trackHp) == 0)
                    { winning = rollSnap; winningStrPct = curStrPct; winningHp = curHp; met = true; break; }
                    if (ct.IsCancellationRequested) break;
                    if (!KeyboardSender.Send(pid.Value, RerollKey, settle, focus, out var err))
                    { failure = err; break; }
                }
            }, ct);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { failure = ex.Message; }
        finally { if (ReferenceEquals(_cts, cts)) IsRolling = false; }

        // If a detach/reset (or a fresh Start) superseded this run while it was unwinding, don't publish
        // its result — Reset() already set the status/lock state, and surfacing the game or clobbering the
        // status here would fight it.
        if (!ReferenceEquals(_cts, cts)) return;

        if (met && winning != null)
        {
            KeyboardSender.BringToFront(pid.Value);   // surface the game so the user can accept the roll
            ResultText = $"✔ Found it after {tried} roll(s): {Describe(winning, winningStrPct, winningHp, mins, strPctMinTarget, hpMinTarget, totalMinTarget, trackHp)}. "
                       + "The game is in front — press y to keep this character, then name it and pick a class.";
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
    // Total amount by which a roll falls below its minimums — per-stat, STR%, the ability total (sum of
    // the six abilities), and HP; 0 means every target is met.
    private static int Shortfall(int[] v, int strPct, int hp, int[] mins, int strPctMin, int hpMin, int totalMin, bool trackHp)
    {
        int sum = 0, attrTotal = 0;
        for (int k = 0; k < v.Length; k++)
        {
            attrTotal += v[k];
            if (k < mins.Length) { int d = mins[k] - v[k]; if (d > 0) sum += d; }
        }
        if (strPctMin > 0) { int d = strPctMin - strPct; if (d > 0) sum += d; }
        if (totalMin > 0) { int d = totalMin - attrTotal; if (d > 0) sum += d; }
        if (trackHp && hpMin > 0) { int d = hpMin - hp; if (d > 0) sum += d; }
        return sum;
    }

    private static int Sum(int[] v, int hp, bool trackHp)
    {
        int sum = 0;
        foreach (var x in v) sum += x;
        if (trackHp) sum += hp;
        return sum;
    }

    // Ranks one roll above another: closer to meeting every minimum first (less shortfall), then the
    // higher overall total. STR% is deliberately excluded from the total tie-breaker — it ranges
    // 0–100, which would overwhelm the six abilities (18–108) and let a high percentile mask a
    // weak roll. STR% is already factored into the shortfall when the user set a STR% target.
    private static bool IsBetter(int[] cand, int candStrPct, int candHp, int[] best, int bestStrPct, int bestHp,
                                 int[] mins, int strPctMin, int hpMin, int totalMin, bool trackHp)
    {
        int cs = Shortfall(cand, candStrPct, candHp, mins, strPctMin, hpMin, totalMin, trackHp);
        int bs = Shortfall(best, bestStrPct, bestHp, mins, strPctMin, hpMin, totalMin, trackHp);
        if (cs != bs) return cs < bs;
        return Sum(cand, candHp, trackHp) > Sum(best, bestHp, trackHp);
    }

    // "STR 12, INT 14, …, CHA 15, STR% 0, HP 8 · total 88 (short 3)" — the shared roll summary.
    private string Describe(int[] v, int strPct, int hp, int[] mins, int strPctMin, int hpMin, int totalMin, bool trackHp)
    {
        var parts = string.Join(", ", v.Select((x, k) => $"{StatNames[k]} {x}"));
        parts += $", STR% {strPct}";
        if (trackHp) parts += $", HP {hp}";
        if (totalMin > 0) parts += $" · total {v.Sum()}";
        int sf = Shortfall(v, strPct, hp, mins, strPctMin, hpMin, totalMin, trackHp);
        return sf == 0 ? parts : $"{parts} (short {sf})";
    }

    // Runs on the UI thread (via OnUi) with immutable snapshots taken in the roll loop: the live
    // readbacks, attempt count, and the best-so-far line.
    private void PublishRoll(int[] roll, int strPct, int hp, int[] best, int bestStrPct, int bestHp, int bestAttempt, int[] mins, int strPctMin, int hpMin, int totalMin, int attempt)
    {
        if (!_isLocked) return;   // a detach/reset may have run before this queued update fired
        for (int k = 0; k < Stats.Count && k < roll.Length; k++) Stats[k].Live = roll[k];
        StrPercent.Live = strPct;
        if (_hpTracked) Hp.Live = hp;
        OnPropertyChanged(nameof(LiveTotalText));
        Attempts = attempt;
        BestText = $"{Describe(best, bestStrPct, bestHp, mins, strPctMin, hpMin, totalMin, _hpTracked)} (roll #{bestAttempt})";
    }

    private bool ReadInto(ProcessMemory mem, nuint addr)
    {
        var sig = new int[CreationScanner.SignatureCount];
        // Reject a short read OR bytes that aren't a plausible roll: the create buffer is ephemeral, so if
        // the screen has closed and the address now holds unrelated bytes the read still succeeds — the
        // in-range gate keeps that garbage out of the Live readout and the tally (mirrors Narrow()).
        if (!CreationScanner.TryReadSignature(mem, addr, sig) || !CreationScanner.InRange(sig)) return false;
        for (int k = 0; k < Stats.Count; k++) Stats[k].Live = sig[k];
        StrPercent.Live = sig[Stats.Count];
        OnPropertyChanged(nameof(LiveTotalText));

        int hp = 0;
        bool haveHp = _hpTracked && CreationScanner.TryReadHp(mem, addr, out hp) && IsPlausibleHp(hp);
        if (haveHp) Hp.Live = hp;

        // A one-shot read (lock / Read current roll) counts as a sample too; a repeat of the same static
        // roll is dropped by the tally's dedup. Runs on the UI thread, and never while the roll loop is
        // active (the commands are disabled then), so there's no race.
        var v = new int[Stats.Count];
        for (int k = 0; k < Stats.Count; k++) v[k] = sig[k];
        if (_tally.Add(v))
        {
            var sp = _strPercentTally; sp.Add(sig[Stats.Count]);
            var hpT = _hpTally;
            if (haveHp) hpT.Add(hp);
            ApplyStatsSnapshot(_tally.Snapshot(), sp.Mean, sp.Min, sp.Max, sp.Count, hpT.Mean, hpT.Min, hpT.Max, hpT.Count);
        }
        return true;
    }

    /// <summary>An HP value read from the create buffer looks real: within 1..255 (a byte). Guards the
    /// tracked-HP readback the same way <see cref="CreationScanner.InRange"/> guards the abilities.</summary>
    private static bool IsPlausibleHp(int hp) => hp >= 1 && hp <= 255;

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

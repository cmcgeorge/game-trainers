using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using MightAndMagic1Trainer.Game;
using MightAndMagic1Trainer.Memory;

namespace MightAndMagic1Trainer.ViewModels;

/// <summary>One attribute row in the roller: the value the user reads off the create
/// screen (to locate the buffer) plus the value last read back from memory.</summary>
public sealed class RollStatViewModel : ObservableObject
{
    public RollStatViewModel(string name) => Name = name;

    public string Name { get; }

    private int? _captured;
    /// <summary>The on-screen value the user types so the buffer can be located; null until
    /// entered (the box shows empty).</summary>
    public int? Captured { get => _captured; set => SetField(ref _captured, value); }

    private int? _minimum;
    /// <summary>The target floor for this stat; null or 0 means "no requirement" (the box shows
    /// empty until entered). The roller stops once every stat is at or above its own minimum.</summary>
    public int? Minimum { get => _minimum; set => SetField(ref _minimum, value is null ? null : Math.Clamp(value.Value, 0, RollScanner.MaxStatValue)); }

    private int _live;
    private bool _hasLive;

    /// <summary>The value last read from the located buffer (the current/best roll).</summary>
    public int Live
    {
        get => _live;
        set { _hasLive = true; SetField(ref _live, value); OnPropertyChanged(nameof(LiveText)); }
    }

    public string LiveText => _hasLive ? _live.ToString() : "—";

    public void ClearLive() { _hasLive = false; _live = 0; OnPropertyChanged(nameof(LiveText)); }

    private string _avgText = "—";
    /// <summary>This stat's running average and observed range over the session's rolls,
    /// e.g. "10.4  (3–18)"; "—" until any roll has been sampled.</summary>
    public string AvgText { get => _avgText; private set => SetField(ref _avgText, value); }

    /// <summary>Updates the average/range readout from a stats snapshot (UI thread).</summary>
    public void SetAverage(double avg, int min, int max) => AvgText = $"{avg:0.0}  ({min}–{max})";

    /// <summary>Clears the average/range readout when the history is reset.</summary>
    public void ClearStats() => AvgText = "—";
}

/// <summary>
/// "Roll a Hero": automates the CREATE NEW CHARACTERS re-roll. The roll lives in a
/// temporary buffer (not a roster record), so the user first captures the seven on-screen
/// numbers and the trainer signature-scans for them (<see cref="RollScanner"/>) to lock the
/// address. From then on it taps Enter to re-roll (<see cref="KeyboardSender"/>), reads the
/// new stats straight from memory, and stops once every stat meets its own minimum — exactly
/// the "keep rolling until Intellect is 18 and Might is at least 15 and …" workflow, hands-free.
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
    private int _lockStride = 1;

    public CharacterRollerViewModel(Func<ProcessMemory?> getMem, Func<int?> getPid, Action<string> setStatus)
    {
        _getMem = getMem;
        _getPid = getPid;
        _setStatus = setStatus;
        _ui = SynchronizationContext.Current ?? new SynchronizationContext();

        foreach (var name in RosterFormat.Stats)
        {
            var stat = new RollStatViewModel(name);
            stat.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(RollStatViewModel.Minimum))
                {
                    OnPropertyChanged(nameof(CriteriaText));
                    OnPropertyChanged(nameof(OddsText));
                    ClearMinimumsCommand?.RaiseCanExecuteChanged();   // null only during ctor, before this fires
                }
            };
            Stats.Add(stat);
        }
        _history = new RollHistory(Stats.Count);

        LockCommand = new RelayCommand(_ => Lock(), _ => Attached && !IsBusy && !IsRolling);
        ReadOnceCommand = new RelayCommand(_ => ReadOnce(), _ => IsLocked && Attached && !IsBusy && !IsRolling);
        ResetLockCommand = new RelayCommand(_ => ResetLock(), _ => IsLocked && !IsBusy && !IsRolling);
        StartCommand = new RelayCommand(_ => Start(), _ => IsLocked && Attached && _getPid() != null && !IsBusy && !IsRolling);
        StopCommand = new RelayCommand(_ => Stop(), _ => IsRolling);
        ClearStatsCommand = new RelayCommand(_ => ClearStats(), _ => _history.Count > 0 && !IsRolling);
        ClearMinimumsCommand = new RelayCommand(_ => ClearMinimums(), _ => Stats.Any(s => s.Minimum is > 0) && !IsRolling);
    }

    /// <summary>Session tally of every fresh roll read, for the statistics panel. Mutated only on
    /// whatever thread is currently reading rolls (the roll loop, or the UI thread for one-shot reads,
    /// which never overlap), then snapshotted to the UI. <c>volatile</c> so a reset's reassignment on
    /// the UI thread is promptly visible to the background roll loop.</summary>
    private volatile RollHistory _history;

    // --- captured / live stats --------------------------------------------------
    public ObservableCollection<RollStatViewModel> Stats { get; } = new();

    /// <summary>The seven attribute names, in record order (used to label rolls in summaries).</summary>
    public string[] StatNames => RosterFormat.Stats;

    // --- target criteria --------------------------------------------------------
    // Each stat carries its own minimum (RollStatViewModel.Minimum); the roller stops once
    // every stat is at or above it. A minimum of 0 means that stat is unconstrained.

    public string CriteriaText
    {
        get
        {
            var active = Stats.Where(s => s.Minimum > 0)         // null and 0 both mean "no requirement"
                              .Select(s => $"{s.Name} ≥ {s.Minimum!.Value}")
                              .ToArray();
            return active.Length == 0
                ? "No minimums set — the first roll will be accepted. Set a minimum on the stats you care about."
                : "Stop when " + string.Join(", ", active) + ".";
        }
    }

    /// <summary>The odds of hitting the current target on any one roll, modelled as seven
    /// independent 3d6 attributes. Recomputed whenever a minimum changes.</summary>
    public string OddsText
    {
        get
        {
            var overMax = Stats.Where(s => (s.Minimum ?? 0) > ThreeD6.Max).Select(s => s.Name).ToArray();
            if (overMax.Length > 0)
                return $"Impossible as 3d6: {string.Join(", ", overMax)} would need more than {ThreeD6.Max}, "
                     + "the most three dice can total.";

            var mins = Stats.Select(s => s.Minimum ?? 0).ToArray();
            if (!mins.Any(m => m > ThreeD6.Min))
                return "No effective minimums — every roll qualifies (1 in 1).";

            double p = ThreeD6.PMeetsAll(mins);
            if (p <= 0) return "Impossible as 3d6.";

            double expectedRolls = 1.0 / p;
            string time = Humanize(expectedRolls * PerRollSeconds);
            return $"Odds of this target as fair 3d6: about 1 in {expectedRolls:N0} rolls "
                 + $"(p = {Percent(p)}). At ~{PerRollSeconds:0.##}s per roll that's roughly {time} of rolling.";
        }
    }

    // --- session statistics (fed from the roll loop) ----------------------------
    private string _samplesText = "No rolls sampled yet.";
    public string SamplesText { get => _samplesText; private set => SetField(ref _samplesText, value); }

    private string _totalSummaryText = "";
    public string TotalSummaryText { get => _totalSummaryText; private set => SetField(ref _totalSummaryText, value); }

    private string _fairnessText = "";
    public string FairnessText { get => _fairnessText; private set => SetField(ref _fairnessText, value); }

    // Rough wall-clock cost of one roll: the post-Enter settle plus a little tap/loop overhead.
    private double PerRollSeconds => (_settleDelayMs + 30) / 1000.0;

    // --- tuning -----------------------------------------------------------------
    private int _maxAttempts = 1000;
    public int MaxAttempts { get => _maxAttempts; set => SetField(ref _maxAttempts, Math.Clamp(value, 1, 1_000_000)); }

    // Settle is the pause between {ENTER} and the next read: it must outlast the game writing the
    // fresh roll to memory, or a stale read can skip a winning roll. Kept at the proven 130ms; the
    // re-roll speed-up comes from KeyboardSender skipping the focus ceremony, not from shrinking this.
    private int _settleDelayMs = 130;
    public int SettleDelayMs { get => _settleDelayMs; set => SetField(ref _settleDelayMs, Math.Clamp(value, 0, 2000)); }

    private int _focusDelayMs = 50;
    public int FocusDelayMs { get => _focusDelayMs; set => SetField(ref _focusDelayMs, Math.Clamp(value, 0, 2000)); }

    // --- state ------------------------------------------------------------------
    private bool _isBusy;
    public bool IsBusy { get => _isBusy; private set { if (SetField(ref _isBusy, value)) RaiseAll(); } }

    private bool _isRolling;
    public bool IsRolling { get => _isRolling; private set { if (SetField(ref _isRolling, value)) RaiseAll(); } }

    private bool _isLocked;
    public bool IsLocked { get => _isLocked; private set { if (SetField(ref _isLocked, value)) { OnPropertyChanged(nameof(LockInfo)); RaiseAll(); } } }

    public string LockInfo => _isLocked
        ? $"Locked onto the roll at 0x{(ulong)_lockAddr:X} (stride {_lockStride})."
        : "Not locked. Enter the seven numbers showing on the create screen, then Lock onto roll.";

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

    /// <summary>Stops any roll and forgets the locked address (called on detach: the
    /// address belongs to a process we're no longer attached to).</summary>
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
        if (!RollScanner.InRange(captured))
        {
            _setStatus($"Enter the seven numbers from the create screen first (each between "
                     + $"{RollScanner.MinStatValue} and {RollScanner.MaxStatValue}).");
            return;
        }

        var cts = ResetCts();
        var ct = cts.Token;
        int? pid = _getPid();
        int settle = _settleDelayMs, focus = _focusDelayMs;   // snapshot UI-owned knobs for the bg thread
        IsBusy = true;
        _setStatus("Searching memory for the roll on the create screen…");

        try
        {
            var locked = await Task.Run(() =>
            {
                var matches = RollScanner.Find(mem, captured, ct);
                if (matches.Count == 0) return (RollMatch?)null;
                if (matches.Count == 1 || pid == null) return matches[0];
                return Narrow(mem, pid.Value, matches, captured.Length, settle, focus, ct);
            }, ct);

            // A detach (+ reattach) may have replaced the handle while we scanned; don't
            // publish results against a stale/disposed ProcessMemory (mirrors MainViewModel.ScanAsync).
            if (ct.IsCancellationRequested || !ReferenceEquals(mem, _getMem())) return;

            if (locked == null)
            {
                _setStatus("Couldn't find those numbers in the game's memory. Make sure you're on the "
                         + "CREATE NEW CHARACTERS screen and the seven values match exactly, then try again.");
                return;
            }

            _lockAddr = locked.Value.Address;
            _lockStride = locked.Value.Stride;
            IsLocked = true;
            ReadInto(mem, _lockAddr, _lockStride);
            _setStatus(LockInfo + " Set your target below, then Roll.");
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { _setStatus("Lock failed: " + ex.Message); }
        finally { if (ReferenceEquals(_cts, cts)) IsBusy = false; }
    }

    // Disambiguate multiple signature hits: re-roll a few times and keep candidates whose
    // seven bytes stay in a plausible stat range AND actually change (the live buffer does;
    // a coincidental static match doesn't). Runs on the scan thread.
    private RollMatch? Narrow(ProcessMemory mem, int pid, List<RollMatch> matches, int count,
                             int settleMs, int focusMs, CancellationToken ct)
    {
        var cands = new List<Cand>();
        foreach (var m in matches)
        {
            var v = new int[count];
            if (RollScanner.TryReadStats(mem, m.Address, m.Stride, count, v) && RollScanner.InRange(v))
                cands.Add(new Cand(m, v));
        }
        if (cands.Count == 0) return matches[0];

        for (int r = 0; r < MaxNarrowRolls && cands.Count > 1; r++)
        {
            ct.ThrowIfCancellationRequested();
            if (!KeyboardSender.Send(pid, "{ENTER}", settleMs, focusMs, out _)) break;

            var keep = new List<Cand>();
            foreach (var c in cands)
            {
                var v = new int[count];
                if (!RollScanner.TryReadStats(mem, c.Match.Address, c.Match.Stride, count, v)) continue;
                if (!RollScanner.InRange(v)) continue;
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
        public Cand(RollMatch m, int[] last) { Match = m; Last = last; }
        public RollMatch Match { get; }
        public int[] Last { get; set; }
        public bool ChangedEver { get; set; }
    }

    private void ReadOnce()
    {
        var mem = _getMem();
        if (mem == null || !_isLocked) return;
        if (ReadInto(mem, _lockAddr, _lockStride))
            _setStatus("Read the current roll from memory.");
        else
            _setStatus("Couldn't read the locked address — Reset lock and capture again (did the screen change?).");
    }

    private void ResetLock()
    {
        _isLocked = false;
        _lockAddr = 0;
        _lockStride = 1;
        Attempts = 0;
        BestText = "";
        ResultText = "";
        foreach (var s in Stats) s.ClearLive();
        ClearStats();   // the tally belonged to the buffer we're releasing
        OnPropertyChanged(nameof(IsLocked));
        OnPropertyChanged(nameof(LockInfo));
        RaiseAll();
    }

    /// <summary>Clears every per-stat target minimum back to blank ("no requirement").</summary>
    private void ClearMinimums()
    {
        foreach (var s in Stats) s.Minimum = null;   // setters fire PropertyChanged → Criteria/Odds/command refresh
    }

    // --- statistics -------------------------------------------------------------
    /// <summary>Empties the session tally and the statistics readouts.</summary>
    private void ClearStats()
    {
        _history = new RollHistory(Stats.Count);
        foreach (var s in Stats) s.ClearStats();
        SamplesText = "No rolls sampled yet.";
        TotalSummaryText = "";
        FairnessText = "";
        RaiseAll();
    }

    /// <summary>Pushes a stats snapshot into the readouts (UI thread).</summary>
    private void ApplyStatsSnapshot(RollStatsSnapshot s)
    {
        if (!_isLocked) return;   // a detach/reset may have run before this queued update fired
        for (int k = 0; k < Stats.Count && k < s.StatMean.Length; k++)
            Stats[k].SetAverage(s.StatMean[k], s.StatMin[k], s.StatMax[k]);

        SamplesText = $"Rolls sampled: {s.Count:N0}";
        TotalSummaryText = s.Count == 0 ? ""
            : $"Average total {s.TotalMean:0.0} (3d6 expects {s.ExpectedTotalMean:0.0}); "
            + $"range {s.TotalMin}–{s.TotalMax}; spread σ {s.TotalStdDev:0.0} (3d6 expects {s.ExpectedTotalStdDev:0.0}).";
        FairnessText = DescribeFairness(s);
        ClearStatsCommand.RaiseCanExecuteChanged();
    }

    // Plain-language verdict on whether the rolls look like fair 3d6.
    private static string DescribeFairness(RollStatsSnapshot s) => s.Fairness switch
    {
        RollFairness.NeedMoreData =>
            $"Collecting data… {s.Count} of {RollHistory.MinSamplesForVerdict} rolls needed before judging the dice.",
        RollFairness.OutOfRange =>
            "These rolls aren't plain 3d6: some stats land outside the 3–18 range three dice can make "
            + "(the game adds bonuses or uses a different method).",
        RollFairness.LikelyConstrained =>
            $"Doesn't look like fair 3d6 — the totals vary far less than dice would (σ {s.TotalStdDev:0.0} vs "
            + $"{s.ExpectedTotalStdDev:0.0} expected). The game appears to steer the total, so extreme rolls "
            + "(e.g. all 18s) likely can't happen.",
        RollFairness.LikelyBiased =>
            $"Doesn't match fair 3d6 — the average total is {s.TotalMean:0.0} versus {s.ExpectedTotalMean:0.0} "
            + $"expected ({Math.Abs(s.TotalMeanZ):0.0}σ off).",
        RollFairness.ConsistentWith3d6 =>
            $"Looks like fair 3d6: average total {s.TotalMean:0.0} (expected {s.ExpectedTotalMean:0.0}) and "
            + $"spread σ {s.TotalStdDev:0.0} (expected {s.ExpectedTotalStdDev:0.0}) both line up.",
        _ => "",
    };

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

    // Percentage down to 0.01%, then scientific for the genuinely tiny (e.g. all-18 targets),
    // where the "1 in N rolls" figure alongside it already conveys the magnitude.
    private static string Percent(double p) =>
        p >= 0.0001 ? p.ToString("0.####%") : p.ToString("0.0E+0");

    // --- the roll loop ----------------------------------------------------------
    private async void Start()
    {
        var mem = _getMem();
        int? pid = _getPid();
        if (mem == null || pid == null || !_isLocked) return;

        var cts = ResetCts();
        var ct = cts.Token;
        int count = Stats.Count;

        // Snapshot everything the background loop needs so it never reads UI-owned state
        // (the lock address, the target, the delays) across threads.
        nuint lockAddr = _lockAddr;
        int lockStride = _lockStride;
        int[] mins = Stats.Select(s => s.Minimum ?? 0).ToArray();   // per-stat floors (blank = 0 = unconstrained), in record order
        int maxAttempts = _maxAttempts;
        int settle = _settleDelayMs, focus = _focusDelayMs;

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
                int[]? best = null;       // best-so-far is owned solely by this loop thread
                int bestAttempt = 0;

                while (!ct.IsCancellationRequested && tried < maxAttempts)
                {
                    if (!RollScanner.TryReadStats(mem, lockAddr, lockStride, count, v))
                    { failure = "lost the locked address (did the create screen close?)"; break; }

                    tried++;

                    if (best == null || IsBetter(v, best, mins))
                    { best = (int[])v.Clone(); bestAttempt = tried; }

                    // Hand the UI immutable snapshots: the loop keeps overwriting `v`, and
                    // `best` is replaced (never edited) so its reference is safe to read later.
                    var rollSnap = (int[])v.Clone();
                    int[] bestSnap = best;
                    int bestAttemptSnap = bestAttempt, triedSnap = tried;
                    OnUi(() => PublishRoll(rollSnap, bestSnap, bestAttemptSnap, mins, triedSnap));

                    // Tally the roll for the statistics panel; a duplicate (stale read) is dropped,
                    // so only post a fresh snapshot when it actually changed the numbers.
                    if (_history.Add(v))
                    {
                        var statsSnap = _history.Snapshot();
                        OnUi(() => ApplyStatsSnapshot(statsSnap));
                    }

                    if (Shortfall(v, mins) == 0)
                    { winning = rollSnap; met = true; break; }
                    if (ct.IsCancellationRequested) break;
                    if (!KeyboardSender.Send(pid.Value, "{ENTER}", settle, focus, out var err))
                    { failure = err; break; }
                }
            }, ct);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { failure = ex.Message; }
        finally { if (ReferenceEquals(_cts, cts)) IsRolling = false; }

        if (met && winning != null)
        {
            KeyboardSender.BringToFront(pid.Value);   // surface the game so the user can pick a class
            ResultText = $"✔ Found it after {tried} roll(s): {Describe(winning, mins)}. "
                       + "The game is in front — press a class number to keep this character.";
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
    // Total amount by which a roll falls below its per-stat minimums; 0 means every stat is met.
    private static int Shortfall(int[] v, int[] mins)
    {
        int sum = 0;
        for (int k = 0; k < v.Length && k < mins.Length; k++) { int d = mins[k] - v[k]; if (d > 0) sum += d; }
        return sum;
    }

    private static int Sum(int[] v)
    {
        int sum = 0;
        foreach (var x in v) sum += x;
        return sum;
    }

    // Ranks one roll above another: closer to meeting every minimum first (less shortfall),
    // then the higher overall total.
    private static bool IsBetter(int[] cand, int[] best, int[] mins)
    {
        int cs = Shortfall(cand, mins), bs = Shortfall(best, mins);
        if (cs != bs) return cs < bs;
        return Sum(cand) > Sum(best);
    }

    // "Might 15, Intellect 18, Personality 10, … (short 3)" — the shared roll summary.
    private string Describe(int[] v, int[] mins)
    {
        var parts = string.Join(", ", v.Select((x, k) => $"{StatNames[k]} {x}"));
        int sf = Shortfall(v, mins);
        return sf == 0 ? parts : $"{parts} (short {sf})";
    }

    // Runs on the UI thread (via OnUi) with immutable snapshots taken in the roll loop:
    // the live readbacks, attempt count, and the best-so-far line.
    private void PublishRoll(int[] roll, int[] best, int bestAttempt, int[] mins, int attempt)
    {
        if (!_isLocked) return;   // a detach/reset may have run before this queued update fired
        for (int k = 0; k < Stats.Count && k < roll.Length; k++) Stats[k].Live = roll[k];
        Attempts = attempt;
        BestText = $"{Describe(best, mins)} (roll #{bestAttempt})";
    }

    private bool ReadInto(ProcessMemory mem, nuint addr, int stride)
    {
        var v = new int[Stats.Count];
        if (!RollScanner.TryReadStats(mem, addr, stride, Stats.Count, v)) return false;
        for (int k = 0; k < Stats.Count; k++) Stats[k].Live = v[k];

        // A one-shot read (lock / Read current roll) counts as a sample too; a repeat of the
        // same static roll is dropped by the history's dedup. Runs on the UI thread, and never
        // while the roll loop is active (the commands are disabled then), so there's no race.
        if (_history.Add(v)) ApplyStatsSnapshot(_history.Snapshot());
        return true;
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

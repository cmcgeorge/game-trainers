using System.Threading;
using System.Threading.Tasks;
using MightAndMagic1Trainer.Memory;
using MightAndMagic1Trainer.Mvvm;

namespace MightAndMagic1Trainer.ViewModels;

/// <summary>
/// Auto-fight: while combat is detected, repeatedly focuses the game window and replays a
/// key sequence (default "attack, attack, block" + a few SPACEs to clear the messages),
/// stopping automatically when combat ends. Combat is detected via the game's own combat
/// gate (<see cref="DataSegment.InCombat"/> = [0xC5DC + activeCharIndex] &amp; 2).
///
/// NOTE: the detection is REVERSE-ENGINEERED from the game's "non-combat-only" gate but not
/// yet confirmed live — watch the game-state readout to verify it tracks combat before
/// trusting auto-fight unattended.
/// </summary>
public sealed class AutoCombatViewModel : ObservableObject
{
    private const int KeyDelayMs = 40;
    private const int FocusDelayMs = 120;
    private const int LoopDelayMs = 450;

    private readonly Func<int?> _targetPid;
    private readonly Func<DataSegment?> _getDataSeg;
    private readonly Action<string> _setStatus;

    private CancellationTokenSource? _cts;

    public AutoCombatViewModel(Func<int?> targetPid, Func<DataSegment?> getDataSeg, Action<string> setStatus)
    {
        _targetPid = targetPid;
        _getDataSeg = getDataSeg;
        _setStatus = setStatus;
    }

    private string _keys = "a a b {SPACE} {SPACE} {SPACE}";
    /// <summary>The sequence sent each combat round. Whitespace is ignored; use {SPACE} for a space.</summary>
    public string Keys
    {
        get => _keys;
        set { if (SetField(ref _keys, value)) OnPropertyChanged(nameof(KeysError)); }
    }

    /// <summary>Null when the sequence parses; otherwise why it doesn't.</summary>
    public string? KeysError => KeyboardSender.Validate(_keys);

    private bool _enabled;
    public bool Enabled
    {
        get => _enabled;
        set { if (SetField(ref _enabled, value)) { if (value) Start(); else Stop(); } }
    }

    private string _activity = "Off.";
    public string Activity { get => _activity; private set => SetField(ref _activity, value); }

    private void Start()
    {
        if (KeyboardSender.Validate(_keys) is { } err) { _setStatus($"Auto-fight not started — {err}"); Enabled = false; return; }
        if (_targetPid() == null) { _setStatus("Auto-fight: attach to the game first."); Enabled = false; return; }
        if (_getDataSeg() == null) { _setStatus("Auto-fight: data segment not located yet (attach + load a game past the title)."); Enabled = false; return; }

        // Cancel any previous loop but do NOT dispose its source: the loop observes the token's
        // WaitHandle (in Sleep), and disposing the source out from under it would throw. The
        // old source is unreferenced once its loop exits and is then GC-collected.
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        Activity = "Watching for combat…";
        _setStatus("Auto-fight ON — it will replay your keys while combat is detected.");
        _ = Task.Run(() => Loop(ct), ct);
    }

    /// <summary>Stops the auto-fight loop (safe to call when already off).</summary>
    public void Stop()
    {
        _cts?.Cancel();   // not disposed — see Start(); the loop exits on the next Sleep return
        _cts = null;
        if (_enabled) SetField(ref _enabled, false, nameof(Enabled));
        Activity = "Off.";
    }

    private void Loop(CancellationToken ct)
    {
        int rounds = 0;
        while (!ct.IsCancellationRequested)
        {
            int? pid = _targetPid();
            var ds = _getDataSeg();
            string keys = Volatile.Read(ref _keys);   // snapshot: the UI thread may edit Keys mid-loop
            if (pid == null || ds == null) { Report("Waiting (not attached)…"); Sleep(ct); continue; }

            bool inCombat = ds.InCombat;
            if (inCombat)
            {
                if (KeyboardSender.Send(pid.Value, keys, KeyDelayMs, FocusDelayMs, out string error))
                    Report($"Fighting — sent round {++rounds}.");
                else
                    Report("Combat detected, but the keys were blocked: " + error);
            }
            else
            {
                if (rounds > 0) { Report($"Combat ended after {rounds} round(s). Watching…"); rounds = 0; }
                else Report("Watching for combat…");
            }
            Sleep(ct);
        }
    }

    // Sleeps, but returns at once when the token is cancelled — so Stop() is responsive.
    // (Thread.Sleep would ignore cancellation and stall up to LoopDelayMs.)
    private static void Sleep(CancellationToken ct) => ct.WaitHandle.WaitOne(LoopDelayMs);

    // Activity is shown in the UI; marshal the update onto the UI thread.
    private void Report(string text)
    {
        var disp = System.Windows.Application.Current?.Dispatcher;
        if (disp != null && !disp.CheckAccess()) disp.InvokeAsync(() => Activity = text);
        else Activity = text;
    }
}

using System.Collections.ObjectModel;
using MightAndMagic1Trainer.Game;
using MightAndMagic1Trainer.Memory;
using MightAndMagic1Trainer.Mvvm;

namespace MightAndMagic1Trainer.ViewModels;

/// <summary>One die's prediction row: the next result and the next several results.</summary>
public sealed class RollPrediction : ObservableObject
{
    public RollPrediction(string die, int n) { Die = die; N = n; }

    public string Die { get; }
    public int N { get; }

    private int _next;
    public int Next { get => _next; set => SetField(ref _next, value); }

    private string _sequence = "";
    public string Sequence { get => _sequence; set => SetField(ref _sequence, value); }
}

/// <summary>
/// Live roll predictor: reads the game's RNG (LFSR) state from the data segment and
/// predicts the upcoming results of <c>rand(n)</c> for common dice — using the byte-exact
/// reimplementation in <see cref="Lfsr"/>. Each row assumes that die is the <em>next</em>
/// random draw the game makes; as the game rolls, the state advances and the predictions
/// update on the timer.
/// </summary>
public sealed class RollPredictorViewModel : ObservableObject
{
    private const int PreviewCount = 8;
    private static readonly (string Die, int N)[] Dice =
    {
        ("d2", 2), ("d3", 3), ("d4", 4), ("d6", 6), ("d8", 8),
        ("d10", 10), ("d12", 12), ("d20", 20), ("d100", 100),
    };

    private readonly Func<DataSegment?> _getDataSeg;

    public RollPredictorViewModel(Func<DataSegment?> getDataSeg)
    {
        System.Diagnostics.Debug.Assert(Lfsr.SelfTest(), "LFSR reimplementation regressed against its reference vectors.");
        _getDataSeg = getDataSeg;
        foreach (var (die, n) in Dice) Predictions.Add(new RollPrediction(die, n));
    }

    public ObservableCollection<RollPrediction> Predictions { get; } = new();

    private bool _hasState;
    public bool HasState { get => _hasState; private set => SetField(ref _hasState, value); }

    private string _stateText = "Attach to the game (past the title) to read the RNG state.";
    public string StateText { get => _stateText; private set => SetField(ref _stateText, value); }

    private uint? _lastState;
    private int _lastRetry;

    /// <summary>Polled from the main timer: re-read the live RNG state and refresh predictions.</summary>
    public void Tick()
    {
        var ds = _getDataSeg();
        uint? state = ds?.ReadRngState();
        if (ds == null || state == null)
        {
            if (HasState) { HasState = false; StateText = "RNG state unavailable (attach + load the game)."; }
            _lastState = null;
            _lastRetry = 0;   // reset the whole cache so a re-attach always refreshes
            return;
        }

        // The retry-shift count lives at DS:0x3BD3 and the game keeps it at 4. It's read live for
        // fidelity, but a transient/pre-init read of 0 (or garbage) would send Lfsr.Rand down the
        // 65,536-shift wrap path on every rejection — on the UI thread — so clamp the untrusted byte.
        int retry = ds.RngRetry;
        if (retry < 1 || retry > 8) retry = 4;

        // Predictions are a pure function of (state, retry); when neither changed since last tick
        // the rolls are identical, so skip the 9-die recompute, string builds, and binding churn.
        if (HasState && state == _lastState && retry == _lastRetry) return;
        _lastState = state;
        _lastRetry = retry;

        HasState = true;
        StateText = $"Live RNG (LFSR) state: 0x{state.Value:X8}   ·   retry shift = {retry}";

        foreach (var row in Predictions)
        {
            // Each row predicts from the SAME current state — i.e. "if the next draw is rand(N)".
            var seq = Lfsr.Predict(state.Value, row.N, PreviewCount, retry);
            row.Next = seq[0];
            row.Sequence = string.Join("  ", seq);
        }
    }
}

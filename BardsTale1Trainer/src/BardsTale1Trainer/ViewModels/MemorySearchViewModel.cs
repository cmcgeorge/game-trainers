using System.Collections.ObjectModel;
using System.Globalization;
using BardsTale1Trainer.Memory;

namespace BardsTale1Trainer.ViewModels;

/// <summary>One row in the search results: an address and its last-read value.</summary>
public sealed class MemResultViewModel : ObservableObject
{
    public nuint Address { get; }
    private long _value;

    public MemResultViewModel(nuint address, long value)
    {
        Address = address;
        _value = value;
    }

    public long Value { get => _value; set { if (SetField(ref _value, value)) { OnPropertyChanged(nameof(ValueText)); OnPropertyChanged(nameof(ValueHex)); } } }
    public string AddressHex => $"0x{(ulong)Address:X}";
    public string ValueText => _value.ToString(CultureInfo.InvariantCulture);
    public string ValueHex => $"0x{_value:X}";
}

/// <summary>
/// Drives a <see cref="MemorySearcher"/>: a small Cheat-Engine-style panel for
/// finding and editing values the roster format doesn't cover — most notably the
/// party's map position (North/East) and facing, which live in game state, not the
/// character records. Typical flow for an unseen value: First scan = Unknown, then
/// step in-game and narrow by Increased/Decreased until one address remains.
/// </summary>
public sealed class MemorySearchViewModel : ObservableObject
{
    private const int DisplayCap = 300;

    private readonly Func<ProcessMemory?> _getMem;
    private readonly Action<string> _setStatus;
    private MemorySearcher? _searcher;
    private CancellationTokenSource? _cts;

    public MemorySearchViewModel(Func<ProcessMemory?> getMem, Action<string> setStatus)
    {
        _getMem = getMem;
        _setStatus = setStatus;

        FirstScanExactCommand = new RelayCommand(() => RunScan((s, ct) => s.FirstScanExact(ParseValue(ValueText), ct)),
            () => CanScan && TryParseValue(ValueText, out _));
        FirstScanUnknownCommand = new RelayCommand(() => RunScan((s, ct) => s.FirstScanUnknown(ct)), () => CanScan);
        NextExactCommand = new RelayCommand(() => Narrow(ScanCompare.Exact),
            () => HasResults && TryParseValue(ValueText, out _));
        NextIncreasedCommand = new RelayCommand(() => Narrow(ScanCompare.Increased), () => HasResults);
        NextDecreasedCommand = new RelayCommand(() => Narrow(ScanCompare.Decreased), () => HasResults);
        NextChangedCommand = new RelayCommand(() => Narrow(ScanCompare.Changed), () => HasResults);
        NextUnchangedCommand = new RelayCommand(() => Narrow(ScanCompare.Unchanged), () => HasResults);
        RefreshValuesCommand = new RelayCommand(() => RunScan((s, ct) => s.RefreshValues(ct)), () => HasResults);
        ResetCommand = new RelayCommand(Reset, () => _searcher != null);
        WriteSelectedCommand = new RelayCommand(WriteSelected,
            () => SelectedResult != null && TryParseValue(NewValueText, out _) && _getMem() != null);
        PokeCommand = new RelayCommand(Poke,
            () => TryParseAddress(PokeAddressText, out _) && TryParseValue(PokeValueText, out _) && _getMem() != null);
    }

    // --- options ----------------------------------------------------------------
    public Array Widths => Enum.GetValues(typeof(ScanWidth));

    private ScanWidth _width = ScanWidth.Byte;
    public ScanWidth Width
    {
        get => _width;
        set
        {
            if (SetField(ref _width, value))
            {
                // A searcher is bound to one width; changing it invalidates results.
                if (_searcher != null && _searcher.Width != value) Reset();
                RaiseCommands();
            }
        }
    }

    private string _valueText = "";
    public string ValueText { get => _valueText; set { if (SetField(ref _valueText, value)) RaiseCommands(); } }

    // --- results ----------------------------------------------------------------
    public ObservableCollection<MemResultViewModel> Results { get; } = new();

    private MemResultViewModel? _selectedResult;
    public MemResultViewModel? SelectedResult
    {
        get => _selectedResult;
        set
        {
            if (SetField(ref _selectedResult, value))
            {
                if (value != null) NewValueText = value.Value.ToString(CultureInfo.InvariantCulture);
                RaiseCommands();
            }
        }
    }

    private string _newValueText = "";
    public string NewValueText { get => _newValueText; set { if (SetField(ref _newValueText, value)) RaiseCommands(); } }

    private string _resultSummary = "No scan yet. Enter a value and First-scan, or First-scan Unknown.";
    public string ResultSummary { get => _resultSummary; private set => SetField(ref _resultSummary, value); }

    private bool _isScanning;
    public bool IsScanning { get => _isScanning; private set { if (SetField(ref _isScanning, value)) RaiseCommands(); } }

    public bool HasResults => _searcher?.HasMatches == true && !_isScanning;
    private bool CanScan => _getMem() != null && !_isScanning;

    // --- manual poke ------------------------------------------------------------
    private string _pokeAddressText = "";
    public string PokeAddressText { get => _pokeAddressText; set { if (SetField(ref _pokeAddressText, value)) RaiseCommands(); } }

    private string _pokeValueText = "";
    public string PokeValueText { get => _pokeValueText; set { if (SetField(ref _pokeValueText, value)) RaiseCommands(); } }

    // --- commands ---------------------------------------------------------------
    public RelayCommand FirstScanExactCommand { get; }
    public RelayCommand FirstScanUnknownCommand { get; }
    public RelayCommand NextExactCommand { get; }
    public RelayCommand NextIncreasedCommand { get; }
    public RelayCommand NextDecreasedCommand { get; }
    public RelayCommand NextChangedCommand { get; }
    public RelayCommand NextUnchangedCommand { get; }
    public RelayCommand RefreshValuesCommand { get; }
    public RelayCommand ResetCommand { get; }
    public RelayCommand WriteSelectedCommand { get; }
    public RelayCommand PokeCommand { get; }

    /// <summary>Drops the in-progress scan/results (called on detach as well as Reset).</summary>
    public void Reset()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        _searcher = null;
        IsScanning = false;
        Results.Clear();
        SelectedResult = null;
        ResultSummary = "No scan yet. Enter a value and First-scan, or First-scan Unknown.";
        RaiseCommands();
    }

    private async void RunScan(Action<MemorySearcher, CancellationToken> op)
    {
        var mem = _getMem();
        if (mem == null) { _setStatus("Attach to the game first."); return; }
        if (_searcher == null || _searcher.Width != _width)
            _searcher = new MemorySearcher(mem, _width);

        // Cancel and dispose any prior scan before starting a new one. Capturing the
        // CTS + searcher locally means a later Reset/Detach can't leave this run
        // mutating disposed state.
        var oldCts = _cts;
        var myCts = new CancellationTokenSource();
        _cts = myCts;
        var ct = myCts.Token;
        oldCts?.Cancel();
        oldCts?.Dispose();
        var searcher = _searcher;

        IsScanning = true;
        _setStatus("Scanning memory…");
        try
        {
            await System.Threading.Tasks.Task.Run(() => op(searcher, ct), ct);
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer scan, or a Reset/Detach. Only clear the busy flag
            // if we're still the current scan; otherwise the newer run owns that state
            // and clearing it here would re-enable the commands mid-scan.
            if (ReferenceEquals(myCts, _cts)) IsScanning = false;
            return;
        }
        catch (Exception ex)
        {
            if (ReferenceEquals(myCts, _cts)) { IsScanning = false; _setStatus("Scan error: " + ex.Message); }
            return;
        }

        // A detach (or a newer scan) may have happened while we were on the pool
        // thread; the ProcessMemory we scanned could now be disposed/replaced. Only
        // publish if we're still the live scan. (ScanAsync uses the same kind of
        // guard for the roster scan.)
        if (ct.IsCancellationRequested || !ReferenceEquals(myCts, _cts) || !ReferenceEquals(searcher, _searcher))
            return;

        IsScanning = false;
        PublishResults();
    }

    private void Narrow(ScanCompare compare)
    {
        long target = TryParseValue(ValueText, out var v) ? v : 0;
        RunScan((s, ct) => s.NextScan(compare, target, ct));
    }

    private void PublishResults()
    {
        Results.Clear();
        SelectedResult = null;
        if (_searcher == null) return;

        int total = _searcher.MatchCount;
        if (total < 0)
        {
            // Unknown baseline captured; candidates aren't materialised until the
            // first relative narrowing (Increased/Decreased/Changed/Unchanged).
            ResultSummary = "Baseline captured. Now take a step / turn in-game, then click "
                + "Decreased, Increased, or Changed to narrow.";
            _setStatus("Memory search: " + ResultSummary);
            RaiseCommands();
            return;
        }

        foreach (var m in _searcher.Take(DisplayCap))
            Results.Add(new MemResultViewModel(m.Address, m.Value));

        string shown = total > DisplayCap ? $"showing first {DisplayCap} of {total:N0}" : $"{total:N0} match(es)";
        string trunc = _searcher.Truncated ? "  ⚠ scan hit the cap — narrow with a more specific value or step again" : "";
        ResultSummary = total == 0
            ? "No matches. Reset and try again (wrong width or value?)."
            : $"{shown}.{trunc}";
        _setStatus($"Memory search: {ResultSummary}");
        RaiseCommands();
    }

    private void WriteSelected()
    {
        if (_searcher == null || SelectedResult == null) return;
        if (!TryParseValue(NewValueText, out long v)) return;
        if (_searcher.WriteValue(SelectedResult.Address, v))
        {
            SelectedResult.Value = v;
            _setStatus($"Wrote {v} to {SelectedResult.AddressHex}.");
        }
        else _setStatus($"Write to {SelectedResult.AddressHex} failed.");
    }

    private void Poke()
    {
        var mem = _getMem();
        if (mem == null) return;
        if (!TryParseAddress(PokeAddressText, out nuint addr) || !TryParseValue(PokeValueText, out long v)) return;
        var searcher = _searcher ?? new MemorySearcher(mem, _width);
        if (searcher.WriteValue(addr, v)) _setStatus($"Poked {v} ({_width}) to 0x{(ulong)addr:X}.");
        else _setStatus($"Poke to 0x{(ulong)addr:X} failed.");
    }

    // --- parsing helpers --------------------------------------------------------
    private long ParseValue(string s) => TryParseValue(s, out var v) ? v : 0;

    private static bool TryParseValue(string? s, out long value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(s)) return false;
        s = s.Trim();
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return long.TryParse(s.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
        return long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryParseAddress(string? s, out nuint addr)
    {
        addr = 0;
        if (string.IsNullOrWhiteSpace(s)) return false;
        s = s.Trim();
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) s = s[2..];
        if (ulong.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var u))
        { addr = (nuint)u; return true; }
        return false;
    }

    private void RaiseCommands()
    {
        OnPropertyChanged(nameof(HasResults));
        FirstScanExactCommand.RaiseCanExecuteChanged();
        FirstScanUnknownCommand.RaiseCanExecuteChanged();
        NextExactCommand.RaiseCanExecuteChanged();
        NextIncreasedCommand.RaiseCanExecuteChanged();
        NextDecreasedCommand.RaiseCanExecuteChanged();
        NextChangedCommand.RaiseCanExecuteChanged();
        NextUnchangedCommand.RaiseCanExecuteChanged();
        RefreshValuesCommand.RaiseCanExecuteChanged();
        ResetCommand.RaiseCanExecuteChanged();
        WriteSelectedCommand.RaiseCanExecuteChanged();
        PokeCommand.RaiseCanExecuteChanged();
    }
}

using System.Collections.ObjectModel;
using System.Windows.Input;
using PoolOfRadianceTrainer.Memory;
using PoolOfRadianceTrainer.Mvvm;

namespace PoolOfRadianceTrainer.ViewModels;

/// <summary>One row in the memory-search results grid.</summary>
public sealed class ScanRowViewModel : ObservableObject
{
    public nuint Address { get; }
    public string AddressHex => $"0x{(ulong)Address:X}";
    private long _value;
    public long Value { get => _value; set => SetProperty(ref _value, value); }

    public ScanRowViewModel(ScanResult r) { Address = r.Address; _value = r.Value; }
}

/// <summary>
/// A Cheat-Engine-style memory scanner tab, for values the character record doesn't hold —
/// the party's map X/Y and facing, the in-combat clock, the encounter counters. Scans run on a
/// background task (cancellable) so the whole-process walk never freezes the UI.
/// </summary>
public sealed class MemorySearchViewModel : ObservableObject
{
    private MemorySearcher? _searcher;
    private CancellationTokenSource? _cts;

    public ObservableCollection<ScanRowViewModel> Results { get; } = new();
    public Array Widths => Enum.GetValues(typeof(ScanWidth));

    private ScanWidth _width = ScanWidth.Int16;
    public ScanWidth Width { get => _width; set => SetProperty(ref _width, value); }

    private bool _isScanning;
    public bool IsScanning { get => _isScanning; private set { if (SetProperty(ref _isScanning, value)) RaiseCommands(); } }

    private string _valueText = "";
    public string ValueText { get => _valueText; set => SetProperty(ref _valueText, value); }

    private string _pokeAddress = "";
    public string PokeAddress { get => _pokeAddress; set => SetProperty(ref _pokeAddress, value); }

    private string _pokeValue = "";
    public string PokeValue { get => _pokeValue; set => SetProperty(ref _pokeValue, value); }

    private string _status = "Attach and first-scan to begin.";
    public string Status { get => _status; set => SetProperty(ref _status, value); }

    private ScanRowViewModel? _selected;
    public ScanRowViewModel? Selected { get => _selected; set => SetProperty(ref _selected, value); }

    public ICommand FirstScanValueCommand { get; }
    public ICommand FirstScanUnknownCommand { get; }
    public ICommand NextEqualCommand { get; }
    public ICommand NextIncreasedCommand { get; }
    public ICommand NextDecreasedCommand { get; }
    public ICommand NextChangedCommand { get; }
    public ICommand NextUnchangedCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand WriteSelectedCommand { get; }
    public ICommand PokeCommand { get; }
    public ICommand ResetCommand { get; }

    public MemorySearchViewModel()
    {
        FirstScanValueCommand = new RelayCommand(_ => FirstScan(false), _ => CanScan());
        FirstScanUnknownCommand = new RelayCommand(_ => FirstScan(true), _ => CanScan());
        NextEqualCommand = new RelayCommand(_ => Next(ScanCompare.Equal, requireValue: true), _ => CanNext());
        NextIncreasedCommand = new RelayCommand(_ => Next(ScanCompare.Increased), _ => CanNext());
        NextDecreasedCommand = new RelayCommand(_ => Next(ScanCompare.Decreased), _ => CanNext());
        NextChangedCommand = new RelayCommand(_ => Next(ScanCompare.Changed), _ => CanNext());
        NextUnchangedCommand = new RelayCommand(_ => Next(ScanCompare.Unchanged), _ => CanNext());
        CancelCommand = new RelayCommand(_ => _cts?.Cancel(), _ => IsScanning);
        WriteSelectedCommand = new RelayCommand(_ => WriteSelected());
        PokeCommand = new RelayCommand(_ => Poke());
        ResetCommand = new RelayCommand(_ => Reset());
    }

    public void Attach(ProcessMemory mem) { _searcher = new MemorySearcher(mem); Reset(); }
    public void Detach() { _cts?.Cancel(); _searcher = null; Reset(); Status = "Detached — candidates cleared."; }

    private bool CanScan() => _searcher != null && !IsScanning;
    private bool CanNext() => _searcher != null && !IsScanning && _searcher.Count > 0;

    private long? ParseValue()
    {
        var s = ValueText?.Trim();
        if (string.IsNullOrEmpty(s)) return null;
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
            long.TryParse(s[2..], System.Globalization.NumberStyles.HexNumber, null, out var h)) return h;
        return long.TryParse(s, out var v) ? v : null;
    }

    private void FirstScan(bool unknown)
    {
        if (_searcher == null) { Status = "Not attached."; return; }
        var width = Width;
        if (unknown)
        {
            RunScan((s, ct) => { s.FirstScanUnknown(width, ct); return $"First scan: {s.Count:N0} candidates."; });
        }
        else
        {
            var v = ParseValue();
            if (v == null) { Status = "Enter a value first."; return; }
            RunScan((s, ct) => { s.FirstScanValue(width, v.Value, ct); return $"First scan: {s.Count:N0} candidates."; });
        }
    }

    private void Next(ScanCompare cmp, bool requireValue = false)
    {
        if (_searcher == null) { Status = "Not attached."; return; }
        if (_searcher.Count == 0) { Status = "No candidates — first-scan first."; return; }
        long? value = requireValue ? ParseValue() : null;
        if (requireValue && value == null) { Status = "Enter a value to match."; return; }
        RunScan((s, ct) => { s.NextScan(cmp, value, ct); return $"{cmp}: {s.Count:N0} candidates remain."; });
    }

    /// <summary>Runs a scan on a background task, then syncs results back on the UI thread.</summary>
    private async void RunScan(Func<MemorySearcher, CancellationToken, string> work)
    {
        var searcher = _searcher;
        if (IsScanning || searcher == null) return;
        IsScanning = true;
        Status = "Scanning…";
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        try
        {
            string result = await Task.Run(() => work(searcher, ct), ct);
            Sync(result);
        }
        catch (OperationCanceledException) { Sync("Scan cancelled."); }
        catch (Exception ex) { Status = "Scan failed: " + ex.Message; }
        finally { _cts?.Dispose(); _cts = null; IsScanning = false; }
    }

    private void WriteSelected()
    {
        if (_searcher == null || Selected == null) { Status = "Select a candidate first."; return; }
        var v = ParseValue();
        if (v == null) { Status = "Enter a value to write."; return; }
        Status = _searcher.Write(Selected.Address, v.Value) ? $"Wrote {v} to {Selected.AddressHex}." : "Write failed.";
    }

    private void Poke()
    {
        if (_searcher == null) { Status = "Not attached."; return; }
        var a = PokeAddress?.Trim();
        if (string.IsNullOrEmpty(a)) { Status = "Enter an address."; return; }
        if (a.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) a = a[2..];
        if (!ulong.TryParse(a, System.Globalization.NumberStyles.HexNumber, null, out var addr))
        { Status = "Address must be hex."; return; }
        if (!long.TryParse(PokeValue?.Trim(), out var val)) { Status = "Value must be a number."; return; }
        Status = _searcher.Write((nuint)addr, val) ? $"Poked {val} to 0x{addr:X}." : "Poke failed.";
    }

    /// <summary>Poll-tick refresh of the displayed values only — never touches the scan baseline.</summary>
    public void RefreshValues()
    {
        if (_searcher == null || IsScanning) return;
        foreach (var row in Results)
        {
            var v = _searcher.ReadLive(row.Address);
            if (v.HasValue) row.Value = v.Value;
        }
    }

    private void Sync(string status)
    {
        Results.Clear();
        // Cap the visible rows; the underlying candidate list can still narrow further.
        int shown = _searcher == null ? 0 : Math.Min(_searcher.Count, 500);
        for (int i = 0; i < shown; i++) Results.Add(new ScanRowViewModel(_searcher!.Results[i]));
        Status = status + (_searcher != null && _searcher.Count > shown ? $" (showing first {shown})" : "");
    }

    private void Reset()
    {
        _searcher?.Reset();
        Results.Clear();
        Status = _searcher == null ? "Attach to begin." : "Ready — first-scan a value or Unknown.";
    }

    private void RaiseCommands()
    {
        (FirstScanValueCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (FirstScanUnknownCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (NextEqualCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (NextIncreasedCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (NextDecreasedCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (NextChangedCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (NextUnchangedCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (CancelCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }
}

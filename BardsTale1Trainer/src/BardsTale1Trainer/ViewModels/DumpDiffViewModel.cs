using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using BardsTale1Trainer.Memory;

namespace BardsTale1Trainer.ViewModels;

/// <summary>One changed run in the diff grid.</summary>
public sealed class DiffRunViewModel
{
    private readonly DumpDiffRun _run;

    public DiffRunViewModel(DumpDiffRun run) => _run = run;

    public string AddressHex => $"0x{_run.Address:X}";
    public int Length => _run.Length;
    public string OldHex => Format(_run.OldBytes);
    public string NewHex => Format(_run.NewBytes);

    private string Format(byte[] b)
    {
        var hex = string.Join(" ", b.Select(x => $"{x:X2}"));
        // The preview holds only the first changed bytes; mark longer runs as elided.
        return _run.Length > b.Length ? hex + " …" : hex;
    }
}

/// <summary>
/// "Compare two dumps": diffs two .bin files written by the Dump tab (each needs its .csv
/// region index beside it) and lists where the bytes changed, by live process address —
/// the reverse-engineering loop in one click instead of an external hex-diff. Pure file
/// work; it doesn't need (or touch) an attached game.
/// </summary>
public sealed class DumpDiffViewModel : ObservableObject
{
    /// <summary>Stop after this many runs: a diff bigger than this means "too much changed
    /// between dumps to be useful" and comparing further would just burn time.</summary>
    private const int MaxRuns = 2000;

    private readonly Action<string> _setStatus;
    private CancellationTokenSource? _cts;

    public DumpDiffViewModel(Action<string> setStatus)
    {
        _setStatus = setStatus;
        CompareCommand = new RelayCommand(Compare, () => !IsComparing && HasBothFiles);
        CancelCommand = new RelayCommand(() => _cts?.Cancel(), () => IsComparing);
    }

    private string _oldPath = "";
    /// <summary>The "before" dump (.bin; its .csv index must sit beside it).</summary>
    public string OldPath
    {
        get => _oldPath;
        set { if (SetField(ref _oldPath, value)) RaiseCommands(); }
    }

    private string _newPath = "";
    /// <summary>The "after" dump.</summary>
    public string NewPath
    {
        get => _newPath;
        set { if (SetField(ref _newPath, value)) RaiseCommands(); }
    }

    private bool HasBothFiles =>
        !string.IsNullOrWhiteSpace(_oldPath) && !string.IsNullOrWhiteSpace(_newPath);

    private bool _isComparing;
    public bool IsComparing
    {
        get => _isComparing;
        private set { if (SetField(ref _isComparing, value)) RaiseCommands(); }
    }

    private double _progress;
    public double Progress { get => _progress; private set => SetField(ref _progress, value); }

    private string _summary = "Pick the two dump files (the older first), then Compare.";
    public string Summary { get => _summary; private set => SetField(ref _summary, value); }

    public ObservableCollection<DiffRunViewModel> Results { get; } = new();

    public RelayCommand CompareCommand { get; }
    public RelayCommand CancelCommand { get; }

    private async void Compare()
    {
        string oldBin = _oldPath, newBin = _newPath;
        string oldCsv = MemoryDumper.IndexPathFor(oldBin), newCsv = MemoryDumper.IndexPathFor(newBin);
        foreach (var f in new[] { oldBin, newBin, oldCsv, newCsv })
        {
            if (!File.Exists(f))
            {
                Summary = $"Missing file: {f}" + (f.EndsWith(".csv") ? " (the dump's region index must sit beside it)" : "");
                return;
            }
        }

        // Capture the CTS locally so a later run (or a cancel) can't have its state
        // clobbered by this one after the await — same supersession pattern as
        // PairSearchViewModel and the roster scan.
        _cts?.Cancel();
        _cts?.Dispose();
        var myCts = new CancellationTokenSource();
        _cts = myCts;
        var ct = myCts.Token;
        IsComparing = true;
        Progress = 0;
        Results.Clear();
        Summary = "Comparing…";
        _setStatus("Comparing memory dumps…");

        var progress = new Progress<double>(v => { if (!ct.IsCancellationRequested) Progress = v; });
        try
        {
            var result = await Task.Run(() =>
            {
                var oldIndex = DumpComparer.ReadIndex(oldCsv);
                var newIndex = DumpComparer.ReadIndex(newCsv);
                return DumpComparer.Compare(oldBin, oldIndex, newBin, newIndex, MaxRuns, progress, ct);
            }, ct);

            if (ct.IsCancellationRequested || !ReferenceEquals(myCts, _cts)) return;

            Progress = 1;
            foreach (var run in result.Runs) Results.Add(new DiffRunViewModel(run));
            string only = result.BytesOnlyInOne > 0
                ? $" {MemoryDumper.FormatBytes(result.BytesOnlyInOne)} existed in only one dump and were not compared."
                : "";
            string unreadable = result.BytesUnreadable > 0
                ? $" ⚠ {MemoryDumper.FormatBytes(result.BytesUnreadable)} were unreadable (zero-filled) when dumped — changes falling there may be phantoms."
                : "";
            string trunc = result.Truncated
                ? $" ⚠ Stopped at {MaxRuns:N0} runs — too much changed between the dumps for a useful diff; re-dump with less happening in between."
                : "";
            Summary = $"{result.Runs.Count:N0} changed run(s), {MemoryDumper.FormatBytes(result.BytesChanged)} changed of "
                + $"{MemoryDumper.FormatBytes(result.BytesCompared)} compared.{only}{unreadable}{trunc}";
            _setStatus("Dump comparison finished.");
        }
        catch (OperationCanceledException)
        {
            if (!ReferenceEquals(myCts, _cts)) return;
            Progress = 0;
            Summary = "Comparison cancelled.";
        }
        catch (Exception ex)
        {
            if (!ReferenceEquals(myCts, _cts)) return;
            Progress = 0;
            Summary = "Comparison failed: " + ex.Message;
        }
        finally
        {
            if (ReferenceEquals(myCts, _cts)) IsComparing = false;
        }
    }

    private void RaiseCommands()
    {
        CompareCommand.RaiseCanExecuteChanged();
        CancelCommand.RaiseCanExecuteChanged();
    }
}

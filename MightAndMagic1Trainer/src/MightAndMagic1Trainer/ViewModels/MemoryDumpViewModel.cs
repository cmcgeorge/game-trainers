using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using MightAndMagic1Trainer.Memory;
using MightAndMagic1Trainer.Mvvm;

namespace MightAndMagic1Trainer.ViewModels;

/// <summary>
/// "Dump": presenter for <see cref="MemoryDumper"/> — writes the attached process's whole
/// memory (the DOSBox-X address space, emulated DOS memory included) to a .bin file with a
/// .csv region index alongside it. Owns the progress/summary state, cancellation, and the
/// post-dump "open folder" affordance; the read/salvage/file-format work lives in the engine.
/// </summary>
public sealed class MemoryDumpViewModel : ObservableObject
{
    private readonly Func<ProcessMemory?> _getMem;
    private readonly Action<string> _setStatus;
    private CancellationTokenSource? _cts;
    private Task? _dumpTask;

    public MemoryDumpViewModel(Func<ProcessMemory?> getMem, Action<string> setStatus)
    {
        _getMem = getMem;
        _setStatus = setStatus;
        CancelCommand = new RelayCommand(() => _cts?.Cancel(), () => IsDumping);
        OpenFolderCommand = new RelayCommand(OpenDumpFolder, () => LastDumpPath != null);
    }

    /// <summary>Enables the dump button: attached and not already dumping.</summary>
    public bool CanDump => _getMem() != null && !IsDumping;

    private bool _isDumping;
    public bool IsDumping
    {
        get => _isDumping;
        private set { if (SetField(ref _isDumping, value)) RefreshState(); }
    }

    private double _progress;
    public double Progress { get => _progress; private set => SetField(ref _progress, value); }

    // True while enumerating regions, before a meaningful total exists; the bar pulses
    // instead of sitting at an idle-looking 0%.
    private bool _isIndeterminate;
    public bool IsIndeterminate { get => _isIndeterminate; private set => SetField(ref _isIndeterminate, value); }

    private string _summary = "No dump yet. Attach to the game, then dump its memory to a file.";
    public string Summary { get => _summary; private set => SetField(ref _summary, value); }

    private string? _lastDumpPath;
    public string? LastDumpPath
    {
        get => _lastDumpPath;
        private set { if (SetField(ref _lastDumpPath, value)) OpenFolderCommand.RaiseCanExecuteChanged(); }
    }

    public RelayCommand CancelCommand { get; }
    public RelayCommand OpenFolderCommand { get; }

    /// <summary>The in-flight (or last) dump task, or null if none has started. It completes only
    /// after the dump has stopped touching the disk — the window waits on it when closing so a
    /// cancelled dump can finish deleting its temp files before the process exits.</summary>
    public Task? DumpTask => _dumpTask;

    /// <summary>Re-evaluates the dump/cancel availability (called by the owner on attach/detach).</summary>
    public void RefreshState()
    {
        OnPropertyChanged(nameof(CanDump));
        CancelCommand.RaiseCanExecuteChanged();
    }

    /// <summary>Requests cancellation of an in-flight dump (called on detach and on window close).
    /// The dump observes it at the next chunk and cleans up its own temp files.</summary>
    public void CancelDump() => _cts?.Cancel();

    /// <summary>Dumps the attached process to <paramref name="path"/> (the view picks it via a save dialog).</summary>
    public void Start(string path)
    {
        var mem = _getMem();
        if (mem == null) { _setStatus("Attach to the game first."); return; }
        if (IsDumping) return;
        _dumpTask = RunAsync(mem, path);
    }

    // Runs on the UI thread up to the first await (so IsDumping is set before Start returns), then
    // offloads the read loop to the pool. The captured `mem` keeps us bound to one process handle.
    private async Task RunAsync(ProcessMemory mem, string path)
    {
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        try
        {
            IsDumping = true;
            IsIndeterminate = true;
            Progress = 0;
            Summary = "Enumerating memory regions…";
            _setStatus("Dumping process memory…");

            // Both report only while un-cancelled so a detach can't resurrect stale text.
            var progress = new Progress<double>(v =>
            {
                if (ct.IsCancellationRequested) return;
                IsIndeterminate = false;
                Progress = v;
            });
            var phase = new Progress<string>(s => { if (!ct.IsCancellationRequested) Summary = s; });

            var result = await Task.Run(() => MemoryDumper.Dump(mem, path, progress, phase, ct), ct);
            // Reached even when a cancel raced the finish: the files were committed, so report
            // the success rather than leaving "Dumping…" on screen and the dump unannounced.
            Progress = 1;
            LastDumpPath = path;
            string skipped = result.BytesUnreadable > 0
                ? $" {MemoryDumper.FormatBytes(result.BytesUnreadable)} unreadable (zero-filled)."
                : "";
            Summary = $"Dumped {MemoryDumper.FormatBytes(result.BytesWritten)} across {result.RegionCount} regions "
                + $"in {result.Elapsed.TotalSeconds:0.0} s to {Path.GetFileName(path)}.{skipped} "
                + $"Region index: {Path.GetFileName(MemoryDumper.IndexPathFor(path))}.";
            ReportUnlessSuperseded("Memory dump complete: " + path);
        }
        catch (OperationCanceledException)
        {
            Progress = 0;   // the temp output was deleted, so an empty bar is the truthful state
            Summary = "Dump cancelled.";
            ReportUnlessSuperseded("Memory dump cancelled.");
        }
        catch (Exception ex)
        {
            Progress = 0;
            Summary = "Dump failed: " + ex.Message;
            _setStatus("Memory dump failed: " + ex.Message);
        }
        finally
        {
            IsIndeterminate = false;
            IsDumping = false;
        }

        // This continuation can land after a re-attach has already put fresh scan progress in the
        // status bar; don't stomp it — the tab's own Summary carries the dump's outcome.
        void ReportUnlessSuperseded(string status)
        {
            var memNow = _getMem();
            if (memNow == null || ReferenceEquals(memNow, mem)) _setStatus(status);
        }
    }

    private void OpenDumpFolder()
    {
        if (LastDumpPath is not { } path || !File.Exists(path))
        {
            _setStatus("The last dump is no longer on disk.");
            return;
        }
        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = false });
    }
}

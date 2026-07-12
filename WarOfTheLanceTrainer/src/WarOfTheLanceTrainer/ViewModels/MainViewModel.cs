using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Input;
using System.Windows.Threading;
using WarOfTheLanceTrainer.Game;
using WarOfTheLanceTrainer.Memory;

namespace WarOfTheLanceTrainer.ViewModels;

/// <summary>A selectable target process.</summary>
public sealed class ProcessEntry
{
    public int Id { get; }
    public string Name { get; }
    public bool IsEmulator { get; }
    public string Display => $"{Name}  (pid {Id})";

    public ProcessEntry(int id, string name, bool isEmulator)
    {
        Id = id; Name = name; IsEmulator = isEmulator;
    }
}

/// <summary>
/// Root view-model: process attach/scan/detach, the located unit-strength list, the freeze poll
/// loop, and the boost/restore quick actions.
/// </summary>
public sealed class MainViewModel : ObservableObject, IStrengthHost, IDisposable
{
    private static readonly string[] EmulatorHints =
        { "dosbox", "dosbox-x", "dosbox-staging", "scummvm", "pcem", "86box", "qemu", "boxer" };

    private ProcessMemory? _mem;
    private readonly DispatcherTimer _poll;
    private CancellationTokenSource? _scanCts;
    private LocatedState? _state;

    public ObservableCollection<ProcessEntry> Processes { get; } = new();
    public ObservableCollection<UnitStrengthViewModel> Units { get; } = new();

    private ProcessEntry? _selectedProcess;
    public ProcessEntry? SelectedProcess { get => _selectedProcess; set { SetField(ref _selectedProcess, value); RaiseCommands(); } }

    private UnitStrengthViewModel? _selectedUnit;
    public UnitStrengthViewModel? SelectedUnit { get => _selectedUnit; set => SetField(ref _selectedUnit, value); }

    public bool IsAttached => _mem is { IsOpen: true };

    private bool _isScanning;
    public bool IsScanning { get => _isScanning; set { SetField(ref _isScanning, value); RaiseCommands(); } }

    private string _status = "Launch War of the Lance in DOSBox, then pick the process and Attach.";
    public string Status { get => _status; set => SetField(ref _status, value); }

    private bool _freezeAll;
    public bool FreezeAll
    {
        get => _freezeAll;
        set
        {
            if (!SetField(ref _freezeAll, value)) return;
            foreach (var u in Units) u.Freeze = value;
            Status = value ? "All unit strengths frozen." : "Freeze OFF.";
        }
    }

    // --- commands ------------------------------------------------------------
    public ICommand RefreshProcessesCommand { get; }
    public ICommand AttachCommand { get; }
    public ICommand DetachCommand { get; }
    public ICommand ScanCommand { get; }
    public ICommand MaxAllCommand { get; }
    public ICommand RestoreAllCommand { get; }

    public MainViewModel()
    {
        RefreshProcessesCommand = new RelayCommand(_ => RefreshProcesses());
        AttachCommand = new RelayCommand(_ => Attach(), _ => SelectedProcess != null && !IsAttached && !IsScanning);
        DetachCommand = new RelayCommand(_ => Detach(), _ => IsAttached);
        ScanCommand = new RelayCommand(_ => Scan(), _ => IsAttached && !IsScanning);
        MaxAllCommand = new RelayCommand(_ => ForEachUnit(u => u.Max()), _ => Units.Count > 0);
        RestoreAllCommand = new RelayCommand(_ => ForEachUnit(u => u.RestoreBase()), _ => Units.Count > 0);

        _poll = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
        _poll.Tick += (_, _) => PollTick();

        RefreshProcesses();
    }

    // --- process management --------------------------------------------------
    public void RefreshProcesses()
    {
        var previous = SelectedProcess?.Id;
        Processes.Clear();
        var list = new List<ProcessEntry>();
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                string name = p.ProcessName;
                bool emu = EmulatorHints.Any(h => name.Contains(h, StringComparison.OrdinalIgnoreCase));
                list.Add(new ProcessEntry(p.Id, name, emu));
            }
            catch { /* process exited between enumeration and query */ }
            finally { p.Dispose(); }
        }
        foreach (var e in list.OrderByDescending(e => e.IsEmulator).ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase))
            Processes.Add(e);

        SelectedProcess = Processes.FirstOrDefault(e => e.Id == previous)
                          ?? Processes.FirstOrDefault(e => e.IsEmulator)
                          ?? Processes.FirstOrDefault();
    }

    private void Attach()
    {
        if (SelectedProcess == null) return;
        try
        {
            _mem = ProcessMemory.Open(SelectedProcess.Id);
            OnPropertyChanged(nameof(IsAttached));
            RaiseCommands();
            _poll.Start();
            Status = $"Attached to {SelectedProcess.Name} (pid {SelectedProcess.Id}). Scanning…";
            Scan();
        }
        catch (Exception ex)
        {
            Status = "Attach failed: " + ex.Message;
        }
    }

    private void Detach()
    {
        _poll.Stop();
        _scanCts?.Cancel();
        _mem?.Dispose();
        _mem = null;
        _state = null;
        Units.Clear();
        SelectedUnit = null;
        _freezeAll = false; OnPropertyChanged(nameof(FreezeAll));
        OnPropertyChanged(nameof(IsAttached));
        RaiseCommands();
        Status = "Detached.";
    }

    // --- scanning ------------------------------------------------------------
    private async void Scan()
    {
        if (_mem == null || IsScanning) return;
        IsScanning = true;
        Status = "Scanning guest memory for War of the Lance…";
        _scanCts?.Dispose();
        _scanCts = new CancellationTokenSource();
        var ct = _scanCts.Token;
        var mem = _mem;
        try
        {
            var found = await Task.Run(() => GameLocator.Locate(mem, ct), ct);
            if (mem != _mem) return;   // detached / re-attached while scanning
            _state = found;
            Units.Clear();
            if (found.StrengthBlockFound)
            {
                for (int i = 0; i < StrengthTable.Entries.Length; i++)
                {
                    nuint addr = found.StrengthBlockAddress + (nuint)i;
                    byte value = i < found.StrengthCells.Length ? found.StrengthCells[i] : (byte)0;
                    Units.Add(new UnitStrengthViewModel(this, StrengthTable.Entries[i], addr, value));
                }
            }
            SelectedUnit = Units.FirstOrDefault();
            if (FreezeAll) foreach (var u in Units) u.Freeze = true;
            Status = BuildScanStatus(found);
        }
        catch (OperationCanceledException) { Status = "Scan cancelled."; }
        catch (Exception ex) { Status = "Scan error: " + ex.Message; }
        finally { IsScanning = false; RaiseCommands(); }
    }

    private static string BuildScanStatus(LocatedState s)
    {
        if (!s.AnythingFound)
            return "War of the Lance not found. Make sure the game is loaded past the title screen, then Re-scan.";
        string nation = s.NationTableFound ? $"nation table @ 0x{(ulong)s.NationTableAddress:X}" : "nation table not found";
        string units = s.StrengthBlockFound
            ? $"{StrengthTable.Entries.Length} unit strengths @ 0x{(ulong)s.StrengthBlockAddress:X}"
            : "strength block not found";
        return $"Located: {nation}; {units}.";
    }

    // --- quick actions -------------------------------------------------------
    private void ForEachUnit(Action<UnitStrengthViewModel> action)
    {
        foreach (var u in Units) action(u);
        Status = "Applied to every located unit.";
    }

    // --- poll loop -----------------------------------------------------------
    private void PollTick()
    {
        if (_mem == null || _state == null || !_state.StrengthBlockFound) return;
        if (!_mem.IsOpen) { Detach(); Status = "Target process exited."; return; }

        int count = StrengthTable.Count;
        var buf = _mem.Read(_state.StrengthBlockAddress, count);
        if (buf.Length < count)   // process gone / region unmapped since the scan
        {
            Detach();
            Status = "Target process exited.";
            return;
        }
        for (int i = 0; i < Units.Count; i++)
        {
            var u = Units[i];
            u.ApplyFreeze();
            if (!u.Freeze && i < buf.Length) u.RefreshLive(buf[i]);
        }
    }

    // --- IStrengthHost -------------------------------------------------------
    bool IStrengthHost.WriteStrength(nuint address, byte value)
        => _mem?.Write(address, new[] { value }) ?? false;

    void IStrengthHost.ReportWriteFailure(string unitLabel)
        => Status = $"Write failed for {unitLabel} — the value shown was not applied.";

    private void RaiseCommands()
    {
        (AttachCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (DetachCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ScanCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (MaxAllCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (RestoreAllCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    public void Dispose()
    {
        _poll.Stop();
        _scanCts?.Cancel();
        _scanCts?.Dispose();
        _mem?.Dispose();
    }
}

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Input;
using System.Windows.Threading;
using AirborneRangerTrainer.Game;
using AirborneRangerTrainer.Memory;

namespace AirborneRangerTrainer.ViewModels;

/// <summary>A selectable target process.</summary>
public sealed class ProcessEntry
{
    /// <summary>Process id.</summary>
    public int Id { get; }

    /// <summary>Process name.</summary>
    public string Name { get; }

    /// <summary>True when the name looks like an emulator, which floats it to the top of the list.</summary>
    public bool IsEmulator { get; }

    /// <summary>Label for the combo box.</summary>
    public string Display => $"{Name}  (pid {Id})";

    /// <summary>Builds an entry.</summary>
    public ProcessEntry(int id, string name, bool isEmulator)
    {
        Id = id;
        Name = name;
        IsEmulator = isEmulator;
    }
}

/// <summary>
/// Root view-model: pick the emulator, attach, auto-locate the data segment, then poll it.
///
/// There is no manual value search anywhere in this trainer. Attaching runs
/// <see cref="GameLocator"/>, which anchors on a literal from the game's own status-panel template
/// to find the data segment and reads the mission state at its fixed offset past it.
/// </summary>
public sealed class MainViewModel : ObservableObject, IMissionHost, IDisposable
{
    private ProcessMemory? _mem;
    private IMemorySource? _source;
    private readonly DispatcherTimer _poll;
    private CancellationTokenSource? _scanCts;
    private int _readFailures;
    private int _panelTicks;

    /// <summary>Consecutive failed poll reads before the user is told the game is gone.</summary>
    private const int ReadFailuresBeforeReporting = 5;

    /// <summary>Poll ticks between status-panel refreshes — it only changes when the game redraws it.</summary>
    private const int PanelRefreshEvery = 4;

    /// <summary>Candidate processes to attach to.</summary>
    public ObservableCollection<ProcessEntry> Processes { get; } = new();

    /// <summary>The reference tabs.</summary>
    public ReferenceViewModel Reference { get; } = new();

    /// <summary>The offline roster editor.</summary>
    public RosterViewModel Roster { get; }

    /// <summary>Builds the shell and enumerates processes.</summary>
    public MainViewModel()
    {
        Roster = new RosterViewModel(msg => Status = msg);

        RefreshProcessesCommand = new RelayCommand(RefreshProcesses);
        AttachCommand = new RelayCommand(Attach, () => SelectedProcess != null && !IsAttached && !IsScanning);
        DetachCommand = new RelayCommand(Detach, () => IsAttached);
        LocateCommand = new RelayCommand(() => Locate(), () => IsAttached && !IsScanning);

        _poll = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _poll.Tick += (_, _) => PollTick();

        RefreshProcesses();
    }

    // --- state ---------------------------------------------------------------

    private ProcessEntry? _selectedProcess;

    /// <summary>The process the user picked.</summary>
    public ProcessEntry? SelectedProcess
    {
        get => _selectedProcess;
        set { SetField(ref _selectedProcess, value); RaiseCommands(); }
    }

    private MissionViewModel? _mission;

    /// <summary>The located mission, or null.</summary>
    public MissionViewModel? Mission
    {
        get => _mission;
        private set { SetField(ref _mission, value); OnPropertyChanged(nameof(HasMission)); }
    }

    /// <summary>True once the data segment has been located.</summary>
    public bool HasMission => _mission != null;

    /// <summary>True while a process is attached.</summary>
    public bool IsAttached => _mem is { IsOpen: true };

    private bool _isScanning;

    /// <summary>True while an auto-locate is running.</summary>
    public bool IsScanning
    {
        get => _isScanning;
        private set { SetField(ref _isScanning, value); RaiseCommands(); }
    }

    private string _status =
        "Start Airborne Ranger in DOSBox and get into a mission, then pick the process and Attach.";

    /// <summary>The status-bar text.</summary>
    public string Status { get => _status; set => SetField(ref _status, value); }

    // --- commands ------------------------------------------------------------

    /// <summary>Re-enumerates the process list.</summary>
    public ICommand RefreshProcessesCommand { get; }

    /// <summary>Opens the selected process and auto-locates.</summary>
    public ICommand AttachCommand { get; }

    /// <summary>Closes the process handle.</summary>
    public ICommand DetachCommand { get; }

    /// <summary>Re-runs the auto-locate.</summary>
    public ICommand LocateCommand { get; }

    // --- process management --------------------------------------------------

    /// <summary>Rebuilds <see cref="Processes"/>, keeping the current selection if it survives.</summary>
    public void RefreshProcesses()
    {
        int? previous = SelectedProcess?.Id;
        var list = new List<ProcessEntry>();
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                string name = p.ProcessName;
                bool emu = GameFacts.EmulatorHints.Any(h => name.Contains(h, StringComparison.OrdinalIgnoreCase));
                list.Add(new ProcessEntry(p.Id, name, emu));
            }
            catch
            {
                // The process exited between enumeration and the query — skip it.
            }
            finally
            {
                p.Dispose();
            }
        }

        Processes.Clear();
        foreach (var e in list.OrderByDescending(e => e.IsEmulator)
                              .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase))
            Processes.Add(e);

        SelectedProcess = Processes.FirstOrDefault(e => e.Id == previous)
                          ?? Processes.FirstOrDefault(e => e.IsEmulator)
                          ?? Processes.FirstOrDefault();
    }

    private void Attach()
    {
        if (SelectedProcess == null || IsScanning) return;
        try
        {
            _mem?.Dispose();   // never leak a handle if Attach is somehow reached twice
            _mem = ProcessMemory.Open(SelectedProcess.Id);
            _source = new ProcessMemorySource(_mem);
            _readFailures = 0;
            OnPropertyChanged(nameof(IsAttached));
            RaiseCommands();
            _poll.Start();
            Status = $"Attached to {SelectedProcess.Name} (pid {SelectedProcess.Id}). Locating the game…";
            Locate();
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
        _source = null;
        Mission = null;
        _readFailures = 0;
        // An in-flight scan only notices cancellation at a chunk boundary, and its `finally` would
        // otherwise clear IsScanning long after the fact. Clear it here so the next Attach is not
        // silently swallowed by the `if (IsScanning) return;` guard in Locate.
        IsScanning = false;
        OnPropertyChanged(nameof(IsAttached));
        RaiseCommands();
        Status = "Detached.";
    }

    // --- auto-locate ---------------------------------------------------------

    private async void Locate()
    {
        // Capture the attachment up front and check it here rather than asserting it away later:
        // _mem and _source are set and cleared together, but stating the requirement once beats a
        // null-forgiving `!` that the compiler cannot check.
        var mem = _mem;
        var source = _source;
        if (mem == null || source == null || IsScanning) return;

        IsScanning = true;
        Status = "Locating the game's data segment…";
        // Cancel the previous scan but do NOT dispose it: an earlier scan may still be polling that
        // token on the thread pool, and disposing a source another thread is still observing is
        // outside the type's documented contract. Let it be collected.
        _scanCts?.Cancel();
        _scanCts = new CancellationTokenSource();

        var ct = _scanCts.Token;
        try
        {
            var found = await Task.Run(() => GameLocator.Locate(source, ct), ct);
            if (mem != _mem) return;   // detached or re-attached while scanning

            if (!found.Found)
            {
                Mission = null;
                Status = found.AnchorsMatchedButStateDidNot
                    ? $"Found Airborne Ranger at 0x{(ulong)found.RejectedAddress:X}, but its mission " +
                      "state does not look like a live mission — start one and click Locate again."
                    : "Airborne Ranger was not found in that process. Make sure AR.EXE is running " +
                      "past the graphics-mode prompt, then click Locate again.";
                return;
            }

            Mission = new MissionViewModel(this, found);
            Status = $"Found the data segment at 0x{(ulong)found.DgroupAddress:X} — {found.Method} " +
                     $"({found.ValidatorsMatched}/{MissionFormat.Validators.Length} corroborating literals matched).";
        }
        catch (OperationCanceledException)
        {
            if (mem == _mem) Status = "Locate cancelled.";
        }
        catch (Exception ex)
        {
            if (mem == _mem) Status = "Locate error: " + ex.Message;
        }
        finally
        {
            // Only the scan that still owns the attachment may clear the flag. Detach already
            // cleared it, and a quick re-attach may have started a newer scan — a stale scan's
            // continuation landing here must not tell the UI that the live one has finished.
            if (mem == _mem)
            {
                IsScanning = false;
                RaiseCommands();
            }
        }
    }

    // --- poll loop -----------------------------------------------------------

    private void PollTick()
    {
        var source = _source;
        var mission = Mission;
        if (source == null || mission == null) return;

        if (GameLocator.Reread(source, mission.DgroupAddress, mission.LiveBuffer))
        {
            _readFailures = 0;
            mission.OnPolled();
            if (++_panelTicks >= PanelRefreshEvery)
            {
                _panelTicks = 0;
                mission.OnStatusPanel(GameLocator.ReadStatusPanel(source, mission.DgroupAddress));
            }
            return;
        }

        // The window has stopped reading — the game exited, restarted (relocating its data segment),
        // or the region was unmapped. Say so instead of leaving a stale mirror on screen that the
        // user would keep editing into an address that no longer belongs to the game.
        if (++_readFailures == ReadFailuresBeforeReporting)
            Status = "Lost the game — it may have exited or reloaded. Click Locate game.";
    }

    // --- IMissionHost --------------------------------------------------------

    bool IMissionHost.WriteBytes(int dgroupOffset, byte[] bytes)
    {
        var mission = Mission;
        if (_mem == null || mission == null) return false;
        return _mem.Write(mission.DgroupAddress + (nuint)dgroupOffset, bytes);
    }

    void IMissionHost.ReportStatus(string message) => Status = message;

    // --- plumbing ------------------------------------------------------------

    private void RaiseCommands()
    {
        (AttachCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (DetachCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (LocateCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _poll.Stop();
        // Cancel but do not dispose, for the same reason as in Locate: a scan may still be running
        // on the thread pool and observing this token. Let it be collected.
        _scanCts?.Cancel();
        _mem?.Dispose();
    }
}

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Input;
using System.Windows.Threading;
using AlternateRealityTrainer.Game;
using AlternateRealityTrainer.Memory;

namespace AlternateRealityTrainer.ViewModels;

/// <summary>A selectable target process.</summary>
public sealed class ProcessEntry
{
    public int Id { get; }
    public string Name { get; }
    public bool IsEmulator { get; }
    public string Display => $"{Name}  (pid {Id})";

    public ProcessEntry(int id, string name, bool isEmulator)
    {
        Id = id;
        Name = name;
        IsEmulator = isEmulator;
    }
}

/// <summary>
/// Root view-model: pick the emulator, attach, auto-locate the character, then poll it.
///
/// There is no manual value search anywhere in this trainer. Attaching runs
/// <see cref="GameLocator"/>, which anchors on a literal from the game's own status-bar template to
/// find the data segment and reads the character record at its fixed offset past it.
/// </summary>
public sealed class MainViewModel : ObservableObject, ICharacterHost, IDisposable
{
    private ProcessMemory? _mem;
    private IMemorySource? _source;
    private readonly DispatcherTimer _poll;
    private CancellationTokenSource? _scanCts;
    private int _readFailures;

    /// <summary>Consecutive failed poll reads before the user is told the character is gone.</summary>
    private const int ReadFailuresBeforeReporting = 5;

    public ObservableCollection<ProcessEntry> Processes { get; } = new();
    public ReferenceViewModel Reference { get; } = new();

    public MainViewModel()
    {
        RefreshProcessesCommand = new RelayCommand(RefreshProcesses);
        AttachCommand = new RelayCommand(Attach, () => SelectedProcess != null && !IsAttached && !IsScanning);
        DetachCommand = new RelayCommand(Detach, () => IsAttached);
        LocateCommand = new RelayCommand(() => Locate(), () => IsAttached && !IsScanning);
        StructuralScanCommand = new RelayCommand(() => Locate(allowStructuralScan: true),
                                                 () => IsAttached && !IsScanning);

        _poll = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
        _poll.Tick += (_, _) => PollTick();

        RefreshProcesses();
        TryAutoAttach();
    }

    /// <summary>
    /// On startup, attach automatically when the pre-selected process looks like a game emulator,
    /// so a running game is picked up without a manual click. Stays a no-op (just the populated process
    /// list) when nothing emulator-looking is running, rather than attaching to some unrelated process
    /// and scanning it fruitlessly.
    /// </summary>
    private void TryAutoAttach()
    {
        if (!IsAttached && SelectedProcess?.IsEmulator == true) Attach();
    }

    // --- state ---------------------------------------------------------------

    private ProcessEntry? _selectedProcess;
    public ProcessEntry? SelectedProcess
    {
        get => _selectedProcess;
        set { SetField(ref _selectedProcess, value); RaiseCommands(); }
    }

    private CharacterViewModel? _character;
    public CharacterViewModel? Character
    {
        get => _character;
        private set { SetField(ref _character, value); OnPropertyChanged(nameof(HasCharacter)); }
    }

    public bool HasCharacter => _character != null;

    public bool IsAttached => _mem is { IsOpen: true };

    private bool _isScanning;
    public bool IsScanning
    {
        get => _isScanning;
        private set { SetField(ref _isScanning, value); RaiseCommands(); }
    }

    private string _status =
        "Launch Alternate Reality in DOSBox and resume a character, then pick the process and Attach.";
    public string Status { get => _status; set => SetField(ref _status, value); }

    // --- commands ------------------------------------------------------------

    public ICommand RefreshProcessesCommand { get; }
    public ICommand AttachCommand { get; }
    public ICommand DetachCommand { get; }
    public ICommand LocateCommand { get; }

    /// <summary>
    /// The opt-in structural scan, for a build whose display literals have moved. Kept separate
    /// from <see cref="LocateCommand"/> because it can match unrelated data in a process that is
    /// not the game — the user has to ask for it, and is told what it found.
    /// </summary>
    public ICommand StructuralScanCommand { get; }

    // --- process management --------------------------------------------------

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
            Status = $"Attached to {SelectedProcess.Name} (pid {SelectedProcess.Id}). Locating the character…";
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
        Character = null;
        Reference.ClearTerrain();
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

    private async void Locate(bool allowStructuralScan = false)
    {
        // Capture the attachment up front and check it here rather than asserting it away later:
        // _mem and _source are set and cleared together, but stating the requirement once beats a
        // null-forgiving `!` that the compiler cannot check.
        var mem = _mem;
        var source = _source;
        if (mem == null || source == null || IsScanning) return;

        IsScanning = true;
        Status = allowStructuralScan
            ? "Scanning for anything shaped like a character record…"
            : "Locating the character record…";
        // Cancel the previous scan but do NOT dispose it: Detach clears IsScanning without waiting,
        // so an earlier scan may still be polling that token on the thread pool. Let it be collected.
        _scanCts?.Cancel();
        _scanCts = new CancellationTokenSource();

        var ct = _scanCts.Token;
        try
        {
            var found = await Task.Run(() => GameLocator.Locate(source, ct, allowStructuralScan), ct);
            if (mem != _mem) return;   // detached or re-attached while scanning

            if (!found.Found)
            {
                Character = null;
                Status = allowStructuralScan
                    ? "Nothing in this process looks like a character record."
                    : "No character found. Check the game is past the title screen with a character " +
                      "loaded and that you picked the right process, then click Locate again. If this " +
                      "is a different build of the game, try Scan anyway.";
                return;
            }

            var vm = new CharacterViewModel(this, found);
            Character = vm;

            // The street map sits at a fixed offset from DGROUP, so only an anchored locate can
            // reach it. Reading it here means the map tab shows real walls the moment we attach.
            Reference.SetTerrain(GameLocator.ReadTerrain(source, found.DgroupAddress));
            Status = found.ValidatorsMatched > 0
                ? $"Found {vm.Name} at 0x{(ulong)vm.Address:X} — {vm.LocateMethod} " +
                  $"({found.ValidatorsMatched}/{CharacterFormat.Validators.Length} corroborating literals matched)."
                : $"Found something shaped like a character ({vm.Name}) at 0x{(ulong)vm.Address:X} by " +
                  "structural scan — no anchor matched, so check the name and numbers against the game " +
                  "before editing anything.";
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
        var character = Character;
        if (source == null || character == null) return;

        if (GameLocator.Reread(source, character.Address, character.LiveBuffer))
        {
            _readFailures = 0;
            character.OnPolled();
            return;
        }

        // The record has stopped reading — the game exited, restarted (relocating its data segment),
        // or the region was unmapped. Say so instead of leaving a stale mirror on screen that the
        // user would keep editing into an address that no longer belongs to the character.
        if (++_readFailures == ReadFailuresBeforeReporting)
            Status = "Lost the character — the game may have exited or reloaded. Click Locate character.";
    }

    // --- ICharacterHost ------------------------------------------------------

    bool ICharacterHost.WriteBytes(nuint recordAddress, int offset, byte[] bytes) =>
        _mem?.Write(recordAddress + (nuint)offset, bytes) ?? false;

    void ICharacterHost.ReportStatus(string message) => Status = message;

    // --- plumbing ------------------------------------------------------------

    private void RaiseCommands()
    {
        (AttachCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (DetachCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (LocateCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (StructuralScanCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    public void Dispose()
    {
        _poll.Stop();
        _scanCts?.Cancel();
        _mem?.Dispose();
    }
}

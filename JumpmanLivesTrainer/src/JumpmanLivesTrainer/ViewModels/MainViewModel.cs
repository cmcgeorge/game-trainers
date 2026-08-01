using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Input;
using System.Windows.Threading;
using JumpmanLivesTrainer.Game;
using JumpmanLivesTrainer.Memory;

namespace JumpmanLivesTrainer.ViewModels;

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
/// Root view-model: pick the emulator, attach, auto-locate the data segment, then poll it.
/// There is no manual value search — attaching runs <see cref="GameLocator"/> automatically.
/// </summary>
public sealed class MainViewModel : ObservableObject, IGameHost, IDisposable
{
    private ProcessMemory? _mem;
    private IMemorySource? _source;
    private readonly DispatcherTimer _poll;
    private CancellationTokenSource? _scanCts;
    private int _readFailures;

    private const int ReadFailuresBeforeReporting = 5;

    /// <summary>Candidate processes to attach to.</summary>
    public ObservableCollection<ProcessEntry> Processes { get; } = new();

    /// <summary>The reference tab's view-model.</summary>
    public ReferenceViewModel Reference { get; } = new();

    public MainViewModel()
    {
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

    public ProcessEntry? SelectedProcess
    {
        get => _selectedProcess;
        set { SetField(ref _selectedProcess, value); RaiseCommands(); }
    }

    private PlayerViewModel? _player;

    public PlayerViewModel? Player
    {
        get => _player;
        private set { SetField(ref _player, value); OnPropertyChanged(nameof(HasPlayer)); }
    }

    public bool HasPlayer => _player != null;

    public bool IsAttached => _mem is { IsOpen: true };

    private bool _isScanning;

    public bool IsScanning
    {
        get => _isScanning;
        private set { SetField(ref _isScanning, value); RaiseCommands(); }
    }

    private string _status = "Start Jumpman Lives! in DOSBox, then pick the process and Attach.";

    public string Status { get => _status; set => SetField(ref _status, value); }

    // --- commands ------------------------------------------------------------

    public ICommand RefreshProcessesCommand { get; }
    public ICommand AttachCommand { get; }
    public ICommand DetachCommand { get; }
    public ICommand LocateCommand { get; }

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
            catch { }
            finally { p.Dispose(); }
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
            _mem?.Dispose();
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
        Player = null;
        _readFailures = 0;
        IsScanning = false;
        OnPropertyChanged(nameof(IsAttached));
        RaiseCommands();
        Status = "Detached.";
    }

    // --- auto-locate ---------------------------------------------------------

    private async void Locate()
    {
        var mem = _mem;
        var source = _source;
        if (mem == null || source == null || IsScanning) return;

        IsScanning = true;
        Status = "Locating the game's data segment…";
        _scanCts?.Cancel();
        _scanCts = new CancellationTokenSource();

        var ct = _scanCts.Token;
        try
        {
            var found = await Task.Run(() => GameLocator.Locate(source, ct), ct);
            if (mem != _mem) return;

            if (!found.Found)
            {
                Player = null;
                Status = found.AnchorsMatchedButGlobalsDidNot
                    ? $"Found Jumpman Lives! at 0x{(ulong)found.RejectedAddress:X}, but its game state " +
                      "does not look plausible. Start a game and click Locate again."
                    : "Jumpman Lives! was not found in that process. Make sure JMAN2.EXE is running " +
                      "in DOSBox, then click Locate again.";
                return;
            }

            int pl = GameLocator.ReadPl(source, found.DgroupAddress);
            if (pl is < 1 or > GameLayout.MaxActivePlayers) pl = 1;

            var playerBytes = GameLocator.ReadPlayer(source, found.DgroupAddress, pl);
            if (playerBytes == null || mem != _mem)
            {
                Status = "Found the data segment but could not read the player record.";
                return;
            }

            Player = new PlayerViewModel(this, found, pl);
            Array.Copy(playerBytes, Player.LivePlayer, playerBytes.Length);
            Array.Copy(found.Globals, Player.LiveGlobals, Math.Min(found.Globals.Length, Player.LiveGlobals.Length));
            Player.SyncFromLive();

            Status = $"Found the data segment at 0x{(ulong)found.DgroupAddress:X} — {found.Method} " +
                     $"({found.ValidatorsMatched}/{GameLayout.MinValidators} validators matched, player {pl}).";
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
        var player = Player;
        if (source == null || player == null) return;

        if (!GameLocator.RereadGlobals(source, player.DgroupAddress, player.LiveGlobals))
        {
            if (++_readFailures == ReadFailuresBeforeReporting)
                Status = "Lost the game — it may have exited or reloaded. Click Locate game.";
            return;
        }

        var playerBytes = GameLocator.ReadPlayer(source, player.DgroupAddress, player.PlayerIndex);
        if (playerBytes == null || playerBytes.Length < GameLayout.PlayerRecordSize)
        {
            if (++_readFailures == ReadFailuresBeforeReporting)
                Status = "Lost the game — it may have exited or reloaded. Click Locate game.";
            return;
        }

        _readFailures = 0;
        Array.Copy(playerBytes, player.LivePlayer, GameLayout.PlayerRecordSize);
        player.OnPolled();
    }

    // --- IGameHost -----------------------------------------------------------

    bool IGameHost.WriteBytes(int dgroupOffset, byte[] bytes)
    {
        var player = Player;
        if (_mem == null || player == null) return false;
        return _mem.Write(player.DgroupAddress + (nuint)dgroupOffset, bytes);
    }

    void IGameHost.ReportStatus(string message) => Status = message;

    // --- plumbing ------------------------------------------------------------

    private void RaiseCommands()
    {
        (AttachCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (DetachCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (LocateCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    public void Dispose()
    {
        _poll.Stop();
        _scanCts?.Cancel();
        _mem?.Dispose();
    }
}

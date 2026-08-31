using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Input;
using System.Windows.Threading;
using LegacyOfTheAncientsTrainer.Game;
using LegacyOfTheAncientsTrainer.Memory;

namespace LegacyOfTheAncientsTrainer.ViewModels;

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
/// Root view-model: process attach/scan, the located character, the freeze poll loop, and
/// the quick actions. Legacy of the Ancients is a single-character RPG, so the "party" is
/// at most one character.
/// </summary>
public sealed class MainViewModel : ObservableObject, ICharacterHost, IDisposable
{
    private ProcessMemory? _mem;
    private IMemorySource? _source;
    private readonly DispatcherTimer _poll;
    private CancellationTokenSource? _scanCts;

    public ObservableCollection<ProcessEntry> Processes { get; } = new();
    public ObservableCollection<CharacterViewModel> Party { get; } = new();

    public ReferenceViewModel Reference { get; } = new();

    private ProcessEntry? _selectedProcess;
    public ProcessEntry? SelectedProcess
    {
        get => _selectedProcess;
        set { SetField(ref _selectedProcess, value); RaiseCommands(); }
    }

    private CharacterViewModel? _selectedCharacter;
    public CharacterViewModel? SelectedCharacter
    {
        get => _selectedCharacter;
        set => SetField(ref _selectedCharacter, value);
    }

    public bool IsAttached => _mem is { IsOpen: true };

    private bool _isScanning;
    public bool IsScanning
    {
        get => _isScanning;
        set { SetField(ref _isScanning, value); RaiseCommands(); }
    }

    private string _status = "Launch Legacy of the Ancients in DOSBox, then pick the process and Attach.";
    public string Status { get => _status; set => SetField(ref _status, value); }

    // --- freeze toggles -----------------------------------------------------
    private bool _freezeHP;
    public bool FreezeHP
    {
        get => _freezeHP;
        set
        {
            if (SetField(ref _freezeHP, value))
            {
                foreach (var c in Party) c.FreezeHP = value;
                Status = value ? "HP frozen." : "HP freeze OFF.";
            }
        }
    }

    // --- commands -----------------------------------------------------------
    public ICommand RefreshProcessesCommand { get; }
    public ICommand AttachCommand { get; }
    public ICommand DetachCommand { get; }
    public ICommand ScanCommand { get; }
    public ICommand HealCommand { get; }
    public ICommand MaxCharacteristicsCommand { get; }
    public ICommand MaxEverythingCommand { get; }

    public MainViewModel()
    {
        RefreshProcessesCommand = new RelayCommand(_ => RefreshProcesses());
        AttachCommand = new RelayCommand(_ => Attach(), _ => SelectedProcess != null && !IsAttached);
        DetachCommand = new RelayCommand(_ => Detach(), _ => IsAttached);
        ScanCommand = new RelayCommand(_ => Scan(), _ => IsAttached && !IsScanning);
        HealCommand = new RelayCommand(_ => Heal(), _ => Party.Count > 0);
        MaxCharacteristicsCommand = new RelayCommand(_ => ForEach(c => c.MaxCharacteristics()), _ => Party.Count > 0);
        MaxEverythingCommand = new RelayCommand(_ => ForEach(c => c.MaxEverything()), _ => Party.Count > 0);

        _poll = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
        _poll.Tick += (_, _) => PollTick();

        RefreshProcesses();
        TryAutoAttach();
    }

    /// <summary>On startup, attach automatically when the pre-selected process looks like a game emulator.</summary>
    private void TryAutoAttach()
    {
        if (!IsAttached && SelectedProcess?.IsEmulator == true) Attach();
    }

    // --- process management -------------------------------------------------
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
                bool emu = GameFacts.EmulatorHints.Any(h => name.Contains(h, StringComparison.OrdinalIgnoreCase));
                list.Add(new ProcessEntry(p.Id, name, emu));
            }
            catch { }
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
            _source = new ProcessMemorySource(_mem);
            OnPropertyChanged(nameof(IsAttached));
            RaiseCommands();
            _poll.Start();
            Status = $"Attached to {SelectedProcess.Name} (pid {SelectedProcess.Id}). Scanning for the character…";
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
        _scanCts = null;
        _source = null;
        _mem?.Dispose();
        _mem = null;
        Party.Clear();
        SelectedCharacter = null;
        _freezeHP = false; OnPropertyChanged(nameof(FreezeHP));
        IsScanning = false;
        OnPropertyChanged(nameof(IsAttached));
        RaiseCommands();
        Status = "Detached.";
    }

    // --- scanning -----------------------------------------------------------
    private async void Scan()
    {
        if (_source == null || IsScanning) return;
        IsScanning = true;
        Status = "Scanning memory for the character…";
        _scanCts?.Dispose();
        _scanCts = new CancellationTokenSource();
        var ct = _scanCts.Token;
        var source = _source;
        try
        {
            var found = await Task.Run(() => CharacterLocator.Find(source, ct), ct);
            if (source != _source) return;
            Party.Clear();
            if (found != null)
                Party.Add(new CharacterViewModel(this, found));
            SelectedCharacter = Party.FirstOrDefault();
            if (FreezeHP) foreach (var c in Party) c.FreezeHP = true;
            Status = Party.Count == 0
                ? "No character found. Make sure the game is loaded (past the title screen), then Re-scan."
                : $"Found character: {Party[0].Record.Name}.";
        }
        catch (OperationCanceledException) { if (source == _source) Status = "Scan cancelled."; }
        catch (Exception ex) { if (source == _source) Status = "Scan error: " + ex.Message; }
        finally { IsScanning = false; RaiseCommands(); }
    }

    // --- actions ------------------------------------------------------------
    private void ForEach(Action<CharacterViewModel> action)
    {
        foreach (var c in Party) action(c);
        Status = "Applied.";
    }

    public void Heal()
    {
        foreach (var c in Party) c.FullHeal();
        Status = "Character healed.";
    }

    // --- poll loop ----------------------------------------------------------
    private readonly byte[] _pollBuf = new byte[CharacterFormat.RecordSize];

    private void PollTick()
    {
        if (_source == null) return;
        foreach (var c in Party)
        {
            if (!CharacterLocator.Reread(_source, c.Address, _pollBuf)) continue;
            if (!CharacterRecord.IsValidRecord(_pollBuf, 0)) continue;
            c.RefreshLiveSummary(_pollBuf);
            c.ApplyFreeze();
        }
    }

    // --- ICharacterHost -----------------------------------------------------
    bool ICharacterHost.WriteBytes(nuint recordAddress, byte[] source, int offset, int length)
        => _mem?.WriteRange(recordAddress, source, offset, length) ?? false;

    private void RaiseCommands()
    {
        (AttachCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (DetachCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ScanCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (HealCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (MaxCharacteristicsCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (MaxEverythingCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    public void Dispose()
    {
        _poll.Stop();
        _scanCts?.Cancel();
        _scanCts?.Dispose();
        _source = null;
        _mem?.Dispose();
    }
}

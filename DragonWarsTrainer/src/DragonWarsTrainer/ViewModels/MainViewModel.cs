using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Input;
using System.Windows.Threading;
using DragonWarsTrainer.Game;
using DragonWarsTrainer.Memory;

namespace DragonWarsTrainer.ViewModels;

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
/// Root view-model: process attach/scan, the located party list, the freeze poll loop, and
/// the party-wide quick actions.
/// </summary>
public sealed class MainViewModel : ObservableObject, ICharacterHost, IDisposable
{
    private static readonly string[] EmulatorHints =
        { "dosbox", "dosbox-x", "dosbox-staging", "scummvm", "pcem", "86box", "qemu", "boxer" };

    private ProcessMemory? _mem;
    private readonly DispatcherTimer _poll;
    private CancellationTokenSource? _scanCts;

    public ObservableCollection<ProcessEntry> Processes { get; } = new();
    public ObservableCollection<CharacterViewModel> Party { get; } = new();

    public ReferenceViewModel Reference { get; } = new();
    public MapsViewModel Maps { get; }

    private ProcessEntry? _selectedProcess;
    public ProcessEntry? SelectedProcess { get => _selectedProcess; set { SetField(ref _selectedProcess, value); RaiseCommands(); } }

    private CharacterViewModel? _selectedCharacter;
    public CharacterViewModel? SelectedCharacter { get => _selectedCharacter; set => SetField(ref _selectedCharacter, value); }

    public bool IsAttached => _mem is { IsOpen: true };

    private bool _isScanning;
    public bool IsScanning { get => _isScanning; set { SetField(ref _isScanning, value); RaiseCommands(); } }

    private string _status = "Launch Dragon Wars in DOSBox, then pick the process and Attach.";
    public string Status { get => _status; set => SetField(ref _status, value); }

    // --- party-wide freeze toggles ------------------------------------------
    private bool _freezeHealth;
    public bool FreezeHealth
    {
        get => _freezeHealth;
        set { if (SetField(ref _freezeHealth, value)) { foreach (var c in Party) c.FreezeHealth = value; Status = value ? "Health frozen for the party." : "Health freeze OFF."; } }
    }

    private bool _freezeStun;
    public bool FreezeStun
    {
        get => _freezeStun;
        set { if (SetField(ref _freezeStun, value)) { foreach (var c in Party) c.FreezeStun = value; Status = value ? "Stun frozen for the party." : "Stun freeze OFF."; } }
    }

    private bool _freezePower;
    public bool FreezePower
    {
        get => _freezePower;
        set { if (SetField(ref _freezePower, value)) { foreach (var c in Party) c.FreezePower = value; Status = value ? "Power frozen for the party." : "Power freeze OFF."; } }
    }

    private bool _freezeStatus;
    public bool FreezeStatus
    {
        get => _freezeStatus;
        set { if (SetField(ref _freezeStatus, value)) { foreach (var c in Party) c.FreezeStatus = value; Status = value ? "Status frozen for the party (no death or stun)." : "Status freeze OFF."; } }
    }

    private bool _freezePoints;
    public bool FreezePoints
    {
        get => _freezePoints;
        set { if (SetField(ref _freezePoints, value)) { foreach (var c in Party) c.FreezePoints = value; Status = value ? "Points to allocate frozen for the party." : "Points freeze OFF."; } }
    }

    // --- commands ------------------------------------------------------------
    public ICommand RefreshProcessesCommand { get; }
    public ICommand AttachCommand { get; }
    public ICommand DetachCommand { get; }
    public ICommand ScanCommand { get; }
    public ICommand HealPartyCommand { get; }
    public ICommand MaxPartyCommand { get; }
    public ICommand MaxEverythingPartyCommand { get; }
    public ICommand MaxMoneyPartyCommand { get; }
    public ICommand LearnSpellsPartyCommand { get; }

    public MainViewModel()
    {
        RefreshProcessesCommand = new RelayCommand(_ => RefreshProcesses());
        AttachCommand = new RelayCommand(_ => Attach(), _ => SelectedProcess != null && !IsAttached);
        DetachCommand = new RelayCommand(_ => Detach(), _ => IsAttached);
        ScanCommand = new RelayCommand(_ => Scan(), _ => IsAttached && !IsScanning);
        HealPartyCommand = new RelayCommand(_ => HealParty(), _ => Party.Count > 0);
        MaxPartyCommand = new RelayCommand(_ => ForEachParty(c => c.MaxAttributes()), _ => Party.Count > 0);
        MaxEverythingPartyCommand = new RelayCommand(_ => ForEachParty(c => c.MaxEverything()), _ => Party.Count > 0);
        MaxMoneyPartyCommand = new RelayCommand(_ => ForEachParty(c => c.MaxMoney()), _ => Party.Count > 0);
        LearnSpellsPartyCommand = new RelayCommand(_ => ForEachParty(c => c.LearnAllSpells()), _ => Party.Count > 0);

        Maps = new MapsViewModel(() => _mem);

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
            Maps.OnAttached();
            _poll.Start();
            Status = $"Attached to {SelectedProcess.Name} (pid {SelectedProcess.Id}). Scanning for the party…";
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
        Party.Clear();
        SelectedCharacter = null;
        Maps.OnDetached();
        _freezeHealth = false; OnPropertyChanged(nameof(FreezeHealth));
        _freezeStun = false; OnPropertyChanged(nameof(FreezeStun));
        _freezePower = false; OnPropertyChanged(nameof(FreezePower));
        _freezeStatus = false; OnPropertyChanged(nameof(FreezeStatus));
        _freezePoints = false; OnPropertyChanged(nameof(FreezePoints));
        OnPropertyChanged(nameof(IsAttached));
        RaiseCommands();
        Status = "Detached.";
    }

    // --- scanning ------------------------------------------------------------
    private async void Scan()
    {
        if (_mem == null || IsScanning) return;
        IsScanning = true;
        Status = "Scanning memory for the party roster…";
        _scanCts?.Dispose();
        _scanCts = new CancellationTokenSource();
        var ct = _scanCts.Token;
        var mem = _mem;
        try
        {
            var found = await Task.Run(() => RosterLocator.FindAll(mem, ct), ct);
            if (mem != _mem) return;   // detached/re-attached while scanning
            Party.Clear();
            foreach (var lc in found)
                Party.Add(new CharacterViewModel(this, lc));
            SelectedCharacter = Party.FirstOrDefault();
            if (FreezeHealth) foreach (var c in Party) c.FreezeHealth = true;
            if (FreezeStun) foreach (var c in Party) c.FreezeStun = true;
            if (FreezePower) foreach (var c in Party) c.FreezePower = true;
            if (FreezeStatus) foreach (var c in Party) c.FreezeStatus = true;
            if (FreezePoints) foreach (var c in Party) c.FreezePoints = true;
            Status = Party.Count == 0
                ? "No party found. Make sure characters are loaded (past the title screen), then Re-scan."
                : $"Found {Party.Count} character(s).";
        }
        catch (OperationCanceledException) { Status = "Scan cancelled."; }
        catch (Exception ex) { Status = "Scan error: " + ex.Message; }
        finally { IsScanning = false; RaiseCommands(); }
    }

    // --- party-wide actions --------------------------------------------------
    private void ForEachParty(Action<CharacterViewModel> action)
    {
        foreach (var c in Party) action(c);
        Status = "Applied to the whole party.";
    }

    public void HealParty()
    {
        foreach (var c in Party) c.FullHeal();
        Status = "Party healed.";
    }

    // --- poll loop -----------------------------------------------------------
    private readonly byte[] _pollBuf = new byte[RosterFormat.RecordSize];

    private void PollTick()
    {
        if (_mem == null) return;
        foreach (var c in Party)
        {
            // Re-read the live bytes first, then freeze, so the freeze re-pins against this tick's
            // vitals rather than last tick's — a hit that dropped HP is corrected now, not up to one
            // poll interval later (which could miss a death the freeze was meant to prevent).
            if (RosterLocator.Reread(_mem, c.Address, _pollBuf)) c.RefreshLiveSummary(_pollBuf);
            c.ApplyFreeze();
        }
        Maps.Tick();
    }

    // --- ICharacterHost ------------------------------------------------------
    bool ICharacterHost.WriteBytes(nuint recordAddress, byte[] source, int offset, int length)
        => _mem?.WriteRange(recordAddress, source, offset, length) ?? false;

    private void RaiseCommands()
    {
        (AttachCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (DetachCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ScanCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (HealPartyCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (MaxPartyCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (MaxEverythingPartyCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (MaxMoneyPartyCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (LearnSpellsPartyCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    public void Dispose()
    {
        _poll.Stop();
        _scanCts?.Cancel();
        Maps.OnDetached();   // cancel any in-flight map snapshot/narrow before the process is disposed
        _scanCts?.Dispose();
        _mem?.Dispose();
    }
}

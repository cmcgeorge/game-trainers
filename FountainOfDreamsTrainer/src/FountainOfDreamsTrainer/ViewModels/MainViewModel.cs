using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Input;
using System.Windows.Threading;
using FountainOfDreamsTrainer.Game;
using FountainOfDreamsTrainer.Memory;

namespace FountainOfDreamsTrainer.ViewModels;

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
/// Root view-model: process attach/scan, the located party list, the freeze poll loop, and the
/// party-wide quick actions.
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

    private string _status = "Launch Fountain of Dreams in DOSBox, then pick the process and Attach.";
    public string Status { get => _status; set => SetField(ref _status, value); }

    // --- party-wide freeze toggles ------------------------------------------
    private bool _freezeHealth;
    public bool FreezeHealth
    {
        get => _freezeHealth;
        set
        {
            if (SetField(ref _freezeHealth, value))
            {
                foreach (var c in Party) c.FreezeHealth = value;
                Status = value ? "Health (CON) frozen for the party." : "Health freeze OFF.";
            }
        }
    }

    // --- commands ------------------------------------------------------------
    public ICommand RefreshProcessesCommand { get; }
    public ICommand AttachCommand { get; }
    public ICommand DetachCommand { get; }
    public ICommand ScanCommand { get; }
    public ICommand HealPartyCommand { get; }
    public ICommand MaxAttributesPartyCommand { get; }
    public ICommand MaxMoneyPartyCommand { get; }
    public ICommand MaxEverythingPartyCommand { get; }

    public MainViewModel()
    {
        RefreshProcessesCommand = new RelayCommand(_ => RefreshProcesses());
        AttachCommand = new RelayCommand(_ => Attach(), _ => SelectedProcess != null && !IsAttached);
        DetachCommand = new RelayCommand(_ => Detach(), _ => IsAttached);
        ScanCommand = new RelayCommand(_ => Scan(), _ => IsAttached && !IsScanning);
        HealPartyCommand = new RelayCommand(_ => HealParty(), _ => Party.Count > 0);
        MaxAttributesPartyCommand = new RelayCommand(_ => ForEachParty(c => c.MaxAttributes()), _ => Party.Count > 0);
        MaxMoneyPartyCommand = new RelayCommand(_ => ForEachParty(c => c.MaxMoney()), _ => Party.Count > 0);
        MaxEverythingPartyCommand = new RelayCommand(_ => ForEachParty(c => c.MaxEverything()), _ => Party.Count > 0);

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
                bool emu = GameFacts.EmulatorHints.Any(h => name.Contains(h, StringComparison.OrdinalIgnoreCase));
                list.Add(new ProcessEntry(p.Id, name, emu));
            }
            catch { }
            finally { p.Dispose(); }
        }
        foreach (var e in list.OrderByDescending(e => e.IsEmulator)
                     .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase))
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
            Status = $"Attached to {SelectedProcess.Name} (pid {SelectedProcess.Id}). Scanning for the party…";
            Scan();
        }
        catch (Exception ex)
        {
            _poll.Stop();
            _source = null;
            _mem?.Dispose();
            _mem = null;
            OnPropertyChanged(nameof(IsAttached));
            RaiseCommands();
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
        Party.Clear();
        SelectedCharacter = null;
        _freezeHealth = false; OnPropertyChanged(nameof(FreezeHealth));
        OnPropertyChanged(nameof(IsAttached));
        RaiseCommands();
        Status = "Detached.";
    }

    // --- scanning ------------------------------------------------------------
    private int _scanGen;

    private async void Scan()
    {
        if (_source == null) return;
        _scanCts?.Cancel();
        _scanCts?.Dispose();
        _scanCts = new CancellationTokenSource();
        var ct = _scanCts.Token;
        int gen = ++_scanGen;
        IsScanning = true;
        Status = "Scanning memory for the party roster…";
        var source = _source;
        try
        {
            var found = await Task.Run(() => PartyLocator.Find(source, ct), ct);
            if (source != _source || gen != _scanGen) return;
            Party.Clear();
            if (found != null)
            {
                foreach (var lc in found.Members)
                    Party.Add(new CharacterViewModel(this, lc));
            }
            SelectedCharacter = Party.FirstOrDefault();
            if (FreezeHealth) foreach (var c in Party) c.FreezeHealth = true;
            Status = Party.Count == 0
                ? "No party found. Make sure characters are loaded (past the title screen), then Re-scan."
                : $"Found {Party.Count} character(s).";
        }
        catch (OperationCanceledException) { if (gen == _scanGen && IsAttached) Status = "Scan cancelled."; }
        catch (Exception ex) { if (gen == _scanGen) Status = "Scan error: " + ex.Message; }
        finally { if (gen == _scanGen) { IsScanning = false; RaiseCommands(); } }
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
    private readonly byte[] _pollBuf = new byte[CharacterFormat.RecordSize];

    private void PollTick()
    {
        if (_source == null) return;
        foreach (var c in Party)
        {
            if (PartyLocator.Reread(_source, c.Address, _pollBuf))
            {
                c.RefreshLiveSummary(_pollBuf);
                c.ApplyFreeze();
            }
        }
    }

    // --- ICharacterHost ------------------------------------------------------
    bool ICharacterHost.IsAttached => _mem is { IsOpen: true };

    bool ICharacterHost.WriteBytes(nuint recordAddress, byte[] source, int offset, int length) =>
        _mem?.WriteRange(recordAddress, source, offset, length) ?? false;

    private void RaiseCommands()
    {
        (AttachCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (DetachCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ScanCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (HealPartyCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (MaxAttributesPartyCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (MaxMoneyPartyCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (MaxEverythingPartyCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    public void Dispose()
    {
        _poll.Stop();
        _scanCts?.Cancel();
        _scanCts?.Dispose();
        _mem?.Dispose();
    }
}

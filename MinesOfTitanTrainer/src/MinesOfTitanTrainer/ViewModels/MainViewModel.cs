using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Input;
using System.Windows.Threading;
using MinesOfTitanTrainer.Game;
using MinesOfTitanTrainer.Memory;

namespace MinesOfTitanTrainer.ViewModels;

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
    private static readonly string[] EmulatorHints =
        { "dosbox", "dosbox-x", "dosbox-staging", "scummvm", "pcem", "86box", "qemu", "boxer" };

    private ProcessMemory? _mem;
    private readonly DispatcherTimer _poll;
    private CancellationTokenSource? _scanCts;

    public ObservableCollection<ProcessEntry> Processes { get; } = new();
    public ObservableCollection<CharacterViewModel> Party { get; } = new();

    private ProcessEntry? _selectedProcess;
    public ProcessEntry? SelectedProcess { get => _selectedProcess; set { SetField(ref _selectedProcess, value); RaiseCommands(); } }

    private CharacterViewModel? _selectedCharacter;
    public CharacterViewModel? SelectedCharacter { get => _selectedCharacter; set => SetField(ref _selectedCharacter, value); }

    public bool IsAttached => _mem is { IsOpen: true };

    private bool _isScanning;
    public bool IsScanning { get => _isScanning; set { SetField(ref _isScanning, value); RaiseCommands(); } }

    private string _status = "Launch Mines of Titan in DOSBox, then pick the process and Attach.";
    public string Status { get => _status; set => SetField(ref _status, value); }

    // --- party-wide freeze toggle -------------------------------------------
    private bool _freezeCredits;
    public bool FreezeCredits
    {
        get => _freezeCredits;
        set { if (SetField(ref _freezeCredits, value)) { foreach (var c in Party) c.FreezeCredits = value; Status = value ? "Credits frozen for the party." : "Credits freeze OFF."; } }
    }

    // --- commands ------------------------------------------------------------
    public ICommand RefreshProcessesCommand { get; }
    public ICommand AttachCommand { get; }
    public ICommand DetachCommand { get; }
    public ICommand ScanCommand { get; }
    public ICommand MaxAttributesPartyCommand { get; }
    public ICommand MaxSkillsPartyCommand { get; }
    public ICommand MaxCreditsPartyCommand { get; }
    public ICommand MaxEverythingPartyCommand { get; }

    public MainViewModel()
    {
        RefreshProcessesCommand = new RelayCommand(_ => RefreshProcesses());
        AttachCommand = new RelayCommand(_ => Attach(), _ => SelectedProcess != null && !IsAttached);
        DetachCommand = new RelayCommand(_ => Detach(), _ => IsAttached);
        ScanCommand = new RelayCommand(_ => Scan(), _ => IsAttached && !IsScanning);
        MaxAttributesPartyCommand = new RelayCommand(_ => ForEachParty(c => c.MaxAttributes()), _ => Party.Count > 0);
        MaxSkillsPartyCommand = new RelayCommand(_ => ForEachParty(c => c.MaxSkills()), _ => Party.Count > 0);
        MaxCreditsPartyCommand = new RelayCommand(_ => ForEachParty(c => c.MaxCredits()), _ => Party.Count > 0);
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
        _freezeCredits = false; OnPropertyChanged(nameof(FreezeCredits));
        OnPropertyChanged(nameof(IsAttached));
        RaiseCommands();
        Status = "Detached.";
    }

    // --- scanning ------------------------------------------------------------
    private async void Scan()
    {
        if (_mem == null || IsScanning) return;
        IsScanning = true;
        Status = "Scanning memory for the party…";
        _scanCts?.Dispose();
        _scanCts = new CancellationTokenSource();
        var ct = _scanCts.Token;
        var mem = _mem;
        try
        {
            var found = await Task.Run(() => PartyLocator.FindAll(mem, ct), ct);
            if (mem != _mem) return;   // detached/re-attached while scanning
            Party.Clear();
            foreach (var lc in found)
                Party.Add(new CharacterViewModel(this, lc));
            SelectedCharacter = Party.FirstOrDefault();
            if (FreezeCredits) foreach (var c in Party) c.FreezeCredits = true;
            Status = Party.Count == 0
                ? "No party found. Load a saved game (past the title screen), then Re-scan."
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

    // --- poll loop -----------------------------------------------------------
    private readonly byte[] _pollBuf = new byte[CharacterFormat.RecordSize];

    private void PollTick()
    {
        if (_mem == null) return;
        foreach (var c in Party)
        {
            c.ApplyFreeze();
            if (PartyLocator.Reread(_mem, c.Address, _pollBuf)) c.RefreshLiveSummary(_pollBuf);
        }
    }

    // --- ICharacterHost ------------------------------------------------------
    bool ICharacterHost.WriteBytes(nuint recordAddress, byte[] source, int offset, int length)
        => _mem?.WriteRange(recordAddress, source, offset, length) ?? false;

    private void RaiseCommands()
    {
        (AttachCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (DetachCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ScanCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (MaxAttributesPartyCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (MaxSkillsPartyCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (MaxCreditsPartyCommand as RelayCommand)?.RaiseCanExecuteChanged();
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

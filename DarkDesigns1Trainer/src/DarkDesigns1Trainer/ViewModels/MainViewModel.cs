using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Input;
using System.Windows.Threading;
using DarkDesigns1Trainer.Game;
using DarkDesigns1Trainer.Memory;

namespace DarkDesigns1Trainer.ViewModels;

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
/// Root view-model: process attach/scan, the located party list, the freeze poll loop,
/// the party-wide quick actions, and the offline save editor.
/// </summary>
public sealed class MainViewModel : ObservableObject, ICharacterHost, IDisposable
{
    private static readonly string[] EmulatorHints =
        { "dosbox", "dosbox-x", "dosbox-staging", "scummvm", "pcem", "86box", "qemu", "boxer" };

    private ProcessMemory? _mem;
    private readonly DispatcherTimer _poll;
    private CancellationTokenSource? _scanCts;

    // The pid we actually attached to. SelectedProcess can be changed in the dropdown afterwards,
    // and the roller has to send keystrokes to the process it is reading memory from.
    private int? _attachedPid;

    public ObservableCollection<ProcessEntry> Processes { get; } = new();
    public ObservableCollection<CharacterViewModel> Party { get; } = new();
    public ReferenceViewModel Reference { get; } = new();

    /// <summary>The create-screen roller: locates the rolled stat pool, re-rolls, and can write it.</summary>
    public CharacterRollerViewModel Roller { get; }

    private ProcessEntry? _selectedProcess;
    public ProcessEntry? SelectedProcess { get => _selectedProcess; set { SetField(ref _selectedProcess, value); RaiseCommands(); } }

    private CharacterViewModel? _selectedCharacter;
    public CharacterViewModel? SelectedCharacter { get => _selectedCharacter; set => SetField(ref _selectedCharacter, value); }

    public bool IsAttached => _mem is { IsOpen: true };

    private bool _isScanning;
    public bool IsScanning { get => _isScanning; set { SetField(ref _isScanning, value); RaiseCommands(); } }

    private string _status = "Launch Dark Designs I in DOSBox, then pick the process and Attach.";
    public string Status { get => _status; set => SetField(ref _status, value); }

    // --- party-wide freeze toggles ------------------------------------------
    private bool _freezeBody;
    public bool FreezeBody
    {
        get => _freezeBody;
        set { if (SetField(ref _freezeBody, value)) { foreach (var c in Party) c.FreezeBody = value; Status = value ? "Body frozen for the party." : "Body freeze OFF."; } }
    }

    private bool _freezeMagic;
    public bool FreezeMagic
    {
        get => _freezeMagic;
        set { if (SetField(ref _freezeMagic, value)) { foreach (var c in Party) c.FreezeMagic = value; Status = value ? "Magic frozen for the party." : "Magic freeze OFF."; } }
    }

    private bool _freezeStatus;
    public bool FreezeStatus
    {
        get => _freezeStatus;
        set { if (SetField(ref _freezeStatus, value)) { foreach (var c in Party) c.FreezeStatus = value; Status = value ? "Status frozen for the party." : "Status freeze OFF."; } }
    }

    // --- save editor ---------------------------------------------------------
    private string? _saveFilePath;
    public string? SaveFilePath { get => _saveFilePath; set => SetField(ref _saveFilePath, value); }

    private SaveFile? _saveFile;
    public SaveFile? SaveFile { get => _saveFile; set => SetField(ref _saveFile, value); }

    public ObservableCollection<CharacterRecord> SaveCharacters { get; } = new();

    private CharacterRecord? _selectedSaveCharacter;
    public CharacterRecord? SelectedSaveCharacter { get => _selectedSaveCharacter; set => SetField(ref _selectedSaveCharacter, value); }

    // --- commands ------------------------------------------------------------
    public ICommand RefreshProcessesCommand { get; }
    public ICommand AttachCommand { get; }
    public ICommand DetachCommand { get; }
    public ICommand ScanCommand { get; }
    public ICommand HealPartyCommand { get; }
    public ICommand MaxPartyCommand { get; }
    public ICommand MaxEverythingPartyCommand { get; }
    public ICommand MaxMoneyPartyCommand { get; }
    public ICommand LoadSaveCommand { get; }
    public ICommand SaveSaveCommand { get; }
    public ICommand SaveMaxAllCommand { get; }

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
        LoadSaveCommand = new RelayCommand(_ => LoadSave(), _ => true);
        SaveSaveCommand = new RelayCommand(_ => SaveSave(), _ => SaveFile != null);
        SaveMaxAllCommand = new RelayCommand(_ => SaveMaxAll(), _ => SaveFile != null);

        Roller = new CharacterRollerViewModel(() => _mem, () => _attachedPid, msg => Status = msg);

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
            _attachedPid = SelectedProcess.Id;
            OnPropertyChanged(nameof(IsAttached));
            RaiseCommands();
            Roller.RefreshCommands();
            _poll.Start();
            Status = $"Attached to {SelectedProcess.Name} (pid {SelectedProcess.Id}). Scanning for characters…";
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
        Roller.Reset();       // its locked address belongs to the process we're letting go of
        _mem?.Dispose();
        _mem = null;
        _attachedPid = null;
        Party.Clear();
        SelectedCharacter = null;
        _freezeBody = false; OnPropertyChanged(nameof(FreezeBody));
        _freezeMagic = false; OnPropertyChanged(nameof(FreezeMagic));
        _freezeStatus = false; OnPropertyChanged(nameof(FreezeStatus));
        OnPropertyChanged(nameof(IsAttached));
        RaiseCommands();
        Roller.RefreshCommands();
        Status = "Detached.";
    }

    // --- scanning ------------------------------------------------------------
    private async void Scan()
    {
        if (_mem == null || IsScanning) return;
        IsScanning = true;
        Status = "Scanning memory for the character roster…";
        _scanCts?.Dispose();
        _scanCts = new CancellationTokenSource();
        var ct = _scanCts.Token;
        var mem = _mem;
        try
        {
            var found = await Task.Run(() => RosterLocator.FindAll(mem, ct), ct);
            if (mem != _mem) return;
            Party.Clear();
            foreach (var lc in found)
                Party.Add(new CharacterViewModel(this, lc));
            SelectedCharacter = Party.FirstOrDefault();
            if (FreezeBody) foreach (var c in Party) c.FreezeBody = true;
            if (FreezeMagic) foreach (var c in Party) c.FreezeMagic = true;
            if (FreezeStatus) foreach (var c in Party) c.FreezeStatus = true;
            Status = Party.Count == 0
                ? "No characters found. Make sure the game is loaded (past the title screen), then Re-scan."
                : $"Found {Party.Count} character(s).";
        }
        catch (OperationCanceledException) { if (mem == _mem) Status = "Scan cancelled."; }
        catch (Exception ex) { if (mem == _mem) Status = "Scan error: " + ex.Message; }
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
    private readonly byte[] _pollBuf = new byte[CharacterFormat.RecordSize];

    private void PollTick()
    {
        if (_mem == null) return;
        foreach (var c in Party)
        {
            if (RosterLocator.Reread(_mem, c.Address, _pollBuf)) c.RefreshLiveSummary(_pollBuf);
            c.ApplyFreeze();
        }
    }

    // --- save editor ---------------------------------------------------------
    public void LoadSave()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Open DDCHARS.DAT",
            Filter = "Dark Designs character file|DDCHARS.DAT|All files|*.*",
            FileName = "DDCHARS.DAT",
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            SaveFile?.Dispose();
            SaveFile = new SaveFile(dlg.FileName);
            SaveFilePath = dlg.FileName;
            SaveCharacters.Clear();
            foreach (var c in SaveFile.OccupiedCharacters)
                SaveCharacters.Add(c);
            SelectedSaveCharacter = SaveCharacters.FirstOrDefault();
            Status = SaveCharacters.Count == 0
                ? $"Loaded {dlg.FileName} — no characters found."
                : $"Loaded {dlg.FileName} — {SaveCharacters.Count} character(s).";
            RaiseCommands();
        }
        catch (Exception ex)
        {
            Status = "Load failed: " + ex.Message;
        }
    }

    public void SaveSave()
    {
        if (SaveFile == null) return;
        try
        {
            SaveFile.Save();
            Status = $"Saved to {SaveFilePath}.";
        }
        catch (Exception ex)
        {
            Status = "Save failed: " + ex.Message;
        }
    }

    public void SaveMaxAll()
    {
        if (SaveFile == null) return;
        foreach (var c in SaveFile.Characters)
        {
            if (!c.IsOccupied) continue;
            for (int i = 0; i < CharacterFormat.AttributeCount; i++)
                c.SetAttribute(i, CharacterFormat.MaxAttribute);
            c.BodyMax = CharacterFormat.MaxVital;
            c.BodyCurrent = CharacterFormat.MaxVital;
            c.MagicCurrent = CharacterFormat.MaxVital;
            c.Level = CharacterFormat.MaxLevel;
            c.Experience = CharacterFormat.MaxExperience;
            c.Gold = CharacterFormat.MaxGold;
            c.Status = CharacterFormat.StatusFine;
        }
        SaveFile.MarkModified();
        SaveFile.Save();
        SaveCharacters.Clear();
        foreach (var c in SaveFile.OccupiedCharacters)
            SaveCharacters.Add(c);
        SelectedSaveCharacter = SaveCharacters.FirstOrDefault();
        Status = "Maxed all characters and saved.";
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
        (SaveSaveCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (SaveMaxAllCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    public void Dispose()
    {
        _poll.Stop();
        // Cancels the roll loop; it can still be mid-keystroke, so one last R may reach the game
        // before it notices. Memory access stays safe either way (ProcessMemory holds a
        // SafeProcessHandle), so this isn't worth blocking shutdown on.
        Roller.Reset();
        _scanCts?.Cancel();
        _scanCts?.Dispose();
        _mem?.Dispose();
        SaveFile?.Dispose();
    }
}

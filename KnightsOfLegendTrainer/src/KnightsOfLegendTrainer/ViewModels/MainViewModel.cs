using System.Diagnostics;
using System.Windows.Input;
using System.Windows.Threading;

namespace KnightsOfLegendTrainer.ViewModels;

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
/// Root view-model for the Knights of Legend trainer. Knights of Legend (Origin Systems,
/// 1989, by Todd Porter) is a party-based DOS RPG that runs under DOSBox / DOSBox-X.
///
/// No game binary or memory dumps were available for static analysis, so the trainer has
/// no GameLocator — it drives Common's <see cref="MemorySearcher"/> as a Cheat-Engine-style
/// value scanner with guided scan recipes for gold, adventure points, body points, the
/// seven primary statistics, level, fatigue, and rations. It also includes a chardata
/// save-file editor for quest status.
/// </summary>
public sealed class MainViewModel : ObservableObject, IScanHost, IDisposable
{
    private const int MaxResultRows = 1000;
    private const int LiveRefreshThreshold = 200;

    private ProcessMemory? _mem;
    private MemorySearcher? _searcher;
    private readonly DispatcherTimer _poll;
    private CancellationTokenSource? _scanCts;
    private int _targetPid;
    private Process? _target;

    private string _pendingPinLabel = "";

    public ObservableCollection<ProcessEntry> Processes { get; } = new();
    public ObservableCollection<ScanResultViewModel> Results { get; } = new();
    public ObservableCollection<FrozenValueViewModel> Frozen { get; } = new();
    public ObservableCollection<ScanRecipe> Recipes { get; } = new(ScanGuide.Recipes);
    public ObservableCollection<RaceEntry> Races { get; } = new(RaceBook.Races);
    public ObservableCollection<ClassEntry> Classes { get; } = new(ClassBook.Classes);
    public ObservableCollection<WeaponEntry> Weapons { get; } = new(WeaponBook.Weapons);
    public ObservableCollection<ArmorEntry> Armor { get; } = new(ArmorBook.Armor);
    public ObservableCollection<SpellEntry> Spells { get; } = new(SpellBook.Spells);
    public ObservableCollection<MonsterEntry> Monsters { get; } = new(MonsterBook.Monsters);
    public ObservableCollection<QuestEntry> Quests { get; } = new(QuestBook.Quests);
    public ObservableCollection<MagicOrder> MagicOrders { get; } = new(MagicOrderBook.Orders);

    public SaveEditorViewModel SaveEditor { get; } = new();
    public MapsViewModel Maps { get; } = new();
    public CluebookViewModel Cluebook { get; } = new();

    public IReadOnlyList<ScanWidth> Widths { get; } = new[] { ScanWidth.Byte, ScanWidth.Int16, ScanWidth.Int32 };

    private ScanWidth _selectedWidth = ScanWidth.Int32;
    public ScanWidth SelectedWidth
    {
        get => _selectedWidth;
        set { if (SetField(ref _selectedWidth, value)) NewScan(); }
    }

    private string _scanText = "";
    public string ScanText { get => _scanText; set => SetField(ref _scanText, value); }

    private ProcessEntry? _selectedProcess;
    public ProcessEntry? SelectedProcess { get => _selectedProcess; set { SetField(ref _selectedProcess, value); RaiseCommands(); } }

    private ScanResultViewModel? _selectedResult;
    public ScanResultViewModel? SelectedResult { get => _selectedResult; set { SetField(ref _selectedResult, value); RaiseCommands(); } }

    private FrozenValueViewModel? _selectedFrozen;
    public FrozenValueViewModel? SelectedFrozen { get => _selectedFrozen; set { SetField(ref _selectedFrozen, value); RaiseCommands(); } }

    private ScanRecipe? _selectedRecipe;
    public ScanRecipe? SelectedRecipe
    {
        get => _selectedRecipe;
        set { SetField(ref _selectedRecipe, value); if (value != null) ApplyRecipe(value); }
    }

    public bool IsAttached => _mem is { IsOpen: true };
    public bool HasResults => _searcher is { HasMatches: true };

    private bool _isScanning;
    public bool IsScanning
    {
        get => _isScanning;
        set { if (SetField(ref _isScanning, value)) { OnPropertyChanged(nameof(NotScanning)); RaiseCommands(); } }
    }

    public bool NotScanning => !_isScanning;

    private string _matchCountText = "";
    public string MatchCount { get => _matchCountText; private set => SetField(ref _matchCountText, value); }

    private string _status = "Launch Knights of Legend in DOSBox, pick the emulator process, and Attach.";
    public string Status { get => _status; set => SetField(ref _status, value); }

    private string _guideText = "";
    public string GuideText { get => _guideText; set => SetField(ref _guideText, value); }

    // ---- Commands -----------------------------------------------------------
    public ICommand RefreshProcessesCommand { get; }
    public ICommand AttachCommand { get; }
    public ICommand DetachCommand { get; }
    public ICommand FirstScanCommand { get; }
    public ICommand NextScanCommand { get; }
    public ICommand NewScanCommand { get; }
    public ICommand PinCommand { get; }
    public ICommand RemoveFrozenCommand { get; }
    public ICommand FreezeAllCommand { get; }
    public ICommand UnfreezeAllCommand { get; }
    public ICommand LoadSaveCommand { get; }
    public ICommand SaveSaveCommand { get; }
    public ICommand SetAllQuestsGivenCommand { get; }
    public ICommand SetAllQuestsCompleteCommand { get; }
    public ICommand SetAllQuestsMedalCommand { get; }

    public MainViewModel()
    {
        RefreshProcessesCommand = new RelayCommand(_ => RefreshProcesses());
        AttachCommand           = new RelayCommand(_ => Attach(),    _ => SelectedProcess != null && !IsAttached && !IsScanning);
        DetachCommand           = new RelayCommand(_ => Detach(),    _ => IsAttached && !IsScanning);

        FirstScanCommand        = new RelayCommand(_ => FirstScan(), _ => IsAttached && !IsScanning && !HasResults);
        NextScanCommand         = new RelayCommand(p => NextScan(p), _ => IsAttached && !IsScanning && HasResults);
        NewScanCommand          = new RelayCommand(_ => NewScan(),   _ => IsAttached && !IsScanning && HasResults);

        PinCommand          = new RelayCommand(_ => PinSelected(), _ => IsAttached && !IsScanning && SelectedResult != null);
        RemoveFrozenCommand = new RelayCommand(_ => RemoveFrozen(), _ => SelectedFrozen != null);
        FreezeAllCommand    = new RelayCommand(_ => SetAllFrozen(true),  _ => Frozen.Count > 0);
        UnfreezeAllCommand  = new RelayCommand(_ => SetAllFrozen(false), _ => Frozen.Count > 0);

        LoadSaveCommand             = new RelayCommand(_ => LoadSaveFile(), _ => true);
        SaveSaveCommand             = new RelayCommand(_ => SaveEditor.Save(), _ => SaveEditor.HasFile && SaveEditor.Dirty);
        SetAllQuestsGivenCommand    = new RelayCommand(_ => SaveEditor.SetAllQuests(SaveFormat.StatusGiven),    _ => SaveEditor.HasFile);
        SetAllQuestsCompleteCommand = new RelayCommand(_ => SaveEditor.SetAllQuests(SaveFormat.StatusComplete), _ => SaveEditor.HasFile);
        SetAllQuestsMedalCommand    = new RelayCommand(_ => SaveEditor.SetAllQuests(SaveFormat.StatusMedalGiven), _ => SaveEditor.HasFile);

        _poll = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _poll.Tick += (_, _) => PollTick();

        RefreshProcesses();
        TryAutoAttach();
    }

    /// <summary>On startup, attach automatically when the pre-selected process looks like a game emulator.</summary>
    private void TryAutoAttach()
    {
        if (!IsAttached && SelectedProcess?.IsEmulator == true) Attach();
    }

    // ---- process management -------------------------------------------------
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
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { }
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
            _targetPid = SelectedProcess.Id;
            _target = Process.GetProcessById(_targetPid);
            _searcher = new MemorySearcher(_mem, SelectedWidth);
            Results.Clear();
            OnPropertyChanged(nameof(IsAttached));
            OnPropertyChanged(nameof(HasResults));
            RaiseCommands();
            _poll.Start();
            Status = $"Attached to {SelectedProcess.Name} (pid {SelectedProcess.Id}). Pick a guided scan or enter a value.";
        }
        catch (Exception ex)
        {
            _mem?.Dispose();
            _mem = null;
            _searcher = null;
            _target?.Dispose();
            _target = null;
            OnPropertyChanged(nameof(IsAttached));
            RaiseCommands();
            Status = "Attach failed: " + ex.Message;
        }
    }

    private void Detach()
    {
        _poll.Stop();
        _scanCts?.Cancel();
        _scanCts?.Dispose();
        _scanCts = null;
        _mem?.Dispose();
        _mem = null;
        _searcher = null;
        _target?.Dispose();
        _target = null;
        _targetPid = 0;
        Results.Clear();
        Frozen.Clear();
        SelectedResult = null;
        SelectedFrozen = null;
        MatchCount = "";
        GuideText = "";
        OnPropertyChanged(nameof(IsAttached));
        OnPropertyChanged(nameof(HasResults));
        RaiseCommands();
        Status = "Detached.";
    }

    // ---- scanning -----------------------------------------------------------
    private async void FirstScan()
    {
        if (_searcher == null || IsScanning) return;
        bool hasValue = ScanValue.TryParse(ScanText, out long value);
        if (!hasValue && !string.IsNullOrWhiteSpace(ScanText))
        {
            Status = $"\"{ScanText}\" is not a valid number — leave the field blank for an unknown-value scan.";
            return;
        }
        if (hasValue && !ScanValue.FitsWidth(value, SelectedWidth))
        {
            Status = $"{value} does not fit a {SelectedWidth} scan — pick a wider type or a smaller value.";
            return;
        }

        long stored = ScanValue.Canonicalize(value, SelectedWidth);
        var searcher = _searcher;
        await RunScan(
            hasValue ? $"First scan for {value}…" : "First scan (unknown value)…",
            ct =>
            {
                if (hasValue) searcher.FirstScanExact(stored, ct);
                else searcher.FirstScanUnknown(ct);
            });
    }

    private async void NextScan(object? parameter)
    {
        if (_searcher == null || IsScanning) return;
        if (parameter is not ScanCompare compare && !Enum.TryParse(parameter?.ToString(), out compare))
            return;

        long value = 0;
        if (compare == ScanCompare.Exact)
        {
            if (!ScanValue.TryParse(ScanText, out value))
            {
                Status = "Enter a value for an Exact scan.";
                return;
            }
            if (!ScanValue.FitsWidth(value, SelectedWidth))
            {
                Status = $"{value} does not fit a {SelectedWidth} scan.";
                return;
            }
            value = ScanValue.Canonicalize(value, SelectedWidth);
        }

        var searcher = _searcher;
        await RunScan($"Narrowing ({compare})…", ct => searcher.NextScan(compare, value, ct));
    }

    private async Task RunScan(string message, Action<CancellationToken> work)
    {
        IsScanning = true;
        Status = message;
        _scanCts?.Dispose();
        _scanCts = new CancellationTokenSource();
        var ct = _scanCts.Token;
        var searcher = _searcher!;
        var mem = _mem;
        try
        {
            await Task.Run(() => work(ct), ct);
            if (mem != _mem || searcher != _searcher || ct.IsCancellationRequested) return;
            PublishResults(searcher);
        }
        catch (OperationCanceledException) { if (mem == _mem) Status = "Scan cancelled."; }
        catch (Exception ex) { if (mem == _mem) Status = "Scan error: " + ex.Message; }
        finally
        {
            IsScanning = false;
            OnPropertyChanged(nameof(HasResults));
            RaiseCommands();
        }
    }

    private void PublishResults(MemorySearcher searcher)
    {
        int count = searcher.MatchCount;
        Results.Clear();
        if (count >= 0)
        {
            foreach (var m in searcher.Take(MaxResultRows))
                Results.Add(new ScanResultViewModel(m.Address, m.Value));
        }

        string shown = count < 0
            ? "baseline captured — narrow with a comparison"
            : count > MaxResultRows ? $"{count:N0} matches (showing first {MaxResultRows:N0})"
            : $"{count:N0} match{(count == 1 ? "" : "es")}";
        MatchCount = shown + (searcher.Truncated ? " (coverage truncated)" : "");
        Status = $"Scan complete: {shown}.";
        SelectedResult = Results.FirstOrDefault();
    }

    private void NewScan()
    {
        _scanCts?.Cancel();
        _pendingPinLabel = "";
        if (_mem != null) _searcher = new MemorySearcher(_mem, SelectedWidth);
        Results.Clear();
        SelectedResult = null;
        MatchCount = "";
        GuideText = "";
        OnPropertyChanged(nameof(HasResults));
        RaiseCommands();
        if (IsAttached) Status = $"New {SelectedWidth} scan. Enter a value and First Scan.";
    }

    // ---- pin / freeze -------------------------------------------------------
    private void PinSelected()
    {
        var r = SelectedResult;
        if (r == null) return;
        if (Frozen.Any(f => f.Address == r.Address))
        {
            Status = $"{r.AddressHex} is already pinned.";
            return;
        }
        Frozen.Add(new FrozenValueViewModel(this, r.Address, SelectedWidth, r.Value, _pendingPinLabel));
        RaiseCommands();
        Status = $"Pinned {r.AddressHex}. Edit Target to poke a value, or tick Freeze to hold it.";
    }

    private void RemoveFrozen()
    {
        if (SelectedFrozen == null) return;
        Frozen.Remove(SelectedFrozen);
        SelectedFrozen = null;
        RaiseCommands();
    }

    private void SetAllFrozen(bool frozen)
    {
        foreach (var f in Frozen) f.Frozen = frozen;
        Status = frozen ? "All pinned values frozen." : "Freeze cleared on all pinned values.";
    }

    // ---- guided scans -------------------------------------------------------
    private void ApplyRecipe(ScanRecipe recipe)
    {
        if (_selectedWidth != recipe.Width) SelectedWidth = recipe.Width;
        else NewScan();
        _pendingPinLabel = recipe.Label;
        ScanText = recipe.SuggestedDefault.ToString();
        GuideText = recipe.Instructions;
        Status = $"{recipe.Label} guide loaded. Follow the instructions below.";
    }

    // ---- save editor --------------------------------------------------------
    private void LoadSaveFile()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Load chardata file",
            Filter = "chardata files|chardata|All files|*.*",
            CheckFileExists = true,
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            SaveEditor.Load(dlg.FileName);
            RaiseCommands();
            Status = $"Loaded {System.IO.Path.GetFileName(dlg.FileName)}.";
        }
        catch (Exception ex)
        {
            Status = "Load failed: " + ex.Message;
        }
    }

    // ---- IScanHost ----------------------------------------------------------
    private bool ReadAt(nuint address, ScanWidth width, out long value)
    {
        value = 0;
        var mem = _mem;
        if (mem is not { IsOpen: true }) return false;
        int w = (int)width;
        var buf = mem.Read(address, w);
        if (buf.Length < w) return false;
        long result = 0;
        for (int i = 0; i < w; i++) result |= (long)buf[i] << (8 * i);
        value = result;
        return true;
    }

    private bool WriteAt(nuint address, long value, ScanWidth width)
    {
        var mem = _mem;
        if (mem is not { IsOpen: true }) return false;
        int w = (int)width;
        var buf = new byte[w];
        ulong v = unchecked((ulong)value);
        for (int i = 0; i < w; i++) { buf[i] = (byte)(v & 0xFF); v >>= 8; }
        return mem.Write(address, buf);
    }

    bool IScanHost.Write(nuint address, long value, ScanWidth width) => WriteAt(address, value, width);
    bool IScanHost.Read(nuint address, ScanWidth width, out long value) => ReadAt(address, width, out value);

    void IScanHost.ReportWriteFailure(nuint address)
    {
        Status = $"Write failed at 0x{(ulong)address:X} — process may have detached.";
    }

    // ---- poll ---------------------------------------------------------------
    private bool HasTargetExited()
    {
        var target = _target;
        if (target == null) return false;
        try
        {
            return target.HasExited;
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return true;
        }
    }

    private void PollTick()
    {
        if (_mem == null) return;

        if (!_mem.IsOpen || HasTargetExited())
        {
            Detach();
            Status = "Target process exited.";
            return;
        }

        if (_searcher != null && !IsScanning && _searcher.HasMatches && _searcher.MatchCount <= LiveRefreshThreshold)
        {
            foreach (var row in Results)
                if (ReadAt(row.Address, SelectedWidth, out long live)) row.RefreshLive(live);
        }

        foreach (var f in Frozen)
        {
            if (ReadAt(f.Address, f.Width, out long live))
                f.RefreshLive(live);
            f.ApplyFreeze();
        }
    }

    private void RaiseCommands()
    {
        (RefreshProcessesCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (AttachCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (DetachCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (FirstScanCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (NextScanCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (NewScanCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (PinCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (RemoveFrozenCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (FreezeAllCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (UnfreezeAllCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (SaveSaveCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (SetAllQuestsGivenCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (SetAllQuestsCompleteCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (SetAllQuestsMedalCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    public void Dispose()
    {
        _poll.Stop();
        _scanCts?.Cancel();
        _scanCts?.Dispose();
        _target?.Dispose();
        _mem?.Dispose();
    }
}

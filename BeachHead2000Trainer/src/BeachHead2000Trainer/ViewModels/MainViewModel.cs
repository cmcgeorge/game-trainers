using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using System.Windows.Threading;
using BeachHead2000Trainer.Game;

namespace BeachHead2000Trainer.ViewModels;

/// <summary>A selectable target process.</summary>
public sealed class ProcessEntry
{
    public int Id { get; }
    public string Name { get; }
    public bool IsGame { get; }
    public string Display => $"{Name}  (pid {Id})";

    public ProcessEntry(int id, string name, bool isGame)
    {
        Id = id; Name = name; IsGame = isGame;
    }
}

/// <summary>
/// Root view-model. BeachHead 2000 (Digital Fusion / WizardWorks, 2000) is a native 32-bit
/// Windows game (no ASLR, image base 0x00400000). The player mans a beach bunker with three
/// weapon types (bullets, projectiles, missiles) against waves of infantry, tanks, APCs,
/// helicopters, jets, and bombers. The mutable game state (health, ammo, score, current level)
/// lives in heap-allocated memory with no adjacent constant byte-run to anchor a locator to,
/// so the reliable primitive is a Cheat-Engine-style <b>value scan</b>: attach to the game
/// process, snapshot memory, and narrow by what the on-screen number does. Survivors are
/// pinned to a freeze table that re-writes them every poll tick. The trainer also includes
/// an offline level-file editor (the shipped <c>Level_00</c>…<c>Level_60</c> files are plain
/// text scripts that define starting ammo, time limit, enemy aggression, and unit waves) and
/// a read-only reference tab with weapon, enemy, and control tables.
/// </summary>
public sealed class MainViewModel : ObservableObject, IScanHost, IDisposable
{
    private static readonly string[] GameHints =
        { "bh", "bh2000", "beachhead", "beachhead15", "beachhead16" };

    private const int MaxResultRows = 1000;
    private const int LiveRefreshThreshold = 200;
    private readonly byte[] _ioBuf = new byte[4];

    private ProcessMemory? _mem;
    private MemorySearcher? _searcher;
    private readonly DispatcherTimer _poll;
    private CancellationTokenSource? _scanCts;
    private int _targetPid;
    private string _pendingPinLabel = "";

    public ObservableCollection<ProcessEntry> Processes { get; } = new();
    public ObservableCollection<ScanResultViewModel> Results { get; } = new();
    public ObservableCollection<FrozenValueViewModel> Frozen { get; } = new();
    public ObservableCollection<WeaponInfo> Weapons { get; } = new(WeaponInfo.Weapons);
    public ObservableCollection<EnemyInfo> Enemies { get; } = new(EnemyInfo.Enemies);
    public ObservableCollection<ControlInfo> Controls { get; } = new(ControlInfo.Controls);

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

    private string _status = "Launch BeachHead 2000, pick the Bh process, and Attach.";
    public string Status { get => _status; set => SetField(ref _status, value); }

    // --- level editor properties --------------------------------------------
    private string _levelFilePath = "";
    public string LevelFilePath { get => _levelFilePath; set => SetField(ref _levelFilePath, value); }

    private bool _hasLevelFile;
    public bool HasLevelFile { get => _hasLevelFile; private set => SetField(ref _hasLevelFile, value); }

    private int _levelBullets;
    public int LevelBullets { get => _levelBullets; set => SetField(ref _levelBullets, value); }

    private int _levelProjectiles;
    public int LevelProjectiles { get => _levelProjectiles; set => SetField(ref _levelProjectiles, value); }

    private int _levelMissiles;
    public int LevelMissiles { get => _levelMissiles; set => SetField(ref _levelMissiles, value); }

    private int _levelTime;
    public int LevelTime { get => _levelTime; set => SetField(ref _levelTime, value); }

    private int _levelAggrTank = 1;
    public int LevelAggrTank { get => _levelAggrTank; set => SetField(ref _levelAggrTank, value); }

    private int _levelAggrJet = 1;
    public int LevelAggrJet { get => _levelAggrJet; set => SetField(ref _levelAggrJet, value); }

    private int _levelAggrHeliGun = 1;
    public int LevelAggrHeliGun { get => _levelAggrHeliGun; set => SetField(ref _levelAggrHeliGun, value); }

    private int _levelAggrHeliRocket = 1;
    public int LevelAggrHeliRocket { get => _levelAggrHeliRocket; set => SetField(ref _levelAggrHeliRocket, value); }

    private int _levelArtillery;
    public int LevelArtillery { get => _levelArtillery; set => SetField(ref _levelArtillery, value); }

    private LevelFile? _levelFile;
    private string? _lastLevelDir;

    // --- commands ------------------------------------------------------------
    public ICommand RefreshProcessesCommand { get; }
    public ICommand AttachCommand { get; }
    public ICommand DetachCommand { get; }
    public ICommand FirstScanCommand { get; }
    public ICommand NextScanCommand { get; }
    public ICommand NewScanCommand { get; }
    public ICommand PinCommand { get; }
    public ICommand RemoveFrozenCommand { get; }
    public ICommand FreezeAllCommand { get; }
    public ICommand FreezeNoneCommand { get; }
    public ICommand HealthGuideCommand { get; }
    public ICommand BulletsGuideCommand { get; }
    public ICommand ProjectilesGuideCommand { get; }
    public ICommand MissilesGuideCommand { get; }
    public ICommand ScoreGuideCommand { get; }
    public ICommand LevelGuideCommand { get; }
    public ICommand LoadLevelCommand { get; }
    public ICommand SaveLevelCommand { get; }
    public ICommand MaxAmmoCommand { get; }

    public MainViewModel()
    {
        RefreshProcessesCommand = new RelayCommand(_ => RefreshProcesses());
        AttachCommand = new RelayCommand(_ => Attach(), _ => SelectedProcess != null && !IsAttached && !IsScanning);
        DetachCommand = new RelayCommand(_ => Detach(), _ => IsAttached);
        FirstScanCommand = new RelayCommand(_ => FirstScan(), _ => IsAttached && !IsScanning && !HasResults);
        NextScanCommand = new RelayCommand(p => NextScan(p), _ => IsAttached && !IsScanning && HasResults);
        NewScanCommand = new RelayCommand(_ => NewScan(), _ => IsAttached && !IsScanning && HasResults);
        PinCommand = new RelayCommand(_ => PinSelected(), _ => SelectedResult != null);
        RemoveFrozenCommand = new RelayCommand(_ => RemoveFrozen(), _ => SelectedFrozen != null);
        FreezeAllCommand = new RelayCommand(_ => SetAllFrozen(true), _ => Frozen.Count > 0);
        FreezeNoneCommand = new RelayCommand(_ => SetAllFrozen(false), _ => Frozen.Count > 0);
        HealthGuideCommand = new RelayCommand(_ => ShowHealthGuide(), _ => IsAttached && !IsScanning);
        BulletsGuideCommand = new RelayCommand(_ => ShowBulletsGuide(), _ => IsAttached && !IsScanning);
        ProjectilesGuideCommand = new RelayCommand(_ => ShowProjectilesGuide(), _ => IsAttached && !IsScanning);
        MissilesGuideCommand = new RelayCommand(_ => ShowMissilesGuide(), _ => IsAttached && !IsScanning);
        ScoreGuideCommand = new RelayCommand(_ => ShowScoreGuide(), _ => IsAttached && !IsScanning);
        LevelGuideCommand = new RelayCommand(_ => ShowLevelGuide(), _ => IsAttached && !IsScanning);
        LoadLevelCommand = new RelayCommand(_ => LoadLevel(), _ => true);
        SaveLevelCommand = new RelayCommand(_ => SaveLevel(), _ => HasLevelFile);
        MaxAmmoCommand = new RelayCommand(_ => MaxAmmo(), _ => HasLevelFile);

        _poll = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
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
                bool game = GameHints.Any(h => name.Contains(h, StringComparison.OrdinalIgnoreCase));
                list.Add(new ProcessEntry(p.Id, name, game));
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
            }
            finally { p.Dispose(); }
        }
        foreach (var e in list.OrderByDescending(e => e.IsGame).ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase))
            Processes.Add(e);

        SelectedProcess = Processes.FirstOrDefault(e => e.Id == previous)
                          ?? Processes.FirstOrDefault(e => e.IsGame)
                          ?? Processes.FirstOrDefault();
    }

    private void Attach()
    {
        if (SelectedProcess == null) return;
        try
        {
            _mem = ProcessMemory.Open(SelectedProcess.Id);
            _targetPid = SelectedProcess.Id;
            _searcher = new MemorySearcher(_mem, SelectedWidth);
            Results.Clear();
            OnPropertyChanged(nameof(IsAttached));
            OnPropertyChanged(nameof(HasResults));
            RaiseCommands();
            _poll.Start();
            Status = $"Attached to {SelectedProcess.Name} (pid {SelectedProcess.Id}). " +
                     "Use a guided scan below, or First Scan a value you can read in-game.";
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
        _searcher = null;
        _targetPid = 0;
        Results.Clear();
        Frozen.Clear();
        SelectedResult = null;
        SelectedFrozen = null;
        MatchCount = "";
        OnPropertyChanged(nameof(IsAttached));
        OnPropertyChanged(nameof(HasResults));
        RaiseCommands();
        Status = "Detached.";
    }

    // --- scanning ------------------------------------------------------------
    private async void FirstScan()
    {
        if (_searcher == null || IsScanning) return;
        bool hasValue = ScanValue.TryParse(ScanText, out long value);
        if (!hasValue && !string.IsNullOrWhiteSpace(ScanText))
        {
            Status = "Enter a number, or clear the box to scan for an unknown value.";
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
            hasValue ? $"First scan for {value}..." : "First scan (unknown value)...",
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
        await RunScan($"Narrowing ({compare})...", ct => searcher.NextScan(compare, value, ct));
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
        OnPropertyChanged(nameof(HasResults));
        RaiseCommands();
        if (IsAttached) Status = $"New {SelectedWidth} scan. Enter a value and First Scan.";
    }

    // --- pin / freeze --------------------------------------------------------
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
        _pendingPinLabel = "";
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
        Status = frozen ? "All pinned values frozen." : "Freeze cleared.";
    }

    // --- guided scans --------------------------------------------------------
    private void BeginGuide(ScanWidth width, string label)
    {
        if (_selectedWidth != width) SelectedWidth = width;
        else NewScan();
        _pendingPinLabel = label;
    }

    private void ShowHealthGuide()
    {
        BeginGuide(ScanWidth.Int32, "Health");
        Status = "Health guide: start a level and note your health (usually 100), type it → " +
                 "First Scan; take a hit so it drops → type the new value → Exact (or Decreased). " +
                 "Repeat until one row remains, then Pin it. Freeze to stay invincible.";
    }

    private void ShowBulletsGuide()
    {
        BeginGuide(ScanWidth.Int32, "Bullets");
        Status = "Bullets guide: read your bullet count from the HUD, type it → First Scan; fire a " +
                 "burst so the count drops → type the new value → Exact (or Decreased). " +
                 "Repeat until one row remains, then Pin and set a high Target. Freeze for infinite ammo.";
    }

    private void ShowProjectilesGuide()
    {
        BeginGuide(ScanWidth.Int32, "Projectiles");
        Status = "Projectiles guide: read your projectile count from the HUD, type it → First Scan; " +
                 "fire a cannon round so the count drops → type the new value → Exact (or Decreased). " +
                 "Pin the survivor. (Try Int16 if Int32 finds nothing.)";
    }

    private void ShowMissilesGuide()
    {
        BeginGuide(ScanWidth.Int32, "Missiles");
        Status = "Missiles guide: read your missile count from the HUD, type it → First Scan; fire a " +
                 "missile so the count drops → type the new value → Exact (or Decreased). " +
                 "Pin the survivor. (Try Int16 if Int32 finds nothing.)";
    }

    private void ShowScoreGuide()
    {
        BeginGuide(ScanWidth.Int32, "Score");
        Status = "Score guide: read your score (press F to toggle FPS/Score if needed), type it → " +
                 "First Scan; destroy enemies so the score rises → type the new value → Exact " +
                 "(or Increased). Pin the survivor.";
    }

    private void ShowLevelGuide()
    {
        BeginGuide(ScanWidth.Int32, "Level");
        Status = "Level guide: note the current level number (shown at level start), type it → " +
                 "First Scan; advance to the next level → type the new number → Exact (or Increased). " +
                 "Pin the survivor to jump to any level. (Try Int16 if Int32 finds nothing.)";
    }

    // --- level file editor ---------------------------------------------------
    private void LoadLevel()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Open BeachHead 2000 Level File",
            Filter = "Level files (Level_*)|Level_*",
            DefaultExt = "",
            InitialDirectory = LevelDirectory.Find(_targetPid, _lastLevelDir) ?? "",
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            _levelFile = LevelFile.Load(dialog.FileName);
            LevelFilePath = dialog.FileName;
            _lastLevelDir = Path.GetDirectoryName(dialog.FileName);
            LevelBullets = _levelFile.Bullets;
            LevelProjectiles = _levelFile.Projectiles;
            LevelMissiles = _levelFile.Missiles;
            LevelTime = _levelFile.Time;
            LevelAggrTank = _levelFile.AggressionTank;
            LevelAggrJet = _levelFile.AggressionJet;
            LevelAggrHeliGun = _levelFile.AggressionHeliGun;
            LevelAggrHeliRocket = _levelFile.AggressionHeliRocket;
            LevelArtillery = _levelFile.Artillery;
            HasLevelFile = true;
            RaiseCommands();
            Status = $"Loaded {Path.GetFileName(dialog.FileName)} — edit values and Save.";
        }
        catch (Exception ex)
        {
            Status = "Failed to load level file: " + ex.Message;
        }
    }

    private void SaveLevel()
    {
        if (_levelFile == null) return;

        if (LevelBullets < 0 || LevelProjectiles < 0 || LevelMissiles < 0)
        {
            Status = "Ammo values must not be negative.";
            return;
        }
        if (LevelTime < 0)
        {
            Status = "Time must not be negative.";
            return;
        }
        if (!IsAggressionValid(LevelAggrTank) || !IsAggressionValid(LevelAggrJet) ||
            !IsAggressionValid(LevelAggrHeliGun) || !IsAggressionValid(LevelAggrHeliRocket))
        {
            Status = $"Aggression values must be {GameFacts.AggressionMin}–{GameFacts.AggressionMax}.";
            return;
        }
        if (LevelArtillery != 0 && LevelArtillery != 1)
        {
            Status = "Artillery must be 0 (off) or 1 (on).";
            return;
        }

        try
        {
            _levelFile.Bullets = LevelBullets;
            _levelFile.Projectiles = LevelProjectiles;
            _levelFile.Missiles = LevelMissiles;
            _levelFile.Time = LevelTime;
            _levelFile.AggressionTank = LevelAggrTank;
            _levelFile.AggressionJet = LevelAggrJet;
            _levelFile.AggressionHeliGun = LevelAggrHeliGun;
            _levelFile.AggressionHeliRocket = LevelAggrHeliRocket;
            _levelFile.Artillery = LevelArtillery;
            _levelFile.Save();
            Status = $"Saved to {Path.GetFileName(_levelFile.SourcePath ?? "")}. " +
                     "Restart the level in-game for changes to take effect.";
        }
        catch (Exception ex)
        {
            Status = "Failed to save level file: " + ex.Message;
        }
    }

    private void MaxAmmo()
    {
        LevelBullets = GameFacts.MaxBullets;
        LevelProjectiles = GameFacts.MaxProjectiles;
        LevelMissiles = GameFacts.MaxMissiles;
        Status = $"Ammo set to max ({GameFacts.MaxBullets}/{GameFacts.MaxProjectiles}/{GameFacts.MaxMissiles}). Click Save to write the file.";
    }

    private static bool IsAggressionValid(int value) =>
        value >= GameFacts.AggressionMin && value <= GameFacts.AggressionMax;

    // --- poll loop -----------------------------------------------------------
    private void PollTick()
    {
        if (_mem == null) return;
        if (!_mem.IsOpen || HasTargetExited()) { Detach(); Status = "Target process exited."; return; }

        foreach (var f in Frozen)
        {
            f.ApplyFreeze();
            if (ReadAt(f.Address, f.Width, out long live)) f.RefreshLive(live);
        }

        if (_searcher != null && !IsScanning && Results.Count > 0 && Results.Count <= LiveRefreshThreshold)
        {
            foreach (var r in Results)
                if (_searcher.ReadValue(r.Address, out long live)) r.RefreshLive(live);
        }
    }

    private bool HasTargetExited()
    {
        if (_targetPid == 0) return false;
        try
        {
            using var p = Process.GetProcessById(_targetPid);
            return p.HasExited;
        }
        catch (ArgumentException)
        {
            return true;
        }
    }

    // --- IScanHost -----------------------------------------------------------
    private bool ReadAt(nuint address, ScanWidth width, out long value)
    {
        value = 0;
        var mem = _mem;
        if (mem is not { IsOpen: true }) return false;
        int w = (int)width;
        if (mem.Read(address, _ioBuf, w) < w) return false;
        long result = 0;
        for (int i = 0; i < w; i++) result |= (long)_ioBuf[i] << (8 * i);
        value = result;
        return true;
    }

    private bool WriteAt(nuint address, long value, ScanWidth width)
    {
        var mem = _mem;
        if (mem is not { IsOpen: true }) return false;
        int w = (int)width;
        ulong v = unchecked((ulong)value);
        for (int i = 0; i < w; i++) { _ioBuf[i] = (byte)(v & 0xFF); v >>= 8; }
        return mem.WriteRange(address, _ioBuf, 0, w);
    }

    bool IScanHost.Write(nuint address, long value, ScanWidth width) => WriteAt(address, value, width);
    bool IScanHost.Read(nuint address, ScanWidth width, out long value) => ReadAt(address, width, out value);
    void IScanHost.ReportWriteFailure(nuint address)
        => Status = $"Write failed at 0x{(ulong)address:X} — the value was not applied.";

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
        (FreezeNoneCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (HealthGuideCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (BulletsGuideCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ProjectilesGuideCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (MissilesGuideCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ScoreGuideCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (LevelGuideCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (SaveLevelCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (MaxAmmoCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    public void Dispose()
    {
        _poll.Stop();
        _scanCts?.Cancel();
        _scanCts?.Dispose();
        _mem?.Dispose();
    }
}

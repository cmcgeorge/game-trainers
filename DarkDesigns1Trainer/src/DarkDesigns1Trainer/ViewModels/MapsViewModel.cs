using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using Microsoft.Win32;
using DarkDesigns1Trainer.Game;
using DarkDesigns1Trainer.Memory;

namespace DarkDesigns1Trainer.ViewModels;

/// <summary>
/// Backs the 🗺 Maps tab: the level schematics, where the party is standing, and teleporting it
/// somewhere else on the level it is already on.
///
/// The party's position is four <c>uint16</c> in the game's data segment and the level it is
/// walking around is a 12,648-byte buffer a few kilobytes further on. Neither address survives a
/// DOSBox restart, so <see cref="MapLocator"/> derives them from the roster the character scan
/// already found and validates the map bytes before believing any of it — see that class for the
/// two strategies.
///
/// Teleport writes X, Y and facing only, and only within the level the party is on: the game loads
/// a level's map when it takes the stairs, so moving the level number on its own would leave the
/// party walking around the wrong map. To change level, teleport onto the stairs and take a step —
/// or edit the position in <c>DDCHARS.DAT</c> on the Save Editor tab, where the level is safe to
/// change because the game loads the matching map when it reads the file back.
/// </summary>
public sealed class MapsViewModel : ObservableObject
{
    private readonly Func<ProcessMemory?> _getMem;
    private readonly Func<nuint?> _getRosterBase;
    private readonly ICharacterHost _host;
    private readonly Action<string> _setStatus;

    private LocatedMap? _located;
    private readonly byte[] _liveBytes = new byte[MapFormat.FileSize];
    private DungeonMap? _liveMap;
    private int _liveMapLevel;                     // which level _liveBytes was read for, 0 = none
    private bool _isLocating;
    private CancellationTokenSource? _locateCts;
    private readonly Dictionary<int, DungeonMap> _offlineMaps = new();

    /// <summary>
    /// Bumped by everything that abandons a locate. A scan captures it before awaiting and drops
    /// its result if it no longer matches — cancellation here is cooperative and the roster path
    /// does not take a token at all, so without this a scan that was explicitly given up on can
    /// still come back and publish a position over the state that replaced it.
    /// </summary>
    private int _locateGeneration;

    public MapsViewModel(Func<ProcessMemory?> getMem, Func<nuint?> getRosterBase,
                         ICharacterHost host, Action<string> setStatus)
    {
        _getMem = getMem;
        _getRosterBase = getRosterBase;
        _host = host;
        _setStatus = setStatus;

        LocateCommand = new RelayCommand(_ => Locate(), _ => IsAttached && !_isLocating);
        TeleportCommand = new RelayCommand(_ => Teleport(), _ => CanTeleport);
        RevealCommand = new RelayCommand(_ => RevealWholeLevel(), _ => CanReveal);
        LoadFolderCommand = new RelayCommand(_ => PickFolder());

        TryAutoLoadFolder();
        SelectedLevel = MapBook.Levels[0];
    }

    private bool IsAttached => _getMem() is { IsOpen: true };

    // --- level selection -----------------------------------------------------
    public IReadOnlyList<MapLevel> Levels => MapBook.Levels;

    private MapLevel? _selectedLevel;
    public MapLevel? SelectedLevel
    {
        get => _selectedLevel;
        set { if (SetField(ref _selectedLevel, value)) RebuildMapView(); }
    }

    /// <summary>True when the schematic is showing the level the located party is standing on.</summary>
    public bool IsLiveLevelSelected =>
        _located != null && _liveMap != null && _selectedLevel?.Number == _liveMapLevel;

    // --- the map being shown -------------------------------------------------
    private DungeonMap? _shownMap;

    private IReadOnlyList<MapSquare> _squares = Array.Empty<MapSquare>();
    /// <summary>Drawable squares of the selected level.</summary>
    public IReadOnlyList<MapSquare> Squares { get => _squares; private set => SetField(ref _squares, value); }

    public ObservableCollection<MapRoom> Rooms { get; } = new();

    private MapRoom? _selectedRoom;
    public MapRoom? SelectedRoom
    {
        get => _selectedRoom;
        set
        {
            if (!SetField(ref _selectedRoom, value)) return;
            if (value != null) SetTarget(value.First.X, value.First.Y);
        }
    }

    public int GridSize => MapFormat.GridSize;

    private string _mapSource = "No map yet — pick a level and load the game folder, or attach and locate the party.";
    /// <summary>Where the squares on screen came from: the running game, a file, or nowhere.</summary>
    public string MapSource { get => _mapSource; private set => SetField(ref _mapSource, value); }

    /// <summary>
    /// Chooses what to draw for the selected level: the live buffer when the party is standing on
    /// that level (it carries the squares already explored and any door opened this session),
    /// otherwise the file loaded from the game folder.
    /// </summary>
    private void RebuildMapView()
    {
        int level = _selectedLevel?.Number ?? 0;
        DungeonMap? map = null;
        string source;

        if (_liveMap != null && level == _liveMapLevel)
        {
            map = _liveMap;
            source = $"Live from the running game — {map.VisitedCount} of {MapFormat.ContentsLength} squares explored.";
        }
        else if (_offlineMaps.TryGetValue(level, out var offline))
        {
            map = offline;
            source = $"From {MapBook.MapFileName(level)}.";
        }
        else
        {
            source = _located == null
                ? "Attach and locate the party to draw the level it is on, or load the game folder to browse all five."
                : $"Not showing {MapBook.LevelName(level)} — the party is on {MapBook.LevelName(_liveMapLevel)}. Load the game folder to browse the others.";
        }

        _shownMap = map;
        Squares = map?.DrawableSquares() ?? Array.Empty<MapSquare>();
        MapSource = source;

        var previous = _selectedRoom?.Code;
        Rooms.Clear();
        if (map != null) foreach (var r in map.Rooms()) Rooms.Add(r);
        _selectedRoom = Rooms.FirstOrDefault(r => r.Code == previous);
        OnPropertyChanged(nameof(SelectedRoom));
        OnPropertyChanged(nameof(IsLiveLevelSelected));
        RaiseCommands();
    }

    // --- live position -------------------------------------------------------
    private PartyPosition _position;
    public PartyPosition Position { get => _position; private set => SetField(ref _position, value); }

    public bool HasPosition => _located != null;

    public int LiveX => _position.X;
    public int LiveY => _position.Y;

    /// <summary>True when the party marker belongs on the schematic currently drawn.</summary>
    public bool ShowLiveMarker => _located != null && _position.InDungeon && _selectedLevel?.Number == _position.Level;

    private string _positionText = "";
    /// <summary>"Ground Level — X 16 · Y 31 facing North" once located; empty otherwise.</summary>
    public string PositionText { get => _positionText; private set => SetField(ref _positionText, value); }

    private string _locateState = "Not located. Attach on the Party tab, then click Locate.";
    public string LocateState { get => _locateState; private set => SetField(ref _locateState, value); }

    // --- teleport target -----------------------------------------------------
    private int _targetX;
    public int TargetX { get => _targetX; set { SetField(ref _targetX, Math.Clamp(value, 0, MapFormat.GridSize - 1)); RaiseCommands(); } }

    private int _targetY;
    public int TargetY { get => _targetY; set { SetField(ref _targetY, Math.Clamp(value, 0, MapFormat.GridSize - 1)); RaiseCommands(); } }

    private int _targetFacing;
    public int TargetFacing { get => _targetFacing; set => SetField(ref _targetFacing, Math.Clamp(value, 0, MapFormat.Directions - 1)); }

    public IReadOnlyList<string> Facings => MapFormat.FacingNames;

    private void SetTarget(int x, int y)
    {
        TargetX = x;
        TargetY = y;
        OnPropertyChanged(nameof(TargetDescription));
    }

    /// <summary>What is on the target square, so the user can see where they are aiming.</summary>
    public string TargetDescription
    {
        get
        {
            if (_shownMap is not { } map) return "";
            var sq = map.Square(TargetX, TargetY);
            string kind = MapFormat.KindName(sq.Kind);
            string name = sq.RoomName.Length > 0 ? sq.RoomName : "";
            return string.Join(" — ", new[] { sq.Coord, name, kind }.Where(s => s.Length > 0));
        }
    }

    // --- commands ------------------------------------------------------------
    public ICommand LocateCommand { get; }
    public ICommand TeleportCommand { get; }
    public ICommand RevealCommand { get; }
    public ICommand LoadFolderCommand { get; }

    private bool CanTeleport =>
        IsAttached && _located != null && _position.InDungeon &&
        _selectedLevel?.Number == _position.Level;

    // Also requires the level on screen to be the live one, so the button can never quietly reveal a
    // different level than the one the user is looking at.
    private bool CanReveal =>
        IsAttached && _located != null && _liveMap != null &&
        _liveMapLevel == _position.Level && _selectedLevel?.Number == _liveMapLevel;

    // --- locating ------------------------------------------------------------
    private async void Locate()
    {
        var mem = _getMem();
        if (mem is not { IsOpen: true }) { _setStatus("Attach on the Party tab first."); return; }
        if (_isLocating) return;

        _locateCts?.Dispose();
        _locateCts = new CancellationTokenSource();
        var ct = _locateCts.Token;

        int generation = ++_locateGeneration;
        _isLocating = true;
        RaiseCommands();
        LocateState = "Looking for the party position and the level it is on…";

        LocatedMap? found;
        try
        {
            var source = new ProcessMemorySource(mem);
            var rosterBase = _getRosterBase();
            found = await Task.Run(() => MapLocator.Find(source, rosterBase, ct), ct);
        }
        catch (OperationCanceledException)
        {
            // Reset/detach already said what happened; don't leave the label mid-sentence.
            if (generation == _locateGeneration) LocateState = "Locate cancelled.";
            return;
        }
        catch (Exception ex)
        {
            if (generation != _locateGeneration) return;
            LocateState = "Locate failed: " + ex.Message;
            _setStatus("Map locate error: " + ex.Message);
            return;
        }
        finally
        {
            // Only the scan that is still the current one may hand the buttons back; an abandoned
            // one returning late must not re-enable Locate underneath its replacement.
            if (generation == _locateGeneration)
            {
                _isLocating = false;
                RaiseCommands();
            }
        }

        // Detached, re-attached, or given up on while scanning — either way this result is no
        // longer about the state the user is looking at.
        if (_getMem() != mem || generation != _locateGeneration) return;

        if (found == null)
        {
            _located = null;
            LocateState = "Not found. Walk into Grelminar's castle (the town's (G) option) so a level is loaded, then Locate again.";
            _setStatus("Could not locate the party position — the map buffer is only recognisable once a level is loaded.");
            return;
        }

        _located = found;
        _liveMapLevel = 0;
        _liveMap = null;
        Tick();

        LocateState = found.Method == MapLocateMethod.Roster
            ? "Located from the character roster and confirmed against the level's own map data."
            : "Located by scanning for the level's map data.";
        _setStatus($"Party located: {found.Position.Describe()}");
    }

    /// <summary>Drops everything tied to the attached process.</summary>
    public void Reset()
    {
        _locateCts?.Cancel();
        _locateCts?.Dispose();
        _locateCts = null;
        _locateGeneration++;      // disown any scan still in flight
        _isLocating = false;
        _located = null;
        _liveMap = null;
        _liveMapLevel = 0;
        _ticksSinceMapRead = 0;
        Array.Clear(_liveBytes);
        Position = default;
        PositionText = "";
        LocateState = "Not located. Attach on the Party tab, then click Locate.";
        OnPropertyChanged(nameof(HasPosition));
        OnPropertyChanged(nameof(ShowLiveMarker));
        RebuildMapView();
    }

    // --- poll tick -----------------------------------------------------------
    /// <summary>Re-reads the position, and the map buffer whenever the party changes level.</summary>
    public void Tick()
    {
        if (_located is not { } located) return;

        var mem = _getMem();
        if (mem is not { IsOpen: true }) { Reset(); return; }

        var source = new ProcessMemorySource(mem);
        var outcome = MapLocator.TryReadPosition(source, located.PositionAddress, out var position);
        if (outcome == MapLocator.ReadOutcome.Unreadable)
        {
            // The block sits at a fixed spot in the data segment for the whole session, so an
            // address that has stopped reading means the game is gone rather than that it moved.
            Reset();
            LocateState = "Lost the position — DOSBox restarted, or the game quit. Click Locate again.";
            return;
        }
        if (outcome == MapLocator.ReadOutcome.Implausible)
        {
            // Readable but out of range. The game does this to itself — stepping off a ledge on the
            // bottom level increments the level past 5 — so hold the address and keep watching
            // rather than tearing down a perfectly good locate.
            LocateState = "The position is momentarily out of range — holding the address. " +
                          "Click Locate again if this does not clear.";
            return;
        }

        bool levelChanged = position.Level != _position.Level;
        Position = position;
        PositionText = position.Describe();
        OnPropertyChanged(nameof(HasPosition));
        OnPropertyChanged(nameof(LiveX));
        OnPropertyChanged(nameof(LiveY));
        OnPropertyChanged(nameof(ShowLiveMarker));

        // Re-read the map when the party changes level, and periodically otherwise so the explored
        // squares and any door opened since fill in. Re-reading every tick would rebuild a
        // thousand-square schematic six times a second for nothing.
        if (position.InDungeon)
        {
            bool needMap = levelChanged || _liveMapLevel != position.Level;
            if (needMap || ++_ticksSinceMapRead >= MapRefreshTicks) RefreshLiveMap(position.Level);
        }

        // Follow the party onto whatever level it walks to, but only when it actually changes, so a
        // level the user is browsing is not yanked away on every tick.
        if (levelChanged && position.InDungeon)
        {
            var level = MapBook.Levels.FirstOrDefault(l => l.Number == position.Level);
            if (level != null && !ReferenceEquals(level, SelectedLevel)) SelectedLevel = level;
            else RebuildMapView();
        }

        RaiseCommands();
    }

    private int _ticksSinceMapRead;

    /// <summary>Poll ticks between routine re-reads of the map buffer (the timer runs at 600 ms).</summary>
    private const int MapRefreshTicks = 8;

    /// <summary>
    /// Re-reads the whole 12,648-byte map buffer. The schematic is only rebuilt when the bytes
    /// actually changed, so a party standing still does not churn the UI.
    /// </summary>
    private void RefreshLiveMap(int level)
    {
        if (_located is not { } located) return;

        _ticksSinceMapRead = 0;
        var fresh = new byte[MapFormat.FileSize];

        // A read that fails, or bytes that stop decoding, must not leave the schematic quietly
        // claiming to be live: the stale copy would still authorise a reveal. Drop it and say so.
        if (!_host.ReadBytes(located.MapAddress, fresh, MapFormat.FileSize) ||
            !MapFormat.LooksLikeMap(fresh, 0))
        {
            if (_liveMap == null) return;
            _liveMap = null;
            _liveMapLevel = 0;
            RebuildMapView();
            LocateState = "The map buffer stopped decoding — the level may be mid-load. " +
                          "Click Locate again if this does not clear.";
            return;
        }

        bool changed = _liveMapLevel != level || !fresh.AsSpan().SequenceEqual(_liveBytes);
        if (!changed) return;

        Array.Copy(fresh, _liveBytes, MapFormat.FileSize);
        _liveMap = new DungeonMap(_liveBytes, 0, level);
        _liveMapLevel = level;
        RebuildMapView();
    }

    // --- teleport ------------------------------------------------------------
    private void Teleport()
    {
        if (_located is not { } located) { _setStatus("Locate the party first."); return; }

        var mem = _getMem();
        if (mem is not { IsOpen: true }) { _setStatus("Attach on the Party tab first."); return; }

        // Re-validate rather than trusting the cached copy. The level is the one field a teleport
        // must not change and the party may have taken the stairs since the last tick — and the
        // position's own four values are only range-checked, so a stale address left behind by a
        // game that quit and restarted inside the same DOSBox can pass that check by accident.
        // Only the map behind it settles it.
        var source = new ProcessMemorySource(mem);
        if (!MapLocator.TryRevalidate(source, located.PositionAddress, out var live, out _))
        {
            Reset();
            _setStatus("The position no longer checks out — click Locate again.");
            return;
        }

        if (!live.InDungeon)
        {
            _setStatus("The party is in town. Enter Grelminar's castle before teleporting.");
            return;
        }
        if (_selectedLevel?.Number != live.Level)
        {
            _setStatus($"Won't teleport: the party is on {MapBook.LevelName(live.Level)} but {MapBook.LevelName(_selectedLevel?.Number ?? 0)} is selected. " +
                       "Teleport only moves within the current level — walk onto a stairway, or edit the position in DDCHARS.DAT on the Save Editor tab.");
            return;
        }

        var target = new PartyPosition(live.Level, TargetX, TargetY, TargetFacing);
        var bytes = target.ToBytes();

        // Write X, Y and facing only — six bytes starting after the level word, so the level the
        // loaded map belongs to is never touched.
        bool ok = _host.WriteBytes(located.PositionAddress, bytes, MapFormat.PosOffX,
                                   MapFormat.PositionBlockSize - MapFormat.PosOffX);

        if (!ok)
        {
            _setStatus("Teleport write failed — click Locate again.");
            return;
        }

        Position = target;
        PositionText = target.Describe();
        OnPropertyChanged(nameof(LiveX));
        OnPropertyChanged(nameof(LiveY));
        OnPropertyChanged(nameof(ShowLiveMarker));

        string what = _shownMap is { } map ? DescribeSquare(map, TargetX, TargetY) : "";
        _setStatus($"Teleported to ({TargetX}, {TargetY}) facing {MapFormat.FacingName(TargetFacing)}{what}. " +
                   "Take a step, or turn, to redraw the view.");
    }

    private static string DescribeSquare(DungeonMap map, int x, int y)
    {
        var sq = map.Square(x, y);
        string kind = MapFormat.KindName(sq.Kind);
        if (kind.Length > 0) return $" — {kind}";
        return sq.RoomName.Length > 0 ? $" — {sq.RoomName}" : "";
    }

    // --- reveal --------------------------------------------------------------
    /// <summary>
    /// Sets the mapped bit on every square of the level the party is on, which is what the game's
    /// own automap draws from. It is the same bit walking there would set, so the game keeps it —
    /// and writes it into <c>DDMAP&lt;n&gt;.DAT</c> the next time it saves that level.
    /// </summary>
    private void RevealWholeLevel()
    {
        if (_located is not { } located || _liveMap == null)
        {
            _setStatus("Locate the party first — the level has to be loaded to reveal it.");
            return;
        }

        var mem = _getMem();
        if (mem is not { IsOpen: true }) { _setStatus("Attach on the Party tab first."); return; }

        // Everything here is re-read, not taken from the cache. There is one map buffer and the
        // game overwrites it whenever it processes a stairway, so a snapshot up to a few seconds
        // old may belong to a level the party has already left — and this writes 1,024 bytes of
        // event codes that the game then saves into that level's own file. Stamping the wrong
        // level's codes over a map file is not something the user could undo.
        var source = new ProcessMemorySource(mem);
        if (!MapLocator.TryRevalidate(source, located.PositionAddress, out var live, out var fresh))
        {
            Reset();
            _setStatus("The map no longer checks out — click Locate again rather than writing to it.");
            return;
        }

        int expected = _liveMapLevel;
        if (!live.InDungeon || live.Level != expected)
        {
            RefreshLiveMap(live.Level);
            _setStatus($"The party is on {MapBook.LevelName(live.Level)} now, not {MapBook.LevelName(expected)} — " +
                       "nothing was written. Try again now the map has caught up.");
            return;
        }

        // Reveal the bytes we just validated, so what gets written is what was checked.
        var target = new DungeonMap(fresh, 0, live.Level);
        int changed = target.RevealAll();
        if (changed == 0) { _setStatus("The whole level is already mapped."); return; }

        if (!_host.WriteBytes(located.MapAddress, fresh, MapFormat.OffContents, MapFormat.ContentsLength))
        {
            _setStatus("Reveal failed — the map buffer could not be written. Click Locate again.");
            return;
        }

        Array.Copy(fresh, _liveBytes, MapFormat.FileSize);
        _liveMap = new DungeonMap(_liveBytes, 0, live.Level);
        _liveMapLevel = live.Level;
        RebuildMapView();
        _setStatus($"Revealed {changed} square(s) on {MapBook.LevelName(live.Level)}. " +
                   "The game keeps this — it is saved into the level's own map file when you leave.");
    }

    // --- offline map files ---------------------------------------------------
    private string _folderStatus = "No game folder loaded — only the level the party is on can be drawn.";
    public string FolderStatus { get => _folderStatus; private set => SetField(ref _folderStatus, value); }

    private void PickFolder()
    {
        var dlg = new OpenFolderDialog
        {
            Title = "Select the Dark Designs I folder (the one holding DDMAP1.DAT … DDMAP5.DAT)",
        };
        if (dlg.ShowDialog() != true) return;
        LoadFolder(dlg.FolderName);
    }

    private bool LoadFolder(string folder)
    {
        if (!MapBook.TryLoadFromFolder(folder, out var maps, out var error))
        {
            FolderStatus = error;
            return false;
        }

        _offlineMaps.Clear();
        foreach (var (level, map) in maps) _offlineMaps[level] = map;
        SavedFolder = folder;
        FolderStatus = $"Loaded {maps.Count} level(s) from {folder}.";
        RebuildMapView();
        return true;
    }

    private void TryAutoLoadFolder()
    {
        var saved = SavedFolder;
        if (!string.IsNullOrEmpty(saved) && LoadFolder(saved)) return;

        // The trainer usually lives nowhere near the game, so this only catches the convenient case
        // of a .game folder sitting alongside it; otherwise the user picks one.
        var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        for (int i = 0; i < 8 && dir != null; i++, dir = dir.Parent)
        {
            string candidate = Path.Combine(dir.FullName, ".game");
            if (Directory.Exists(candidate) && LoadFolder(candidate)) return;
        }
    }

    private static string SettingsFile =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DarkDesigns1Trainer", "gamepath.txt");

    private static string? SavedFolder
    {
        get
        {
            try { return File.Exists(SettingsFile) ? File.ReadAllText(SettingsFile).Trim() : null; }
            catch { return null; }
        }
        set
        {
            try
            {
                if (string.IsNullOrEmpty(value)) return;
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsFile)!);
                File.WriteAllText(SettingsFile, value);
            }
            catch { /* best effort — remembering the folder is a convenience, not a feature */ }
        }
    }

    // --- plumbing ------------------------------------------------------------
    public void RaiseCommands()
    {
        (LocateCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (TeleportCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (RevealCommand as RelayCommand)?.RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(TargetDescription));
    }
}

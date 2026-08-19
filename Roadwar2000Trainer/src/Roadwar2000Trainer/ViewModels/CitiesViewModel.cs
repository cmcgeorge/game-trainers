using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using GameTrainers.Common.Mvvm;
using Roadwar2000Trainer.Game;

namespace Roadwar2000Trainer.ViewModels;

/// <summary>A city as a grid row: where it is, who holds it, and what the gang has stashed there.</summary>
public sealed class CityRowViewModel : ObservableObject
{
    private readonly CitiesViewModel _owner;

    public CityRowViewModel(CitiesViewModel owner, CityInfo info)
    {
        _owner = owner;
        Info = info;
    }

    public CityInfo Info { get; }

    public int Index => Info.Id;
    public string Name => Info.Name;
    public string MapName => Info.MapName;
    public int X => Info.X;
    public int Y => Info.Y;

    private int _size;
    public int Size
    {
        get => _size;
        set { if (SetField(ref _size, value)) _owner.Write(Index, c => c.Size = value); }
    }

    private int _resident;
    public int Resident
    {
        get => _resident;
        set
        {
            if (!SetField(ref _resident, value)) return;
            _owner.Write(Index, c => c.Resident = value);
            OnPropertyChanged(nameof(ResidentName));
        }
    }

    public string ResidentName => ResidentBook.NameOf(_resident);

    private int _strength;
    public int Strength
    {
        get => _strength;
        set { if (SetField(ref _strength, value)) _owner.Write(Index, c => c.Strength = value); }
    }

    private int _cacheFood, _cacheTires, _cacheFuel, _cacheGuns, _cacheMedical;

    public int CacheFood { get => _cacheFood; set { if (SetField(ref _cacheFood, value)) _owner.Write(Index, c => c.SetCache(CityRecord.CacheFood, value)); } }
    public int CacheTires { get => _cacheTires; set { if (SetField(ref _cacheTires, value)) _owner.Write(Index, c => c.SetCache(CityRecord.CacheTires, value)); } }
    public int CacheFuel { get => _cacheFuel; set { if (SetField(ref _cacheFuel, value)) _owner.Write(Index, c => c.SetCache(CityRecord.CacheFuel, value)); } }
    public int CacheGuns { get => _cacheGuns; set { if (SetField(ref _cacheGuns, value)) _owner.Write(Index, c => c.SetCache(CityRecord.CacheGuns, value)); } }
    public int CacheMedical { get => _cacheMedical; set { if (SetField(ref _cacheMedical, value)) _owner.Write(Index, c => c.SetCache(CityRecord.CacheMedical, value)); } }

    /// <summary>Blanks the row when the trainer detaches, so a stale session is not shown as live.</summary>
    internal void Clear()
    {
        SetField(ref _size, 0, nameof(Size));
        SetField(ref _resident, 0, nameof(Resident));
        OnPropertyChanged(nameof(ResidentName));
        SetField(ref _strength, 0, nameof(Strength));
        SetField(ref _cacheFood, 0, nameof(CacheFood));
        SetField(ref _cacheTires, 0, nameof(CacheTires));
        SetField(ref _cacheFuel, 0, nameof(CacheFuel));
        SetField(ref _cacheGuns, 0, nameof(CacheGuns));
        SetField(ref _cacheMedical, 0, nameof(CacheMedical));
    }

    internal void Load(CityRecord c)
    {
        SetField(ref _size, c.Size, nameof(Size));
        SetField(ref _resident, c.Resident, nameof(Resident));
        OnPropertyChanged(nameof(ResidentName));
        SetField(ref _strength, c.Strength, nameof(Strength));
        SetField(ref _cacheFood, c.GetCache(CityRecord.CacheFood), nameof(CacheFood));
        SetField(ref _cacheTires, c.GetCache(CityRecord.CacheTires), nameof(CacheTires));
        SetField(ref _cacheFuel, c.GetCache(CityRecord.CacheFuel), nameof(CacheFuel));
        SetField(ref _cacheGuns, c.GetCache(CityRecord.CacheGuns), nameof(CacheGuns));
        SetField(ref _cacheMedical, c.GetCache(CityRecord.CacheMedical), nameof(CacheMedical));
    }
}

/// <summary>
/// The Cities tab. Everything here is per-city world state the engine randomises at the start of
/// a game and then updates as towns are looted and fought over.
/// </summary>
public sealed class CitiesViewModel : ObservableObject
{
    private readonly MainViewModel _main;

    public CitiesViewModel(MainViewModel main)
    {
        _main = main;
        foreach (var info in CityBook.All) Rows.Add(new CityRowViewModel(this, info));
        View = CollectionViewSource.GetDefaultView(Rows);
        View.Filter = o => o is CityRowViewModel r && Matches(r);
        Residents = ResidentBook.Names;

        FillCacheCommand = new RelayCommand(FillSelectedCache, () => _main.CanEdit && Selected is not null);
        FillAllCachesCommand = new RelayCommand(FillAllCaches, () => _main.CanEdit);
        ClearSelectedCommand = new RelayCommand(ClearSelected, () => _main.CanEdit && Selected is not null);
        ClearAllCommand = new RelayCommand(ClearAll, () => _main.CanEdit);
        RestockAllCommand = new RelayCommand(RestockAll, () => _main.CanEdit);
        TeleportToCityCommand = new RelayCommand(TeleportToSelected, () => _main.CanEdit && Selected is not null);
    }

    public ObservableCollection<CityRowViewModel> Rows { get; } = new();

    public ICollectionView View { get; }

    public IReadOnlyList<string> Residents { get; }

    private CityRowViewModel? _selected;
    public CityRowViewModel? Selected
    {
        get => _selected;
        set
        {
            if (!SetField(ref _selected, value)) return;
            FillCacheCommand?.RaiseCanExecuteChanged();
            ClearSelectedCommand?.RaiseCanExecuteChanged();
            TeleportToCityCommand?.RaiseCanExecuteChanged();
        }
    }

    private string _filter = "";
    /// <summary>Free-text filter over city name and holding faction.</summary>
    public string Filter
    {
        get => _filter;
        set { if (SetField(ref _filter, value)) View.Refresh(); }
    }

    public RelayCommand FillCacheCommand { get; }
    public RelayCommand FillAllCachesCommand { get; }
    public RelayCommand ClearSelectedCommand { get; }
    public RelayCommand ClearAllCommand { get; }
    public RelayCommand RestockAllCommand { get; }
    public RelayCommand TeleportToCityCommand { get; }

    private GameSlab? Slab => _main.Slab;

    private bool Matches(CityRowViewModel r)
    {
        if (string.IsNullOrWhiteSpace(_filter)) return true;
        return r.Name.Contains(_filter, StringComparison.OrdinalIgnoreCase) ||
               r.ResidentName.Contains(_filter, StringComparison.OrdinalIgnoreCase) ||
               r.MapName.Contains(_filter, StringComparison.OrdinalIgnoreCase);
    }

    internal void Write(int index, Action<CityRecord> apply)
    {
        if (_main.SuppressWriteBack) return;
        if (!_main.CanEdit || Slab is not { } slab) return;
        apply(new CityRecord(slab, index));
    }

    public void Reload()
    {
        // The commands are re-queried even when there is no slab, because Detach() calls this to
        // reset the tab: returning early left the previous session's figures on screen with every
        // action button still enabled.
        if (Slab is { } slab)
            for (int i = 0; i < Rows.Count; i++) Rows[i].Load(new CityRecord(slab, i));
        else
            foreach (var row in Rows) row.Clear();

        FillCacheCommand.RaiseCanExecuteChanged();
        FillAllCachesCommand.RaiseCanExecuteChanged();
        ClearSelectedCommand.RaiseCanExecuteChanged();
        ClearAllCommand.RaiseCanExecuteChanged();
        RestockAllCommand.RaiseCanExecuteChanged();
        TeleportToCityCommand.RaiseCanExecuteChanged();
    }

    private void FillSelectedCache()
    {
        if (Selected is not { } sel) return;
        Write(sel.Index, c => c.FillCache());
        _main.Report($"{sel.Name}'s cache filled to 255 of each of the five stashable supplies.");
        _main.Refresh(force: true);
    }

    private void FillAllCaches()
    {
        if (Slab is not { } slab) return;
        for (int i = 0; i < CityBook.All.Count; i++) new CityRecord(slab, i).FillCache();
        _main.Report("Every city cache filled. Use T)ransfer inside a town to draw on it.");
        _main.Refresh(force: true);
    }

    /// <summary>
    /// Empties a town of its residents. This is the one edit here with a clear gameplay effect:
    /// a town with no residents raises no residential encounter when the gang passes through.
    /// </summary>
    private void ClearSelected()
    {
        if (Selected is not { } sel) return;
        Write(sel.Index, c => c.Clear());
        _main.Report($"{sel.Name} cleared of residents.");
        _main.Refresh(force: true);
    }

    private void ClearAll()
    {
        if (Slab is not { } slab) return;
        for (int i = 0; i < CityBook.All.Count; i++) new CityRecord(slab, i).Clear();
        _main.Report("Every city cleared of residents; residential encounters should stop. " +
                     "Note this does not make the towns yours -- see the docs on city control.");
        _main.Refresh(force: true);
    }

    /// <summary>Puts every town's supply level back to what the engine shipped it with.</summary>
    private void RestockAll()
    {
        if (Slab is not { } slab) return;
        int n = 0;
        for (int i = 0; i < CityBook.All.Count; i++)
        {
            var record = new CityRecord(slab, i);
            int original = CityBook.All[i].Size;
            if (record.Size >= original) continue;
            record.Size = original;
            n++;
        }
        _main.Report(n == 0 ? "No town needed restocking." : $"Restocked {n} town(s) to their shipped supply level.");
        _main.Refresh(force: true);
    }

    /// <summary>
    /// Jumps the gang to the selected city.
    /// <para>
    /// The destination is validated here rather than trusted, for two reasons the shipped data
    /// makes real. HOUSTON is stored at X = 0, which the engine's flat index wraps onto the
    /// previous row's last column and where the game prints a blank location line -- the same
    /// square <see cref="OverlandMap.IsInside"/> exists to exclude, and refusing it here keeps this
    /// path honest with the Map tab's teleport, which already refused it. And a city on the *other*
    /// overland map cannot simply be jumped to: the engine loads a map's 2,016 terrain bytes when
    /// it reads the file, and nothing the trainer writes makes it re-read one, so setting the map
    /// id alone would leave the game walking on the wrong continent's terrain.
    /// </para>
    /// </summary>
    private void TeleportToSelected()
    {
        if (Selected is not { } sel || _main.GangRecord is not { } gang) return;

        if (!OverlandMap.IsInside(sel.Info.X, sel.Info.Y))
        {
            _main.Report($"{sel.Name} is stored at {sel.X},{sel.Y}, which is outside the squares a " +
                         "gang can occupy -- the engine wraps it onto the previous row and prints a " +
                         "blank location line there. Pick another town.");
            return;
        }

        if (gang.CurrentMap != sel.Info.Map)
        {
            _main.Report($"{sel.Name} is on the {sel.MapName.ToLowerInvariant()} map and the gang is " +
                         $"on the {(gang.CurrentMap == 1 ? "west" : "east")} one. The engine only loads " +
                         "a map's terrain when it reads the file, so jumping between them would leave " +
                         "the game on the wrong continent's terrain. Drive to the seam instead.");
            return;
        }

        gang.X = sel.Info.X;
        gang.Y = sel.Info.Y;
        _main.Report($"Moved the gang to {sel.Name} ({sel.MapName} map, {sel.X},{sel.Y}). " +
                     "The overland display redraws on the next move or command.");
        _main.Refresh(force: true);
    }
}

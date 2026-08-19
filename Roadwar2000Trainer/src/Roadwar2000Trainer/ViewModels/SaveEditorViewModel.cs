using System.Collections.ObjectModel;
using System.IO;
using GameTrainers.Common.Mvvm;
using Roadwar2000Trainer.Game;

namespace Roadwar2000Trainer.ViewModels;

/// <summary>
/// The Save Editor tab: opens a <c>.RWS</c> file and edits it with the game closed.
/// <para>
/// Because a save is a verbatim image of the same slab the live editor writes to, this tab is
/// the same code over a different target. It backs the original up once before its first write.
/// </para>
/// </summary>
public sealed class SaveEditorViewModel : ObservableObject
{
    private readonly MainViewModel _main;

    public SaveEditorViewModel(MainViewModel main)
    {
        _main = main;
        BrowseCommand = new RelayCommand(Browse);
        ScanFolderCommand = new RelayCommand(ScanFolder);
        OpenSelectedCommand = new RelayCommand(OpenSelected, () => SelectedFile is not null);
        SaveCommand = new RelayCommand(Save, () => Save_CanExecute());
        MaxOutCommand = new RelayCommand(MaxOut, () => Loaded is not null);
        ClearResidentsCommand = new RelayCommand(ClearResidents, () => Loaded is not null);
        FillCachesCommand = new RelayCommand(FillCaches, () => Loaded is not null);
        CompareWithGameCommand = new RelayCommand(CompareWithGame, () => Loaded is not null && _main.IsAttached);

        Folder = GuessGameFolder() ?? "";
        if (!string.IsNullOrEmpty(Folder)) ScanFolder();
    }

    public RelayCommand BrowseCommand { get; }
    public RelayCommand ScanFolderCommand { get; }
    public RelayCommand OpenSelectedCommand { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand MaxOutCommand { get; }
    public RelayCommand ClearResidentsCommand { get; }
    public RelayCommand FillCachesCommand { get; }
    public RelayCommand CompareWithGameCommand { get; }

    public ObservableCollection<string> Files { get; } = new();

    private string _folder = "";
    /// <summary>The Roadwar 2000 game folder. Saves land here, not on drive A:, despite the prompts.</summary>
    public string Folder
    {
        get => _folder;
        set
        {
            if (!SetField(ref _folder, value)) return;
            _main.Map.GameFolder = value;
        }
    }

    private string? _selectedFile;
    public string? SelectedFile
    {
        get => _selectedFile;
        set { if (SetField(ref _selectedFile, value)) OpenSelectedCommand.RaiseCanExecuteChanged(); }
    }

    private SaveGame? _loaded;
    public SaveGame? Loaded
    {
        get => _loaded;
        private set
        {
            if (!SetField(ref _loaded, value)) return;
            OnPropertyChanged(nameof(HasSave));
            RaiseAll();
            ReloadFields();
        }
    }

    public bool HasSave => _loaded is not null;

    private string _status = "No save loaded.";
    public string Status { get => _status; private set => SetField(ref _status, value); }

    // ---- bound gang fields ---------------------------------------------------

    private string _gangName = "";
    public string GangName
    {
        get => _gangName;
        set { if (SetField(ref _gangName, value)) Edit(g => g.Name = value); }
    }

    private int _food, _tires, _fuel, _ammo, _guns, _medical, _antitoxin, _maxVehicles, _vehicleCount, _day;

    public int Food { get => _food; set { if (SetField(ref _food, value)) Edit(g => g.Food = value); } }
    public int Tires { get => _tires; set { if (SetField(ref _tires, value)) Edit(g => g.Tires = value); } }
    public int Fuel { get => _fuel; set { if (SetField(ref _fuel, value)) Edit(g => g.Fuel = value); } }
    public int Ammo { get => _ammo; set { if (SetField(ref _ammo, value)) Edit(g => g.Ammo = value); } }
    public int Guns { get => _guns; set { if (SetField(ref _guns, value)) Edit(g => g.Guns = value); } }
    public int Medical { get => _medical; set { if (SetField(ref _medical, value)) Edit(g => g.Medical = value); } }
    public int Antitoxin { get => _antitoxin; set { if (SetField(ref _antitoxin, value)) Edit(g => g.Antitoxin = value); } }
    public int MaxVehicles { get => _maxVehicles; set { if (SetField(ref _maxVehicles, value)) Edit(g => g.MaxVehicles = value); } }
    public int Day { get => _day; set { if (SetField(ref _day, value)) Edit(g => g.Day = value); } }

    public int VehicleCount { get => _vehicleCount; private set => SetField(ref _vehicleCount, value); }

    public ObservableCollection<string> Fleet { get; } = new();

    // ---- actions -------------------------------------------------------------

    /// <summary>
    /// Re-queries the commands whose availability depends on the live session. Called by
    /// <see cref="MainViewModel"/> on attach and detach: "Compare with running game" needs
    /// <c>IsAttached</c>, which changes outside this view-model, and <c>RelayCommand</c>
    /// deliberately does not subscribe to <c>CommandManager.RequerySuggested</c>.
    /// </summary>
    public void OnAttachStateChanged() => CompareWithGameCommand.RaiseCanExecuteChanged();

    private void Browse()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Open a Roadwar 2000 saved game",
            Filter = "Roadwar 2000 saves (*.RWS)|*.RWS|All files (*.*)|*.*",
            InitialDirectory = Directory.Exists(_folder) ? _folder : null,
        };
        if (dialog.ShowDialog() != true) return;
        Folder = Path.GetDirectoryName(dialog.FileName) ?? _folder;
        ScanFolder();
        SelectedFile = dialog.FileName;
        OpenSelected();
    }

    private void ScanFolder()
    {
        Files.Clear();
        if (!Directory.Exists(_folder)) { Status = $"'{_folder}' is not a folder."; return; }
        foreach (var f in SaveGame.FindSaves(_folder)) Files.Add(f);
        SelectedFile = Files.FirstOrDefault();
        Status = Files.Count == 0
            ? $"No .RWS saves in {_folder}. Save a game inside DOSBox first (S at the map, then S again)."
            : $"Found {Files.Count} save(s) in {_folder}.";
    }

    private void OpenSelected()
    {
        if (SelectedFile is not { } path) return;
        var save = SaveGame.Load(path, out string error);
        if (save is null) { Status = error; Loaded = null; return; }
        Loaded = save;
        Status = $"Opened {save.FileName} - gang '{save.Gang.Name}', " +
                 $"day {save.Gang.Day}, {save.Gang.VehicleCount} vehicle(s).";
    }

    private bool Save_CanExecute() => _loaded is { IsDirty: true };

    private void Save()
    {
        if (_loaded is not { } save) return;
        if (!save.Save(out string error)) { Status = "Could not write the save: " + error; return; }
        Status = $"Wrote {save.FileName}. The original was backed up to {save.FileName}.bak.";
        SaveCommand.RaiseCanExecuteChanged();
    }

    private void Edit(Action<GangRecord> apply)
    {
        if (_loaded is not { } save) return;
        apply(save.Gang);
        save.MarkDirty();
        SaveCommand.RaiseCanExecuteChanged();
    }

    private void MaxOut()
    {
        if (_loaded is not { } save) return;
        var g = save.Gang;
        g.Food = 9999;
        g.Tires = 9999;
        g.Fuel = 9999;
        g.Ammo = 30000;
        g.Guns = 9999;
        g.Medical = 999;
        g.Antitoxin = 255;
        for (int r = 0; r < SaveFormat.CrewRankCount; r++) g.SetCrew(r, 250);
        g.DoctorQuality = 9;
        g.DrillSergeantQuality = 9;
        g.PoliticianQuality = 9;
        g.HasRadioDirectionFinder = true;
        g.HasSnowTires = true;
        g.HasFuelSpecial = true;
        g.MaxVehicles = SaveFormat.MaxVehicleSlots;
        for (int i = 0; i < g.VehicleCount && i < SaveFormat.MaxVehicleSlots; i++)
            new VehicleRecord(save.Slab, i).Maximize();
        save.MarkDirty();
        Status = "Supplies, crew, cronies and the whole fleet maxed out. Press Save to write the file.";
        ReloadFields();
        SaveCommand.RaiseCanExecuteChanged();
    }

    private void ClearResidents()
    {
        if (_loaded is not { } save) return;
        for (int i = 0; i < CityBook.All.Count; i++) new CityRecord(save.Slab, i).Clear();
        save.MarkDirty();
        Status = "Every city cleared of residents. Press Save to write the file.";
        SaveCommand.RaiseCanExecuteChanged();
    }

    private void FillCaches()
    {
        if (_loaded is not { } save) return;
        for (int i = 0; i < CityBook.All.Count; i++) new CityRecord(save.Slab, i).FillCache();
        save.MarkDirty();
        Status = "Every city cache filled to 255 of each supply. Press Save to write the file.";
        SaveCommand.RaiseCanExecuteChanged();
    }

    /// <summary>
    /// Diffs the loaded file against the running game's slab, ignoring the three bytes the save
    /// routine itself rewrites. A save taken from the current session should differ in zero.
    /// </summary>
    private void CompareWithGame()
    {
        if (_loaded is not { } save || _main.Slab is not { } slab) return;
        int diff = save.DifferencesFrom(slab.Snapshot);
        Status = diff == 0
            ? $"{save.FileName} matches the running game byte for byte (excluding the 3 save-routine bytes)."
            : $"{save.FileName} differs from the running game in {diff} of {SaveFormat.SlabLength} bytes.";
    }

    private void ReloadFields()
    {
        Fleet.Clear();
        if (_loaded is not { } save)
        {
            SetField(ref _gangName, "", nameof(GangName));
            VehicleCount = 0;
            return;
        }

        var g = save.Gang;
        SetField(ref _gangName, g.Name, nameof(GangName));
        SetField(ref _food, g.Food, nameof(Food));
        SetField(ref _tires, g.Tires, nameof(Tires));
        SetField(ref _fuel, g.Fuel, nameof(Fuel));
        SetField(ref _ammo, g.Ammo, nameof(Ammo));
        SetField(ref _guns, g.Guns, nameof(Guns));
        SetField(ref _medical, g.Medical, nameof(Medical));
        SetField(ref _antitoxin, g.Antitoxin, nameof(Antitoxin));
        SetField(ref _maxVehicles, g.MaxVehicles, nameof(MaxVehicles));
        SetField(ref _day, g.Day, nameof(Day));
        VehicleCount = g.VehicleCount;

        for (int i = 0; i < g.VehicleCount && i < SaveFormat.MaxVehicleSlots; i++)
        {
            var v = new VehicleRecord(save.Slab, i);
            Fleet.Add($"{i + 1}. {v.TypeName} - structure {v.Structure}/{v.StructureMax}, " +
                      $"tires {v.Tires}/{v.TiresMax}, crew {v.CrewAboard}");
        }
    }

    private void RaiseAll()
    {
        SaveCommand.RaiseCanExecuteChanged();
        MaxOutCommand.RaiseCanExecuteChanged();
        ClearResidentsCommand.RaiseCanExecuteChanged();
        FillCachesCommand.RaiseCanExecuteChanged();
        CompareWithGameCommand.RaiseCanExecuteChanged();
    }

    /// <summary>
    /// Environment variable that names the Roadwar 2000 folder outright, for installations that
    /// are not in one of the conventional places.
    /// </summary>
    public const string FolderEnvironmentVariable = "ROADWAR2000_DIR";

    /// <summary>
    /// Looks for a Roadwar 2000 folder: the <see cref="FolderEnvironmentVariable"/> first, then the
    /// places these DOS games usually end up on each fixed drive. There are deliberately **no
    /// machine-specific paths** baked in here; point the variable at the folder, or use Browse.
    /// <para>
    /// A folder only qualifies if it holds both overland maps, so a same-named folder belonging to
    /// some other game is never mistaken for this one.
    /// </para>
    /// </summary>
    public static string? GuessGameFolder()
    {
        if (Environment.GetEnvironmentVariable(FolderEnvironmentVariable) is { Length: > 0 } fromEnv &&
            IsGameFolder(fromEnv))
            return fromEnv;

        var stems = new[] { @"\GAMES\RW2000", @"\DOS\GAMES\RW2000", @"\RW2000", @"\ROADWAR" };
        string[] drives;
        try
        {
            drives = DriveInfo.GetDrives()
                .Where(d => d.IsReady && d.DriveType == DriveType.Fixed)
                .Select(d => d.Name.TrimEnd(Path.DirectorySeparatorChar))
                .ToArray();
        }
        catch (IOException) { drives = new[] { "C:" }; }

        foreach (var drive in drives)
        foreach (var stem in stems)
            if (IsGameFolder(drive + stem)) return drive + stem;

        string profile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Games", "RW2000");
        return IsGameFolder(profile) ? profile : null;
    }

    /// <summary>A folder is Roadwar's only if it holds both overland maps.</summary>
    public static bool IsGameFolder(string folder)
    {
        try
        {
            return File.Exists(Path.Combine(folder, "WEST.MAP")) &&
                   File.Exists(Path.Combine(folder, "EAST.MAP"));
        }
        catch (ArgumentException) { return false; }   // a malformed path from the environment
    }
}

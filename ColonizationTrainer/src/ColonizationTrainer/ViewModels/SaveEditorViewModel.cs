using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using ColonizationTrainer.Game;
using Microsoft.Win32;

namespace ColonizationTrainer.ViewModels;

/// <summary>
/// The Save Editor tab — the trainer's verified path. Opens a <c>COLONYxx.SAV</c>, edits the selected
/// nation's gold / tax / Liberty Bells / crosses / Founding Fathers / boycotts and any colonies'
/// stockpiles, then writes the file back in place (keeping a one-time <c>.bak</c>). Every edit mutates
/// the decoded buffer through the typed record views, which clamp to safe ranges, so a shifted or
/// partially-understood field can never be corrupted.
/// </summary>
public sealed class SaveEditorViewModel : ObservableObject
{
    private SaveGame? _save;
    private NationRecord? _nation;

    public ObservableCollection<string> NationChoices { get; } = new();
    public ObservableCollection<FoundingFatherRowViewModel> Fathers { get; } = new();
    public ObservableCollection<ColonyEditorViewModel> Colonies { get; } = new();

    public ICommand OpenCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand MaxGoldCommand { get; }
    public ICommand ClearTaxCommand { get; }
    public ICommand GrantAllFathersCommand { get; }
    public ICommand ClearFathersCommand { get; }
    public ICommand ClearBoycottsCommand { get; }
    public ICommand FillAllColoniesCommand { get; }

    public SaveEditorViewModel()
    {
        OpenCommand = new RelayCommand(_ => Open());
        SaveCommand = new RelayCommand(_ => Save(), _ => IsLoaded);
        MaxGoldCommand = new RelayCommand(_ => { if (_nation != null) Gold = SaveFormat.MaxGoldTarget; }, _ => IsLoaded);
        ClearTaxCommand = new RelayCommand(_ => { if (_nation != null) TaxRate = 0; }, _ => IsLoaded);
        GrantAllFathersCommand = new RelayCommand(_ => GrantAllFathers(), _ => IsLoaded);
        ClearFathersCommand = new RelayCommand(_ => ClearFathers(), _ => IsLoaded);
        ClearBoycottsCommand = new RelayCommand(_ => ClearBoycotts(), _ => IsLoaded);
        FillAllColoniesCommand = new RelayCommand(_ => FillAllColonies(), _ => IsLoaded && Colonies.Count > 0);

        TryOpenDefault();
    }

    // --- state ---------------------------------------------------------------
    public bool IsLoaded => _save != null;

    private string _filePath = "";
    public string FilePath { get => _filePath; private set => SetField(ref _filePath, value); }

    private string _headerSummary = "No save loaded. Click Open to pick a COLONYxx.SAV file.";
    public string HeaderSummary { get => _headerSummary; private set => SetField(ref _headerSummary, value); }

    private string _status = "";
    public string Status { get => _status; set => SetField(ref _status, value); }

    // True while LoadFrom repopulates the bound collections, so the SelectedNationIndex two-way
    // binding (the ComboBox writing -1 back during a Clear()) can't drive BindNation through a
    // transient null state; the load rebinds exactly once at the end.
    private bool _loading;

    private int _selectedNationIndex;
    public int SelectedNationIndex
    {
        get => _selectedNationIndex;
        set
        {
            if (!SetField(ref _selectedNationIndex, value)) return;
            if (!_loading) BindNation();
        }
    }

    // --- selected nation fields ----------------------------------------------
    public long Gold
    {
        get => _nation?.Gold ?? 0;
        set { if (_nation == null) return; _nation.Gold = value; OnPropertyChanged(); }
    }

    public int TaxRate
    {
        get => _nation?.TaxRate ?? 0;
        set { if (_nation == null) return; _nation.TaxRate = value; OnPropertyChanged(); }
    }

    public int LibertyBells
    {
        get => _nation?.LibertyBells ?? 0;
        set { if (_nation == null) return; _nation.LibertyBells = value; OnPropertyChanged(); }
    }

    public int Crosses
    {
        get => _nation?.Crosses ?? 0;
        set { if (_nation == null) return; _nation.Crosses = value; OnPropertyChanged(); }
    }

    public string BoycottStatus => _nation == null ? ""
        : _nation.HasAnyBoycott ? "Some goods are boycotted." : "No goods boycotted.";

    // --- open / save ---------------------------------------------------------
    private void Open()
    {
        var dlg = new OpenFileDialog
        {
            Title = "Open a Colonization save",
            Filter = "Colonization saves (COLONY*.SAV)|COLONY*.SAV|All save files (*.SAV)|*.SAV|All files (*.*)|*.*",
        };
        if (!string.IsNullOrEmpty(FilePath))
        {
            var dir = Path.GetDirectoryName(FilePath);
            if (Directory.Exists(dir)) dlg.InitialDirectory = dir;
        }
        if (dlg.ShowDialog() == true) LoadFrom(dlg.FileName);
    }

    private void LoadFrom(string path)
    {
        SaveGame save;
        try
        {
            save = SaveGame.Load(path);
        }
        catch (Exception ex)
        {
            Status = "Could not open save: " + ex.Message;
            return;
        }

        // Repopulate under the _loading guard so a bound-collection reset can't rebind mid-way.
        _loading = true;
        try
        {
            _save = save;
            FilePath = path;

            NationChoices.Clear();
            for (int i = 0; i < save.Nations.Count; i++)
            {
                string leader = i < save.LeaderNames.Count ? save.LeaderNames[i] : "";
                string country = i < save.CountryNames.Count ? save.CountryNames[i] : "";
                NationChoices.Add($"{save.Nations[i].Name} — {country} ({leader})");
            }

            Colonies.Clear();
            foreach (var c in save.Colonies) Colonies.Add(new ColonyEditorViewModel(c));

            HeaderSummary =
                $"{save.HumanNationName} ({(save.IsManualSave ? "manual save" : "autosave")}) · " +
                $"Year {save.Year}{(save.IsAutumn ? " (autumn)" : "")}, turn {save.Turn} · " +
                $"{save.DifficultyName} · map {save.MapWidth}×{save.MapHeight} · " +
                $"{save.ColonyCount} colonies, {save.UnitCount} units, {save.TribeCount} native dwellings.";

            // Default to editing the human player's nation.
            _selectedNationIndex = Math.Clamp(save.HumanPlayer, 0, save.Nations.Count - 1);
            OnPropertyChanged(nameof(SelectedNationIndex));
        }
        finally
        {
            _loading = false;
        }

        BindNation();   // exactly one deterministic rebind, after the collections have settled
        OnPropertyChanged(nameof(IsLoaded));
        RaiseCommands();
        Status = $"Loaded {Path.GetFileName(path)}.";
    }

    private void Save()
    {
        if (_save == null) return;
        try
        {
            _save.Save();
            Status = $"Saved {Path.GetFileName(_save.Path)} (a .bak of the original was kept). " +
                     "Load it in-game to see the changes.";
        }
        catch (Exception ex)
        {
            Status = "Save failed: " + ex.Message;
        }
    }

    // --- nation binding ------------------------------------------------------
    private void BindNation()
    {
        _nation = _save != null && _selectedNationIndex >= 0 && _selectedNationIndex < _save.Nations.Count
            ? _save.Nations[_selectedNationIndex]
            : null;

        Fathers.Clear();
        if (_nation != null)
            foreach (var f in FoundingFatherBook.Fathers)
                Fathers.Add(new FoundingFatherRowViewModel(_nation, f));

        OnPropertyChanged(nameof(Gold));
        OnPropertyChanged(nameof(TaxRate));
        OnPropertyChanged(nameof(LibertyBells));
        OnPropertyChanged(nameof(Crosses));
        OnPropertyChanged(nameof(BoycottStatus));
    }

    private void GrantAllFathers()
    {
        _nation?.GrantAllFathers();
        foreach (var f in Fathers) f.Refresh();
        Status = "Granted all Founding Fathers to " + (_nation?.Name ?? "the nation") + ".";
    }

    private void ClearFathers()
    {
        _nation?.ClearAllFathers();
        foreach (var f in Fathers) f.Refresh();
        Status = "Cleared all Founding Fathers.";
    }

    private void ClearBoycotts()
    {
        _nation?.ClearBoycotts();
        OnPropertyChanged(nameof(BoycottStatus));
        Status = "Lifted all boycotts.";
    }

    private void FillAllColonies()
    {
        foreach (var c in Colonies) c.FillWarehouseCommand.Execute(null);
        Status = $"Filled the warehouses of {Colonies.Count} colon{(Colonies.Count == 1 ? "y" : "ies")}.";
    }

    // --- auto-detect a shipped save ------------------------------------------
    private void TryOpenDefault()
    {
        var path = FindDefaultSave();
        if (path != null) LoadFrom(path);
    }

    /// <summary>
    /// Best-effort: walk up from the executable looking for a <c>.games</c> or <c>.game</c> folder and
    /// return the newest <c>COLONY*.SAV</c> inside it (so the trainer opens the dev's shipped save
    /// automatically). Returns null if none is found.
    /// </summary>
    private static string? FindDefaultSave()
    {
        try
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            for (int i = 0; i < 8 && dir != null; i++, dir = dir.Parent)
            {
                foreach (var name in new[] { ".games", ".game" })
                {
                    var gameDir = Path.Combine(dir.FullName, name);
                    if (!Directory.Exists(gameDir)) continue;
                    var hit = new DirectoryInfo(gameDir)
                        .GetFiles("COLONY*.SAV")
                        // GetFiles's 8.3 matching can over-match longer extensions; keep only real .SAV files.
                        .Where(f => f.Extension.Equals(".SAV", StringComparison.OrdinalIgnoreCase))
                        .OrderByDescending(f => f.LastWriteTimeUtc)
                        .FirstOrDefault();
                    if (hit != null) return hit.FullName;
                }
            }
        }
        catch { /* auto-detect is a convenience; ignore any IO error */ }
        return null;
    }

    private void RaiseCommands()
    {
        (SaveCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (MaxGoldCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ClearTaxCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (GrantAllFathersCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ClearFathersCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ClearBoycottsCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (FillAllColoniesCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }
}

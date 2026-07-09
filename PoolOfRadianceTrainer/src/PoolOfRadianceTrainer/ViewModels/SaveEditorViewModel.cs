using System.Collections.ObjectModel;
using System.Windows.Input;
using PoolOfRadianceTrainer.Game;
using PoolOfRadianceTrainer.Mvvm;

namespace PoolOfRadianceTrainer.ViewModels;

/// <summary>A checkable row in the assignable-effects list.</summary>
public sealed class EffectPickViewModel : ObservableObject
{
    public EffectInfo Info { get; }
    public EffectPickViewModel(EffectInfo info) => Info = info;

    public byte Code => Info.Code;
    public string Name => Info.Name;
    public string Hex => Info.Hex;
    public bool Beneficial => Info.Beneficial;

    private bool _checked;
    public bool IsChecked { get => _checked; set => SetProperty(ref _checked, value); }
}

/// <summary>A character in the loaded save, with its live effect list.</summary>
public sealed class SaveCharacterViewModel : ObservableObject
{
    public SaveCharacter Model { get; }
    public SaveCharacterViewModel(SaveCharacter model) { Model = model; Refresh(); }

    public int Index => Model.Index;
    public string Name => Model.Name;
    public ObservableCollection<EffectEntry> Effects { get; } = new();
    public ObservableCollection<ItemEntry> Items { get; } = new();

    public string Label => $"{Name}  ({Effects.Count} effect{(Effects.Count == 1 ? "" : "s")})";
    public string ItemLabel => $"{Name}  ({Items.Count} item{(Items.Count == 1 ? "" : "s")})";

    public void Refresh()
    {
        Effects.Clear();
        foreach (var e in Model.Effects) Effects.Add(e);
        Items.Clear();
        foreach (var it in Model.Items) Items.Add(it);
        OnPropertyChanged(nameof(Label));
        OnPropertyChanged(nameof(ItemLabel));
    }
}

/// <summary>
/// Offline save-game effect ("powers") editor. Loads a Gold Box save folder's CHRDATAn files,
/// shows each character's effects, and assigns chosen effects to a character or the whole party by
/// rewriting the .SPC files. Edits files only — the game must be closed and the save reloaded.
/// </summary>
public sealed class SaveEditorViewModel : ObservableObject
{
    private SaveGame? _save;
    private bool _backedUp;
    private string? _lastBackup;

    private readonly List<EffectPickViewModel> _allEffects;

    public ObservableCollection<SaveCharacterViewModel> Characters { get; } = new();
    public ObservableCollection<EffectPickViewModel> Effects { get; } = new();   // filtered view

    public SaveEditorViewModel()
    {
        _allEffects = EffectBook.All.Select(e => new EffectPickViewModel(e)).ToList();
        ApplyFilter();

        LoadCommand = new RelayCommand(_ => Load());
        ApplyAllCommand = new RelayCommand(_ => Apply(all: true), _ => CanApply);
        ApplySelectedCommand = new RelayCommand(_ => Apply(all: false), _ => CanApply && SelectedCharacter != null);
        RemoveEffectCommand = new RelayCommand(_ => RemoveSelectedEffect(),
            _ => SelectedCharacter != null && SelectedEffect != null);
        CheckSurvivalCommand = new RelayCommand(_ => CheckSet(EffectBook.SurvivalSet));
        ClearChecksCommand = new RelayCommand(_ => { foreach (var e in _allEffects) e.IsChecked = false; });
        IdentifyItemsSelectedCommand = new RelayCommand(_ => IdentifyItems(all: false), _ => _save != null && SelectedCharacter != null);
        IdentifyItemsAllCommand = new RelayCommand(_ => IdentifyItems(all: true), _ => _save != null);
        DuplicateInventoryCommand = new RelayCommand(_ => DuplicateInventory(),
            _ => _save != null && DuplicateSource != null && SelectedCharacter != null && DuplicateSource != SelectedCharacter);
    }

    // --- state ---------------------------------------------------------------
    private string _saveFolder = @"C:\Temp\Games\POOLRAD";
    public string SaveFolder { get => _saveFolder; set => SetProperty(ref _saveFolder, value); }

    private string _status =
        "Point at a Gold Box save folder (containing CHRDATAn.SAV) and Load. Close the game first.";
    public string Status { get => _status; set => SetProperty(ref _status, value); }

    public bool IsLoaded => _save != null;

    private SaveCharacterViewModel? _selectedCharacter;
    public SaveCharacterViewModel? SelectedCharacter
    {
        get => _selectedCharacter;
        set { if (SetProperty(ref _selectedCharacter, value)) RaiseItemCommands(); }
    }

    /// <summary>The "copy inventory from" character for the duplicate-inventory action.</summary>
    private SaveCharacterViewModel? _duplicateSource;
    public SaveCharacterViewModel? DuplicateSource
    {
        get => _duplicateSource;
        set { if (SetProperty(ref _duplicateSource, value)) RaiseItemCommands(); }
    }

    private void RaiseItemCommands()
    {
        (IdentifyItemsSelectedCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (IdentifyItemsAllCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (DuplicateInventoryCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    private EffectEntry? _selectedEffect;
    public EffectEntry? SelectedEffect { get => _selectedEffect; set => SetProperty(ref _selectedEffect, value); }

    private string _filter = "";
    public string Filter { get => _filter; set { if (SetProperty(ref _filter, value)) ApplyFilter(); } }

    private bool _beneficialOnly = true;
    public bool BeneficialOnly { get => _beneficialOnly; set { if (SetProperty(ref _beneficialOnly, value)) ApplyFilter(); } }

    public bool CanApply => _save != null && _allEffects.Any(e => e.IsChecked);

    // --- commands ------------------------------------------------------------
    public ICommand LoadCommand { get; }
    public ICommand ApplyAllCommand { get; }
    public ICommand ApplySelectedCommand { get; }
    public ICommand RemoveEffectCommand { get; }
    public ICommand CheckSurvivalCommand { get; }
    public ICommand ClearChecksCommand { get; }
    public ICommand IdentifyItemsSelectedCommand { get; }
    public ICommand IdentifyItemsAllCommand { get; }
    public ICommand DuplicateInventoryCommand { get; }

    // --- logic ---------------------------------------------------------------
    private void ApplyFilter()
    {
        Effects.Clear();
        foreach (var e in _allEffects.Where(e =>
                     (!BeneficialOnly || e.Beneficial) &&
                     (string.IsNullOrWhiteSpace(Filter)
                        || e.Name.Contains(Filter, StringComparison.OrdinalIgnoreCase)
                        || e.Hex.Contains(Filter, StringComparison.OrdinalIgnoreCase))))
            Effects.Add(e);
    }

    private void CheckSet(byte[] codes)
    {
        var set = codes.ToHashSet();
        foreach (var e in _allEffects) if (set.Contains(e.Code)) e.IsChecked = true;
        Status = "Checked the survival set (keeps fighting when unconscious + troll regen + regen 3/round).";
    }

    private void Load()
    {
        try
        {
            if (!SaveGame.LooksLikeSaveFolder(SaveFolder))
            {
                Status = "No CHRDATAn.SAV files found in that folder.";
                return;
            }
            _save = SaveGame.Load(SaveFolder);
            _backedUp = false;
            _lastBackup = null;
            // Start each newly-loaded save with a clean selection so a stale checklist (or filter)
            // from a previous save folder can't be applied to a different party by accident.
            foreach (var e in _allEffects) e.IsChecked = false;
            _filter = "";
            OnPropertyChanged(nameof(Filter));
            ApplyFilter();
            Characters.Clear();
            foreach (var c in _save.Characters) Characters.Add(new SaveCharacterViewModel(c));
            DuplicateSource = null;
            SelectedCharacter = Characters.FirstOrDefault();
            RaiseItemCommands();
            OnPropertyChanged(nameof(IsLoaded));
            Status = $"Loaded {Characters.Count} character(s). A backup is made automatically before the first change.";
        }
        catch (Exception ex)
        {
            _save = null;
            Characters.Clear();
            OnPropertyChanged(nameof(IsLoaded));
            Status = "Load failed: " + ex.Message;
        }
    }

    private void EnsureBackup()
    {
        if (_save == null || _backedUp) return;
        _lastBackup = _save.Backup();
        _backedUp = true;
    }

    private void Apply(bool all)
    {
        if (_save == null) return;
        var codes = _allEffects.Where(e => e.IsChecked).Select(e => e.Code).ToArray();
        if (codes.Length == 0) { Status = "No effects checked."; return; }

        var targets = all
            ? Characters.ToList()
            : SelectedCharacter != null ? new List<SaveCharacterViewModel> { SelectedCharacter } : new();
        if (targets.Count == 0) { Status = "No character selected."; return; }

        // If every checked effect is already present on every target, there's nothing to do —
        // report it and don't create a backup folder for a no-op.
        bool willChange = targets.Any(t => codes.Any(code => t.Model.Effects.All(e => e.Type != code)));
        if (!willChange) { Status = "Nothing to add — the selected effects are already present."; return; }

        try
        {
            EnsureBackup();
            int totalAdded = 0;
            foreach (var cvm in targets)
            {
                int added = 0;
                foreach (var code in codes) if (SaveGame.AddEffect(cvm.Model, code)) added++;
                if (added > 0) { SaveGame.Write(cvm.Model); cvm.Refresh(); totalAdded += added; }
            }
            Status = $"Added {totalAdded} effect(s) across {targets.Count} character(s). " +
                     $"Backup: {_lastBackup}. Reload the save in the game to see them.";
        }
        catch (Exception ex) { Status = "Apply failed: " + ex.Message; }
    }

    private void RemoveSelectedEffect()
    {
        if (_save == null || SelectedCharacter == null || SelectedEffect == null) return;
        try
        {
            EnsureBackup();
            SelectedCharacter.Model.Effects.Remove(SelectedEffect);
            SaveGame.Write(SelectedCharacter.Model);
            SelectedCharacter.Refresh();
            Status = $"Removed an effect from {SelectedCharacter.Name}. Backup: {_lastBackup}.";
        }
        catch (Exception ex) { Status = "Remove failed: " + ex.Message; }
    }

    // --- items ---------------------------------------------------------------
    private void IdentifyItems(bool all)
    {
        if (_save == null) return;
        var targets = all
            ? Characters.ToList()
            : SelectedCharacter != null ? new List<SaveCharacterViewModel> { SelectedCharacter } : new();
        if (targets.Count == 0) { Status = "No character selected."; return; }

        // Nothing to do if every item on every target is already identified — don't back up a no-op.
        bool willChange = targets.Any(t => t.Model.Items.Any(it => !it.Identified));
        if (!willChange) { Status = "Nothing to identify — all items are already identified."; return; }

        try
        {
            EnsureBackup();
            int total = 0, chars = 0;
            foreach (var cvm in targets)
            {
                int n = SaveGame.IdentifyAll(cvm.Model);
                if (n > 0) { cvm.Refresh(); total += n; chars++; }
            }
            Status = $"Identified {total} item(s) across {chars} character(s). " +
                     $"Backup: {_lastBackup}. Reload the save in the game to see the full names.";
        }
        catch (Exception ex) { Status = "Identify failed: " + ex.Message; }
    }

    private void DuplicateInventory()
    {
        if (_save == null || DuplicateSource == null || SelectedCharacter == null) return;
        if (DuplicateSource == SelectedCharacter) { Status = "Pick two different characters."; return; }
        try
        {
            EnsureBackup();
            int n = SaveGame.DuplicateInventory(DuplicateSource.Model, SelectedCharacter.Model);
            SelectedCharacter.Refresh();
            Status = $"Copied {n} item(s) from {DuplicateSource.Name} onto {SelectedCharacter.Name}, " +
                     $"replacing {SelectedCharacter.Name}'s inventory. Backup: {_lastBackup}. " +
                     "Reload the save in the game.";
        }
        catch (Exception ex) { Status = "Duplicate failed: " + ex.Message; }
    }
}

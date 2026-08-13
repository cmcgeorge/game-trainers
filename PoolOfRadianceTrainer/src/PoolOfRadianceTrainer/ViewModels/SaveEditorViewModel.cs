using System.Collections.ObjectModel;
using System.Windows.Input;
using PoolOfRadianceTrainer.Game;
using PoolOfRadianceTrainer.Memory;
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

/// <summary>One carried item in a loaded save file. Wraps the record so the ID'd column can be
/// ticked directly, writing the change straight back to the character's .ITM file.</summary>
public sealed class SaveItemViewModel : ObservableObject
{
    private readonly Action<SaveItemViewModel> _persist;

    public ItemEntry Item { get; }

    public SaveItemViewModel(ItemEntry item, Action<SaveItemViewModel> persist)
    {
        Item = item;
        _persist = persist;
    }

    public string DisplayName => Item.DisplayName;
    public bool Readied => Item.Readied;
    public int Count => Item.Count;
    public int Value => Item.Value;
    public string ChargesDisplay => Item.ChargesDisplay;
    public string Tags => Item.Tags;

    /// <summary>Whether the item is identified. Ticking it reveals every name part and rewrites the
    /// .ITM file; the game shows the full name once the save is reloaded.</summary>
    public bool Identified
    {
        get => Item.Identified;
        set
        {
            if (Item.Identified == value) return;
            if (!Item.SetIdentified(value)) { OnPropertyChanged(); return; }
            _persist(this);
            Raise();
        }
    }

    public void Raise()
    {
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(Identified));
        OnPropertyChanged(nameof(Tags));
    }

    public override string ToString() => DisplayName;
}

/// <summary>A character in the loaded save, with its live effect list.</summary>
public sealed class SaveCharacterViewModel : ObservableObject
{
    private readonly Action<SaveItemViewModel> _persistItem;

    public SaveCharacter Model { get; }

    public SaveCharacterViewModel(SaveCharacter model, Action<SaveItemViewModel> persistItem)
    {
        Model = model;
        _persistItem = persistItem;
        Refresh();
    }

    public int Index => Model.Index;
    public string Name => Model.Name;
    public ObservableCollection<EffectEntry> Effects { get; } = new();
    public ObservableCollection<SaveItemViewModel> Items { get; } = new();

    public string Label => $"{Name}  ({Effects.Count} effect{(Effects.Count == 1 ? "" : "s")})";
    public string ItemLabel => $"{Name}  ({Items.Count} item{(Items.Count == 1 ? "" : "s")})";

    public void Refresh()
    {
        Effects.Clear();
        foreach (var e in Model.Effects) Effects.Add(e);
        Items.Clear();
        foreach (var it in Model.Items) Items.Add(new SaveItemViewModel(it, _persistItem));
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

        // Open on the folder the game is really saving into, and load it, so the character and item
        // lists (and the buttons that need a loaded save) are usable without hunting for a path.
        string? found = SaveFolderLocator.Find();
        if (found != null)
        {
            _saveFolder = found;
            Load();
        }
    }

    // --- state ---------------------------------------------------------------
    private string _saveFolder = DefaultSaveFolder;
    public string SaveFolder { get => _saveFolder; set => SetProperty(ref _saveFolder, value); }

    /// <summary>Shown until the game's own save folder is located — a plausible manual mount point,
    /// not somewhere the app expects to find anything.</summary>
    private const string DefaultSaveFolder = @"C:\Temp\Games\POOLRAD";

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
                Status = "No CHRDATn.SAV save files found in that folder. " +
                         "Point at the folder the game saves into (for a GOG install that is the " +
                         "cloud_saves\\POOLRAD folder beside the game, not POOLRAD itself).";
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
            foreach (var c in _save.Characters) Characters.Add(new SaveCharacterViewModel(c, PersistItem));
            DuplicateSource = null;
            SelectedCharacter = Characters.FirstOrDefault();
            RaiseItemCommands();
            OnPropertyChanged(nameof(IsLoaded));
            int slots = SaveGame.Slots(SaveFolder).Count;
            Status = $"Loaded {Characters.Count} character(s) from save slot {_save.Slot}" +
                     (slots > 1 ? $" (the most recent of {slots} saves in this folder)" : "") +
                     ". A backup is made automatically before the first change.";
        }
        catch (Exception ex)
        {
            _save = null;
            Characters.Clear();
            OnPropertyChanged(nameof(IsLoaded));
            Status = "Load failed: " + ex.Message;
        }
    }

    /// <summary>
    /// Asks the user to confirm before an edit that could be lost. The window supplies a real
    /// dialog; left unset (headless) every edit goes ahead.
    /// </summary>
    public Func<string, bool> Confirm { get; set; } = _ => true;

    private bool _warnedGameRunning;

    /// <summary>
    /// Everything a mutating command must do before it writes: warn once if the game is running,
    /// then take the session's one-shot backup. Returns false when the user backs out.
    ///
    /// <para>The warning matters because these edits go to files, not to the running game: the game
    /// holds the party in memory and writes the save on its own schedule, so an edit applied
    /// underneath it survives only until it next saves — and if it saves after the edit, the edit is
    /// simply gone. The atomic write protects the file from being truncated; it cannot protect it
    /// from being overwritten a minute later.</para>
    /// </summary>
    private bool ReadyToWrite()
    {
        if (_save == null) return false;
        if (!_warnedGameRunning && SaveFolderLocator.EmulatorRunning())
        {
            _warnedGameRunning = true;   // ask once per session, not once per button
            if (!Confirm("Pool of Radiance looks like it is still running.\n\n" +
                         "Save-file edits are applied to the files on disk, so the running game will " +
                         "overwrite them the next time it saves — and it may already hold the party in " +
                         "memory. Quit the game first, or use the live tabs to edit it as it runs.\n\n" +
                         "Apply the edit anyway?"))
                return false;
        }
        EnsureBackup();
        return true;
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
            if (!ReadyToWrite()) { Status = "Edit cancelled."; return; }
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
            if (!ReadyToWrite()) { Status = "Edit cancelled."; return; }
            SelectedCharacter.Model.Effects.Remove(SelectedEffect);
            SaveGame.Write(SelectedCharacter.Model);
            SelectedCharacter.Refresh();
            Status = $"Removed an effect from {SelectedCharacter.Name}. Backup: {_lastBackup}.";
        }
        catch (Exception ex) { Status = "Remove failed: " + ex.Message; }
    }

    // --- items ---------------------------------------------------------------
    /// <summary>Writes one item's edited record back to its owner's .ITM file. Called by a
    /// <see cref="SaveItemViewModel"/> when its ID'd checkbox is ticked.</summary>
    private void PersistItem(SaveItemViewModel item)
    {
        var owner = Characters.FirstOrDefault(c => c.Items.Contains(item));
        if (_save == null || owner == null) return;
        try
        {
            if (!ReadyToWrite()) { Status = "Edit cancelled."; return; }
            SaveGame.WriteItems(owner.Model);
            Status = $"{(item.Identified ? "Identified" : "Re-hid")} '{item.DisplayName}' on {owner.Name}. " +
                     $"Backup: {_lastBackup}. Reload the save in the game to see it.";
        }
        catch (Exception ex)
        {
            item.Item.SetIdentified(!item.Identified);   // put the record back the way it was
            item.Raise();
            Status = "Item write failed: " + ex.Message;
        }
    }

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
            if (!ReadyToWrite()) { Status = "Edit cancelled."; return; }
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
            if (!ReadyToWrite()) { Status = "Edit cancelled."; return; }
            int n = SaveGame.DuplicateInventory(DuplicateSource.Model, SelectedCharacter.Model);
            SelectedCharacter.Refresh();
            Status = $"Copied {n} item(s) from {DuplicateSource.Name} onto {SelectedCharacter.Name}, " +
                     $"replacing {SelectedCharacter.Name}'s inventory. Backup: {_lastBackup}. " +
                     "Reload the save in the game.";
        }
        catch (Exception ex) { Status = "Duplicate failed: " + ex.Message; }
    }
}

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using BardsTaleTrilogyTrainer.Game;
using BardsTaleTrilogyTrainer.Memory;

namespace BardsTaleTrilogyTrainer.ViewModels;

/// <summary>
/// Top-level view model for the Bard's Tale Trilogy trainer. Handles process
/// attachment, auto-locate, character list, gold editing, spell assignment,
/// item charge editing, and shop editing.
/// </summary>
public sealed class MainViewModel : ObservableObject, ICharacterHost
{
    private ProcessMemory? _proc;
    private IMemorySource? _mem;
    private GameLocation? _location;
    private nuint _moduleBase;
    private System.Threading.Timer? _pollTimer;

    private string _statusMessage = "Attach to the game to begin.";
    private bool _isAttached;
    private bool _isLocating;
    private int _selectedCharacterIndex = -1;
    private int _gold;
    private string _spellCode = "";
    private string _scanValueText = "";

    public ObservableCollection<CharacterViewModel> Characters { get; } = new();
    public CharacterViewModel? SelectedCharacter =>
        _selectedCharacterIndex >= 0 && _selectedCharacterIndex < Characters.Count
            ? Characters[_selectedCharacterIndex] : null;

    public string StatusMessage { get => _statusMessage; set => SetField(ref _statusMessage, value); }
    public bool IsAttached
    {
        get => _isAttached;
        set { SetField(ref _isAttached, value); RaiseAllCanExecuteChanged(); }
    }
    public bool IsLocating
    {
        get => _isLocating;
        set { SetField(ref _isLocating, value); RaiseAllCanExecuteChanged(); }
    }
    public int SelectedCharacterIndex
    {
        get => _selectedCharacterIndex;
        set { SetField(ref _selectedCharacterIndex, value); OnPropertyChanged(nameof(SelectedCharacter)); RaiseAllCanExecuteChanged(); }
    }
    public int Gold { get => _gold; set => SetField(ref _gold, value); }
    public string SpellCode { get => _spellCode; set => SetField(ref _spellCode, value); }
    public string ScanValueText { get => _scanValueText; set => SetField(ref _scanValueText, value); }

    // --- commands ---
    public RelayCommand AttachCommand { get; }
    public RelayCommand LocateCommand { get; }
    public RelayCommand WriteGoldCommand { get; }
    public RelayCommand AssignSpellCommand { get; }
    public RelayCommand LearnAllSpellsAllCommand { get; }
    public RelayCommand SetInfiniteItemsAllCommand { get; }
    public RelayCommand SetGarthShopAllCommand { get; }
    public RelayCommand DetachCommand { get; }

    public MainViewModel()
    {
        AttachCommand = new RelayCommand(_ => Attach(), _ => !IsAttached);
        LocateCommand = new RelayCommand(_ => Locate(), _ => IsAttached);
        WriteGoldCommand = new RelayCommand(_ => WriteGold(), _ => IsAttached && _location != null);
        AssignSpellCommand = new RelayCommand(_ => AssignSpell(), _ => IsAttached && SelectedCharacter != null && !string.IsNullOrWhiteSpace(SpellCode));
        LearnAllSpellsAllCommand = new RelayCommand(_ => LearnAllSpellsAll(), _ => IsAttached && Characters.Count > 0);
        SetInfiniteItemsAllCommand = new RelayCommand(_ => SetInfiniteItemsAll(), _ => IsAttached && Characters.Count > 0);
        SetGarthShopAllCommand = new RelayCommand(_ => SetGarthShopAll(), _ => IsAttached);
        DetachCommand = new RelayCommand(_ => Detach(), _ => IsAttached);
    }

    private void Attach()
    {
        var proc = GameLocator.FindGameProcess();
        if (proc == null)
        {
            StatusMessage = $"Process '{GameFacts.ProcessName}.exe' not found. Start the game first.";
            return;
        }

        try
        {
            _proc = ProcessMemory.Open(proc.Id);
            try
            {
                _mem = new ProcessMemorySource(_proc);
                _moduleBase = GameLocator.FindModuleBase(proc, GameFacts.GameModuleName);
                IsAttached = true;
                StatusMessage = _moduleBase != 0
                    ? $"Attached to PID {proc.Id}. {GameFacts.GameModuleName} @ 0x{_moduleBase:X}. Click Locate."
                    : $"Attached to PID {proc.Id}. {GameFacts.GameModuleName} not found — locate will use structural scan.";

                _pollTimer = new System.Threading.Timer(_ => PollCallback(), null, 500, 500);
            }
            catch
            {
                _proc.Dispose();
                _proc = null;
                _mem = null;
                throw;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Attach failed: {ex.Message}";
        }
    }

    private void Locate()
    {
        if (_mem == null) return;
        IsLocating = true;
        StatusMessage = "Scanning memory for character data...";

        Task.Run(() =>
        {
            try
            {
                _location = GameLocator.Locate(_mem, _moduleBase);
                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    IsLocating = false;
                    if (_location == null)
                    {
                        StatusMessage = "No character data found. Try the value scanner tab or ensure a party is loaded in-game.";
                        return;
                    }

                    Characters.Clear();
                    int idx = 0;
                    foreach (var addr in _location.CharacterAddresses)
                    {
                        var record = new CharacterRecord(_mem!, addr, idx);
                        if (record.IsOccupied)
                            Characters.Add(new CharacterViewModel(record, this));
                        idx++;
                    }

                    if (Characters.Count > 0)
                    {
                        SelectedCharacterIndex = 0;
                        ReadGold();
                    }

                    StatusMessage = _location.Summary +
                        (_location.UsedFallback ? " (fallback scan)" : "") +
                        $". {Characters.Count} characters loaded.";
                    RaiseAllCanExecuteChanged();
                });
            }
            catch (OperationCanceledException)
            {
                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    IsLocating = false;
                    StatusMessage = "Scan cancelled.";
                });
            }
            catch (Exception ex)
            {
                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    IsLocating = false;
                    StatusMessage = $"Scan failed: {ex.Message}";
                });
            }
        });
    }

    private void ReadGold()
    {
        if (_mem == null || _location == null || _location.PartyObject == 0) return;
        var buf = new byte[4];
        if (_mem.Read(_location.PartyObject + (nuint)GameFacts.PartyGoldOffset, buf, 4) == 4)
        {
            Gold = buf[0] | (buf[1] << 8) | (buf[2] << 16) | (buf[3] << 24);
        }
    }

    private void WriteGold()
    {
        if (_mem == null || _location == null || _location.PartyObject == 0) return;
        var buf = new byte[]
        {
            (byte)(_gold & 0xFF),
            (byte)((_gold >> 8) & 0xFF),
            (byte)((_gold >> 16) & 0xFF),
            (byte)((_gold >> 24) & 0xFF),
        };
        if (_mem.Write(_location.PartyObject + (nuint)GameFacts.PartyGoldOffset, buf))
            OnMessage($"Gold set to {_gold}");
        else
            OnMessage("Failed to write gold.");
    }

    private void AssignSpell()
    {
        var chr = SelectedCharacter;
        if (chr == null) return;
        var spell = Spellbook.FindByCode(SpellCode.Trim());
        if (spell == null)
        {
            OnMessage($"Unknown spell code '{SpellCode}'. Valid codes: ZZGO, NUKE, ARFI, MAFL, …");
            return;
        }

        int spellClassIdx = spell.Class switch
        {
            SpellClass.Conjurer => 0,
            SpellClass.Magician => 1,
            SpellClass.Sorcerer => 2,
            SpellClass.Wizard => 3,
            _ => -1,
        };

        if (spellClassIdx >= 0)
        {
            int cur = chr.ConjurerLevel;
            if (spellClassIdx == 1) cur = chr.MagicianLevel;
            if (spellClassIdx == 2) cur = chr.SorcererLevel;
            if (spellClassIdx == 3) cur = chr.WizardLevel;

            if (cur < spell.Level)
            {
                switch (spellClassIdx)
                {
                    case 0: chr.ConjurerLevel = (byte)spell.Level; chr.WriteSpellLevels(); break;
                    case 1: chr.MagicianLevel = (byte)spell.Level; chr.WriteSpellLevels(); break;
                    case 2: chr.SorcererLevel = (byte)spell.Level; chr.WriteSpellLevels(); break;
                    case 3: chr.WizardLevel = (byte)spell.Level; chr.WriteSpellLevels(); break;
                }
                OnMessage($"{chr.Name}: set {Spellbook.ArtName(spell.Class)} to level {spell.Level} (grants {spell.Code} — {spell.Name})");
            }
            else
            {
                OnMessage($"{chr.Name}: already has {spell.Class} level {cur} (≥ {spell.Level} needed for {spell.Code})");
            }
        }
        else if (spell.Class == SpellClass.Archmage)
        {
            // Archmage spells require all four standard magic class levels at the spell's level.
            int needed = spell.Level;
            if (chr.ConjurerLevel < needed) chr.ConjurerLevel = (byte)needed;
            if (chr.MagicianLevel < needed) chr.MagicianLevel = (byte)needed;
            if (chr.SorcererLevel < needed) chr.SorcererLevel = (byte)needed;
            if (chr.WizardLevel < needed) chr.WizardLevel = (byte)needed;
            chr.WriteSpellLevels();
            OnMessage($"{chr.Name}: set all four magic classes to level {needed} for Archmage spell {spell.Code} — {spell.Name}");
        }
        else if (spell.Class == SpellClass.Chronomancer || spell.Class == SpellClass.Geomancer)
        {
            OnMessage($"{chr.Name}: {spell.Code} ({spell.Name}) is a {spell.Class} spell. The {spell.Class} spell-level offset has not been located yet — these are BT2/BT3 advanced classes with their own spell tracking.");
        }
        else
        {
            // Any Magic User spells (ZZGO, NUKE, etc.) — these require the character
            // to be a magic user. The remaster likely stores these as additional
            // spell knowledge beyond the four class levels. Setting all class levels
            // to 7 grants access to all standard spells; the special cross-game
            // spells may require additional flags we haven't located.
            chr.LearnAllSpells();
            OnMessage($"{chr.Name}: {spell.Code} ({spell.Name}) is an 'Any Magic User' spell. All class levels set to 7 — if the spell doesn't appear, it may require a flag we haven't located yet.");
        }
    }

    private void LearnAllSpellsAll()
    {
        foreach (var chr in Characters)
            chr.LearnAllSpells();
        OnMessage($"All {Characters.Count} characters: learned all class spells");
    }

    private void SetInfiniteItemsAll()
    {
        foreach (var chr in Characters)
            chr.SetInfiniteItems();
        OnMessage($"All {Characters.Count} characters: item charges set to 0 (infinite)");
    }

    private void SetGarthShopAll()
    {
        if (_mem == null || _location == null)
        {
            OnMessage("Not attached or not located. Attach and locate first.");
            return;
        }

        // Garth's shop inventory location has not been confirmed against a live
        // game session. The shop data is likely on the game-state object or a
        // sub-object accessible through the pointer chain. When the game is
        // available, run Il2CppDumper to find the shop manager class and its
        // inventory array offset. For now, this feature requires the user to
        // locate the shop data through the value scanner.
        OnMessage("Garth's shop editor: the shop inventory offset has not been confirmed against a live game. Use the value scanner to find the shop's item array, or run Il2CppDumper to locate the ShopManager class.");
    }

    private void Detach()
    {
        _pollTimer?.Dispose();
        _pollTimer = null;
        _proc?.Dispose();
        _proc = null;
        _mem = null;
        _location = null;
        _moduleBase = 0;
        IsAttached = false;
        Characters.Clear();
        SelectedCharacterIndex = -1;
        Gold = 0;
        StatusMessage = "Detached. Attach to the game to begin.";
    }

    private void PollCallback()
    {
        if (_mem == null || _location == null) return;
        try
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                foreach (var chr in Characters)
                    chr.PollFreezes();
            });
        }
        catch { }
    }

    public void OnMessage(string msg)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            StatusMessage = msg;
        });
    }

    private void RaiseAllCanExecuteChanged()
    {
        AttachCommand.RaiseCanExecuteChanged();
        LocateCommand.RaiseCanExecuteChanged();
        WriteGoldCommand.RaiseCanExecuteChanged();
        AssignSpellCommand.RaiseCanExecuteChanged();
        LearnAllSpellsAllCommand.RaiseCanExecuteChanged();
        SetInfiniteItemsAllCommand.RaiseCanExecuteChanged();
        SetGarthShopAllCommand.RaiseCanExecuteChanged();
        DetachCommand.RaiseCanExecuteChanged();
    }
}

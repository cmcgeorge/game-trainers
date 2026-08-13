using System.Collections.ObjectModel;
using PoolOfRadianceTrainer.Game;
using PoolOfRadianceTrainer.Mvvm;

namespace PoolOfRadianceTrainer.ViewModels;

/// <summary>
/// "Generate a party": rolls a ready-to-play party of good-aligned level-1 characters (see
/// <see cref="PartyGenerator"/>) and writes it either over the party in the running game or over
/// the characters in a loaded save folder.
///
/// <para>Both targets replace characters that already exist rather than adding new ones. How many
/// party members a game has is the game's own bookkeeping — in memory the party is a linked list
/// the engine builds, and on disk the count lives somewhere in <c>SAVGAM?.DAT</c>, which this
/// trainer has not decoded — so the generator fills the slots it can see and says so when the party
/// it rolled is bigger than the party it found.</para>
/// </summary>
public sealed class PartyGeneratorViewModel : ObservableObject
{
    private readonly Func<IReadOnlyList<CharacterViewModel>> _getLiveParty;
    private readonly SaveEditorViewModel _saveEditor;
    private readonly Action<string> _setStatus;
    private readonly Random _rng = new();

    /// <summary>The rolled party, in marching order. A preview until it is applied to something.</summary>
    public ObservableCollection<RolledCharacter> Preview { get; } = new();

    public PartyGeneratorViewModel(Func<IReadOnlyList<CharacterViewModel>> getLiveParty,
                                   SaveEditorViewModel saveEditor,
                                   Action<string> setStatus)
    {
        _getLiveParty = getLiveParty;
        _saveEditor = saveEditor;
        _setStatus = setStatus;

        RollCommand = new RelayCommand(_ => RollParty());
        ApplyLiveCommand = new RelayCommand(_ => ApplyLive(), _ => Preview.Count > 0 && LiveCount > 0);
        ApplySaveCommand = new RelayCommand(_ => ApplyToSave(), _ => Preview.Count > 0 && _saveEditor.LoadedCharacterCount > 0);

        RollParty();   // open on a party rather than an empty list
    }

    /// <summary>
    /// Asks before replacing characters that already exist. The window supplies a real dialog;
    /// left unset (headless) every write goes ahead.
    /// </summary>
    public Func<string, bool> Confirm { get; set; } = _ => true;

    // --- options --------------------------------------------------------------
    private int _partySize = PartyGenerator.MaxParty;
    /// <summary>How many characters to roll (1..6). A short party keeps the roles that matter:
    /// four is always a fighter, a cleric, a magic-user and a thief.</summary>
    public int PartySize
    {
        get => _partySize;
        set
        {
            if (SetProperty(ref _partySize, Math.Clamp(value, 1, PartyGenerator.MaxParty)))
                RollParty();
        }
    }

    public int[] PartySizeOptions { get; } = Enumerable.Range(1, PartyGenerator.MaxParty).ToArray();

    private bool _heroicRolls = true;
    /// <summary>Roll four dice and drop the lowest (on) or the game's own three (off).</summary>
    public bool HeroicRolls
    {
        get => _heroicRolls;
        set { if (SetProperty(ref _heroicRolls, value)) { RollParty(); OnPropertyChanged(nameof(RollStyleNote)); } }
    }

    public string RollStyleNote => _heroicRolls
        ? "Rolling 4d6 and dropping the lowest die — averages 12.2 an ability, and never rolls a 3."
        : "Rolling straight 3d6, exactly as the game's own create screen does — averages 10.5 an ability.";

    // --- readouts -------------------------------------------------------------
    private int LiveCount => _getLiveParty().Count;

    public string LiveTargetSummary
    {
        get
        {
            var party = _getLiveParty();
            if (party.Count == 0)
                return "No live party found — attach to the running game and Scan first. " +
                       "The game must already have the characters; this replaces them, it can't add any.";
            string names = string.Join(", ", party.Select(c => c.Record.Name));
            return $"{party.Count} party member(s) in the running game: {names}.";
        }
    }

    public string SaveTargetSummary => _saveEditor.LoadedSlotSummary;

    /// <summary>Re-reads both targets — call when the live party or the loaded save changes.</summary>
    public void Refresh()
    {
        OnPropertyChanged(nameof(LiveTargetSummary));
        OnPropertyChanged(nameof(SaveTargetSummary));
    }

    // --- commands -------------------------------------------------------------
    public RelayCommand RollCommand { get; }
    public RelayCommand ApplyLiveCommand { get; }
    public RelayCommand ApplySaveCommand { get; }

    private void RollParty()
    {
        Preview.Clear();
        var style = _heroicRolls ? RollStyle.FourD6DropLowest : RollStyle.ThreeD6;
        foreach (var c in PartyGenerator.Generate(_rng, _partySize, style)) Preview.Add(c);
        OnPropertyChanged(nameof(Summary));
        Refresh();
    }

    public string Summary => Preview.Count == 0
        ? ""
        : $"{Preview.Count} character(s) rolled: " +
          string.Join(", ", Preview.Select(c => $"{c.Name} the {PorFormat.ClassName(c.Class)}")) + ".";

    private void ApplyLive()
    {
        var party = _getLiveParty();
        if (Preview.Count == 0 || party.Count == 0) return;

        int n = Math.Min(Preview.Count, party.Count);
        if (!Confirm($"Replace {n} character(s) in the running game with the generated party?\n\n" +
                     $"{string.Join("\n", party.Take(n).Select((c, i) => $"{c.Record.Name}  →  {Preview[i].Title}"))}\n\n" +
                     "Their money and carried items stay with the slot — only the character sheet changes. " +
                     "There is no undo, and the game will save these characters the next time you save."))
        {
            _setStatus("Party generation cancelled.");
            return;
        }

        for (int i = 0; i < n; i++) party[i].ApplyGenerated(Preview[i]);
        Refresh();
        _setStatus($"Replaced {n} party member(s) with the generated party. " +
                   (Preview.Count > n
                        ? $"The game's party holds only {n}, so {Preview.Count - n} rolled character(s) were not used. "
                        : "") +
                   "Their money and items are untouched, so readied armour may no longer suit the new class — " +
                   "re-ready it in the game and the AC recomputes.");
    }

    private void ApplyToSave()
    {
        if (Preview.Count == 0 || _saveEditor.LoadedCharacterCount == 0) return;

        int n = Math.Min(Preview.Count, _saveEditor.LoadedCharacterCount);
        if (!Confirm($"Write {n} generated character(s) over the loaded save?\n\n" +
                     _saveEditor.LoadedSlotSummary + "\n\n" +
                     "A backup of the whole save folder is taken first. Close the game before doing this."))
        {
            _setStatus("Party generation cancelled.");
            return;
        }

        int written = _saveEditor.ApplyGeneratedParty(Preview.ToList());
        Refresh();
        if (written > 0) _setStatus(_saveEditor.Status);
    }
}

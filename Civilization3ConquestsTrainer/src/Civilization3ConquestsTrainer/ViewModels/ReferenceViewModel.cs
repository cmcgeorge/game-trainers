using System.Collections.ObjectModel;
using Civilization3ConquestsTrainer.Game;

namespace Civilization3ConquestsTrainer.ViewModels;

/// <summary>
/// The References tab: the nine shipped conquests, the behaviour notes that explain why this trainer
/// works the way it does, and — once a game is located — the civilization and unit tables read out of
/// whatever ruleset is actually loaded.
/// </summary>
public sealed class ReferenceViewModel : ObservableObject
{
    /// <summary>The nine conquests Conquests ships with.</summary>
    public IReadOnlyList<Conquest> Conquests => ConquestBook.All;

    /// <summary>Behaviour notes — chiefly the gold obfuscation, which surprises everyone.</summary>
    public IReadOnlyList<BehaviourNote> Notes => ConquestBook.Notes;

    /// <summary>Civilizations in the loaded ruleset. Empty until a game is located.</summary>
    public ObservableCollection<RaceInfo> Races { get; } = new();

    /// <summary>Unit types in the loaded ruleset. Empty until a game is located.</summary>
    public ObservableCollection<UnitTypeInfo> UnitTypes { get; } = new();

    private string _tablesNote = "Attach and Auto-locate to read the civilization and unit tables out of the game.";
    public string TablesNote { get => _tablesNote; private set => SetField(ref _tablesNote, value); }

    /// <summary>Publishes the tables read from a located game.</summary>
    public void Adopt(GameTables tables)
    {
        Races.Clear();
        foreach (var r in tables.Races) Races.Add(r);
        UnitTypes.Clear();
        foreach (var u in tables.UnitTypes) UnitTypes.Add(u);

        TablesNote = tables.Races.Count == 0
            ? "The rules tables could not be read — the BIC layout may differ in this build."
            : $"Read live from the loaded ruleset: {tables.Races.Count} civilizations, " +
              $"{tables.UnitTypes.Count} unit types. A conquest or a mod substitutes its own, which is " +
              "why these are read from the game rather than hard-coded.  " + RolesNote(tables);
    }

    /// <summary>
    /// What the ruleset nominates for the two roles the Units tab can write, and whether the domain
    /// filter is in use. Both are stated because both can legitimately come back unavailable under a
    /// mod, and a user who cannot see why a button is refusing has no way to find out.
    /// </summary>
    private static string RolesNote(GameTables tables)
    {
        string army = tables.ArmyUnitTypeId >= 0
            ? $"army = {tables.UnitTypeName(tables.ArmyUnitTypeId)}"
            : "no army type (the id this ruleset gives does not carry the Army ability)";
        string leader = tables.GreatLeaderUnitTypeId >= 0
            ? $"great leader = {tables.UnitTypeName(tables.GreatLeaderUnitTypeId)}"
            : "no great-leader type, so \"Make great leader\" is unavailable";
        string domains = tables.UnitClassesUsable
            ? "Land/sea/air passed its check, so the Type column offers each unit its own domain."
            : "The land/sea/air field did not pass its check, so the Type column offers every type " +
              "regardless of the \"Any domain\" box.";
        return $"This ruleset's special types: {army}, {leader}.  {domains}";
    }

    /// <summary>Forgets the tables on detach.</summary>
    public void Clear()
    {
        Races.Clear();
        UnitTypes.Clear();
        TablesNote = "Attach and Auto-locate to read the civilization and unit tables out of the game.";
    }
}

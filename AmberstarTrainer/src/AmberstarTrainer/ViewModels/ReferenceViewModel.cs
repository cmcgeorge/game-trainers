using System.Collections.ObjectModel;
using AmberstarTrainer.Game;

namespace AmberstarTrainer.ViewModels;

/// <summary>Reference data tab: spell lists, race/class tables, ailment reference.</summary>
public sealed class ReferenceViewModel : ObservableObject
{
    public record SpellRow(string School, int Bit, string Name);

    public ObservableCollection<SpellRow> AllSpells { get; } = new();

    public ReferenceViewModel()
    {
        BuildSpells(SpellBook.SchoolNames[0], SpellBook.WhiteSpells);
        BuildSpells(SpellBook.SchoolNames[1], SpellBook.GreySpells);
        BuildSpells(SpellBook.SchoolNames[2], SpellBook.BlackSpells);
        BuildSpells(SpellBook.SchoolNames[3], SpellBook.SpecialSpells);
    }

    private void BuildSpells(string school, string[] names)
    {
        for (int i = 0; i < names.Length; i++)
            AllSpells.Add(new SpellRow(school, i + 1, names[i]));
    }
}

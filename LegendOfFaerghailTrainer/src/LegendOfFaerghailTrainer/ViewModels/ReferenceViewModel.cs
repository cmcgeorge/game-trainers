using System.Collections.ObjectModel;
using LegendOfFaerghailTrainer.Game;

namespace LegendOfFaerghailTrainer.ViewModels;

/// <summary>A single row in one of the read-only reference tables.</summary>
public sealed record ReferenceRow(string Id, string Name, string Note);

/// <summary>
/// The read-only reference tables, all recovered from <c>LOF.EXE</c> rather than transcribed from a
/// walkthrough: 186 items with their shop prices, 141 spells, the six races, the thirteen trade
/// slots (with the manual's alternative names), the eight health states, the eight languages, the
/// nine trained abilities, and the eight named regions.
/// </summary>
public sealed class ReferenceViewModel
{
    public ObservableCollection<ReferenceRow> Items { get; } = new();
    public ObservableCollection<ReferenceRow> Spells { get; } = new();
    public ObservableCollection<ReferenceRow> Races { get; } = new();
    public ObservableCollection<ReferenceRow> Classes { get; } = new();
    public ObservableCollection<ReferenceRow> Statuses { get; } = new();
    public ObservableCollection<ReferenceRow> Languages { get; } = new();
    public ObservableCollection<ReferenceRow> Abilities { get; } = new();
    public ObservableCollection<ReferenceRow> Locations { get; } = new();

    public ReferenceViewModel()
    {
        foreach (var it in ItemBook.All)
            Items.Add(new ReferenceRow(it.Id.ToString(), it.Name, it.Price > 0 ? $"{it.Price} gold" : ""));

        foreach (var sp in SpellBook.All)
            Spells.Add(new ReferenceRow(sp.Id.ToString(), sp.Name,
                sp.Id is >= 128 and <= 141 ? "untranslated in this build; a monster or event effect" : ""));

        for (int i = 0; i < RaceBook.Count; i++)
            Races.Add(new ReferenceRow(i.ToString(), RaceBook.NameOf(i), ""));

        for (int i = 0; i < ClassBook.Count; i++)
            Classes.Add(new ReferenceRow(i.ToString(), ClassBook.NameOf(i), ClassBook.DescriptionOf(i)));

        for (int i = 0; i < StatusBook.Count; i++)
            Statuses.Add(new ReferenceRow(i.ToString(), StatusBook.NameOf(i), ""));

        for (int i = 0; i < LanguageBook.Count; i++)
            Languages.Add(new ReferenceRow(i.ToString(), LanguageBook.NameOf(i),
                $"record byte +0x{CharacterFormat.OffLanguages + i:X2}"));

        for (int i = 0; i < AbilityBook.Count; i++)
            Abilities.Add(new ReferenceRow($"+0x{CharacterFormat.AbilityOffsets[i]:X2}",
                AbilityBook.NameOf(i), AbilityBook.DescriptionOf(i)));

        for (int i = 0; i < LocationBook.Names.Length; i++)
            Locations.Add(new ReferenceRow(i.ToString(), LocationBook.Names[i], ""));
    }
}

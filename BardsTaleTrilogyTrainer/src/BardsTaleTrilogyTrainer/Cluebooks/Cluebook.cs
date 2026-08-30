using BardsTaleTrilogyTrainer.Game;

namespace BardsTaleTrilogyTrainer.Cluebooks;

public sealed class CluebookOptions
{
    public bool IncludeSpells { get; init; } = true;
    public bool IncludeClasses { get; init; } = true;
    public bool IncludeItems { get; init; } = true;
    public bool IncludeWalkthrough { get; init; } = true;
    public bool IncludeStrategy { get; init; } = true;
}

public sealed record SpellReference(int Id, string Code, string Name);

public sealed class Cluebook
{
    public required CluebookOptions Options { get; init; }
    public required IReadOnlyList<ClassInfo> Classes { get; init; }
    public required IReadOnlyList<SpellReference> Spells { get; init; }
    public required IReadOnlyList<ItemBook.ItemChoice> Items { get; init; }

    public static Cluebook Build(CluebookOptions? options = null) =>
        new()
        {
            Options = options ?? new CluebookOptions(),
            Classes = ClassBook.Classes,
            Spells = BuildSpells(),
            Items = ItemBook.Choices.Where(item => item.Id != 0).ToArray(),
        };

    private static IReadOnlyList<SpellReference> BuildSpells() =>
        Enumerable.Range((int)SpellId.MageFlame, (int)SpellId.Gotterdamurung + 1)
            .Select(id =>
            {
                var spell = (SpellId)id;
                var special = SpecialSpells.All.FirstOrDefault(entry => entry.Id == spell);
                return new SpellReference(id, special?.Code ?? "", special?.Name ?? SpellCatalog.ReadableName(spell));
            })
            .ToArray();
}

using Space1889Trainer.Game;

namespace Space1889Trainer.ViewModels;

/// <summary>A skill row for the reference tab.</summary>
public sealed record SkillReference(string Attribute, string Skill, string Use);

/// <summary>A career row for the reference tab.</summary>
public sealed record CareerReference(int Id, string Name);

/// <summary>Read-only tables: the item catalogue, skills, careers and story flags.</summary>
public sealed class ReferenceViewModel : ObservableObject
{
    private string _itemFilter = "";

    public IReadOnlyList<ItemInfo> AllItems { get; } = ItemBook.Holdable.ToList();

    public IReadOnlyList<SkillReference> Skills { get; } = Enumerable.Range(0, StateFormat.SkillCount)
        .Select(i => new SkillReference(SkillBook.Attributes[SkillBook.AttributeOf(i)], SkillBook.Skills[i], SkillBook.SkillUses[i]))
        .ToList();

    public IReadOnlyList<CareerReference> Careers { get; } = CareerBook.Names.Select((n, i) => new CareerReference(i, n)).ToList();

    public IReadOnlyList<StoryFlag> StoryFlags => StoryFlagBook.All;

    public IReadOnlyList<string> SocialClasses { get; } =
        SkillBook.SocialClasses.Select((c, i) => $"SOC {i + 1}: {c}").ToList();

    /// <summary>Case-insensitive filter over item name, type and shops.</summary>
    public string ItemFilter
    {
        get => _itemFilter;
        set { if (SetField(ref _itemFilter, value ?? "")) OnPropertyChanged(nameof(Items)); }
    }

    public IEnumerable<ItemInfo> Items => string.IsNullOrWhiteSpace(_itemFilter)
        ? AllItems
        : AllItems.Where(i => i.Name.Contains(_itemFilter, StringComparison.OrdinalIgnoreCase)
                           || i.TypeName.Contains(_itemFilter, StringComparison.OrdinalIgnoreCase)
                           || i.Shops.Contains(_itemFilter, StringComparison.OrdinalIgnoreCase));
}

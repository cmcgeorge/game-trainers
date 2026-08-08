using FountainOfDreamsTrainer.Game;

namespace FountainOfDreamsTrainer.ViewModels;

/// <summary>
/// One editable skill row: a <see cref="SkillInfo"/> (id, name, min-IQ) plus the character's
/// level in that skill. The level is displayed read-only from the record (the skill encoding
/// is variable-length packed data that the trainer does not write directly).
/// </summary>
public sealed class SkillRowViewModel : ObservableObject
{
    private readonly Func<int> _get;

    public SkillInfo Info { get; }

    public SkillRowViewModel(SkillInfo info, Func<int> get)
    {
        Info = info;
        _get = get;
    }

    public int Id => Info.Id;
    public string Name => Info.Name;
    public string Description => Info.FullDescription;

    public int Level => _get();

    /// <summary>Re-reads the backing value (after a live refresh).</summary>
    public void Refresh() => OnPropertyChanged(nameof(Level));
}

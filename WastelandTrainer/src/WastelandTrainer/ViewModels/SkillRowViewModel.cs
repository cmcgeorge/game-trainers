using WastelandTrainer.Game;

namespace WastelandTrainer.ViewModels;

/// <summary>
/// One editable skill row: a <see cref="SkillInfo"/> (id, name, min-IQ) plus the character's level
/// in that skill. Setting the level reuses or appends the packed entry and writes the whole skill
/// block back to live memory.
/// </summary>
public sealed class SkillRowViewModel : ObservableObject
{
    private readonly Func<int> _get;
    private readonly Action<int> _set;

    public SkillInfo Info { get; }

    public SkillRowViewModel(SkillInfo info, Func<int> get, Action<int> set)
    {
        Info = info;
        _get = get;
        _set = set;
    }

    public int Id => Info.Id;
    public string Name => Info.Name;
    public string Description => Info.Description;

    public int Level
    {
        get => _get();
        set { _set(value); OnPropertyChanged(); }
    }

    /// <summary>Re-reads the backing value (after a live refresh or a "max" action).</summary>
    public void Refresh() => OnPropertyChanged(nameof(Level));
}

using ColonizationTrainer.Game;

namespace ColonizationTrainer.ViewModels;

/// <summary>
/// One Founding Father checkbox in the save editor: bound to a single bit of the selected nation's
/// acquired-Fathers bitfield. Toggling it grants/removes that Father in the decoded save (the dead
/// slot 18 is shown but not toggleable).
/// </summary>
public sealed class FoundingFatherRowViewModel : ObservableObject
{
    private readonly NationRecord _nation;
    private readonly FoundingFather _father;

    public FoundingFatherRowViewModel(NationRecord nation, FoundingFather father)
    {
        _nation = nation;
        _father = father;
    }

    public string Name => _father.Name;
    public string Category => _father.Category;
    public string Effect => _father.Effect;

    /// <summary>False for the game's dead slot 18, so its checkbox is disabled.</summary>
    public bool Grantable => _father.Category != "—";

    public bool Acquired
    {
        get => _nation.HasFather(_father.Bit);
        set
        {
            if (!Grantable) { OnPropertyChanged(); return; }
            _nation.SetFather(_father.Bit, value);
            OnPropertyChanged();
        }
    }

    /// <summary>Re-reads the checkbox from the record (after Grant All / Clear All).</summary>
    public void Refresh() => OnPropertyChanged(nameof(Acquired));
}

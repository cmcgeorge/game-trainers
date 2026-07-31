using AlternateRealityTrainer.Game;

namespace AlternateRealityTrainer.ViewModels;

/// <summary>
/// One editable attribute row. Reading and writing go straight through to the character record, so
/// a change is in the game as soon as the text box commits.
/// </summary>
public sealed class AttributeViewModel : ObservableObject
{
    private readonly CharacterRecord _record;
    private readonly Action<int> _afterWrite;

    public AttributeInfo Info { get; }

    /// <param name="afterWrite">Called with this attribute's index once a write has landed.</param>
    public AttributeViewModel(AttributeInfo info, CharacterRecord record, Action<int> afterWrite)
    {
        Info = info;
        _record = record ?? throw new ArgumentNullException(nameof(record));
        _afterWrite = afterWrite ?? throw new ArgumentNullException(nameof(afterWrite));
    }

    public string Name => Info.Name;
    public string Abbreviation => Info.Abbreviation;

    /// <summary>"Physical Speed" has no column on the game's status bar.</summary>
    public string Label => Info.Hidden ? $"{Info.Name} (hidden)" : $"{Info.Name} ({Info.Abbreviation})";

    public int Value
    {
        get => _record.GetAttribute(Info.Index);
        set
        {
            int clamped = Math.Clamp(value, 1, CharacterFormat.AttributeCeiling);
            bool moved = clamped != _record.GetAttribute(Info.Index);
            if (moved)
            {
                _record.SetAttribute(Info.Index, clamped);
                _afterWrite(Info.Index);
            }
            // Notify when the value moved, and also when the input had to be clamped, so a text box
            // handed an out-of-range number snaps back to what was actually written.
            if (moved || clamped != value) OnPropertyChanged();
        }
    }

    /// <summary>Sub-point progress toward the next whole point, shown for information only.</summary>
    public int Fraction => _record.GetAttributeFraction(Info.Index);

    /// <summary>Re-raises change notifications after a bulk action or a reload.</summary>
    public void Refresh()
    {
        OnPropertyChanged(nameof(Value));
        OnPropertyChanged(nameof(Fraction));
    }
}

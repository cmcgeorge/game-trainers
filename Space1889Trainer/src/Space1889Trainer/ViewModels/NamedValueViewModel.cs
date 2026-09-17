namespace Space1889Trainer.ViewModels;

/// <summary>An editable named number (an attribute or a skill row).</summary>
public sealed class NamedValueViewModel : ObservableObject
{
    private readonly Func<int, bool> _write;
    private readonly Action<string> _onFailure;
    private readonly int _min, _max;
    private bool _loading;
    private int _value;

    /// <param name="min">Smallest value the underlying field accepts (attributes 1, skills 0).</param>
    /// <param name="max">Largest value the underlying field accepts.</param>
    public NamedValueViewModel(int index, string name, string group, string hint, int min, int max,
                               Func<int, bool> write, Action<string> onFailure)
    {
        Index = index;
        Name = name;
        Group = group;
        Hint = hint;
        _min = min;
        _max = max;
        _write = write;
        _onFailure = onFailure;
    }

    public int Index { get; }

    public string Name { get; }

    /// <summary>The attribute a skill belongs to (empty for an attribute row).</summary>
    public string Group { get; }

    public string Hint { get; }

    public int Value
    {
        get => _value;
        set
        {
            // Clamp to exactly what the record will store, so the row never shows a value the block does not hold.
            int v = Math.Clamp(value, _min, _max);
            if (!SetField(ref _value, v) || _loading) return;
            if (!_write(v)) _onFailure(Name);
        }
    }

    /// <summary>Sets the value without writing it (a refresh from the game).</summary>
    public void Load(int value)
    {
        _loading = true;
        try { Value = value; }
        finally { _loading = false; }
    }
}

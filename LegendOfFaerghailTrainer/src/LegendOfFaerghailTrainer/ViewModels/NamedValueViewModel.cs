namespace LegendOfFaerghailTrainer.ViewModels;

/// <summary>
/// A labelled, editable integer backed by getter/setter delegates onto a character record — used
/// for the attribute and ability rows. The setter writes through to live memory.
/// </summary>
public sealed class NamedValueViewModel : ObservableObject
{
    private readonly Func<int> _get;
    private readonly Action<int> _set;

    public string Name { get; }
    public string Description { get; }

    public NamedValueViewModel(string name, Func<int> get, Action<int> set, string description = "")
    {
        Name = name;
        _get = get;
        _set = set;
        Description = description;
    }

    /// <summary>
    /// The value. Setting it to what it already holds is a no-op — the same guard the other write
    /// paths carry, because every one of these rows is a live write into the emulator's memory and
    /// WPF re-pushes bound values whenever a template is re-applied or a box merely loses focus.
    /// </summary>
    public int Value
    {
        get => _get();
        set
        {
            if (value == _get()) { OnPropertyChanged(); return; }
            _set(value);
            OnPropertyChanged();
        }
    }

    /// <summary>Re-reads the backing value (after a live refresh or a "max" action).</summary>
    public void Refresh() => OnPropertyChanged(nameof(Value));
}

/// <summary>A labelled boolean backed by getter/setter delegates — used for the language rows.</summary>
public sealed class NamedFlagViewModel : ObservableObject
{
    private readonly Func<bool> _get;
    private readonly Action<bool> _set;

    public string Name { get; }

    public NamedFlagViewModel(string name, Func<bool> get, Action<bool> set)
    {
        Name = name;
        _get = get;
        _set = set;
    }

    public bool Value
    {
        get => _get();
        set
        {
            if (value == _get()) { OnPropertyChanged(); return; }
            _set(value);
            OnPropertyChanged();
        }
    }

    public void Refresh() => OnPropertyChanged(nameof(Value));
}

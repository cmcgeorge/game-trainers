namespace WastelandTrainer.ViewModels;

/// <summary>
/// A labelled, editable integer backed by getter/setter delegates onto a character record — used
/// for the per-attribute rows. The setter writes through to live memory.
/// </summary>
public sealed class NamedValueViewModel : ObservableObject
{
    private readonly Func<int> _get;
    private readonly Action<int> _set;

    public string Name { get; }

    public NamedValueViewModel(string name, Func<int> get, Action<int> set)
    {
        Name = name;
        _get = get;
        _set = set;
    }

    public int Value
    {
        get => _get();
        set { _set(value); OnPropertyChanged(); }
    }

    /// <summary>Re-reads the backing value (after a live refresh or a "max" action).</summary>
    public void Refresh() => OnPropertyChanged(nameof(Value));
}

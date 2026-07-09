using PoolOfRadianceTrainer.Mvvm;

namespace PoolOfRadianceTrainer.ViewModels;

/// <summary>The contract a character editor needs from its owner to push edits to the game.</summary>
public interface ICharacterHost
{
    bool IsAttached { get; }
    /// <summary>Writes <paramref name="length"/> bytes of <paramref name="source"/> (from
    /// <paramref name="offset"/>) to the record's live address + offset. No-op when offline.</summary>
    bool WriteBytes(nuint recordAddress, byte[] source, int offset, int length);
}

/// <summary>One editable ability score (STR/INT/WIS/DEX/CON/CHA).</summary>
public sealed class StatViewModel : ObservableObject
{
    private readonly Func<int> _get;
    private readonly Action<int> _set;

    public string Label { get; }
    public string Short { get; }

    public StatViewModel(string label, string shortLabel, Func<int> get, Action<int> set)
    {
        Label = label; Short = shortLabel; _get = get; _set = set;
    }

    public int Value
    {
        get => _get();
        set { _set(Math.Clamp(value, 1, 25)); OnPropertyChanged(); }
    }

    public void Refresh() => OnPropertyChanged(nameof(Value));
}

/// <summary>One coin/treasure counter (copper … jewelry).</summary>
public sealed class CoinViewModel : ObservableObject
{
    private readonly Func<int> _get;
    private readonly Action<int> _set;

    public string Label { get; }

    public CoinViewModel(string label, Func<int> get, Action<int> set)
    {
        Label = label; _get = get; _set = set;
    }

    public int Value
    {
        get => _get();
        set { _set(Math.Clamp(value, 0, 0xFFFF)); OnPropertyChanged(); }
    }

    public void Refresh() => OnPropertyChanged(nameof(Value));
}

/// <summary>One per-class level byte.</summary>
public sealed class ClassLevelViewModel : ObservableObject
{
    private readonly Func<int> _get;
    private readonly Action<int> _set;

    public string Label { get; }

    public ClassLevelViewModel(string label, Func<int> get, Action<int> set)
    {
        Label = label; _get = get; _set = set;
    }

    public int Value
    {
        get => _get();
        set { _set(Math.Clamp(value, 0, 40)); OnPropertyChanged(); }
    }

    public void Refresh() => OnPropertyChanged(nameof(Value));
}

/// <summary>One byte in the raw-hex editor, annotated with its known field name.</summary>
public sealed class HexByteViewModel : ObservableObject
{
    private readonly Func<int, int> _get;
    private readonly Action<int, int> _set;

    public int Offset { get; }
    public string OffsetHex => $"0x{Offset:X2}";
    public string Label { get; }

    public HexByteViewModel(int offset, string label, Func<int, int> get, Action<int, int> set)
    {
        Offset = offset; Label = label; _get = get; _set = set;
    }

    /// <summary>Two-hex-digit string; parses hex on set.</summary>
    public string ValueHex
    {
        get => $"{_get(Offset):X2}";
        set
        {
            if (int.TryParse(value?.Trim(), System.Globalization.NumberStyles.HexNumber, null, out int v))
            {
                _set(Offset, Math.Clamp(v, 0, 255));
                OnPropertyChanged();
            }
        }
    }

    public string Ascii
    {
        get { int b = _get(Offset); return b >= 32 && b < 127 ? ((char)b).ToString() : "·"; }
    }

    public void Refresh() { OnPropertyChanged(nameof(ValueHex)); OnPropertyChanged(nameof(Ascii)); }
}

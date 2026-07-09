using MightAndMagic1Trainer.Mvvm;

namespace MightAndMagic1Trainer.ViewModels;

/// <summary>One editable byte in the raw hex view. Edits flow back into the owning
/// <see cref="CharacterViewModel"/> which persists them to memory.</summary>
public sealed class HexByteViewModel : ObservableObject
{
    private readonly CharacterViewModel _owner;

    public int Offset { get; }
    public string OffsetHex => $"0x{Offset:X2}";
    public string Label { get; }

    public HexByteViewModel(CharacterViewModel owner, int offset, string label)
    {
        _owner = owner;
        Offset = offset;
        Label = label;
    }

    public byte Value
    {
        get => _owner.Record.GetByte(Offset);
        set
        {
            if (_owner.Record.GetByte(Offset) == value) return;
            _owner.Record.SetByte(Offset, value);
            _owner.OnRawByteEdited(Offset);
            OnPropertyChanged();
            OnPropertyChanged(nameof(ValueHex));
        }
    }

    /// <summary>Two-way text binding for the byte as hex; an optional "0x" prefix is accepted.</summary>
    public string ValueHex
    {
        get => $"{Value:X2}";
        set
        {
            var s = (value ?? string.Empty).Trim();
            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) s = s[2..];
            if (byte.TryParse(s, System.Globalization.NumberStyles.HexNumber, null, out var b))
                Value = b;
            else
                OnPropertyChanged();   // revert display
        }
    }

    /// <summary>Called by the owner when the underlying byte changed elsewhere.</summary>
    public void Refresh()
    {
        OnPropertyChanged(nameof(Value));
        OnPropertyChanged(nameof(ValueHex));
    }
}

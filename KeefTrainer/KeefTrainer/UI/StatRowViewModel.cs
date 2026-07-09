using System.ComponentModel;
using System.Runtime.CompilerServices;
using KeefTrainer.Core;

namespace KeefTrainer.UI;

/// <summary>One editable/freezable stat row.</summary>
public sealed class StatRowViewModel : INotifyPropertyChanged
{
    private readonly TrainerEngine _engine;
    private int _value;
    private string _editText = "0";
    private bool _isFrozen;
    private bool _isEditing;

    public KeefField Field { get; }
    public string Name { get; }
    public string? Hint { get; }
    public int Max { get; }

    public StatRowViewModel(TrainerEngine engine, KeefField field, string name, string? hint = null)
    {
        _engine = engine;
        Field = field;
        Name = name;
        Hint = hint;
        Max = KeefMap.Info(field).Max;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged([CallerMemberName] string? p = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));

    /// <summary>Live value from the game; refreshed by the poll loop.</summary>
    public int Value
    {
        get => _value;
        private set
        {
            if (_value == value) return;
            _value = value;
            OnChanged();
            if (!IsEditing)
            {
                _editText = value.ToString();
                OnChanged(nameof(EditText));
            }
        }
    }

    /// <summary>Text in the editor box. Committed via CommitEdit().</summary>
    public string EditText
    {
        get => _editText;
        set { _editText = value; OnChanged(); }
    }

    /// <summary>True while the textbox has keyboard focus (suppresses live refresh).</summary>
    public bool IsEditing
    {
        get => _isEditing;
        set
        {
            if (_isEditing == value) return;   // avoid double-commit (Enter + focus loss)
            _isEditing = value;
            OnChanged();
            if (!value) CommitEdit();
        }
    }

    public bool IsFrozen
    {
        get => _isFrozen;
        set
        {
            if (_isFrozen == value) return;
            _isFrozen = value;
            OnChanged();
            _engine.SetFrozen(Field, value ? ParseEdit() ?? Value : null);
        }
    }

    public void UpdateFromSnapshot(GameSnapshot snapshot) => Value = snapshot[Field];

    public void CommitEdit()
    {
        int? parsed = ParseEdit();
        if (parsed is not int v)
        {
            EditText = Value.ToString();   // revert bad input
            return;
        }
        var info = KeefMap.Info(Field);
        int clamped = Math.Clamp(v, info.Min, info.Max);
        if (clamped != Value)
            _engine.WriteField(Field, clamped);
        if (IsFrozen)
            _engine.SetFrozen(Field, clamped);
        EditText = clamped.ToString();     // always reconcile the display with what took effect
    }

    /// <summary>Programmatic set (presets): writes now and re-targets a freeze.</summary>
    public void Apply(int v)
    {
        var info = KeefMap.Info(Field);
        v = Math.Clamp(v, info.Min, info.Max);
        _engine.WriteField(Field, v);
        if (IsFrozen) _engine.SetFrozen(Field, v);
        if (!IsEditing) EditText = v.ToString();
    }

    private int? ParseEdit() =>
        int.TryParse(EditText.Trim(), out int v) ? v : null;
}

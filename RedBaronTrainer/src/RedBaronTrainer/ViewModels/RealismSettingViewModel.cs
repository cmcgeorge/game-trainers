using RedBaronTrainer.Game;

namespace RedBaronTrainer.ViewModels;

/// <summary>One row of the realism panel: a tick box, or a three-way selector rendered as a combo.</summary>
public sealed class RealismSettingViewModel : ObservableObject
{
    private readonly Action _onChanged;
    private ushort _value;

    public RealismSettingViewModel(RealismSetting setting, ushort value, Action onChanged)
    {
        ArgumentNullException.ThrowIfNull(setting);
        ArgumentNullException.ThrowIfNull(onChanged);
        Setting = setting;
        _value = value;
        _onChanged = onChanged;
        Choices = setting.Kind switch
        {
            RealismKind.CombatLevel => RealismSettings.CombatLevelNames,
            RealismKind.FlightModel => RealismSettings.FlightModelNames,
            _ => Array.Empty<string>(),
        };
    }

    public RealismSetting Setting { get; }

    public string Name => Setting.Name;
    public string Description => Setting.Description;

    /// <summary>True for the settings a player usually wants switched off (ammunition, fuel, damage...).</summary>
    public bool OffIsEasier => Setting.OffIsEasier;

    public bool IsToggle => Setting.Kind == RealismKind.Toggle;
    public bool IsChoice => !IsToggle;

    public IReadOnlyList<string> Choices { get; }

    public ushort Value
    {
        get => _value;
        set
        {
            ushort clamped = Math.Min(value, (ushort)RealismSettings.MaximumValue(Setting.Kind));
            if (!SetField(ref _value, clamped)) return;
            OnPropertyChanged(nameof(IsOn));
            OnPropertyChanged(nameof(SelectedIndex));
            _onChanged();
        }
    }

    public bool IsOn
    {
        get => _value != 0;
        set => Value = value ? (ushort)1 : (ushort)0;
    }

    public int SelectedIndex
    {
        get => _value;
        set { if (value >= 0) Value = (ushort)value; }
    }

    /// <summary>Applies a value from a preset without raising the "user edited this" callback twice.</summary>
    public void SetQuietly(ushort value)
    {
        ushort clamped = Math.Min(value, (ushort)RealismSettings.MaximumValue(Setting.Kind));
        if (_value == clamped) return;
        _value = clamped;
        OnPropertyChanged(nameof(Value));
        OnPropertyChanged(nameof(IsOn));
        OnPropertyChanged(nameof(SelectedIndex));
    }
}

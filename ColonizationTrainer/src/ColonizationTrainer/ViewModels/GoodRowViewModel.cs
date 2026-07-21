using ColonizationTrainer.Game;

namespace ColonizationTrainer.ViewModels;

/// <summary>
/// One good's stockpile row inside a colony editor: the good's name and an editable quantity that
/// writes straight into the colony record (clamped to the signed-16-bit range the field holds).
/// </summary>
public sealed class GoodRowViewModel : ObservableObject
{
    private readonly ColonyRecord _colony;
    private readonly int _goodId;

    public string Name { get; }

    public GoodRowViewModel(ColonyRecord colony, int goodId)
    {
        _colony = colony;
        _goodId = goodId;
        Name = CargoBook.Names[goodId];
    }

    /// <summary>Quantity in the warehouse. Clamped to the field's signed-16-bit range on write.</summary>
    public int Quantity
    {
        get => _colony.GetStock(_goodId);
        set
        {
            short clamped = (short)Math.Clamp(value, 0, SaveFormat.GoodsMax);
            _colony.SetStock(_goodId, clamped);
            OnPropertyChanged();
        }
    }

    /// <summary>Re-reads the quantity from the record (after a bulk "fill" action).</summary>
    public void Refresh() => OnPropertyChanged(nameof(Quantity));
}

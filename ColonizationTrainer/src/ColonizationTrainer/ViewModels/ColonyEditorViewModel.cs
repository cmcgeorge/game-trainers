using System.Collections.ObjectModel;
using System.Windows.Input;
using ColonizationTrainer.Game;

namespace ColonizationTrainer.ViewModels;

/// <summary>
/// Editor for one colony: its name, population, and hammers (all editable and written into the save
/// buffer) plus a row per good in the 16-slot stockpile and a "fill warehouse" action.
/// </summary>
public sealed class ColonyEditorViewModel : ObservableObject
{
    private readonly ColonyRecord _colony;

    public ColonyEditorViewModel(ColonyRecord colony)
    {
        _colony = colony;
        Goods = new ObservableCollection<GoodRowViewModel>(
            Enumerable.Range(0, SaveFormat.GoodsCount).Select(g => new GoodRowViewModel(colony, g)));
        FillWarehouseCommand = new RelayCommand(_ => FillWarehouse());
    }

    /// <summary>Position + owner, for the colony list header.</summary>
    public string Location => $"({_colony.X}, {_colony.Y})  {NationBook.NameOf(_colony.Nation)}";

    public string Name
    {
        get => _colony.Name;
        set { _colony.Name = value ?? string.Empty; OnPropertyChanged(); OnPropertyChanged(nameof(Title)); }
    }

    public int Population
    {
        get => _colony.Population;
        set { _colony.Population = value; OnPropertyChanged(); OnPropertyChanged(nameof(Title)); }
    }

    public int Hammers
    {
        get => _colony.Hammers;
        set { _colony.Hammers = value; OnPropertyChanged(); }
    }

    /// <summary>Short label for the colony list.</summary>
    public string Title => $"{Name} — pop {Population}";

    public ObservableCollection<GoodRowViewModel> Goods { get; }

    public ICommand FillWarehouseCommand { get; }

    private void FillWarehouse()
    {
        _colony.FillAllStock(SaveFormat.GoodsFill);
        foreach (var g in Goods) g.Refresh();
    }
}

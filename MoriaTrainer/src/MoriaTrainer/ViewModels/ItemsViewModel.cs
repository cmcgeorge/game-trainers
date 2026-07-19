using System.Collections.ObjectModel;
using System.Windows.Input;
using MoriaTrainer.Game;

namespace MoriaTrainer.ViewModels;

/// <summary>
/// Backs the Items tab: a read-only reference of UMoria's item categories, ego weapons, crowns, and
/// wearable flags. Derived from <c>constant.h</c>'s TV_* constants and the manual/FAQ (Confirmed —
/// see <c>.docs/ReverseEngineering.md</c> §3.6). The live <c>inventory[34]</c> array can be read
/// once a COFF-base locator is built; until then this tab documents what each <c>tval</c> byte means
/// so the user can interpret the game's item descriptions.
/// </summary>
public sealed class ItemsViewModel : ObservableObject
{
    public ObservableCollection<ItemInfo> Items { get; } = new(ItemBook.Items);

    public IReadOnlyList<(string Code, string Name, string Effect)> EgoWeapons => ItemBook.EgoWeapons;
    public IReadOnlyList<(string Name, string Effect)> Crowns => ItemBook.Crowns;
    public IReadOnlyList<(string Code, string Effect)> WearableFlags => ItemBook.WearableFlags;

    private ItemInfo? _selected;
    public ItemInfo? Selected
    {
        get => _selected;
        set => SetField(ref _selected, value);
    }

    private string _filter = "";
    public string Filter
    {
        get => _filter;
        set
        {
            if (!SetField(ref _filter, value)) return;
            ApplyFilter();
        }
    }

    public ICommand ClearFilterCommand { get; }

    public ItemsViewModel()
    {
        ClearFilterCommand = new RelayCommand(_ => Filter = "");
        Selected = Items.FirstOrDefault();
    }

    private void ApplyFilter()
    {
        var selected = Selected;
        Items.Clear();
        var filtered = string.IsNullOrWhiteSpace(Filter)
            ? ItemBook.Items
            : ItemBook.Items.Where(i => i.Category.Contains(Filter, StringComparison.OrdinalIgnoreCase)
                                     || i.Examples.Contains(Filter, StringComparison.OrdinalIgnoreCase)
                                     || i.Notes.Contains(Filter, StringComparison.OrdinalIgnoreCase));
        foreach (var i in filtered) Items.Add(i);
        Selected = Items.FirstOrDefault(i => selected != null && i.Tval == selected.Tval)
                   ?? Items.FirstOrDefault();
    }
}

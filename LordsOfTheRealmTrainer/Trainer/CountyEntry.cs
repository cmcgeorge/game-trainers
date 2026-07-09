using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LordsTrainer;

/// <summary>
/// One county row in the county-resources grid: shows the live grain, cattle, sheep and wool
/// for a single province, can be ticked for a bulk "max selected counties" write, and can be
/// frozen so the game can't erode the goods back down. Memory access stays in
/// <see cref="MainWindow"/> (this is a passive view model, like <see cref="WatchEntry"/>); the
/// goods' addresses and cheat targets travel in <see cref="Goods"/>.
/// </summary>
public sealed class CountyEntry : INotifyPropertyChanged
{
    /// <summary>The county's slot index in the per-county record array.</summary>
    public int Index { get; init; }

    /// <summary>True when this is the county whose screen you currently have open in-game.</summary>
    public bool IsViewed { get; init; }

    /// <summary>
    /// The four goods to display and cheat, in the fixed order grain, cattle, sheep, wool
    /// (matching <see cref="LordsGame"/>'s good table). Each carries the guest address and the
    /// amount a "max" writes; the live amounts are surfaced through the named columns below.
    /// </summary>
    public IReadOnlyList<LordsGame.ProvinceGood> Goods { get; init; } = [];

    /// <summary>County-column label: the slot index, marked when it's the one you're viewing.</summary>
    public string Label => IsViewed ? $"#{Index} · viewing" : $"#{Index}";

    private long _grain, _cattle, _sheep, _wool;
    /// <summary>Live grain amount (goods slot 0).</summary>
    public long Grain { get => _grain; private set { if (_grain != value) { _grain = value; OnChanged(); } } }
    /// <summary>Live cattle amount (goods slot 1).</summary>
    public long Cattle { get => _cattle; private set { if (_cattle != value) { _cattle = value; OnChanged(); } } }
    /// <summary>Live sheep amount (goods slot 2).</summary>
    public long Sheep { get => _sheep; private set { if (_sheep != value) { _sheep = value; OnChanged(); } } }
    /// <summary>Live wool amount (goods slot 3).</summary>
    public long Wool { get => _wool; private set { if (_wool != value) { _wool = value; OnChanged(); } } }

    /// <summary>Updates the live amount for goods slot <paramref name="index"/> (0..3), keeping
    /// the named columns in step with <see cref="Goods"/>' fixed grain/cattle/sheep/wool order.</summary>
    public void SetLive(int index, long value)
    {
        switch (index)
        {
            case 0: Grain = value; break;
            case 1: Cattle = value; break;
            case 2: Sheep = value; break;
            case 3: Wool = value; break;
        }
    }

    private bool _selected;
    /// <summary>Ticked by the user to include this county in the next "max selected counties" write.</summary>
    public bool Selected { get => _selected; set { _selected = value; OnChanged(); } }

    private bool _freeze;
    /// <summary>When set, the live tick re-writes every good to its max so the game can't erode it.</summary>
    public bool Freeze { get => _freeze; set { _freeze = value; OnChanged(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

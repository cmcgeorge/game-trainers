using Civilization3ConquestsTrainer.Game;

namespace Civilization3ConquestsTrainer.ViewModels;

/// <summary>
/// One city.
///
/// Deliberately narrow. The C3X struct header's own <c>field_XX</c> anchors agree with arithmetic up
/// to <c>City_Body + 0x54</c> and then drift by 0x18, which means population, corruption, the per-turn
/// incomes, the build queue and the city name are all at offsets nobody has confirmed. Rather than
/// display plausible-looking numbers read from unconfirmed offsets, this row exposes only the fields
/// inside the anchored prefix — the ones the header brackets on both sides. See
/// <c>docs/ReverseEngineering.md</c> for what it would take to open the rest up.
/// </summary>
public sealed class CityRowViewModel : ObservableObject
{
    private readonly IGameHost _host;
    private readonly nuint _body;

    /// <summary>Slot in the city container; also the city's own id.</summary>
    public int Slot { get; }

    private string _owner = "";
    public string Owner { get => _owner; private set => SetField(ref _owner, value); }

    /// <summary>Whether this city belongs to the civ the human is playing.</summary>
    public bool IsMine { get; private set; }

    private int _x, _y;
    public int X { get => _x; private set => SetField(ref _x, value); }
    public int Y { get => _y; private set => SetField(ref _y, value); }
    public string Position => $"{_x}, {_y}";

    private int _food;
    /// <summary>Food banked toward the next citizen.</summary>
    public int StoredFood
    {
        get => _food;
        set
        {
            if (!Reject(value < 0, "Stored food cannot be negative — edit rejected.")) return;
            if (!SetField(ref _food, value)) return;
            _freezeFood = value;
            _host.WriteInt32(_body + (nuint)Civ3Layout.CityStoredFood, value);
        }
    }

    private int _shields;
    /// <summary>Shields banked toward whatever is being built.</summary>
    public int StoredProduction
    {
        get => _shields;
        set
        {
            if (!Reject(value < 0, "Stored shields cannot be negative — edit rejected.")) return;
            if (!SetField(ref _shields, value)) return;
            _freezeShields = value;
            _host.WriteInt32(_body + (nuint)Civ3Layout.CityStoredProduction, value);
        }
    }

    private int _culturalLevel;
    /// <summary>Cultural level — the border-expansion ladder, not the accumulated culture total.</summary>
    public int CulturalLevel
    {
        get => _culturalLevel;
        set
        {
            if (!Reject(value is < 0 or > 100, "Cultural level must be between 0 and 100 — edit rejected.")) return;
            if (!SetField(ref _culturalLevel, value)) return;
            _host.WriteInt32(_body + (nuint)Civ3Layout.CityCulturalLevel, value);
        }
    }

    private bool _freeze;
    private int _freezeFood, _freezeShields;

    /// <summary>
    /// Re-applies food and shields every tick, so the turn's consumption can't eat them.
    ///
    /// The pinned amounts are captured here rather than read back from <see cref="StoredFood"/> and
    /// <see cref="StoredProduction"/> at apply time — the poll loop refreshes those from the game
    /// immediately before calling <see cref="ApplyFreeze"/>, so writing them back would just re-write
    /// whatever the game had already decremented them to, and the freeze would do nothing at all.
    /// </summary>
    public bool Freeze
    {
        get => _freeze;
        set
        {
            if (!SetField(ref _freeze, value)) return;
            if (value) { _freezeFood = _food; _freezeShields = _shields; }
        }
    }

    /// <summary>Refuses an out-of-range edit, or any edit at all while writes are blocked.</summary>
    private bool Reject(bool outOfRange, string message, [System.Runtime.CompilerServices.CallerMemberName] string? name = null)
    {
        if (outOfRange) { _host.Report(message); OnPropertyChanged(name); return false; }
        if (!_host.WritesAllowed) { _host.Report("Writes are disabled — the edit was not applied."); OnPropertyChanged(name); return false; }
        return true;
    }

    public CityRowViewModel(IGameHost host, nuint body, int slot)
    {
        _host = host;
        _body = body;
        Slot = slot;
    }

    /// <summary>Re-reads this city. Returns false if the record no longer validates (it was captured or razed).</summary>
    public bool Refresh(GameTables tables, Civ3Location loc)
    {
        byte[] b = _host.Read(_body, Civ3Layout.CityTrustedPrefixEnd);
        if (!Civ3Layout.ValidateCity(b, Slot, loc.MapWidth, loc.MapHeight)) return false;

        int civ = b[Civ3Layout.CityCivId];
        IsMine = civ == loc.HumanCivId;
        Owner = tables.RaceName(civ < 0 ? -1 : LeaderRaceOf(civ, loc));

        int x = BitConverter.ToInt16(b, Civ3Layout.CityX);
        int y = BitConverter.ToInt16(b, Civ3Layout.CityY);
        if (x != _x || y != _y) { X = x; Y = y; OnPropertyChanged(nameof(Position)); }

        int food = BitConverter.ToInt32(b, Civ3Layout.CityStoredFood);
        if (food != _food) { _food = food; OnPropertyChanged(nameof(StoredFood)); }

        int shields = BitConverter.ToInt32(b, Civ3Layout.CityStoredProduction);
        if (shields != _shields) { _shields = shields; OnPropertyChanged(nameof(StoredProduction)); }

        int level = BitConverter.ToInt32(b, Civ3Layout.CityCulturalLevel);
        if (level != _culturalLevel) { _culturalLevel = level; OnPropertyChanged(nameof(CulturalLevel)); }

        return true;
    }

    /// <summary>Re-applies the frozen food and shield stores. Called from the poll loop.</summary>
    public void ApplyFreeze()
    {
        if (!_freeze) return;
        _host.WriteInt32(_body + (nuint)Civ3Layout.CityStoredFood, _freezeFood);
        _host.WriteInt32(_body + (nuint)Civ3Layout.CityStoredProduction, _freezeShields);
    }

    /// <summary>A city stores a civ id; the label wants that civ's race, which lives on its leader.</summary>
    private int LeaderRaceOf(int civId, Civ3Location loc)
    {
        if (!Civ3Layout.IsValidCivId(civId)) return -1;
        return _host.ReadInt32(loc.LeaderField(civId, Civ3Layout.LeaderRaceId), out int race) ? race : -1;
    }
}

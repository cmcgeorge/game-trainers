using Civilization3ConquestsTrainer.Game;

namespace Civilization3ConquestsTrainer.ViewModels;

/// <summary>
/// One civilization in the game: its treasury, its three rate sliders, culture and research state.
///
/// The treasury is the interesting one. Civ3 stores it as two fields whose sum is the real number,
/// so this row decodes on every refresh and re-encodes on every write — and it deliberately writes
/// only the encoded half, leaving the game's per-civ key (<c>Gold_Decrement</c>) untouched. Freezing
/// re-encodes each tick rather than replaying a cached byte pattern, so the freeze stays correct even
/// if the game re-seeds the key.
/// </summary>
public sealed class PlayerRowViewModel : ObservableObject
{
    private readonly IGameHost _host;
    private readonly nuint _record;

    /// <summary>Slot index in the <c>leaders</c> array; also the civ id used by units and cities.</summary>
    public int CivId { get; }

    /// <summary>Whether this is the civ the human is playing.</summary>
    public bool IsHuman { get; }

    /// <summary>Whether this is the barbarian pseudo-player in slot 0.</summary>
    public bool IsBarbarian => CivId == GameFacts.BarbarianCivId;

    private string _civName = "";
    public string CivName { get => _civName; private set => SetField(ref _civName, value); }

    public string RowLabel => IsHuman ? $"▶ {CivName}" : CivName;

    private long _treasury;
    /// <summary>Decoded treasury. Setting it re-encodes and pokes the encoded half.</summary>
    public long Treasury
    {
        get => _treasury;
        set
        {
            if (!Reject(!Civ3Layout.IsPlausibleTreasury(value),
                    $"{value:N0} is outside the range Civ3 can hold — edit rejected.")) return;
            if (!SetField(ref _treasury, value)) return;
            _freezeTarget = value;
            PokeTreasury(value);
        }
    }

    /// <summary>
    /// Gate for every editable property: refuses the edit — reverting the bound cell — when the value
    /// is out of range or when writes are blocked entirely, so the grid never shows a number the game
    /// did not actually receive.
    /// </summary>
    private bool Reject(bool outOfRange, string message, [System.Runtime.CompilerServices.CallerMemberName] string? name = null)
    {
        if (outOfRange)
        {
            _host.Report(message);
            OnPropertyChanged(name);
            return false;
        }
        if (!_host.WritesAllowed)
        {
            _host.Report("Writes are disabled — the edit was not applied.");
            OnPropertyChanged(name);
            return false;
        }
        return true;
    }

    private int _luxury, _science, _tax;

    /// <summary>Luxury rate in tens of percent (0–10). Setting it rebalances the other two.</summary>
    public int LuxuryRate { get => _luxury; set => SetSliders(value, _science, _tax, changed: 0); }

    /// <summary>Science rate in tens of percent (0–10). Setting it rebalances the other two.</summary>
    public int ScienceRate { get => _science; set => SetSliders(_luxury, value, _tax, changed: 1); }

    /// <summary>Tax rate in tens of percent (0–10). Setting it rebalances the other two.</summary>
    public int TaxRate { get => _tax; set => SetSliders(_luxury, _science, value, changed: 2); }

    /// <summary>The three sliders as percentages, for display.</summary>
    public string RatesLabel => $"{_tax * 10}% / {_science * 10}% / {_luxury * 10}%";

    private int _cultureLevel;
    public int CultureLevel { get => _cultureLevel; private set => SetField(ref _cultureLevel, value); }

    private int _cultureTotal;
    public int CultureTotal
    {
        get => _cultureTotal;
        set
        {
            if (!Reject(value < 0, "Culture cannot be negative — edit rejected.")) return;
            if (!SetField(ref _cultureTotal, value)) return;
            _host.WriteInt32(_record + (nuint)(Civ3Layout.LeaderCulture + Civ3Layout.CultureTotalAccumulated), value);
        }
    }

    private int _era;
    public int Era
    {
        get => _era;
        set
        {
            if (!Reject(value is < 0 or > GameFacts.MaxEraIndex,
                    $"Era must be between 0 and {GameFacts.MaxEraIndex} — edit rejected.")) return;
            if (!SetField(ref _era, value)) return;
            _host.WriteInt32(_record + (nuint)Civ3Layout.LeaderEra, value);
        }
    }

    private int _bulbs;
    /// <summary>Research points banked toward the current advance.</summary>
    public int ResearchBulbs
    {
        get => _bulbs;
        set
        {
            if (!Reject(value < 0, "Research points cannot be negative — edit rejected.")) return;
            if (!SetField(ref _bulbs, value)) return;
            _host.WriteInt32(_record + (nuint)Civ3Layout.LeaderResearchBulbs, value);
        }
    }

    private int _cityCount;
    public int CityCount { get => _cityCount; private set => SetField(ref _cityCount, value); }

    private int _unitCount;
    public int UnitCount { get => _unitCount; private set => SetField(ref _unitCount, value); }

    private long _freezeTarget;
    private bool _freezeTreasury;

    /// <summary>Holds the treasury against the turn tick, which otherwise adds or subtracts income.</summary>
    public bool FreezeTreasury
    {
        get => _freezeTreasury;
        set
        {
            if (!SetField(ref _freezeTreasury, value)) return;
            if (value) _freezeTarget = _treasury;
        }
    }

    public PlayerRowViewModel(IGameHost host, nuint record, int civId, bool isHuman)
    {
        _host = host;
        _record = record;
        CivId = civId;
        IsHuman = isHuman;
    }

    /// <summary>Re-reads the fields this row shows. Called from the poll loop.</summary>
    public void Refresh(GameTables tables)
    {
        byte[] head = _host.Read(_record, Civ3Layout.LeaderGoldSlider + 4);
        if (head.Length < Civ3Layout.LeaderGoldSlider + 4) return;

        int raceId = BitConverter.ToInt32(head, Civ3Layout.LeaderRaceId);
        CivName = raceId >= 0 ? tables.RaceName(raceId) : $"(empty slot {CivId})";

        long gold = Civ3Layout.DecodeGold(
            BitConverter.ToInt32(head, Civ3Layout.LeaderGoldDecrement),
            BitConverter.ToInt32(head, Civ3Layout.LeaderGoldEncoded));
        if (gold != _treasury) { _treasury = gold; OnPropertyChanged(nameof(Treasury)); }

        int lux = BitConverter.ToInt32(head, Civ3Layout.LeaderLuxurySlider);
        int sci = BitConverter.ToInt32(head, Civ3Layout.LeaderScienceSlider);
        int tax = BitConverter.ToInt32(head, Civ3Layout.LeaderGoldSlider);
        if (lux != _luxury || sci != _science || tax != _tax)
        {
            _luxury = lux; _science = sci; _tax = tax;
            OnPropertyChanged(nameof(LuxuryRate));
            OnPropertyChanged(nameof(ScienceRate));
            OnPropertyChanged(nameof(TaxRate));
            OnPropertyChanged(nameof(RatesLabel));
        }

        int era = BitConverter.ToInt32(head, Civ3Layout.LeaderEra);
        if (era != _era) { _era = era; OnPropertyChanged(nameof(Era)); }

        int bulbs = BitConverter.ToInt32(head, Civ3Layout.LeaderResearchBulbs);
        if (bulbs != _bulbs) { _bulbs = bulbs; OnPropertyChanged(nameof(ResearchBulbs)); }

        CityCount = BitConverter.ToInt32(head, Civ3Layout.LeaderCitiesCount);
        UnitCount = BitConverter.ToInt32(head, Civ3Layout.LeaderUnitCount);

        byte[] culture = _host.Read(_record + (nuint)Civ3Layout.LeaderCulture, Civ3Layout.CultureCivId + 4);
        if (culture.Length >= Civ3Layout.CultureCivId + 4)
        {
            CultureLevel = BitConverter.ToInt32(culture, Civ3Layout.CultureLevel);
            int total = BitConverter.ToInt32(culture, Civ3Layout.CultureTotalAccumulated);
            if (total != _cultureTotal) { _cultureTotal = total; OnPropertyChanged(nameof(CultureTotal)); }
        }
    }

    /// <summary>Re-applies the frozen treasury. Called from the poll loop.</summary>
    public void ApplyFreeze()
    {
        if (_freezeTreasury) PokeTreasury(_freezeTarget);
    }

    /// <summary>
    /// Sets the treasury to <paramref name="amount"/> — the toolbar's amount box rather than a fixed
    /// preset, so a player who wants a plausible-looking 5,000 does not have to undo a hundred million.
    /// The <see cref="Treasury"/> setter still range-checks it, so an absurd amount writes nothing.
    /// </summary>
    public void MaxTreasury(long amount) => Treasury = amount;

    /// <summary>
    /// Banks enough research points to complete the current advance. Civ3 compares the accumulated
    /// points against the advance's cost at the turn boundary, so the tech arrives when you end the
    /// turn rather than immediately.
    /// </summary>
    public void FinishResearch() => ResearchBulbs = GameFacts.FinishResearchBulbs;

    // --- writes --------------------------------------------------------------------------------

    /// <summary>
    /// Writes the encoded half so that <c>Gold_Decrement + Gold_Encoded</c> equals
    /// <paramref name="desired"/>. The decrement is re-read immediately before encoding rather than
    /// cached, so a freeze stays correct even if the game changes its key.
    /// </summary>
    private void PokeTreasury(long desired)
    {
        if (!_host.WritesAllowed) return;
        if (!_host.ReadInt32(_record + (nuint)Civ3Layout.LeaderGoldDecrement, out int decrement)) return;
        if (!Civ3Layout.TryEncodeGold(desired, decrement, out int encoded))
        {
            _host.Report($"{desired:N0} cannot be encoded against this civ's key — edit rejected.");
            return;
        }
        if (!_host.WriteInt32(_record + (nuint)Civ3Layout.LeaderGoldEncoded, encoded))
            _host.Report($"Treasury write failed for {CivName}.");
    }

    /// <summary>
    /// Applies a slider edit, rebalancing the other two so the three still total 10. Civ3 rejects any
    /// other combination outright, so normalising here is what makes single-slider editing possible at
    /// all. Note the government's own rate cap still applies — the game may clamp further.
    /// </summary>
    private void SetSliders(int luxury, int science, int tax, int changed)
    {
        int target = Math.Clamp(changed switch { 0 => luxury, 1 => science, _ => tax }, 0, GameFacts.SliderTotal);
        int remainder = GameFacts.SliderTotal - target;

        // The two sliders that were not edited, in a stable order.
        (int a, int b) = changed switch
        {
            0 => (_science, _tax),
            1 => (_luxury, _tax),
            _ => (_luxury, _science),
        };

        int sum = a + b;
        int newA, newB;
        if (sum == remainder) { newA = a; newB = b; }
        else if (sum == 0) { newA = 0; newB = remainder; }        // give it all to the second slider
        else
        {
            newA = (int)Math.Round((double)a * remainder / sum, MidpointRounding.AwayFromZero);
            newA = Math.Clamp(newA, 0, remainder);
            newB = remainder - newA;
        }

        (int lux, int sci, int gold) = changed switch
        {
            0 => (target, newA, newB),
            1 => (newA, target, newB),
            _ => (newA, newB, target),
        };

        // Refuse rather than commit locally when the rebalance is impossible or writes are blocked —
        // otherwise the three columns would show a split the game never received.
        if (!Civ3Layout.IsPlausibleSliderSet(lux, sci, gold) || !_host.WritesAllowed)
        {
            if (!_host.WritesAllowed) _host.Report("Writes are disabled — the rate change was not applied.");
            OnPropertyChanged(nameof(LuxuryRate));
            OnPropertyChanged(nameof(ScienceRate));
            OnPropertyChanged(nameof(TaxRate));
            return;
        }

        // Attempt all three writes before committing the cache. A mid-sequence failure leaves the
        // game partially rebalanced, and the UI must not show a split the game never received — so
        // any writes that landed are rolled back to the old values and the cache stays as it was.
        int oldLux = _luxury, oldSci = _science, oldGold = _tax;
        bool w1 = _host.WriteInt32(_record + (nuint)Civ3Layout.LeaderLuxurySlider, lux);
        bool w2 = _host.WriteInt32(_record + (nuint)Civ3Layout.LeaderScienceSlider, sci);
        bool w3 = _host.WriteInt32(_record + (nuint)Civ3Layout.LeaderGoldSlider, gold);

        if (w1 && w2 && w3)
        {
            _luxury = lux; _science = sci; _tax = gold;
        }
        else
        {
            if (w1) _host.WriteInt32(_record + (nuint)Civ3Layout.LeaderLuxurySlider, oldLux);
            if (w2) _host.WriteInt32(_record + (nuint)Civ3Layout.LeaderScienceSlider, oldSci);
            if (w3) _host.WriteInt32(_record + (nuint)Civ3Layout.LeaderGoldSlider, oldGold);
            _host.Report("One of the three rate writes failed — the change was rolled back.");
        }

        OnPropertyChanged(nameof(LuxuryRate));
        OnPropertyChanged(nameof(ScienceRate));
        OnPropertyChanged(nameof(TaxRate));
        OnPropertyChanged(nameof(RatesLabel));
    }
}

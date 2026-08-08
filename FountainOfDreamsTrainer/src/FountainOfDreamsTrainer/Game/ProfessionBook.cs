namespace FountainOfDreamsTrainer.Game;

/// <summary>
/// One Fountain of Dreams profession: its index, name, CON range, and starting attribute
/// template. The starting attributes and CON ranges were decoded from the ARCHTYPE file
/// (128-byte records at file offset 0x08, seven professions).
/// </summary>
public sealed record ProfessionInfo(
    int Index, string Name, int ConMin, int ConMax,
    int St, int Iq, int Dx, int Wp, int Ap, int Ch, int Lk, string Description)
{
    public int[] StartingAttributes => new[] { St, Iq, Dx, Wp, Ap, Ch, Lk };
}

/// <summary>
/// The five playable professions plus two NPC types. CON ranges and starting attributes
/// come from ARCHTYPE: CON min/max at +0x70 (two uint16 LE), attributes at +0x10 (seven bytes).
/// Survivalist and Vigilante share CON 20-25; Medic, Hood, and Mechanic share CON 15-25.
/// Yuppie and Clown are NPC-only and cannot be selected at character creation.
/// </summary>
public static class ProfessionBook
{
    public static readonly IReadOnlyList<ProfessionInfo> Professions = new ProfessionInfo[]
    {
        new(0, "Survivalist", 20, 25,
            11, 16, 11, 11, 16, 11, 11,
            "Wilderness scout; high Appeal and IQ, the best CON ceiling. Good with rifles and outdoor skills."),
        new(1, "Vigilante", 20, 25,
            11, 16, 11, 11, 11, 11, 11,
            "Self-appointed lawman; balanced stats, high CON. Combat-oriented with a focus on firearms."),
        new(2, "Medic", 15, 20,
            11, 16, 11, 11, 17, 11, 11,
            "Field surgeon; highest Appeal of the playable set. Essential for healing the party."),
        new(3, "Hood", 15, 25,
            11, 16, 11, 11, 17, 11, 11,
            "Street-smart thief; high Appeal. Good at stealth, lockpicking, and getting deals."),
        new(4, "Mechanic", 15, 25,
            11, 16, 11, 11, 16, 11, 11,
            "Tinkerer and repair specialist; high Appeal. Handles technical challenges and equipment upkeep."),
        new(5, "Yuppie", 10, 20,
            0, 0, 0, 0, 0, 0, 0,
            "NPC-only profession; not selectable at character creation."),
        new(6, "Clown", 1, 20,
            0, 0, 0, 0, 0, 0, 0,
            "NPC-only profession; not selectable at character creation."),
    };

    /// <summary>Playable professions (indices 0..4).</summary>
    public static IReadOnlyList<ProfessionInfo> Playable =>
        Professions.Take(5).ToList();

    public static ProfessionInfo? Find(int index) =>
        index >= 0 && index < Professions.Count ? Professions[index] : null;

    public static string Name(int index) => Find(index)?.Name ?? $"?({index})";
}

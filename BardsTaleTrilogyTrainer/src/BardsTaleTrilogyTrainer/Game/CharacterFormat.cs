namespace BardsTaleTrilogyTrainer.Game;

/// <summary>
/// Layout constants for the IL2CPP character object in The Bard's Tale Trilogy
/// remaster. The remaster is a Unity IL2CPP (64-bit) game, so every managed
/// object starts with a 16-byte header (<c>Il2CppClass*</c> + monitor), and the
/// first user field is at +0x10.
///
/// Offsets marked [Confirmed] were verified by the Cheat Engine community
/// (FearlessRevolution, game version 4.28). Offsets marked [Inferred] were
/// estimated from the original DOS character format and IL2CPP layout
/// conventions but have not been verified against a live game session.
/// </summary>
public static class CharacterFormat
{
    // --- IL2CPP object header ---------------------------------------------------
    public const int ObjectHeaderSize = 0x10;       // Il2CppClass* + monitor
    public const int FirstField = 0x10;

    /// <summary>IL2CPP array header size on x64: klass (8) + monitor (8) + bounds (8) + length (4, padded to 8) = 0x20.</summary>
    public const int ArrayHeaderSize = 0x20;

    // --- Confirmed offsets (CE AOB scripts, v4.28) -------------------------------
    /// <summary>[Confirmed] Experience points. CE pointer-scan: "offset ends at +50".</summary>
    public const int OffExperience = 0x50;           // int32

    /// <summary>[Confirmed] Current hit points. CE script: <c>cmp [rbp+00000084],r13d</c>.</summary>
    public const int OffHpCur = 0x84;                // int32

    /// <summary>[Confirmed] Current spell points (mana). CE script: <c>mov edi,[rbx+0000008C]</c>.</summary>
    public const int OffSpCur = 0x8C;                // int32

    // --- Inferred offsets (from original format + IL2CPP conventions) ------------
    /// <summary>[Inferred] Pointer to IL2CPP String for the character name.</summary>
    public const int OffName = 0x10;                 // ptr (8 bytes on x64)

    /// <summary>[Inferred] Race enum (0=Human … 6=Gnome).</summary>
    public const int OffRace = 0x18;                 // int32

    /// <summary>[Inferred] Class enum (0=Warrior … 9=Wizard).</summary>
    public const int OffClass = 0x1C;                // int32

    /// <summary>[Inferred] Status bitfield (Alive=0, Dead=2, Old=4, Poisoned=8, Stoned=16, Paralyzed=32, Possessed=64, Nuts=128).</summary>
    public const int OffStatus = 0x20;               // int32

    /// <summary>[Inferred] Current Strength.</summary>
    public const int OffStrCur = 0x28;               // int32

    /// <summary>[Inferred] Current IQ.</summary>
    public const int OffIqCur = 0x2C;                // int32

    /// <summary>[Inferred] Current Dexterity.</summary>
    public const int OffDxCur = 0x30;                // int32

    /// <summary>[Inferred] Current Constitution.</summary>
    public const int OffCnCur = 0x34;                // int32

    /// <summary>[Inferred] Current Luck.</summary>
    public const int OffLkCur = 0x38;                // int32

    /// <summary>[Inferred] Maximum Strength.</summary>
    public const int OffStrMax = 0x3C;               // int32

    /// <summary>[Inferred] Maximum IQ.</summary>
    public const int OffIqMax = 0x40;                // int32

    /// <summary>[Inferred] Maximum Dexterity.</summary>
    public const int OffDxMax = 0x44;                // int32

    /// <summary>[Inferred] Maximum Constitution.</summary>
    public const int OffCnMax = 0x48;                // int32

    /// <summary>[Inferred] Maximum Luck.</summary>
    public const int OffLkMax = 0x4C;                // int32

    /// <summary>[Inferred] Character level.</summary>
    public const int OffLevel = 0x54;                // int32

    /// <summary>[Inferred] Maximum hit points (immediately before current HP at +0x84).</summary>
    public const int OffHpMax = 0x80;                // int32

    /// <summary>[Inferred] Maximum spell points (immediately before current SP at +0x8C).</summary>
    public const int OffSpMax = 0x88;                // int32

    /// <summary>[Inferred] Base armor class (lower is better).</summary>
    public const int OffArmorClass = 0x90;           // int32

    /// <summary>[Inferred] Conjurer spell-class level (0–7).</summary>
    public const int OffConjurerLevel = 0x94;        // byte

    /// <summary>[Inferred] Magician spell-class level (0–7).</summary>
    public const int OffMagicianLevel = 0x95;        // byte

    /// <summary>[Inferred] Sorcerer spell-class level (0–7).</summary>
    public const int OffSorcererLevel = 0x96;        // byte

    /// <summary>[Inferred] Wizard spell-class level (0–7).</summary>
    public const int OffWizardLevel = 0x97;          // byte

    /// <summary>[Inferred] Pointer to IL2CPP array of inventory Item objects.</summary>
    public const int OffInventory = 0xA0;            // ptr

    /// <summary>[Inferred] Pointer to spell-knowledge bitfield or array.</summary>
    public const int OffSpellKnowledge = 0xB0;       // ptr or byte[]

    /// <summary>[Inferred] Offset of item charges within an Item object.</summary>
    public const int ItemChargesOffset = 0x18;       // int32 (after IL2CPP header + type ptr)

    /// <summary>[Inferred] Offset of item type ID within an Item object.</summary>
    public const int ItemTypeIdOffset = 0x10;        // int32

    /// <summary>[Inferred] Offset of equipped flag within an Item object.</summary>
    public const int ItemEquippedOffset = 0x1C;      // int32 (bool)

    /// <summary>Number of inventory slots (matching the original game).</summary>
    public const int InventorySlots = 8;

    // --- Enums ------------------------------------------------------------------
    public static readonly string[] Stats = { "Strength", "IQ", "Dexterity", "Constitution", "Luck" };

    public static readonly string[] Classes =
    {
        "Warrior", "Paladin", "Rogue", "Bard", "Hunter", "Monk",
        "Conjurer", "Magician", "Sorcerer", "Wizard"
    };

    public static readonly string[] Races =
    {
        "Human", "Elf", "Dwarf", "Hobbit", "Half-Elf", "Half-Orc", "Gnome"
    };

    public static readonly string[] SpellClasses = { "Conjurer", "Magician", "Sorcerer", "Wizard" };

    public static string ClassName(int c) => c >= 0 && c < Classes.Length ? Classes[c] : $"?({c})";
    public static string RaceName(int r) => r >= 0 && r < Races.Length ? Races[r] : $"?({r})";

    /// <summary>
    /// Plausibility check on a candidate character object. Reads the confirmed
    /// fields (XP, HP, SP) and the inferred fields (race, class, level, stats)
    /// and rejects anything outside mortal bounds. Used by the locator as a
    /// scan-time sanity gate.
    /// </summary>
    public static bool LooksLikeCharacter(ReadOnlySpan<byte> buf)
    {
        if (buf.Length < OffSpCur + 4) return false;

        int xp = ReadI32(buf, OffExperience);
        if (xp < 0 || xp > 100_000_000) return false;

        int hp = ReadI32(buf, OffHpCur);
        if (hp < 0 || hp > 9999) return false;

        int sp = ReadI32(buf, OffSpCur);
        if (sp < 0 || sp > 9999) return false;

        int race = ReadI32(buf, OffRace);
        if (race < 0 || race > 6) return false;

        int cls = ReadI32(buf, OffClass);
        if (cls < 0 || cls > 9) return false;

        int level = ReadI32(buf, OffLevel);
        if (level < 0 || level > GameFacts.MaxLevel) return false;

        int hpMax = ReadI32(buf, OffHpMax);
        if (hpMax <= 0 || hpMax > 9999) return false;

        for (int i = 0; i < 5; i++)
        {
            int cur = ReadI32(buf, OffStrCur + i * 4);
            if (cur < 0 || cur > GameFacts.MaxAttribute) return false;
        }

        // Cross-check: current should not exceed max for HP and SP
        if (hp > hpMax) return false;

        int spMax = ReadI32(buf, OffSpMax);
        if (spMax < 0 || spMax > 9999) return false;
        if (sp > spMax) return false;

        // Validate Max attributes are in range
        for (int i = 0; i < 5; i++)
        {
            int max = ReadI32(buf, OffStrMax + i * 4);
            if (max < 0 || max > GameFacts.MaxAttribute) return false;
        }

        return true;
    }

    public static int ReadI32(ReadOnlySpan<byte> buf, int off) =>
        buf[off] | (buf[off + 1] << 8) | (buf[off + 2] << 16) | (buf[off + 3] << 24);
}

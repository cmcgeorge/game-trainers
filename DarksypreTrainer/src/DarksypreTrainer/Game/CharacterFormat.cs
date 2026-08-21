namespace DarksypreTrainer.Game;

/// <summary>
/// Layout of the three structures DarkSpyre keeps live character state in. The game does not
/// hold one character record — it spreads the state across:
///
/// <list type="number">
/// <item><b>Status block</b> — six <see cref="ushort"/>s (current HP, SP, ENC then maximum
/// HP, SP, ENC). This is what the on-screen bars are drawn from. The game refreshes it from
/// the other two structures every frame, so writes here are overwritten on the next tick.</item>
/// <item><b>Character record</b> — the six attribute bytes followed by maximum HP, SP and ENC.
/// Writing the maxima here is what actually raises them.</item>
/// <item><b>Player actor</b> — entry 0 of the per-level creature table loaded from
/// <c>CR.DAT</c>, whose name field holds the literal string <c>player</c>. Current HP and SP
/// live here and this is the copy the game plays out of.</item>
/// </list>
///
/// All offsets were confirmed against a live DOSBox session; see
/// <c>docs/ReverseEngineering.md</c> for the method and the evidence.
/// </summary>
internal static class CharacterFormat
{
    // ---- status block -------------------------------------------------------
    /// <summary>Bytes in the status block (six little-endian 16-bit values).</summary>
    public const int StatusSize = 12;

    public const int StatusCurrentHp = 0;
    public const int StatusCurrentSp = 2;
    public const int StatusCurrentEnc = 4;
    public const int StatusMaxHp = 6;
    public const int StatusMaxSp = 8;
    public const int StatusMaxEnc = 10;

    // ---- character record ---------------------------------------------------
    /// <summary>Bytes of the character record the locator validates and writes.</summary>
    public const int RecordSize = 12;

    /// <summary>Attribute bytes, in the order the character screen lists them.</summary>
    public const int RecordAttributes = 0;

    public const int RecordMaxHp = 6;
    public const int RecordMaxSp = 8;
    public const int RecordMaxEnc = 10;

    /// <summary>Number of attribute bytes at <see cref="RecordAttributes"/>.</summary>
    public const int AttributeCount = 6;

    /// <summary>Attribute names, in record order.</summary>
    public static readonly string[] AttributeNames =
        { "Strength", "Agility", "Endurance", "Accuracy", "Talent", "Power" };

    // ---- player actor -------------------------------------------------------
    /// <summary>Bytes per entry in the creature table loaded from <c>CR.DAT</c>.</summary>
    public const int ActorSize = 0x56;

    /// <summary>Current hit points, little-endian 16-bit.</summary>
    public const int ActorCurrentHp = 0x10;

    /// <summary>Current spell points, little-endian 16-bit.</summary>
    public const int ActorCurrentSp = 0x12;

    /// <summary>ASCIIZ creature name; the player's entry always reads <c>player</c>.</summary>
    public const int ActorName = 0x1D;

    /// <summary>The name the player's creature-table entry carries — the locator's anchor.</summary>
    public const string PlayerActorName = "player";

    // ---- validation ---------------------------------------------------------
    /// <summary>
    /// Widest attribute value the locator will still accept. The game's own cap is
    /// <see cref="GameFacts.MaxAttribute"/> (20); the slack keeps a character edited by this
    /// trainer — or a future version with a higher cap — locatable.
    /// </summary>
    public const int LocatorMaxAttribute = 40;

    /// <summary>Upper bound the locator accepts for HP, SP and encumbrance.</summary>
    public const int LocatorMaxVital = 9999;

    /// <summary>Reads a little-endian 16-bit value.</summary>
    public static int ReadU16(byte[] buffer, int offset) =>
        buffer[offset] | (buffer[offset + 1] << 8);

    /// <summary>Writes a little-endian 16-bit value.</summary>
    public static void WriteU16(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)(value & 0xFF);
        buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
    }

    /// <summary>
    /// Whether the 12 bytes at <paramref name="offset"/> could be the status block for a
    /// character whose current HP/SP are <paramref name="hp"/>/<paramref name="sp"/>: the
    /// current values must match the actor's, and each current value must sit inside its
    /// maximum.
    /// </summary>
    public static bool IsStatusBlock(byte[] buffer, int offset, int hp, int sp)
    {
        if (offset < 0 || offset + StatusSize > buffer.Length) return false;
        if (ReadU16(buffer, offset + StatusCurrentHp) != hp) return false;
        if (ReadU16(buffer, offset + StatusCurrentSp) != sp) return false;

        int enc = ReadU16(buffer, offset + StatusCurrentEnc);
        int maxHp = ReadU16(buffer, offset + StatusMaxHp);
        int maxSp = ReadU16(buffer, offset + StatusMaxSp);
        int maxEnc = ReadU16(buffer, offset + StatusMaxEnc);

        if (maxHp < 1 || maxHp > LocatorMaxVital || hp > maxHp) return false;
        if (maxSp > LocatorMaxVital || sp > maxSp) return false;
        if (maxEnc < 1 || maxEnc > LocatorMaxVital || enc > maxEnc) return false;
        return true;
    }

    /// <summary>
    /// Whether the 12 bytes at <paramref name="offset"/> are the character record behind a
    /// status block carrying these maxima: six in-range attribute bytes followed by the same
    /// three maxima the status block reports.
    /// </summary>
    public static bool IsCharacterRecord(byte[] buffer, int offset, int maxHp, int maxSp, int maxEnc)
    {
        if (offset < 0 || offset + RecordSize > buffer.Length) return false;
        for (int i = 0; i < AttributeCount; i++)
        {
            int v = buffer[offset + RecordAttributes + i];
            if (v < 1 || v > LocatorMaxAttribute) return false;
        }
        return ReadU16(buffer, offset + RecordMaxHp) == maxHp
            && ReadU16(buffer, offset + RecordMaxSp) == maxSp
            && ReadU16(buffer, offset + RecordMaxEnc) == maxEnc;
    }

    /// <summary>Whether the bytes at <paramref name="offset"/> are a plausible player actor.</summary>
    public static bool IsPlayerActor(byte[] buffer, int offset)
    {
        if (offset < 0 || offset + ActorSize > buffer.Length) return false;
        int hp = ReadU16(buffer, offset + ActorCurrentHp);
        int sp = ReadU16(buffer, offset + ActorCurrentSp);
        if (hp < 1 || hp > LocatorMaxVital) return false;
        if (sp > LocatorMaxVital) return false;

        for (int i = 0; i < PlayerActorName.Length; i++)
            if (buffer[offset + ActorName + i] != (byte)PlayerActorName[i]) return false;
        return buffer[offset + ActorName + PlayerActorName.Length] == 0;
    }
}

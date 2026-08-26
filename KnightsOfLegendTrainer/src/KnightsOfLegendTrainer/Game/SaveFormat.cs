namespace KnightsOfLegendTrainer.Game;

/// <summary>
/// The chardata save file format for Knights of Legend. The full layout is not publicly
/// documented; only the quest status region (offsets 482-487) is confirmed. Quest status
/// encodes 24 quests in 6 bytes using 2 bits per quest, packed two quests per hex digit.
///
/// Encoding: each byte holds four quests (two hex digits, each digit = two quests).
/// The 2-bit codes are: 00 = not given, 01 = given but not complete,
/// 10 = complete but medal not given, 11 = medal given. [Manual]
/// </summary>
internal static class SaveFormat
{
    /// <summary>Offset of the quest status block in the chardata file. [Manual]</summary>
    public const int QuestStatusOffset = 0x1E2;

    /// <summary>Length of the quest status block in bytes. [Manual]</summary>
    public const int QuestStatusLength = 6;

    /// <summary>Number of quests encoded. [Manual]</summary>
    public const int QuestCount = 24;

    /// <summary>Bits per quest. [Manual]</summary>
    public const int BitsPerQuest = 2;

    /// <summary>Quest status: not given. [Manual]</summary>
    public const int StatusNotGiven = 0;

    /// <summary>Quest status: given but not complete. [Manual]</summary>
    public const int StatusGiven = 1;

    /// <summary>Quest status: complete but medal not given. [Manual]</summary>
    public const int StatusComplete = 2;

    /// <summary>Quest status: medal given (fully resolved). [Manual]</summary>
    public const int StatusMedalGiven = 3;

    /// <summary>Human-readable labels for the four quest status codes. [Manual]</summary>
    public static readonly string[] StatusLabels =
    {
        "Not Given", "Given", "Complete", "Medal Given"
    };

    /// <summary>
    /// Reads the status of quest <paramref name="questIndex"/> (0-23) from the
    /// <paramref name="data"/> buffer. The buffer must contain at least
    /// <see cref="QuestStatusOffset"/> + <see cref="QuestStatusLength"/> bytes.
    /// </summary>
    public static int ReadQuestStatus(byte[] data, int questIndex)
    {
        if (questIndex < 0 || questIndex >= QuestCount)
            throw new ArgumentOutOfRangeException(nameof(questIndex));
        if (data.Length < QuestStatusOffset + QuestStatusLength)
            throw new ArgumentException("Buffer too small for chardata quest status.", nameof(data));

        int bitOffset = questIndex * BitsPerQuest;
        int byteOffset = QuestStatusOffset + bitOffset / 8;
        int shift = bitOffset % 8;
        return (data[byteOffset] >> shift) & 0x3;
    }

    /// <summary>
    /// Writes the status of quest <paramref name="questIndex"/> (0-23) into the
    /// <paramref name="data"/> buffer. The value is clamped to 0-3. The buffer must
    /// contain at least <see cref="QuestStatusOffset"/> + <see cref="QuestStatusLength"/> bytes.
    /// </summary>
    public static void WriteQuestStatus(byte[] data, int questIndex, int status)
    {
        if (questIndex < 0 || questIndex >= QuestCount)
            throw new ArgumentOutOfRangeException(nameof(questIndex));
        if (data.Length < QuestStatusOffset + QuestStatusLength)
            throw new ArgumentException("Buffer too small for chardata quest status.", nameof(data));

        int clamped = Math.Clamp(status, 0, 3);
        int bitOffset = questIndex * BitsPerQuest;
        int byteOffset = QuestStatusOffset + bitOffset / 8;
        int shift = bitOffset % 8;
        int mask = 0x3 << shift;
        data[byteOffset] = (byte)((data[byteOffset] & ~mask) | ((clamped << shift) & mask));
    }

    /// <summary>Returns true if the buffer is large enough to hold a chardata quest status block.</summary>
    public static bool IsValidChardata(byte[] data) =>
        data.Length >= QuestStatusOffset + QuestStatusLength;

    /// <summary>Reads all 24 quest statuses into an int array.</summary>
    public static int[] ReadAllQuestStatuses(byte[] data)
    {
        var result = new int[QuestCount];
        for (int i = 0; i < QuestCount; i++)
            result[i] = ReadQuestStatus(data, i);
        return result;
    }

    /// <summary>Status label for a given quest index.</summary>
    public static string GetStatusLabel(byte[] data, int questIndex)
    {
        int status = ReadQuestStatus(data, questIndex);
        return StatusLabels[status];
    }
}

using System.IO;
using System.Text;

namespace MightAndMagic1Trainer.Game;

/// <summary>
/// One message an overlay prints: a single null-terminated string out of the file's data section.
///
/// <para><see cref="Lines"/> keeps the game's own window breaks (the <c>0x0D</c> bytes inside the
/// string) because they are load-bearing rather than cosmetic: MM1 wraps at a fixed width and does
/// not add a space at the break, so lines joined naively run words together exactly as they do on
/// the original screen ("COME TO" + "THE RIGHT PLACE"). Reflowing the text would quietly change what
/// the game says.</para>
/// </summary>
/// <param name="Offset">Where the string starts, relative to the data section.</param>
/// <param name="Lines">The message, split on the game's line breaks.</param>
public sealed record OverlayMessage(int Offset, IReadOnlyList<string> Lines)
{
    /// <summary>The lines run together with a space, for searching. Not for display — see the note above.</summary>
    public string SearchText => string.Join(" ", Lines);

    /// <summary>The first line, for a table of contents.</summary>
    public string FirstLine => Lines.Count > 0 ? Lines[0] : "";

    /// <summary>Whether the message contains <paramref name="text"/>, ignoring the window breaks.</summary>
    public bool Mentions(string text) =>
        SearchText.Contains(text, StringComparison.OrdinalIgnoreCase);

    public override string ToString() => SearchText;
}

/// <summary>How the reader decided where the text starts in an overlay's data section.</summary>
public enum OverlayTextStart
{
    /// <summary>The dispatch tables were the documented size, so the first string is arithmetic.</summary>
    DispatchTable,

    /// <summary>The tables were not the documented shape; the first plausible phrase was searched for.</summary>
    FirstPhrase,
}

/// <summary>
/// One <c>*.ovr</c> overlay: the compiled event handlers for a single Might &amp; Magic 1 location,
/// and — the part a cluebook wants — every sign, hint, riddle and prompt those handlers print.
///
/// <para><b>The format is this project's own decode</b>, written up in <c>docs/ovr-format.md</c>:
/// a 14-byte header, a code section, a data section, and the rule
/// <c>code_size + data_size = filesize - 14</c> that holds for all 55 shipped files and is what
/// proves the split. The header's two signature words and the <c>data_addr = code_size + 0xF43E</c>
/// bias are checked here, so a file that merely ends in <c>.ovr</c> cannot be read as one.</para>
///
/// <para><b>Nothing from the game is redistributed.</b> This reads the overlays out of the
/// installation the player already has, the same way the drawn map reads their
/// <c>Mazedata.dta</c>. The trainer ships no game text.</para>
/// </summary>
public sealed class Overlay
{
    /// <summary>Bytes before the code section.</summary>
    public const int HeaderSize = 14;

    /// <summary>Header word 0, constant in every shipped overlay (<c>docs/ovr-format.md</c> §3).</summary>
    public const ushort Signature0 = 0x00F2;

    /// <summary>Header word 1, likewise constant.</summary>
    public const ushort Signature1 = 0xF47C;

    /// <summary>Header word at 0x06: the resident data base the init stub installs.</summary>
    public const ushort ResidentBase = 0xC940;

    /// <summary>Run-time offset the code section is loaded at, so <c>data_addr - this + 14</c> is a file offset.</summary>
    public const ushort CodeLoadAddress = 0xF43E;

    /// <summary>
    /// Bytes of dispatch table per event: one id, one flag mask, and a two-byte handler pointer.
    ///
    /// Recovered from the <c>Pp3.ovr</c> disassembly in <c>docs/ovr-format.md</c> §8, where the
    /// dispatcher reads a count at the data section's first byte, scans the id table immediately
    /// after it, ands against a parallel mask table, then calls through a pointer table — putting
    /// the first string at <c>1 + 4 × count</c>, which is exactly where that file's text begins.
    /// </summary>
    private const int DispatchBytesPerEvent = 4;

    /// <summary>Shortest run of text kept as a message. Below this a run is padding or table bytes.</summary>
    private const int MinMessageLength = 3;

    /// <summary>
    /// What the first message must clear when the reader has to go looking for it.
    ///
    /// The id and pointer tables at the head of the data section occasionally decode as a short
    /// printable run — <c>docs/ovr-format.md</c> names <c>8XVZ</c>, <c>GUTZ4:</c> and <c>;.JBR</c> —
    /// so the fallback needs a bar those cannot clear. Every message in the extracted content is far
    /// longer than this; the shortest run to about ten characters.
    /// </summary>
    private const int MinFirstMessageLength = 8;

    private Overlay(string rawName, string fileName, int fileSize, ushort codeSize, uint dataSize,
                    ushort dataAddress, int eventCount, IReadOnlyList<byte> eventIds,
                    OverlayTextStart textStart, IReadOnlyList<OverlayMessage> messages,
                    IReadOnlyList<string> notes)
    {
        RawName = rawName;
        FileName = fileName;
        FileSize = fileSize;
        CodeSize = codeSize;
        DataSize = dataSize;
        DataAddress = dataAddress;
        EventCount = eventCount;
        EventIds = eventIds;
        TextStart = textStart;
        Messages = messages;
        Notes = notes;
    }

    /// <summary>The overlay's name as the engine builds it, e.g. <c>sorpigal</c>.</summary>
    public string RawName { get; }

    /// <summary>The file it was read from, e.g. <c>Sorpigal.ovr</c>.</summary>
    public string FileName { get; }

    public int FileSize { get; }
    public ushort CodeSize { get; }
    public uint DataSize { get; }

    /// <summary>Header offset 0x0C: where the data section lands at run time.</summary>
    public ushort DataAddress { get; }

    /// <summary>
    /// How many event tiles the location's dispatcher knows about, from the data section's first
    /// byte — meaningful only when <see cref="TextStart"/> is
    /// <see cref="OverlayTextStart.DispatchTable"/>.
    /// </summary>
    public int EventCount { get; }

    /// <summary>
    /// The event ids the dispatcher scans. <b>What they index is not established</b> — the format
    /// notes call them "small byte values that look like map coordinates / event indices" — so they
    /// are carried as numbers and never presented as squares.
    /// </summary>
    public IReadOnlyList<byte> EventIds { get; }

    /// <summary>How the start of the text was found.</summary>
    public OverlayTextStart TextStart { get; }

    /// <summary>Every string the handlers print, in the order the file stores them.</summary>
    public IReadOnlyList<OverlayMessage> Messages { get; }

    /// <summary>Anything about this file the reader could not fully account for.</summary>
    public IReadOnlyList<string> Notes { get; }

    /// <summary>The first message mentioning <paramref name="text"/>, or null.</summary>
    public OverlayMessage? Find(string text) => Messages.FirstOrDefault(m => m.Mentions(text));

    /// <summary>
    /// Reads one overlay, or returns null with <paramref name="why"/> set.
    ///
    /// The header is checked rather than trusted: both signature words, the section arithmetic, and
    /// that everything past the two sections is zero. A file that fails any of those is not an
    /// overlay of this game, and reading its data section as text would produce confident rubbish.
    /// </summary>
    public static Overlay? TryRead(ReadOnlySpan<byte> bytes, string rawName, string fileName, out string why)
    {
        ArgumentNullException.ThrowIfNull(rawName);
        ArgumentNullException.ThrowIfNull(fileName);

        if (bytes.Length < HeaderSize)
        {
            why = $"{fileName} is {bytes.Length} bytes — shorter than the {HeaderSize}-byte header.";
            return null;
        }

        ushort sig0 = ReadU16(bytes, 0x00);
        ushort sig1 = ReadU16(bytes, 0x02);
        if (sig0 != Signature0 || sig1 != Signature1)
        {
            why = $"{fileName} does not start with the overlay signature " +
                  $"(got {sig0:X4} {sig1:X4}, expected {Signature0:X4} {Signature1:X4}).";
            return null;
        }

        ushort codeSize = ReadU16(bytes, 0x04);
        ushort marker = ReadU16(bytes, 0x06);
        uint dataSize = ReadU32(bytes, 0x08);
        ushort dataAddress = ReadU16(bytes, 0x0C);

        long end = (long)HeaderSize + codeSize + dataSize;
        if (end > bytes.Length)
        {
            why = $"{fileName} claims {codeSize} code + {dataSize} data bytes, which overruns its {bytes.Length}.";
            return null;
        }

        // The two sections should account for the whole file. The format notes allow a few bytes of
        // padding, so what says this is not an overlay is a non-zero tail, not a short one.
        var tail = bytes[(int)end..];
        if (tail.IndexOfAnyExcept((byte)0) >= 0)
        {
            why = $"{fileName} has {tail.Length} bytes past its two sections and they are not all zero.";
            return null;
        }

        var notes = new List<string>();
        if (marker != ResidentBase)
            notes.Add($"header offset 0x06 is {marker:X4}, not the {ResidentBase:X4} every shipped overlay carries");
        if (dataAddress != (ushort)(codeSize + CodeLoadAddress))
            notes.Add($"data_addr {dataAddress:X4} is not code_size + {CodeLoadAddress:X4} " +
                      $"({(ushort)(codeSize + CodeLoadAddress):X4}), so the file may be from another build");
        if (tail.Length > 0)
            notes.Add($"{tail.Length} zero bytes follow the data section");

        var data = bytes.Slice(HeaderSize + codeSize, (int)dataSize);
        int textAt = FindTextStart(data, out var start, out int eventCount);

        var ids = new List<byte>();
        if (start == OverlayTextStart.DispatchTable)
        {
            for (int i = 0; i < eventCount; i++) ids.Add(data[1 + i]);
        }
        else
        {
            notes.Add("the dispatch tables were not the documented size, so the text was found by " +
                      "looking for the first phrase rather than by arithmetic");
        }

        why = "";
        return new Overlay(rawName, fileName, bytes.Length, codeSize, dataSize, dataAddress,
                           start == OverlayTextStart.DispatchTable ? eventCount : 0, ids,
                           start, ReadMessages(data, textAt), notes);
    }

    /// <inheritdoc cref="TryRead(ReadOnlySpan{byte}, string, string, out string)"/>
    public static Overlay? TryReadFile(string path, string rawName, out string why)
    {
        ArgumentNullException.ThrowIfNull(path);
        try
        {
            return TryRead(File.ReadAllBytes(path), rawName, Path.GetFileName(path), out why);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            why = $"Could not read {Path.GetFileName(path)}: {e.Message}";
            return null;
        }
    }

    /// <summary>
    /// Where the location's text begins in the data section.
    ///
    /// The arithmetic is tried first and then <i>checked</i>, rather than trusted: the tables' shape
    /// is recovered from one disassembled file, so a location whose dispatcher is built differently
    /// would otherwise have its first handful of messages silently swallowed. When the computed
    /// offset does not land on a phrase, the reader falls back to looking for one — and says so,
    /// through <see cref="OverlayTextStart"/>, so the cluebook can carry the caveat rather than hide
    /// it.
    /// </summary>
    private static int FindTextStart(ReadOnlySpan<byte> data, out OverlayTextStart how, out int eventCount)
    {
        eventCount = data.Length > 0 ? data[0] : 0;
        int computed = 1 + eventCount * DispatchBytesPerEvent;

        if (computed < data.Length && IsPhraseAt(data, computed))
        {
            how = OverlayTextStart.DispatchTable;
            return computed;
        }

        how = OverlayTextStart.FirstPhrase;
        for (int at = 0; at < data.Length; at++)
        {
            // Start of a string, or of the first text after something that cannot be one. The second
            // half matters: the tables the fallback is walking past end in pointer bytes, not in a
            // terminator, so insisting on a preceding NUL would skip the first message.
            if (at > 0 && data[at - 1] != 0 && IsTextByte(data[at - 1])) continue;
            if (IsPhraseAt(data, at)) return at;
        }
        return data.Length;
    }

    /// <summary>Whether a null-terminated phrase long enough to be game text starts at <paramref name="at"/>.</summary>
    private static bool IsPhraseAt(ReadOnlySpan<byte> data, int at)
    {
        int end = at;
        while (end < data.Length && data[end] != 0)
        {
            if (!IsTextByte(data[end])) return false;
            end++;
        }
        if (end >= data.Length) return false;               // unterminated: not a string
        return end - at >= MinFirstMessageLength && HasLetterRun(data[at..end]);
    }

    /// <summary>Splits the rest of the data section into messages on the null terminators.</summary>
    private static List<OverlayMessage> ReadMessages(ReadOnlySpan<byte> data, int from)
    {
        var messages = new List<OverlayMessage>();

        for (int at = from; at < data.Length;)
        {
            int end = at;
            while (end < data.Length && data[end] != 0) end++;

            var run = data[at..end];
            if (run.Length >= MinMessageLength && IsText(run) && HasLetterRun(run))
                messages.Add(new OverlayMessage(at, SplitLines(run)));

            at = end + 1;
        }

        return messages;
    }

    /// <summary>Splits one string on the game's own <c>0x0D</c> window breaks.</summary>
    private static List<string> SplitLines(ReadOnlySpan<byte> run)
    {
        var lines = new List<string>();
        var line = new StringBuilder();

        foreach (byte b in run)
        {
            if (b == (byte)'\r')
            {
                lines.Add(line.ToString().TrimEnd());
                line.Clear();
                continue;
            }
            line.Append((char)b);
        }

        lines.Add(line.ToString().TrimEnd());
        while (lines.Count > 1 && lines[^1].Length == 0) lines.RemoveAt(lines.Count - 1);
        return lines;
    }

    /// <summary>A byte the game's text window can hold: printable ASCII, or its line break.</summary>
    private static bool IsTextByte(byte b) => b == (byte)'\r' || b is >= 0x20 and <= 0x7E;

    private static bool IsText(ReadOnlySpan<byte> run)
    {
        foreach (byte b in run) if (!IsTextByte(b)) return false;
        return true;
    }

    /// <summary>Whether the run holds three letters in a row — a word, rather than table bytes.</summary>
    private static bool HasLetterRun(ReadOnlySpan<byte> run)
    {
        int streak = 0;
        foreach (byte b in run)
        {
            bool letter = b is >= (byte)'A' and <= (byte)'Z' or >= (byte)'a' and <= (byte)'z';
            streak = letter ? streak + 1 : 0;
            if (streak >= 3) return true;
        }
        return false;
    }

    private static ushort ReadU16(ReadOnlySpan<byte> b, int at) => (ushort)(b[at] | (b[at + 1] << 8));

    private static uint ReadU32(ReadOnlySpan<byte> b, int at) =>
        (uint)(b[at] | (b[at + 1] << 8) | (b[at + 2] << 16) | (b[at + 3] << 24));
}

/// <summary>
/// The overlays found in one Might &amp; Magic 1 installation, lined up with the game's own map
/// order so a location's walls and its words can be shown together.
///
/// <para>The alignment is free rather than guessed: an overlay is named after its location, and
/// <see cref="MazeMap.RawName"/> already carries that name for every one of the 55 maze records —
/// both come from the same table in <c>Mm.exe</c>.</para>
/// </summary>
public sealed class OverlaySet
{
    private readonly Dictionary<string, Overlay> _byName;

    private OverlaySet(string folder, Dictionary<string, Overlay> byName, IReadOnlyList<string> problems)
    {
        Folder = folder;
        _byName = byName;
        Problems = problems;
    }

    /// <summary>The folder the overlays were read from.</summary>
    public string Folder { get; }

    /// <summary>Files that looked like overlays but could not be read, and why.</summary>
    public IReadOnlyList<string> Problems { get; }

    /// <summary>How many locations have text.</summary>
    public int Count => _byName.Count;

    /// <summary>Every overlay read, in no particular order.</summary>
    public IEnumerable<Overlay> All => _byName.Values;

    /// <summary>The overlay for a maze record's raw name, or null when that file is absent.</summary>
    public Overlay? For(string rawName) =>
        rawName is not null && _byName.TryGetValue(rawName, out var overlay) ? overlay : null;

    /// <summary>
    /// Reads every <c>&lt;name&gt;.ovr</c> in <paramref name="folder"/> that one of
    /// <paramref name="rawNames"/> asks for.
    ///
    /// The directory is enumerated once and matched case-insensitively rather than each name being
    /// probed: the shipped files are <c>Sorpigal.ovr</c> while the engine's own table says
    /// <c>sorpigal</c>, and a folder copied off a case-sensitive file system keeps whichever case it
    /// was given.
    /// </summary>
    public static OverlaySet Load(string folder, IEnumerable<string> rawNames)
    {
        ArgumentNullException.ThrowIfNull(rawNames);

        var found = new Dictionary<string, Overlay>(StringComparer.OrdinalIgnoreCase);
        var problems = new List<string>();

        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            return new OverlaySet(folder ?? "", found, problems);

        Dictionary<string, string> onDisk;
        try
        {
            onDisk = Directory.EnumerateFiles(folder, "*.ovr")
                              .GroupBy(p => Path.GetFileNameWithoutExtension(p) ?? "", StringComparer.OrdinalIgnoreCase)
                              .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            problems.Add($"Could not list {folder}: {e.Message}");
            return new OverlaySet(folder, found, problems);
        }

        foreach (string name in rawNames.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!onDisk.TryGetValue(name, out string? path)) continue;

            var overlay = Overlay.TryReadFile(path, name, out string why);
            if (overlay is null) problems.Add(why);
            else found[name] = overlay;
        }

        return new OverlaySet(folder, found, problems);
    }
}

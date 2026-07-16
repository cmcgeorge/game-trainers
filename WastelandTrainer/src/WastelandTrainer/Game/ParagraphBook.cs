using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace WastelandTrainer.Game;

/// <summary>One numbered passage from the printed Wasteland paragraph book.</summary>
public sealed record ParagraphEntry(int Number, string Text)
{
    public string Label => $"#{Number}";
}

/// <summary>
/// Loads Wasteland's paragraph book from the game's own <c>paragraphs.txt</c> at runtime rather
/// than embedding the copyrighted booklet text in source. At scripted spots the game prompts the
/// player to "read paragraph #N"; this parses the shipped text file into numbered entries so the
/// trainer can show them as a lookup aid. If the file cannot be found the References tab shows a
/// short instruction instead. Reference only; drives no memory writes.
/// </summary>
public static class ParagraphBook
{
    // A paragraph begins with up to a few leading spaces, the number, a dot and whitespace.
    // Continuation lines in the shipped file are indented further, so they never match.
    private static readonly Regex StartLine = new(@"^\s{0,6}(\d{1,4})\.\s+(.*)$", RegexOptions.Compiled);

    /// <summary>Parses the raw booklet text into numbered entries (best-effort).</summary>
    public static IReadOnlyList<ParagraphEntry> Parse(string text)
    {
        var entries = new List<ParagraphEntry>();
        int currentNumber = -1;
        var sb = new StringBuilder();

        void Flush()
        {
            if (currentNumber >= 0)
                entries.Add(new ParagraphEntry(currentNumber, CollapseWhitespace(sb.ToString())));
            sb.Clear();
        }

        foreach (var raw in text.Replace("\r\n", "\n").Split('\n'))
        {
            var m = StartLine.Match(raw);
            if (m.Success && int.TryParse(m.Groups[1].Value, out int n) && n != currentNumber)
            {
                Flush();
                currentNumber = n;
                sb.Append(m.Groups[2].Value.Trim());
            }
            else if (currentNumber >= 0)
            {
                string t = raw.Trim();
                if (t.Length > 0) { if (sb.Length > 0) sb.Append(' '); sb.Append(t); }
            }
        }
        Flush();
        return entries;
    }

    private static string CollapseWhitespace(string s) =>
        Regex.Replace(s, @"\s+", " ").Trim();

    /// <summary>
    /// Loads and parses <c>paragraphs.txt</c> from <paramref name="folder"/>. Returns false and a
    /// human-readable <paramref name="status"/> when the file is missing or unreadable.
    /// </summary>
    public static bool TryLoadFromFolder(string folder, out IReadOnlyList<ParagraphEntry> entries, out string status)
    {
        entries = Array.Empty<ParagraphEntry>();
        try
        {
            string path = Path.Combine(folder, "paragraphs.txt");
            if (!File.Exists(path))
            {
                status = $"paragraphs.txt not found in {folder}.";
                return false;
            }
            byte[] bytes = File.ReadAllBytes(path);
            string text = Encoding.GetEncoding(28591).GetString(bytes);   // Latin-1
            entries = Parse(text);
            status = entries.Count > 0
                ? $"Loaded {entries.Count} paragraph(s) from {path}."
                : $"paragraphs.txt at {path} contained no recognisable numbered entries.";
            return entries.Count > 0;
        }
        catch (Exception ex)
        {
            status = "Failed to read paragraphs.txt: " + ex.Message;
            return false;
        }
    }
}

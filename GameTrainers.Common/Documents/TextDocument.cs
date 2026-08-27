using System.Text;

namespace GameTrainers.Common.Documents;

/// <summary>
/// Builds a plain-text report: underlined headings, a label column, bullets and wrapped prose.
///
/// Small, but the two things it does are the two that are easy to get subtly wrong by hand and hard
/// to notice afterwards: a bullet whose continuation lines hang under the text rather than under the
/// dash, and a wrap that never breaks a word in half. Both were written wrongly first in The Quest's
/// cluebook writer, which is why they live here.
///
/// Plain text is worth producing beside anything richer: it diffs between two versions of the same
/// game data, it greps, and it reads in a terminal beside the game.
/// </summary>
public sealed class TextDocument
{
    private readonly StringBuilder _sb = new();

    /// <summary>Builds a document wrapped at <paramref name="width"/> columns.</summary>
    public TextDocument(int width = 92)
    {
        if (width < 20) throw new ArgumentOutOfRangeException(nameof(width), width, "too narrow to wrap into");
        Width = width;
    }

    /// <summary>The wrap column.</summary>
    public int Width { get; }

    /// <summary>Columns given to a label before its value, in <see cref="Fact"/>.</summary>
    public int LabelWidth { get; init; } = 18;

    /// <summary>The document's title, underlined with <c>=</c>.</summary>
    public TextDocument Title(string text) => Rule(text, '=');

    /// <summary>A section heading, preceded by a blank line and underlined with <c>-</c>.</summary>
    public TextDocument Heading(string text)
    {
        Blank();
        return Rule(text, '-');
    }

    /// <summary>A label and a value in two columns.</summary>
    public TextDocument Fact(string name, string value)
    {
        _sb.Append("  ").Append(name.PadRight(LabelWidth)).AppendLine(value);
        return this;
    }

    /// <summary>What a bullet puts before its text. Its width is what the continuation hangs at.</summary>
    private const string Marker = "- ";

    /// <summary>
    /// A bullet. Continuation lines hang under the text, not under the dash, so a long note reads as
    /// one item rather than as several.
    ///
    /// The hanging indent is <paramref name="indent"/> plus the width of the marker, so the wrapped
    /// lines start in the same column the first line's text does. Getting that arithmetic wrong is
    /// not a crash — it is a document that looks very slightly off and wraps a word early, which is
    /// why the check for it compares columns rather than eyeballing the output.
    /// </summary>
    public TextDocument Bullet(string text, string indent = "  ")
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(indent);

        string hanging = indent + new string(' ', Marker.Length);

        var wrapped = new TextDocument(Width);
        wrapped.Paragraph(text, hanging);
        string body = wrapped.ToString();

        // Nothing to wrap: a bullet with no text is still a bullet, and the arithmetic below would
        // run off the front of an empty string.
        if (body.Length < hanging.Length)
        {
            _sb.Append(indent).Append(Marker.TrimEnd()).AppendLine();
            return this;
        }

        // The first line's indent is replaced by the marker; the rest keep the hanging indent.
        _sb.Append(indent).Append(Marker).Append(body, hanging.Length, body.Length - hanging.Length);
        return this;
    }

    /// <summary>Prose, wrapped at <see cref="Width"/> and indented, never breaking a word.</summary>
    public TextDocument Paragraph(string text, string indent = "")
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(indent);

        int room = Math.Max(20, Width - indent.Length);
        var line = new StringBuilder();

        foreach (string word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Length > 0 && line.Length + 1 + word.Length > room)
            {
                _sb.Append(indent).AppendLine(line.ToString());
                line.Clear();
            }
            if (line.Length > 0) line.Append(' ');
            line.Append(word);
        }

        if (line.Length > 0) _sb.Append(indent).AppendLine(line.ToString());
        return this;
    }

    /// <summary>One line, as given.</summary>
    public TextDocument Line(string text = "")
    {
        _sb.AppendLine(text);
        return this;
    }

    /// <summary>A blank line.</summary>
    public TextDocument Blank()
    {
        _sb.AppendLine();
        return this;
    }

    /// <summary>The document.</summary>
    public override string ToString() => _sb.ToString();

    private TextDocument Rule(string text, char rule)
    {
        _sb.AppendLine(text);
        _sb.AppendLine(new string(rule, Math.Clamp(text.Length, 4, Width)));
        return this;
    }
}

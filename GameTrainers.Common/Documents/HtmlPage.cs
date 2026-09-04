using System.Text;

namespace GameTrainers.Common.Documents;

/// <summary>
/// Builds one self-contained HTML page.
///
/// <b>Self-contained is the point, and it is enforced rather than intended.</b> A generated strategy
/// guide gets moved, mailed and opened years later on a machine with no network; a page that pulls a
/// stylesheet, a font or an image from somewhere is a page that will one day render as a column of
/// unstyled text. <see cref="IsSelfContained"/> is the check, and a verification harness can call it
/// on whatever the writer produced.
///
/// The page itself is assembled by the caller — section structure is where a document says what it is
/// about, and that is not shared between two games. What is shared is the scaffold, the escaping and
/// that one invariant.
/// </summary>
public sealed class HtmlPage
{
    private readonly StringBuilder _body = new();
    private readonly List<string> _styles = [];

    /// <summary>Builds a page titled <paramref name="title"/>.</summary>
    public HtmlPage(string title)
    {
        ArgumentNullException.ThrowIfNull(title);
        PageTitle = title;
    }

    /// <summary>What the browser puts in the tab.</summary>
    public string PageTitle { get; }

    /// <summary>The document language, for a screen reader and for hyphenation.</summary>
    public string Language { get; init; } = "en";

    /// <summary>Adds a stylesheet, inlined into the page's own <c>&lt;style&gt;</c>.</summary>
    public HtmlPage Style(string css)
    {
        _styles.Add(css);
        return this;
    }

    /// <summary>Appends markup to the body as-is. Escape anything that came from data first.</summary>
    public HtmlPage Append(string markup)
    {
        _body.Append(markup);
        return this;
    }

    /// <inheritdoc cref="Append"/>
    public HtmlPage AppendLine(string markup)
    {
        _body.AppendLine(markup);
        return this;
    }

    /// <summary>Appends escaped text.</summary>
    public HtmlPage Text(string text) => Append(Escape(text));

    /// <summary>The whole document.</summary>
    public string ToHtml()
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.Append("<html lang=\"").Append(EscapeAttribute(Language)).AppendLine("\"><head><meta charset=\"utf-8\">");
        sb.Append("<title>").Append(Escape(PageTitle)).AppendLine("</title>");
        sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");

        foreach (string css in _styles) sb.Append("<style>").Append(css).AppendLine("</style>");

        sb.AppendLine("</head><body>");
        sb.Append(_body);
        sb.AppendLine("</body></html>");
        return sb.ToString();
    }

    /// <inheritdoc cref="ToHtml"/>
    public override string ToString() => ToHtml();

    /// <summary>
    /// Escapes text for element content.
    ///
    /// Quotes are escaped even though element content does not require it. That is deliberate: a page
    /// is assembled by hand out of string interpolation, so the same helper inevitably ends up inside
    /// an attribute sooner or later, and the cost of being wrong there is a value that closes the
    /// attribute and turns the rest of it into markup. The cost of being right is <c>&amp;quot;</c> in
    /// the source, which no reader ever sees.
    /// </summary>
    public static string Escape(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var sb = new StringBuilder(value.Length);
        foreach (char c in value)
        {
            switch (c)
            {
                case '&': sb.Append("&amp;"); break;
                case '<': sb.Append("&lt;"); break;
                case '>': sb.Append("&gt;"); break;
                case '"': sb.Append("&quot;"); break;
                default:
                    if (c is '\t' or '\n' or '\r' || c >= ' ') sb.Append(c);
                    break;
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// Escapes a value going into a double-quoted attribute.
    ///
    /// The same as <see cref="Escape"/> today, and a separate name on purpose: a call site that says
    /// which context it is in documents itself, and if attribute escaping ever has to do more than
    /// text escaping this is where it goes.
    /// </summary>
    public static string EscapeAttribute(string value) => Escape(value);

    /// <summary>
    /// Whether <paramref name="html"/> looks like it can be opened with no network and no other file
    /// beside it, and with nothing that executes.
    ///
    /// <b>This is a conservative scan of the raw markup, not a proof.</b> It does not parse the
    /// document, so it cannot tell a URL inside an attribute from one inside a sentence: a page whose
    /// prose quotes a web address is reported as not self-contained even though the text is inert.
    /// That direction is the safe one to be wrong in — it is a guard for a verification harness, and
    /// the answer it must never get wrong is "yes" for a page that does reach out.
    ///
    /// Namespace declarations are allowed through, because a namespace is an identifier a document
    /// must declare rather than something a browser goes and asks for. They are matched as
    /// <c>xmlns…="…"</c> rather than deleted wherever they appear, so a URL that merely begins with a
    /// namespace cannot hide behind one.
    /// </summary>
    /// <param name="html">The page.</param>
    /// <param name="why">Set to the first thing found that would reach outside the file, or execute.</param>
    public static bool IsSelfContained(string html, out string why)
    {
        ArgumentNullException.ThrowIfNull(html);

        string scanned = NamespaceDeclaration.Replace(html, "");

        foreach (string forbidden in Outbound)
        {
            if (scanned.Contains(forbidden, StringComparison.OrdinalIgnoreCase))
            {
                why = $"the page contains \"{forbidden}\", which reaches outside the file or executes";
                return false;
            }
        }

        foreach (var pattern in OutboundPatterns)
        {
            var match = pattern.Match(scanned);
            if (match.Success)
            {
                why = $"the page contains \"{match.Value}\", which reaches outside the file or executes";
                return false;
            }
        }

        why = "";
        return true;
    }

    /// <summary>
    /// What a self-contained page may not contain.
    ///
    /// <c>&lt;base&gt;</c> is in the list because it silently re-points every relative URL in the
    /// document; <c>@import</c> and a non-<c>data:</c> <c>url(</c> because <see cref="Style"/> writes
    /// CSS in raw and a relative URL there has no scheme to spot.
    /// </summary>
    private static readonly string[] Outbound =
    [
        "<script", "<link", "<img", "<iframe", "<object", "<embed", "<base",
        "<video", "<audio", "<source", "<track", "<use ",
        "http://", "https://", "//", "javascript:", "@import", "http-equiv",
    ];

    /// <summary>
    /// The same idea where a substring will not do: an inline event handler is any <c>on…=</c>
    /// attribute, and <c>url(</c> is only outbound when it is not a self-contained <c>data:</c> URI or
    /// a reference to something in this same document.
    /// </summary>
    private static readonly System.Text.RegularExpressions.Regex[] OutboundPatterns =
    [
        new(@"\son[a-z]+\s*=", RegexOptions),
        new(@"url\(\s*['""]?(?!data:|#)", RegexOptions),
    ];

    /// <summary>A namespace declaration, which is an identifier rather than a fetch.</summary>
    private static readonly System.Text.RegularExpressions.Regex NamespaceDeclaration =
        new(@"xmlns(:\w+)?\s*=\s*""[^""]*""", RegexOptions);

    private const System.Text.RegularExpressions.RegexOptions RegexOptions =
        System.Text.RegularExpressions.RegexOptions.IgnoreCase |
        System.Text.RegularExpressions.RegexOptions.CultureInvariant;
}

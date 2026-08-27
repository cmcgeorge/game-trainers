using System.Globalization;
using GameTrainers.Common.Documents;

namespace TheQuestTrainer.FormatCheck;

/// <summary>
/// The shared document builders in <c>GameTrainers.Common.Documents</c>.
///
/// They are checked from this harness because this is where they grew: the cluebook and the
/// Alternate Reality city plan were writing the same SVG by hand, and most of what is below is a
/// mistake that was made by hand first in one or other of them. The library has no harness of its
/// own, and giving it one for three files would cost more than it is worth while both consumers run
/// harnesses that exercise it end to end anyway.
/// </summary>
internal static partial class Program
{
    private static void DocumentChecks()
    {
        Section("document builders");

        SvgChecks();
        SvgGuardChecks();
        TextChecks();
        HtmlChecks();
    }

    /// <summary>Markup, nesting, numbers and escaping.</summary>
    private static void SvgChecks()
    {
        // Self-closing versus nested. A <title> written into a rectangle has to be that rectangle's
        // tooltip rather than its next sibling, which is what the hand-rolled version emitted.
        string bare = new SvgCanvas(10, 10).Rect(0, 0, 4, 4).ToSvg();
        Check("an empty element self-closes",
            bare.Contains("<rect x=\"0\" y=\"0\" width=\"4\" height=\"4\" />", StringComparison.Ordinal));

        string titled = new SvgCanvas(10, 10).Rect(0, 0, 4, 4, "a place").ToSvg();
        Check("an element with a child is closed with a tag, not slash-closed",
            titled.Contains("><title>a place</title></rect>", StringComparison.Ordinal) &&
            !titled.Contains("/><title>", StringComparison.Ordinal));

        // Numbers. A machine with a comma decimal separator must not produce viewBox="0 0 10,5 …".
        var wasCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            string german = new SvgCanvas(10.5, 10.5).Circle(1.25, 2.5, 0.75).ToSvg();
            Check("numbers are invariant whatever the machine's culture is",
                german.Contains("cx=\"1.25\"", StringComparison.Ordinal) &&
                german.Contains("r=\"0.75\"", StringComparison.Ordinal) &&
                !german.Contains(',', StringComparison.Ordinal));
        }
        finally
        {
            CultureInfo.CurrentCulture = wasCulture;
        }

        // Escaping, and the two places it differs.
        string quoted = new SvgCanvas(10, 10).Text(0, 0, "Xebec's <Demise> & co", ("class", "a\"b")).ToSvg();
        Check("markup in a text node is escaped", quoted.Contains("&lt;Demise&gt; &amp; co", StringComparison.Ordinal));
        Check("an apostrophe stays readable in a text node", quoted.Contains("Xebec's", StringComparison.Ordinal));
        Check("a quote in an attribute is escaped", quoted.Contains("class=\"a&quot;b\"", StringComparison.Ordinal));

        // Characters XML cannot carry at all, escaped or not. Game strings hold them, and one is
        // enough to make a standalone file refuse to open.
        string withControls = "a" + (char)0x01 + "b" + (char)0x1F + "c";
        Check("characters XML cannot carry are dropped, not escaped into nonsense",
            SvgCanvas.EscapeText(withControls) == "abc");
        Check("tabs and newlines survive, because XML can carry them",
            SvgCanvas.EscapeText("a\tb\nc") == "a\tb\nc");

        Check("the document declares the SVG namespace",
            bare.Contains("xmlns=\"" + SvgCanvas.Namespace + "\"", StringComparison.Ordinal));
        Check("a labelled document says what it is",
            new SvgCanvas(1, 1, "a plan").ToSvg().Contains("aria-label=\"a plan\"", StringComparison.Ordinal));

        // A file wants a size; something embedded in a page wants the page's width and no height,
        // because a fixed height beside a percentage width letterboxes the drawing.
        Check("a standalone canvas carries its own size",
            new SvgCanvas(20, 10).ToSvg().Contains("width=\"20\" height=\"10\"", StringComparison.Ordinal));
        string embedded = SvgCanvas.Responsive(20, 10).ToSvg();
        Check("an embedded canvas takes the page's width and no height",
            embedded.Contains("width=\"100%\"", StringComparison.Ordinal) &&
            !embedded.Contains("height=", StringComparison.Ordinal));
        Check("both keep the viewBox, which is what actually sets the aspect",
            embedded.Contains("viewBox=\"0 0 20 10\"", StringComparison.Ordinal));

        // The file layout, which is what makes an exported map readable rather than one long line.
        string file = SvgCanvas.File(10, 10, "a plan").Open("g").Rect(0, 0, 4, 4).Close().ToSvg();
        Check("a file is laid out one element per line", file.Split('\n').Length >= 4);
        Check("a file indents by depth", file.Contains("\n  <g>", StringComparison.Ordinal));
        Check("a file still self-closes an empty element",
            file.Contains("<rect x=\"0\" y=\"0\" width=\"4\" height=\"4\" />", StringComparison.Ordinal));

        // ...but never inside an element holding text: in SVG that whitespace is drawn, so breaking a
        // label across lines would move it on the page.
        string labelled = SvgCanvas.File(10, 10).Text(1, 2, "Port of Mithria").ToSvg();
        Check("a text element is not broken across lines",
            labelled.Contains(">Port of Mithria</text>", StringComparison.Ordinal));
    }

    /// <summary>What the canvas refuses to do, which is most of what it is for.</summary>
    private static void SvgGuardChecks()
    {
        var scoped = new SvgCanvas(10, 10);
        using (scoped.Scope("g", ("stroke", "#000"))) scoped.Line(0, 0, 1, 1);
        Check("a scope closes its own element", scoped.ToSvg().EndsWith("</g></svg>", StringComparison.Ordinal));

        // A scope closes back to its own depth, so an element left open inside it cannot shift the
        // nesting of everything that follows.
        var strays = new SvgCanvas(10, 10);
        using (strays.Scope("g")) strays.Open("g");
        Check("a scope tidies up an element left open inside it", strays.Depth == 1);

        Check("an attribute added after the tag is closed is refused, not silently dropped",
            ThrowsInvalid(() =>
            {
                var canvas = new SvgCanvas(1, 1);
                canvas.Content("x");
                canvas.Attribute("y", 1);
            }));
        Check("closing more than was opened is refused",
            ThrowsInvalid(() =>
            {
                var canvas = new SvgCanvas(1, 1);
                canvas.Close();
                canvas.Close();
            }));

        // Two attributes of the same name are a well-formedness error, so a standalone file would not
        // open at all. Splicing caller attributes after the fixed ones makes that easy to do by hand.
        Check("a duplicate attribute is refused rather than written",
            ThrowsInvalid(() => new SvgCanvas(10, 10).Rect(0, 0, 4, 4, ("width", 9))));

        // Once the document is closed, anything more would land after </svg>.
        var finished = new SvgCanvas(10, 10);
        string once = finished.ToSvg();
        Check("finishing twice gives the same document", finished.ToSvg() == once);
        Check("writing after the document is finished is refused",
            ThrowsInvalid(() => finished.Rect(0, 0, 1, 1)));

        // ToString is what a debugger, a log line or an interpolated string calls at arbitrary
        // moments; finishing the document there would silently terminate a half-built canvas, and
        // then throw out of a scope's dispose.
        var partial = new SvgCanvas(10, 10);
        partial.Open("g");
        Check("ToString does not finish the document",
            !partial.ToString().Contains("</svg>", StringComparison.Ordinal));
        partial.Rect(0, 0, 1, 1);
        Check("and the canvas still works afterwards",
            partial.ToSvg().EndsWith("</g></svg>", StringComparison.Ordinal));
    }

    /// <summary>Headings, the label column, hanging bullets and the wrap.</summary>
    private static void TextChecks()
    {
        var doc = new TextDocument(40);
        doc.Title("Title").Heading("Section").Fact("Name", "Value");
        doc.Bullet("A note long enough that it has to wrap over more than one line to be read.");

        string text = doc.ToString();
        var lines = text.Split('\n').Select(l => l.TrimEnd('\r')).ToList();

        Check("a title is underlined", lines.Count > 1 && lines[1] == new string('=', 5));
        Check("a heading is preceded by a blank line and underlined",
            lines.Count > 4 && lines[2].Length == 0 && lines[3] == "Section" && lines[4] == new string('-', 7));
        Check("a fact lines its value up in a column",
            lines.Count > 5 && lines[5] == "  Name" + new string(' ', 14) + "Value");

        // The continuation must start in the column the first line's text does — two for the default
        // indent plus two for "- ". Six would look almost right and wrap a word early, which is what
        // the first version of this did, and it changed every note in the shipped cluebook.
        Check("a bullet's continuation hangs under the text, not under the dash",
            lines.Count > 7 && lines[6].StartsWith("  - A note", StringComparison.Ordinal) &&
            Column(lines[7]) == lines[6].IndexOf('A', StringComparison.Ordinal));
        Check("a bullet's continuation is indented exactly four with the default indent",
            lines.Count > 7 && Column(lines[7]) == 4);
        Check("a bullet uses the full width, so it wraps no earlier than a paragraph would",
            lines.Count > 6 && lines[6].Length > 40 - 8);
        Check("nothing runs past the wrap column", lines.TrueForAll(l => l.Length <= 40));

        // A custom indent has to move the marker and the hanging indent together.
        var deep = new TextDocument(40);
        deep.Bullet("A note long enough that it has to wrap over more than one line to be read.", "      ");
        var deepLines = deep.ToString().Split('\n').Select(l => l.TrimEnd('\r')).ToList();
        Check("a custom indent moves the marker", deepLines[0].StartsWith("      - ", StringComparison.Ordinal));
        Check("and moves the hanging indent with it", deepLines.Count > 1 && Column(deepLines[1]) == 8);

        // A bullet with nothing to say is still a bullet; the arithmetic behind the hanging indent
        // runs off the front of an empty string if that is not handled.
        foreach (string empty in new[] { "", "   " })
        {
            var blank = new TextDocument(40);
            bool wrote;
            try { blank.Bullet(empty); wrote = true; }
            catch (ArgumentOutOfRangeException) { wrote = false; }
            Check($"a bullet with {(empty.Length == 0 ? "no" : "only blank")} text does not throw", wrote);
        }

        var narrow = new TextDocument(30);
        narrow.Paragraph("supercalifragilisticexpialidocious and more");
        Check("a word longer than the line is left whole rather than cut in half",
            narrow.ToString().Contains("supercalifragilisticexpialidocious", StringComparison.Ordinal));

        var indented = new TextDocument(40);
        indented.Paragraph("one two three four five six seven eight nine ten", "    ");
        Check("an indent is applied to every line, not just the first",
            indented.ToString().Split('\n').Where(l => l.Trim().Length > 0)
                    .All(l => l.StartsWith("    ", StringComparison.Ordinal)));

        Check("a width nothing could wrap into is refused", ThrowsRange(() => new TextDocument(4)));
    }

    /// <summary>The scaffold, and the self-contained rule that is the whole reason for the class.</summary>
    private static void HtmlChecks()
    {
        Check("a plain page is self-contained", HtmlPage.IsSelfContained("<p>hello</p>", out _));
        Check("a script is caught",
            !HtmlPage.IsSelfContained("<script>x()</script>", out string why) && why.Length > 0);
        Check("a remote image is caught", !HtmlPage.IsSelfContained("<img src=\"a.png\">", out _));
        Check("a linked stylesheet is caught", !HtmlPage.IsSelfContained("<link rel=stylesheet>", out _));
        Check("any other URL is caught",
            !HtmlPage.IsSelfContained("<a href=\"https://example.com\">x</a>", out _));

        // The things a list of tag names alone would miss, which the class promises to catch.
        Check("an inline event handler is caught",
            !HtmlPage.IsSelfContained("<svg onload=\"x()\"></svg>", out _));
        Check("a javascript: link is caught",
            !HtmlPage.IsSelfContained("<a href=\"javascript:x()\">x</a>", out _));
        Check("a CSS @import is caught", !HtmlPage.IsSelfContained("<style>@import 'a.css';</style>", out _));
        Check("a relative url() in CSS is caught",
            !HtmlPage.IsSelfContained("<style>body{background:url(logo.png)}</style>", out _));
        Check("a base tag is caught, because it re-points every relative URL",
            !HtmlPage.IsSelfContained("<base href=\"/x/\">", out _));
        Check("a protocol-relative URL is caught",
            !HtmlPage.IsSelfContained("<a href=\"//cdn.example.com/a\">x</a>", out _));

        // ...and the things that only look outbound.
        Check("a data URI is self-contained, because it carries its own bytes",
            HtmlPage.IsSelfContained("<style>body{background:url(data:image/png;base64,AAAA)}</style>", out _));
        Check("a fragment reference points inside this same document",
            HtmlPage.IsSelfContained("<svg><rect fill=\"url(#g)\" /></svg>", out _));
        Check("a namespace declaration is an identifier, not a fetch",
            HtmlPage.IsSelfContained(
                "<svg xmlns=\"" + SvgCanvas.Namespace + "\" xmlns:xlink=\"http://www.w3.org/1999/xlink\"></svg>",
                out _));
        Check("a URL that merely begins with the namespace cannot hide behind it",
            !HtmlPage.IsSelfContained("<img src=\"" + SvgCanvas.Namespace + "/../evil.png\">", out _));

        string html = new HtmlPage("A & B").Style("body{color:#000}").Append("<p>body</p>").ToHtml();
        Check("the page is a whole document",
            html.StartsWith("<!DOCTYPE html>", StringComparison.Ordinal) &&
            html.TrimEnd().EndsWith("</html>", StringComparison.Ordinal));
        Check("the title is escaped", html.Contains("<title>A &amp; B</title>", StringComparison.Ordinal));
        Check("the stylesheet is inlined",
            html.Contains("<style>body{color:#000}</style>", StringComparison.Ordinal));
        Check("what it builds passes its own self-contained rule", HtmlPage.IsSelfContained(html, out _));

        Check("text escaping escapes quotes too, because the same helper ends up in an attribute",
            HtmlPage.Escape("say \"hi\"") == "say &quot;hi&quot;");
        Check("attribute escaping does not", HtmlPage.EscapeAttribute("say \"hi\"") == "say &quot;hi&quot;");
    }

    /// <summary>The column a line's first non-space character sits in.</summary>
    private static int Column(string line) => line.Length - line.TrimStart(' ').Length;

    private static bool ThrowsInvalid(Action action)
    {
        try { action(); return false; }
        catch (InvalidOperationException) { return true; }
    }

    private static bool ThrowsRange(Action action)
    {
        try { action(); return false; }
        catch (ArgumentOutOfRangeException) { return true; }
    }
}

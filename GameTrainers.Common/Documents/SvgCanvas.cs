using System.Globalization;
using System.Text;

namespace GameTrainers.Common.Documents;

/// <summary>
/// An attribute value: a string written as-is, or a number written in invariant culture.
///
/// The implicit conversions are what let a call site write <c>("x", 12.5)</c> and <c>("class", "cell")</c>
/// in the same list without either side casting. The number path is the important one — a
/// culture-formatted <c>12,5</c> in an SVG attribute is silently wrong on a machine with a comma
/// decimal separator, and it is the kind of bug that never shows up where it was written.
/// </summary>
public readonly record struct SvgValue(string Text)
{
    public static implicit operator SvgValue(string value) => new(value ?? "");
    public static implicit operator SvgValue(double value) => new(SvgCanvas.Number(value));
    public static implicit operator SvgValue(int value) => new(value.ToString(CultureInfo.InvariantCulture));
}

/// <summary>
/// How a canvas lays its markup out.
/// </summary>
public enum SvgLayout
{
    /// <summary>One line, no whitespace between elements. What a page wants to embed.</summary>
    Compact,

    /// <summary>
    /// One element per line, indented by depth. What a file on disk wants, so it can be read, diffed
    /// and inspected by eye.
    ///
    /// Whitespace is only ever added between elements, never inside one that holds text — in SVG the
    /// whitespace inside a <c>&lt;text&gt;</c> is drawn, so indenting it would move the label.
    /// </summary>
    Indented,
}

/// <summary>
/// Builds an SVG document as text.
///
/// It exists because two trainers were hand-rolling the same thing — the Alternate Reality city plan
/// and The Quest's world plan — and both had grown their own copy of the same three-<c>Replace</c>
/// escape. Between them they demonstrated the mistakes this class is meant to make impossible:
///
/// <list type="bullet">
/// <item><b>Numbers formatted in the current culture.</b> Every number goes through
/// <see cref="Number"/>, which is invariant.</item>
/// <item><b>A child written after a self-closed tag.</b> An element stays open until it is closed and
/// only self-closes if nothing was written inside it, so a <c>&lt;title&gt;</c> put inside a rectangle
/// really is that rectangle's tooltip rather than its next sibling.</item>
/// <item><b>Markup written outside the root.</b> Once <see cref="ToSvg"/> has closed the document,
/// writing more throws rather than appending after <c>&lt;/svg&gt;</c>.</item>
/// <item><b>A duplicate attribute.</b> Two attributes of the same name are a well-formedness error, so
/// a standalone file would fail to open at all; adding one throws here instead.</item>
/// <item><b>Characters XML cannot carry.</b> Names read out of a game file can hold control bytes,
/// which no escape can represent; <see cref="EscapeText"/> drops them.</item>
/// </list>
///
/// Nothing here knows about maps, grids or games: those are layout decisions, and the two callers make
/// them differently. This is the markup, and only the markup.
///
/// No WPF, so a verification harness can assert what it produces without a desktop.
/// </summary>
public sealed class SvgCanvas
{
    /// <summary>The SVG namespace. Not a fetch — it is an identifier, and a document must declare it.</summary>
    public const string Namespace = "http://www.w3.org/2000/svg";

    /// <summary>What one level of nesting is indented by in <see cref="SvgLayout.Indented"/>.</summary>
    private const string IndentStep = "  ";

    /// <summary>One open element: its name and what has been written inside it.</summary>
    private struct Frame(string name)
    {
        public readonly string Name = name;
        public bool HasText;
        public bool HasChildElement;
    }

    private readonly StringBuilder _sb = new();
    private readonly List<Frame> _open = [];
    private readonly HashSet<string> _attributeNames = new(StringComparer.Ordinal);
    private readonly SvgLayout _layout;

    /// <summary>True when an element's tag is written but not yet closed with <c>&gt;</c> or <c>/&gt;</c>.</summary>
    private bool _unterminated;

    /// <summary>True once <see cref="ToSvg"/> has closed the document.</summary>
    private bool _finished;

    /// <summary>
    /// Starts a document <paramref name="width"/> × <paramref name="height"/> user units across,
    /// carrying both a viewBox and matching width and height — the shape a standalone <c>.svg</c> file
    /// wants. Use <see cref="Responsive"/> for one that is going inside a page.
    /// </summary>
    /// <param name="width">Width in user units.</param>
    /// <param name="height">Height in user units.</param>
    /// <param name="ariaLabel">What the picture is, for a reader that cannot see it.</param>
    /// <param name="attributes">Anything else the root should carry.</param>
    public SvgCanvas(double width, double height, string? ariaLabel = null,
                     params (string Name, SvgValue Value)[] attributes)
        : this(width, height, sized: true, SvgLayout.Compact, ariaLabel, attributes)
    {
    }

    private SvgCanvas(double width, double height, bool sized, SvgLayout layout, string? ariaLabel,
                      (string Name, SvgValue Value)[] attributes)
    {
        _layout = layout;

        Open("svg", ("xmlns", Namespace), ("viewBox", $"0 0 {Number(width)} {Number(height)}"));

        if (sized)
        {
            Attribute("width", width);
            Attribute("height", height);
        }
        else
        {
            Attribute("width", "100%");
        }

        if (ariaLabel is { Length: > 0 })
        {
            Attribute("role", "img");
            Attribute("aria-label", ariaLabel);
        }

        foreach (var (name, value) in attributes) Attribute(name, value);

        // The root never self-closes, so terminate its tag now.
        Terminate();
    }

    /// <summary>
    /// A canvas meant to be embedded in a page rather than saved as a file: it takes the width it is
    /// given and gets its height from the viewBox, so it scales with whatever it is dropped into.
    ///
    /// A fixed height beside a percentage width would letterbox the drawing, which is why this is a
    /// separate factory rather than an attribute the caller can bolt on.
    /// </summary>
    public static SvgCanvas Responsive(double width, double height, string? ariaLabel = null,
                                       params (string Name, SvgValue Value)[] attributes) =>
        new(width, height, sized: false, SvgLayout.Compact, ariaLabel, attributes);

    /// <summary>
    /// A canvas for a file on disk: sized, and laid out one element per line so the result can be
    /// read, diffed and inspected rather than being a single very long line.
    /// </summary>
    public static SvgCanvas File(double width, double height, string? ariaLabel = null,
                                 params (string Name, SvgValue Value)[] attributes) =>
        new(width, height, sized: true, SvgLayout.Indented, ariaLabel, attributes);

    /// <summary>How many elements are open, the root included.</summary>
    public int Depth => _open.Count;

    /// <summary>
    /// Adds an attribute to the element whose tag is still open.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The element's tag has already been closed, or it already carries an attribute of that name —
    /// a duplicate is a well-formedness error, so a standalone file would not open at all.
    /// </exception>
    public SvgCanvas Attribute(string name, SvgValue value)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        if (!_unterminated)
            throw new InvalidOperationException($"cannot add '{name}': the element's tag is already closed");
        if (!_attributeNames.Add(name))
            throw new InvalidOperationException($"'{name}' is already on this element; a duplicate attribute is not well-formed");

        _sb.Append(' ').Append(name).Append("=\"").Append(EscapeAttribute(value.Text)).Append('"');
        return this;
    }

    /// <summary>
    /// Opens an element and leaves it open. Close it with <see cref="Close"/>, or let
    /// <see cref="Scope"/> do it.
    /// </summary>
    public SvgCanvas Open(string name, params (string Name, SvgValue Value)[] attributes)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ThrowIfFinished();
        Terminate();
        Break();

        if (_open.Count > 0)
        {
            var parent = _open[^1];
            parent.HasChildElement = true;
            _open[^1] = parent;
        }

        _sb.Append('<').Append(name);
        _open.Add(new Frame(name));
        _attributeNames.Clear();
        _unterminated = true;

        foreach (var (attribute, value) in attributes) Attribute(attribute, value);
        return this;
    }

    /// <summary>
    /// Opens an element and closes it when the returned value is disposed, so nesting cannot be got
    /// wrong by an early <c>return</c> or a <c>continue</c>. The scope closes back to the depth it was
    /// opened at, so a stray <see cref="Close"/> inside it cannot silently shift the nesting.
    /// </summary>
    public SvgScope Scope(string name, params (string Name, SvgValue Value)[] attributes)
    {
        Open(name, attributes);
        return new SvgScope(this, _open.Count);
    }

    /// <summary>
    /// Closes the innermost open element — self-closing it when nothing was written inside, and
    /// writing a closing tag when something was.
    /// </summary>
    public SvgCanvas Close()
    {
        if (_open.Count == 0) throw new InvalidOperationException("there is no open element to close");

        var frame = _open[^1];
        _open.RemoveAt(_open.Count - 1);

        if (_unterminated)
        {
            _sb.Append(" />");
            _unterminated = false;
        }
        else
        {
            // Only break before a closing tag when the element held other elements; one that held
            // text must close tight against it, because that whitespace would be drawn.
            if (frame.HasChildElement && !frame.HasText) Break();
            _sb.Append("</").Append(frame.Name).Append('>');
        }
        return this;
    }

    /// <summary>Closes elements until <see cref="Depth"/> is <paramref name="depth"/>.</summary>
    public SvgCanvas CloseTo(int depth)
    {
        while (_open.Count > depth) Close();
        return this;
    }

    /// <summary>Writes escaped text inside the open element.</summary>
    public SvgCanvas Content(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        ThrowIfFinished();
        Terminate();
        MarkText();
        _sb.Append(EscapeText(text));
        return this;
    }

    /// <summary>Writes markup that has already been escaped — a nested document, or a style block.</summary>
    public SvgCanvas Raw(string markup)
    {
        ArgumentNullException.ThrowIfNull(markup);
        ThrowIfFinished();
        Terminate();
        MarkText();
        _sb.Append(markup);
        return this;
    }

    /// <summary>A <c>&lt;title&gt;</c>, which a viewer shows as the parent element's tooltip.</summary>
    public SvgCanvas Title(string text) => Element("title", text);

    /// <summary>A <c>&lt;style&gt;</c> block. The CSS is written as-is; keep it free of markup.</summary>
    public SvgCanvas Style(string css) => Open("style").Raw(css).Close();

    /// <summary>An element holding nothing but text.</summary>
    public SvgCanvas Element(string name, string text, params (string Name, SvgValue Value)[] attributes) =>
        Open(name, attributes).Content(text).Close();

    /// <summary>An element holding nothing at all, which therefore self-closes.</summary>
    public SvgCanvas Element(string name, params (string Name, SvgValue Value)[] attributes) =>
        Open(name, attributes).Close();

    /// <summary>A rectangle. Self-closing unless something is written into it first.</summary>
    public SvgCanvas Rect(double x, double y, double width, double height,
                          params (string Name, SvgValue Value)[] attributes) =>
        Element("rect", [("x", x), ("y", y), ("width", width), ("height", height), .. attributes]);

    /// <summary>A rectangle with a tooltip.</summary>
    public SvgCanvas Rect(double x, double y, double width, double height, string title,
                          params (string Name, SvgValue Value)[] attributes)
    {
        Open("rect", [("x", x), ("y", y), ("width", width), ("height", height), .. attributes]);
        Title(title);
        return Close();
    }

    /// <summary>A line.</summary>
    public SvgCanvas Line(double x1, double y1, double x2, double y2,
                          params (string Name, SvgValue Value)[] attributes) =>
        Element("line", [("x1", x1), ("y1", y1), ("x2", x2), ("y2", y2), .. attributes]);

    /// <summary>A circle.</summary>
    public SvgCanvas Circle(double cx, double cy, double radius,
                            params (string Name, SvgValue Value)[] attributes) =>
        Element("circle", [("cx", cx), ("cy", cy), ("r", radius), .. attributes]);

    /// <summary>A run of text at a point.</summary>
    public SvgCanvas Text(double x, double y, string text,
                          params (string Name, SvgValue Value)[] attributes) =>
        Element("text", text, [("x", x), ("y", y), .. attributes]);

    /// <summary>
    /// The document, with every element still open closed off. Calling it again returns the same
    /// string; writing to the canvas after it throws, rather than appending after <c>&lt;/svg&gt;</c>.
    /// </summary>
    public string ToSvg()
    {
        if (!_finished)
        {
            while (_open.Count > 0) Close();
            if (_layout == SvgLayout.Indented) _sb.Append('\n');
            _finished = true;
        }
        return _sb.ToString();
    }

    /// <summary>
    /// The markup so far, <b>without</b> finishing the document.
    ///
    /// Deliberately not <see cref="ToSvg"/>: a debugger watch, a log line or an interpolated string
    /// stringifies an object at arbitrary moments, and finishing the document there would silently
    /// terminate a half-built canvas — and then throw out of a <see cref="SvgScope"/>'s dispose.
    /// </summary>
    public override string ToString() => _sb.ToString();

    /// <summary>A number as SVG wants it: invariant culture, at most two decimals, no exponent.</summary>
    public static string Number(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>
    /// Escapes a text node.
    ///
    /// Quotes are left alone deliberately: they are legal in element content, and escaping them turns
    /// readable prose into <c>&amp;quot;</c> soup in every place name. Characters XML 1.0 cannot carry
    /// at all — most of C0, which game strings really do contain — are dropped, because no escape can
    /// represent them and a single one makes a standalone file unopenable.
    /// </summary>
    public static string EscapeText(string value)
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
                default:
                    if (IsLegalXml(c)) sb.Append(c);
                    break;
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// Escapes an attribute value. Double quotes must go, because every attribute this class writes is
    /// double-quoted; single quotes need not, and are left readable.
    /// </summary>
    public static string EscapeAttribute(string value) => EscapeText(value).Replace("\"", "&quot;");

    /// <summary>Whether XML 1.0 can carry <paramref name="c"/> at all, escaped or not.</summary>
    private static bool IsLegalXml(char c) =>
        c is '\t' or '\n' or '\r' || (c >= ' ' && c != '\uFFFE' && c != '\uFFFF');

    /// <summary>Terminates a still-open tag with <c>&gt;</c>, so something can be written inside it.</summary>
    private void Terminate()
    {
        if (!_unterminated) return;
        _sb.Append('>');
        _unterminated = false;
    }

    /// <summary>Records that the open element now holds text, so nothing may be indented inside it.</summary>
    private void MarkText()
    {
        if (_open.Count == 0) return;
        var frame = _open[^1];
        frame.HasText = true;
        _open[^1] = frame;
    }

    /// <summary>
    /// A newline and an indent, when the layout asks for one and it would be safe.
    ///
    /// Called from <see cref="Open"/> before the new element is pushed and from <see cref="Close"/>
    /// after the closed one is popped, so in both cases <c>_open.Count</c> is that element's own depth.
    /// Never breaks inside an element that holds text: in SVG that whitespace is drawn.
    /// </summary>
    private void Break()
    {
        if (_layout != SvgLayout.Indented || _sb.Length == 0) return;
        if (_open.Count > 0 && _open[^1].HasText) return;

        _sb.Append('\n');
        for (int i = 0; i < _open.Count; i++) _sb.Append(IndentStep);
    }

    private void ThrowIfFinished()
    {
        if (_finished) throw new InvalidOperationException("the document is finished; nothing may be written after ToSvg()");
    }
}

/// <summary>
/// Closes the element it was opened for. See <see cref="SvgCanvas.Scope"/>.
///
/// It closes back to the depth it was created at rather than closing exactly one element, so an
/// unclosed <c>Open</c> inside the scope is tidied up and a stray <c>Close</c> cannot make the scope
/// close somebody else's element.
/// </summary>
public readonly struct SvgScope(SvgCanvas canvas, int depth) : IDisposable
{
    private readonly SvgCanvas _canvas = canvas;
    private readonly int _depth = depth;

    /// <summary>Closes back to the depth this scope was opened at.</summary>
    public void Dispose() => _canvas?.CloseTo(_depth - 1);
}

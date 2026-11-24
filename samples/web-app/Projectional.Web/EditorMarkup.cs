using System.Globalization;
using System.Text;

using Projectional.Editing;
using Projectional.Language;
using Projectional.Projection;

namespace Projectional.Web;

/// <summary>Turns one painted frame into the HTML the page shows.</summary>
/// <remarks>
/// <para>
/// A frame is text, styles, highlights and a caret in layout coordinates - exactly what a painter needs, and what
/// a browser can be handed as absolutely positioned spans. Nothing here re-derives a position: every rectangle
/// comes from the layout the session already built, so the page and the caret cannot disagree about where a cell
/// is. That is the whole reason the snapshot renderer and the desktop control can share this layer.
/// </para>
/// <para>
/// The output is a string rather than a component tree on purpose. A document is hundreds of spans that change
/// wholesale after every keystroke, so a diff of component instances would cost more than replacing one
/// <c>innerHTML</c>; the interactive parts - the completion list, the menus, the panels - stay ordinary Razor and
/// are the only things Blazor diffs.
/// </para>
/// </remarks>
public static class EditorMarkup
{
    /// <summary>The canvas for <paramref name="frame"/>, sized from <paramref name="extent"/>.</summary>
    /// <param name="frame">The frame to paint.</param>
    /// <param name="extent">How much room the document takes, in layout units.</param>
    /// <param name="metrics">How one layout unit becomes a CSS pixel.</param>
    /// <exception cref="ArgumentNullException"><paramref name="frame"/> is <see langword="null"/>.</exception>
    public static string Canvas(EditorFrame frame, LayoutSize extent, CellMetrics metrics)
    {
        ArgumentNullException.ThrowIfNull(frame);

        var width = metrics.X(Math.Max(extent.Width, 1) + 1);
        var height = metrics.Y(Math.Max(extent.Height, 1));
        var html = new StringBuilder(1024);

        html.Append("<div class=\"pn-canvas\" style=\"width:").Append(Px(width))
            .Append(";height:").Append(Px(height)).Append("\">");

        foreach (var highlight in frame.Highlights)
        {
            html.Append("<div class=\"pn-selection\" style=\"")
                .Append(Box(highlight, metrics))
                .Append("\"></div>");
        }

        foreach (var run in frame.TextRuns)
        {
            html.Append("<span class=\"pn-run ").Append(ClassOf(run.Style)).Append(EmphasisOf(run.Emphasis))
                .Append("\" style=\"left:").Append(Px(metrics.X(run.Bounds.X)))
                .Append(";top:").Append(Px(metrics.Y(run.Bounds.Y)))
                .Append("\">").Append(Escape(Text(run.Text)))
                .Append("</span>");
        }

        if (frame.CaretRect is { } caret)
        {
            html.Append("<div class=\"pn-caret\" data-role=\"caret\" style=\"left:")
                .Append(Px(metrics.X(caret.X)))
                .Append(";top:").Append(Px(metrics.Y(caret.Y)))
                .Append(";height:").Append(Px(metrics.Y(Math.Max(caret.Height, 1))))
                .Append("\"></div>");
        }

        html.Append("</div>");

        return html.ToString();
    }

    /// <summary>The canvas for <paramref name="document"/>'s current frame.</summary>
    /// <param name="document">The document to paint.</param>
    /// <param name="metrics">How one layout unit becomes a CSS pixel.</param>
    public static string Canvas(EditorDocument document, CellMetrics metrics)
    {
        ArgumentNullException.ThrowIfNull(document);

        return Canvas(document.Frame(), document.Extent, metrics);
    }

    /// <summary>Escapes text that comes from a model, which is free text a user typed.</summary>
    /// <param name="text">The text to escape.</param>
    public static string Escape(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var escaped = new StringBuilder(text.Length);

        foreach (var character in text)
        {
            escaped.Append(character switch
            {
                '&' => "&amp;",
                '<' => "&lt;",
                '>' => "&gt;",
                '"' => "&quot;",
                '\'' => "&#39;",
                _ => character.ToString(),
            });
        }

        return escaped.ToString();
    }

    /// <summary>The CSS class that says what a run means.</summary>
    /// <param name="kind">What the run's text means.</param>
    public static string ClassOf(TextStyleKind kind) => kind switch
    {
        TextStyleKind.Keyword => "pn-keyword",
        TextStyleKind.Punctuation => "pn-punctuation",
        TextStyleKind.Identifier => "pn-identifier",
        TextStyleKind.Number => "pn-number",
        TextStyleKind.Text => "pn-text",
        TextStyleKind.Reference => "pn-reference",
        TextStyleKind.Unresolved => "pn-unresolved",
        TextStyleKind.Placeholder => "pn-placeholder",
        TextStyleKind.Missing => "pn-missing",
        TextStyleKind.Annotation => "pn-annotation",
        _ => "pn-default",
    };

    /// <summary>
    /// The emphasis classes a run carries, or an empty string.
    /// </summary>
    /// <remarks>
    /// Bold and italic are deliberately not painted. The monospace measure that produced the layout treats them as
    /// the same advance, so a browser face that is wider would push the glyphs out of the rectangles the caret and
    /// the hit test were computed from - and the two decorations a language uses here, underline and strikethrough,
    /// change no metric at all.
    /// </remarks>
    private static string EmphasisOf(TextEmphasis emphasis)
    {
        var classes = new StringBuilder();

        if ((emphasis & TextEmphasis.Underline) != 0)
        {
            classes.Append(" pn-underline");
        }

        if ((emphasis & TextEmphasis.Strikethrough) != 0)
        {
            classes.Append(" pn-strike");
        }

        return classes.ToString();
    }

    /// <summary>The text of one run on one line, with nothing that a single line cannot hold.</summary>
    /// <remarks>
    /// A tab would be measured as one column by the monospace measure and painted as a variable gap by the browser;
    /// a newline belongs to a line break the layout already made. Both become one space, which is the width the
    /// layout was told they have.
    /// </remarks>
    private static string Text(string text) => text.Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');

    private static string Box(LayoutRect rect, CellMetrics metrics) =>
        $"left:{Px(metrics.X(rect.X))};top:{Px(metrics.Y(rect.Y))};width:{Px(metrics.X(rect.Width))};height:{Px(metrics.Y(rect.Height))}";

    private static string Px(double value) =>
        value.ToString("0.###", CultureInfo.InvariantCulture) + "px";
}

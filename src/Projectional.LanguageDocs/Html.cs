using System.Globalization;
using System.Text;

namespace Projectional.LanguageDocs;

/// <summary>The little of HTML this generator needs, in one place so every page is built the same way.</summary>
/// <remarks>
/// A builder rather than a template engine: the output is derived from a model, it has to be byte-stable, and the pages
/// are simple enough that a template language would be a second thing to learn. Every value that comes from a model
/// goes through <see cref="Escape"/>, because a concept's description is free text a user typed.
/// </remarks>
internal static class Html
{
    /// <summary>A whole page: the header with its search, the navigation tree, and the content.</summary>
    /// <param name="chain">The languages the documentation covers, which the tree and the brand come from.</param>
    /// <param name="title">What the browser tab says and what the page's <c>h1</c> says.</param>
    /// <param name="path">The page's own path in the tree, which every link and the tree's cursor are derived from.</param>
    /// <param name="breadcrumb">The links above the title, already rendered, or empty for the root page.</param>
    /// <param name="body">The page's sections, already rendered.</param>
    internal static string Page(DocumentationChain chain, string title, string path, string breadcrumb, string body)
    {
        ArgumentNullException.ThrowIfNull(chain);

        var depth = Depth(path);
        var page = new StringBuilder();

        page.Append("<!DOCTYPE html>\n");
        page.Append("<html lang=\"en\">\n<head>\n");
        page.Append("<meta charset=\"utf-8\">\n");
        page.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\n");
        page.Append("<title>").Append(Escape(title)).Append("</title>\n");
        page.Append("<link rel=\"stylesheet\" href=\"").Append(DocumentationChain.AssetLink(depth, "docs.css")).Append("\">\n");
        page.Append("<script src=\"").Append(DocumentationChain.AssetLink(depth, "docs.js")).Append("\" defer></script>\n");
        page.Append("</head>\n<body>\n");

        page.Append("<header class=\"top\">\n");
        page.Append("<button class=\"menu\" type=\"button\" aria-label=\"Show navigation\" aria-controls=\"nav\" aria-expanded=\"false\">")
            .Append(Icon())
            .Append("</button>\n");
        page.Append("<a class=\"brand\" href=\"").Append(DocumentationChain.HomeLink(depth)).Append("\">")
            .Append(Escape(chain.Title))
            .Append("</a>\n");

        if (chain.EditorHref is { Length: > 0 } editorHref)
        {
            page.Append("<a class=\"editor-link\" href=\"").Append(DocumentationChain.EditorLink(depth, editorHref)).Append("\">Open the editor</a>\n");
        }
        page.Append("<input type=\"search\" id=\"find\" placeholder=\"Search concepts\" aria-label=\"Search concepts\" autocomplete=\"off\">\n");
        page.Append("</header>\n");

        page.Append("<div class=\"shell\">\n");
        page.Append(SiteNav.Render(chain, depth, path));
        page.Append("<main>\n");

        if (breadcrumb.Length > 0)
        {
            page.Append("<nav class=\"breadcrumb\">").Append(breadcrumb).Append("</nav>\n");
        }

        page.Append("<h1>").Append(Escape(title)).Append("</h1>\n");
        page.Append(body);
        page.Append("</main>\n</div>\n");
        page.Append("<div class=\"backdrop\" hidden></div>\n");
        page.Append("</body>\n</html>\n");

        return page.ToString();
    }

    /// <summary>How many folders deep a page sits, which every relative link is built from.</summary>
    internal static int Depth(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        var depth = 0;

        foreach (var character in path)
        {
            if (character == '/')
            {
                depth++;
            }
        }

        return depth;
    }

    /// <summary>A link, or plain text when there is nothing to link to.</summary>
    internal static string Link(string? href, string text) =>
        href is null ? $"<span class=\"unresolved\">{Escape(text)}</span>" : $"<a href=\"{Escape(href)}\">{Escape(text)}</a>";

    /// <summary>A table whose first row is its header, or a sentence saying there is nothing to show.</summary>
    /// <remarks>
    /// Scrolled sideways rather than squeezed, because a slot table has six columns and half of them are keys: on a phone
    /// a wrapped table is unreadable, while a scrolling one keeps every row a row.
    /// </remarks>
    internal static string Table(IReadOnlyList<string> columns, IEnumerable<IReadOnlyList<string>> rows, string empty)
    {
        var body = new StringBuilder();
        var any = false;

        foreach (var row in rows)
        {
            any = true;
            body.Append("<tr>");

            foreach (var cell in row)
            {
                body.Append("<td>").Append(cell).Append("</td>");
            }

            body.Append("</tr>\n");
        }

        if (!any)
        {
            return $"<p class=\"empty\">{Escape(empty)}</p>\n";
        }

        var table = new StringBuilder();

        table.Append("<div class=\"tw\">\n<table>\n<thead><tr>");

        foreach (var column in columns)
        {
            table.Append("<th>").Append(Escape(column)).Append("</th>");
        }

        table.Append("</tr></thead>\n<tbody>\n").Append(body).Append("</tbody>\n</table>\n</div>\n");

        return table.ToString();
    }

    /// <summary>A section with a heading.</summary>
    internal static string Section(string heading, string content) =>
        $"<section>\n<h2>{Escape(heading)}</h2>\n{content}</section>\n";

    /// <summary>Text a page shows as it was written, with nothing interpreted.</summary>
    internal static string Escape(string? text)
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

    /// <summary>A sentence, escaped. Use <see cref="MarkupParagraph"/> when the text already holds markup.</summary>
    internal static string Paragraph(string text) => $"<p>{Escape(text)}</p>\n";

    /// <summary>A paragraph of HTML this generator built, which is not escaped again.</summary>
    internal static string MarkupParagraph(string html) => $"<p>{html}</p>\n";

    /// <summary>A count with its noun: <c>1 concept</c>, <c>6 concepts</c>.</summary>
    internal static string Count(int value, string singular) =>
        value == 1
            ? $"1 {singular}"
            : $"{value.ToString(CultureInfo.InvariantCulture)} {singular}s";

    /// <summary>The navigation toggle, drawn as three lines so it needs no icon font.</summary>
    private static string Icon() =>
        "<svg viewBox=\"0 0 16 16\" width=\"16\" height=\"16\" aria-hidden=\"true\" focusable=\"false\">"
        + "<path d=\"M2 4h12M2 8h12M2 12h12\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.6\" stroke-linecap=\"round\"/>"
        + "</svg>";
}
using System.Text;

using Projectional.Languages;

namespace Projectional.LanguageDocs;

/// <summary>The documents a language ships, drawn: what the language looks like rather than what its concepts are.</summary>
/// <remarks>
/// A concept page answers "what does one of these look like", and this page answers "what does a document of this
/// language look like" - the notation in use, with real names in the slots and its indentation as the editor draws it.
/// Both are text, so the page stays diffable and needs no image to be rendered on the way.
/// </remarks>
internal static class ExamplesPage
{
    /// <summary>Renders <c>&lt;Language&gt;/examples.html</c>.</summary>
    internal static string Render(DocumentationChain chain, LanguageProject language, string path)
    {
        ArgumentNullException.ThrowIfNull(chain);
        ArgumentNullException.ThrowIfNull(language);

        var depth = Html.Depth(path);
        var drawings = chain.Examples(language);
        var body = new StringBuilder();

        body.Append(Html.Paragraph(drawings.Count == 0
            ? $"{language.Name} ships no document of its own, so there is nothing here to draw."
            : $"{Html.Count(drawings.Count, "document")} {language.Name} ships, drawn and coloured the way the editor draws it."));

        foreach (var drawing in drawings)
        {
            body.Append(Html.Section(drawing.Name, drawing.Refusal is { Length: > 0 } refusal
                ? Html.Paragraph($"It could not be drawn: {refusal}")
                : $"<pre class=\"example\">{drawing.Html}</pre>\n"));
        }

        var breadcrumb =
            $"{Html.Link(DocumentationChain.HomeLink(depth), chain.Title)} / "
            + $"{Html.Link(DocumentationChain.Link(depth, language, "index.html"), language.Name)} / examples";

        return Html.Page(chain, $"{language.Name} - examples", path, breadcrumb, body.ToString());
    }
}
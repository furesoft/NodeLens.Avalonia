using System.Globalization;
using System.Text;

using Projectional.Core;
using Projectional.Languages;

namespace Projectional.LanguageDocs;

/// <summary>The tree beside every page: a language opens into its folders, and a folder into what it holds.</summary>
/// <remarks>
/// <para>
/// The shape is the project explorer's rather than a flat list of links, because that is how a reader knows a language:
/// one language at a time, its parts under it, and the thing being read visible where it sits. Native
/// <c>details</c>/<c>summary</c> does the folding, so the tree works with the script blocked or not loaded at all - the
/// script only adds the filter and the mobile drawer.
/// </para>
/// <para>
/// The folder the current page lives in starts open, and so does its language, so arriving on a concept page shows the
/// reader where they are instead of a tree with everything shut.
/// </para>
/// </remarks>
internal static class SiteNav
{
    /// <summary>Renders the tree for a page at <paramref name="depth"/> that is <paramref name="path"/>.</summary>
    internal static string Render(DocumentationChain chain, int depth, string path)
    {
        ArgumentNullException.ThrowIfNull(chain);

        var nav = new StringBuilder();

        nav.Append("<nav class=\"tree\" id=\"nav\" aria-label=\"Documentation\">\n");

        foreach (var language in chain.Languages)
        {
            var folder = DocumentationChain.Folder(language);
            var concepts = DocumentationChain.Own(language);

            nav.Append("<details class=\"language\"").Append(Open(path, $"{folder}/") ? " open" : string.Empty).Append(">\n");
            nav.Append("<summary>").Append(Html.Escape(language.Name)).Append("</summary>\n");

            Item(nav, depth, language, "index.html", "Overview", path);
            Item(nav, depth, language, "aspects.html", "Aspects", path);

            nav.Append("<details class=\"folder\"").Append(Open(path, $"{folder}/concepts/") ? " open" : string.Empty).Append(">\n");
            nav.Append("<summary>Concepts <span class=\"count\">")
                .Append(concepts.Count.ToString(CultureInfo.InvariantCulture))
                .Append("</span></summary>\n");

            foreach (var concept in concepts)
            {
                Item(nav, depth, language, $"concepts/{concept.Key}.html", concept.Key, path);
            }

            nav.Append("</details>\n");
            nav.Append("</details>\n");
        }

        nav.Append("</nav>\n");

        return nav.ToString();
    }

    /// <summary>Whether the current path sits under <paramref name="folder"/>, which is what opens a level.</summary>
    private static bool Open(string path, string folder) =>
        path.Equals(folder, StringComparison.Ordinal) || path.StartsWith(folder, StringComparison.Ordinal);

    private static void Item(
        StringBuilder nav,
        int depth,
        LanguageProject language,
        string file,
        string text,
        string path)
    {
        var folder = DocumentationChain.Folder(language);
        var current = string.Equals(path, $"{folder}/{file}", StringComparison.Ordinal);

        // The filter reads the link's own text, so what a reader searches is exactly what the item says - a second
        // attribute carrying the same word is a copy that can drift from the visible one.
        nav.Append("<a class=\"item")
            .Append(current ? " current" : string.Empty)
            .Append("\" href=\"")
            .Append(DocumentationChain.Link(depth, language, file))
            .Append('"')
            .Append(current ? " aria-current=\"page\"" : string.Empty)
            .Append('>')
            .Append(Html.Escape(text))
            .Append("</a>\n");
    }
}
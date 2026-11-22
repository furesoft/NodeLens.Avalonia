using System.Globalization;
using System.Text;

using Projectional.Languages;

namespace Projectional.LanguageDocs;

/// <summary>The page at the top of the tree: what this documentation covers, and a search over all of it.</summary>
/// <remarks>
/// It is the set's page rather than one language's, because that is what a reader arrives at: the language they asked
/// about, the languages it borrowed from, and one box that searches all of them. A search limited to one language would
/// fail exactly the reader who typed <c>Quantity</c> while reading about stops.
/// <para>
/// What a single language declares it stands on is not here but on that language's own page, so the same table serves a
/// one-language export and a site of a dozen without a second rendering that could disagree with the first.
/// </para>
/// </remarks>
internal static class RootPage
{
    /// <summary>Renders <c>index.html</c>.</summary>
    /// <param name="chain">The languages the documentation covers.</param>
    /// <param name="primary">The language the reader asked about, or <see langword="null"/> for a whole site.</param>
    internal static string Render(DocumentationChain chain, LanguageProject? primary)
    {
        ArgumentNullException.ThrowIfNull(chain);

        const string path = "index.html";

        var body = new StringBuilder();

        if (primary?.Description is { Length: > 0 } description)
        {
            body.Append(Html.Paragraph(description));
        }

        body.Append(Html.Paragraph(Summary(chain, primary)));
        body.Append(Html.Section("Languages", Languages(chain, primary)));

        // No search section: the box is in the header of every page, and a second one here would filter a table that
        // the tree already is. No breadcrumb either - this page is the top of the tree.
        return Html.Page(chain, chain.Title, path, breadcrumb: string.Empty, body.ToString());
    }

    private static string Summary(DocumentationChain chain, LanguageProject? primary)
    {
        var count = chain.Languages.Count;

        if (primary is null)
        {
            return count == 1
                ? "One language, with the language it stands on if it stands on one."
                : $"{count.ToString(CultureInfo.InvariantCulture)} languages, each documented once, together with every vocabulary they borrow from.";
        }

        var version = primary.Version is { Length: > 0 } declared ? $" {declared}" : string.Empty;
        var standsOn = count - 1;

        return standsOn == 0
            ? $"{primary.Name}{version} stands on nothing: every page here belongs to it."
            : $"{primary.Name}{version} stands on {standsOn} other language{(standsOn == 1 ? string.Empty : "s")}, whose documentation is included.";
    }

    private static string Languages(DocumentationChain chain, LanguageProject? primary)
    {
        var rows = chain.Languages.Select(each => new List<string>
        {
            Html.Link(DocumentationChain.Link(0, each, "index.html"), each.Name)
                + (ReferenceEquals(each, primary) ? " <span class=\"muted\">(this documentation)</span>" : string.Empty),
            Html.Escape(each.Version ?? "-"),
            DocumentationChain.Own(each).Count.ToString(CultureInfo.InvariantCulture),
            StandsOn(chain, each),
        });


        return Html.Table(["Language", "Version", "Concepts", "Stands on"], rows, "No language, which cannot happen.");
    }

    /// <summary>The languages a language declares it stands on, each a link when the set documents it.</summary>
    private static string StandsOn(DocumentationChain chain, LanguageProject language)
    {
        var names = language.Dependencies
            .Select(dependency => dependency.Name)
            .Concat(language.Extends.Select(extended => extended.Name))
            .ToArray();

        if (names.Length == 0)
        {
            return "<span class=\"muted\">nothing</span>";
        }

        return string.Join(", ", names.Select(name =>
            chain.Languages.FirstOrDefault(each => string.Equals(each.Name, name, StringComparison.Ordinal)) is { } documented
                ? Html.Link(DocumentationChain.Link(0, documented, "index.html"), name)
                : $"<span class=\"unresolved\">{Html.Escape(name)}</span>"));
    }
}

using System.Globalization;
using System.Text;

using Projectional.Core;
using Projectional.Languages;

namespace Projectional.LanguageDocs;

/// <summary>One language's page: what it stands on, and its concepts grouped by the concept they inherit from.</summary>
/// <remarks>
/// Grouped by inheritance root rather than listed alphabetically, because that is how a reader learns a language: the
/// roots are the few things a document can be, and everything else is a kind of one of them.
/// </remarks>
internal static class LanguageIndexPage
{
    /// <summary>Renders <c>&lt;Language&gt;/index.html</c>.</summary>
    internal static string Render(DocumentationChain chain, LanguageProject language, string path)
    {
        ArgumentNullException.ThrowIfNull(chain);
        ArgumentNullException.ThrowIfNull(language);

        var depth = Html.Depth(path);

        var body = new StringBuilder();

        if (language.Description is { Length: > 0 } description)
        {
            body.Append(Html.Paragraph(description));
        }

        if (language.Dependencies.Count > 0 || language.Extends.Count > 0)
        {
            body.Append(Html.Section("What it says it stands on", Dependencies(chain, language, depth)));
        }

        foreach (var group in Groups(language))
        {
            body.Append(Html.Section(group.Key, Concepts(language, group, depth)));
        }

        // A paragraph that carries a link, so it is built as markup: escaping it would print the anchor as text.
        body.Append(Html.Section("Aspects", Html.MarkupParagraph(
            $"{Html.Count(DocumentationChain.Own(language).Count, "concept")}, "
            + $"{Html.Count(language.Notations.Notations.Count, "notation")}, "
            + $"{Html.Count(language.Types.Count, "type rule")}. "
            + $"What every aspect lowered into is on {Html.Link(DocumentationChain.Link(depth, language, "aspects.html"), "the aspects page")}.")));

        var breadcrumb = $"{Html.Link(DocumentationChain.HomeLink(depth), chain.Title)} / {Html.Escape(language.Name)}";

        return Html.Page(chain, language.Name, path, breadcrumb, body.ToString());
    }

    /// <summary>The declared dependencies, what the host answered, and whether the two versions agree.</summary>
    /// <remarks>
    /// There is deliberately no "declared but unresolved" row: a language whose dependency the host cannot answer is
    /// refused by the loader, so a <see cref="LanguageProject"/> that exists has had every dependency answered, and a
    /// row saying otherwise would be a branch nothing can reach. What <em>can</em> happen, and used to happen silently,
    /// is a version the host answered that is not the version asked for - a resolver is asked by name only. That row is
    /// marked here, because a reader who is told 2.0.0 backs a 1.0.0 language is being told something the language
    /// author did not write.
    /// </remarks>
    private static string Dependencies(DocumentationChain chain, LanguageProject language, int depth)
    {
        IEnumerable<IReadOnlyList<string>> Rows()
        {
            foreach (var dependency in language.Dependencies)
            {
                yield return Row(dependency.Name, dependency.Version, "depends on");
            }

            foreach (var extended in language.Extends)
            {
                yield return Row(extended.Name, extended.Version, "extends");
            }

            IReadOnlyList<string> Row(string name, string? asked, string relation)
            {
                // The loader refuses a language whose dependency nothing answered, so a declared name always has an
                // answer here; the lookup is by name because that is how a resolver is asked.
                var resolved = chain.Languages.First(each => string.Equals(each.Name, name, StringComparison.Ordinal));
                var answered = resolved.Version ?? "-";

                if (asked is { Length: > 0 } askedVersion && !string.Equals(askedVersion, answered, StringComparison.Ordinal))
                {
                    answered = $"<span class=\"mismatch\">{Html.Escape(answered)} &ndash; not the version asked for</span>";
                }

                return
                [
                    Html.Escape(name),
                    Html.Escape(relation),
                    Html.Escape(asked ?? "-"),
                    answered,
                    Html.Link(DocumentationChain.Link(depth, resolved, "index.html"), resolved.Name),
                ];
            }
        }

        return Html.Table(["Name", "Relation", "Asked for", "Answered", "Documented as"], Rows(), "It stands on nothing.");
    }

    /// <summary>The concepts grouped by their inheritance root, the roots in key order.</summary>
    private static IReadOnlyList<IGrouping<string, Concept>> Groups(LanguageProject language) =>
    [
        .. DocumentationChain.Own(language)
            .GroupBy(RootOf)
            .OrderBy(group => group.Key, StringComparer.Ordinal),
    ];

    /// <summary>The top of a concept's inheritance chain, which is the concept a reader starts from.</summary>
    private static string RootOf(Concept concept)
    {
        var current = concept;

        while (current.SuperConcepts.FirstOrDefault(super => !IsFramework(super.Key)) is { } next)
        {
            current = next;
        }

        return current.Key;
    }

    private static bool IsFramework(string key) =>
        key is "Framework.BaseConcept" or "Framework.Named";

    private static string Concepts(LanguageProject language, IGrouping<string, Concept> group, int depth)
    {
        var rows = group
            .OrderBy(concept => concept.Key, StringComparer.Ordinal)
            .Select(concept => new List<string>
            {
                Html.Link(DocumentationChain.Link(depth, language, $"concepts/{concept.Key}.html"), concept.Key),
                Kind(concept),
                concept.NameSlot?.Name is { } name ? $"<code>{Html.Escape(name)}</code>" : "<span class=\"muted\">none</span>",
                Html.Escape(concept.Description ?? string.Empty),
            });

        return Html.Table(["Concept", "Kind", "Named by", "Description"], rows, "Nothing inherits from this concept.");
    }

    /// <summary>How a concept is declared, in the words the structure language uses.</summary>
    private static string Kind(Concept concept) => concept switch
    {
        { IsInterface: true } => "interface",
        { IsAbstract: true } => "abstract",
        { IsRoot: true } => "root",
        _ => "concrete",
    };
}
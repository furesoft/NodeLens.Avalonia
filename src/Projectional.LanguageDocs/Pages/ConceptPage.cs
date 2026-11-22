using System.Globalization;
using System.Text;

using Projectional.Core;
using Projectional.Language;
using Projectional.Languages;

namespace Projectional.LanguageDocs;

/// <summary>One concept's page: what it is, what it inherits from, and every slot it holds.</summary>
/// <remarks>
/// Every slot is a row with its kind, its key, what it holds or points at, and how often - the four facts a language
/// author reads a concept for. Links to other concepts go through the chain, so a slot over a base language's concept
/// is a link into that language's pages rather than a dead key.
/// </remarks>
internal static class ConceptPage
{
    /// <summary>Renders <c>&lt;Language&gt;/concepts/&lt;key&gt;.html</c>.</summary>
    internal static string Render(DocumentationChain chain, LanguageProject language, Concept concept, string path)
    {
        ArgumentNullException.ThrowIfNull(chain);
        ArgumentNullException.ThrowIfNull(language);
        ArgumentNullException.ThrowIfNull(concept);

        var depth = Html.Depth(path);

        var body = new StringBuilder();

        if (concept.Description is { Length: > 0 } description)
        {
            body.Append(Html.Paragraph(description));
        }

        body.Append(Html.Section("Declaration", Declaration(language, concept)));
        body.Append(Html.Section("Inheritance", Inheritance(chain, concept, depth)));

        if (language.Notations.TryGet(concept.Key, out var notation))
        {
            body.Append(Html.Section("How it is drawn", Drawn(notation)));
        }

        body.Append(Html.Section("Slots", Slots(chain, concept, depth)));

        // The home crumb carries the tree's own name, so a reader who came in from a search engine knows what they are
        // inside and has one click back to the list of languages.
        var breadcrumb =
            $"{Html.Link(DocumentationChain.HomeLink(depth), chain.Title)} / "
            + $"{Html.Link(DocumentationChain.Link(depth, language, "index.html"), language.Name)} / "
            + Html.Escape(concept.Key);

        return Html.Page(chain, concept.Key, path, breadcrumb, body.ToString());
    }

    private static string Declaration(LanguageProject language, Concept concept)
    {
        var rows = new List<IReadOnlyList<string>>
        {
            Row("kind", Kind(concept)),
            Row("alias in completion", concept.Alias is { Length: > 0 } alias ? $"<code>{Html.Escape(alias)}</code>" : "none"),
            Row("offered in completion", concept.IsOfferedInCompletion ? "yes" : "no"),
            Row("name slot", concept.NameSlot?.Name is { } name ? $"<code>{Html.Escape(name)}</code>" : "none"),
            Row("name rule", concept.NameRule?.Text ?? "none"),
            Row("notation", language.Notations.TryGet(concept.Key, out _) ? "a notation is registered for this concept" : "no notation of its own"),
            Row("usable in", concept.Applicability.IsAnywhere
                ? "any slot its type allows"
                : Html.Escape(string.Join(", ", concept.Applicability.Places.Select(place => place.ToString())))),
            Row("collapses to", concept.CollapseSlot?.Name is { } collapse ? $"<code>{Html.Escape(collapse)}</code>" : "nothing, so it never folds"),
            Row("type", language.Types.Rules.TryGetValue(concept.Key, out _) ? "a type rule is declared for this concept" : "no type rule of its own"),
        };

        return Html.Table(["Fact", "Value"], rows, "Nothing is declared.");
    }

    private static IReadOnlyList<string> Row(string fact, string value) => [Html.Escape(fact), value];

    /// <summary>The notation written out as text, with the sentence that says what the placeholders mean.</summary>
    private static string Drawn(Notation notation) =>
        $"<pre class=\"notation\">{Html.Escape(NotationSpelling.Of(notation))}</pre>\n"
        + Html.Paragraph("A slot is written as its name, so <name> holds one child or value and <name...> holds a list; further indentation is another level. A slot draws in its place whatever the child itself draws, so punctuation the child owns - the semicolon a statement ends with - belongs to that statement and is not repeated here. This is the shape a document of the concept takes, not a document.");

    private static string Inheritance(DocumentationChain chain, Concept concept, int depth)
    {
        var supers = concept.Extensions
            .Select(each => new List<string> { "extends", chain.ConceptReference(depth, each.Key) })
            .Concat(concept.Interfaces.Select(each => new List<string> { "implements", chain.ConceptReference(depth, each.Key) }));

        // The other direction, because a reader lands on a base concept with the opposite question: `CSharp.Member`
        // is where "what may a class hold" is answered, and the answer is the five concepts that extend it. A page that
        // only looked up would make the reader open every concept of the language to find them.
        var derived = Related(chain, concept, extended: true)
            .Select(each => new List<string> { "extended by", chain.ConceptReference(depth, each.Key) })
            .Concat(Related(chain, concept, extended: false)
                .Select(each => new List<string> { "implemented by", chain.ConceptReference(depth, each.Key) }));

        return Html.Table(
            ["Relation", "Concept"],
            [.. supers, .. derived],
            "Nothing in this set relates to it: it extends nothing, and nothing extends or implements it.");
    }

    /// <summary>The concepts of the set that extend or implement <paramref name="concept"/>.</summary>
    /// <remarks>
    /// Sorted by key rather than left in declaration order, because a reader comparing two pages expects the same list -
    /// and the set a page belongs to can hold a dozen languages, whose declarations no one order relates.
    /// </remarks>
    private static IEnumerable<Concept> Related(DocumentationChain chain, Concept concept, bool extended) =>
        chain.Languages
            .SelectMany(DocumentationChain.Own)
            .Where(candidate => !string.Equals(candidate.Key, concept.Key, StringComparison.Ordinal)
                && (extended ? candidate.Extensions : candidate.Interfaces)
                    .Any(relation => string.Equals(relation.Key, concept.Key, StringComparison.Ordinal)))
            .DistinctBy(candidate => candidate.Key)
            .OrderBy(candidate => candidate.Key, StringComparer.Ordinal);

    private static string Slots(DocumentationChain chain, Concept concept, int depth)
    {
        var rows = concept.Slots.Select(slot => new List<string>
        {
            $"<code>{Html.Escape(slot.Name)}</code>",
            Kind(slot),
            Target(chain, slot, depth),
            Range(slot),
            Default(slot),
            Html.Escape(Description(slot) ?? string.Empty),
        });

        return Html.Table(["Slot", "Kind", "Holds", "How often", "Default", "Description"], rows, "It declares no slot.");
    }

    private static string Kind(SlotDeclaration slot) => slot switch
    {
        PropertySlot => "property",
        ChildSlot => "child",
        ReferenceSlot => "reference",
        _ => "slot",
    };

    /// <summary>What a slot holds: a property type, or a link to the concept it is declared over.</summary>
    private static string Target(DocumentationChain chain, SlotDeclaration slot, int depth) => slot switch
    {
        PropertySlot property => $"<code>{Html.Escape(property.Type.Name)}</code>",
        ChildSlot child => chain.ConceptReference(depth, child.ChildConceptKey),
        ReferenceSlot reference => chain.ConceptReference(depth, reference.TargetConceptKey),
        _ => string.Empty,
    };

    private static string Range(SlotDeclaration slot) => slot switch
    {
        PropertySlot => "one value",
        ChildSlot { Cardinality: Cardinality.One, Minimum: 0 } => "0 or 1",
        ChildSlot { Cardinality: Cardinality.One } => "exactly 1",
        ChildSlot { Minimum: 0 } => "any number",
        ChildSlot child => $"at least {child.Minimum.ToString(CultureInfo.InvariantCulture)}",
        ReferenceSlot reference => reference.AllowOwnSubtree ? "one target, its own subtree allowed" : "one target",
        _ => string.Empty,
    };

    private static string Default(SlotDeclaration slot) =>
        slot is PropertySlot { DefaultValue.Length: > 0 } property
            ? $"<code>{Html.Escape(property.DefaultValue)}</code>"
            : "<span class=\"muted\">-</span>";

    private static string? Description(SlotDeclaration slot) => slot switch
    {
        PropertySlot property => property.Description,
        _ => null,
    };

    /// <summary>How a concept is declared, in the words the structure language uses.</summary>
    private static string Kind(Concept concept) => concept switch
    {
        { IsInterface: true } => "interface",
        { IsAbstract: true } => "abstract",
        { IsRoot: true } => "root",
        _ => "concrete",
    };
}
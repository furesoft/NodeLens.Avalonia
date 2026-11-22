using System.Globalization;
using System.Text;

using Projectional.Core;
using Projectional.Language;
using Projectional.Languages;
using Projectional.Structures;

namespace Projectional.LanguageDocs;

/// <summary>One page per aspect of a language, so an enumeration or a generator is a page a reader can link to.</summary>
/// <remarks>
/// <para>
/// A language is eleven aspects, and the reason this is one page per aspect rather than ten sections on one page is the
/// tree: a reader looking for "which concepts generate text" should find <c>Generators</c> under the language, not scroll
/// an overview until they meet a heading. The overview keeps the counts and links here.
/// </para>
/// <para>
/// Where the lowered form has lost the author's words the page says what it has instead of inventing a name: a
/// constraint holds a <c>Check</c> method, an intention is chosen at a caret, an enumeration keeps only its symbols.
/// Where it has kept them, it writes them out - a notation as the shape it spells, a generator as the template it emits.
/// </para>
/// </remarks>
internal static class AspectPages
{
    /// <summary>Every aspect, in the order the overview and the tree list them.</summary>
    internal static IReadOnlyList<Aspect> All { get; } =
    [
        Aspect.Enumerations,
        Aspect.Notations,
        Aspect.Types,
        Aspect.Typing,
        Aspect.Constraints,
        Aspect.Scopes,
        Aspect.Intentions,
        Aspect.Generators,
        Aspect.Importers,
        Aspect.Accessory,
    ];

    /// <summary>Renders the page of one aspect.</summary>
    internal static string Render(DocumentationChain chain, LanguageProject language, string path, Aspect aspect)
    {
        ArgumentNullException.ThrowIfNull(chain);
        ArgumentNullException.ThrowIfNull(language);

        var depth = Html.Depth(path);

        var breadcrumb =
            $"{Html.Link(DocumentationChain.HomeLink(depth), chain.Title)} / "
            + $"{Html.Link(DocumentationChain.Link(depth, language, "index.html"), language.Name)} / "
            + $"{Html.Link(DocumentationChain.Link(depth, language, "aspects.html"), "aspects")} / "
            + Html.Escape(AspectNames.Title(aspect));

        return Html.Page(chain, $"{language.Name} - {AspectNames.Title(aspect)}", path, breadcrumb, Body(chain, language, depth, aspect));
    }

    /// <summary>How many of it the language declares, which the overview and the tree list.</summary>
    internal static int Count(LanguageProject language, Aspect aspect)
    {
        ArgumentNullException.ThrowIfNull(language);

        return aspect switch
        {
            Aspect.Enumerations => Enumerations(DocumentationChain.Own(language)).Count,
            Aspect.Notations => language.Notations.Notations.Count,
            Aspect.Types => language.Types.Count,
            Aspect.Typing => language.DeclaredTypingRules.Count,
            Aspect.Constraints => language.Constraints.Count,
            Aspect.Scopes => language.Scopes.Count,
            Aspect.Intentions => language.HasIntentions ? 1 : 0,
            Aspect.Generators => language.Generators.Count,
            Aspect.Importers => language.Importers.Count,
            Aspect.Accessory => language.Accessory.Count,
            _ => 0,
        };
    }

    /// <summary>One sentence saying how much the aspect holds, which is what an empty aspect page leads with.</summary>
    internal static string Summary(LanguageProject language, Aspect aspect)
    {
        var count = Count(language, aspect);

        return aspect switch
        {
            Aspect.Enumerations => count == 0
                ? "No property of this language is declared over an enumeration."
                : $"{Html.Count(count, "enumeration")} the language's properties are declared over.",
            Aspect.Notations => count == 0
                ? "No concept has a notation of its own."
                : $"{Html.Count(count, "notation")}, one per concept that draws itself.",
            Aspect.Types => count == 0
                ? "No type rule is declared, so the language makes no claim about what its nodes hold."
                : $"{Html.Count(count, "type rule")}, with the relations below.",
            Aspect.Typing => count == 0
                ? "Nothing is claimed about typing, so a burst of typing creates nothing in this language."
                : $"{Html.Count(count, "typing rule")}.",
            Aspect.Constraints => count == 0
                ? "No constraint is declared, so nothing is reported about a document of this language."
                : $"{Html.Count(count, "constraint")}.",
            Aspect.Scopes => count == 0
                ? "No concept provides a scope, so a reference of this language has nothing to point at."
                : $"{Html.Count(count, "scope provider")}.",
            Aspect.Intentions => count == 0
                ? "The language offers no intention."
                : "The language offers intentions.",
            Aspect.Generators => count == 0
                ? "The language generates no text."
                : $"{Html.Count(count, "generator")}.",
            Aspect.Importers => count == 0
                ? "The language imports no text."
                : $"{Html.Count(count, "import format")}.",
            Aspect.Accessory => count == 0
                ? "The language ships no accessory document."
                : $"{Html.Count(count, "accessory document")}.",
            _ => string.Empty,
        };
    }

    private static string Body(DocumentationChain chain, LanguageProject language, int depth, Aspect aspect)
    {
        var concepts = DocumentationChain.Own(language);
        var body = new StringBuilder();

        body.Append(Html.Paragraph(Summary(language, aspect)));

        switch (aspect)
        {
            case Aspect.Enumerations:
                body.Append(EnumerationsTable(concepts));
                break;

            case Aspect.Notations:
                body.Append(Notations(chain, language, depth));
                break;

            case Aspect.Types:
                body.Append(Types(language));
                break;

            case Aspect.Typing:
                body.Append(Typing(language));
                break;

            case Aspect.Constraints:
                body.Append(Constraints(language, concepts));
                break;

            case Aspect.Scopes:
                body.Append(Scopes(language, concepts));
                break;

            case Aspect.Intentions:
                body.Append(Intentions(language));
                break;

            case Aspect.Generators:
                Generators(language, body);
                break;

            case Aspect.Importers:
                body.Append(Importers(language));
                break;

            case Aspect.Accessory:
                body.Append(Accessory(language));
                break;
        }

        body.Append(Html.Paragraph($"{Html.Link(DocumentationChain.Link(depth, language, "aspects.html"), "All aspects of " + language.Name)}."));

        return body.ToString();
    }

    /// <summary>The enumerations the language's property slots are declared over.</summary>
    /// <remarks>
    /// Derived from the slots rather than from a declaration list, because lowering resolves an enumeration into the
    /// symbols a property may hold and keeps no key for it: what the framework has after lowering is
    /// <c>enum(a, b, c)</c>, so that - with the slots that hold it - is what a page can honestly name. Two enumerations
    /// with the same symbols in the same order are one type, here as everywhere.
    /// </remarks>
    internal static IReadOnlyList<IReadOnlyList<string>> Enumerations(IReadOnlyList<Concept> concepts) =>
    [
        .. concepts
            .SelectMany(concept => concept.Slots
                .OfType<PropertySlot>()
                .Where(slot => slot.Type.Symbols.Count > 0)
                .Select(slot => (Concept: concept, Slot: slot)))
            .GroupBy(entry => entry.Slot.Type.Name, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => (IReadOnlyList<string>)
            [
                $"<code>{Html.Escape(group.First().Slot.Type.Name)}</code>",
                Html.Escape(string.Join(", ", group.First().Slot.Type.Symbols)),
                Html.Escape(string.Join(", ", group
                    .Select(entry => $"{entry.Concept.Key}.{entry.Slot.Name}")
                    .Order(StringComparer.Ordinal))),
            ]),
    ];

    private static string EnumerationsTable(IReadOnlyList<Concept> concepts) =>
        Html.Table(["Symbols", "Values", "Held by"], Enumerations(concepts), "No property is declared over an enumeration.");

    private static string Notations(DocumentationChain chain, LanguageProject language, int depth)
    {
        var rows = language.Notations.Notations
            .OrderBy(notation => notation.ConceptKey, StringComparer.Ordinal)
            .ThenBy(notation => notation.Variant, StringComparer.Ordinal)
            .Select(notation => new List<string>
            {
                chain.ConceptReference(depth, notation.ConceptKey),
                $"<code>{Html.Escape(notation.Variant)}</code>",
                $"<pre class=\"notation\">{Html.Escape(NotationSpelling.Of(notation))}</pre>",
            });

        return Html.Table(["Concept", "Variant", "Written as"], rows, "No concept has a notation of its own.");
    }

    private static string Types(LanguageProject language)
    {
        var body = new StringBuilder();

        var rules = language.Types.Rules
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .Select(entry => new List<string>
            {
                $"<code>{Html.Escape(entry.Key)}</code>",
                $"<code>{Html.Escape(entry.Value.GetType().Name)}</code>",
            });

        body.Append(Html.Table(["Concept", "Rule"], rules, "No type rule is declared."));

        var subtypes = language.Types.Subtypes.Edges
            .Select(edge => new List<string>
            {
                $"<code>{Html.Escape(edge.Subtype.Name)}</code>",
                $"<code>{Html.Escape(edge.Supertype.Name)}</code>",
            });

        body.Append("<h2>Subtypes</h2>\n");
        body.Append(Html.Table(["A value of", "is also a value of"], subtypes, "No subtype edge is declared."));

        var pairs = language.Types.Comparisons.Pairs
            .Select(pair => new List<string>
            {
                $"<code>{Html.Escape(pair.Left.Name)}</code>",
                $"<code>{Html.Escape(pair.Right.Name)}</code>",
            });

        body.Append("<h2>Comparisons</h2>\n");
        body.Append(Html.Table(["Left", "Right"], pairs, "No comparison is declared."));

        var replacements = language.Types.Replacements
            .Select(replacement => new List<string>
            {
                Html.Escape(replacement.Claim.ToString()),
                $"<code>{Html.Escape(replacement.Left.Name)}</code>",
                $"<code>{Html.Escape(replacement.Right.Name)}</code>",
                Html.Escape(string.Join(" ", replacement.Instead.Select(equation => equation.ToString()))),
            });

        body.Append("<h2>Replacements</h2>\n");
        body.Append(Html.Table(["Claim", "Left", "Right", "Instead"], replacements, "No claim is replaced."));

        return body.ToString();
    }

    private static string Typing(LanguageProject language)
    {
        var rows = language.DeclaredTypingRules.Select(rule => new List<string>
        {
            $"<code>{Html.Escape(rule.ConceptKey)}</code>",
            $"<code>{Html.Escape(rule.Claims)}</code>",
            rule.Text is { Length: > 0 } text ? $"<code>{Html.Escape(text)}</code>" : "<span class=\"muted\">-</span>",
            rule.PropertySlot is { Length: > 0 } property ? $"<code>{Html.Escape(property)}</code>" : "<span class=\"muted\">-</span>",
        });

        return Html.Table(["Creates", "Claims", "Text", "Written into"], rows, "Nothing is claimed about typing.");
    }

    /// <summary>Which concepts carry a constraint; a rule holds a check method, so it has no name a page could print.</summary>
    private static string Constraints(LanguageProject language, IReadOnlyList<Concept> concepts)
    {
        var body = new StringBuilder();

        body.Append(Html.Paragraph(language.Constraints.EverywhereRules.Count switch
        {
            0 => "No rule applies everywhere.",
            1 => "One rule applies to every node of every document.",
            var count => $"{count.ToString(CultureInfo.InvariantCulture)} rules apply to every node of every document.",
        }));

        var rows = concepts
            .OrderBy(concept => concept.Key, StringComparer.Ordinal)
            .Select(concept => (Concept: concept, Rules: language.Constraints.For(concept)))
            .Where(entry => entry.Rules.Count > 0)
            .Select(entry => new List<string>
            {
                $"<code>{Html.Escape(entry.Concept.Key)}</code>",
                entry.Rules.Count.ToString(CultureInfo.InvariantCulture),
            });

        body.Append(Html.Table(["Concept", "Rules"], rows, "No concept has a rule of its own."));

        return body.ToString();
    }

    private static string Scopes(LanguageProject language, IReadOnlyList<Concept> concepts)
    {
        var rows = concepts
            .OrderBy(concept => concept.Key, StringComparer.Ordinal)
            .Where(concept => language.Scopes.ProviderFor(concept) is not null)
            .Select(concept => new List<string>
            {
                $"<code>{Html.Escape(concept.Key)}</code>",
                Html.Escape(string.Join(", ", concept.Slots.OfType<ReferenceSlot>().Select(slot => slot.Name))),
            });

        // The concept's own reference slots, not "the references it answers for": a provider is asked per reference at
        // scope time and says nothing in advance about which ones it will fill, so claiming that would be an inference
        // dressed as a fact.
        return Html.Table(["Concept", "Reference slots of the concept"], rows, "No concept provides a scope.");
    }

    /// <summary>Intentions are asked at a caret, so the registry can only be asked whether it has any.</summary>
    private static string Intentions(LanguageProject language) =>
        Html.Paragraph(language.HasIntentions
            ? "A provider is registered. It is asked what it offers at a caret, so this page can say that much but not which offers it makes."
            : "No provider is registered, so a caret has no offers.");

    /// <summary>The generators, with the template each concept is written out by.</summary>
    private static void Generators(LanguageProject language, StringBuilder body)
    {
        foreach (var generator in language.Generators.OrderBy(generator => generator.Name, StringComparer.Ordinal))
        {
            body.Append(CultureInfo.InvariantCulture, $"<h2><code>{Html.Escape(generator.Name)}</code></h2>\n");

            var rows = generator.Templates.Templates
                .OrderBy(entry => entry.Key, StringComparer.Ordinal)
                .Select(entry => new List<string>
                {
                    $"<code>{Html.Escape(entry.Key)}</code>",
                    entry.Value.IsEmpty
                        ? "<span class=\"muted\">empty, which writes nothing</span>"
                        : $"<pre class=\"notation\">{Html.Escape(TemplateSpelling.Of(entry.Value))}</pre>",
                });

            body.Append(Html.Table(["Concept", "Template"], rows, "The generator has no template, so it generates nothing."));
        }
    }

    private static string Importers(LanguageProject language)
    {
        var rows = language.Importers
            .Select(importer => new List<string>
            {
                $"<code>{Html.Escape(importer.Format)}</code>",
                Html.Escape(importer.GetType().Name),
            });

        return Html.Table(["Format", "Importer"], rows, "The language imports no text.");
    }

    /// <summary>The accessory documents, which are models of the language rather than of its syntax.</summary>
    private static string Accessory(LanguageProject language)
    {
        var rows = language.Accessory
            .OrderBy(model => model.Name, StringComparer.Ordinal)
            .Select(model => new List<string>
            {
                Html.Escape(model.Name),
                model.Roots.Count.ToString(CultureInfo.InvariantCulture),
            });

        return Html.Table(["Document", "Roots"], rows, "The language ships no accessory document.");
    }
}
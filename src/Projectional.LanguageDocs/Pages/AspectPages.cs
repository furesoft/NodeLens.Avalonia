using System.Globalization;
using System.Text;

using Projectional.Core;
using Projectional.ImportRules;
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
        Aspect.Checks,
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
            Aspect.Checks => language.Checks?.Count ?? 0,
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
            Aspect.Checks => count == 0
                ? "The language brings no check of its own, so its documents are checked with the ones the host supplies."
                : $"{Html.Count(count, "check")} the framework derives from the declarations.",
            _ => string.Empty,
        };
    }

    /// <summary>The checks the language brings, with the code each of its findings carries.</summary>
    /// <remarks>
    /// A constraint and a check answer one question from two sides, and the constraints page beside this one would invite
    /// the question: a constraint is a rule the language spells out in its own model, a check is one the framework derives
    /// from the declarations - the name slot, the child slots, the property types. A language can have both, and then the
    /// two pages read as one answer: that is what the author stated, this is what the framework adds.
    /// </remarks>
    private static string Checks(LanguageProject language)
    {
        var rows = (language.Checks?.Checks ?? []).Select(check => new List<string>
        {
            $"<code>{Html.Escape(check.Code)}</code>",
            Html.Escape(check.Title),
        });

        return Html.Table(["Code", "Looks for"], rows, "It brings none.");
    }
    private static string Body(DocumentationChain chain, LanguageProject language, int depth, Aspect aspect)
    {
        var concepts = DocumentationChain.Own(language);
        var body = new StringBuilder();

        body.Append(Html.Paragraph(Summary(language, aspect)));

        // An aspect that holds nothing has already been answered by that sentence, and every table's own "nothing here"
        // would say it a second time - on the constraints page, three times. So the body is the answer to a question
        // only when there is something to answer with.
        if (Count(language, aspect) > 0)
        {
            switch (aspect)
            {
                case Aspect.Checks:
                    body.Append(Checks(language));
                    body.Append(Html.Paragraph(
                        "A check may report more than one code: the one listed is the code it is known by, and the"
                        + " findings a repair can only be made by removing the node carry a second."));
                    break;

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
        }

        body.Append(Html.MarkupParagraph($"{Html.Link(DocumentationChain.Link(depth, language, "aspects.html"), "All aspects of " + language.Name)}."));

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
            body.Append(Html.MarkupParagraph(
                "A slot is written as <code>{name}</code> whether it holds a value or a child, and a list as"
                + " <code>{name...}</code>. A child contributes the text of its own template rather than a placeholder,"
                + " so a field that reuses a statement prints that statement including the <code>;</code> it ends with."));
        }
    }

    /// <summary>Each importer, with what it actually reads: a rule per concept, or the templates it inverts.</summary>
    /// <remarks>
    /// A page that named the CLR type of each importer said nothing a reader could use - "TemplateImporter" is the name
    /// of a class, not of a format. What decides whether a file is read is the rule that matches it, so that is what is
    /// printed: for a declared importer the rules and token classes its model states, for a generator's inverse the
    /// templates it reads back. Both come from the importer itself rather than from the documents, because the inverse
    /// has no document - its rules are the generator's templates.
    /// </remarks>
    private static string Importers(LanguageProject language)
    {
        var body = new StringBuilder();

        foreach (var importer in language.Importers)
        {
            body.Append(CultureInfo.InvariantCulture, $"<h2><code>{Html.Escape(importer.Format)}</code></h2>\n");

            switch (importer)
            {
                case DeclaredImporter declared:
                    Declared(declared, body);
                    break;

                case TemplateImporter inverted:
                    Inverted(inverted, body);
                    break;

                default:
                    body.Append(Html.Paragraph(
                        "This importer is supplied by the host, so this page can name it and not describe what it reads."));
                    break;
            }
        }

        return body.ToString();
    }

    /// <summary>A declared importer: the rules a model states and the token classes they read.</summary>
    private static void Declared(DeclaredImporter importer, StringBuilder body)
    {
        body.Append(Html.MarkupParagraph(
            "Reads the format from rules its model declares, rather than from a generator's templates, so it can read a"
            + $" file the generator never prints. {Starts(importer.Roots)}"));

        var rules = importer.Rules
            .OrderBy(rule => rule.ConceptKey, StringComparer.Ordinal)
            .Select(rule => new List<string>
            {
                Code(rule.ConceptKey),
                $"<pre class=\"notation\">{Html.Escape(ImportSpelling.Of(rule))}</pre>",
            });

        body.Append(Html.Table(["Concept", "Rule"], rules, "The importer declares no rule."));

        var tokens = importer.Tokens
            .OrderBy(token => token.Name, StringComparer.Ordinal)
            .Select(token => new List<string>
            {
                Code(token.Name),
                $"<code>{Html.Escape(ImportSpelling.Of(token))}</code>",
            });

        body.Append(Html.Table(
            ["Token class", "What it reads"],
            tokens,
            "No token class is declared, so every run ends at the next literal."));

        body.Append(Html.MarkupParagraph(
            "Every choice is decided by the text at the cursor, so nothing is tried and undone: a rule is taken by the"
            + " literal it starts with, and a repeat's separator has to be told apart from what one of its entries begins"
            + " with - a rule where it cannot be is refused when the importer is built, not halfway through a file. A file"
            + $" that does not match is refused with one of {Codes(DeclaredImporter.EndsEarly, DeclaredImporter.TrailingText, DeclaredImporter.UnexpectedText, DeclaredImporter.EmptyText, DeclaredImporter.TooFew)}, naming the place it stopped."));
    }

    /// <summary>A generator's inverse: the templates it reads the text back from.</summary>
    private static void Inverted(TemplateImporter importer, StringBuilder body)
    {
        var generator = importer.Generator;

        body.Append(Html.MarkupParagraph(
            $"Reads back what the {Code(generator.Name)} generator writes, so the file a document was generated into can"
            + $" be opened again rather than being a one-way street. {Starts(importer.Roots)}"));

        var rows = generator.Templates.Templates
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .Select(entry => new List<string>
            {
                Code(entry.Key),
                entry.Value.IsEmpty
                    ? "<span class=\"muted\">empty, so it writes nothing and nothing is read back</span>"
                    : $"<pre class=\"notation\">{Html.Escape(TemplateSpelling.Of(entry.Value))}</pre>",
            });

        body.Append(Html.Table(
            ["Concept", "The text it reads"],
            rows,
            "The generator has no template, so there is nothing to read back."));

        body.Append(Html.MarkupParagraph(
            "Nothing is tried and undone: a literal anchors the shape, a property is the run up to the template's next"
            + " literal, a child is read by the template of the concept its slot allows, and a list repeats until its"
            + " separator stops matching. A file whose text differs from what the generator writes is refused with one of"
            + $" {Codes(TemplateImporter.EndsEarly, TemplateImporter.TrailingText, TemplateImporter.UnexpectedText, TemplateImporter.EmptyText)}, naming the place it stopped."));
    }

    /// <summary>The concepts a document may start with, as a sentence.</summary>
    private static string Starts(IReadOnlyList<string> roots) => roots.Count switch
    {
        0 => "No concept is named as a document root, so it could not read a file at all.",
        1 => $"A document starts with {Code(roots[0])}.",
        _ => $"A document starts with {string.Join(", ", roots.Take(roots.Count - 1).Select(Code))} or {Code(roots[^1])}.",
    };

    /// <summary>The refusal codes, so a reader who sees one in the status line can find it here.</summary>
    private static string Codes(params ReadOnlySpan<string> codes) =>
        string.Join(", ", codes.ToArray().Select(Code));

    private static string Code(string text) => $"<code>{Html.Escape(text)}</code>";

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
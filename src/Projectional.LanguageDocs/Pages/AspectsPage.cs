using System.Globalization;
using System.Text;

using Projectional.Core;
using Projectional.Language;
using Projectional.Languages;

namespace Projectional.LanguageDocs;

/// <summary>One language's aspects: what each folder of the language lowered into, written out where it can be read.</summary>
/// <remarks>
/// <para>
/// The point of this page is that a language is eleven aspects and a reader should not have to open eleven documents to
/// see which ones it has. Each section answers what the folder holds, and where the lowered form still carries the
/// author's words it writes them out - a notation as the shape it spells, a generator as the template it emits.
/// </para>
/// <para>
/// Where the lowered form has lost the words, the page says what it has instead of inventing a name: a constraint holds
/// a <c>Check</c> method, so the page counts the rules and names the concepts they belong to; an intention is chosen at
/// a caret, so the page can only say that a provider is registered. Pretending otherwise would be a page that reads
/// better than the framework deserves.
/// </para>
/// </remarks>
internal static class AspectsPage
{
    /// <summary>Renders <c>&lt;Language&gt;/aspects.html</c>.</summary>
    internal static string Render(DocumentationChain chain, LanguageProject language, string path)
    {
        ArgumentNullException.ThrowIfNull(chain);
        ArgumentNullException.ThrowIfNull(language);

        var depth = Html.Depth(path);
        var concepts = DocumentationChain.Own(language);

        var body = new StringBuilder();

        body.Append(Html.Section("Overview", Overview(language, concepts.Count)));
        body.Append(Html.Section("Concepts", Subsection(language, concepts)));
        body.Append(Html.Section("Enumerations", Enumerations(concepts)));
        body.Append(Html.Section("Notations", Notations(chain, language, depth)));
        body.Append(Html.Section("Types", Types(language)));
        body.Append(Html.Section("Typing", Typing(language)));
        body.Append(Html.Section("Constraints", Constraints(language, concepts)));
        body.Append(Html.Section("Scopes", Scopes(language, concepts)));
        body.Append(Html.Section("Intentions", Intentions(language)));
        body.Append(Html.Section("Generators", Generators(language)));
        body.Append(Html.Section("Import", Import(language)));
        body.Append(Html.Section("Accessory models", Accessory(language)));

        var breadcrumb =
            $"{Html.Link(DocumentationChain.HomeLink(depth), chain.Title)} / "
            + $"{Html.Link(DocumentationChain.Link(depth, language, "index.html"), language.Name)} / aspects";

        return Html.Page(chain, $"{language.Name} - aspects", path, breadcrumb, body.ToString());
    }

    /// <summary>One row per aspect folder: how much of it the language declares.</summary>
    private static string Overview(LanguageProject language, int concepts)
    {
        var rows = new List<IReadOnlyList<string>>();

        void Row(string folder, int count) =>
            rows.Add([$"<code>{Html.Escape(folder)}</code>", count.ToString(CultureInfo.InvariantCulture)]);

        Row(LanguageProject.StructureFolder, concepts);
        Row(LanguageProject.EditorFolder, language.Notations.Notations.Count);
        Row(LanguageProject.TypesFolder, language.Types.Count);
        Row(LanguageProject.TypingFolder, language.DeclaredTypingRules.Count);
        Row(LanguageProject.ConstraintsFolder, language.Constraints.Count);
        Row(LanguageProject.ScopesFolder, language.Scopes.Count);
        Row(LanguageProject.IntentionsFolder, language.HasIntentions ? 1 : 0);
        Row(LanguageProject.GeneratorFolder, language.Generators.Count);
        Row(LanguageProject.ImportFolder, language.Importers.Count);
        Row(LanguageProject.AccessoryFolder, language.Accessory.Count);

        return Html.Table(["Aspect folder", "Declared"], rows, "Nothing is declared.");
    }

    private static string Subsection(LanguageProject language, IReadOnlyList<Concept> concepts)
    {
        var visible = language.Concepts.Concepts.Count;
        var inherited = visible - concepts.Count;

        var body = new StringBuilder();

        body.Append(Html.Paragraph(inherited switch
        {
            0 => "Every concept the registry holds is declared here.",
            1 => "One further concept is visible through the chain, declared by a language this one stands on.",
            _ => $"{inherited.ToString(CultureInfo.InvariantCulture)} further concepts are visible through the chain, declared by the languages this one stands on.",
        }));

        var kinds = concepts
            .GroupBy(Kind)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new List<string>
            {
                Html.Escape(group.Key),
                group.Count().ToString(CultureInfo.InvariantCulture),
                Html.Escape(string.Join(", ", group.Select(each => each.Key).Order(StringComparer.Ordinal))),
            });

        body.Append(Html.Table(["Kind", "Count", "Concepts"], kinds, "No concept is declared."));

        return body.ToString();
    }

    /// <summary>The enumerations the language's property slots are declared over.</summary>
    /// <remarks>
    /// Derived from the slots rather than from a declaration list, because lowering resolves an enumeration into the
    /// symbols a property may hold and keeps no key for it: what the framework has after lowering is
    /// <c>enum(a, b, c)</c>, so that - with the slots that hold it - is what the page can honestly name. A language
    /// that declares two enumerations with the same symbols in the same order has one type, here as everywhere.
    /// </remarks>
    private static string Enumerations(IReadOnlyList<Concept> concepts)
    {
        var uses = concepts
            .SelectMany(concept => concept.Slots
                .OfType<PropertySlot>()
                .Where(slot => slot.Type.Symbols.Count > 0)
                .Select(slot => (Concept: concept, Slot: slot)))
            .GroupBy(entry => entry.Slot.Type.Name, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal);

        var rows = uses.Select(group => new List<string>
        {
            $"<code>{Html.Escape(group.Key)}</code>",
            Html.Escape(string.Join(", ", group.First().Slot.Type.Symbols)),
            Html.Escape(string.Join(", ", group
                .Select(entry => $"{entry.Concept.Key}.{entry.Slot.Name}")
                .Order(StringComparer.Ordinal))),
        });

        return Html.Table(["Symbols", "Values", "Held by"], rows, "No property is declared over an enumeration.");
    }

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

        body.Append("<h3>Subtypes</h3>\n");
        body.Append(Html.Table(["A value of", "is also a value of"], subtypes, "No subtype edge is declared."));

        var pairs = language.Types.Comparisons.Pairs
            .Select(pair => new List<string>
            {
                $"<code>{Html.Escape(pair.Left.Name)}</code>",
                $"<code>{Html.Escape(pair.Right.Name)}</code>",
            });

        body.Append("<h3>Comparisons</h3>\n");
        body.Append(Html.Table(["Left", "Right"], pairs, "No comparison is declared."));

        var replacements = language.Types.Replacements
            .Select(replacement => new List<string>
            {
                Html.Escape(replacement.Claim.ToString()),
                $"<code>{Html.Escape(replacement.Left.Name)}</code>",
                $"<code>{Html.Escape(replacement.Right.Name)}</code>",
                Html.Escape(string.Join(" ", replacement.Instead.Select(equation => equation.ToString()))),
            });

        body.Append("<h3>Replacements</h3>\n");
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
            ? "The language offers intentions. A provider is asked what it offers at a caret, so this page can say that one is registered but not which offers it makes."
            : "The language offers no intention.");

    /// <summary>The generators, with the template each concept is written out by.</summary>
    private static string Generators(LanguageProject language)
    {
        var body = new StringBuilder();

        Body(language.Generators, body);

        return body.ToString();

        static void Body(IReadOnlyList<TextGenerator> generators, StringBuilder body)
        {
            foreach (var generator in generators.OrderBy(generator => generator.Name, StringComparer.Ordinal))
            {
                body.Append(CultureInfo.InvariantCulture, $"<h3><code>{Html.Escape(generator.Name)}</code></h3>\n");

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
    }

    private static string Import(LanguageProject language)
    {
        var rows = language.Importers
            .Select(importer => new List<string> { $"<code>{Html.Escape(importer.Format)}</code>" });

        return Html.Table(["Format"], rows, "The language imports no text.");
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

    /// <summary>How a concept is declared, in the words the structure language uses.</summary>
    private static string Kind(Concept concept) => concept switch
    {
        { IsInterface: true } => "interface",
        { IsAbstract: true } => "abstract",
        { IsRoot: true } => "root",
        _ => "concrete",
    };
}
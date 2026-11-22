using System.Globalization;
using System.Text;

using Projectional.Core;
using Projectional.Languages;

namespace Projectional.LanguageDocs;

/// <summary>One language's aspects page: what each of the eleven folders lowered into, counted and named.</summary>
/// <remarks>
/// The point of this page is the count. A language that declares seven concepts and no notation is not wrong, but it
/// is incomplete, and the aspect list is what says so at a glance - for the language itself and for the base language
/// it stands on, whose concepts are on the chain and therefore have no notation of their own here.
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

    private static string Notations(DocumentationChain chain, LanguageProject language, int depth)
    {
        var rows = language.Notations.Notations
            .OrderBy(notation => notation.ConceptKey, StringComparer.Ordinal)
            .Select(notation => new List<string>
            {
                chain.ConceptReference(depth, notation.ConceptKey),
                $"<code>{Html.Escape(notation.Variant)}</code>",
            });

        return Html.Table(["Concept", "Variant"], rows, "No concept has a notation of its own.");
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

        body.Append($"<h3>Subtypes</h3>\n");
        body.Append(Html.Table(["A value of", "is also a value of"], subtypes, "No subtype edge is declared."));

        var pairs = language.Types.Comparisons.Pairs
            .Select(pair => new List<string>
            {
                $"<code>{Html.Escape(pair.Left.Name)}</code>",
                $"<code>{Html.Escape(pair.Right.Name)}</code>",
            });

        body.Append("<h3>Comparisons</h3>\n");
        body.Append(Html.Table(["Left", "Right"], pairs, "No comparison is declared."));

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

    /// <summary>Which concepts carry a constraint; the rules themselves hold no name a page could print.</summary>
    private static string Constraints(LanguageProject language, IReadOnlyList<Concept> concepts)
    {
        var body = new StringBuilder();

        var everywhere = language.Constraints.EverywhereRules.Count;

        body.Append(Html.Paragraph(everywhere switch
        {
            0 => "No rule applies everywhere.",
            1 => "One rule applies to every node.",
            _ => $"{everywhere.ToString(CultureInfo.InvariantCulture)} rules apply to every node.",
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
            .Select(concept => new List<string> { $"<code>{Html.Escape(concept.Key)}</code>" });

        return Html.Table(["Concept"], rows, "No concept provides a scope.");
    }

    /// <summary>Intentions are asked at a caret, so the registry can only be asked whether it has any.</summary>
    private static string Intentions(LanguageProject language) =>
        Html.Paragraph(language.HasIntentions
            ? "The language offers intentions. They are chosen by caret, so this page can only say that a provider is registered, not which offers it makes."
            : "The language offers no intention.");

    private static string Generators(LanguageProject language)
    {
        var rows = language.Generators
            .OrderBy(generator => generator.Name, StringComparer.Ordinal)
            .Select(generator => new List<string>
            {
                $"<code>{Html.Escape(generator.Name)}</code>",
                generator.Templates.Count.ToString(CultureInfo.InvariantCulture),
            });

        return Html.Table(["Generator", "Templates"], rows, "The language generates no text.");
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
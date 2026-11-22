using System.Globalization;
using System.Text;

using Projectional.Core;
using Projectional.Languages;

namespace Projectional.LanguageDocs;

/// <summary>One language's aspects at a glance: how much of each it declares, and the page that shows it.</summary>
/// <remarks>
/// An overview rather than the content: every aspect has a page of its own, because a reader looking for "which concepts
/// generate text" wants <c>Generators</c> in the tree and not a heading they have to scroll to. What belongs here is the
/// comparison - a language with seven concepts and no notation is not wrong, but it is incomplete, and this table is what
/// says so at a glance.
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

        body.Append(Html.MarkupParagraph($"Every folder of the language and what it lowered into. Each aspect has {Html.Link(DocumentationChain.Link(depth, language, "index.html"), "its own page")}, listed in the tree beside this one."));

        body.Append(Html.Section("Aspects", Aspects(language, depth)));
        body.Append(Html.Section("Concepts", Concepts(language, concepts)));

        var breadcrumb =
            $"{Html.Link(DocumentationChain.HomeLink(depth), chain.Title)} / "
            + $"{Html.Link(DocumentationChain.Link(depth, language, "index.html"), language.Name)} / aspects";

        return Html.Page(chain, $"{language.Name} - aspects", path, breadcrumb, body.ToString());
    }

    /// <summary>One row per aspect: the folder it comes from, how much the language declares, and where to read it.</summary>
    private static string Aspects(LanguageProject language, int depth)
    {
        var rows = AspectPages.All.Select(aspect => new List<string>
        {
            Html.Link(DocumentationChain.Link(depth, language, AspectNames.File(aspect)), AspectNames.Title(aspect)),
            $"<code>{Html.Escape(Folder(aspect))}</code>",
            AspectPages.Count(language, aspect).ToString(CultureInfo.InvariantCulture),
            AspectPages.Summary(language, aspect),
        });

        return Html.Table(["Aspect", "Folder", "Declared", "What it says"], rows, "The language declares no aspect.");
    }

    /// <summary>The folder an aspect is declared in, or the reason it has none.</summary>
    private static string Folder(Aspect aspect) => aspect switch
    {
        Aspect.Enumerations => "structure (a declaration, not a folder of its own)",
        Aspect.Notations => LanguageProject.EditorFolder,
        Aspect.Types => LanguageProject.TypesFolder,
        Aspect.Typing => LanguageProject.TypingFolder,
        Aspect.Constraints => LanguageProject.ConstraintsFolder,
        Aspect.Scopes => LanguageProject.ScopesFolder,
        Aspect.Intentions => LanguageProject.IntentionsFolder,
        Aspect.Generators => LanguageProject.GeneratorFolder,
        Aspect.Importers => LanguageProject.ImportFolder,
        Aspect.Accessory => LanguageProject.AccessoryFolder,
        _ => "-",
    };

    private static string Concepts(LanguageProject language, IReadOnlyList<Concept> concepts)
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

    /// <summary>How a concept is declared, in the words the structure language uses.</summary>
    private static string Kind(Concept concept) => concept switch
    {
        { IsInterface: true } => "interface",
        { IsAbstract: true } => "abstract",
        { IsRoot: true } => "root",
        _ => "concrete",
    };
}
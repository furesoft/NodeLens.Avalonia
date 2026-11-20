using Projectional.Core;
using Projectional.Language;

namespace Projectional.ImportRules;

/// <summary>Turns the importer documents a language project holds into the importers a host uses.</summary>
/// <remarks>
/// One document holds one importer and the importer is the document root, the way a generator is one document per
/// generator. Every refusal here is a load error, so a rule that names a property the concept does not have, a repeat
/// over a single-child slot, or two rules that begin with the same text fails where the author is looking rather than
/// during a file.
/// </remarks>
public static class ImportLowering
{
    /// <summary>The importers the documents of one <c>import</c> folder declare.</summary>
    /// <param name="documents">The documents, one importer each.</param>
    /// <param name="concepts">The language's concepts.</param>
    /// <exception cref="ConceptDefinitionException">A document is not an importer, or a rule cannot be read.</exception>
    public static IReadOnlyList<ITextImporter> CreateImporters(
        IEnumerable<Node> documents,
        ConceptRegistry concepts)
    {
        ArgumentNullException.ThrowIfNull(documents);
        ArgumentNullException.ThrowIfNull(concepts);

        List<ITextImporter> importers = [];

        foreach (var document in documents)
        {
            importers.Add(CreateImporter(document, concepts));
        }

        return importers;
    }

    /// <summary>The one importer a document declares.</summary>
    /// <param name="document">The document, whose root is the importer.</param>
    /// <param name="concepts">The language's concepts.</param>
    /// <exception cref="ConceptDefinitionException">The document is not an importer, or a rule cannot be read.</exception>
    public static ITextImporter CreateImporter(Node document, ConceptRegistry concepts)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(concepts);

        if (document.Concept.Key != ImportConceptKeys.Importer)
        {
            throw new ConceptDefinitionException(
                "An import document holds exactly one importer: the document's root has to be one.");
        }

        var root = document;
        var name = root.GetOrNull(ImportLanguage.NameSlot)
            ?? throw new ConceptDefinitionException("An importer names the format it reads.");

        List<ImportRule> rules = [];

        foreach (var node in root.GetChildren(ImportLanguage.RulesSlot))
        {
            rules.Add(LowerRule(node));
        }

        return new DeclaredImporter(name, rules, Tokens(root), concepts, Roots(concepts, rules));
    }

    /// <summary>The token classes the importer declares, or none when it declares none.</summary>
    private static List<ImportToken> Tokens(Node importer)
    {
        List<ImportToken> tokens = [];

        foreach (var node in importer.GetChildren(ImportLanguage.TokensSlot))
        {
            var name = node.GetOrNull(ImportLanguage.TokenNameSlot);

            if (name is not { Length: > 0 })
            {
                throw new ConceptDefinitionException("A token class names itself, because a rule has to refer to it.");
            }

            tokens.Add(node.Concept.Key switch
            {
                ImportConceptKeys.Word => new ImportToken(name, ImportTokenKind.Word, Extra: node.GetOrNull(ImportLanguage.ExtraSlot) ?? string.Empty),
                ImportConceptKeys.Number => new ImportToken(name, ImportTokenKind.Number),
                ImportConceptKeys.Quoted => new ImportToken(
                    name,
                    ImportTokenKind.Quoted,
                    Quote: node.GetOrNull(ImportLanguage.QuoteSlot) ?? "\"",
                    Escape: node.GetOrNull(ImportLanguage.EscapeSlot) ?? string.Empty),
                ImportConceptKeys.Skipped => new ImportToken(
                    name,
                    ImportTokenKind.Skipped,
                    Prefix: node.GetOrNull(ImportLanguage.PrefixSlot) ?? string.Empty,
                    Until: node.GetOrNull(ImportLanguage.UntilSlot) ?? "\n"),
                var other => throw new ConceptDefinitionException(
                    $"'{other}' is not a token class an importer can declare."),
            });
        }

        return tokens;
    }
    private static ImportRule LowerRule(Node rule)
    {
        var key = rule.GetOrNull(ImportLanguage.ConceptKeySlot)
            ?? throw new ConceptDefinitionException("A rule names the concept it reads.");

        List<ImportPart> parts = [];

        foreach (var part in rule.GetChildren(ImportLanguage.PartsSlot))
        {
            parts.Add(LowerPart(key, part));
        }

        return new ImportRule(key, parts);
    }

    private static ImportPart LowerPart(string key, Node part) => part.Concept.Key switch
    {
        ImportConceptKeys.Literal => ImportPart.Literal(Required(key, part, ImportLanguage.TextSlot)),
        ImportConceptKeys.Capture => ImportPart.Capture(
            Required(key, part, ImportLanguage.SlotSlot),
            part.GetOrNull(ImportLanguage.TokenSlot) ?? string.Empty),
        ImportConceptKeys.Nest => ImportPart.Nest(Required(key, part, ImportLanguage.SlotSlot)),
        ImportConceptKeys.Repeat => ImportPart.Repeat(
            Required(key, part, ImportLanguage.SlotSlot),
            part.GetOrNull(ImportLanguage.SeparatorSlot) ?? string.Empty,
            Minimum(key, part)),
        var other => throw new ConceptDefinitionException(
            $"The rule of '{key}' holds a '{other}', which is not a part a rule is built from."),
    };

    private static string Required(string key, Node part, string slot) =>
        part.GetOrNull(slot) is { Length: > 0 } value
            ? value
            : throw new ConceptDefinitionException(
                $"A '{part.Concept.Key}' in the rule of '{key}' leaves out its '{slot}'.");

    private static int Minimum(string key, Node part) =>
        part.GetOrNull(ImportLanguage.MinimumSlot) is not { } text || text.Length == 0
            ? 0
            : int.TryParse(text, out var minimum) && minimum >= 0
                ? minimum
                : throw new ConceptDefinitionException(
                    $"The repeat of '{key}' states '{text}' entries as its minimum, which is not a count of zero or more.");

    /// <summary>The concepts whose rule may stand at the top of a document.</summary>
    /// <remarks>
    /// A concept with a rule that no other concept's child slot can hold. Without that condition every rule would be a
    /// document, and a stray line would import as whatever happened to have an anchor.
    /// </remarks>
    private static List<string> Roots(ConceptRegistry concepts, IReadOnlyList<ImportRule> rules)
    {
        List<string> roots = [];

        foreach (var rule in rules)
        {
            var concept = concepts.Get(rule.ConceptKey);

            if (concepts.Concepts.Any(owner => owner.Slots.OfType<ChildSlot>().Any(slot => concept.IsKindOf(slot.ChildConceptKey))))
            {
                continue;
            }

            roots.Add(rule.ConceptKey);
        }

        return roots;
    }
}

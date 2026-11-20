using Projectional.Core;
using Projectional.Language;

namespace Projectional.ImportRules;

/// <summary>Reads a format from rules a language wrote as a model, rather than from its generator's templates.</summary>
/// <remarks>
/// <para>
/// The generator's inverse reads exactly what the generator writes. This reads what a <em>rule</em> says, which is the
/// case the inverse cannot cover: a file with a shape the generator never produces, a second spelling of the same
/// model, a format the model was built to read rather than to print.
/// </para>
/// <para>
/// The vocabulary is a literal, a capture, a nest and a repeat - the verbs a template has, because they are the ones a
/// language author already knows and because anything else would be a programming language in a model. The discipline
/// is the inverse's: every choice is decided by the text at the cursor, so two rules whose anchors are the same, or a
/// repeat whose entry cannot be told from its separator, are refused when the importer is built.
/// </para>
/// </remarks>
public sealed class DeclaredImporter : ITextImporter
{
    /// <summary>Text where a rule's next literal was expected.</summary>
    public const string UnexpectedText = "import.unexpected-text";

    /// <summary>The text ended before a rule was finished.</summary>
    public const string EndsEarly = "import.ends-early";

    /// <summary>Characters after the one document.</summary>
    public const string TrailingText = "import.trailing-text";

    /// <summary>Nothing at all.</summary>
    public const string EmptyText = "import.empty-text";

    /// <summary>A repeat found fewer entries than it requires.</summary>
    public const string TooFew = "import.too-few";

    private readonly ConceptRegistry _concepts;
    private readonly IReadOnlyDictionary<string, ImportRule> _rules;
    private readonly IReadOnlyList<string> _roots;
    private readonly Dictionary<string, ImportToken> _tokens;

    /// <summary>Builds an importer from rules a language declared.</summary>
    /// <param name="format">The format's name, which a host's file dialog filters on.</param>
    /// <param name="rules">One rule per concept.</param>
    /// <param name="concepts">The language's concepts, which decide what a slot may hold.</param>
    /// <param name="roots">The concepts that may stand at the top of a document, in the order to try.</param>
    /// <exception cref="ConceptDefinitionException">The rules cannot be read, and the message says why.</exception>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public DeclaredImporter(
        string format,
        IReadOnlyList<ImportRule> rules,
        IReadOnlyList<ImportToken> tokens,
        ConceptRegistry concepts,
        IReadOnlyList<string> roots)
    {
        ArgumentException.ThrowIfNullOrEmpty(format);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(tokens);
        ArgumentNullException.ThrowIfNull(concepts);
        ArgumentNullException.ThrowIfNull(roots);

        Format = format;
        _concepts = concepts;
        _roots = roots;
        _rules = rules.ToDictionary(rule => rule.ConceptKey, StringComparer.Ordinal);
        _tokens = tokens.ToDictionary(token => token.Name, StringComparer.Ordinal);

        Validate(rules);
    }

    /// <summary>The name of the format, which is the importer document's name.</summary>
    public string Format { get; }

    /// <summary>The rules this importer reads.</summary>
    public IReadOnlyList<ImportRule> Rules => [.. _rules.Values];

    /// <summary>Whether the text starts like one of the rules that may be a document.</summary>
    public bool CanImport(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        return _roots.Any(root => _rules.TryGetValue(root, out var rule)
            && text.AsSpan().StartsWith(rule.Anchor, StringComparison.Ordinal));
    }

    /// <summary>Reads <paramref name="text"/> into <paramref name="model"/> as one root, in one undo step.</summary>
    /// <param name="model">The model the host created; it is filled, never replaced.</param>
    /// <param name="text">The whole text, read by the host.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public ImportResult Import(Model model, string text)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(text);

        if (string.IsNullOrWhiteSpace(text))
        {
            return ImportResult.Refused(new ImportDiagnostic(EmptyText, "There is no text to import.", 1, 1));
        }

        using var transaction = model.BeginTransaction();

        var reader = new RuleReader(model, _concepts, _rules, _tokens, text);

        if (reader.ParseRoot(_roots) is not { } root)
        {
            return ImportResult.Refused(reader.Refusal!);
        }

        if (reader.Trailing is { } trailing)
        {
            return ImportResult.Refused(trailing);
        }

        transaction.Complete();

        return ImportResult.Imported([root]);
    }

    /// <summary>Refuses the rules a reader could not decide, before a file is read with them.</summary>
    /// <exception cref="ConceptDefinitionException">A rule cannot be read.</exception>
    private void Validate(IReadOnlyList<ImportRule> rules)
    {
        if (_roots.Count == 0)
        {
            throw new ConceptDefinitionException(
                $"The importer '{Format}' cannot be read: no concept is named as a document root.");
        }

        foreach (var root in _roots)
        {
            if (!_rules.ContainsKey(root))
            {
                throw new ConceptDefinitionException($"The importer '{Format}' names the root '{root}', which has no rule.");
            }
        }

        foreach (var rule in rules)
        {
            var concept = _concepts.Get(rule.ConceptKey);

            if (rule.Parts.Count == 0)
            {
                throw new ConceptDefinitionException(
                    $"The rule of '{rule.ConceptKey}' has no parts, so it would read nothing and leave a node behind.");
            }

            // A matches whose bind is not earlier in the same rule would be a name nothing remembers, which is
            // knowable here and surprising only later.
            var bound = new HashSet<string>(StringComparer.Ordinal);

            foreach (var part in rule.Parts)
            {
                switch (part.Kind)
                {
                    case ImportPartKind.Bind when part.Token.Length > 0 && !_tokens.ContainsKey(part.Token):
                        throw new ConceptDefinitionException(
                            "The rule of '" + rule.ConceptKey + "' binds a '" + part.Token
                            + "', and the importer declares no token class of that name.");

                    case ImportPartKind.Bind:
                        bound.Add(part.Name);
                        break;

                    case ImportPartKind.Matches when !bound.Contains(part.Name):
                        throw new ConceptDefinitionException(
                            "The rule of '" + rule.ConceptKey + "' requires '" + part.Name
                            + "' again, and nothing in it bound that name earlier.");

                    case ImportPartKind.Raw when part.Text.Length == 0:
                        throw new ConceptDefinitionException(
                            "The raw part of '" + rule.ConceptKey + "' names no terminator, so it would swallow the rest of the file.");

                    case ImportPartKind.Fail when part.Text.Length == 0:
                        throw new ConceptDefinitionException(
                            "The fail part of '" + rule.ConceptKey + "' states no code, so a host could not group the refusal.");

                    case ImportPartKind.Capture when part.Token.Length > 0 && !_tokens.ContainsKey(part.Token):
                        throw new ConceptDefinitionException(
                            "The rule of '" + rule.ConceptKey + "' reads a '" + part.Token
                            + "', and the importer declares no token class of that name.");

                    case ImportPartKind.Capture when part.Token.Length > 0 && _tokens[part.Token].Kind == ImportTokenKind.Skipped:
                        throw new ConceptDefinitionException(
                            "The rule of '" + rule.ConceptKey + "' captures the skipped class '" + part.Token
                            + "', which by definition produces no value.");

                    case ImportPartKind.Capture or ImportPartKind.Line when concept.FindProperty(part.Slot) is null:
                        throw new ConceptDefinitionException(
                            $"The rule of '{rule.ConceptKey}' fills '{part.Slot}', which is not a property of it.");

                    case ImportPartKind.Nest:
                        RequireSlot(concept, rule, part, many: false);
                        break;

                    case ImportPartKind.Repeat:
                        RequireSlot(concept, rule, part, many: true);
                        RequireSeparable(rule, part, rules, _concepts);
                        break;
                }
            }
        }

        RequireDistinctAnchors(rules, _roots, _rules, _concepts);
    }

    private static void RequireSlot(Concept concept, ImportRule rule, ImportPart part, bool many)
    {
        var declaration = concept.FindChild(part.Slot)
            ?? throw new ConceptDefinitionException(
                $"The rule of '{rule.ConceptKey}' reads '{part.Slot}', which is not a child slot of it.");

        if (many != (declaration.Cardinality == Cardinality.Many))
        {
            throw new ConceptDefinitionException(
                $"The rule of '{rule.ConceptKey}' reads '{part.Slot}' as "
                + (many ? "many children" : "one child")
                + $", but the slot holds {(many ? "one" : "many")}.");
        }
    }

    /// <summary>Refuses a repeat whose separator an entry's own text could begin with.</summary>
    /// <remarks>
    /// The question is asked of the rules that fill the slot, not of the rule that owns the list: what has to be told
    /// apart is where one entry ends and the separator begins. A literal of the owning rule that equals the separator
    /// is therefore fine - the newline before a list is not competing with the newline between its entries.
    /// </remarks>
    private static void RequireSeparable(
        ImportRule owner,
        ImportPart part,
        IReadOnlyList<ImportRule> rules,
        ConceptRegistry concepts)
    {
        if (part.Separator.Length == 0)
        {
            return;
        }

        var slot = concepts.Get(owner.ConceptKey).FindChild(part.Slot)
            ?? throw new ConceptDefinitionException(
                $"The rule of '{owner.ConceptKey}' reads '{part.Slot}', which is not a child slot of it.");

        foreach (var entry in rules.Where(entry => concepts.Get(entry.ConceptKey).IsKindOf(slot.ChildConceptKey)))
        {
            if (entry.Anchor.Length > 0 && entry.Anchor.StartsWith(part.Separator, StringComparison.Ordinal))
            {
                throw new ConceptDefinitionException(
                    $"The rule of '{owner.ConceptKey}' separates '{part.Slot}' with '{part.Separator}', which the rule of "
                    + $"'{entry.ConceptKey}' begins with ('{entry.Anchor}'), so the text could not be split.");
            }
        }
    }

    /// <summary>Refuses anchors that two rules of one slot, or two roots, share.</summary>
    private static void RequireDistinctAnchors(
        IReadOnlyList<ImportRule> rules,
        IReadOnlyList<string> roots,
        IReadOnlyDictionary<string, ImportRule> byKey,
        ConceptRegistry concepts)
    {
        var rootAnchors = roots
            .Where(byKey.ContainsKey)
            .Select(root => byKey[root].Anchor)
            .Where(anchor => anchor.Length > 0)
            .GroupBy(anchor => anchor, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);

        if (rootAnchors is not null)
        {
            throw new ConceptDefinitionException(
                $"Two document roots begin with '{rootAnchors.Key}', so which one the text is cannot be told.");
        }

        foreach (var owner in rules)
        {
            var slots = concepts.Get(owner.ConceptKey).Slots.OfType<ChildSlot>().ToList();

            foreach (var slot in slots)
            {
                var rivals = rules
                    .Where(rule => concepts.Get(rule.ConceptKey).IsKindOf(slot.ChildConceptKey))
                    .Where(rule => rule.Anchor.Length > 0)
                    .GroupBy(rule => rule.Anchor, StringComparer.Ordinal)
                    .FirstOrDefault(group => group.Count() > 1);

                if (rivals is not null)
                {
                    throw new ConceptDefinitionException(
                        $"Two rules for '{slot.ChildConceptKey}' begin with '{rivals.Key}', so which one was written "
                        + "cannot be told.");
                }
            }
        }
    }
}

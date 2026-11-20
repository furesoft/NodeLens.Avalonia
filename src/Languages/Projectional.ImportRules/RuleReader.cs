using Projectional.Core;
using Projectional.Language;

namespace Projectional.ImportRules;

/// <summary>Reads text by walking declared rules, deciding every choice at the cursor.</summary>
/// <remarks>
/// The same discipline the generator's inverse follows, one level more explicit: a literal must be there, a capture is
/// the run up to the next literal, a nest takes whichever rule the slot allows, a repeat goes round while the entry's
/// anchor is still written. Nothing is tried and undone, so an ambiguity is refused when the importer is built rather
/// than found halfway through a file.
/// </remarks>
internal sealed class RuleReader
{
    private readonly Model _model;
    private readonly ConceptRegistry _concepts;
    private readonly IReadOnlyDictionary<string, ImportRule> _rules;
    private readonly string _text;

    private int _position;
    private int _depth;

    /// <summary>Reads <paramref name="text"/> into <paramref name="model"/> through the declared rules.</summary>
    public RuleReader(
        Model model,
        ConceptRegistry concepts,
        IReadOnlyDictionary<string, ImportRule> rules,
        string text)
    {
        _model = model;
        _concepts = concepts;
        _rules = rules;
        _text = text;
    }

    /// <summary>The place the text stopped making sense, or <see langword="null"/> while it still does.</summary>
    public ImportDiagnostic? Refusal { get; private set; }

    /// <summary>Text left over after the one document, or <see langword="null"/> when there is none.</summary>
    public ImportDiagnostic? Trailing { get; private set; }

    /// <summary>Reads one document, which is the root concept whose rule's anchor is at the cursor.</summary>
    /// <param name="roots">The concepts that may stand at the top of a document.</param>
    public Node? ParseRoot(IReadOnlyList<string> roots)
    {
        var rule = ChooseRoot(roots);

        if (rule is null)
        {
            return null;
        }

        var root = _model.NewNode(rule.ConceptKey);

        if (!ParseRule(rule, root, follow: null))
        {
            return null;
        }

        if (FirstNonWhitespace(_position) is { } trailing)
        {
            Trailing = Diagnostic(
                trailing,
                DeclaredImporter.TrailingText,
                "There is text after the document, and a rule reads one document.");
        }

        return root;
    }

    /// <summary>Reads one rule into <paramref name="node"/>, up to <paramref name="follow"/>.</summary>
    private bool ParseRule(ImportRule rule, Node node, string? follow)
    {
        if (++_depth > 200)
        {
            Refusal = Diagnostic(_position, DeclaredImporter.UnexpectedText, "The rules nest without end.");

            return false;
        }

        try
        {
            for (var index = 0; index < rule.Parts.Count; index++)
            {
                var part = rule.Parts[index];
                var next = FirstLiteral(rule.Parts, index + 1) ?? follow;

                switch (part.Kind)
                {
                    case ImportPartKind.Literal when !Expect(part.Text):
                        return false;

                    case ImportPartKind.Capture when !Capture(node, part.Slot, next):
                        return false;

                    case ImportPartKind.Nest when !ParseEntry(node, part.Slot, next):
                        return false;

                    case ImportPartKind.Repeat when !ParseRepeat(node, part, follow):
                        return false;
                }
            }

            return true;
        }
        finally
        {
            _depth--;
        }
    }

    /// <summary>Reads one child of <paramref name="slot"/>, by whichever rule the slot allows.</summary>
    private bool ParseEntry(Node owner, string slot, string? follow)
    {
        if (ChooseRule(owner, slot) is not { } rule)
        {
            return false;
        }

        return ParseRule(rule, _model.NewChild(owner, slot, rule.ConceptKey), follow);
    }

    /// <summary>Reads the entries of a list while the entry's anchor is still written.</summary>
    private bool ParseRepeat(Node owner, ImportPart part, string? follow)
    {
        var written = 0;

        while (true)
        {
            var rule = ChooseRule(owner, part.Slot, optional: written > 0);

            if (rule is null)
            {
                break;
            }

            if (written > 0 && part.Separator.Length > 0 && !Expect(part.Separator))
            {
                return false;
            }

            var start = _position;

            if (!ParseRule(rule, _model.NewChild(owner, part.Slot, rule.ConceptKey), FirstLiteral(part, follow)))
            {
                return false;
            }

            if (_position == start)
            {
                Refusal = Diagnostic(_position, DeclaredImporter.UnexpectedText, "The repeat reads nothing, so it would never end.");

                return false;
            }

            written++;
        }

        if (written >= part.Minimum)
        {
            return true;
        }

        Refusal = Diagnostic(
            _position,
            DeclaredImporter.TooFew,
            $"'{part.Slot}' needs at least {part.Minimum} entr(y/ies) and the text holds {written}.");

        return false;
    }

    /// <summary>The separator of a repeat taken as the follow of one entry, or the outer follow when it has none.</summary>
    private static string? FirstLiteral(ImportPart part, string? follow) =>
        part.Separator.Length > 0 ? part.Separator : follow;

    private ImportRule? ChooseRoot(IReadOnlyList<string> roots)
    {
        ImportRule? fallback = null;

        foreach (var key in roots)
        {
            if (!_rules.TryGetValue(key, out var rule))
            {
                continue;
            }

            if (rule.Anchor.Length == 0)
            {
                fallback ??= rule;

                continue;
            }

            if (StartsWith(rule.Anchor))
            {
                return rule;
            }
        }

        if (fallback is not null)
        {
            return fallback;
        }

        Refusal = Diagnostic(_position, DeclaredImporter.UnexpectedText, $"No rule begins with '{Snippet()}'.");

        return null;
    }

    /// <summary>The rule of whichever concept the slot allows whose anchor is at the cursor.</summary>
    /// <param name="owner">The node whose slot is being filled.</param>
    /// <param name="slot">The child slot.</param>
    /// <param name="optional">Whether "nothing here" is an answer rather than a refusal.</param>
    private ImportRule? ChooseRule(Node owner, string slot, bool optional = false)
    {
        var declaration = owner.Concept.FindChild(slot)
            ?? throw new ConceptDefinitionException($"The rule of '{owner.Concept.Key}' names '{slot}', which is not a child slot.");

        ImportRule? fallback = null;

        foreach (var candidate in _concepts.Concepts)
        {
            if (candidate.IsAbstract || !candidate.IsKindOf(declaration.ChildConceptKey))
            {
                continue;
            }

            if (!_rules.TryGetValue(candidate.Key, out var rule))
            {
                continue;
            }

            if (rule.Anchor.Length == 0)
            {
                fallback ??= rule;

                continue;
            }

            if (StartsWith(rule.Anchor))
            {
                return rule;
            }
        }

        if (fallback is not null)
        {
            return fallback;
        }

        if (optional)
        {
            return null;
        }

        Refusal = Diagnostic(
            _position,
            DeclaredImporter.UnexpectedText,
            $"'{Snippet()}' is where a rule for '{declaration.ChildConceptKey}' was expected.");

        return null;
    }

    private bool StartsWith(string literal) =>
        _position + literal.Length <= _text.Length
        && string.CompareOrdinal(_text, _position, literal, 0, literal.Length) == 0;

    private bool Expect(string literal)
    {
        if (StartsWith(literal))
        {
            _position += literal.Length;

            return true;
        }

        Refusal = Diagnostic(_position, DeclaredImporter.UnexpectedText, $"'{Snippet()}' is where '{literal}' was expected.");

        return false;
    }

    private bool Capture(Node node, string slot, string? terminator)
    {
        if (node.Concept.FindProperty(slot) is null)
        {
            Refusal = Diagnostic(_position, DeclaredImporter.UnexpectedText, $"'{slot}' is not a property of '{node.Concept.Key}'.");

            return false;
        }

        var end = terminator is null ? _text.Length : _text.IndexOf(terminator, _position, StringComparison.Ordinal);

        if (end < 0)
        {
            Refusal = Diagnostic(
                _position,
                DeclaredImporter.EndsEarly,
                $"The text ends before '{terminator}', so '{slot}' cannot be read.");

            return false;
        }

        node.Set(slot, _text[_position..end]);
        _position = end;

        return true;
    }

    /// <summary>The first literal of the parts from <paramref name="index"/> on, or null when there is none.</summary>
    private static string? FirstLiteral(IReadOnlyList<ImportPart> parts, int index) =>
        index < parts.Count && parts[index].Kind == ImportPartKind.Literal ? parts[index].Text : null;

    private int? FirstNonWhitespace(int from)
    {
        for (var index = from; index < _text.Length; index++)
        {
            if (!char.IsWhiteSpace(_text[index]))
            {
                return index;
            }
        }

        return null;
    }

    private string Snippet() =>
        _position >= _text.Length ? "the end of the text" : _text[_position..Math.Min(_position + 12, _text.Length)];

    private ImportDiagnostic Diagnostic(int offset, string code, string message)
    {
        var line = 1;
        var column = 1;

        for (var index = 0; index < offset && index < _text.Length; index++)
        {
            if (_text[index] == '\n')
            {
                line++;
                column = 1;

                continue;
            }

            column++;
        }

        return new ImportDiagnostic(code, message, line, column);
    }
}

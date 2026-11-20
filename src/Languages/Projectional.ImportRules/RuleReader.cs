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
    private readonly IReadOnlyDictionary<string, ImportToken> _tokens;
    private readonly string _text;

    private int _position;
    private int _depth;
    private bool _endAllowed;

    /// <summary>Reads <paramref name="text"/> into <paramref name="model"/> through the declared rules.</summary>
    public RuleReader(
        Model model,
        ConceptRegistry concepts,
        IReadOnlyDictionary<string, ImportRule> rules,
        IReadOnlyDictionary<string, ImportToken> tokens,
        string text)
    {
        _model = model;
        _concepts = concepts;
        _rules = rules;
        _tokens = tokens;
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
        // A file may open with the trivia an importer declares - a licence comment, a blank line - so the root is
        // chosen after it, not before it.
        SkipTrivia();

        var rule = ChooseRoot(roots);

        if (rule is null)
        {
            return null;
        }

        var root = _model.NewNode(rule.ConceptKey);

        _endAllowed = true;

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
                // An importer that declares a skipped class reads a stream of tokens, so what stands between the
                // parts is not part of the text: whitespace and comments go before anything is matched.
                SkipTrivia();

                var part = rule.Parts[index];
                var next = FirstLiteral(rule.Parts, index + 1) ?? follow;

                switch (part.Kind)
                {
                    case ImportPartKind.Literal when !Expect(part.Text):
                        return false;

                    // The two captures are exclusive: a class was named, so the token reader answers and the run to the
                    // next literal never runs - otherwise a successful token read would fall through and overwrite the
                    // value with the rest of the text.
                    case ImportPartKind.Capture when part.Token.Length > 0:
                        if (!CaptureToken(node, part))
                        {
                            return false;
                        }

                        break;

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
            // The separator stands between entries, so it is read before the next one is chosen: looking for an
            // entry first would stop the list at its own separator, because that text is not an anchor.
            if (written > 0 && part.Separator.Length > 0)
            {
                if (!StartsWith(part.Separator))
                {
                    break;
                }

                _position += part.Separator.Length;
            }

            SkipTrivia();

            var rule = ChooseRule(owner, part.Slot, optional: written == 0);

            if (rule is null)
            {
                break;
            }

            var start = _position;
            var was = _endAllowed;

            _endAllowed = true;

            var parsed = ParseRule(rule, _model.NewChild(owner, part.Slot, rule.ConceptKey), FirstLiteral(part, follow));

            _endAllowed = was;

            if (!parsed)
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

    /// <summary>Steps over whatever the importer declares as producing no node.</summary>
    /// <remarks>
    /// Nothing is skipped unless a class says so, which is why an importer written before the token layer keeps reading
    /// its own exact spelling: whitespace is text until a language calls it trivia.
    /// </remarks>
    private void SkipTrivia()
    {
        while (true)
        {
            var matched = false;

            foreach (var token in _tokens.Values)
            {
                if (token.Kind != ImportTokenKind.Skipped)
                {
                    continue;
                }

                var after = Skip(token);

                if (after > _position)
                {
                    _position = after;
                    matched = true;

                    break;
                }
            }

            if (!matched)
            {
                return;
            }
        }
    }

    /// <summary>The offset past one skipped run at the cursor, or the cursor when there is none.</summary>
    private int Skip(ImportToken token)
    {
        if (token.Prefix.Length == 0)
        {
            var at = _position;

            while (at < _text.Length && char.IsWhiteSpace(_text[at]))
            {
                at++;
            }

            return at;
        }

        if (!StartsWith(token.Prefix))
        {
            return _position;
        }

        if (token.Until.Length == 0)
        {
            return _text.Length;
        }

        var end = _text.IndexOf(token.Until, _position + token.Prefix.Length, StringComparison.Ordinal);

        return end < 0 ? _text.Length : end;
    }

    /// <summary>Reads one token of the class <paramref name="part"/> names into its property.</summary>
    private bool CaptureToken(Node node, ImportPart part)
    {
        if (node.Concept.FindProperty(part.Slot) is null)
        {
            Refusal = Diagnostic(_position, DeclaredImporter.UnexpectedText, $"'{part.Slot}' is not a property of '{node.Concept.Key}'.");

            return false;
        }

        var start = _position;
        var value = _tokens[part.Token] switch
        {
            { Kind: ImportTokenKind.Word } word => ReadWord(word),
            { Kind: ImportTokenKind.Number } => ReadNumber(),
            { Kind: ImportTokenKind.Quoted } quoted => ReadQuoted(quoted),
            var other => throw new ConceptDefinitionException($"'{other.Name}' is a token class a capture cannot read."),
        };

        if (value is null || _position == start)
        {
            Refusal = Diagnostic(
                start,
                DeclaredImporter.UnexpectedText,
                $"'{Snippet(start)}' is where a '{part.Token}' was expected.");

            return false;
        }

        node.Set(part.Slot, value);

        return true;
    }

    private string? ReadWord(ImportToken token)
    {
        var start = _position;

        while (_position < _text.Length
            && (char.IsLetterOrDigit(_text[_position]) || _text[_position] == '_' || token.Extra.Contains(_text[_position], StringComparison.Ordinal)))
        {
            _position++;
        }

        return _position == start ? null : _text[start.._position];
    }

    private string? ReadNumber()
    {
        var start = _position;

        if (_position < _text.Length && (_text[_position] == '-' || _text[_position] == '+'))
        {
            _position++;
        }

        var digits = 0;

        while (_position < _text.Length && (char.IsAsciiDigit(_text[_position]) || _text[_position] is '.' or 'e' or 'E'
            || ((_text[_position] == '-' || _text[_position] == '+') && _position > start && _text[_position - 1] is 'e' or 'E')))
        {
            if (char.IsAsciiDigit(_text[_position]))
            {
                digits++;
            }

            _position++;
        }

        return digits == 0 ? null : _text[start.._position];
    }

    private string? ReadQuoted(ImportToken token)
    {
        if (_position >= _text.Length || !StartsWith(token.Quote))
        {
            return null;
        }

        var value = new System.Text.StringBuilder();
        var at = _position + token.Quote.Length;

        while (at < _text.Length)
        {
            if (string.CompareOrdinal(_text, at, token.Quote, 0, token.Quote.Length) == 0)
            {
                _position = at + token.Quote.Length;

                return value.ToString();
            }

            if (token.Escape.Length > 0 && string.CompareOrdinal(_text, at, token.Escape, 0, token.Escape.Length) == 0)
            {
                at += token.Escape.Length;

                if (at < _text.Length)
                {
                    value.Append(_text[at]);
                    at++;
                }

                continue;
            }

            value.Append(_text[at]);
            at++;
        }

        return null;
    }
    private bool Capture(Node node, string slot, string? terminator)
    {
        if (node.Concept.FindProperty(slot) is null)
        {
            Refusal = Diagnostic(_position, DeclaredImporter.UnexpectedText, $"'{slot}' is not a property of '{node.Concept.Key}'.");

            return false;
        }

        var end = terminator is null ? _text.Length : _text.IndexOf(terminator, _position, StringComparison.Ordinal);

        if (end < 0 && _endAllowed)
        {
            end = _text.Length;
        }

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
    /// <summary>The text that ends a run from <paramref name="index" /> on, or null when nothing does.</summary>
    /// <remarks>
    /// The next literal, or the separator of a repeat, because a capture that stands before a list ends where the list
    /// starts - the same question a template's parts answer, asked of a rule's.
    /// </remarks>
    private static string? FirstLiteral(IReadOnlyList<ImportPart> parts, int index)
    {
        for (var look = index; look < parts.Count; look++)
        {
            if (parts[look].Kind == ImportPartKind.Literal)
            {
                return parts[look].Text;
            }

            if (parts[look].Kind == ImportPartKind.Repeat && parts[look].Separator.Length > 0)
            {
                return parts[look].Separator;
            }
        }

        return null;
    }

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

    private string Snippet(int from) =>
        from >= _text.Length ? "the end of the text" : _text[from..Math.Min(from + 12, _text.Length)];

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

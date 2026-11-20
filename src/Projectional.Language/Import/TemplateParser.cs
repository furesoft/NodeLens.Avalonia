using Projectional.Core;

namespace Projectional.Language;

/// <summary>Reads text by walking a generator's templates, deciding every choice at the cursor.</summary>
/// <remarks>
/// <para>
/// The parser is the inverse of <c>Generator</c>: where that one writes a property, this one captures up to the next
/// literal; where that one expands a child, this one picks the concept whose template's literal prefix is at the
/// cursor; where that one repeats a list between separators, this one repeats until the separator stops matching.
/// Nothing is guessed and nothing is undone, which is what makes every ambiguity a load-time refusal rather than a
/// wrong tree found halfway through a file.
/// </para>
/// <para>
/// One writer and one reader per generator is the point: a change to a template changes both sides at once, so a
/// language cannot print a spelling it cannot read.
/// </para>
/// </remarks>
internal sealed class TemplateParser
{
    private readonly Model _model;
    private readonly ConceptRegistry _concepts;
    private readonly TextGenerator _generator;
    private readonly string _text;

    private int _position;
    private int _indent;
    private int _depth;
    private bool _endAllowed;

    /// <summary>Reads <paramref name="text"/> into <paramref name="model"/> through <paramref name="generator"/>.</summary>
    /// <param name="model">The model the host created.</param>
    /// <param name="concepts">The language's concepts.</param>
    /// <param name="generator">The templates to invert.</param>
    /// <param name="text">The whole text.</param>
    public TemplateParser(Model model, ConceptRegistry concepts, TextGenerator generator, string text)
    {
        _model = model;
        _concepts = concepts;
        _generator = generator;
        _text = text;
    }

    /// <summary>The place the text stopped making sense, or <see langword="null"/> while it still does.</summary>
    public ImportDiagnostic? Refusal { get; private set; }

    /// <summary>Text left over after the one document, or <see langword="null"/> when there is none.</summary>
    public ImportDiagnostic? Trailing { get; private set; }

    /// <summary>Reads one document, which is the first root concept whose template's prefix is at the cursor.</summary>
    /// <param name="roots">The concepts that may stand at the top of a document.</param>
    /// <returns>The root, or <see langword="null"/> when the text did not start like one.</returns>
    public Node? ParseRoot(IReadOnlyList<string> roots)
    {
        if (Choose(roots, static root => root) is not { } conceptKey)
        {
            return null;
        }

        var concept = _concepts.Get(conceptKey);
        var template = _generator.Templates.TemplateFor(concept)!;
        var root = _model.NewNode(conceptKey);

        _endAllowed = true;

        if (!ParseTemplate(template, root, follow: null))
        {
            return null;
        }

        _endAllowed = false;

        if (FirstNonWhitespace(_position) is { } trailing)
        {
            Trailing = Diagnostic(
                trailing,
                TemplateImporter.TrailingText,
                "There is text after the document, and a generator writes one document.");
        }

        return root;
    }

    /// <summary>Refuses the templates this parser could not decide, before anyone imports a file with them.</summary>
    /// <param name="generator">The generator to check.</param>
    /// <param name="concepts">The language's concepts.</param>
    /// <param name="roots">The concepts that may stand at the top of a document.</param>
    /// <exception cref="ConceptDefinitionException">A template cannot be read back.</exception>
    public static void Validate(TextGenerator generator, ConceptRegistry concepts, IReadOnlyList<string> roots)
    {
        ArgumentNullException.ThrowIfNull(generator);
        ArgumentNullException.ThrowIfNull(concepts);
        ArgumentNullException.ThrowIfNull(roots);

        if (roots.Count == 0)
        {
            throw NotInvertible(generator, "no concept is named as a document root");
        }

        var visited = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<Concept>();

        foreach (var root in roots)
        {
            if (!concepts.TryGet(root, out var concept) || generator.Templates.TemplateFor(concept) is null)
            {
                throw NotInvertible(generator, $"'{root}' is a document root but has no template");
            }

            queue.Enqueue(concept);
        }

        while (queue.Count > 0)
        {
            var concept = queue.Dequeue();

            if (!visited.Add(concept.Key) || generator.Templates.TemplateFor(concept) is not { } template)
            {
                continue;
            }

            foreach (var part in Whole(template).ToList())
            {
                switch (part)
                {
                    case ParentPart:
                    case ReferencePart:
                        throw NotInvertible(
                            generator,
                            $"the template of '{concept.Key}' names a {Kind(part)}, which writes something the node does "
                            + "not own and has nothing to read into");

                    case WhenFilledPart whenFilled:
                        RequireDecidable(generator, concept, whenFilled.Then, whenFilled.Otherwise, "when filled");
                        break;

                    case ConditionalPart conditional:
                        RequireDecidable(generator, concept, conditional.Then, conditional.Otherwise, "conditional");
                        break;

                    case ChildPart child:
                        foreach (var candidate in Candidates(generator, concepts, concept, child.Slot))
                        {
                            queue.Enqueue(candidate);
                        }

                        break;

                    case ChildrenPart children:
                        foreach (var candidate in Candidates(generator, concepts, concept, children.Slot))
                        {
                            queue.Enqueue(candidate);
                        }

                        break;

                    case LoopPart loop:
                        foreach (var candidate in Candidates(generator, concepts, concept, loop.Slot))
                        {
                            queue.Enqueue(candidate);
                        }

                        break;
                }
            }
        }
    }

    /// <summary>The concepts that may stand in <paramref name="slot"/>, refusing a set the text cannot tell apart.</summary>
    /// <exception cref="ConceptDefinitionException">The slot cannot be read back.</exception>
    internal static IReadOnlyList<Concept> Candidates(
        TextGenerator generator,
        ConceptRegistry concepts,
        Concept owner,
        string slot)
    {
        var declaration = owner.FindChild(slot)
            ?? throw NotInvertible(generator, $"'{slot}' is not a child slot of '{owner.Key}'");

        var candidates = concepts.Concepts
            .Where(candidate => !candidate.IsAbstract && candidate.IsKindOf(declaration.ChildConceptKey))
            .Where(candidate => generator.Templates.TemplateFor(candidate) is not null)
            .ToList();

        if (candidates.Count == 0)
        {
            throw NotInvertible(
                generator,
                $"no concrete concept below '{declaration.ChildConceptKey}' has a template, so '{slot}' of '{owner.Key}' "
                + "cannot be read");
        }

        if (candidates.Any(candidate => generator.Templates.TemplateFor(candidate) is { IsEmpty: true }))
        {
            throw NotInvertible(
                generator,
                $"a concept below '{declaration.ChildConceptKey}' has an empty template, so its entry in '{slot}' of "
                + $"'{owner.Key}' leaves no trace to read");
        }

        RequireDistinct(generator, $"{slot}' of '{owner.Key}", candidates.Select(candidate => LiteralPrefix(generator.Templates.TemplateFor(candidate)!)));

        return candidates;
    }

    /// <summary>The literal a template emits before it needs the node, or empty when it needs one first.</summary>
    internal static string LiteralPrefix(TextTemplate template)
    {
        var text = new System.Text.StringBuilder();

        foreach (var part in template.Parts)
        {
            if (part is TextPart literal)
            {
                text.Append(literal.Text);

                continue;
            }

            if (part is LineBreakPart)
            {
                return text.Append('\n').ToString();
            }

            break;
        }

        return text.ToString();
    }

    /// <summary>Every part of <paramref name="template"/>, descending into the parts that hold one.</summary>
    internal static IEnumerable<TemplatePart> Whole(TextTemplate template)
    {
        foreach (var part in template.Parts)
        {
            yield return part;

            foreach (var nested in part switch
            {
                IndentPart indent => Whole(indent.Body),
                WhenFilledPart whenFilled => Whole(whenFilled.Then).Concat(Whole(whenFilled.Otherwise)),
                ConditionalPart conditional => Whole(conditional.Then).Concat(Whole(conditional.Otherwise)),
                ChildrenPart children => Whole(children.Separator),
                LoopPart loop => Whole(loop.Body).Concat(Whole(loop.Separator)),
                _ => [],
            })
            {
                yield return nested;
            }
        }
    }

    /// <summary>Reads one template into <paramref name="node"/>, up to <paramref name="follow"/>.</summary>
    /// <param name="template">What to read.</param>
    /// <param name="node">The node whose slots the parts fill.</param>
    /// <param name="follow">The literal that ends this template, or <see langword="null"/> for the end of the text.</param>
    private bool ParseTemplate(TextTemplate template, Node node, string? follow)
    {
        if (++_depth > 200)
        {
            Refusal = Diagnostic(_position, TemplateImporter.UnexpectedText, "The template expands itself without end.");

            return false;
        }

        try
        {
            var parts = template.Parts;

            for (var index = 0; index < parts.Count; index++)
            {
                var next = FirstLiteral(parts, index + 1) ?? follow;

                switch (parts[index])
                {
                    case TextPart literal when !Expect(literal.Text):
                        return false;

                    case LineBreakPart when !ExpectBreak():
                        return false;

                    case PropertyPart property when !Capture(node, property.Slot, next):
                        return false;

                    case IndentPart indent:
                        _indent++;

                        var indented = ParseTemplate(indent.Body, node, follow);

                        _indent--;

                        if (!indented)
                        {
                            return false;
                        }

                        break;

                    case ChildPart child:
                        if (ParseEntry(node, child.Slot, next) is false)
                        {
                            return false;
                        }

                        break;

                    case ChildrenPart children:
                        if (!ParseList(node, children.Slot, children.Separator, follow, body: null))
                        {
                            return false;
                        }

                        break;

                    case LoopPart loop:
                        if (!ParseList(node, loop.Slot, loop.Separator, follow, loop.Body))
                        {
                            return false;
                        }

                        break;

                    case WhenFilledPart whenFilled:
                        if (!ParseTemplate(Branch(whenFilled.Then, whenFilled.Otherwise), node, follow))
                        {
                            return false;
                        }

                        break;

                    case ConditionalPart conditional:
                        if (!ParseTemplate(Branch(conditional.Then, conditional.Otherwise), node, follow))
                        {
                            return false;
                        }

                        break;

                    case SelfPart:
                        if (!ParseTemplate(
                            _generator.Templates.TemplateFor(node.Concept)!,
                            node,
                            follow))
                        {
                            return false;
                        }

                        break;
                }
            }

            return true;
        }
        finally
        {
            _depth--;
        }
    }

    /// <summary>Reads one child of <paramref name="slot"/> into a new node.</summary>
    private bool ParseEntry(Node owner, string slot, string? follow)
    {
        if (Choose(Candidates(_generator, _concepts, owner.Concept, slot), static candidate => candidate.Key) is not { } key)
        {
            return false;
        }

        var concept = _concepts.Get(key);
        var child = _model.NewChild(owner, slot, key);

        return ParseTemplate(_generator.Templates.TemplateFor(concept)!, child, follow);
    }

    /// <summary>Reads a list slot until the separator no longer matches.</summary>
    /// <param name="owner">The node owning the list.</param>
    /// <param name="slot">The many-valued slot.</param>
    /// <param name="separator">What stands between two entries.</param>
    /// <param name="follow">The literal that ends the list, or <see langword="null"/> for the end of the text.</param>
    /// <param name="body">A loop body applied to the entry instead of the entry's own template.</param>
    private bool ParseList(Node owner, string slot, TextTemplate separator, string? follow, TextTemplate? body)
    {
        var between = FirstLiteral(separator.Parts, 0);
        var candidates = Candidates(_generator, _concepts, owner.Concept, slot);
        var written = 0;

        while (true)
        {
            if (AtEnd || (follow is { } stop && StartsWith(stop)))
            {
                return true;
            }

            if (written > 0 && between is { } literal && !Expect(literal))
            {
                return false;
            }

            var start = _position;

            if (Choose(candidates, static candidate => candidate.Key) is not { } key)
            {
                return false;
            }

            var concept = _concepts.Get(key);
            var child = _model.NewChild(owner, slot, key);
            var template = body ?? _generator.Templates.TemplateFor(concept)!;
            var was = _endAllowed;

            _endAllowed = true;

            var parsed = ParseTemplate(template, child, between ?? follow);

            _endAllowed = was;

            if (!parsed)
            {
                return false;
            }

            if (_position == start)
            {
                Refusal = Diagnostic(_position, TemplateImporter.UnexpectedText, "The list repeats without reading anything.");

                return false;
            }

            written++;
        }
    }

    /// <summary>The branch a conditional takes, decided by the literal at the cursor.</summary>
    private TextTemplate Branch(TextTemplate then, TextTemplate otherwise)
    {
        var first = LiteralPrefix(then);
        var second = LiteralPrefix(otherwise);

        if (first.Length > 0 && StartsWith(first))
        {
            return then;
        }

        return second.Length > 0 && StartsWith(second)
            ? otherwise
            : first.Length == 0 ? then : otherwise;
    }

    /// <summary>Picks the candidate whose literal prefix is at the cursor, or the one without a prefix.</summary>
    private string? Choose(IReadOnlyList<Concept> candidates, Func<Concept, string> key) =>
        Choose(candidates.Select(candidate => (Concept: candidate, Key: key(candidate))).ToList());

    private string? Choose(IReadOnlyList<string> roots, Func<string, string> key) =>
        Choose(roots.Select(root => (Concept: _concepts.Get(root), Key: key(root))).ToList());

    private string? Choose(IReadOnlyList<(Concept Concept, string Key)> candidates)
    {
        string? fallback = null;

        foreach (var (concept, key) in candidates)
        {
            var prefix = LiteralPrefix(_generator.Templates.TemplateFor(concept)!);

            if (prefix.Length == 0)
            {
                fallback ??= key;

                continue;
            }

            if (StartsWith(prefix))
            {
                return key;
            }
        }

        if (fallback is not null)
        {
            return fallback;
        }

        Refusal = Diagnostic(
            _position,
            TemplateImporter.UnexpectedText,
            $"No template of this slot begins with '{Snippet()}'.");

        return null;
    }

    /// <summary>Whether the text at the cursor is <paramref name="literal"/>.</summary>
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

        Refusal = Diagnostic(
            _position,
            TemplateImporter.UnexpectedText,
            $"'{Snippet()}' is where '{literal}' was expected.");

        return false;
    }

    /// <summary>Reads a line break and the indentation the current level asks for.</summary>
    private bool ExpectBreak()
    {
        if (!StartsWith("\n"))
        {
            Refusal = Diagnostic(_position, TemplateImporter.UnexpectedText, $"'{Snippet()}' is where a line break was expected.");

            return false;
        }

        _position++;

        for (var spaces = 0; spaces < _indent * 2; spaces++)
        {
            if (AtEnd || _text[_position] != ' ')
            {
                Refusal = Diagnostic(_position, TemplateImporter.UnexpectedText, $"'{Snippet()}' is where indentation was expected.");

                return false;
            }

            _position++;
        }

        return true;
    }

    /// <summary>Reads a run into <paramref name="slot"/> of <paramref name="node"/>, up to <paramref name="terminator"/>.</summary>
    private bool Capture(Node node, string slot, string? terminator)
    {
        if (node.Concept.FindProperty(slot) is null)
        {
            Refusal = Diagnostic(_position, TemplateImporter.UnexpectedText, $"'{slot}' is not a property of '{node.Concept.Key}'.");

            return false;
        }

        if (terminator is null)
        {
            node.Set(slot, _text[_position..]);
            _position = _text.Length;

            return true;
        }

        var end = terminator is null ? _text.Length : _text.IndexOf(terminator, _position, StringComparison.Ordinal);

        if (end < 0)
        {
            if (!_endAllowed)
            {
                Refusal = Diagnostic(
                    _position,
                    TemplateImporter.EndsEarly,
                    $"The text ends before '{terminator}', so '{slot}' cannot be read.");

                return false;
            }

            end = _text.Length;
        }

        node.Set(slot, _text[_position..end]);
        _position = end;

        return true;
    }

    private bool AtEnd => _position >= _text.Length;

    /// <summary>The first literal from <paramref name="index"/> on, or <see langword="null"/> when there is none.</summary>
    private static string? FirstLiteral(IReadOnlyList<TemplatePart> parts, int index)
    {
        if (index >= parts.Count)
        {
            return null;
        }

        return parts[index] switch
        {
            TextPart literal => literal.Text,
            LineBreakPart => "\n",
            _ => null,
        };
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

    private static ConceptDefinitionException NotInvertible(TextGenerator generator, string reason) =>
        new($"The generator '{generator.Name}' cannot be read back: {reason}.");

    /// <summary>Refuses a choice whose branches the text cannot tell apart.</summary>
    /// <remarks>
    /// At most one branch may have no literal prefix, because that branch is the fallback; two branches with the same
    /// prefix are the same text, so the file would not say which was meant.
    /// </remarks>
    private static void RequireDecidable(
        TextGenerator generator,
        Concept concept,
        TextTemplate then,
        TextTemplate otherwise,
        string what)
    {
        var first = LiteralPrefix(then);
        var second = LiteralPrefix(otherwise);

        if (first.Length == 0 && second.Length == 0)
        {
            throw NotInvertible(
                generator,
                $"the {what} in the template of '{concept.Key}' has two branches that begin with no text at all, so "
                + "neither can be chosen from the file");
        }

        if (first.Length > 0 && second.Length > 0
            && (first.StartsWith(second, StringComparison.Ordinal) || second.StartsWith(first, StringComparison.Ordinal)))
        {
            throw NotInvertible(
                generator,
                $"the {what} in the template of '{concept.Key}' has two branches whose text begins alike ('{first}' and "
                + $"'{second}'), so which one was written cannot be told");
        }

        if (first.Length > 0 && string.Equals(first, second, StringComparison.Ordinal))
        {
            throw NotInvertible(
                generator,
                $"the {what} in the template of '{concept.Key}' has two branches that both begin with '{first}', so "
                + "which one was written cannot be told");
        }
    }

    /// <summary>Refuses a set of prefixes that cannot be told apart at the cursor.</summary>
    private static void RequireDistinct(TextGenerator generator, string where, IEnumerable<string> prefixes)
    {
        var all = prefixes.ToList();

        if (all.Count(prefix => prefix.Length == 0) > 1)
        {
            throw NotInvertible(
                generator,
                $"'{where}' holds two concepts whose templates begin with no text at all, so the fallback is ambiguous");
        }

        var duplicated = all
            .Where(prefix => prefix.Length > 0)
            .GroupBy(prefix => prefix, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicated is not null)
        {
            throw NotInvertible(
                generator,
                $"'{where}' holds two concepts whose templates begin with '{duplicated.Key}', so which was written "
                + "cannot be told");
        }
    }

    private static string Kind(TemplatePart part) =>
        part switch
        {
            ParentPart => "holding node's property",
            ReferencePart => "reference",
            _ => part.GetType().Name,
        };
}

using Projectional.Core;

namespace Projectional.Language;

/// <summary>Reads back the text a generator's templates write, so a language is not a one-way street.</summary>
/// <remarks>
/// <para>
/// A language already says how it is written twice - the notation says how it is drawn, the generator says how it is
/// printed - and a third grammar would be a second statement of the same syntax, free to drift from both. This
/// importer therefore reads the generator's own templates rather than a grammar: a literal must be there, a property
/// part is the run up to the next literal, a child part is the template of whichever concept the slot allows, a list
/// repeats until its separator stops matching.
/// </para>
/// <para>
/// <b>There is no backtracking, and that is the design rather than an omission.</b> Every choice - which concept a
/// child slot holds, which branch of a conditional or a "when filled" applies, whether a list has another entry - is
/// decided by the literal text at the cursor, so no construct has to be tried and undone. A choice the text cannot
/// decide is refused when the importer is built, not discovered halfway through a file, which is the rule the
/// notation registry and the declared generator already follow: the earliest moment both halves exist.
/// </para>
/// <para>
/// <b>What it refuses is what a generator cannot write back.</b> A <see cref="ParentPart"/> emits a value the child
/// does not own, and a <see cref="ReferencePart"/> emits a target's name, so neither can be filled from the text
/// without the scope chain resolution needs; a template naming one is refused as a whole, with a message naming the
/// concept. Everything else in the closed vocabulary inverts: text, line break, indent, property, child, list, loop,
/// conditional, when-filled and self.
/// </para>
/// <para>
/// <b>The file is what the generator writes.</b> A line break part emits a newline and the current indentation, so the
/// importer expects exactly that and refuses whitespace the generator never writes - "the same bytes back" is the only
/// promise that can be checked against the generator. Whitespace after the one document is the single exception, since
/// a text file ends in a newline far more often than not.
/// </para>
/// </remarks>
public sealed class TemplateImporter : ITextImporter
{
    /// <summary>Text where the generator's next literal was expected.</summary>
    public const string UnexpectedText = "template.unexpected-text";

    /// <summary>The text ended before a template was finished.</summary>
    public const string EndsEarly = "template.ends-early";

    /// <summary>Characters after the one document.</summary>
    public const string TrailingText = "template.trailing-text";

    /// <summary>Nothing at all.</summary>
    public const string EmptyText = "template.empty-text";

    private readonly ConceptRegistry _concepts;
    private readonly IReadOnlyList<string> _roots;

    /// <summary>Builds the inverse of one generator.</summary>
    /// <param name="format">The name a host's file dialog filters on, such as <c>shell-script</c>.</param>
    /// <param name="generator">The templates to read.</param>
    /// <param name="concepts">The language's concepts, which decide what a slot may hold.</param>
    /// <param name="roots">The concepts that may stand at the top of a document, in the order to try.</param>
    /// <exception cref="ConceptDefinitionException">A template cannot be read back, and the message says why.</exception>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public TemplateImporter(
        string format,
        TextGenerator generator,
        ConceptRegistry concepts,
        IReadOnlyList<string> roots)
    {
        ArgumentException.ThrowIfNullOrEmpty(format);
        ArgumentNullException.ThrowIfNull(generator);
        ArgumentNullException.ThrowIfNull(concepts);
        ArgumentNullException.ThrowIfNull(roots);

        Format = format;
        Generator = generator;
        _concepts = concepts;
        _roots = roots;

        TemplateParser.Validate(generator, concepts, roots);
    }

    /// <summary>The name of the format, which is the generator's own name.</summary>
    public string Format { get; }

    /// <summary>The generator this importer reads back.</summary>
    public TextGenerator Generator { get; }

    /// <summary>The concepts that may stand at the top of a document, in the order the text is tried against them.</summary>
    /// <remarks>
    /// Exposed because it is the one fact about an importer a host cannot derive: which document roots it accepts is
    /// decided when it is built, from the generator's templates, and a file dialog or a documentation page that wants
    /// to say "this reads a document starting with a pipeline" has nowhere else to ask.
    /// </remarks>
    public IReadOnlyList<string> Roots => _roots;

    /// <summary>Whether the text starts like one of the concepts that may be a document.</summary>
    /// <remarks>
    /// A cheap shape check, never a promise: it compares the text against the literal prefixes of the root templates.
    /// The answer that counts is <see cref="Import"/>.
    /// </remarks>
    public bool CanImport(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        return _roots.Any(root => _concepts.TryGet(root, out var concept)
            && Generator.Templates.TemplateFor(concept) is { } template
            && text.AsSpan().StartsWith(TemplateParser.LiteralPrefix(template), StringComparison.Ordinal));
    }

    /// <summary>Reads <paramref name="text"/> into <paramref name="model"/> as one root, in one undo step.</summary>
    /// <param name="model">The model the host created; it is filled, never replaced.</param>
    /// <param name="text">The whole text, read by the host.</param>
    /// <returns>The root that was added, or the place the text stopped making sense.</returns>
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

        var parser = new TemplateParser(model, _concepts, Generator, text);

        if (parser.ParseRoot(_roots) is not { } root)
        {
            return ImportResult.Refused(parser.Refusal!);
        }

        if (parser.Trailing is { } trailing)
        {
            return ImportResult.Refused(trailing);
        }

        transaction.Complete();

        return ImportResult.Imported([root]);
    }
}

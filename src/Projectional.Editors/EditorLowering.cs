using Projectional.Core;
using Projectional.Language;

namespace Projectional.Editors;

/// <summary>
/// Turns a written notation into the <see cref="Notation"/>s the projection engine draws.
/// </summary>
/// <remarks>
/// <para>
/// The lowering goes through <see cref="NotationEditor"/> rather than assembling cells itself, and that is the
/// point: the declared path ends up calling <em>the same builder calls</em> a language author writes by hand, so a
/// declared notation is not a second kind of notation. It also means the defaults the builder applies (an empty
/// child's text, the spacing of a list) are decided in one place.
/// </para>
/// <para>
/// What it checks is only what the model cannot: that a notation names a concept the target registry declares.
/// Everything else - a line holding a block, a property cell naming a slot that does not exist, two notations for
/// one concept and variant - is refused by the model or by <see cref="NotationRegistry.Add(Notation)"/>, and a
/// second copy of any of those rules here would eventually disagree with it.
/// </para>
/// </remarks>
public static class EditorLowering
{
    /// <summary>The notations a definition describes.</summary>
    /// <param name="document">The notation, which is a root of <see cref="EditorConceptKeys.Notations"/>.</param>
    /// <param name="concepts">The language the notations are about.</param>
    /// <returns>One notation per <see cref="EditorConceptKeys.Notation"/> node, in the order they were written.</returns>
    /// <exception cref="ArgumentNullException">A parameter is <see langword="null"/>.</exception>
    /// <exception cref="ConceptDefinitionException">
    /// The document is not a notation, a notation names no concept, or it names one the registry does not declare.
    /// </exception>
    /// <remarks>
    /// The concept is checked here rather than left to the registry because the message can then say <em>which
    /// notation</em> is about a concept that does not exist, which is the one thing an author can act on.
    /// </remarks>
    public static IReadOnlyList<Notation> Lower(Node document, ConceptRegistry concepts)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(concepts);

        if (document.Concept.Key != EditorConceptKeys.Notations)
        {
            throw new ConceptDefinitionException(
                $"'{document.Concept.Key}' is not a notation; expected '{EditorConceptKeys.Notations}'.");
        }

        return document.GetChildren(EditorLanguage.NotationSlot).Select(one => LowerNotation(one, concepts)).ToList();
    }

    /// <summary>A registry holding those notations, which is what a session draws with.</summary>
    /// <param name="document">The notation.</param>
    /// <param name="concepts">The language the notations are about.</param>
    /// <exception cref="ConceptDefinitionException">The notation is malformed or draws something impossible.</exception>
    /// <exception cref="NotationException">A notation names a slot its concept does not declare, or repeats one.</exception>
    public static NotationRegistry CreateRegistry(Node document, ConceptRegistry concepts)
    {
        var notations = new NotationRegistry(concepts);

        foreach (var notation in Lower(document, concepts))
        {
            notations.Add(notation);
        }

        return notations;
    }

    private static Notation LowerNotation(Node notation, ConceptRegistry concepts)
    {
        var conceptKey = Required(notation, EditorLanguage.ConceptKeySlot);

        if (!concepts.TryGet(conceptKey, out _))
        {
            throw new ConceptDefinitionException(
                $"A notation draws '{conceptKey}', which the language does not declare.");
        }

        var variant = notation.GetOrNull("variant") is { Length: > 0 } named ? named : Notation.DefaultVariant;

        return new Notation(conceptKey, variant, LowerBlock(notation.GetChild(EditorLanguage.RootSlot), conceptKey));
    }

    /// <summary>A block cell and everything it stacks.</summary>
    /// <remarks>
    /// A notation whose root is missing answers an empty sequence rather than throwing: the model already refuses
    /// a notation without one, so this can only happen for a node built by hand, and an empty drawing is a better
    /// answer for a broken definition than an exception from a repaint.
    /// </remarks>
    private static BlockNotationCell LowerBlock(Node? cell, string conceptKey)
    {
        var ed = NotationEditor.Create();

        if (cell is null)
        {
            return ed.Sequence();
        }

        return cell.Concept.Key switch
        {
            EditorConceptKeys.Line => ed.Line([.. cell.GetChildren(EditorLanguage.CellSlot).Select(child => LowerInline(child, conceptKey))]),
            EditorConceptKeys.Sequence => new SequenceNotationCell([.. cell.GetChildren(EditorLanguage.CellSlot).Select(child => LowerBlock(child, conceptKey))]),
            EditorConceptKeys.Indent => new IndentNotationCell([.. cell.GetChildren(EditorLanguage.CellSlot).Select(child => LowerBlock(child, conceptKey))]),
            EditorConceptKeys.Children => ed.Children(
                Required(cell, "slot"),
                Optional(cell, "emptyText") ?? "<empty>",
                Spacing(cell)),
            _ => throw NotACell(cell, conceptKey, "not a block"),
        };
    }

    /// <summary>An inline cell, which is what a line may hold.</summary>
    private static InlineNotationCell LowerInline(Node cell, string conceptKey)
    {
        var ed = NotationEditor.Create();

        return cell.Concept.Key switch
        {
            EditorConceptKeys.Space => ed.Space,
            EditorConceptKeys.Keyword => ed.Keyword(Required(cell, "text")),
            EditorConceptKeys.Punct => ed.Punct(Required(cell, "text")),
            EditorConceptKeys.Property => ed.Property(Required(cell, "slot")),
            EditorConceptKeys.Reference => ed.Reference(Required(cell, "slot")),
            EditorConceptKeys.ChildInline => new SingleChildNotationCell(Required(cell, "slot")),
            _ => throw NotACell(cell, conceptKey, "not inline"),
        };
    }

    private static int Spacing(Node cell) =>
        int.TryParse(cell.GetOrNull("spacing"), out var spacing) ? spacing : 1;

    private static ConceptDefinitionException NotACell(Node cell, string conceptKey, string reason) =>
        new($"The notation of '{conceptKey}' holds a '{cell.Concept.Key}', which is {reason}.");

    private static string Required(Node node, string slot) =>
        node.GetOrNull(slot) is { Length: > 0 } value
            ? value
            : throw new ConceptDefinitionException(
                $"The notation of '{node.GetOrNull(EditorLanguage.ConceptKeySlot) ?? "?"}' leaves out its '{slot}'.");

    private static string? Optional(Node node, string slot) =>
        node.GetOrNull(slot) is { Length: > 0 } value ? value : null;
}

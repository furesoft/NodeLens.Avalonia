using System.Globalization;

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
    /// <summary>The notations a language's editor documents describe, in the order they were written.</summary>
    /// <param name="notations">
    /// The roots of those documents: one <see cref="EditorConceptKeys.Notation"/> per document, in file order.
    /// </param>
    /// <param name="concepts">The language the notations are about.</param>
    /// <returns>One notation per root, in that order.</returns>
    /// <exception cref="ArgumentNullException">A parameter is <see langword="null"/>.</exception>
    /// <exception cref="ConceptDefinitionException">
    /// A root is not a notation, a notation names no concept, or it names one the registry does not declare.
    /// </exception>
    /// <remarks>
    /// The concept is checked here rather than left to the registry because the message can then say <em>which
    /// notation</em> is about a concept that does not exist, which is the one thing an author can act on.
    /// </remarks>
    public static IReadOnlyList<Notation> Lower(IEnumerable<Node> notations, ConceptRegistry concepts)
    {
        ArgumentNullException.ThrowIfNull(notations);
        ArgumentNullException.ThrowIfNull(concepts);

        var roots = notations as IReadOnlyList<Node> ?? [.. notations];

        foreach (var root in roots)
        {
            if (root.Concept.Key != EditorConceptKeys.Notation)
            {
                throw new ConceptDefinitionException(
                    $"'{root.Concept.Key}' is not a notation; an editor document holds one '{EditorConceptKeys.Notation}'.");
            }
        }

        return roots.Select(one => LowerNotation(one, concepts)).ToList();
    }

    /// <summary>A registry holding those notations, which is what a session draws with.</summary>
    /// <param name="notations">The roots of the editor documents.</param>
    /// <param name="concepts">The language the notations are about.</param>
    /// <exception cref="ConceptDefinitionException">A notation is malformed or draws something impossible.</exception>
    /// <exception cref="NotationException">A notation names a slot its concept does not declare, or repeats one.</exception>
    public static NotationRegistry CreateRegistry(IEnumerable<Node> notations, ConceptRegistry concepts)
    {
        var registry = new NotationRegistry(concepts);

        foreach (var notation in Lower(notations, concepts))
        {
            registry.Add(notation);
        }

        return registry;
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
            EditorConceptKeys.Horizontal => new HorizontalNotationCell([.. cell.GetChildren(EditorLanguage.CellSlot).Select(child => LowerBlock(child, conceptKey))]),
            EditorConceptKeys.Children => ed.Children(
                Required(cell, "slot"),
                EmptyText(cell),
                Spacing(cell),
                IsTrue(cell, "stacked")),
            EditorConceptKeys.Collection => ed.Collection(
                Required(cell, "slot"),
                cell.GetChild("separator") is { } separator ? LowerInline(separator, conceptKey) : null,
                EmptyText(cell)),
            EditorConceptKeys.Table => ed.Table(
                Required(cell, "slot"),
                EmptyText(cell),
                [.. cell.GetChildren(EditorLanguage.ColumnSlot).Select(column => LowerColumn(column, conceptKey))]),
            EditorConceptKeys.WhileFilledBlock => ed.WhenFilledBlock(
                Required(cell, "slot"),
                [.. cell.GetChildren(EditorLanguage.CellSlot).Select(child => LowerBlock(child, conceptKey))]),
            EditorConceptKeys.ConditionalBlock => ed.ConditionalBlock(
                Required(cell, "slot"),
                Required(cell, "value"),
                [.. cell.GetChildren(EditorLanguage.CellSlot).Select(child => LowerBlock(child, conceptKey))]),
            _ => throw NotACell(cell, conceptKey, "not a block"),
        };
    }

    /// <summary>
    /// One column of a table: the slot of the row concept it shows, and the header drawn above it.
    /// </summary>
    /// <remarks>
    /// The header is lowered as an inline cell, so a header that names a slot is refused by
    /// <see cref="TableColumn"/> rather than silently answered by whichever row came first.
    /// </remarks>
    private static TableColumn LowerColumn(Node column, string conceptKey)
    {
        var slot = Required(column, "slot");

        if (column.GetChild("header") is not { } header)
        {
            throw new ConceptDefinitionException(
                $"The table of '{conceptKey}' declares column '{slot}' without a header.");
        }

        return new TableColumn(slot, LowerInline(header, conceptKey))
        {
            EmptyText = Optional(column, "emptyText") ?? "<empty>",
        };
    }

    /// <summary>An inline cell, which is what a line may hold.</summary>
    private static InlineNotationCell LowerInline(Node cell, string conceptKey)    {
        var ed = NotationEditor.Create();

        return cell.Concept.Key switch
        {
            EditorConceptKeys.Space => ed.Space,
            EditorConceptKeys.TypeOf => ed.TypeOf(),
            EditorConceptKeys.Image => ed.Image(Required(cell, "key"), Size(cell, "width"), Size(cell, "height")),
            EditorConceptKeys.Keyword => ed.Keyword(Required(cell, "text")),
            EditorConceptKeys.Punct => ed.Punct(Required(cell, "text")),
            EditorConceptKeys.Property => ed.Property(
                Required(cell, "slot"),
                emptyText: Optional(cell, "emptyText") ?? string.Empty),
            EditorConceptKeys.Reference => ed.Reference(Required(cell, "slot")),
            EditorConceptKeys.ChildInline => new SingleChildNotationCell(Required(cell, "slot"))
            {
                EmptyText = EmptyText(cell),
            },
            EditorConceptKeys.Conditional => ed.Conditional(
                Required(cell, "slot"),
                Optional(cell, "value") ?? string.Empty,
                [.. cell.GetChildren(EditorLanguage.CellSlot).Select(child => LowerInline(child, conceptKey))]),
            EditorConceptKeys.WhileFilled => ed.WhenFilled(
                Required(cell, "slot"),
                [.. cell.GetChildren(EditorLanguage.CellSlot).Select(child => LowerInline(child, conceptKey))]),
            EditorConceptKeys.InlineCollection => ed.InlineCollection(
                Required(cell, "slot"),
                cell.GetChild("separator") is { } inlineSeparator ? LowerInline(inlineSeparator, conceptKey) : null,
                EmptyText(cell),
                Spacing(cell)),
            _ => throw NotACell(cell, conceptKey, "not inline"),
        };
    }

    private static int Spacing(Node cell) =>
        int.TryParse(cell.GetOrNull("spacing"), out var spacing) ? spacing : 1;

    /// <summary>
    /// The side of a picture's box, in layout units; zero when the notation left it out.
    /// </summary>
    /// <remarks>
    /// Checked here rather than left to <see cref="ImageNotationCell"/>, which refuses a negative side: the cell's
    /// exception names a parameter, and the message an author can act on has to name the notation and the slot -
    /// the same reason a notation naming an undeclared concept is refused here.
    /// </remarks>
    private static double Size(Node cell, string slot)
    {
        var text = cell.GetOrNull(slot);

        if (text is not { Length: > 0 })
        {
            return 0;
        }

        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var size) || size < 0)
        {
            throw new ConceptDefinitionException(
                $"A '{cell.Concept.Key}' cell has '{text}' for its '{slot}', which is not a size; leave it out for a picture as tall as the line.");
        }

        return size;
    }

    /// <summary>Whether a boolean property of <paramref name="cell"/> is on, which an unset one never is.</summary>
    private static bool IsTrue(Node cell, string slot) =>
        string.Equals(cell.GetOrNull(slot), "true", StringComparison.Ordinal);

    private static ConceptDefinitionException NotACell(Node cell, string conceptKey, string reason) =>
        new($"The notation of '{conceptKey}' holds a '{cell.Concept.Key}', which is {reason}.");

    /// <summary>A slot a cell cannot be lowered without.</summary>
    /// <remarks>
    /// The message names the cell's own concept, not the notation's <c>conceptKey</c>: the node doing the asking is a
    /// cell, which declares no such slot, so reading it would throw a <see cref="KeyNotFoundException"/> out of the
    /// error path and hide the message an author has to act on.
    /// </remarks>
    private static string Required(Node node, string slot) =>
        node.GetOrNull(slot) is { Length: > 0 } value
            ? value
            : throw new ConceptDefinitionException(
                $"A '{node.Concept.Key}' cell leaves out its '{slot}'.");

    private static string? Optional(Node node, string slot) =>
        node.GetOrNull(slot) is { Length: > 0 } value ? value : null;

    /// <summary>What a slot-bearing cell shows while its slot holds nothing.</summary>
    /// <remarks>
    /// Not <see cref="Optional"/>: "draw nothing" is a real answer - <c>f()</c> is a call with no arguments - and it
    /// has to stay distinguishable from "the author said nothing", which takes the default. Reading an empty string as
    /// unset would make the placeholder the only thing a declared language could ever show.
    /// </remarks>
    private static string EmptyText(Node cell) =>
        cell.GetOrNull("emptyText") is { } text ? text : "<empty>";
}

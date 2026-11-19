using Projectional.Language;

namespace Projectional.LanguageExport;

/// <summary>Writes a language's notations as the <c>ed.*</c> builder calls a hand-written language makes.</summary>
/// <remarks>
/// <para>
/// The cells are emitted as the builder call they came from, which is what makes the export a round trip: a declared
/// notation lowers through <c>NotationEditor</c>, and this writes the same call back out. A cell with children breaks
/// onto several lines once it grows past a comfortable width, because a reviewer reads the shape of a notation from
/// its indentation - a single 200-character line would hide it.
/// </para>
/// <para>
/// Only the arguments a language actually wrote are emitted. The builder's defaults are the same values these cells
/// carry, so writing them out would be noise that looks like a decision.
/// </para>
/// </remarks>
internal static class NotationWriter
{
    private const int Width = 100;

    /// <summary>Writes one <c>notations.Add</c> call per notation, in registration order.</summary>
    public static void Write(SourceWriter source, IReadOnlyList<Notation> notations)
    {
        foreach (var notation in notations)
        {
            var variant = notation.Variant == Notation.DefaultVariant
                ? string.Empty
                : $", {SourceWriter.Literal(notation.Variant)}";

            var lines = Block(notation.Root);

            if (lines.Count == 1)
            {
                source.Line($"notations.Add({SourceWriter.Literal(notation.ConceptKey)}{variant}, ed => {lines[0]});");
                continue;
            }

            source.Line($"notations.Add({SourceWriter.Literal(notation.ConceptKey)}{variant}, ed => {lines[0]}");

            // The continuation lines carry their own indentation: the block builder is what knows how deep a cell
            // sits inside its parent, so an indent scope here would count every level twice.
            foreach (var line in lines.Skip(1).SkipLast(1))
            {
                source.Line(line);
            }

            source.Line($"{lines[^1]});");
        }
    }

    /// <summary>The cell as one line if it is short enough, otherwise as a block.</summary>
    private static List<string> Block(NotationCell cell)
    {
        var flat = Flat(cell);

        return flat.Length <= Width ? [flat] : Broken(cell);
    }

    private static List<string> Broken(NotationCell cell)
    {
        (string Head, IReadOnlyList<NotationCell> Children, string Tail)? parts = cell switch
        {
            LineNotationCell line => ("ed.Line(", line.Content, ")"),
            SequenceNotationCell sequence => ("ed.Sequence(", sequence.Content, ")"),
            IndentNotationCell indent => ("ed.Indent(", indent.Content, ")"),
            HorizontalNotationCell horizontal => ("ed.Horizontal(", horizontal.Content, ")"),
            ConditionalNotationCell conditional => ($"ed.Conditional({SourceWriter.Literal(conditional.Slot)}, {SourceWriter.Literal(conditional.Value)}, ", conditional.Content, ")"),
            WhenFilledNotationCell whenFilled => ($"ed.WhenFilled({SourceWriter.Literal(whenFilled.Slot)}, ", whenFilled.Content, ")"),
            _ => null,
        };

        if (parts is not { } edges)
        {
            return [Flat(cell)];
        }

        var (head, children, tail) = edges;

        var lines = new List<string> { head };

        for (var i = 0; i < children.Count; i++)
        {
            var child = Block(children[i]);

            lines.Add($"    {child[0]}");

            for (var rest = 1; rest < child.Count; rest++)
            {
                lines.Add($"    {child[rest]}");
            }

            if (i < children.Count - 1)
            {
                lines[^1] += ",";
            }
        }

        lines.Add(tail);

        return lines;
    }

    private static string Flat(NotationCell cell) => cell switch
    {
        ConstantNotationCell constant => Constant(constant),
        SpaceNotationCell => "ed.Space",
        PropertyNotationCell property => property.EmptyText.Length == 0
            ? $"ed.Property({SourceWriter.Literal(property.Slot)})"
            : $"ed.Property({SourceWriter.Literal(property.Slot)}, emptyText: {SourceWriter.Literal(property.EmptyText)})",
        ReferenceNotationCell reference => reference.UnsetText == "<none>"
            ? $"ed.Reference({SourceWriter.Literal(reference.Slot)})"
            : $"ed.Reference({SourceWriter.Literal(reference.Slot)}, unsetText: {SourceWriter.Literal(reference.UnsetText)})",
        SingleChildNotationCell single => single.EmptyText == "<empty>"
            ? $"ed.ChildInline({SourceWriter.Literal(single.Slot)})"
            : $"ed.ChildInline({SourceWriter.Literal(single.Slot)}, emptyText: {SourceWriter.Literal(single.EmptyText)})",
        ChildNotationCell children => Children(children),
        CollectionNotationCell collection => Collection(collection),
        InlineCollectionNotationCell inline => InlineCollection(inline),
        TableNotationCell table => Table(table),
        ConditionalNotationCell conditional => $"ed.Conditional({SourceWriter.Literal(conditional.Slot)}, {SourceWriter.Literal(conditional.Value)}, {Join(conditional.Content)})",
        WhenFilledNotationCell whenFilled => $"ed.WhenFilled({SourceWriter.Literal(whenFilled.Slot)}, {Join(whenFilled.Content)})",
        LineNotationCell line => $"ed.Line({Join(line.Content)})",
        SequenceNotationCell sequence => $"ed.Sequence({Join(sequence.Content)})",
        IndentNotationCell indent => $"ed.Indent({Join(indent.Content)})",
        HorizontalNotationCell horizontal => $"ed.Horizontal({Join(horizontal.Content)})",
        _ => throw new NotSupportedException(
            $"The cell '{cell.GetType().Name}' has no export spelling."),
    };

    private static string Constant(ConstantNotationCell constant)
    {
        var call = constant.Kind == ConstantKind.Keyword
            ? $"ed.Keyword({SourceWriter.Literal(constant.Text)}"
            : $"ed.Punct({SourceWriter.Literal(constant.Text)}";

        return constant.Emphasis == TextEmphasis.None
            ? $"{call})"
            : $"{call}, TextEmphasis.{constant.Emphasis})";
    }

    private static string Children(ChildNotationCell children)
    {
        var arguments = new List<string> { SourceWriter.Literal(children.Slot) };

        if (children.EmptyText != "<empty>" || children.Spacing != 1 || children.Stacked)
        {
            arguments.Add(SourceWriter.Literal(children.EmptyText));
        }

        if (children.Spacing != 1 || children.Stacked)
        {
            arguments.Add($"spacing: {children.Spacing}");
        }

        if (children.Stacked)
        {
            arguments.Add("stacked: true");
        }

        return $"ed.Children({string.Join(", ", arguments)})";
    }

    private static string Collection(CollectionNotationCell collection)
    {
        var arguments = new List<string> { SourceWriter.Literal(collection.Slot) };

        if (collection.Separator is not null)
        {
            arguments.Add($"separator: {Flat(collection.Separator)}");
        }

        if (collection.EmptyText != "<empty>" || collection.Spacing != 1)
        {
            arguments.Add($"emptyText: {SourceWriter.Literal(collection.EmptyText)}");
        }

        if (collection.Spacing != 1)
        {
            arguments.Add($"spacing: {collection.Spacing}");
        }

        return $"ed.Collection({string.Join(", ", arguments)})";
    }

    private static string InlineCollection(InlineCollectionNotationCell inline)
    {
        var arguments = new List<string> { SourceWriter.Literal(inline.Slot) };

        if (inline.Separator is not null)
        {
            arguments.Add($"separator: {Flat(inline.Separator)}");
        }

        if (inline.EmptyText != "<empty>" || inline.Spacing != 1)
        {
            arguments.Add($"emptyText: {SourceWriter.Literal(inline.EmptyText)}");
        }

        if (inline.Spacing != 1)
        {
            arguments.Add($"spacing: {inline.Spacing}");
        }

        return $"ed.InlineCollection({string.Join(", ", arguments)})";
    }

    private static string Table(TableNotationCell table)
    {
        var arguments = new List<string> { SourceWriter.Literal(table.Slot) };

        if (table.EmptyText != "<empty>")
        {
            arguments.Add(SourceWriter.Literal(table.EmptyText));
        }

        foreach (var column in table.Columns)
        {
            arguments.Add(Column(column));
        }

        return $"ed.Table({string.Join(", ", arguments)})";
    }

    private static string Column(TableColumn column)
    {
        var call = $"ed.Column({SourceWriter.Literal(column.Slot)}, {Flat(column.Header)}";

        return column.EmptyText == "<empty>"
            ? $"{call})"
            : $"{call}, emptyText: {SourceWriter.Literal(column.EmptyText)})";
    }

    private static string Join(IEnumerable<NotationCell> cells) =>
        string.Join(", ", cells.Select(Flat));
}

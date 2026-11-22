using System.Text;

using Projectional.Language;

namespace Projectional.LanguageDocs;

/// <summary>A concept's notation written out as the text it composes, so a reader can see the shape.</summary>
/// <remarks>
/// <para>
/// The notation is a tree of cells, not a template string, so there is nothing to print: this walks the tree and writes
/// what the cells would spell - a keyword as itself, a slot as <c>&lt;name&gt;</c>, a list as <c>&lt;places...&gt;</c>,
/// and every level of indentation as two spaces. It is the documentation's answer to "how do I write one of these",
/// and it is derived from the same notation the editor projects, so it cannot describe a shape the editor would not
/// draw.
/// </para>
/// <para>
/// What it deliberately does not do is render a document: a notation's <em>content</em> is a model, and a shape drawn
/// from an empty one would be a picture of placeholders. This is the placeholder layer made explicit, in text, which is
/// why the page says "the shape" rather than showing something that looks like an example.
/// </para>
/// </remarks>
internal static class NotationSpelling
{
    /// <summary>Writes <paramref name="notation"/> out as text, one line per line of the notation.</summary>
    internal static string Of(Notation notation)
    {
        ArgumentNullException.ThrowIfNull(notation);

        return Of(notation.Root);
    }

    /// <summary>Writes the notation tree below <paramref name="root"/> out as text.</summary>
    internal static string Of(BlockNotationCell root)
    {
        ArgumentNullException.ThrowIfNull(root);

        var lines = new List<string>();
        var current = new StringBuilder();
        var level = 0;

        Walk(root, ref level, lines, current);
        Flush(lines, current);

        return string.Join('\n', lines);
    }

    /// <summary>Appends a completed line, trimmed, unless it holds nothing but blanks.</summary>
    private static void Flush(List<string> lines, StringBuilder current)
    {
        var text = current.ToString().TrimEnd();

        if (text.Length > 0)
        {
            lines.Add(text);
        }

        current.Clear();
    }

    /// <summary>Starts a new line unless the current one is still empty.</summary>
    private static void Break(List<string> lines, StringBuilder current, int level)
    {
        if (current.Length > 0)
        {
            Flush(lines, current);
        }

        current.Append(' ', level * 2);
    }

    /// <summary>Walks one block cell, writing as many lines as it composes.</summary>
    private static void Walk(BlockNotationCell cell, ref int level, List<string> lines, StringBuilder current)
    {
        ArgumentNullException.ThrowIfNull(cell);

        switch (cell)
        {
            case LineNotationCell line:
                Break(lines, current, level);

                foreach (var each in line.Content)
                {
                    current.Append(Inline(each));
                }

                Flush(lines, current);

                break;

            case SequenceNotationCell sequence:
                foreach (var each in sequence.Content)
                {
                    Walk(each, ref level, lines, current);
                }

                break;

            case HorizontalNotationCell horizontal:
                // Cells side by side belong on one line: that is the whole difference to a sequence.
                Break(lines, current, level);

                foreach (var each in horizontal.Content)
                {
                    current.Append(Flat(each));
                }

                Flush(lines, current);

                break;

            case IndentNotationCell indent:
                level++;

                foreach (var each in indent.Content)
                {
                    Walk(each, ref level, lines, current);
                }

                level--;

                break;

            case WhenFilledBlockNotationCell whenFilled:
                Marker(lines, current, level, $"when {whenFilled.Slot} is filled");
                WalkAll(whenFilled.Content, ref level, lines, current);
                break;

            case ConditionalBlockNotationCell conditional:
                Marker(lines, current, level, $"if {conditional.Slot} is \"{conditional.Value}\"");
                WalkAll(conditional.Content, ref level, lines, current);
                break;

            default:
                // Every remaining block cell is an inline cell that happens to be a block, or a table: both fit on one
                // line, and a shape this reader does not know still shows rather than disappearing.
                Break(lines, current, level);
                current.Append(Inline(cell));
                Flush(lines, current);

                break;
        }
    }

    /// <summary>Walks <paramref name="cells"/> one level in, under a marker line.</summary>
    private static void WalkAll(IReadOnlyList<BlockNotationCell> cells, ref int level, List<string> lines, StringBuilder current)
    {
        level++;

        foreach (var each in cells)
        {
            Walk(each, ref level, lines, current);
        }

        level--;
    }

    /// <summary>Writes a line that says why what follows is drawn, closed again by a bracket.</summary>
    private static void Marker(List<string> lines, StringBuilder current, int level, string text)
    {
        Break(lines, current, level);
        current.Append('[').Append(text).Append(']');
        Flush(lines, current);
    }

    /// <summary>Writes a block cell on one line, which is what a cell beside another one asks for.</summary>
    private static string Flat(BlockNotationCell cell) => cell switch
    {
        LineNotationCell line => string.Concat(line.Content.Select(Inline)),
        SequenceNotationCell sequence => string.Concat(sequence.Content.Select(Flat)),
        HorizontalNotationCell horizontal => string.Concat(horizontal.Content.Select(Flat)),
        IndentNotationCell indent => string.Concat(indent.Content.Select(Flat)),
        WhenFilledBlockNotationCell whenFilled => Bracket($"when {whenFilled.Slot} is filled", string.Concat(whenFilled.Content.Select(Flat))),
        ConditionalBlockNotationCell conditional => Bracket($"if {conditional.Slot} is \"{conditional.Value}\"", string.Concat(conditional.Content.Select(Flat))),
        _ => Inline(cell),
    };

    /// <summary>
    /// A conditional part, in brackets, with its content trimmed.
    /// </summary>
    /// <remarks>
    /// The cells inside a conditional usually begin and end with a space cell, because the notation needs it when the
    /// part is drawn; inside a bracket the separator does that job, and keeping both gives the shape a double space
    /// that reads like a generator's output rather than the language's spelling.
    /// </remarks>
    private static string Bracket(string condition, string content)
    {
        var inside = content.Trim();

        return inside.Length == 0 ? $"[{condition}]" : $"[{condition}: {inside}]";
    }

    /// <summary>Writes one leaf cell: a literal, a space, or a slot marked by what it holds.</summary>
    private static string Inline(NotationCell cell) => cell switch
    {
        ConstantNotationCell constant => constant.Text,
        SpaceNotationCell => " ",
        PropertyNotationCell property => $"<{property.Slot}>",
        SingleChildNotationCell child => $"<{child.Slot}>",
        ChildNotationCell children => $"<{children.Slot}...>",
        CollectionNotationCell collection => $"<{collection.Slot}...>",
        InlineCollectionNotationCell collection => $"<{collection.Slot}...>",
        ReferenceNotationCell reference => $"<{reference.Slot}>",
        TableNotationCell table => $"<{table.Slot}: table of {string.Join(" | ", table.Columns.Select(column => column.Slot))}>",
        TypeOfNotationCell => " : Type",
        WhenFilledNotationCell whenFilled => Bracket($"when {whenFilled.Slot} is filled", string.Concat(whenFilled.Content.Select(Inline))),
        ConditionalNotationCell conditional => Bracket($"if {conditional.Slot} is \"{conditional.Value}\"", string.Concat(conditional.Content.Select(Inline))),
        _ => $"<{cell}>",
    };
}
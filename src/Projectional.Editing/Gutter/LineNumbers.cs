using Projectional.Projection;

namespace Projectional.Editing;

/// <summary>
/// Which rows a laid-out document has, and what each of them is called.
/// </summary>
/// <remarks>
/// <para>
/// The interesting question is what a "line" is when the document is a two-dimensional placement of
/// cells rather than a sequence of lines. The answer taken here is the one a reader would give: a row
/// is a band of the layout that text is drawn in, no matter which cell put it there. Two cells that
/// share a row - the keyword and the value of one property, or a node and its first child - share a
/// number, because they are one line on the screen. A wrapped value contributes one number per drawn
/// line, which is again what the reader sees.
/// </para>
/// <para>
/// That definition is derived, like everything else the editor draws: nothing is stored, nothing is
/// persisted, and no cell carries a number. It follows that a row's number changes when the document
/// above it changes height, so a host must not treat a number as a stable reference to anything.
/// </para>
/// <para>
/// Rows are found from the whole layout, not from the visible part of it, so the numbers stay put while
/// the view scrolls. Painting then skips the rows that are off screen.
/// </para>
/// </remarks>
public static class LineNumbers
{
    /// <summary>The numbered rows of <paramref name="layout"/>, top to bottom.</summary>
    /// <param name="layout">The layout whose rows are counted.</param>
    /// <param name="measure">The measure that says how tall a line is.</param>
    /// <remarks>
    /// A cell that draws nothing contributes no row of its own: a container covers the same area as its
    /// children, so counting it would number the same band twice. A cell that shows a placeholder does
    /// contribute one, because an empty slot is a line the reader can see.
    /// </remarks>
    public static IReadOnlyList<LineNumberRow> Of(CellLayout layout, ITextMeasure measure)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(measure);

        var tops = TopsOf(layout, measure);

        tops.Sort();

        var rows = new List<LineNumberRow>(tops.Count);
        var tolerance = measure.LineHeight / 2;
        var last = double.NegativeInfinity;

        foreach (var top in tops)
        {
            if (top - last < tolerance)
            {
                continue;
            }

            last = top;
            rows.Add(new LineNumberRow(rows.Count + 1, new LayoutRect(0, top, 0, measure.LineHeight)));
        }

        return rows;
    }

    /// <summary>How many digits the largest number of <paramref name="rows"/> needs.</summary>
    /// <param name="rows">The rows to label.</param>
    /// <remarks>
    /// Exposed separately from the rows themselves so a control can size its gutter from a row count it
    /// already has, and so the width of a gutter is a question with one answer.
    /// </remarks>
    public static int DigitsOf(IReadOnlyList<LineNumberRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var digits = 1;

        for (var count = rows.Count; count > 9; count /= 10)
        {
            digits++;
        }

        return digits;
    }

    /// <summary>The top of every drawn line of every cell that draws anything.</summary>
    /// <remarks>
    /// Kept separate and unsorted because collecting and sorting are the two passes that make this
    /// linear apart from the sort: comparing every top against every row instead would be quadratic on a
    /// document with a thousand rows.
    /// </remarks>
    private static List<double> TopsOf(CellLayout layout, ITextMeasure measure)
    {
        var tops = new List<double>(layout.Cells.Count);
        var lineHeight = measure.LineHeight;

        foreach (var placed in layout.Cells)
        {
            if (!CellText.IsVisible(placed.Cell))
            {
                continue;
            }

            var lines = layout.TextOf(placed.Cell, measure);

            for (var line = 0; line < lines.LineCount; line++)
            {
                tops.Add(placed.Bounds.Y + (line * lineHeight));
            }
        }

        return tops;
    }
}

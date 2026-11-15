using Projectional.Projection;

namespace Projectional.Editing;

/// <summary>
/// Where a caret position sits inside a cell whose text may be several lines tall.
/// </summary>
/// <remarks>
/// <para>
/// A position names a cell and an offset, but a rectangle needs a line as well: the same offset is at
/// one height on a one-line cell and somewhere in the middle of a wrapped one. Every question the
/// caret asks about its own geometry - which line it is on, how far along that line, which offset a
/// point found - is answered here, so the bar, the highlight, the click and the vertical move cannot
/// drift apart.
/// </para>
/// <para>
/// Columns are measured from the start of the line, not from the start of the cell: the second line of
/// a wrapped value starts at the cell's left edge again, so its prefix is the characters after the
/// break and nothing else.
/// </para>
/// </remarks>
internal static class CaretGeometry
{
    /// <summary>How the text of a place's cell is broken into lines.</summary>
    internal static LaidOutText LinesOf(CaretPlace place, CellLayout layout, ITextMeasure measure) =>
        layout.TextOf(place.Cell, measure);

    /// <summary>The index of the line <paramref name="offset"/> stands on.</summary>
    internal static int LineIndexOf(LaidOutText lines, int offset) => lines.IndexAt(offset);

    /// <summary>The top of a line, in layout coordinates.</summary>
    internal static double TopOf(LayoutRect bounds, int lineIndex, ITextMeasure measure) =>
        bounds.Y + (lineIndex * measure.LineHeight);

    /// <summary>
    /// The distance from the cell's left edge to the boundary <paramref name="offset"/> names on
    /// <paramref name="lineIndex"/>.
    /// </summary>
    /// <remarks>
    /// The offset is clamped into the line rather than trusted, so a caret left over from a shorter
    /// value draws at the end of the line it stands on instead of outside its cell. It is also clamped
    /// to the place's own text: an empty slot draws a marker instead of text, and the marker's width
    /// must not turn into caret offsets the place does not have.
    /// </remarks>
    internal static double LeftOn(CaretPlace place, LaidOutText lines, int lineIndex, int offset, ITextMeasure measure)
    {
        var line = lines.Lines[Math.Clamp(lineIndex, 0, lines.LineCount - 1)];
        var clamped = Math.Clamp(offset, line.Start, Math.Min(line.ContentEnd, place.Length));

        return clamped <= line.Start ? 0 : measure.Measure(lines.Text[line.Start..clamped], place.Emphasis);
    }

    /// <summary>The line a vertical coordinate falls on.</summary>
    internal static int LineAtY(LaidOutText lines, LayoutRect bounds, double y, ITextMeasure measure)
    {
        var index = Math.Floor((y - bounds.Y) / measure.LineHeight);

        return (int)Math.Clamp(index, 0, lines.LineCount - 1);
    }

    /// <summary>
    /// The line of a cell whose top is <paramref name="top"/> within <paramref name="tolerance"/>, or
    /// <c>-1</c> when the cell has no line there.
    /// </summary>
    internal static int RowAt(LaidOutText lines, LayoutRect bounds, double top, double tolerance, ITextMeasure measure)
    {
        var index = (int)Math.Round((top - bounds.Y) / measure.LineHeight);

        return index >= 0 && index < lines.LineCount && Math.Abs(TopOf(bounds, index, measure) - top) < tolerance
            ? index
            : -1;
    }

    /// <summary>
    /// The offset on <paramref name="lineIndex"/> whose boundary is closest to <paramref name="x"/>,
    /// which is measured from the cell's left edge.
    /// </summary>
    /// <remarks>
    /// Ties go to the boundary further left, which is what makes clicking the left half of a character
    /// put the caret before it and the right half after it.
    /// </remarks>
    internal static int OffsetAt(CaretPlace place, LaidOutText lines, int lineIndex, double x, ITextMeasure measure)
    {
        var line = lines.Lines[Math.Clamp(lineIndex, 0, lines.LineCount - 1)];
        var end = Math.Min(line.ContentEnd, place.Length);
        var best = Math.Min(line.Start, end);
        var distance = Math.Abs(x);

        for (var offset = best + 1; offset <= end; offset++)
        {
            var candidate = Math.Abs(measure.Measure(lines.Text[line.Start..offset], place.Emphasis) - x);

            if (candidate < distance)
            {
                best = offset;
                distance = candidate;
            }
        }

        return best;
    }
}

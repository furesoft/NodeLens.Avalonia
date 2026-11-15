using Projectional.Language;
using Projectional.Projection;
using Projectional.Semantics;

namespace Projectional.Editing;

/// <summary>
/// Turns a marker set into the rectangles a page strokes, one per line of text.
/// </summary>
/// <remarks>
/// <para>
/// This is the second half of placing a diagnostic. <see cref="MarkerCells"/> answers which cells a
/// finding is about; this answers how much of each drawn line those cells actually cover. A value may be
/// drawn across several lines because it wrapped or because it contains a break, and a range inside it
/// therefore splits: the first line gets the part before the break, the middle lines are marked whole, and
/// the last line gets the part after it.
/// </para>
/// <para>
/// A range is clipped rather than dropped. A check runs against the model, so the text it measured may
/// have been edited since: a range that now reaches past the end marks what is left, and one that starts
/// past the end marks the whole cell. A marker that vanished because a value got shorter would be a bug
/// the user cannot see, and the finding is still true.
/// </para>
/// <para>
/// Nothing here knows about a viewport. The result covers the whole document, because a page that
/// scrolled has to be able to draw a marker it did not draw a moment ago without re-resolving anything;
/// a renderer that only wants the visible part filters this list, which is a comparison on
/// <see cref="MarkerLine.Y"/>.
/// </para>
/// <para>
/// It is public because the control that draws these lives in another assembly and there is no
/// <c>InternalsVisibleTo</c> across the frontend boundary - the same reason <see cref="EditorFrame"/> is
/// public. A host that paints its own page needs exactly this conversion too.
/// </para>
/// </remarks>
public static class MarkerGeometry
{
    /// <summary>The markers of <paramref name="markers"/> as drawn lines of <paramref name="layout"/>.</summary>
    /// <param name="layout">The layout the markers are placed in.</param>
    /// <param name="markers">The findings to place.</param>
    /// <param name="measure">Measures the text, which is how a character offset becomes a width.</param>
    /// <remarks>
    /// The answer is in reading order - by <see cref="MarkerLine.Y"/>, then <see cref="MarkerLine.X"/> - so
    /// a caller that walks it visits markers the way the eye does. Findings on nodes the layout does not
    /// contain contribute nothing, and no marker is ever zero wide.
    /// </remarks>
    public static IReadOnlyList<MarkerLine> Of(CellLayout layout, MarkerSet markers, ITextMeasure measure)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(markers);
        ArgumentNullException.ThrowIfNull(measure);

        var lines = new List<MarkerLine>();

        foreach (var diagnostic in markers.Diagnostics)
        {
            foreach (var cell in MarkerCells.Of(layout, diagnostic))
            {
                if (layout.BoundsOf(cell) is { } bounds)
                {
                    Add(lines, cell, bounds, layout.TextOf(cell, measure), diagnostic, measure);
                }
            }
        }

        lines.Sort(static (left, right) =>
            left.Y != right.Y ? left.Y.CompareTo(right.Y) : left.X.CompareTo(right.X));

        return lines;
    }

    private static void Add(
        List<MarkerLine> lines,
        EditorCell cell,
        LayoutRect bounds,
        LaidOutText text,
        Diagnostic diagnostic,
        ITextMeasure measure)
    {
        var emphasis = (cell as TextCell)?.Emphasis ?? TextEmphasis.None;

        // A range counts from the start of one value, and only the cell showing that value is handed one,
        // so the two cases never mix on the same cell.
        if (diagnostic.Range is not { } range)
        {
            AddWholeText(lines, cell, bounds, text, diagnostic, emphasis, measure);

            return;
        }

        if (range.Start >= text.Text.Length)
        {
            // Every character the range was counted against is gone. Marking the cell keeps the finding
            // visible; marking nothing would silently lose it.
            AddWholeText(lines, cell, bounds, text, diagnostic, emphasis, measure);

            return;
        }

        var start = range.Start;
        var end = Math.Min(range.End, text.Text.Length);

        if (end <= start)
        {
            // An empty range points between two characters. Marking the one it points at is what makes
            // "this position is wrong" visible at all.
            end = start + 1;
        }

        var added = false;

        for (var index = 0; index < text.LineCount; index++)
        {
            var line = text.Lines[index];
            var from = Math.Max(start, line.Start);
            var to = Math.Min(end, line.ContentEnd);

            if (to > from)
            {
                AddSegment(lines, cell, bounds, text, diagnostic, emphasis, index, from, to, measure);
                added = true;
            }
            else if (line.IsEmpty && start <= line.Start && line.Start < end)
            {
                // A range that crosses a blank line marks that line too, so a squiggle never skips a line
                // in the middle of itself.
                AddSegment(lines, cell, bounds, text, diagnostic, emphasis, index, line.Start, line.Start, measure);
                added = true;
            }
        }

        if (!added)
        {
            AddSegment(lines, cell, bounds, text, diagnostic, emphasis, 0, start, start, measure);
        }
    }

    private static void AddWholeText(
        List<MarkerLine> lines,
        EditorCell cell,
        LayoutRect bounds,
        LaidOutText text,
        Diagnostic diagnostic,
        TextEmphasis emphasis,
        ITextMeasure measure)
    {
        var added = false;

        for (var index = 0; index < text.LineCount; index++)
        {
            var line = text.Lines[index];

            if (!line.IsEmpty)
            {
                AddSegment(lines, cell, bounds, text, diagnostic, emphasis, index, line.Start, line.ContentEnd, measure);
                added = true;
            }
        }

        if (!added)
        {
            // An empty slot draws no characters, but the finding about it is still real.
            AddSegment(lines, cell, bounds, text, diagnostic, emphasis, 0, 0, 0, measure);
        }
    }

    private static void AddSegment(
        List<MarkerLine> lines,
        EditorCell cell,
        LayoutRect bounds,
        LaidOutText text,
        Diagnostic diagnostic,
        TextEmphasis emphasis,
        int lineIndex,
        int start,
        int end,
        ITextMeasure measure)
    {
        var index = Math.Clamp(lineIndex, 0, text.LineCount - 1);
        var line = text.Lines[index];
        var from = Math.Clamp(start, line.Start, line.ContentEnd);
        var to = Math.Clamp(end, from, line.ContentEnd);

        var left = from == line.Start ? 0 : measure.Measure(text.Text[line.Start..from], emphasis);
        var width = to == from ? 0 : measure.Measure(text.Text[from..to], emphasis);

        // A zero-width squiggle is invisible, and a finding the user cannot see is a finding that does not
        // exist. An empty slot still reserves one space, so one space is always inside the cell.
        if (width <= 0)
        {
            width = measure.Measure(" ", emphasis);
        }

        lines.Add(new MarkerLine(
            cell.Address,
            diagnostic,
            bounds.X + left,
            bounds.Y + (index * measure.LineHeight),
            width,
            measure.LineHeight,
            index));
    }
}

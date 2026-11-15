using Projectional.Language;

namespace Projectional.Projection;

/// <summary>
/// Breaks a text into the lines a text cell draws.
/// </summary>
/// <remarks>
/// <para>
/// There are two ways a line ends. A hard break is a newline in the value itself, and it is always
/// honoured: a value that contains one is a value with several lines, whatever the page is wide. A soft
/// break is the wrapper's own, taken at a space when the next word no longer fits in
/// <c>limit</c> - the width the caller allows, which the layout passes as the room left to the page's
/// edge.
/// </para>
/// <para>
/// A soft break is only ever taken at a space. A word wider than the limit - a keyword, a name, a token
/// that happens to start near the page's edge - is drawn whole and runs past the edge instead of being
/// broken between two of its characters, and the reader scrolls to it. Breaking it puts as little as one
/// character on each line, so a cell that is one token tall becomes a cell of six rows the moment the
/// window narrows, and the line numbers beside it count a document of three lines as eight. Running past
/// the edge is also what a cell starting <em>past</em> the edge already does, so both sides of the edge
/// agree. A wrapped cell can therefore end up wider than the limit, and the layout's width grows with
/// it - which is exactly what the horizontal scrollbar is for.
/// </para>
/// <para>
/// Measuring is not additive - a character's width depends on the text around it in a proportional font
/// - so a candidate line is measured as a whole, and only until it stops fitting. That costs one measure
/// per character of each line, which is nothing for a paragraph.
/// </para>
/// </remarks>
internal static class TextWrapper
{
    /// <summary>
    /// Splits <paramref name="text"/> into the lines it is drawn in, breaking softly at spaces where
    /// <paramref name="limit"/> does not reach.
    /// </summary>
    /// <param name="text">The text to break.</param>
    /// <param name="emphasis">How the text is drawn, which is how wide it is.</param>
    /// <param name="measure">Measures the text.</param>
    /// <param name="limit">
    /// The room a line has before it runs past the page's edge, or <see langword="null"/> to honour hard
    /// breaks only - which is what a cell whose width the layout has yet to decide on asks for. It is a
    /// place to break at, not a cap: a line holding a word wider than this is wider than this.
    /// </param>
    /// <returns>The lines, in the order they are drawn.</returns>
    internal static IReadOnlyList<TextLine> Lines(
        string text,
        TextEmphasis emphasis,
        ITextMeasure measure,
        double? limit)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(measure);

        var lines = new List<TextLine>();
        var position = 0;

        while (true)
        {
            var breakAt = text.IndexOf('\n', position);
            var segmentEnd = breakAt < 0 ? text.Length : breakAt;
            var contentEnd = ContentEndOf(text, position, segmentEnd);

            // The break character belongs to the line it ends, so that consecutive lines cover the text
            // without overlap and without a gap. A `\r\n` pair is one break, and the `\r` is not drawn.
            var consumedEnd = breakAt < 0 ? segmentEnd : breakAt + 1;

            AppendSegment(text, emphasis, measure, limit, position, contentEnd, consumedEnd, lines);

            if (breakAt < 0)
            {
                return lines;
            }

            position = breakAt + 1;
        }
    }

    /// <summary>One space of <paramref name="emphasis"/> is the narrowest a cell is allowed to be.</summary>
    /// <remarks>
    /// A cell nobody has typed into is still a place the caret has to be able to stand on, and a
    /// zero-width cell is not one of those.
    /// </remarks>
    internal static double MinimumWidth(TextEmphasis emphasis, ITextMeasure measure) =>
        measure.Measure(" ", emphasis);

    private static int ContentEndOf(string text, int start, int segmentEnd) =>
        segmentEnd > start && text[segmentEnd - 1] == '\r' ? segmentEnd - 1 : segmentEnd;

    private static void AppendSegment(
        string text,
        TextEmphasis emphasis,
        ITextMeasure measure,
        double? limit,
        int start,
        int contentEnd,
        int consumedEnd,
        List<TextLine> lines)
    {
        if (start >= contentEnd || limit is not { } width)
        {
            // An empty segment is a blank line - the value starts or ends with a break, or has two in a
            // row - and it still takes a line, which is exactly what makes it visible as a gap.
            lines.Add(
                new TextLine(
                    start,
                    consumedEnd - start,
                    Math.Max(0, contentEnd - start),
                    Measure(text, emphasis, measure, start, contentEnd)));

            return;
        }

        var cursor = start;

        while (cursor < contentEnd)
        {
            var fit = FitEnd(text, emphasis, measure, width, cursor, contentEnd);

            if (fit >= contentEnd)
            {
                lines.Add(new TextLine(cursor, consumedEnd - cursor, contentEnd - cursor, Measure(text, emphasis, measure, cursor, contentEnd)));
                return;
            }

            var space = LastSpaceBetween(text, cursor, fit);

            // A break at a space draws everything before it and consumes the space, so the next line
            // starts at a word rather than at the blank the wrap was taken for. With no space to break
            // at, the line's first word is wider than the limit: it is drawn whole, past the edge.
            var contentBreak = space > cursor ? space : WordEnd(text, cursor, contentEnd);
            var next = space > cursor ? space + 1 : contentBreak;

            // The run of blanks the break landed on belongs to the line it ended, which keeps the lines
            // covering the text without a gap.
            while (next < contentEnd && text[next] == ' ')
            {
                next++;
            }

            lines.Add(new TextLine(cursor, next - cursor, contentBreak - cursor, Measure(text, emphasis, measure, cursor, contentBreak)));
            cursor = next;
        }

        // The segment ended in trailing spaces that fit on the line above; it still ends the line, so
        // that the offsets after them belong to it and not to the next one.
        if (consumedEnd > contentEnd)
        {
            var last = lines[^1];
            lines[^1] = new TextLine(last.Start, consumedEnd - last.Start, last.ContentLength, last.Width);
        }
    }

    /// <summary>The index of the first space at or after <paramref name="start"/>, or <paramref name="end"/>.</summary>
    private static int WordEnd(string text, int start, int end)
    {
        var space = text.IndexOf(' ', start, end - start);

        return space < 0 ? end : space;
    }

    /// <summary>The end of the longest prefix of a segment that fits, and never less than one character.</summary>
    private static int FitEnd(
        string text,
        TextEmphasis emphasis,
        ITextMeasure measure,
        double width,
        int start,
        int end)
    {
        var fits = start;

        for (var candidate = start + 1; candidate <= end; candidate++)
        {
            if (Measure(text, emphasis, measure, start, candidate) > width)
            {
                break;
            }

            fits = candidate;
        }

        // A character wider than the whole line still has to be drawn somewhere, and drawing it past the
        // edge beats a loop that makes no progress.
        return fits == start ? start + 1 : fits;
    }

    /// <summary>The index of the last space in the fitting range, or the start when there is none.</summary>
    private static int LastSpaceBetween(string text, int start, int end)
    {
        var space = text.LastIndexOf(' ', Math.Max(start, end - 1), end - start);

        return space > start ? space : start;
    }

    private static double Measure(string text, TextEmphasis emphasis, ITextMeasure measure, int start, int end) =>
        end <= start ? 0 : measure.Measure(text[start..end], emphasis);
}

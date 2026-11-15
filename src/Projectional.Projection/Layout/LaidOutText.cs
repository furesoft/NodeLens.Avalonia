using Projectional.Language;

namespace Projectional.Projection;

/// <summary>
/// A text cell's text as the lines it is drawn in, with the conversions the caret and the page need
/// between an offset in the text and a point on the page.
/// </summary>
/// <remarks>
/// <para>
/// The lines are computed once, when the cell is laid out, and everything that asks where something in
/// the text sits reads them instead of measuring again. Recomputing would be the same answer - the text
/// and the measure have not changed, because a changed text is a new tree - at the price of measuring a
/// paragraph on every keystroke.
/// </para>
/// <para>
/// A text with no breaks and no limit is one line, which is what most cells are, so the single-line case
/// costs a wrapper call and one measure.
/// </para>
/// </remarks>
public sealed class LaidOutText
{
    private LaidOutText(string text, IReadOnlyList<TextLine> lines, double width, double lineHeight)
    {
        Text = text;
        Lines = lines;
        Width = width;
        LineHeight = lineHeight;
    }

    /// <summary>The text the lines were cut from, which is what the cell draws.</summary>
    public string Text { get; }

    /// <summary>The lines, in the order they are drawn.</summary>
    public IReadOnlyList<TextLine> Lines { get; }

    /// <summary>The width of the widest line, and never narrower than one space.</summary>
    public double Width { get; }

    /// <summary>The height of one line.</summary>
    public double LineHeight { get; }

    /// <summary>The height of the whole text.</summary>
    public double Height => Lines.Count * LineHeight;

    /// <summary>Whether the text is drawn on one line, which needs none of the line bookkeeping.</summary>
    public bool IsSingleLine => Lines.Count == 1;

    /// <summary>How many lines the text has.</summary>
    public int LineCount => Lines.Count;

    /// <summary>Breaks <paramref name="text"/> into lines as wide as <paramref name="limit"/> allows.</summary>
    /// <param name="text">The cell's text.</param>
    /// <param name="emphasis">How the text is drawn, which decides how wide it is.</param>
    /// <param name="measure">Measures the text.</param>
    /// <param name="limit">
    /// The widest line allowed, or <see langword="null"/> to honour hard breaks only.
    /// </param>
    public static LaidOutText Wrap(string text, TextEmphasis emphasis, ITextMeasure measure, double? limit = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(measure);

        var lines = TextWrapper.Lines(text, emphasis, measure, limit);
        var widest = 0.0;

        foreach (var line in lines)
        {
            widest = Math.Max(widest, line.Width);
        }

        return new LaidOutText(text, lines, Math.Max(widest, TextWrapper.MinimumWidth(emphasis, measure)), measure.LineHeight);
    }

    /// <summary>
    /// A text nobody measured: its own line breaks are honoured, and nothing is soft-wrapped, because
    /// there is no page edge to wrap at.
    /// </summary>
    public static LaidOutText OneLine(string text, TextEmphasis emphasis, ITextMeasure measure) =>
        Wrap(text, emphasis, measure, limit: null);

    /// <summary>The index of the line <paramref name="offset"/> is a caret position on.</summary>
    /// <remarks>
    /// The offset after the last character of the text belongs to the last line, because that is where
    /// typing appends to the text.
    /// </remarks>
    public int IndexAt(int offset)
    {
        for (var index = 0; index < Lines.Count; index++)
        {
            if (Lines[index].Holds(offset))
            {
                return index;
            }
        }

        return offset > Lines[^1].ContentEnd ? Lines.Count - 1 : 0;
    }

    /// <summary>The line <paramref name="offset"/> is a caret position on.</summary>
    public TextLine LineAt(int offset) => Lines[IndexAt(offset)];

    /// <summary>The characters the <paramref name="lineIndex"/>th line draws, without its break.</summary>
    public string ContentOf(int lineIndex)
    {
        var line = Lines[Math.Clamp(lineIndex, 0, Lines.Count - 1)];

        return line.IsEmpty ? string.Empty : Text[line.Start..line.ContentEnd];
    }

    /// <summary>How many characters into its line <paramref name="offset"/> is.</summary>
    public int ColumnOf(int offset)
    {
        var line = LineAt(offset);

        return Math.Clamp(offset - line.Start, 0, line.ContentLength);
    }

    /// <summary>The offset a caret standing at <paramref name="column"/> of a line stands on.</summary>
    public int OffsetAt(int lineIndex, int column)
    {
        var line = Lines[Math.Clamp(lineIndex, 0, Lines.Count - 1)];

        return line.Start + Math.Clamp(column, 0, line.ContentLength);
    }
}

namespace Projectional.Projection;

/// <summary>
/// One drawn line of a text cell: a range of the cell's text, its width, and how much of the text it
/// consumed.
/// </summary>
/// <remarks>
/// <para>
/// A line is a range rather than a string. The text is stored once, in the cell, and a line only says
/// which part of it is drawn where, so turning an offset - a caret position, an end of a selection, a
/// click - into a point on the page and back needs no copy of anything.
/// </para>
/// <para>
/// <see cref="Length"/> and <see cref="ContentLength"/> differ exactly where a line was continued: a
/// line broken at a space consumes the space, and a line that ends at a hard break consumes the
/// newline, but neither draws it. Converting between offsets and columns uses the second; partitioning
/// the text uses the first, so that consecutive lines cover the text without overlap and without a gap.
/// </para>
/// </remarks>
public readonly record struct TextLine
{
    /// <summary>Creates a line that starts at <paramref name="start"/>.</summary>
    /// <param name="start">The offset of the first character of the line.</param>
    /// <param name="length">How many offsets the line consumes, including the character it was broken at.</param>
    /// <param name="contentLength">How many characters of the line are drawn.</param>
    /// <param name="width">How wide the drawn characters are.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A value is negative, or more characters are drawn than the line consumes.
    /// </exception>
    public TextLine(int start, int length, int contentLength, double width)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        ArgumentOutOfRangeException.ThrowIfNegative(contentLength);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(contentLength, length, nameof(contentLength));
        ArgumentOutOfRangeException.ThrowIfNegative(width);

        Start = start;
        Length = length;
        ContentLength = contentLength;
        Width = width;
    }

    /// <summary>The offset of the first character of the line.</summary>
    public int Start { get; }

    /// <summary>How many offsets the line consumes, including the character it was broken at.</summary>
    public int Length { get; }

    /// <summary>How many characters of the line are drawn, which is the line without its break.</summary>
    public int ContentLength { get; }

    /// <summary>How wide the drawn characters are.</summary>
    public double Width { get; }

    /// <summary>The offset right after the line, which is where the next one starts.</summary>
    public int End => Start + Length;

    /// <summary>The offset right after the last drawn character.</summary>
    public int ContentEnd => Start + ContentLength;

    /// <summary>Whether the line draws nothing, which a blank line does.</summary>
    public bool IsEmpty => ContentLength == 0;

    /// <summary>Whether <paramref name="offset"/> is a caret position on this line.</summary>
    /// <remarks>
    /// A caret may stand after the last drawn character, because that is where typing continues a full
    /// line before it breaks. It may not stand on the break character itself, so that moving right
    /// through a wrapped cell steps from the end of one line to the start of the next.
    /// </remarks>
    public bool Holds(int offset) => offset >= Start && offset <= ContentEnd;

    public override string ToString() => $"[{Start}..{End}) {Width} wide";
}

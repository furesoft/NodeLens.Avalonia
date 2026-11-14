namespace Projectional.Projection;

/// <summary>
/// How much room a cell takes: width and height in layout units, not pixels.
/// </summary>
/// <remarks>
/// Layout units are whatever <see cref="ITextMeasure"/> says they are, which is why nothing in the
/// projection layer multiplies or divides them. A monospace measure makes one unit one character
/// column, an Avalonia measure will make it a device pixel, and neither changes a single line of
/// arrangement.
/// </remarks>
public readonly record struct LayoutSize(double Width, double Height)
{
    /// <summary>The size of nothing, which no cell has: even an empty cell is one line tall.</summary>
    public static LayoutSize Empty => default;

    public override string ToString() => $"{Width} x {Height}";
}

namespace Projectional.Language;

/// <summary>
/// How a block is decorated: the room around its content, a line around it, and what fills it.
/// </summary>
/// <remarks>
/// <para>
/// A decoration is notation, not a renderer: it says what region a block is, and the projection layer turns it into
/// space in the layout and a rectangle in the frame. That is what lets a table be a table without the renderer
/// knowing what a table is - the notation asks for a fill and a border, and the renderer draws a fill and a border.
/// </para>
/// <para>
/// The three parts are independent on purpose. Padding is room the content keeps for itself, a border is a line
/// drawn in that room's outside edge, and a fill is what the whole region is painted with; a notation may ask for
/// any of them alone. A paint with no fill, no border, no padding and no corner radius is empty and draws nothing,
/// so a notation that wants to say "plain" can leave the property null instead.
/// </para>
/// <para>
/// Every distance is in layout units and every colour is a <see cref="PaintRole"/>, so a decoration survives a
/// palette change and a font change alike. Nothing here knows what a pixel is.
/// </para>
/// </remarks>
public sealed record CellPaint
{
    /// <summary>Creates a decoration.</summary>
    /// <param name="padding">The room the content keeps for itself, inside the border.</param>
    /// <param name="border">The line drawn around the region.</param>
    /// <param name="fill">What fills the region, or <see langword="null"/> for nothing.</param>
    /// <param name="cornerRadius">How round the corners are, in layout units; zero draws square corners.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="cornerRadius"/> is negative.</exception>
    public CellPaint(
        CellEdges padding = default,
        CellBorder border = default,
        PaintRole? fill = null,
        double cornerRadius = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(cornerRadius);

        Padding = padding;
        Border = border;
        Fill = fill;
        CornerRadius = cornerRadius;
    }

    /// <summary>The room the content keeps for itself, inside the border.</summary>
    public CellEdges Padding { get; }

    /// <summary>The line drawn around the region.</summary>
    public CellBorder Border { get; }

    /// <summary>What fills the region, or <see langword="null"/> for nothing.</summary>
    public PaintRole? Fill { get; }

    /// <summary>How round the corners are, in layout units.</summary>
    public double CornerRadius { get; }

    /// <summary>
    /// The room the decoration takes on each side: its border outside its padding.
    /// </summary>
    /// <remarks>
    /// Layout grows a decorated cell by this much and pushes its content in by it, so the border sits outside the
    /// padding and both sit outside the content. One property rather than two, because every consumer that grows a
    /// cell by a decoration must grow it by exactly what layout does.
    /// </remarks>
    public CellEdges Inset => Padding.Plus(CellEdges.All(Border.Thickness));

    /// <summary>Whether the decoration draws nothing at all.</summary>
    public bool IsEmpty => Fill is null && !Border.IsVisible && Padding.IsZero && CornerRadius == 0;

    public override string ToString() =>
        IsEmpty ? "no paint" : $"paint {Padding}, {Border}, fill {Fill?.ToString() ?? "none"}, radius {CornerRadius}";
}

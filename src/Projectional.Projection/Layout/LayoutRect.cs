namespace Projectional.Projection;

/// <summary>
/// Where a cell ended up: an origin plus the size it occupies, in layout units.
/// </summary>
/// <remarks>
/// The origin is the top-left corner and the extent runs right and down, because that is how both a
/// text flow and a screen index space. The area is half-open, so a rectangle at (0, 0) that is 1 wide
/// contains x = 0 but not x = 1: neighbouring cells must not both claim the boundary column, or hit
/// testing would return whichever of them happened to be tested first.
/// </remarks>
public readonly record struct LayoutRect(double X, double Y, double Width, double Height)
{
    /// <summary>The first column to the right of the rectangle, which the rectangle does not include.</summary>
    public double Right => X + Width;

    /// <summary>The first row below the rectangle, which the rectangle does not include.</summary>
    public double Bottom => Y + Height;

    /// <summary>Whether the point falls inside the rectangle.</summary>
    public bool Contains(double x, double y) => x >= X && x < Right && y >= Y && y < Bottom;

    /// <summary>
    /// Whether the two rectangles share any area.
    /// </summary>
    /// <remarks>
    /// Half-open like <see cref="Contains"/>, so a rectangle that only touches another's edge does not
    /// intersect it. A viewport therefore never reports the cell that starts exactly at its right edge
    /// as visible, which is what keeps the visible set equal to what a draw pass would touch.
    /// </remarks>
    public bool Intersects(LayoutRect other) =>
        X < other.Right && other.X < Right && Y < other.Bottom && other.Y < Bottom;

    /// <summary>The same rectangle moved by <paramref name="dx"/> and <paramref name="dy"/>.</summary>
    public LayoutRect Offset(double dx, double dy) => new(X + dx, Y + dy, Width, Height);

    public override string ToString() => $"({X}, {Y}) {Width} x {Height}";
}

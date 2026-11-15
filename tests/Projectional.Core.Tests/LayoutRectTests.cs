using Projectional.Projection;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// Rectangles are the one piece of geometry every other part agrees on, so the two operations the
/// viewport added are pinned here rather than exercised only through it.
/// </summary>
public sealed class LayoutRectTests
{
    [Fact]
    public void OverlappingRectanglesIntersect()
    {
        var cell = new LayoutRect(5, 5, 10, 1);

        Assert.True(cell.Intersects(new LayoutRect(0, 0, 20, 20)));
        Assert.True(new LayoutRect(0, 0, 20, 20).Intersects(cell));
    }

    [Fact]
    public void RectanglesThatOnlyTouchAnEdgeDoNotIntersect()
    {
        // Half-open, like Contains: the cell starting exactly at the window's right edge starts where
        // the window ends, so it is not visible. Anything else would draw one cell too many.
        var window = new LayoutRect(0, 0, 10, 1);

        Assert.False(new LayoutRect(10, 0, 5, 1).Intersects(window));
        Assert.False(new LayoutRect(0, 1, 10, 1).Intersects(window));
        Assert.False(new LayoutRect(-5, 0, 5, 1).Intersects(window));
    }

    [Fact]
    public void DisjointRectanglesDoNotIntersect()
    {
        var window = new LayoutRect(0, 0, 10, 1);

        Assert.False(new LayoutRect(11, 0, 5, 1).Intersects(window));
        Assert.False(new LayoutRect(0, 2, 10, 1).Intersects(window));
    }

    [Fact]
    public void OffsetMovesARectangleWithoutResizingIt()
    {
        var moved = new LayoutRect(3, 4, 7, 2).Offset(-1, 2);

        Assert.Equal(new LayoutRect(2, 6, 7, 2), moved);
    }
}

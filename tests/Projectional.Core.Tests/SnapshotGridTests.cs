using Projectional.Projection;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The text grid a layout is drawn into: where characters land, what the snapshot looks like, and
/// what happens when a drawing does not fit.
/// </summary>
public sealed class SnapshotGridTests
{
    [Fact]
    public void ABlankGridRendersNothing()
        => Assert.Equal("", new SnapshotGrid(width: 4, height: 3).ToString());

    [Fact]
    public void DrawnTextKeepsItsPlaceAndGivesUpTrailingBlanks()
    {
        var grid = new SnapshotGrid(width: 4, height: 2);

        grid.Write(row: 0, column: 2, text: "ab");
        grid.Write(row: 1, column: 0, text: "c");

        Assert.Equal("  ab\nc", grid.ToString());
    }

    [Fact]
    public void ABlankLineInTheMiddleIsKept()
    {
        var grid = new SnapshotGrid(width: 3, height: 3);

        grid.Write(row: 0, column: 0, text: "a");
        grid.Write(row: 2, column: 0, text: "b");

        Assert.Equal("a\n\nb", grid.ToString());
    }

    [Fact]
    public void DrawingOverATextReplacesIt()
    {
        var grid = new SnapshotGrid(width: 3, height: 1);

        grid.Write(row: 0, column: 0, text: "abc");
        grid.Write(row: 0, column: 1, text: "z");

        Assert.Equal("azc", grid.ToString());
    }

    [Fact]
    public void DrawingAnEmptyTextChangesNothing()
    {
        var grid = new SnapshotGrid(width: 2, height: 1);

        grid.Write(row: 0, column: 0, text: "a");
        grid.Write(row: 0, column: 2, text: "");
        grid.Write(row: 0, column: 1, text: "");

        Assert.Equal("a", grid.ToString());
    }

    [Fact]
    public void TextThatWouldLeaveTheGridIsRefused()
    {
        var grid = new SnapshotGrid(width: 4, height: 1);

        var refused = Assert.Throws<ArgumentException>(() => grid.Write(row: 0, column: 2, text: "abc"));

        Assert.Contains("does not fit", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ARowBelowTheGridIsRefused()
    {
        var grid = new SnapshotGrid(width: 2, height: 1);

        Assert.Throws<ArgumentOutOfRangeException>(() => grid.Write(row: 1, column: 0, text: "a"));
    }

    [Fact]
    public void ANegativePositionIsRefused()
    {
        var grid = new SnapshotGrid(width: 2, height: 2);

        Assert.Throws<ArgumentOutOfRangeException>(() => grid.Write(row: -1, column: 0, text: "a"));
        Assert.Throws<ArgumentOutOfRangeException>(() => grid.Write(row: 0, column: -1, text: "a"));
    }

    [Fact]
    public void AGridWithoutRoomIsRefused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SnapshotGrid(width: 0, height: 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SnapshotGrid(width: 1, height: 0));
    }
}

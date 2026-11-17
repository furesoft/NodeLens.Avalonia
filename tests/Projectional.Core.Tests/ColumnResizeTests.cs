using Projectional.Core;
using Projectional.Editing;
using Projectional.Projection;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// Resizing a table's columns: the width is view state, it is a floor rather than an exact width, and
/// the boundary a reader grabs is the right edge of a column inside the header row.
/// </summary>
public sealed class ColumnResizeTests
{
    [Fact]
    public void AResizedColumnPushesTheNextOneRight()
    {
        var (session, body) = Table();

        var before = session.Layout.BoundsOf(Header(session, 1))!.Value.X;

        session.SetColumnWidth(body.Id, "steps", 0, 12);

        var after = session.Layout.BoundsOf(Header(session, 1))!.Value.X;

        // The first column was four characters wide ("step") and is now twelve, so the second header
        // starts eight units further right.
        Assert.Equal(before + 8, after);
    }

    [Fact]
    public void AWidthNarrowerThanTheContentIsIgnored()
    {
        var (session, body) = Table();

        var before = session.Layout.Size.Width;

        session.SetColumnWidth(body.Id, "steps", 0, 1);

        // "step" is four characters, so a column asked to be one character wide is still four: a column
        // narrower than its content would clip a value, and the model is what the reader came to read.
        Assert.Equal(before, session.Layout.Size.Width);
    }

    [Fact]
    public void ResizingIsViewStateAndCreatesNoUndoStep()
    {
        var (session, body) = Table();

        session.SetColumnWidth(body.Id, "steps", 0, 12);

        Assert.False(session.History.CanUndo);
        Assert.Equal(12, session.ColumnWidths.WidthOf(body.Id, "steps", 0));
    }

    [Fact]
    public void AWidthSurvivesAModelChange()
    {
        var (session, body) = Table();

        session.SetColumnWidth(body.Id, "steps", 0, 12);

        body.Model.NewChild(body, "steps", TestLanguage.Step).Set("text", "Send");
        session.Refresh();

        Assert.Equal(12, session.ColumnWidths.WidthOf(body.Id, "steps", 0));
        Assert.Equal(12, session.Layout.BoundsOf(Header(session, 0))!.Value.Width + 8);
    }

    [Fact]
    public void ResettingPutsEveryColumnBackToItsContentWidth()
    {
        var (session, body) = Table();

        var before = session.Layout.Size.Width;

        session.SetColumnWidth(body.Id, "steps", 0, 12);
        session.ResetColumnWidths();

        Assert.True(session.ColumnWidths.IsEmpty);
        Assert.Equal(before, session.Layout.Size.Width);
    }

    [Fact]
    public void TheHandleIsTheRightEdgeOfAColumnInsideTheHeaderRow()
    {
        var (session, body) = Table();

        // "step" is four characters wide, so its right edge is at x = 4, and the header row is the
        // first line of the table.
        var handle = ColumnResize.HandleAt(session, x: 4, y: 0, tolerance: 1);

        Assert.NotNull(handle);
        Assert.Equal(body.Id, handle.Value.Owner);
        Assert.Equal("steps", handle.Value.Slot);
        Assert.Equal(0, handle.Value.Column);
        Assert.Equal(4, handle.Value.Width);
        Assert.Equal(4, handle.Value.Boundary);
    }

    [Fact]
    public void APointAwayFromEveryBoundaryIsNoHandle()
    {
        var (session, _) = Table();

        Assert.Null(ColumnResize.HandleAt(session, x: 2, y: 0, tolerance: 1));
    }

    [Fact]
    public void TheBodyOfATableIsNotAHandle()
    {
        var (session, _) = Table();

        // The same x as the boundary, but on the row below the header: the body is where the reader
        // selects and types, so a boundary there would turn a click into a drag.
        Assert.Null(ColumnResize.HandleAt(session, x: 4, y: 1, tolerance: 1));
    }

    private static (EditorSession Session, Node Body) Table()
    {
        var model = TestLanguage.NewModel();
        var body = model.NewNode(TestLanguage.Body);

        var pay = model.NewChild(body, "steps", TestLanguage.Step);
        pay.Set("text", "Pay");

        return (new EditorSession(body, TableNotations.Registry), body);
    }

    private static EditorCell Header(EditorSession session, int column) =>
        session.Tree.Cells().OfType<TableCell>().Single().Headers[column];
}

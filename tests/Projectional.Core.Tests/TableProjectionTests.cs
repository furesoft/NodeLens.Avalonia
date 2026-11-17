using Projectional.Core;
using Projectional.Editing;
using Projectional.Projection;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// A concept drawn as a table: what the grid looks like, where its cells sit, and that the caret can
/// still reach them.
/// </summary>
/// <remarks>
/// The point of these tests is that a table is a layout, not a second kind of model. Every cell keeps
/// the address the row's own notation would have given it, so nothing about editing, undo or the caret
/// had to learn about tables.
/// </remarks>
public sealed class TableProjectionTests
{
    [Fact]
    public void RendersRowsUnderTheirHeadersInAlignedColumns()
    {
        var model = TestLanguage.NewModel();
        var body = model.NewNode(TestLanguage.Body);

        var pay = model.NewChild(body, "steps", TestLanguage.Step);
        pay.Set("text", "Pay");
        pay.Set("index", 1);

        var send = model.NewChild(body, "steps", TestLanguage.Step);
        send.Set("text", "Send");
        send.Set("index", 22);
        model.NewChild(send, "detail", TestLanguage.Step).Set("text", "x");

        Assert.Equal(
            """
            step no detail
            Pay  1  <empty>
            Send 22 x
            """,
            LayoutSnapshot.Render(model, TableNotations.Registry, width: 40));
    }

    [Fact]
    public void AnEmptyTableShowsItsHeaderAndAnAddRowAffordance()
    {
        var model = TestLanguage.NewModel();
        model.NewNode(TestLanguage.Body);

        Assert.Equal(
            """
            step    no detail
            <empty>
            """,
            LayoutSnapshot.Render(model, TableNotations.Registry, width: 40));
    }

    [Fact]
    public void EveryCellKeepsTheAddressItsRowWouldHave()
    {
        var model = TestLanguage.NewModel();
        var body = model.NewNode(TestLanguage.Body);
        var pay = model.NewChild(body, "steps", TestLanguage.Step);
        var send = model.NewChild(body, "steps", TestLanguage.Step);

        var table = Assert.Single(
            new EditorTreeBuilder(TableNotations.Registry).Build(body).Cells().OfType<TableCell>());

        Assert.Equal("steps", table.Slot);
        Assert.Equal(3, table.ColumnCount);
        Assert.Equal(2, table.Rows.Count);
        Assert.False(table.IsEmpty);
        Assert.Equal(CellAddress.OfSlot(pay.Id, "text"), table.Rows[0][0].Address);
        Assert.Equal(CellAddress.OfSlot(send.Id, "text"), table.Rows[1][0].Address);
        Assert.Equal(CellAddress.OfSlot(send.Id, "index"), table.Rows[1][1].Address);
        // Headers belong to the table's notation, so they are the owning node's cells, not a row's.
        Assert.Equal(body.Id, table.Headers[0].Address.Owner);
    }

    [Fact]
    public void TheCaretStepsFromOneColumnToTheNext()
    {
        var model = TestLanguage.NewModel();
        var body = model.NewNode(TestLanguage.Body);
        var pay = model.NewChild(body, "steps", TestLanguage.Step);
        pay.Set("text", "Pay");

        using var session = new EditorSession(body, TableNotations.Registry);

        session.Select(Selection.At(CaretPosition.At(CellAddress.OfSlot(pay.Id, "text"), 3)));

        Assert.Equal(3, session.Selection.Caret.Offset);

        session.Apply(new MoveCaretAction(CaretMove.Right));

        Assert.Equal(CellAddress.OfSlot(pay.Id, "index"), session.Selection.Caret.Address);
    }

    [Fact]
    public void ATableGrowsARowWhenTheModelGainsOne()
    {
        var model = TestLanguage.NewModel();
        var body = model.NewNode(TestLanguage.Body);
        var pay = model.NewChild(body, "steps", TestLanguage.Step);
        pay.Set("text", "Pay");

        using var session = new EditorSession(body, TableNotations.Registry);

        var shortest = session.Layout.Size.Height;

        model.NewChild(body, "steps", TestLanguage.Step).Set("text", "Send");

        session.Refresh();

        // A row is a line, so the second one makes the grid taller without changing any column.
        Assert.True(session.Layout.Size.Height > shortest);
    }

    [Fact]
    public void TheAffordanceOfAnEmptyTableIsAnEmptySlotTheCaretCanStandIn()
    {
        var model = TestLanguage.NewModel();
        var body = model.NewNode(TestLanguage.Body);

        using var session = new EditorSession(body, TableNotations.Registry);

        var place = CaretNavigation.PlaceAt(
            session.Tree,
            CaretPosition.At(CellAddress.OfSlot(body.Id, "steps"), 0));

        Assert.NotNull(place);
        Assert.IsType<AddRowCell>(place.Value.Cell);
        Assert.True(place.Value.IsEmptySlot);
    }

    [Fact]
    public void EnterOnTheAffordanceCreatesTheFirstRow()
    {
        var model = TestLanguage.NewModel();
        var body = model.NewNode(TestLanguage.Body);

        using var session = new EditorSession(body, TableNotations.Registry);

        session.Select(Selection.At(CaretPosition.At(CellAddress.OfSlot(body.Id, "steps"), 0)));
        session.Apply(KeyRouter.For(session, EditorKey.Enter));

        var row = Assert.Single(body.GetChildren("steps"));
        Assert.Equal(TestLanguage.Step, row.Concept.Key);

        // The affordance is gone: the table draws the row it just created.
        Assert.DoesNotContain(session.Tree.Cells().OfType<AddRowCell>(), _ => true);
    }

    [Fact]
    public void TabReachesTheAffordanceOfAnEmptyTable()
    {
        var model = TestLanguage.NewModel();
        var body = model.NewNode(TestLanguage.Body);

        using var session = new EditorSession(body, TableNotations.Registry);

        session.Select(Selection.At(CaretPosition.At(new CellAddress(body.Id), 0)));

        for (var press = 0; press < 8; press++)
        {
            if (CaretNavigation.PlaceAt(session.Tree, session.Selection.Caret) is { Cell: AddRowCell })
            {
                return;
            }

            session.Apply(KeyRouter.For(session, EditorKey.Tab));
        }

        Assert.Fail("Tab never reached the table's add-row affordance.");
    }
}

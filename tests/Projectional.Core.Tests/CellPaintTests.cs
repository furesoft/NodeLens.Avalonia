using Projectional.Core;
using Projectional.Editing;
using Projectional.Language;
using Projectional.Projection;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// A decoration is notation, not a renderer: it is room the content keeps, a line around the region and a fill
/// behind it. Layout turns the first two into size and the frame hands the third to the painter as a region and a
/// meaning, so a renderer draws a box without knowing whether it surrounds a table, a heading or a warning.
/// </summary>
/// <remarks>
/// Everything here is measured by <see cref="MonospaceTextMeasure"/>, so one layout unit is one character and the
/// expected numbers can be counted off the decoration by hand.
/// </remarks>
public sealed class CellPaintTests
{
    private static readonly CellLayouter Layouter = new(MonospaceTextMeasure.Instance);

    [Fact]
    public void AnInsetIsTheBorderOutsideThePadding()
    {
        var paint = new CellPaint(
            new CellEdges(top: 1, right: 2, bottom: 3, left: 4),
            new CellBorder(thickness: 2));

        Assert.Equal(new CellEdges(top: 3, right: 4, bottom: 5, left: 6), paint.Inset);
    }

    [Fact]
    public void APaintThatAsksForNothingIsEmpty()
    {
        Assert.True(new CellPaint().IsEmpty);
        Assert.False(new CellPaint(fill: PaintRole.Muted).IsEmpty);
        Assert.False(new CellPaint(border: new CellBorder(thickness: 1)).IsEmpty);
        Assert.False(new CellPaint(padding: CellEdges.All(1)).IsEmpty);
        Assert.False(new CellPaint(cornerRadius: 2).IsEmpty);
    }

    [Fact]
    public void ADecoratedBlockGrowsByItsInsetsAndPushesItsContentIn()
    {
        var (layout, container, text) = Layout(
            new CellPaint(
                new CellEdges(top: 1, right: 2, bottom: 1, left: 2),
                new CellBorder(thickness: 1)));

        // The text is one character wide and one line tall; the decoration adds three units on each side
        // horizontally and two vertically, so the block is seven by five and the text sits at its inset.
        AssertRect(0, 0, 7, 5, Bounds(layout, container));
        AssertRect(3, 2, 1, 1, Bounds(layout, text));
        Assert.Equal(7, layout.Size.Width);
        Assert.Equal(5, layout.Size.Height);
    }

    [Fact]
    public void AnUndecoratedBlockIsExactlyItsContent()
    {
        var (layout, container, text) = Layout(paint: null);

        AssertRect(0, 0, 1, 1, Bounds(layout, container));
        AssertRect(0, 0, 1, 1, Bounds(layout, text));
    }

    [Fact]
    public void TheFrameCarriesTheDecorationOverTheCellsOwnBounds()
    {
        var paint = new CellPaint(new CellEdges(top: 1, right: 1, bottom: 1, left: 1), fill: PaintRole.Muted);

        var (layout, container, _) = Layout(paint);

        var frame = Frame(layout);

        var region = Assert.Single(frame.Paints);

        Assert.Same(container, region.Cell);
        Assert.Equal(paint, region.Paint);
        Assert.Equal(Bounds(layout, container), region.Bounds);
    }

    [Fact]
    public void AnUndecoratedDocumentContributesNoPaint()
    {
        var (layout, _, _) = Layout(paint: null);

        Assert.Empty(Frame(layout).Paints);
    }

    [Fact]
    public void ATableHeaderIsOneBandAcrossTheWholeGrid()
    {
        var (layout, table, header) = LayoutTable(fill: PaintRole.Muted, paint: null);

        var band = Assert.Single(Frame(layout).Paints);

        Assert.Same(table, band.Cell);
        Assert.Equal(PaintRole.Muted, band.Paint.Fill);

        // The header's own text is one character wide, the column under it four, so the band is the column and not
        // the text: a fill that stopped at the text would leave most of the column unmarked.
        AssertRect(0, 0, 4, 1, band.Bounds);
        AssertRect(0, 0, 1, 1, Bounds(layout, header));
    }

    [Fact]
    public void ABoxedTablesHeaderBandStartsInsideItsOwnPadding()
    {
        var paint = new CellPaint(CellEdges.All(1), new CellBorder(thickness: 1));

        var (layout, table, _) = LayoutTable(fill: PaintRole.Muted, paint);

        var band = Assert.Single(Frame(layout).Paints, region => !ReferenceEquals(region.Paint, paint));

        AssertRect(2, 2, 4, 1, band.Bounds);
        Assert.Equal(Bounds(layout, table), Frame(layout).Paints[0].Bounds);
    }

    [Fact]
    public void ATableWithoutAHeaderRoleContributesNoBand()
    {
        var (layout, _, _) = LayoutTable(fill: null, paint: null);

        Assert.Empty(Frame(layout).Paints);
    }

    private static (CellLayout Layout, TableCell Table, ConstantCell Header) LayoutTable(
        PaintRole? fill,
        CellPaint? paint)
    {
        var node = TestLanguage.NewModel().NewNode(TestLanguage.Rule);
        var address = new CellAddress(node.Id);

        var header = new ConstantCell(address, "a", ConstantKind.Keyword);
        var wider = new ConstantCell(address, "aaaa", ConstantKind.Keyword);

        var table = new TableCell(address, "rules", "<empty>", [header], [[wider]], headerFill: fill)
        {
            Paint = paint,
        };

        return (Layouter.Layout(new EditorTree(node, table)), table, header);
    }

    private static (CellLayout Layout, ContainerCell Container, ConstantCell Text) Layout(CellPaint? paint)
    {
        var node = TestLanguage.NewModel().NewNode(TestLanguage.Rule);
        var address = new CellAddress(node.Id);

        var text = new ConstantCell(address, "x", ConstantKind.Keyword);
        var container = new ContainerCell(address, CellOrientation.Vertical, text) { Paint = paint };
        var tree = new EditorTree(node, container);

        return (Layouter.Layout(tree), container, text);
    }

    private static EditorFrame Frame(CellLayout layout) =>
        EditorFrame.Build(
            layout,
            new LayoutSize(layout.Size.Width, layout.Size.Height),
            0,
            0,
            Selection.At(CaretPosition.At(layout.Tree.Root.Address)),
            MonospaceTextMeasure.Instance);

    private static LayoutRect Bounds(CellLayout layout, EditorCell cell) =>
        layout.BoundsOf(cell) ?? throw new InvalidOperationException($"The cell {cell} was not placed.");

    private static void AssertRect(double x, double y, double width, double height, LayoutRect actual) =>
        Assert.Equal(new LayoutRect(x, y, width, height), actual);
}

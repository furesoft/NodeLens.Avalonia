using Projectional.Core;
using Projectional.Language;
using Projectional.Projection;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// A viewport is the scroll window over a layout: how far the control may scroll, which cells that
/// makes visible, and how the caret pulls the window along when it moves out of sight.
/// </summary>
/// <remarks>
/// The document is a rule holding three steps, which lays out as five rows: the header, the three
/// steps, the <c>extends</c> line and the closing brace. Laid out in monospace it is 41 wide and 5
/// tall, so a two-row window has three rows of scroll range and every expected number here can be
/// counted off the notation by hand - the same reason the layout tests measure in characters.
/// </remarks>
public sealed class LayoutViewportTests
{
    private static readonly EditorTreeBuilder Builder = new(TestNotations.Registry);
    private static readonly CellLayouter Layouter = new(MonospaceTextMeasure.Instance);

    /// <summary>The cell the <c>extends</c> line starts with, on the fourth row.</summary>
    private static readonly LayoutRect ExtendsLine = new(10, 3, 7, 1);

    [Fact]
    public void TheScrollableExtentIsTheContentSizeMinusTheWindow()
    {
        var layout = Layout();

        Assert.Equal(new LayoutSize(21, 3), LayoutViewport.ScrollableExtent(layout, new LayoutSize(20, 2)));
    }

    [Fact]
    public void ContentSmallerThanTheWindowCannotScroll()
    {
        var layout = Layout();

        Assert.Equal(new LayoutSize(0, 0), LayoutViewport.ScrollableExtent(layout, new LayoutSize(80, 20)));
    }

    [Fact]
    public void AnOffsetPastTheEndIsClampedToTheScrollableExtent()
    {
        var viewport = LayoutViewport.Of(Layout(), new LayoutSize(20, 2), offsetX: 100, offsetY: 100);

        Assert.Equal(21, viewport.OffsetX);
        Assert.Equal(3, viewport.OffsetY);
    }

    [Fact]
    public void AnOffsetBeforeTheStartIsClampedToZero()
    {
        var viewport = LayoutViewport.Of(Layout(), new LayoutSize(20, 2), offsetX: -4, offsetY: -1);

        Assert.Equal(0, viewport.OffsetX);
        Assert.Equal(0, viewport.OffsetY);
    }

    [Fact]
    public void OnlyCellsThatIntersectTheWindowAreListed()
    {
        var layout = Layout();

        var atTop = LayoutViewport.Of(layout, new LayoutSize(41, 2), 0, 0);
        var atBottom = LayoutViewport.Of(layout, new LayoutSize(41, 2), 0, 3);

        Assert.Equal(40, atTop.Cells.Count);
        Assert.Equal(8, atBottom.Cells.Count);
        Assert.DoesNotContain(atTop.Cells, cell => cell.Bounds == ExtendsLine);
        Assert.Contains(atBottom.Cells, cell => cell.Bounds == ExtendsLine);
    }

    [Fact]
    public void EveryListedCellIntersectsTheWindowAndNothingElseIsLeftOut()
    {
        var layout = Layout();
        var viewport = LayoutViewport.Of(layout, new LayoutSize(20, 2), 5, 1);
        var window = viewport.Window;

        Assert.Equal(20, viewport.Cells.Count);
        Assert.All(viewport.Cells, cell => Assert.True(cell.Bounds.Intersects(window)));
        Assert.All(
            layout.Cells.Where(cell => !cell.Bounds.Intersects(window)),
            cell => Assert.DoesNotContain(viewport.Cells, listed => listed.Cell == cell.Cell));
    }

    [Fact]
    public void TheCellUnderTheWholeDocumentIsVisibleFromEveryScrollOffset()
    {
        var layout = Layout();

        foreach (var offset in new[] { 0, 1, 2, 3 })
        {
            var viewport = LayoutViewport.Of(layout, new LayoutSize(41, 2), 0, offset);

            Assert.Contains(viewport.Cells, cell => cell.Cell == layout.Tree.Root);
        }
    }

    [Fact]
    public void ScrollIntoViewDoesNotMoveWhenTheTargetIsAlreadyVisible()
    {
        var (x, y) = LayoutViewport.ScrollIntoView(Layout(), new LayoutSize(41, 2), 0, 3, ExtendsLine);

        Assert.Equal(0, x);
        Assert.Equal(3, y);
    }

    [Fact]
    public void ScrollIntoViewMovesJustFarEnoughToRevealATargetBelowTheWindow()
    {
        var (x, y) = LayoutViewport.ScrollIntoView(Layout(), new LayoutSize(41, 2), 0, 0, ExtendsLine);

        // The line ends at row 4 and the window is two rows tall, so row 2 is the nearest offset that
        // shows it - not row 3, which would also show it but would scroll one row further than needed.
        Assert.Equal(0, x);
        Assert.Equal(2, y);
    }

    [Fact]
    public void ScrollIntoViewAlignsATargetTallerThanTheWindowToItsStart()
    {
        var layout = Layout();
        var whole = new LayoutRect(0, 0, 41, 5);

        var (_, y) = LayoutViewport.ScrollIntoView(layout, new LayoutSize(41, 2), 0, 3, whole);

        Assert.Equal(0, y);
    }

    [Fact]
    public void ScrollIntoViewScrollsSidewaysForAnAnchorOffToTheRight()
    {
        var brace = new LayoutRect(23, 0, 1, 1);

        var (x, y) = LayoutViewport.ScrollIntoView(Layout(), new LayoutSize(20, 2), 0, 0, brace);

        Assert.Equal(4, x);
        Assert.Equal(0, y);
    }

    [Fact]
    public void ToViewportSubtractsTheOffsetAndToLayoutAddsItBack()
    {
        var viewport = LayoutViewport.Of(Layout(), new LayoutSize(41, 2), 0, 2);

        Assert.Equal(new LayoutRect(10, 1, 7, 1), viewport.ToViewport(ExtendsLine));
        Assert.Equal((10.0, 3.0), viewport.ToLayout(10, 1));
    }

    [Fact]
    public void TheWindowIsTheViewportSizePlacedAtTheOffset()
    {
        var viewport = LayoutViewport.Of(Layout(), new LayoutSize(20, 2), 5, 1);

        Assert.Equal(new LayoutRect(5, 1, 20, 2), viewport.Window);
    }

    private static CellLayout Layout() => Layouter.Layout(Builder.Build(Document()));

    private static Node Document()
    {
        var (rule, body, _) = TestEditor.RuleWithStep();
        body.Model.NewChild(body, "steps", TestLanguage.Step);
        body.Model.NewChild(body, "steps", TestLanguage.Step);

        return rule;
    }
}

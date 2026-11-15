using Projectional.Core;
using Projectional.Editing;
using Projectional.Language;
using Projectional.Projection;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// A selection paints as one box per cell it touches, trimmed to the characters the two ends name.
/// </summary>
/// <remarks>
/// The fixture is the rule-with-three-steps document of the viewport tests. Its header row is
/// <c>rule unnamed as &lt;none&gt; {</c>, so a selection from the end of <c>unnamed</c> into the start of
/// the tag covers exactly the three cells between them - the space, <c>as</c> and the space after it -
/// and the ends themselves stay unpainted because no character of them is selected.
/// </remarks>
public sealed class SelectionRectsTests
{
    private static readonly EditorTreeBuilder Builder = new(TestNotations.Registry);
    private static readonly CellLayouter Layouter = new(MonospaceTextMeasure.Instance);

    [Fact]
    public void AnEmptySelectionHighlightsNothing()
    {
        var (layout, rule) = Layout();
        var caret = CaretPosition.At(CellAddress.OfSlot(rule.Id, "name"), 2);

        Assert.Empty(SelectionRects.Of(layout.Tree, layout, Selection.At(caret), MonospaceTextMeasure.Instance));
    }

    [Fact]
    public void ASelectionInsideOneCellHighlightsTheCharactersBetweenItsEnds()
    {
        var (layout, rule) = Layout();
        var name = CellAddress.OfSlot(rule.Id, "name");
        var selection = new Selection(CaretPosition.At(name, 3), CaretPosition.At(name, 1));

        var rects = SelectionRects.Of(layout.Tree, layout, selection, MonospaceTextMeasure.Instance);

        Assert.Equal([new LayoutRect(6, 0, 2, 1)], rects);
    }

    [Fact]
    public void ASelectionAcrossCellsHighlightsEachCellSeparately()
    {
        var (layout, rule) = Layout();
        var selection = new Selection(
            CaretPosition.At(CellAddress.OfSlot(rule.Id, "name"), "unnamed".Length),
            CaretPosition.At(CellAddress.OfSlot(rule.Id, "tag")));

        var rects = SelectionRects.Of(layout.Tree, layout, selection, MonospaceTextMeasure.Instance);

        Assert.Equal(
            [new LayoutRect(12, 0, 1, 1), new LayoutRect(13, 0, 2, 1), new LayoutRect(15, 0, 1, 1)],
            rects);
    }

    [Fact]
    public void ReversingTheEndsHighlightsTheSameCells()
    {
        var (layout, rule) = Layout();
        var name = CaretPosition.At(CellAddress.OfSlot(rule.Id, "name"), "unnamed".Length);
        var tag = CaretPosition.At(CellAddress.OfSlot(rule.Id, "tag"));

        var forward = SelectionRects.Of(layout.Tree, layout, new Selection(name, tag), MonospaceTextMeasure.Instance);
        var backward = SelectionRects.Of(layout.Tree, layout, new Selection(tag, name), MonospaceTextMeasure.Instance);

        Assert.Equal(forward, backward);
    }

    [Fact]
    public void ASelectionThatNamesNoCellHighlightsNothing()
    {
        var (layout, rule) = Layout();
        var nowhere = CaretPosition.At(new CellAddress(TestEditor.BareTag().Id));
        var selection = new Selection(CaretPosition.At(CellAddress.OfSlot(rule.Id, "name"), 1), nowhere);

        Assert.Empty(SelectionRects.Of(layout.Tree, layout, selection, MonospaceTextMeasure.Instance));
    }

    private static (CellLayout Layout, Node Rule) Layout()
    {
        var (rule, body, _) = TestEditor.RuleWithStep();
        body.Model.NewChild(body, "steps", TestLanguage.Step);
        body.Model.NewChild(body, "steps", TestLanguage.Step);

        return (Layouter.Layout(Builder.Build(rule)), rule);
    }
}

using Projectional.Core;
using Projectional.Editing;
using Projectional.Language;
using Projectional.Projection;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// Caret geometry is what turns a position - an address and an offset - into a bar a renderer can paint
/// and a control can scroll to.
/// </summary>
/// <remarks>
/// The document is the rule-with-three-steps fixture the viewport tests use: 41 by 5 in monospace, with
/// the <c>name</c> property on the header row at (5, 0) and seven characters wide, which is why every
/// expected x here can be counted off the notation.
/// </remarks>
public sealed class CaretBoundsTests
{
    private static readonly EditorTreeBuilder Builder = new(TestNotations.Registry);
    private static readonly CellLayouter Layouter = new(MonospaceTextMeasure.Instance);

    [Fact]
    public void ACaretAtTheStartOfACellStandsAtItsLeftEdge()
    {
        var (layout, rule) = Layout();

        var bounds = CaretBounds.Of(
            layout.Tree,
            layout,
            CaretPosition.At(CellAddress.OfSlot(rule.Id, "name")),
            MonospaceTextMeasure.Instance);

        Assert.Equal(new LayoutRect(5, 0, 0, 1), bounds);
    }

    [Fact]
    public void ACaretInsideACellStandsAsManyCharactersInAsItsOffset()
    {
        var (layout, rule) = Layout();

        var bounds = CaretBounds.Of(
            layout.Tree,
            layout,
            CaretPosition.At(CellAddress.OfSlot(rule.Id, "name"), 3),
            MonospaceTextMeasure.Instance);

        Assert.Equal(8, bounds!.Value.X);
    }

    [Fact]
    public void ACaretAtTheEndOfACellStandsAtItsRightEdge()
    {
        var (layout, rule) = Layout();

        var bounds = CaretBounds.Of(
            layout.Tree,
            layout,
            CaretPosition.At(CellAddress.OfSlot(rule.Id, "name"), "unnamed".Length),
            MonospaceTextMeasure.Instance);

        Assert.Equal(12, bounds!.Value.X);
        Assert.Equal(12, layout.BoundsOf(CellAddress.OfSlot(rule.Id, "name"))!.Value.Right);
    }

    [Fact]
    public void AnOffsetPastTheEndIsClampedToTheRightEdge()
    {
        var (layout, rule) = Layout();

        var bounds = CaretBounds.Of(
            layout.Tree,
            layout,
            CaretPosition.At(CellAddress.OfSlot(rule.Id, "name"), 99),
            MonospaceTextMeasure.Instance);

        Assert.Equal(12, bounds!.Value.X);
    }

    [Fact]
    public void ACaretOnTheFourthRowIsDrawnOnTheFourthRow()
    {
        var (layout, rule) = Layout();

        var bounds = CaretBounds.Of(
            layout.Tree,
            layout,
            CaretPosition.At(CellAddress.OfSlot(rule.Id, "extends")),
            MonospaceTextMeasure.Instance);

        Assert.Equal(new LayoutRect(10, 3, 0, 1), bounds);
    }

    [Fact]
    public void ACaretThatNamesNoCellIsNotDrawn()
    {
        var (layout, _) = Layout();
        var nowhere = CaretPosition.At(new CellAddress(TestEditor.BareTag().Id));

        Assert.Null(CaretBounds.Of(layout.Tree, layout, nowhere, MonospaceTextMeasure.Instance));
    }

    private static (CellLayout Layout, Node Rule) Layout()
    {
        var (rule, body, _) = TestEditor.RuleWithStep();
        body.Model.NewChild(body, "steps", TestLanguage.Step);
        body.Model.NewChild(body, "steps", TestLanguage.Step);

        return (Layouter.Layout(Builder.Build(rule)), rule);
    }
}

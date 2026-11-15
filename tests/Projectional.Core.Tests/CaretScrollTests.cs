using Projectional.Core;
using Projectional.Editing;
using Projectional.Language;
using Projectional.Projection;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// Typing has to keep the caret on screen without the view jumping around: an offset that already shows
/// the caret must stay put, and one that does not must move exactly far enough to touch the nearest edge.
/// </summary>
/// <remarks>
/// The fixture is the rule-with-three-steps document of the viewport tests, 41 by 5 in monospace. The
/// header row holds <c>rule unnamed as &lt;none&gt; {</c>, the three steps share the second row, the
/// <c>extends</c> line is the fourth and the closing brace the fifth.
/// </remarks>
public sealed class CaretScrollTests
{
    private static readonly EditorTreeBuilder Builder = new(TestNotations.Registry);
    private static readonly CellLayouter Layouter = new(MonospaceTextMeasure.Instance);

    [Fact]
    public void ACaretAlreadyOnScreenLeavesTheOffsetAlone()
    {
        var (layout, rule) = Layout();

        Assert.Equal(
            (0, 0),
            Scroll(layout, new LayoutSize(41, 3), 0, 0, CaretPosition.At(CellAddress.OfSlot(rule.Id, "name"), 3)));
    }

    [Fact]
    public void ACaretBelowTheWindowScrollsJustFarEnoughToShowIt()
    {
        var (layout, rule) = Layout();

        Assert.Equal(
            (0, 1),
            Scroll(layout, new LayoutSize(41, 3), 0, 0, CaretPosition.At(CellAddress.OfSlot(rule.Id, "extends"))));
    }

    [Fact]
    public void ACaretAboveTheWindowScrollsUpToTheTopEdge()
    {
        var (layout, rule) = Layout();

        Assert.Equal(
            (0, 0),
            Scroll(layout, new LayoutSize(41, 3), 0, 2, CaretPosition.At(CellAddress.OfSlot(rule.Id, "name"))));
    }

    [Fact]
    public void ACaretPastTheRightEdgeScrollsToItsColumn()
    {
        var (layout, rule) = Layout();

        Assert.Equal(
            (4, 0),
            Scroll(layout, new LayoutSize(12, 5), 0, 0, CaretPosition.At(CellAddress.OfSlot(rule.Id, "tag"))));
    }

    [Fact]
    public void ACaretThatNamesNoCellKeepsTheOffset()
    {
        var (layout, _) = Layout();
        var nowhere = CaretPosition.At(new CellAddress(TestEditor.BareTag().Id));

        Assert.Equal((5, 1), Scroll(layout, new LayoutSize(20, 4), 5, 1, nowhere));
    }

    [Fact]
    public void ACaretThatNamesNoCellStillLeavesALegalOffset()
    {
        var (layout, _) = Layout();
        var nowhere = CaretPosition.At(new CellAddress(TestEditor.BareTag().Id));

        Assert.Equal((31, 2), Scroll(layout, new LayoutSize(12, 3), 100, 100, nowhere));
    }

    private static (double X, double Y) Scroll(
        CellLayout layout,
        LayoutSize viewport,
        double offsetX,
        double offsetY,
        CaretPosition caret) =>
        CaretScroll.Offset(layout, viewport, offsetX, offsetY, caret, MonospaceTextMeasure.Instance);

    private static (CellLayout Layout, Node Rule) Layout()
    {
        var (rule, body, _) = TestEditor.RuleWithStep();
        body.Model.NewChild(body, "steps", TestLanguage.Step);
        body.Model.NewChild(body, "steps", TestLanguage.Step);

        return (Layouter.Layout(Builder.Build(rule)), rule);
    }
}

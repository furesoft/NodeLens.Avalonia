using Projectional.Core;
using Projectional.Editing;
using Projectional.Language;
using Projectional.Projection;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// A click has to name a caret position, and a projectional editor never lets the caret rest between
/// cells, so every point has to resolve to one cell and one character boundary in it.
/// </summary>
/// <remarks>
/// The fixture is the rule-with-three-steps document of the viewport tests, in monospace. Its header
/// row reads <c>rule unnamed as &lt;none&gt; {</c>, so <c>unnamed</c> occupies columns 5 to 12 and the
/// third step sits at column 28 with its detail slot from 34 to 41.
/// </remarks>
public sealed class HitTestTests
{
    private static readonly EditorTreeBuilder Builder = new(TestNotations.Registry);
    private static readonly CellLayouter Layouter = new(MonospaceTextMeasure.Instance);

    [Fact]
    public void AClickOnTheLeftHalfOfACharacterStandsBeforeIt()
    {
        var (layout, rule, _) = Layout();

        var hit = Hit(layout, 8.2, 0.5);

        Assert.Equal(CellAddress.OfSlot(rule.Id, "name"), hit!.Value.Address);
        Assert.Equal(3, hit.Value.Offset);
    }

    [Fact]
    public void AClickOnTheRightHalfOfACharacterStandsAfterIt()
    {
        var (layout, _, _) = Layout();

        var hit = Hit(layout, 8.8, 0.5);

        Assert.Equal(4, hit!.Value.Offset);
    }

    [Fact]
    public void AClickPastTheEndOfTheTextStandsBeforeTheRightEdge()
    {
        var (layout, rule, _) = Layout();

        var hit = Hit(layout, 11.9, 0.5);

        Assert.Equal(CellAddress.OfSlot(rule.Id, "name"), hit!.Value.Address);
        Assert.Equal("unnamed".Length, hit.Value.Offset);
    }

    [Fact]
    public void AClickOnAKeywordStandsInsideTheKeyword()
    {
        var (layout, rule, _) = Layout();

        var hit = Hit(layout, 13.7, 0.5);

        Assert.Equal(rule.Id, hit!.Value.Address.Owner);
        Assert.Equal(1, hit.Value.Offset);
    }

    [Fact]
    public void AClickOnAnEmptySlotStandsAtItsOnlyOffset()
    {
        var (layout, _, last) = Layout();

        var hit = Hit(layout, 37.5, 1.5);

        Assert.Equal(CellAddress.OfSlot(last.Id, "detail"), hit!.Value.Address);
        Assert.Equal(0, hit.Value.Offset);
    }

    [Fact]
    public void AClickInTheGapAfterALineKeepsTheCaretOnThatLine()
    {
        var (layout, _, _) = Layout();

        var hit = Hit(layout, 39.5, 0.5);

        Assert.Equal(0, RowOf(layout, hit!.Value));
    }

    [Fact]
    public void AClickBelowTheDocumentLandsOnTheLastLine()
    {
        var (layout, _, _) = Layout();

        var hit = Hit(layout, 0.5, 20);

        Assert.Equal(4, RowOf(layout, hit!.Value));
    }

    [Fact]
    public void APointWithNoPlaceToStandNamesNoCell()
    {
        var (layout, _, _) = Layout();

        Assert.Null(HitTest.Caret([], layout, 0, 0, MonospaceTextMeasure.Instance));
    }

    private static CaretPosition? Hit(CellLayout layout, double x, double y) =>
        HitTest.Caret(layout.Tree, layout, x, y, MonospaceTextMeasure.Instance);

    private static double RowOf(CellLayout layout, CaretPosition position) =>
        CaretBounds.Of(layout.Tree, layout, position, MonospaceTextMeasure.Instance)!.Value.Y;

    private static (CellLayout Layout, Node Rule, Node Last) Layout()
    {
        var (rule, body, _) = TestEditor.RuleWithStep();
        body.Model.NewChild(body, "steps", TestLanguage.Step);
        var last = body.Model.NewChild(body, "steps", TestLanguage.Step);

        return (Layouter.Layout(Builder.Build(rule)), rule, last);
    }
}

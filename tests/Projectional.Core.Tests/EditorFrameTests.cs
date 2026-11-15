using Projectional.Core;
using Projectional.Editing;
using Projectional.Language;
using Projectional.Projection;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// A frame is the whole picture one painted pass needs: what text is on screen, what is highlighted and
/// where the caret is - in layout coordinates, with the scroll offset applied in one place.
/// </summary>
/// <remarks>
/// The fixture is the rule-with-three-steps document of the viewport tests, 41 by 5 and 47 cells in
/// monospace, of which 29 carry text. Rows are the header, the three steps side by side on the second
/// row, the empty <c>notes</c> collection on the third, then the <c>extends</c> line and the closing
/// brace.
/// </remarks>
public sealed class EditorFrameTests
{
    private static readonly EditorTreeBuilder Builder = new(TestNotations.Registry);
    private static readonly CellLayouter Layouter = new(MonospaceTextMeasure.Instance);

    [Fact]
    public void TheFrameDrawsEveryTextCellThatTheViewportCanSee()
    {
        var (layout, rule) = Layout();

        var frame = Frame(layout, rule, new LayoutSize(41, 5), 0, 0);

        Assert.Equal(29, frame.TextRuns.Count);
        Assert.All(frame.TextRuns, run => Assert.False(string.IsNullOrEmpty(run.Text)));
        Assert.DoesNotContain(frame.TextRuns, run => run.Cell.Children.Count > 0);
    }

    [Fact]
    public void ATextRunCarriesTheTextAndTheRectangleOfItsCell()
    {
        var (layout, rule) = Layout();

        var frame = Frame(layout, rule, new LayoutSize(41, 5), 0, 0);
        var run = frame.TextRuns.Single(candidate => candidate.Text == "extends");

        Assert.Equal(new LayoutRect(2, 3, 7, 1), run.Bounds);
        Assert.Equal(CellAddress.OfSlot(rule.Id, "extends").Owner, run.Address.Owner);
    }

    [Fact]
    public void ARowOutsideTheWindowContributesNoRuns()
    {
        var (layout, rule) = Layout();

        var frame = Frame(layout, rule, new LayoutSize(41, 2), 0, 0);

        Assert.Equal(24, frame.TextRuns.Count);
        Assert.All(frame.TextRuns, run => Assert.True(run.Bounds.Bottom <= 2));
    }

    [Fact]
    public void TheCaretIsAZeroWidthBarAtTheCharacterBoundary()
    {
        var (layout, rule) = Layout();

        var frame = Frame(
            layout,
            rule,
            new LayoutSize(41, 5),
            0,
            0,
            Selection.At(CaretPosition.At(Name(rule), 3)));

        Assert.Equal(new LayoutRect(8, 0, 0, 1), frame.CaretRect);
    }

    [Fact]
    public void TheCaretFollowsTheMovingEndOfASelectionNotItsAnchor()
    {
        var (layout, rule) = Layout();

        var frame = Frame(
            layout,
            rule,
            new LayoutSize(41, 5),
            0,
            0,
            new Selection(CaretPosition.At(Name(rule), 3), CaretPosition.At(Name(rule), 1)));

        Assert.Equal(6, frame.CaretRect!.Value.X);
        Assert.Equal([new LayoutRect(6, 0, 2, 1)], frame.Highlights);
    }

    [Fact]
    public void AnEmptySelectionHasNoHighlights()
    {
        var (layout, rule) = Layout();

        var frame = Frame(
            layout,
            rule,
            new LayoutSize(41, 5),
            0,
            0,
            Selection.At(CaretPosition.At(Name(rule), 2)));

        Assert.Empty(frame.Highlights);
    }

    [Fact]
    public void TheFrameStaysInLayoutCoordinatesSoTheViewportCanApplyTheOffset()
    {
        var (layout, rule) = Layout();
        var atExtends = CaretPosition.At(CellAddress.OfSlot(rule.Id, "extends"));

        var frame = Frame(layout, rule, new LayoutSize(41, 3), 0, 2, Selection.At(atExtends));
        var caret = frame.CaretRect!.Value;

        Assert.Equal(new LayoutRect(10, 3, 0, 1), caret);
        Assert.Equal(new LayoutRect(10, 1, 0, 1), frame.Viewport.ToViewport(caret));
    }

    [Fact]
    public void ACaretThatNamesNoCellLeavesTheFrameWithoutOne()
    {
        var (layout, rule) = Layout();
        var nowhere = CaretPosition.At(new CellAddress(TestEditor.BareTag().Id));

        var frame = Frame(layout, rule, new LayoutSize(41, 5), 0, 0, Selection.At(nowhere));

        Assert.Null(frame.CaretRect);
        Assert.Empty(frame.Highlights);
    }

    private static CellAddress Name(Node rule) => CellAddress.OfSlot(rule.Id, "name");

    private static EditorFrame Frame(
        CellLayout layout,
        Node rule,
        LayoutSize viewport,
        double offsetX,
        double offsetY,
        Selection? selection = null) =>
        EditorFrame.Build(
            layout,
            viewport,
            offsetX,
            offsetY,
            selection ?? Selection.At(CaretPosition.At(CellAddress.OfSlot(rule.Id, "name"))),
            MonospaceTextMeasure.Instance);

    private static (CellLayout Layout, Node Rule) Layout()
    {
        var (rule, body, _) = TestEditor.RuleWithStep();
        body.Model.NewChild(body, "steps", TestLanguage.Step);
        body.Model.NewChild(body, "steps", TestLanguage.Step);

        return (Layouter.Layout(Builder.Build(rule)), rule);
    }
}

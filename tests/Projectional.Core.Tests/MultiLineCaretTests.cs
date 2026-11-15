using Projectional.Core;
using Projectional.Editing;
using Projectional.Language;
using Projectional.Projection;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The caret and everything that is drawn about it live on a line of a cell, not on the cell as a whole.
/// </summary>
/// <remarks>
/// <para>
/// The fixture is a rule whose name is two lines long, so the name cell is two lines tall while every
/// other cell of the header stays one. In monospace a line is one unit high, and the name starts after
/// <c>rule </c>, which puts its first line at column five of row zero.
/// </para>
/// <para>
/// A value is broken into lines either because it holds a break of its own - <c>"ab\ncd"</c> - or
/// because it ran out of room at the page's edge, and both cases have to behave the same here: the
/// second line is a row lower, and the caret never stands on the break character itself.
/// </para>
/// </remarks>
public sealed class MultiLineCaretTests
{
    private static readonly MonospaceTextMeasure Measure = MonospaceTextMeasure.Instance;
    private static readonly EditorTreeBuilder Builder = new(TestNotations.Registry);

    [Fact]
    public void TheCaretOnTheSecondLineIsDrawnOneLineLower()
    {
        var (layout, rule, _) = Layout("ab\ncd");

        var first = Bar(layout, rule, offset: 1);
        var second = Bar(layout, rule, offset: 4);

        // The first line: one column into the name, on the header row.
        Assert.Equal(6, first.X);
        Assert.Equal(0, first.Y);

        // The second line starts at the cell's left edge again, one row down.
        Assert.Equal(6, second.X);
        Assert.Equal(1, second.Y);
    }

    [Fact]
    public void TheCaretCoversOneLineRatherThanTheWholeCell()
    {
        var (layout, rule, _) = Layout("ab\ncd");

        var bar = Bar(layout, rule, offset: 3);

        Assert.Equal(0, bar.Width);
        Assert.Equal(Measure.LineHeight, bar.Height);
    }

    [Fact]
    public void AClickOnTheSecondLineStandsInThatLine()
    {
        var (layout, rule, _) = Layout("ab\ncd");

        var hit = Hit(layout, 6.2, 1.5);

        Assert.Equal(CellAddress.OfSlot(rule.Id, "name"), hit!.Value.Address);
        Assert.Equal(4, hit.Value.Offset);
    }

    [Fact]
    public void AClickOnTheFirstLineStaysOnIt()
    {
        var (layout, rule, _) = Layout("ab\ncd");

        var hit = Hit(layout, 6.2, 0.5);

        Assert.Equal(CellAddress.OfSlot(rule.Id, "name"), hit!.Value.Address);
        Assert.Equal(1, hit.Value.Offset);
    }

    [Fact]
    public void ASoftBreakLeavesTheCaretJustAsWellOnTheContinuationLine()
    {
        // "rule " is five wide, so the name has three characters of room before the page's edge and
        // "ab cd" breaks at its space.
        var (layout, rule, _) = Layout("ab cd", wrapWidth: 8);

        var hit = Hit(layout, 5.2, 1.5);

        Assert.Equal(CellAddress.OfSlot(rule.Id, "name"), hit!.Value.Address);
        Assert.Equal(3, hit.Value.Offset);
    }

    [Fact]
    public void ASelectionAcrossABreakIsOneBoxPerLine()
    {
        var (layout, rule, _) = Layout("ab\ncd");
        var name = CellAddress.OfSlot(rule.Id, "name");

        var rects = SelectionRects.Of(
            layout.Tree,
            layout,
            new Selection(CaretPosition.At(name, 1), CaretPosition.At(name, 4)),
            Measure);

        Assert.Equal(2, rects.Count);

        // From one column into "ab" to the end of that line.
        Assert.Equal(new LayoutRect(6, 0, 1, Measure.LineHeight), rects[0]);

        // From the left edge of the second line up to its second column.
        Assert.Equal(new LayoutRect(5, 1, 1, Measure.LineHeight), rects[1]);
    }

    [Fact]
    public void DownFromTheFirstLineStaysInTheCell()
    {
        var (layout, rule, _) = Layout("ab\ncd");

        var moved = Move(layout, rule, offset: 0, CaretMove.Down);

        Assert.Equal(3, moved.Offset);
    }

    [Fact]
    public void UpFromTheSecondLineReturnsToTheFirst()
    {
        var (layout, rule, _) = Layout("ab\ncd");

        var moved = Move(layout, rule, offset: 4, CaretMove.Up);

        Assert.Equal(1, moved.Offset);
    }

    [Fact]
    public void HomeAndEndActOnTheLineTheCaretIsOn()
    {
        var (layout, rule, _) = Layout("ab\ncd");

        var home = Move(layout, rule, offset: 4, CaretMove.LineStart);
        var end = Move(layout, rule, offset: 1, CaretMove.LineEnd);

        // The start of the second line, not the start of the value.
        Assert.Equal(3, home.Offset);

        // The end of the first line is before the break character, so the caret never stands on it.
        Assert.Equal(2, end.Offset);
    }

    [Fact]
    public void TheFrameDrawsOneRunPerLine()
    {
        var (layout, rule, name) = Layout("ab\ncd");

        var frame = EditorFrame.Build(layout, new LayoutSize(41, 5), 0, 0, Selection.At(CellAddress.OfSlot(rule.Id, "name")), Measure);

        var runs = frame.TextRuns.Where(run => run.Cell == name).ToList();

        Assert.Equal(2, runs.Count);
        Assert.Equal("ab", runs[0].Text);
        Assert.Equal("cd", runs[1].Text);
        Assert.Equal(0, runs[0].Bounds.Y);
        Assert.Equal(1, runs[1].Bounds.Y);
        Assert.Equal(Measure.LineHeight, runs[1].Bounds.Height);
    }

    private static LayoutRect Bar(CellLayout layout, Node rule, int offset) =>
        CaretBounds.Of(layout.Tree, layout, CaretPosition.At(CellAddress.OfSlot(rule.Id, "name"), offset), Measure)!.Value;

    private static CaretPosition? Hit(CellLayout layout, double x, double y) =>
        HitTest.Caret(layout.Tree, layout, x, y, Measure);

    private static CaretPosition Move(CellLayout layout, Node rule, int offset, CaretMove move) =>
        CaretNavigation.Move(layout, Measure, CaretPosition.At(CellAddress.OfSlot(rule.Id, "name"), offset), move, 0);

    private static (CellLayout Layout, Node Rule, EditorCell Name) Layout(string name, double? wrapWidth = null)
    {
        var rule = TestLanguage.NewModel().NewNode(TestLanguage.Rule);
        rule.Set("name", name);
        var tree = Builder.Build(rule);

        return (new CellLayouter(Measure, wrapWidth).Layout(tree), rule, tree.Root.Children[0].Children[2]);
    }
}

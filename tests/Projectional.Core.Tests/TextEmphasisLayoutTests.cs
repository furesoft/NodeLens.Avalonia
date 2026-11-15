using Projectional.Editing;
using Projectional.Language;
using Projectional.Projection;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// Checks that an emphasis reaches the geometry, not only the paint call.
/// </summary>
/// <remarks>
/// A bold face advances differently than a regular one, so the cell has to be measured with the
/// emphasis it is drawn in - otherwise the caret would stand in the wrong place inside every bold word
/// and hit testing would select the wrong character. These tests measure with
/// <see cref="WideBoldTextMeasure"/>, which makes bold cost twice the width, so a layout that ignored
/// the emphasis could not accidentally produce the expected number.
/// </remarks>
public sealed class TextEmphasisLayoutTests
{
    [Fact]
    public void ABoldCellIsLaidOutAsWideAsTheBoldFaceMeasures()
    {
        Assert.Equal(8, WidthOf(EmphasisNotations.Registry, "rule"));
        Assert.Equal(4, WidthOf(TestNotations.Registry, "rule"));
    }

    [Fact]
    public void TheCaretInsideABoldCellSitsWhereTheBoldFacePutsIt()
    {
        var (tree, layout) = Project(EmphasisNotations.Registry);
        var cell = Cell(tree, "rule");

        Assert.Equal(4, CaretOffset(tree, layout, cell, 2));

        var (regularTree, regularLayout) = Project(TestNotations.Registry);
        var regular = Cell(regularTree, "rule");

        Assert.Equal(2, CaretOffset(regularTree, regularLayout, regular, 2));
    }

    [Fact]
    public void TheFrameHandsTheEmphasisOfTheCellOnToItsRun()
    {
        var (tree, layout) = Project(EmphasisNotations.Registry);
        var run = RunOf(tree, layout, "walk");

        Assert.Equal(TextEmphasis.Italic, run.Emphasis);
        Assert.Equal(4, run.Bounds.Width);
    }

    [Fact]
    public void ARunThatIsDrawnBoldIsAsWideAsItsBoldBounds()
    {
        var (tree, layout) = Project(
            EmphasisNotations.Registry,
            ConceptEmphasis.On(TestLanguage.Step, TextEmphasis.Bold));

        var run = RunOf(tree, layout, "walk");

        Assert.Equal(TextEmphasis.Bold | TextEmphasis.Italic, run.Emphasis);
        Assert.Equal(8, run.Bounds.Width);
    }

    [Fact]
    public void AStruckThroughRunKeepsTheWidthOfTheRegularFace()
    {
        var (tree, layout) = Project(EmphasisNotations.Registry, ConceptEmphasis.On(TestLanguage.Step, TextEmphasis.Strikethrough));

        var run = RunOf(tree, layout, "walk");

        Assert.Equal(TextEmphasis.Strikethrough | TextEmphasis.Italic, run.Emphasis);
        Assert.Equal(4, run.Bounds.Width);
    }

    private static (EditorTree Tree, CellLayout Layout) Project(
        NotationRegistry notations,
        ICellEmphasis? emphasis = null)
    {
        var (rule, _, step) = TestEditor.RuleWithStep();
        step.Set("text", "walk");

        var tree = new EditorTreeBuilder(notations, emphasis).Build(rule);

        return (tree, new CellLayouter(WideBoldTextMeasure.Instance).Layout(tree));
    }

    private static TextCell Cell(EditorTree tree, string text) =>
        tree.Cells().OfType<TextCell>().Single(cell => cell.Text == text);

    private static double WidthOf(NotationRegistry notations, string text)
    {
        var (tree, layout) = Project(notations);

        return BoundsOf(tree, layout, Cell(tree, text)).Width;
    }

    private static double CaretOffset(EditorTree tree, CellLayout layout, TextCell cell, int offset) =>
        (CaretBounds.Of(tree, layout, CaretPosition.At(cell.Address, offset), WideBoldTextMeasure.Instance)
            ?? throw new InvalidOperationException($"No caret at {offset} in '{cell.Text}'.")).X - BoundsOf(tree, layout, cell).X;

    private static TextRun RunOf(EditorTree tree, CellLayout layout, string text)
    {
        var frame = EditorFrame.Build(
            layout,
            layout.Size,
            offsetX: 0,
            offsetY: 0,
            Selection.At(CaretPosition.At(Cell(tree, text).Address)),
            WideBoldTextMeasure.Instance);

        return frame.TextRuns.Single(run => run.Text == text);
    }

    private static LayoutRect BoundsOf(EditorTree tree, CellLayout layout, TextCell cell) =>
        layout.BoundsOf(cell) ?? throw new InvalidOperationException($"'{cell.Text}' is not laid out.");
}

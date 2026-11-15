using Projectional.Core;
using Projectional.Language;
using Projectional.Projection;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// A scroll range is the four numbers a scroll host needs: the extent, the window, the clamped offset,
/// and the step and page the host turns a gesture into.
/// </summary>
/// <remarks>
/// The document is the same five-row, 43-column rule the viewport tests use, so every expected number
/// can be counted off the notation: one step is one indent column by one line (2 x 1 in monospace), and
/// a page is the window less one step so two pages overlap by a line.
/// </remarks>
public sealed class ScrollRangeTests
{
    private static readonly EditorTreeBuilder Builder = new(TestNotations.Registry);
    private static readonly CellLayouter Layouter = new(MonospaceTextMeasure.Instance);

    [Fact]
    public void TheExtentIsTheLaidOutDocumentAndNotTheWindow()
    {
        var range = ScrollRange.Of(Layout(), new LayoutSize(20, 2), 0, 0, MonospaceTextMeasure.Instance);

        Assert.Equal(new LayoutSize(43, 5), range.Extent);
        Assert.Equal(new LayoutSize(20, 2), range.Viewport);
    }

    [Fact]
    public void AStepIsOneIndentColumnAndOneLine()
    {
        var range = ScrollRange.Of(Layout(), new LayoutSize(20, 2), 0, 0, MonospaceTextMeasure.Instance);

        Assert.Equal(new LayoutSize(2, 1), range.Step);
    }

    [Fact]
    public void APageKeepsOneLineOfOverlapWithTheOneBefore()
    {
        var range = ScrollRange.Of(Layout(), new LayoutSize(20, 5), 0, 0, MonospaceTextMeasure.Instance);

        Assert.Equal(new LayoutSize(18, 4), range.Page);
    }

    [Fact]
    public void APageIsNeverSmallerThanOneStep()
    {
        var range = ScrollRange.Of(Layout(), new LayoutSize(20, 1), 0, 0, MonospaceTextMeasure.Instance);

        // A window exactly one line tall cannot overlap and still move, so the step wins.
        Assert.Equal(new LayoutSize(18, 1), range.Page);
    }

    [Fact]
    public void TheOffsetIsClampedToTheScrollableExtent()
    {
        var range = ScrollRange.Of(Layout(), new LayoutSize(20, 2), offsetX: 100, offsetY: 100, MonospaceTextMeasure.Instance);

        Assert.Equal(new LayoutSize(23, 3), range.Offset);
    }

    [Fact]
    public void ADocumentSmallerThanTheWindowCannotScroll()
    {
        var range = ScrollRange.Of(Layout(), new LayoutSize(80, 20), 5, 5, MonospaceTextMeasure.Instance);

        Assert.Equal(new LayoutSize(0, 0), range.Offset);
        Assert.Equal(new LayoutSize(43, 5), range.Extent);
    }

    [Fact]
    public void ADocumentExactlyTheSizeOfTheWindowCannotScroll()
    {
        var range = ScrollRange.Of(Layout(), new LayoutSize(43, 5), 1, 1, MonospaceTextMeasure.Instance);

        Assert.Equal(new LayoutSize(0, 0), range.Offset);
    }

    [Fact]
    public void TheRangeRefusesAMissingLayoutOrMeasure()
    {
        Assert.Throws<ArgumentNullException>(() => ScrollRange.Of(null!, new LayoutSize(20, 2), 0, 0, MonospaceTextMeasure.Instance));
        Assert.Throws<ArgumentNullException>(() => ScrollRange.Of(Layout(), new LayoutSize(20, 2), 0, 0, null!));
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

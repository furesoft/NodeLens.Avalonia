using Projectional.Core;
using Projectional.Editing;
using Projectional.Projection;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The bulb is geometry, so it is tested without a window: one line square, left of the page, level with
/// the caret's own line.
/// </summary>
public sealed class QuickFixBulbTests
{
    [Fact]
    public void TheBulbIsOneLineSquareLeftOfThePage()
    {
        var rule = TestEditor.BareRule();
        using var session = TestEditor.Open(rule);

        var caret = CaretBounds.Of(session.Tree, session.Layout, session.Selection.Caret, session.Measure);

        var bulb = QuickFixBulb.BoundsOf(session.Tree, session.Layout, session.Selection.Caret, session.Measure);

        Assert.NotNull(bulb);
        Assert.Equal(-(session.Measure.LineHeight + QuickFixBulb.Gap), bulb.Value.X);
        Assert.Equal(session.Measure.LineHeight, bulb.Value.Width);
        Assert.Equal(session.Measure.LineHeight, bulb.Value.Height);

        // Level with the caret: the bulb names a line, and a box that is a pixel off reads as a different
        // line's bulb.
        Assert.Equal(caret!.Value.Y, bulb.Value.Y);
    }

    [Fact]
    public void TheBulbFollowsTheCaretToAnotherLine()
    {
        var (rule, body) = TestEditor.RuleWithBody();
        using var session = TestEditor.Open(rule);

        var first = QuickFixBulb.BoundsOf(session.Tree, session.Layout, session.Selection.Caret, session.Measure);

        session.Select(Selection.At(CaretPosition.At(CellAddress.OfSlot(body.Id, "steps"), 0)));

        var second = QuickFixBulb.BoundsOf(session.Tree, session.Layout, session.Selection.Caret, session.Measure);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.True(second.Value.Y > first.Value.Y, "the body is on a lower line than the rule's name");
    }

    [Fact]
    public void ACaretThatIsNotDrawnHasNoBulb()
    {
        Assert.Null(QuickFixBulb.BoundsOf(null, MonospaceTextMeasure.Instance));
    }

    [Fact]
    public void APositionNamingNoCellHasNoBulb()
    {
        var rule = TestEditor.BareRule();
        using var session = TestEditor.Open(rule);

        var nowhere = CaretPosition.At(CellAddress.OfSlot(NodeId.New(), "name"), 0);

        Assert.Null(QuickFixBulb.BoundsOf(session.Tree, session.Layout, nowhere, session.Measure));
    }

    [Fact]
    public void TheMeasureIsRequired()
    {
        Assert.Throws<ArgumentNullException>(() => QuickFixBulb.BoundsOf((LayoutRect?)null, null!));
    }
}

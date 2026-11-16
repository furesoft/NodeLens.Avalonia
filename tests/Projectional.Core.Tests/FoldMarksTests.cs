using Projectional.Core;
using Projectional.Editing;
using Projectional.Projection;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>Where the fold arrows go, and which one a press in the margin hits.</summary>
public sealed class FoldMarksTests
{
    [Fact]
    public void OneMarkPerFoldableNodeInVisualOrder()
    {
        var (rule, body) = TestEditor.RuleWithBody();
        using var session = TestEditor.Open(rule);

        var marks = FoldMarks.Of(session, session.Layout, session.Measure);

        // The fixture nominates `enabled` as its collapse slot, so the rule folds; the body does not, because
        // Test.Body nominates nothing.
        Assert.Single(marks);
        Assert.Equal(rule.Id, marks[0].Node);
        Assert.False(marks[0].IsCollapsed);
    }

    [Fact]
    public void ANestedFoldableNodeGetsItsOwnMark()
    {
        // The regression the runtime probe caught: only the document root carries the node's own address,
        // so a test that folds the root proves nothing about every other node.
        var (rule, body) = TestEditor.RuleWithBody();
        var inner = rule.Model.NewChild(rule, "extends", TestLanguage.Rule);
        using var session = TestEditor.Open(rule);

        var marks = FoldMarks.Of(session, session.Layout, session.Measure);

        Assert.Contains(marks, mark => mark.Node == rule.Id);
        Assert.Contains(marks, mark => mark.Node == inner.Id);
        Assert.DoesNotContain(marks, mark => mark.Node == body.Id);
    }

    [Fact]
    public void AMarkSaysWhichWayTheNodeIs()
    {
        var (rule, _) = TestEditor.RuleWithBody();
        using var session = TestEditor.Open(rule);

        session.ToggleCollapsed(rule.Id);

        var marks = FoldMarks.Of(session, session.Layout, session.Measure);

        Assert.Single(marks);
        Assert.True(marks[0].IsCollapsed);
    }

    [Fact]
    public void AFoldedNodeThatShrankToOneLineKeepsItsArrow()
    {
        // Otherwise it would fold invisibly and lose the only way back except the menu.
        var rule = TestEditor.BareRule();
        using var session = TestEditor.Open(rule);

        session.ToggleCollapsed(rule.Id);

        Assert.Single(FoldMarks.Of(session, session.Layout, session.Measure));
    }

    [Fact]
    public void ALanguageThatDoesNotOptInHasNoMarks()
    {
        var step = TestEditor.BareStep();
        using var session = TestEditor.Open(step);

        Assert.Empty(FoldMarks.Of(session, session.Layout, session.Measure));
    }

    [Fact]
    public void TheMarkSitsOnTheNodesFirstRow()
    {
        var (rule, _) = TestEditor.RuleWithBody();
        using var session = TestEditor.Open(rule);

        var mark = FoldMarks.Of(session, session.Layout, session.Measure)[0];
        var bounds = session.Layout.BoundsOf(new CellAddress(rule.Id));

        Assert.NotNull(bounds);
        Assert.Equal(bounds.Value.Y, mark.Bounds.Y);
        Assert.Equal(session.Measure.LineHeight, mark.Bounds.Height);
    }

    [Fact]
    public void OnlyTheArrowColumnHits()
    {
        var (rule, _) = TestEditor.RuleWithBody();
        using var session = TestEditor.Open(rule);

        var marks = FoldMarks.Of(session, session.Layout, session.Measure);
        var mark = marks[0];
        var middle = mark.Bounds.Y + (mark.Bounds.Height / 2);

        Assert.NotNull(FoldHitTest.At(marks, 0, 1, 0.5, middle));

        // A point in the same row but outside the column is a press on a number, not on an arrow.
        Assert.Null(FoldHitTest.At(marks, 0, 1, 3, middle));

        // And a point in the column but on another row is nothing.
        Assert.Null(FoldHitTest.At(marks, 0, 1, 0.5, middle + 100));
    }

    [Fact]
    public void TheArgumentsAreRequired()
    {
        var (rule, _) = TestEditor.RuleWithBody();
        using var session = TestEditor.Open(rule);

        Assert.Throws<ArgumentNullException>(
            () => FoldMarks.Of(null!, session.Layout, session.Measure));
        Assert.Throws<ArgumentNullException>(() => FoldHitTest.At(null!, 0, 1, 0, 0));
    }
}

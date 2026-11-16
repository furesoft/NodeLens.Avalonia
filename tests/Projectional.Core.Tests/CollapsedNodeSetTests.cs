using Projectional.Core;
using Projectional.Editing;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>The fold set is a value: every change returns a new one and nesting needs no bookkeeping.</summary>
public sealed class CollapsedNodeSetTests
{
    [Fact]
    public void EmptyHoldsNothing()
    {
        Assert.True(CollapsedNodeSet.Empty.IsEmpty);
        Assert.Empty(CollapsedNodeSet.Empty);
        Assert.False(CollapsedNodeSet.Empty.Contains(NodeId.New()));
    }

    [Fact]
    public void WithAndWithoutReturnNewSets()
    {
        var node = NodeId.New();
        var folded = CollapsedNodeSet.Empty.With(node);

        Assert.NotSame(CollapsedNodeSet.Empty, folded);
        Assert.True(folded.Contains(node));
        Assert.False(CollapsedNodeSet.Empty.Contains(node), "the empty set must be unchanged");

        var opened = folded.Without(node);

        Assert.False(opened.Contains(node));
        Assert.True(folded.Contains(node), "the folded set must be unchanged");
    }

    [Fact]
    public void ToggleFlipsMembership()
    {
        var node = NodeId.New();

        var once = CollapsedNodeSet.Empty.Toggle(node);
        var twice = once.Toggle(node);

        Assert.True(once.Contains(node));
        Assert.False(twice.Contains(node));
    }

    [Fact]
    public void AddingTwiceIsTheSameSet()
    {
        var node = NodeId.New();
        var folded = CollapsedNodeSet.Empty.With(node);

        // Same instance, because nothing changed - which keeps a redundant toggle from re-projecting.
        Assert.Same(folded, folded.With(node));
    }

    [Fact]
    public void ItComparesAsASet()
    {
        var first = NodeId.New();
        var second = NodeId.New();

        var left = CollapsedNodeSet.Empty.With(first).With(second);
        var right = CollapsedNodeSet.Empty.With(second).With(first);

        Assert.True(left.SetEquals(right));
        Assert.True(left.Overlaps([first]));
        Assert.True(left.IsSupersetOf([first]));
    }
}

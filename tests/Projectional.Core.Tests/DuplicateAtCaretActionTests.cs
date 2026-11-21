using Projectional.Core;
using Projectional.Editing;
using Projectional.Projection;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>Ctrl+D duplicates the node the caret is in - resolved from the caret, not named by a menu.</summary>
public sealed class DuplicateAtCaretActionTests
{
    [Fact]
    public void TheCaretNodeIsCopiedRightAfterItself()
    {
        var (body, steps) = TestEditor.BodyWithSteps("walk", "run");

        using var session = TestEditor.Open(body);
        session.Select(Selection.At(CellAddress.OfSlot(steps[0].Id, "text")));

        var result = session.Apply(new DuplicateAtCaretAction());

        Assert.True(result.Changed);

        var after = body.GetChildren("steps");

        Assert.Equal(3, after.Count);
        Assert.Equal("walk", after[0].GetOrNull("text"));
        Assert.Equal("walk", after[1].GetOrNull("text"));
        Assert.Equal("run", after[2].GetOrNull("text"));

        // A copy, not the same node twice: the two can be edited apart, which is why one duplicates at all.
        Assert.NotEqual(after[0].Id, after[1].Id);
    }

    [Fact]
    public void ARootHasNoPlaceAndIsRejected()
    {
        // There is always a caret, so "the caret names nothing" is not a state: the refusal that exists is a node
        // with nowhere to go - a root, which has no slot to be copied into.
        var rule = TestEditor.BareRule();

        using var session = TestEditor.Open(rule);
        TestEditor.SelectWhole(session, rule);

        var result = session.Apply(new DuplicateAtCaretAction());

        Assert.Equal(EditOutcome.Rejected, result.Outcome);
        Assert.NotNull(result.Reason);
        Assert.Single(rule.Model.Roots);
    }
}

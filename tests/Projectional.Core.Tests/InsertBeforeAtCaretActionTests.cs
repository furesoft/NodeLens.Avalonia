using Projectional.Core;
using Projectional.Editing;
using Projectional.Projection;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>The Insert key adds a sibling before the node the caret is in - the side Enter cannot reach.</summary>
public sealed class InsertBeforeAtCaretActionTests
{
    [Fact]
    public void TheNewSiblingStandsBeforeTheCaretNode()
    {
        var (body, steps) = TestEditor.BodyWithSteps("walk", "run");

        using var session = TestEditor.Open(body);
        session.Select(Selection.At(CellAddress.OfSlot(steps[1].Id, "text")));

        var result = session.Apply(new InsertBeforeAtCaretAction());

        Assert.True(result.Changed);

        var after = body.GetChildren("steps");

        Assert.Equal(3, after.Count);
        Assert.Equal("walk", after[0].GetOrNull("text"));
        Assert.Null(after[1].GetOrNull("text"));
        Assert.Equal("run", after[2].GetOrNull("text"));

        // The new node is the sibling's concept, not a default: "insert before a step" has to make a step.
        Assert.Equal(steps[1].Concept.Key, after[1].Concept.Key);

        // And the caret is on the new node, so typing fills it rather than editing the neighbour.
        Assert.Equal(after[1].Id, session.Selection.Caret.Address.Owner);
    }

    [Fact]
    public void ARootHasNoParentSlotAndIsRejected()
    {
        // There is always a caret, so the meaningful refusal is a node with no parent list - a root has no sibling
        // place to insert into.
        var rule = TestEditor.BareRule();

        using var session = TestEditor.Open(rule);
        TestEditor.SelectWhole(session, rule);

        var result = session.Apply(new InsertBeforeAtCaretAction());

        Assert.Equal(EditOutcome.Rejected, result.Outcome);
        Assert.Single(rule.Model.Roots);
    }
}

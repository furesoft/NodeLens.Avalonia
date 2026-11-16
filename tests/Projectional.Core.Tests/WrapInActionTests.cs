using Projectional.Core;
using Projectional.Editing;
using Projectional.Projection;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// Surrounding a node with a new parent, which is the repair for an expression whose grouping the drawing
/// does not show.
/// </summary>
/// <remarks>
/// The fixture has no purpose-built wrapper concept, so the tests wrap a step inside a step's
/// <c>detail</c> slot: structurally that is exactly the case that matters - a concept that can stand in the
/// node's place and has a single-child slot that accepts it - without growing the shared language a concept
/// only these tests would use.
/// </remarks>
public sealed class WrapInActionTests
{
    [Fact]
    public void TheWrapperTakesTheNodesPlaceAndTheNodeKeepsItsIdentity()
    {
        var (rule, body) = TestEditor.RuleWithBody();
        var step = body.Model.NewChild(body, "steps", TestLanguage.Step);
        step.Set("text", "first");

        using var session = TestEditor.Open(rule);

        var result = session.Apply(new WrapInAction(step.Id, TestLanguage.Step, "detail"));

        Assert.Equal(EditOutcome.Applied, result.Outcome);

        var wrapper = Assert.Single(body.GetChildren("steps"));

        Assert.NotEqual(step.Id, wrapper.Id);
        Assert.Same(step, wrapper.GetChild("detail"));
    }

    [Fact]
    public void TheWrapperStandsWhereTheNodeStood()
    {
        var (rule, body) = TestEditor.RuleWithBody();
        var first = body.Model.NewChild(body, "steps", TestLanguage.Step);
        var second = body.Model.NewChild(body, "steps", TestLanguage.Step);

        using var session = TestEditor.Open(rule);

        session.Apply(new WrapInAction(second.Id, TestLanguage.Step, "detail"));

        var children = body.GetChildren("steps");

        Assert.Equal(2, children.Count);
        Assert.Same(first, children[0]);
        Assert.Same(second, children[1].GetChild("detail"));
        Assert.Equal(1, children[1].IndexInParent);
    }

    [Fact]
    public void UndoAndRedoRestoreTheWrapInOneStep()
    {
        var (rule, body) = TestEditor.RuleWithBody();
        var step = body.Model.NewChild(body, "steps", TestLanguage.Step);

        // The session is opened after the fixture exists, so the wrap is the only step its history holds.
        using var session = TestEditor.Open(rule);

        session.Apply(new WrapInAction(step.Id, TestLanguage.Step, "detail"));

        Assert.True(session.Undo());

        var restored = Assert.Single(body.GetChildren("steps"));

        Assert.Same(step, restored);
        Assert.Same(body, step.Parent);
        Assert.False(session.History.CanUndo);

        // The wrap journals three steps in one change set - the node leaves, the wrapper arrives, the node
        // returns inside it - so redo has to replay all three closures in the forward direction.
        Assert.True(session.Redo());

        var rewrapped = Assert.Single(body.GetChildren("steps"));

        Assert.Same(step, rewrapped.GetChild("detail"));
        Assert.Same(rewrapped, step.Parent);
    }

    [Fact]
    public void TheCaretStaysWhereItWas()
    {
        var (rule, body) = TestEditor.RuleWithBody();
        var step = body.Model.NewChild(body, "steps", TestLanguage.Step);

        using var session = TestEditor.Open(rule);

        session.Select(Selection.At(CellAddress.OfSlot(step.Id, "text")));
        var before = session.Selection.Caret.Address;

        session.Apply(new WrapInAction(step.Id, TestLanguage.Step, "detail"));

        Assert.Equal(before, session.Selection.Caret.Address);
    }

    [Fact]
    public void ANodeWithNoPlaceIsRefused()
    {
        // A root cannot be wrapped: the wrapper would have no slot to stand in.
        var step = TestEditor.BareStep("first");
        using var session = TestEditor.Open(step);

        var result = session.Apply(new WrapInAction(step.Id, TestLanguage.Step, "detail"));

        Assert.Equal(EditOutcome.Rejected, result.Outcome);
        Assert.Null(step.Parent);
    }

    [Fact]
    public void AConceptThatCannotStandThereIsRefused()
    {
        var (rule, body) = TestEditor.RuleWithBody();
        var step = body.Model.NewChild(body, "steps", TestLanguage.Step);

        using var session = TestEditor.Open(rule);

        // A body cannot be a step, so it cannot take one's place in the steps list.
        var result = session.Apply(new WrapInAction(step.Id, TestLanguage.Body, "steps"));

        Assert.Equal(EditOutcome.Rejected, result.Outcome);
        Assert.Same(step, Assert.Single(body.GetChildren("steps")));
    }

    [Fact]
    public void AWrapperWithNoSlotForTheNodeIsRefused()
    {
        var (rule, body) = TestEditor.RuleWithBody();
        var note = body.Model.NewChild(body, "notes", TestLanguage.Note);

        using var session = TestEditor.Open(rule);

        // A note can stand where a note stands, but its only slot is a property - there is nowhere to put
        // the node it was asked to surround.
        var result = session.Apply(new WrapInAction(note.Id, TestLanguage.Note, "text"));

        Assert.Equal(EditOutcome.Rejected, result.Outcome);
        Assert.Same(note, Assert.Single(body.GetChildren("notes")));
    }

    [Fact]
    public void TheActionNamesTheNodeAndTheWrapper()
    {
        Assert.Throws<ArgumentException>(() => new WrapInAction(NodeId.Empty, TestLanguage.Step, "detail"));
        Assert.Throws<ArgumentException>(() => new WrapInAction(NodeId.New(), string.Empty, "detail"));
        Assert.Throws<ArgumentException>(() => new WrapInAction(NodeId.New(), TestLanguage.Step, string.Empty));
    }
}

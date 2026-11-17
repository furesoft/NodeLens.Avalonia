using Projectional.Core;
using Projectional.Editing;
using Projectional.Projection;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// Surrounding a node, or a run of siblings, with a new parent, which is the repair for an expression
/// whose grouping the drawing does not show and for a selection of lines that wants a shared wrapper.
/// </summary>
/// <remarks>
/// <para>
/// The fixture has no purpose-built wrapper concept, so the single-node tests wrap a step inside a step's
/// <c>detail</c> slot: structurally that is exactly the case that matters - a concept that can stand in the
/// node's place and has a single-child slot that accepts it - without growing the shared language a concept
/// only these tests would use.
/// </para>
/// <para>
/// A run needs the other shape, a many-valued slot on a concept that can stand where the run stands, and
/// <see cref="GroupLanguage"/> is that shape: an item can be wrapped in a group, and a group holds any
/// number of items. The run tests are written there because the single-node fixture cannot express one.
/// </para>
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
    public void AWrapperCanBeBornWithTheValueThatMakesItWhatItMeans()
    {
        // "Negate" is a unary whose operator is "!", and the caller cannot write that afterwards because
        // the wrapper's id does not exist until the wrap runs.
        var (rule, body) = TestEditor.RuleWithBody();
        var step = body.Model.NewChild(body, "steps", TestLanguage.Step);

        using var session = TestEditor.Open(rule);

        var result = session.Apply(new WrapInAction(
            step.Id,
            TestLanguage.Step,
            "detail",
            new Dictionary<string, string> { ["text"] = "negated" }));

        Assert.Equal(EditOutcome.Applied, result.Outcome);

        var wrapper = Assert.Single(body.GetChildren("steps"));

        Assert.Equal("negated", wrapper.GetOrNull("text"));
        Assert.Same(step, wrapper.GetChild("detail"));
        Assert.Null(step.GetOrNull("text"));
    }

    [Fact]
    public void APresetTheWrapperDoesNotDeclareIsRefusedBeforeAnythingMoves()
    {
        var (rule, body) = TestEditor.RuleWithBody();
        var step = body.Model.NewChild(body, "steps", TestLanguage.Step);

        using var session = TestEditor.Open(rule);

        var result = session.Apply(new WrapInAction(
            step.Id,
            TestLanguage.Step,
            "detail",
            new Dictionary<string, string> { ["nonsense"] = "x" }));

        Assert.Equal(EditOutcome.Rejected, result.Outcome);

        // The node never left its place, which is what validating first buys.
        Assert.Same(step, Assert.Single(body.GetChildren("steps")));
        Assert.Same(body, step.Parent);
        Assert.False(session.History.CanUndo);
    }

    [Fact]
    public void APresetOfTheWrongTypeIsRefused()
    {
        var (rule, body) = TestEditor.RuleWithBody();
        var step = body.Model.NewChild(body, "steps", TestLanguage.Step);

        using var session = TestEditor.Open(rule);

        var result = session.Apply(new WrapInAction(
            step.Id,
            TestLanguage.Step,
            "detail",
            new Dictionary<string, string> { ["index"] = "not a number" }));

        Assert.Equal(EditOutcome.Rejected, result.Outcome);
        Assert.Same(step, Assert.Single(body.GetChildren("steps")));
    }

    [Fact]
    public void TheActionNamesTheNodeAndTheWrapper()
    {
        Assert.Throws<ArgumentException>(() => new WrapInAction(NodeId.Empty, TestLanguage.Step, "detail"));
        Assert.Throws<ArgumentException>(() => new WrapInAction(NodeId.New(), string.Empty, "detail"));
        Assert.Throws<ArgumentException>(() => new WrapInAction(NodeId.New(), TestLanguage.Step, string.Empty));
        Assert.Throws<ArgumentException>(() => new WrapInAction([], TestLanguage.Step, "detail"));
        Assert.Throws<ArgumentException>(() => new WrapInAction([NodeId.Empty], TestLanguage.Step, "detail"));
    }

    [Fact]
    public void TheWrapperTakesTheRunsPlaceAndKeepsTheirOrder()
    {
        var (group, items) = GroupEditor.GroupWithItems("first", "second");

        using var session = GroupEditor.Open(group);

        var result = session.Apply(new WrapInAction([.. items.Select(item => item.Id)], GroupLanguage.Group, "items"));

        Assert.Equal(EditOutcome.Applied, result.Outcome);

        var wrapper = Assert.Single(group.GetChildren("items"));

        Assert.Equal(GroupLanguage.Group, wrapper.Concept.Key);
        Assert.Equal(["first", "second"], wrapper.GetChildren("items").Select(item => item.GetOrNull("name")));
        Assert.All(items, item => Assert.Same(wrapper, item.Parent));
    }

    [Fact]
    public void AWrapOfSeveralNodesIsOneUndoStep()
    {
        var (group, items) = GroupEditor.GroupWithItems("first", "second", "third");

        // Opened after the fixture exists, so the wrap is the only step the history holds.
        using var session = GroupEditor.Open(group);

        session.Apply(new WrapInAction([items[0].Id, items[1].Id], GroupLanguage.Group, "items"));

        Assert.True(session.Undo());

        Assert.Equal(items, group.GetChildren("items"));
        Assert.All(items, item => Assert.Same(group, item.Parent));
        Assert.False(session.History.CanUndo);

        Assert.True(session.Redo());

        var children = group.GetChildren("items");
        var wrapper = children[0];

        Assert.Equal(2, children.Count);
        Assert.Equal(GroupLanguage.Group, wrapper.Concept.Key);
        Assert.Equal([items[0], items[1]], wrapper.GetChildren("items"));
        Assert.Same(items[2], children[1]);
    }

    [Fact]
    public void SiblingsThatAreNotNextToEachOtherAreRefused()
    {
        var (group, items) = GroupEditor.GroupWithItems("first", "second", "third");

        using var session = GroupEditor.Open(group);

        // The first and the last of three: a wrapper would leave the second one outside it, which is not
        // what a selection that covered two of them asked for.
        var result = session.Apply(new WrapInAction([items[0].Id, items[2].Id], GroupLanguage.Group, "items"));

        Assert.Equal(EditOutcome.Rejected, result.Outcome);
        Assert.Equal(items, group.GetChildren("items"));
        Assert.False(session.History.CanUndo);
    }

    [Fact]
    public void NodesOfDifferentParentsAreRefused()
    {
        var (root, nested, items) = GroupEditor.NestedGroupWithItems();
        var sibling = root.Model.NewChild(root, "items", GroupLanguage.Item);

        using var session = GroupEditor.Open(root);

        var result = session.Apply(new WrapInAction([items[0].Id, sibling.Id], GroupLanguage.Group, "items"));

        Assert.Equal(EditOutcome.Rejected, result.Outcome);
        Assert.Equal(items, nested.GetChildren("items"));
        Assert.Same(root, sibling.Parent);
    }

    [Fact]
    public void ASlotThatHoldsOneChildCannotTakeARun()
    {
        var (body, steps) = TestEditor.BodyWithSteps("first", "second");

        using var session = TestEditor.Open(body);

        // A step's `detail` holds one child, so it cannot take both steps even though each of them conforms.
        var result = session.Apply(new WrapInAction(
            [steps[0].Id, steps[1].Id],
            TestLanguage.Step,
            "detail"));

        Assert.Equal(EditOutcome.Rejected, result.Outcome);
        Assert.Equal(steps, body.GetChildren("steps"));
    }
}

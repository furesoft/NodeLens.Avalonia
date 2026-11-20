using Projectional.Core;
using Projectional.Editing;
using Projectional.Projection;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>Moving a node to a stated place, which is the primitive a refactoring needs.</summary>
/// <remarks>
/// <see cref="MoveNodeAction"/> only trades places inside one slot. An extract names a place instead - a parent, a slot
/// and an index it computed - so the two questions worth pinning are whether the model keeps the node's identity on the
/// way (so references and caret addresses survive) and whether undo puts it back where it was, in one step.
/// </remarks>
public sealed class MoveNodeToActionTests
{
    [Fact]
    public void ANodeMovesIntoAnotherParentAndKeepsItsIdentity()
    {
        var (rule, _, first, second, step) = Model();

        using var session = TestEditor.Open(rule);

        var id = step.Id;
        var result = session.Apply(new MoveNodeToAction(id, second.Id, "detail", 0));

        Assert.Equal(EditOutcome.Applied, result.Outcome);
        Assert.Empty(first.GetChildren("detail"));
        Assert.Same(step, Assert.Single(second.GetChildren("detail")));
        Assert.Equal(id, step.Id);
        Assert.Same(second, step.Parent);
        Assert.Equal("detail", step.ParentSlot);
        Assert.Equal(0, step.IndexInParent);
    }

    [Fact]
    public void ACaretInsideTheMovedSubtreeStillLands()
    {
        // The point of a move rather than a copy: the node keeps its id, so a cell address that named it before the
        // move still names it - which is what keeps the caret (and every reference) where the user left it.
        var (rule, _, _, second, step) = Model();

        using var session = TestEditor.Open(rule);

        var place = CellAddress.OfSlot(step.Id, "text");

        Assert.NotNull(session.Tree.Find(place));

        session.Apply(new MoveNodeToAction(step.Id, second.Id, "detail", 0));

        Assert.NotNull(session.Tree.Find(place));
        Assert.Same(step, session.Tree.Find(place)!.Address.Owner == step.Id ? step : null);
    }

    [Fact]
    public void UndoPutsTheNodeBackInTheSlotItCameFrom()
    {
        var (rule, _, first, second, step) = Model();

        using var session = TestEditor.Open(rule);

        session.Apply(new MoveNodeToAction(step.Id, second.Id, "detail", 0));
        session.History.Undo();

        Assert.Same(step, Assert.Single(first.GetChildren("detail")));
        Assert.Empty(second.GetChildren("detail"));
        Assert.Same(first, step.Parent);
        Assert.Equal("detail", step.ParentSlot);
    }

    [Fact]
    public void AMoveWithinOneSlotReorders()
    {
        var (rule, body, first, second, _) = Model();

        using var session = TestEditor.Open(rule);

        var result = session.Apply(new MoveNodeToAction(second.Id, body.Id, "steps", 0));

        Assert.Equal(EditOutcome.Applied, result.Outcome);
        Assert.Equal([second.Id, first.Id], body.GetChildren("steps").Select(child => child.Id));
    }

    [Fact]
    public void ANodeAlreadyInThatPlaceIsNoEdit()
    {
        // The world the place was computed from may have moved already: an edit that changes nothing must not become
        // an undo step the user cannot explain.
        var (rule, _, first, _, step) = Model();

        using var session = TestEditor.Open(rule);

        var result = session.Apply(new MoveNodeToAction(step.Id, first.Id, "detail", 0));

        Assert.Equal(EditOutcome.Unchanged, result.Outcome);
    }

    [Fact]
    public void ANodeThatIsGoneIsAReasonNotAThrow()
    {
        // A program computes its places from a declaration, and the world can move on before it runs: throwing would
        // abandon the rest of the program with the model half-changed.
        var (rule, _, _, second, step) = Model();

        using var session = TestEditor.Open(rule);
        var id = step.Id;

        session.Apply(new DeleteNodeAction(id));

        var result = session.Apply(new MoveNodeToAction(id, second.Id, "detail", 0));

        Assert.Equal(EditOutcome.Rejected, result.Outcome);
        Assert.Contains("no longer in the document", result.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void ASlotTheParentDoesNotDeclareIsAReason()
    {
        var (rule, _, _, second, step) = Model();

        using var session = TestEditor.Open(rule);

        var result = session.Apply(new MoveNodeToAction(step.Id, second.Id, "nowhere", 0));

        Assert.Equal(EditOutcome.Rejected, result.Outcome);
        Assert.Contains("declares no child slot", result.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void AConceptTheSlotDoesNotHoldIsAReason()
    {
        var (rule, body, _, _, _) = Model();
        var note = rule.Model.NewChild(body, "notes", TestLanguage.Note);

        using var session = TestEditor.Open(rule);

        // A note cannot stand where a step is expected: the slot's own declaration says so, and the model would throw.
        var result = session.Apply(new MoveNodeToAction(note.Id, body.Id, "steps", 0));

        Assert.Equal(EditOutcome.Rejected, result.Outcome);
        Assert.Contains("cannot stand in slot", result.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void ANodeCannotBeMovedIntoItself()
    {
        var (rule, body, _, second, _) = Model();

        using var session = TestEditor.Open(rule);

        var result = session.Apply(new MoveNodeToAction(body.Id, second.Id, "detail", 0));

        Assert.Equal(EditOutcome.Rejected, result.Outcome);
        Assert.Contains("into itself", result.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void ASlotThatHoldsOneChildCannotBeGivenASecond()
    {
        var (rule, _, first, second, _) = Model();

        using var session = TestEditor.Open(rule);

        // `first`'s detail already holds a step, and the slot holds one.
        var result = session.Apply(new MoveNodeToAction(second.Id, first.Id, "detail", 0));

        Assert.Equal(EditOutcome.Rejected, result.Outcome);
        Assert.Contains("at most one child", result.Reason, StringComparison.Ordinal);
    }

    /// <summary>
    /// A body holding two steps, whose first already has a detail and whose second has none - two parents inside one
    /// tree, so a node can move out of one into the other. The caret has to stay inside one root: a session draws one,
    /// and a node of another root is not in its tree at all.
    /// </summary>
    private static (Node Rule, Node Body, Node First, Node Second, Node Step) Model()
    {
        var (rule, body, first) = TestEditor.RuleWithStep();
        var second = body.Model.NewChild(body, "steps", TestLanguage.Step);
        var step = body.Model.NewChild(first, "detail", TestLanguage.Step);

        return (rule, body, first, second, step);
    }

    [Fact]
    public void AProgramOfAMoveAndAnEditIsOneUndoStep()
    {
        // What a refactoring is: the move plus whatever else the same gesture decides. The program is the shape the
        // declared extract will lower into, and one undo step is the promise every other edit keeps.
        var (rule, _, first, second, step) = Model();

        using var session = TestEditor.Open(rule);
        var id = step.Id;

        var program = EditProgram.Of(
            new MoveNodeToAction(id, second.Id, "detail", 0),
            new SetNodePropertyAction(id, "text", "moved"));

        Assert.Equal(EditOutcome.Applied, session.Apply(program).Outcome);
        Assert.Equal("moved", step.GetOrNull("text"));
        Assert.Same(second, step.Parent);

        session.History.Undo();

        Assert.Same(first, step.Parent);
        Assert.NotEqual("moved", step.GetOrNull("text"));
    }
}

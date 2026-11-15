using Projectional.Core;
using Projectional.Editing;
using Projectional.Projection;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// What Backspace and Delete do where there is no character to remove: they remove the node the caret
/// stands in.
/// </summary>
/// <remarks>
/// A projectional editor has no characters between cells, so the neighbour of a caret on a keyword is a
/// node. These tests pin the two halves of that: a key that still has text to delete deletes text, and a
/// key that has none deletes the node - landing the caret where the node used to be, so the next one can
/// be typed without moving.
/// </remarks>
public sealed class DeleteNodeActionTests
{
    [Fact]
    public void BackspaceOnAKeywordDeletesTheNodeTheKeywordBelongsTo()
    {
        var (body, steps) = BodyWithSteps("first", "second");
        using var session = TestEditor.Open(body);

        // The step's own address resolves to its first caret place, which is the "index" property.
        session.Select(Selection.At(new CellAddress(steps[1].Id)));

        var result = session.Apply(KeyRouter.For(session, EditorKey.Backspace));

        Assert.Equal(EditOutcome.Applied, result.Outcome);
        Assert.Equal(new[] { steps[0] }, body.GetChildren("steps"));
    }

    [Fact]
    public void DeletingANodeLandsTheCaretOnTheSiblingBeforeIt()
    {
        var (body, steps) = BodyWithSteps("first", "second", "third");
        using var session = TestEditor.Open(body);

        session.Select(Selection.At(new CellAddress(steps[1].Id)));

        var result = session.Apply(KeyRouter.For(session, EditorKey.Backspace));

        // The element address of the sibling before it, which the delete does not shift.
        Assert.Equal(
            CellAddress.OfElement(body.Id, "steps", 0),
            result.Selection.Caret.Address);
        Assert.Equal(steps[0].Id, session.Selection.Caret.Address.Owner);
    }

    [Fact]
    public void DeletingTheFirstNodeLandsTheCaretOnTheOneThatTakesItsIndex()
    {
        var (body, steps) = BodyWithSteps("first", "second");
        using var session = TestEditor.Open(body);

        session.Select(Selection.At(new CellAddress(steps[0].Id)));

        var result = session.Apply(KeyRouter.For(session, EditorKey.Backspace));

        Assert.Equal(
            CellAddress.OfElement(body.Id, "steps", 0),
            result.Selection.Caret.Address);
        Assert.Equal(steps[1].Id, session.Selection.Caret.Address.Owner);
    }

    [Fact]
    public void DeletingTheOnlyNodeLandsTheCaretOnTheEmptySlot()
    {
        var (body, steps) = BodyWithSteps("only");
        using var session = TestEditor.Open(body);

        session.Select(Selection.At(new CellAddress(steps[0].Id)));

        var result = session.Apply(KeyRouter.For(session, EditorKey.Backspace));

        // The slot is empty now, so its own address is the place a new node would be created at.
        Assert.Equal(CellAddress.OfSlot(body.Id, "steps"), result.Selection.Caret.Address);
        Assert.Equal(CellAddress.OfSlot(body.Id, "steps"), session.Selection.Caret.Address);
    }

    [Fact]
    public void BackspaceEmptiesASingleValuedSlot()
    {
        var (rule, body) = TestEditor.RuleWithBody();
        using var session = TestEditor.Open(rule);

        // A child's own notation root carries the parent's element address, and this body's notation is a
        // bare sequence with no constant of its own, so the body has no cell named after it. The element
        // address is what puts the caret inside the body, which is where a user clicking into it lands.
        session.Select(Selection.At(CellAddress.OfElement(rule.Id, "body", 0)));

        var result = session.Apply(KeyRouter.For(session, EditorKey.Backspace));

        Assert.Equal(EditOutcome.Applied, result.Outcome);
        Assert.Null(rule.GetChild("body"));
        Assert.Equal(CellAddress.OfSlot(rule.Id, "body"), result.Selection.Caret.Address);
    }

    [Fact]
    public void DeleteAtTheEndOfAValueDeletesTheNode()
    {
        var (body, steps) = BodyWithSteps("first", "second");
        using var session = TestEditor.Open(body);
        var text = CellAddress.OfSlot(steps[0].Id, "text");

        session.Select(new Selection(CaretPosition.At(text, 5), CaretPosition.At(text, 5)));

        var result = session.Apply(KeyRouter.For(session, EditorKey.Delete));

        Assert.Equal(EditOutcome.Applied, result.Outcome);
        Assert.Equal(new[] { steps[1] }, body.GetChildren("steps"));
    }

    [Fact]
    public void BackspaceInsideAValueStillDeletesACharacter()
    {
        var (body, steps) = BodyWithSteps("first");
        using var session = TestEditor.Open(body);
        var text = CellAddress.OfSlot(steps[0].Id, "text");

        session.Select(new Selection(CaretPosition.At(text, 3), CaretPosition.At(text, 3)));

        var action = KeyRouter.For(session, EditorKey.Backspace);
        session.Apply(action);

        Assert.IsType<DeleteTextAction>(action);
        Assert.Equal("fist", steps[0].GetOrNull("text"));
    }

    [Fact]
    public void BackspaceWithNoTextLeftDeletesTheNodeInsteadOfDoingNothing()
    {
        var (body, steps) = BodyWithSteps("first", "second");
        using var session = TestEditor.Open(body);
        var text = CellAddress.OfSlot(steps[0].Id, "text");

        session.Select(Selection.At(text));

        var action = KeyRouter.For(session, EditorKey.Backspace);
        session.Apply(action);

        Assert.IsType<DeleteNodeAction>(action);
        Assert.Equal(new[] { steps[1] }, body.GetChildren("steps"));
    }

    [Fact]
    public void TheDocumentRootIsNeverDeleted()
    {
        var step = TestEditor.BareStep("abc");
        using var session = TestEditor.Open(step);
        var text = CellAddress.OfSlot(step.Id, "text");

        session.Select(Selection.At(text));

        var result = session.Apply(KeyRouter.For(session, EditorKey.Backspace));

        Assert.Equal(EditOutcome.Unchanged, result.Outcome);
        Assert.Equal(new[] { step }, step.Model.Roots);
    }

    [Fact]
    public void DeletingANodeIsOneUndoableStepThatRestoresTheSubtree()
    {
        var (body, steps) = BodyWithSteps("first", "second");
        using var session = TestEditor.Open(body);

        // A nested child makes the deleted node a subtree rather than a single node.
        var detail = body.Model.NewChild(steps[1], "detail", TestLanguage.Step);
        session.Refresh();
        var before = session.History.UndoDepth;

        session.Select(Selection.At(new CellAddress(steps[1].Id)));
        session.Apply(KeyRouter.For(session, EditorKey.Backspace));

        Assert.Equal(before + 1, session.History.UndoDepth);

        Assert.True(session.Undo());
        Assert.Equal(new[] { steps[0], steps[1] }, body.GetChildren("steps"));
        Assert.Same(detail, steps[1].GetChild("detail"));
    }

    [Fact]
    public void DeleteNodeActionRejectsANullContext()
    {
        Assert.Throws<ArgumentNullException>(() => new DeleteNodeAction().Apply(null!));
    }

    private static (Node Body, IReadOnlyList<Node> Steps) BodyWithSteps(params string[] texts)
    {
        var (_, body) = TestEditor.RuleWithBody();

        return (body, [.. texts.Select(text =>
        {
            var step = body.Model.NewChild(body, "steps", TestLanguage.Step);
            step.Set("text", text);

            return step;
        })]);
    }
}

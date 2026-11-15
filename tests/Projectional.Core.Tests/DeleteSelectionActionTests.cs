using Projectional.Core;
using Projectional.Editing;
using Projectional.Projection;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// What Delete does with a selection that spans cells: it removes the nodes the range covers, as one
/// edit.
/// </summary>
/// <remarks>
/// Select All widens from a value to a node to the document, and until this action existed the widest two
/// rungs left Delete with nothing to do - the selection held no text, so the text action refused and the
/// keystroke was a no-op. These tests pin the three answers the action has to get right: which nodes a
/// range covers (the outermost ones, never the root), that the removals are one undo step, and where the
/// caret ends up afterwards.
/// </remarks>
public sealed class DeleteSelectionActionTests
{
    [Fact]
    public void DeletingASelectedNodeRemovesIt()
    {
        var (body, steps) = TestEditor.BodyWithSteps("first", "second");
        using var session = TestEditor.Open(body);

        SelectWhole(session, steps[0]);

        var result = session.Apply(KeyRouter.For(session, EditorKey.Delete));

        Assert.Equal(EditOutcome.Applied, result.Outcome);
        Assert.Equal(new[] { steps[1] }, body.GetChildren("steps"));
    }

    [Fact]
    public void DeletingASelectionLandsOnTheSiblingBeforeTheFirstNodeRemoved()
    {
        var (body, steps) = TestEditor.BodyWithSteps("first", "second", "third");
        using var session = TestEditor.Open(body);

        SelectWhole(session, steps[1]);

        var result = session.Apply(KeyRouter.For(session, EditorKey.Delete));

        Assert.Equal(CellAddress.OfElement(body.Id, "steps", 0), result.Selection.Caret.Address);

        // An element address names a child's projection root, which arranges other cells and is therefore
        // not a caret stop. The session settles onto the first slot inside it - the sibling the action
        // asked for, one level down.
        Assert.Equal(steps[0].Id, session.Selection.Caret.Address.Owner);
    }

    [Fact]
    public void DeletingTheOnlyNodeLandsOnTheEmptiedSlot()
    {
        var (body, steps) = TestEditor.BodyWithSteps("only");
        using var session = TestEditor.Open(body);

        SelectWhole(session, steps[0]);

        var result = session.Apply(KeyRouter.For(session, EditorKey.Delete));

        Assert.Empty(body.GetChildren("steps"));
        Assert.Equal(CellAddress.OfSlot(body.Id, "steps"), result.Selection.Caret.Address);
    }

    [Fact]
    public void DeletingEverythingLeavesTheRootInPlace()
    {
        var (rule, _, step) = TestEditor.RuleWithStep();
        step.Set("text", "abc");
        using var session = TestEditor.Open(rule);

        SelectDocument(session, rule);

        var result = session.Apply(KeyRouter.For(session, EditorKey.Delete));

        Assert.Equal(EditOutcome.Applied, result.Outcome);
        Assert.Null(rule.GetChild("body"));
        Assert.Equal(new[] { rule }, rule.Model.Roots);
    }

    [Fact]
    public void DeletingASelectionInsideOneCellChangesNothing()
    {
        var (body, steps) = TestEditor.BodyWithSteps("first", "second");
        using var session = TestEditor.Open(body);
        var text = CellAddress.OfSlot(steps[0].Id, "text");

        session.Select(new Selection(CaretPosition.At(text, 0), CaretPosition.At(text, 3)));

        // Asked directly, because the router sends a single-cell selection to the text action - this pins
        // what the structural action itself does when it is handed one.
        var result = session.Apply(new DeleteSelectionAction());

        Assert.Equal(EditOutcome.Unchanged, result.Outcome);
        Assert.Equal(new[] { steps[0], steps[1] }, body.GetChildren("steps"));
        Assert.Equal("first", steps[0].Get("text"));
    }

    [Fact]
    public void DeletingASelectionThatCoversNoWholeNodeChangesNothing()
    {
        var (body, steps) = TestEditor.BodyWithSteps("first", "second");
        using var session = TestEditor.Open(body);

        // From the start of the index value to the middle of the text value: two cells, so it spans, but
        // it ends inside the node it starts in and therefore covers none.
        session.Select(new Selection(
            CaretPosition.At(CellAddress.OfSlot(steps[0].Id, "index"), 0),
            CaretPosition.At(CellAddress.OfSlot(steps[0].Id, "text"), 2)));

        Assert.True(session.Selection.SpansCells);

        var result = session.Apply(new DeleteSelectionAction());

        Assert.Equal(EditOutcome.Unchanged, result.Outcome);
        Assert.Equal(new[] { steps[0], steps[1] }, body.GetChildren("steps"));
    }

    [Fact]
    public void DeletingANestedSelectionRemovesOnlyTheOutermostNode()
    {
        var (rule, body, step) = TestEditor.RuleWithStep();
        using var session = TestEditor.Open(rule);
        var before = session.History.UndoDepth;

        SelectDocument(session, rule);

        var result = session.Apply(KeyRouter.For(session, EditorKey.Delete));

        Assert.Equal(EditOutcome.Applied, result.Outcome);
        Assert.Null(rule.GetChild("body"));

        // The step went with the body it hung in: a removal unlinks the node it was given and leaves the
        // descendants attached, which is what lets one undo put the whole subtree back. Had the walk
        // reported the step as a second node, deleting it would have been a second edit of a node that is
        // already gone from the document.
        Assert.Equal(new[] { step }, body.GetChildren("steps"));
        Assert.Equal(before + 1, session.History.UndoDepth);

        Assert.True(session.Undo());
        Assert.Equal(body, rule.GetChild("body"));
        Assert.Equal(new[] { step }, body.GetChildren("steps"));
    }

    [Fact]
    public void DeletingSeveralNodesIsOneUndoableStep()
    {
        var (body, steps) = TestEditor.BodyWithSteps("first", "second", "third");
        using var session = TestEditor.Open(body);
        var before = session.History.UndoDepth;

        session.Select(Selection.At(CellAddress.OfSlot(steps[0].Id, "text")));
        session.Apply(KeyRouter.For(EditorKey.SelectAll));
        session.Apply(KeyRouter.For(EditorKey.SelectAll));
        session.Apply(KeyRouter.For(EditorKey.SelectAll));

        var result = session.Apply(KeyRouter.For(session, EditorKey.Delete));

        Assert.Equal(EditOutcome.Applied, result.Outcome);
        Assert.Empty(body.GetChildren("steps"));
        Assert.Equal(before + 1, session.History.UndoDepth);

        Assert.True(session.Undo());
        Assert.Equal(new[] { steps[0], steps[1], steps[2] }, body.GetChildren("steps"));
    }

    [Fact]
    public void DeleteRoutesToTheSelectionActionOnlyWhenTheSelectionSpansCells()
    {
        var (body, steps) = TestEditor.BodyWithSteps("first", "second");
        using var session = TestEditor.Open(body);
        var text = CellAddress.OfSlot(steps[0].Id, "text");

        session.Select(Selection.At(new CellAddress(steps[0].Id)));
        Assert.IsType<DeleteNodeAction>(KeyRouter.For(session, EditorKey.Delete));

        session.Select(new Selection(CaretPosition.At(text, 0), CaretPosition.At(text, 2)));
        Assert.IsType<DeleteTextAction>(KeyRouter.For(session, EditorKey.Delete));

        SelectWhole(session, steps[0]);
        Assert.True(session.Selection.SpansCells);
        Assert.IsType<DeleteSelectionAction>(KeyRouter.For(session, EditorKey.Delete));
    }

    [Fact]
    public void TheMenuDeleteEntryDeletesTheSelection()
    {
        var (body, steps) = TestEditor.BodyWithSteps("first", "second");
        using var session = TestEditor.Open(body);

        SelectWhole(session, steps[0]);

        var delete = Assert.IsType<EditorMenuItem>(
            EditorMenu.For(session).Single(entry => entry.Label == "Delete"));

        Assert.True(delete.IsEnabled);

        delete.Execute();

        Assert.Equal(new[] { steps[1] }, body.GetChildren("steps"));
    }

    [Fact]
    public void DeleteSelectionActionRejectsANullContext()
    {
        Assert.Throws<ArgumentNullException>(() => new DeleteSelectionAction().Apply(null!));
    }

    /// <summary>Widens the caret to the whole of <paramref name="node"/>.</summary>
    /// <remarks>
    /// One press is enough because the caret is placed on the node's own address, which resolves to the
    /// first place inside it - a value - so the first press selects the node that value belongs to. The
    /// next press would widen to the whole document.
    /// </remarks>
    private static void SelectWhole(EditorSession session, Node node)
    {
        session.Select(Selection.At(new CellAddress(node.Id)));
        session.Apply(KeyRouter.For(EditorKey.SelectAll));
    }

    /// <summary>Widens the caret to the whole document.</summary>
    /// <remarks>
    /// The ladder has three rungs - value, node, document - so three presses are the most that can be
    /// needed; a fourth would change nothing.
    /// </remarks>
    private static void SelectDocument(EditorSession session, Node within)
    {
        session.Select(Selection.At(new CellAddress(within.Id)));

        for (var press = 0; press < 3; press++)
        {
            session.Apply(KeyRouter.For(EditorKey.SelectAll));
        }
    }
}
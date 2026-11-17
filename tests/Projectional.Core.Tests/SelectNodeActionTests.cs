using Projectional.Core;
using Projectional.Editing;
using Projectional.Projection;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>Selecting a named node: the whole subtree, its parent, and the two refusals.</summary>
/// <remarks>
/// What these pin is that the range is the node's <em>whole</em> drawing - the outermost cell through the last one
/// any descendant owns - because that is what makes Delete remove the node and Copy take it. A selection that stopped
/// at the node's own cells would leave the body out.
/// </remarks>
public sealed class SelectNodeActionTests
{
    [Fact]
    public void TheWholeNodeIsSelected()
    {
        var (body, steps) = TestEditor.BodyWithSteps("a");

        using var session = TestEditor.Open(body.Parent!);

        var result = session.Apply(new SelectNodeAction(steps[0].Id));

        Assert.Equal(EditOutcome.Unchanged, result.Outcome);
        AssertCovers(session, steps[0]);
    }

    [Fact]
    public void TheParentCanBeSelectedFromACaretInsideAChild()
    {
        var (body, steps) = TestEditor.BodyWithSteps("a");

        using var session = TestEditor.Open(body.Parent!);
        session.Select(Selection.At(CellAddress.OfSlot(steps[0].Id, "text")));

        session.Apply(new SelectNodeAction(body.Id));

        // The parent's range reaches past its own cells into the child's, which is the point of selecting a subtree.
        AssertCovers(session, body);
        Assert.True(session.Selection.Anchor.Address.Owner != session.Selection.Caret.Address.Owner);
    }

    [Fact]
    public void SelectingWhatIsAlreadySelectedChangesNothing()
    {
        var (body, steps) = TestEditor.BodyWithSteps("a");

        using var session = TestEditor.Open(body.Parent!);

        session.Apply(new SelectNodeAction(steps[0].Id));

        var before = session.Selection;
        var result = session.Apply(new SelectNodeAction(steps[0].Id));

        Assert.Equal(EditOutcome.Unchanged, result.Outcome);
        Assert.Equal(before.Anchor.Address, session.Selection.Anchor.Address);
        Assert.Equal(before.Caret.Address, session.Selection.Caret.Address);
        Assert.Equal(before.Caret.Offset, session.Selection.Caret.Offset);
    }

    [Fact]
    public void ANodeThatIsGoneIsRejected()
    {
        var (body, steps) = TestEditor.BodyWithSteps("a");

        using var session = TestEditor.Open(body.Parent!);

        session.Select(Selection.At(CellAddress.OfSlot(steps[0].Id, "text")));
        session.Apply(new DeleteNodeAction());

        var result = session.Apply(new SelectNodeAction(steps[0].Id));

        Assert.Equal(EditOutcome.Rejected, result.Outcome);
        Assert.NotNull(result.Reason);
    }

    [Fact]
    public void ASelectionIsNotAnUndoStep()
    {
        var (body, steps) = TestEditor.BodyWithSteps("a");

        using var session = TestEditor.Open(body.Parent!);

        session.Apply(new SelectNodeAction(steps[0].Id));

        Assert.False(session.Undo());
    }

    /// <summary>Asserts the selection runs from the first place of <paramref name="node"/>'s drawing to its last.</summary>
    private static void AssertCovers(EditorSession session, Node node)
    {
        var owned = Ids(node);
        var places = CaretPlaces.Of(session.Tree).Where(place => owned.Contains(place.Address.Owner)).ToList();

        Assert.NotEmpty(places);
        Assert.False(session.Selection.IsSingleCell);
        Assert.Equal(places[0].Address, session.Selection.Anchor.Address);
        Assert.Equal(0, session.Selection.Anchor.Offset);
        Assert.Equal(places[^1].Address, session.Selection.Caret.Address);
        Assert.Equal(places[^1].Length, session.Selection.Caret.Offset);
    }

    /// <summary>The ids of <paramref name="node"/> and everything below it.</summary>
    private static HashSet<NodeId> Ids(Node node)
    {
        HashSet<NodeId> ids = [node.Id];

        foreach (var child in node.ChildrenInDocumentOrder())
        {
            ids.UnionWith(Ids(child));
        }

        return ids;
    }
}

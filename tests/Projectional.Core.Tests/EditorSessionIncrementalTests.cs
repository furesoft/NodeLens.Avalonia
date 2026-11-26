using Projectional.Core;
using Projectional.Editing;
using Projectional.Projection;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The safety net for incremental projection: whatever a change does, the tree the session rebuilds has to
/// draw what a session built from notation would draw.
/// </summary>
/// <remarks>
/// <para>
/// The session keeps the projections a change did not invalidate and rebuilds the rest. That is only sound if
/// "did not invalidate" is right, and the way to know is not to read the invalidation rules but to compare
/// against a session that has no cache at all. Every test here therefore renders the session after a change and
/// renders a fresh session over the same model, and asserts the two are the same text.
/// </para>
/// <para>
/// The cases are the ones where "the projection did not change" could be wrong: a change deep in the tree, a
/// change nobody told the session about, an insertion that moves its siblings' addresses, a removal, an undo,
/// a multi-step program (whose rebuilds happen before the commit publishes the change), and a reference whose
/// target changed elsewhere.
/// </para>
/// </remarks>
public sealed class EditorSessionIncrementalTests
{
    [Fact]
    public void APropertyEditDeepInTheTreeDrawsWhatAFreshSessionDraws()
    {
        var (body, steps) = TestEditor.BodyWithSteps("one", "two");
        var document = body.Parent!;
        using var session = TestEditor.Open(document);

        session.Apply(new SetNodePropertyAction(steps[0].Id, "text", "changed"));

        AssertMatchesAFreshSession(session, document);
    }

    [Fact]
    public void AChangeTheSessionWasNotToldAboutDrawsWhatAFreshSessionDraws()
    {
        var (body, steps) = TestEditor.BodyWithSteps("one", "two");
        var document = body.Parent!;
        using var session = TestEditor.Open(document);

        steps[0].Set("text", "typed elsewhere");
        session.Refresh();

        AssertMatchesAFreshSession(session, document);
    }

    [Fact]
    public void InsertingAChildThatMovesItsSiblingsDrawsWhatAFreshSessionDraws()
    {
        var (body, steps) = TestEditor.BodyWithSteps("one", "two");
        var document = body.Parent!;
        using var session = TestEditor.Open(document);

        body.Model.NewChildAt(body, "steps", 0, TestLanguage.Step);
        session.Refresh();

        AssertMatchesAFreshSession(session, document);

        // The moved siblings keep their projections only if their addresses still match, so the render above is
        // the real assertion: the element addresses after the insertion name different nodes.
        Assert.Equal(3, body.GetChildren("steps").Count);
        Assert.Equal("one", steps[0].Get("text"));
    }

    [Fact]
    public void RemovingANodeDrawsWhatAFreshSessionDraws()
    {
        var (body, steps) = TestEditor.BodyWithSteps("one", "two", "three");
        var document = body.Parent!;
        using var session = TestEditor.Open(document);

        body.RemoveChild("steps", steps[1]);
        session.Refresh();

        AssertMatchesAFreshSession(session, document);
    }

    [Fact]
    public void AnUndoDrawsWhatAFreshSessionDraws()
    {
        var (body, steps) = TestEditor.BodyWithSteps("one", "two");
        var document = body.Parent!;
        using var session = TestEditor.Open(document);

        session.Apply(new SetNodePropertyAction(steps[0].Id, "text", "changed"));
        Assert.True(session.Undo());

        AssertMatchesAFreshSession(session, document);
    }

    [Fact]
    public void AProgramDrawsWhatAFreshSessionDraws()
    {
        var (body, steps) = TestEditor.BodyWithSteps("one", "two");
        var document = body.Parent!;
        using var session = TestEditor.Open(document);

        // The steps of a program rebuild before the commit publishes the change, so the second step's rebuild
        // is the one that would reuse a projection the first step invalidated.
        session.Apply(EditProgram.Of(
            new SetNodePropertyAction(steps[0].Id, "text", "first"),
            new SetNodePropertyAction(steps[0].Id, "text", "second")));

        AssertMatchesAFreshSession(session, document);
        Assert.Contains("second", LayoutSnapshot.Render(session.Layout, width: 120), StringComparison.Ordinal);
    }

    [Fact]
    public void AReferenceFollowsTheNodeItNames()
    {
        var (rule, tag) = TestEditor.RuleUsingTag();
        tag.Set("label", "original");
        using var session = TestEditor.Open(rule);

        Assert.Contains("original", LayoutSnapshot.Render(session.Layout, width: 120), StringComparison.Ordinal);

        // The tag is not a descendant of the rule, so nothing about the rule's subtree changed. A projection
        // that shows a reference is never reused for exactly this reason.
        tag.Set("label", "renamed");
        session.Refresh();

        Assert.Contains("renamed", LayoutSnapshot.Render(session.Layout, width: 120), StringComparison.Ordinal);
        AssertMatchesAFreshSession(session, rule);
    }

    [Fact]
    public void AChangeKeepsTheProjectionOfASiblingItDidNotTouch()
    {
        var (body, steps) = TestEditor.BodyWithSteps("one", "two");
        var document = body.Parent!;
        using var session = TestEditor.Open(document);

        var untouched = CellAt(session.Tree, CellAddress.OfElement(body.Id, "steps", 1));

        session.Apply(new SetNodePropertyAction(steps[0].Id, "text", "changed"));

        // The point of the cache: the sibling's projection is the same object, not an equal one. The comparison
        // above proves the reuse is correct; this one proves it happens.
        Assert.Same(untouched, CellAt(session.Tree, CellAddress.OfElement(body.Id, "steps", 1)));
    }

    private static void AssertMatchesAFreshSession(EditorSession session, Node document)
    {
        using var fresh = TestEditor.Open(document);

        Assert.Equal(
            LayoutSnapshot.Render(fresh.Layout, width: 120),
            LayoutSnapshot.Render(session.Layout, width: 120));
    }

    private static EditorCell? CellAt(EditorTree tree, CellAddress address)
    {
        EditorCell? found = null;

        Visit(tree.Root);

        return found;

        void Visit(EditorCell cell)
        {
            if (found is not null)
            {
                return;
            }

            if (cell.Address == address)
            {
                found = cell;
                return;
            }

            foreach (var child in cell.Children)
            {
                Visit(child);
            }
        }
    }
}

using Projectional.Core;
using Projectional.Editing;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>The action that puts a wrapper's only child in its place, addressed by the node rather than a cell.</summary>
/// <remarks>
/// The model is the authority on a legal placement, so the action's own refusals are the cheap ones and everything
/// else comes back as a rejection carrying the model's sentence - including the cases the model alone can judge,
/// such as a node that still holds a second child list. A test that expected an exception here would be testing
/// the wrong contract.
/// </remarks>
public sealed class UnwrapActionTests
{
    [Fact]
    public void ItHoistsTheChildIntoTheNodesPlace()
    {
        var (root, outer, inner) = Chain();

        using var session = TestEditor.Open(root);

        var result = session.Apply(new UnwrapAction(outer.Id, "extends"));

        Assert.Equal(EditOutcome.Applied, result.Outcome);
        Assert.Same(inner, Assert.Single(root.GetChildren("extends")));

        Assert.True(session.Undo());
        Assert.Same(outer, Assert.Single(root.GetChildren("extends")));
        Assert.Same(inner, Assert.Single(outer.GetChildren("extends")));
    }

    [Fact]
    public void ANodeTheDocumentDoesNotHoldIsRejected()
    {
        var (root, _, _) = Chain();

        using var session = TestEditor.Open(root);

        var result = session.Apply(new UnwrapAction(NodeId.New(), "extends"));

        Assert.Equal(EditOutcome.Rejected, result.Outcome);
        Assert.Contains("no longer holds", result.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void ARootIsRejected()
    {
        var (root, _, _) = Chain();

        using var session = TestEditor.Open(root);

        var result = session.Apply(new UnwrapAction(root.Id, "extends"));

        Assert.Equal(EditOutcome.Rejected, result.Outcome);
        Assert.Contains("not inside anything", result.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void ASlotTheConceptDoesNotDeclareIsRejected()
    {
        var (root, outer, _) = Chain();

        using var session = TestEditor.Open(root);

        var result = session.Apply(new UnwrapAction(outer.Id, "name"));

        Assert.Equal(EditOutcome.Rejected, result.Outcome);
        Assert.Contains("declares no child slot named 'name'", result.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void AnEmptySlotIsRejected()
    {
        var (root, outer, _) = Chain();

        using var session = TestEditor.Open(root);

        var result = session.Apply(new UnwrapAction(outer.Id, "body"));

        Assert.Equal(EditOutcome.Rejected, result.Outcome);
        Assert.Contains("holds 0 children", result.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void WhatOnlyTheModelCanJudgeComesBackAsARejection()
    {
        // `outer` still holds a body, so unwrapping it would delete that subtree. The action does not repeat that
        // rule - it asks the model and translates the refusal, which is what keeps one authority.
        var (root, outer, _) = Chain();
        outer.Model.NewChild(outer, "body", TestLanguage.Body);

        using var session = TestEditor.Open(root);

        var result = session.Apply(new UnwrapAction(outer.Id, "extends"));

        Assert.Equal(EditOutcome.Rejected, result.Outcome);
        Assert.Contains("also holds children in 'body'", result.Reason, StringComparison.Ordinal);
        Assert.Same(outer, Assert.Single(root.GetChildren("extends")));
    }

    [Fact]
    public void AProgramOfTwoUnwrapsRemovesBothOperatorsInOneStep()
    {
        // The shape STD041's repair uses: the inner negation takes the outer's place, and is then unwrapped too, so
        // the operand itself ends up where the pair stood.
        var model = TestLanguage.NewModel();
        var root = model.NewNode(TestLanguage.Rule);
        model.AddRoot(root);

        var outer = model.NewChild(root, "extends", TestLanguage.Rule);
        var inner = model.NewChild(outer, "extends", TestLanguage.Rule);
        var leaf = model.NewChild(inner, "extends", TestLanguage.Rule);

        using var session = TestEditor.Open(root);

        var result = session.Apply(EditProgram.Of(
            new UnwrapAction(outer.Id, "extends"),
            new UnwrapAction(inner.Id, "extends")));

        Assert.Equal(EditOutcome.Applied, result.Outcome);
        Assert.Equal(leaf.Id, Assert.Single(root.GetChildren("extends")).Id);
        Assert.Null(model.Project.FindNode(outer.Id));
        Assert.Null(model.Project.FindNode(inner.Id));

        Assert.True(session.Undo());
        Assert.Equal(outer.Id, Assert.Single(root.GetChildren("extends")).Id);
        Assert.Equal(inner.Id, Assert.Single(outer.GetChildren("extends")).Id);
        Assert.Equal(leaf.Id, Assert.Single(inner.GetChildren("extends")).Id);
    }

    /// <summary><c>root -extends-&gt; outer -extends-&gt; inner</c>.</summary>
    private static (Node Root, Node Outer, Node Inner) Chain()
    {
        var model = TestLanguage.NewModel();
        var root = model.NewNode(TestLanguage.Rule);
        model.AddRoot(root);

        var outer = model.NewChild(root, "extends", TestLanguage.Rule);

        return (root, outer, model.NewChild(outer, "extends", TestLanguage.Rule));
    }
}
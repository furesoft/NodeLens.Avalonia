using Xunit;

namespace Projectional.Core.Tests;

/// <summary>Unwrapping: the only child of a slot takes its parent's place, and the parent is deleted.</summary>
/// <remarks>
/// The chain the tests use is <c>g -extends-&gt; outer -extends-&gt; inner</c>, because a node can only be unwrapped
/// where something can take its place: the slot that held it has to accept the child it gives up. <c>extends</c>
/// holds a rule, so unwrapping an inner rule into an outer rule's place is the conforming case - and unwrapping a
/// body into a rule's place is the case the model must refuse.
/// </remarks>
public sealed class ModelUnwrapTests
{
    [Fact]
    public void TheChildTakesThePlaceTheNodeStoodIn()
    {
        var (model, outer, inner) = Chain();
        var root = outer.Parent!;

        var hoisted = model.Unwrap(outer, "extends");

        Assert.Same(inner, hoisted);
        Assert.Same(root, inner.Parent);
        Assert.Equal("extends", inner.ParentSlot);
        Assert.Equal(inner.Id, root.GetChildren("extends")[0].Id);

        // The node is gone from the model and from the project index, not only from the tree.
        Assert.Null(model.Project.FindNode(outer.Id));
        Assert.DoesNotContain(model.Nodes, node => node.Id == outer.Id);
    }

    [Fact]
    public void ItIsOneUndoStepAndRedoPutsTheWrapperBack()
    {
        var (model, outer, inner) = Chain();

        using var history = new UndoHistory(model);

        model.Unwrap(outer, "extends");

        Assert.Equal(1, history.UndoDepth);
        Assert.True(history.Undo());

        var back = outer.Parent!.GetChildren("extends")[0];

        Assert.Equal(outer.Id, back.Id);
        Assert.Equal(inner.Id, Assert.Single(back.GetChildren("extends")).Id);
        Assert.NotNull(model.Project.FindNode(outer.Id));

        Assert.True(history.Redo());
        Assert.Null(model.Project.FindNode(outer.Id));
    }

    [Fact]
    public void AReferenceIntoTheDeletedNodeIsCleared()
    {
        // The node is deleted, so a document that kept pointing at it would be pointing at nothing - the same rule
        // a deletion follows, because that is what this is.
        var (model, outer, _) = Chain();
        var other = model.NewNode(TestLanguage.Rule);

        model.AddRoot(other);
        other.SetReference("inherits", outer);

        model.Unwrap(outer, "extends");

        Assert.Null(other.GetReference("inherits"));
    }

    [Fact]
    public void AModelRootHasNoPlaceToGiveUp()
    {
        var (model, outer, _) = Chain();
        var root = outer.Parent!;

        var error = Assert.Throws<ModelInvariantException>(() => model.Unwrap(root, "extends"));

        Assert.Contains("has no place", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ASlotTheConceptDoesNotDeclareIsRefused()
    {
        var (model, outer, _) = Chain();

        var error = Assert.Throws<ModelInvariantException>(() => model.Unwrap(outer, "nope"));

        Assert.Contains("no child slot named 'nope'", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnEmptySlotIsRefused()
    {
        var (model, outer, _) = Chain();

        var error = Assert.Throws<ModelInvariantException>(() => model.Unwrap(outer, "body"));

        Assert.Contains("holds 0 children", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AChildThePlaceDoesNotAcceptIsRefusedAndTheDocumentIsUnchanged()
    {
        // A body cannot stand where a rule stands, so the model refuses before anything moves - the node and its
        // child are both still there afterwards.
        var (model, outer, _) = Chain();
        var body = model.NewChild(outer, "body", TestLanguage.Body);

        var error = Assert.Throws<ModelInvariantException>(() => model.Unwrap(outer, "body"));

        Assert.Contains("does not conform", error.Message, StringComparison.Ordinal);
        Assert.Same(body, outer.GetChild("body"));
        Assert.NotNull(model.Project.FindNode(outer.Id));
    }

    [Fact]
    public void UnwrappingMayNotDropAnotherChildList()
    {
        // `outer` would lose its body silently, and an unwrap that deletes a subtree nobody asked about is a delete
        // in disguise.
        var (model, outer, _) = Chain();
        var body = model.NewChild(outer, "body", TestLanguage.Body);

        var error = Assert.Throws<ModelInvariantException>(() => model.Unwrap(outer, "extends"));

        Assert.Contains("also holds children in 'body'", error.Message, StringComparison.Ordinal);
        Assert.Same(body, outer.GetChild("body"));
    }

    [Fact]
    public void UnwrappingMayNotDropAReference()
    {
        var (model, outer, _) = Chain();
        var tag = model.NewNode(TestLanguage.Tag);

        model.AddRoot(tag);
        outer.SetReference("tag", tag);

        var error = Assert.Throws<ModelInvariantException>(() => model.Unwrap(outer, "extends"));

        Assert.Contains("also points at something in 'tag'", error.Message, StringComparison.Ordinal);
        Assert.NotNull(outer.GetReference("tag"));
    }

    /// <summary><c>g -extends-&gt; outer -extends-&gt; inner</c>, with <c>g</c> as the only root.</summary>
    private static (Model Model, Node Outer, Node Inner) Chain()
    {
        var model = TestLanguage.NewModel();
        var root = model.NewNode(TestLanguage.Rule);
        model.AddRoot(root);

        var outer = model.NewChild(root, "extends", TestLanguage.Rule);
        var inner = model.NewChild(outer, "extends", TestLanguage.Rule);

        return (model, outer, inner);
    }
}
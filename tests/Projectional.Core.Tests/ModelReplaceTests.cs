using Projectional.Expressions;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// Replacing a node: a new node of another concept takes the place the old one stood in.
/// </summary>
/// <remarks>
/// <para>
/// The contract is "one place, two steps, one undo". The place survives - the new node is where the old one was,
/// at the same index in the same slot or at the same root position - and so does the old node, in the undo stack,
/// with its identity and its subtree rather than a rebuilt copy of it.
/// </para>
/// <para>
/// What does <em>not</em> survive is what the old node held: a replacement is destructive on purpose, because no
/// declaration says which slot of one concept corresponds to which slot of another. The reference test pins that
/// too, since a document whose references still pointed into a deleted subtree would be the one way this could
/// silently go wrong.
/// </para>
/// </remarks>
public sealed class ModelReplaceTests
{
    [Fact]
    public void TheNewNodeTakesThePlaceOfTheOldOne()
    {
        var model = TestLanguage.NewModel();
        var body = model.NewNode(TestLanguage.Body);
        var first = model.NewChild(body, "steps", TestLanguage.Step);
        var second = model.NewChild(body, "steps", TestLanguage.Step);
        var third = model.NewChild(body, "steps", TestLanguage.Step);

        var created = model.Replace(second, TestLanguage.Step);

        var after = body.GetChildren("steps");

        Assert.Equal(3, after.Count);
        Assert.Equal(created.Id, after[1].Id);
        Assert.Equal(first.Id, after[0].Id);
        Assert.Equal(third.Id, after[2].Id);
        Assert.NotEqual(second.Id, created.Id);

        // The old node leaves the project index, not only the tree.
        Assert.Null(model.Project.FindNode(second.Id));
    }

    [Fact]
    public void AnOnlyChildIsReplacedInItsSlot()
    {
        var model = TestLanguage.NewModel();
        var rule = model.NewNode(TestLanguage.Rule);
        var body = model.NewChild(rule, "body", TestLanguage.Body);

        var created = model.Replace(body, TestLanguage.Body);

        Assert.Equal(created.Id, Assert.Single(rule.GetChildren("body")).Id);
        Assert.Equal(rule.Id, created.Parent!.Id);
        Assert.Equal("body", created.ParentSlot);
    }

    [Fact]
    public void WhatTheOldNodeHeldDoesNotTravel()
    {
        var model = TestLanguage.NewModel();
        var body = model.NewNode(TestLanguage.Body);
        var step = model.NewChild(body, "steps", TestLanguage.Step);

        step.Set("text", "walk");
        step.Set("index", "4");
        model.NewChild(step, "detail", TestLanguage.Step);

        var created = model.Replace(step, TestLanguage.Step);

        Assert.Null(created.GetOrNull("text"));
        Assert.Null(created.GetOrNull("index"));
        Assert.Empty(created.GetChildren("detail"));
    }

    [Fact]
    public void AReplacementIsOneUndoStepThatPutsTheOldSubtreeBack()
    {
        var model = TestLanguage.NewModel();
        var body = model.NewNode(TestLanguage.Body);
        var step = model.NewChild(body, "steps", TestLanguage.Step);
        var detail = model.NewChild(step, "detail", TestLanguage.Step);

        detail.Set("text", "inside");

        using var history = new UndoHistory(model);

        var created = model.Replace(step, TestLanguage.Step);

        Assert.Equal(1, history.UndoDepth);

        Assert.True(history.Undo());

        var restored = Assert.Single(body.GetChildren("steps"));

        Assert.Equal(step.Id, restored.Id);
        Assert.Equal("inside", restored.GetChild("detail")!.GetOrNull("text"));
        Assert.Null(model.Project.FindNode(created.Id));

        Assert.True(history.Redo());
        Assert.Equal(created.Id, Assert.Single(body.GetChildren("steps")).Id);
    }

    [Fact]
    public void ARootIsReplacedAtItsOwnPosition()
    {
        var model = TestLanguage.NewModel();
        var first = model.NewNode(TestLanguage.Rule);
        var second = model.NewNode(TestLanguage.Tag);

        var created = model.Replace(first, TestLanguage.Tag);

        Assert.Equal(created.Id, model.Roots[0].Id);
        Assert.Equal(second.Id, model.Roots[1].Id);
        Assert.True(created.IsRoot);
    }

    [Fact]
    public void AConceptThatCannotStandThereLeavesTheNodeAlone()
    {
        var model = TestLanguage.NewModel();
        var body = model.NewNode(TestLanguage.Body);
        var step = model.NewChild(body, "steps", TestLanguage.Step);

        // `steps` holds steps, and a note is not one.
        Assert.Throws<ModelInvariantException>(() => model.Replace(step, TestLanguage.Note));

        Assert.Equal(step.Id, Assert.Single(body.GetChildren("steps")).Id);
        Assert.NotNull(model.Project.FindNode(step.Id));
    }

    [Fact]
    public void AnAbstractConceptDoesNotBecomeANode()
    {
        var model = ExpressionsLanguage.NewProject().NewModel("expressions");
        var literal = model.NewNode("Expressions.IntLiteral");

        Assert.Throws<ModelInvariantException>(
            () => model.Replace(literal, "Expressions.Expression"));

        Assert.Equal(literal.Id, Assert.Single(model.Roots).Id);
    }

    [Fact]
    public void ReferencesIntoTheDeletedSubtreeAreCleared()
    {
        var model = TestLanguage.NewModel();
        var pointedAt = model.NewNode(TestLanguage.Rule);
        var pointing = model.NewNode(TestLanguage.Rule);

        pointing.SetReference("inherits", pointedAt);

        model.Replace(pointedAt, TestLanguage.Rule);

        Assert.Null(pointing.GetReference("inherits"));
    }
}

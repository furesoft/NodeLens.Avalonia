using Xunit;

namespace Projectional.Core.Tests;

public sealed class NodeTests
{
    [Fact]
    public void ANewNodeIsARootOfItsModel()
    {
        var model = TestLanguage.NewModel();

        var node = model.NewNode(TestLanguage.Rule);

        Assert.Same(model, node.Model);
        Assert.True(node.IsRoot);
        Assert.Null(node.Parent);
        Assert.Null(node.ParentSlot);
        Assert.Equal(0, node.IndexInParent);
        Assert.Equal([node], model.Roots);
    }

    [Fact]
    public void PropertiesStartAtTheirDeclaredDefault()
    {
        var node = TestLanguage.NewModel().NewNode(TestLanguage.Rule);

        Assert.True(node.IsSet("name"));
        Assert.Equal("unnamed", node.Get("name"));
        Assert.Equal("true", node.GetOrNull("enabled"));
        Assert.True(node.GetBool("enabled"));
        Assert.Equal("unnamed", node.Name);
    }

    [Fact]
    public void IsNamedIsAFactOfTheConcept()
    {
        // `Test.Tag` is named by `label`, so it is named without owning a "name" property - the designation is
        // what counts, and a concept implementing Framework.Named has it without declaring anything.
        var model = TestLanguage.NewModel();

        Assert.True(model.NewNode(TestLanguage.Tag).IsNamed);
        Assert.False(model.NewNode(TestLanguage.Step).IsNamed);
    }

    [Fact]
    public void APropertyWithoutDefaultIsUnsetUntilItIsWritten()    {
        var node = TestLanguage.NewModel().NewNode(TestLanguage.Step);

        Assert.Null(node.GetOrNull("text"));
        Assert.False(node.IsSet("text"));
        Assert.Throws<ModelInvariantException>(() => node.Get("text"));
    }

    [Fact]
    public void TextValuesAreValidatedAgainstTheDeclaredType()
    {
        var node = TestLanguage.NewModel().NewNode(TestLanguage.Step);

        node.Set("index", 42);
        node.Set("status", "approved");
        node.Set("due", "2024-02-29");

        Assert.Equal(42, node.GetInt("index"));
        Assert.Equal(new DateOnly(2024, 2, 29), node.GetDate("due"));

        Assert.Throws<ModelInvariantException>(() => node.Set("index", "forty-two"));
        Assert.Throws<ModelInvariantException>(() => node.Set("status", "unknown"));
        Assert.Throws<ModelInvariantException>(() => node.Set("due", "2023-02-29"));
    }

    [Fact]
    public void ATypedSetterOnlyAcceptsItsOwnType()
    {
        var node = TestLanguage.NewModel().NewNode(TestLanguage.Step);

        Assert.Throws<ArgumentException>(() => node.Set("text", 1));
        Assert.Throws<ArgumentException>(() => node.Set("index", true));
        Assert.Throws<ArgumentException>(() => node.Set("due", 1));
    }

    [Fact]
    public void WritingTheSameValueAgainIsNotAMutation()
    {
        var model = TestLanguage.NewModel();
        var node = model.NewNode(TestLanguage.Step);

        node.Set("text", "same");
        var afterFirstWrite = model.Version;
        node.Set("text", "same");

        Assert.Equal(afterFirstWrite, model.Version);
        Assert.Equal("same", node.Get("text"));
    }

    [Fact]
    public void ASlotOfTheWrongKindIsRejected()
    {
        var node = TestLanguage.NewModel().NewNode(TestLanguage.Rule);

        Assert.Throws<KeyNotFoundException>(() => node.Get("body"));
        Assert.Throws<KeyNotFoundException>(() => node.GetChildren("name"));
        Assert.Throws<KeyNotFoundException>(() => node.AddChild("name", node));
        Assert.Throws<KeyNotFoundException>(() => node.GetReference("name"));
        Assert.Throws<KeyNotFoundException>(() => node.Set("missing", "value"));
    }

    [Fact]
    public void AChildSlotOnlyAcceptsConformingConcepts()
    {
        var model = TestLanguage.NewModel();
        var rule = model.NewNode(TestLanguage.Rule);
        var tag = model.NewNode(TestLanguage.Tag);

        Assert.Throws<ModelInvariantException>(() => rule.AddChild("body", tag));
    }

    [Fact]
    public void InsertingAChildPlacesItAtIndexAndRenumbersItsSiblings()
    {
        var model = TestLanguage.NewModel();
        var body = model.NewNode(TestLanguage.Body);
        var first = model.NewChild(body, "steps", TestLanguage.Step);
        var second = model.NewChild(body, "steps", TestLanguage.Step);
        var middle = model.NewNode(TestLanguage.Step);

        body.InsertChild("steps", 1, middle);

        Assert.Equal([first, middle, second], body.GetChildren("steps"));
        Assert.Equal(1, middle.IndexInParent);
        Assert.Equal(2, second.IndexInParent);
        Assert.Same(body, middle.Parent);
        Assert.Equal("steps", middle.ParentSlot);
        Assert.False(middle.IsRoot);
    }

    [Fact]
    public void InsertingPastTheEndAppendsInsteadOfThrowing()
    {
        var model = TestLanguage.NewModel();
        var body = model.NewNode(TestLanguage.Body);
        model.NewChild(body, "steps", TestLanguage.Step);
        var late = model.NewNode(TestLanguage.Step);

        body.InsertChild("steps", 99, late);

        Assert.Equal(1, late.IndexInParent);
        Assert.Equal(2, body.GetChildren("steps").Count);
    }

    [Fact]
    public void MovingANodeLeavesItInExactlyOnePlace()
    {
        var model = TestLanguage.NewModel();
        var left = model.NewNode(TestLanguage.Body);
        var right = model.NewNode(TestLanguage.Body);
        var step = model.NewChild(left, "steps", TestLanguage.Step);

        right.AddChild("steps", step);

        Assert.Empty(left.GetChildren("steps"));
        Assert.Equal([step], right.GetChildren("steps"));
        Assert.Same(right, step.Parent);
    }

    [Fact]
    public void ANodeCannotBeInsertedIntoItsOwnSubtree()
    {
        var model = TestLanguage.NewModel();
        var outer = model.NewNode(TestLanguage.Step);
        var inner = model.NewChild(outer, "detail", TestLanguage.Step);

        Assert.Throws<ModelInvariantException>(() => outer.AddChild("detail", outer));
        Assert.Throws<ModelInvariantException>(() => inner.AddChild("detail", outer));
    }

    [Fact]
    public void ASingleCardinalitySlotRejectsASecondChild()
    {
        var model = TestLanguage.NewModel();
        var rule = model.NewNode(TestLanguage.Rule);
        model.NewChild(rule, "body", TestLanguage.Body);
        var other = model.NewNode(TestLanguage.Body);

        Assert.Throws<ModelInvariantException>(() => rule.AddChild("body", other));
    }

    [Fact]
    public void SetChildReplacesAndDeletesThePreviousChild()
    {
        var model = TestLanguage.NewModel();
        var rule = model.NewNode(TestLanguage.Rule);
        var previous = model.NewChild(rule, "body", TestLanguage.Body);
        var replacement = model.NewNode(TestLanguage.Body);

        rule.SetChild("body", replacement);

        Assert.Equal([replacement], rule.GetChildren("body"));
        Assert.Null(model.Project.FindNode(previous.Id));
        Assert.Same(rule, replacement.Parent);
    }

    [Fact]
    public void SetChildWithTheChildItAlreadyHasChangesNothing()
    {
        var model = TestLanguage.NewModel();
        var rule = model.NewNode(TestLanguage.Rule);
        var body = model.NewChild(rule, "body", TestLanguage.Body);

        rule.SetChild("body", body);

        Assert.Equal([body], rule.GetChildren("body"));
        Assert.NotNull(model.Project.FindNode(body.Id));
    }

    [Fact]
    public void SetChildIsOnlyForSingleValuedSlots()
    {
        var model = TestLanguage.NewModel();
        var body = model.NewNode(TestLanguage.Body);

        Assert.Throws<InvalidOperationException>(() => body.SetChild("steps", null));
    }

    [Fact]
    public void ChildrenInDocumentOrderFollowTheSlotDeclarationNotTheInsertionOrder()
    {
        var model = TestLanguage.NewModel();
        var body = model.NewNode(TestLanguage.Body);
        var note = model.NewChild(body, "notes", TestLanguage.Note);
        var step = model.NewChild(body, "steps", TestLanguage.Step);

        Assert.Equal([step, note], body.ChildrenInDocumentOrder());
    }

    [Fact]
    public void ANameComesFromTheDeclaredNameSlot()
    {
        var model = TestLanguage.NewModel();
        var tag = model.NewNode(TestLanguage.Tag);

        Assert.Null(tag.Name);
        tag.Set("label", "urgent");

        Assert.Equal("urgent", tag.Name);
    }

    [Fact]
    public void AReferenceResolvesThroughTheProject()
    {
        var model = TestLanguage.NewModel();
        var rule = model.NewNode(TestLanguage.Rule);
        var tag = model.NewNode(TestLanguage.Tag);
        tag.Set("label", "urgent");

        rule.SetReference("tag", tag);

        var reference = rule.GetReference("tag");
        Assert.NotNull(reference);
        Assert.Equal(tag.Id, reference.Value.Target);
        Assert.Equal("urgent", reference.Value.ResolveInfo);
        Assert.Same(tag, rule.GetReferenceTarget("tag"));
        Assert.True(reference.Value.IsResolvedIn(model.Project));
    }

    [Fact]
    public void AReferenceRejectsANonConformingTarget()
    {
        var model = TestLanguage.NewModel();
        var rule = model.NewNode(TestLanguage.Rule);
        var body = model.NewChild(rule, "body", TestLanguage.Body);

        Assert.Throws<ModelInvariantException>(() => rule.SetReference("tag", body));
    }

    [Fact]
    public void AReferenceMustNotPointIntoItsOwnSubtree()
    {
        var model = TestLanguage.NewModel();
        var outer = model.NewNode(TestLanguage.Rule);
        var inner = model.NewChild(outer, "extends", TestLanguage.Rule);

        Assert.Throws<ModelInvariantException>(() => outer.SetReference("inherits", outer));
        Assert.Throws<ModelInvariantException>(() => outer.SetReference("inherits", inner));

        outer.SetReference("self", inner);
        Assert.Same(inner, outer.GetReferenceTarget("self"));
    }

    [Fact]
    public void AReferenceMayPointAtAnotherModelOfTheSameProject()
    {
        // A project holds several documents, and the scope provider already walks up to the project to answer what a
        // name may mean - so what it offers has to be linkable, or the list would name nodes no reference can hold.
        var project = TestLanguage.NewProject();
        var here = project.NewModel("here");
        var there = project.NewModel("there");

        var rule = here.NewNode(TestLanguage.Rule);
        var tag = there.NewNode(TestLanguage.Tag);

        rule.SetReference("tag", tag);

        Assert.Same(tag, rule.GetReferenceTarget("tag"));
    }

    [Fact]
    public void AReferenceRejectsATargetOfAnotherProject()
    {
        var rule = TestLanguage.NewModel().NewNode(TestLanguage.Rule);
        var stranger = TestLanguage.NewModel().NewNode(TestLanguage.Tag);

        var exception = Assert.Throws<ModelInvariantException>(() => rule.SetReference("tag", stranger));

        Assert.Contains("another project", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DeletingATargetClearsAReferenceFromAnotherModel()
    {
        var project = TestLanguage.NewProject();
        var here = project.NewModel("here");
        var there = project.NewModel("there");

        var rule = here.NewNode(TestLanguage.Rule);
        var tag = there.NewNode(TestLanguage.Tag);
        rule.SetReference("tag", tag);

        // The clearing walks the project, not the model, which is why a cross-model reference cannot go stale when
        // its target is deleted from the other document.
        there.DeleteSubtree(tag);

        Assert.Null(rule.GetReference("tag"));
    }

    [Fact]
    public void ClearingAReferenceReleasesTheTarget()
    {
        var model = TestLanguage.NewModel();
        var rule = model.NewNode(TestLanguage.Rule);
        var tag = model.NewNode(TestLanguage.Tag);
        rule.SetReference("tag", tag);

        rule.SetReference("tag", null);

        Assert.Null(rule.GetReference("tag"));
        Assert.Null(rule.GetReferenceTarget("tag"));
    }

    [Fact]
    public void AReferenceToAnIdOutsideTheProjectIsKeptButDoesNotResolve()
    {
        var model = TestLanguage.NewModel();
        var rule = model.NewNode(TestLanguage.Rule);
        var missing = NodeId.New();

        rule.SetReference("tag", missing, "deleted elsewhere");

        var reference = rule.GetReference("tag");
        Assert.NotNull(reference);
        Assert.Equal(missing, reference.Value.Target);
        Assert.Equal("deleted elsewhere", reference.Value.ResolveInfo);
        Assert.Null(rule.GetReferenceTarget("tag"));
        Assert.False(reference.Value.IsResolvedIn(model.Project));
    }

    [Fact]
    public void WritingTheSameReferenceAgainIsNotAMutation()
    {
        var model = TestLanguage.NewModel();
        var rule = model.NewNode(TestLanguage.Rule);
        var tag = model.NewNode(TestLanguage.Tag);
        rule.SetReference("tag", tag);
        var version = model.Version;

        rule.SetReference("tag", tag.Id, tag.Name);

        Assert.Equal(version, model.Version);
    }

    [Fact]
    public void AnEmptyReferenceTargetIsRejected()
    {
        var rule = TestLanguage.NewModel().NewNode(TestLanguage.Rule);

        Assert.Throws<ArgumentException>(() => rule.SetReference("tag", default(NodeId)));
    }
}

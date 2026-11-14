using Xunit;

namespace Projectional.Core.Tests;

public sealed class ModelTests
{
    [Fact]
    public void VersionStartsAtZeroAndCountsMutations()
    {
        var model = TestLanguage.NewModel();
        Assert.Equal(0, model.Version);

        var node = model.NewNode(TestLanguage.Step);
        Assert.Equal(1, model.Version);

        node.Set("index", 1);
        Assert.Equal(2, model.Version);
    }

    [Fact]
    public void ACreatedChildIsOwnedByItsParentAndRegisteredInTheProject()
    {
        var model = TestLanguage.NewModel();
        var rule = model.NewNode(TestLanguage.Rule);

        var body = model.NewChild(rule, "body", TestLanguage.Body);

        Assert.Same(rule, body.Parent);
        Assert.Same(body, model.Project.FindNode(body.Id));
        Assert.Equal(2, model.Project.NodeCount);
        Assert.Equal([rule], model.Roots);
    }

    [Fact]
    public void AFailedNewChildLeavesNoOrphanBehind()
    {
        var model = TestLanguage.NewModel();
        var rule = model.NewNode(TestLanguage.Rule);

        Assert.Throws<ModelInvariantException>(() => model.NewChild(rule, "body", TestLanguage.Step));

        Assert.Equal(1, model.Project.NodeCount);
        Assert.Equal([rule], model.Roots);
    }

    [Fact]
    public void DeletingASubtreeRemovesEveryNodeBelowIt()
    {
        var model = TestLanguage.NewModel();
        var root = model.NewNode(TestLanguage.Rule);
        var nested = model.NewChild(root, "extends", TestLanguage.Rule);
        var body = model.NewChild(nested, "body", TestLanguage.Body);
        var step = model.NewChild(body, "steps", TestLanguage.Step);
        var other = model.NewNode(TestLanguage.Rule);

        root.RemoveChild("extends", nested);

        Assert.Null(model.Project.FindNode(nested.Id));
        Assert.Null(model.Project.FindNode(body.Id));
        Assert.Null(model.Project.FindNode(step.Id));
        Assert.Equal(2, model.Project.NodeCount);
        Assert.Equal([root, other], model.Roots);
    }

    [Fact]
    public void DeletingASubtreeClearsEveryReferenceIntoIt()
    {
        var model = TestLanguage.NewModel();
        var root = model.NewNode(TestLanguage.Rule);
        var nested = model.NewChild(root, "extends", TestLanguage.Rule);
        var other = model.NewNode(TestLanguage.Rule);
        other.SetReference("self", nested);

        root.RemoveChild("extends", nested);

        Assert.Null(other.GetReference("self"));
        Assert.NotNull(model.Project.FindNode(other.Id));
    }

    [Fact]
    public void RemovingAChildThatIsNoChildOfThatSlotIsRejected()
    {
        var model = TestLanguage.NewModel();
        var body = model.NewNode(TestLanguage.Body);
        var step = model.NewChild(body, "steps", TestLanguage.Step);
        var other = model.NewNode(TestLanguage.Body);

        Assert.Throws<ModelInvariantException>(() => other.RemoveChild("steps", step));
        Assert.NotNull(model.Project.FindNode(step.Id));
    }

    [Fact]
    public void MovingARootIntoAnotherRootRenumbersTheRemainingRoots()
    {
        var model = TestLanguage.NewModel();
        var first = model.NewNode(TestLanguage.Rule);
        var second = model.NewNode(TestLanguage.Rule);
        var third = model.NewNode(TestLanguage.Rule);

        first.AddChild("extends", second);

        Assert.Equal([first, third], model.Roots);
        Assert.Equal(0, first.IndexInParent);
        Assert.Equal(1, third.IndexInParent);
        Assert.Equal(0, second.IndexInParent);
        Assert.False(second.IsRoot);
    }

    [Fact]
    public void ANodeOfAnotherModelCannotBecomeARoot()
    {
        var project = TestLanguage.NewProject();
        var first = project.NewModel("a");
        var second = project.NewModel("b");
        var node = first.NewNode(TestLanguage.Tag);

        Assert.Throws<ModelInvariantException>(() => second.AddRoot(node));
    }

    [Fact]
    public void AChildOfAnotherModelCannotBeInserted()
    {
        var project = TestLanguage.NewProject();
        var first = project.NewModel("a");
        var second = project.NewModel("b");
        var rule = first.NewNode(TestLanguage.Rule);
        var foreign = second.NewNode(TestLanguage.Body);

        Assert.Throws<ModelInvariantException>(() => rule.AddChild("body", foreign));
    }

    [Fact]
    public void AddRootKeepsAnExistingRootWhereItIs()
    {
        var model = TestLanguage.NewModel();
        var node = model.NewNode(TestLanguage.Tag);
        var version = model.Version;

        model.AddRoot(node);

        Assert.Equal([node], model.Roots);
        Assert.Equal(version, model.Version);
    }

    [Fact]
    public void AddRootPromotesAChildBackToARoot()
    {
        var model = TestLanguage.NewModel();
        var body = model.NewNode(TestLanguage.Body);
        var step = model.NewChild(body, "steps", TestLanguage.Step);

        model.AddRoot(step);

        Assert.Empty(body.GetChildren("steps"));
        Assert.True(step.IsRoot);
        Assert.Equal([body, step], model.Roots);
        Assert.Equal(1, step.IndexInParent);
    }

    [Fact]
    public void RenamingKeepsTheId()
    {
        var model = TestLanguage.NewModel();
        var id = model.Id;

        model.Rename("Renamed");

        Assert.Equal("Renamed", model.Name);
        Assert.Equal(id, model.Id);
    }

    [Fact]
    public void AModelNeedsANonEmptyId()
    {
        var project = TestLanguage.NewProject();

        Assert.Throws<ArgumentException>(() => project.NewModel(ModelId.Empty, "broken"));
        Assert.Empty(project.Models);
    }

    [Fact]
    public void AModelRejectsAConceptOfAForeignLanguage()
    {
        var model = TestLanguage.NewModel();
        var foreign = ConceptRegistry.Create([new ConceptDeclaration("Foreign.Thing")]);

        Assert.Throws<ModelInvariantException>(() => model.NewNode(foreign.Get("Foreign.Thing")));
    }

    [Fact]
    public void AnUnknownConceptKeyIsRejected()
    {
        var model = TestLanguage.NewModel();

        Assert.Throws<KeyNotFoundException>(() => model.NewNode("Test.Nope"));
    }
}

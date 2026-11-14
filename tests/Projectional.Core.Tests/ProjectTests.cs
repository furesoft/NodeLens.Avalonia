using Xunit;

namespace Projectional.Core.Tests;

public sealed class ProjectTests
{
    [Fact]
    public void ANewProjectHasItsLanguageButNoModels()
    {
        var project = TestLanguage.NewProject();

        Assert.Same(TestLanguage.Registry, project.Concepts);
        Assert.Empty(project.Models);
        Assert.Empty(project.Nodes);
        Assert.Equal(0, project.NodeCount);
    }

    [Fact]
    public void AModelNeedsAProject()
    {
        Assert.Throws<ArgumentNullException>(() => new Project(null!));
    }

    [Fact]
    public void ModelsAreListedInTheOrderTheyWereOpened()
    {
        var project = TestLanguage.NewProject();

        var first = project.NewModel("a");
        var second = project.NewModel("b");

        Assert.Equal([first, second], project.Models);
        Assert.False(first.Id.IsEmpty);
        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public void AModelMayBeUnnamed()
    {
        var model = TestLanguage.NewProject().NewModel();

        Assert.Null(model.Name);
        Assert.False(model.Id.IsEmpty);
    }

    [Fact]
    public void EveryLiveNodeIsIndexedAcrossModels()
    {
        var project = TestLanguage.NewProject();
        var first = project.NewModel("a");
        var second = project.NewModel("b");
        var a = first.NewNode(TestLanguage.Tag);
        var b = second.NewNode(TestLanguage.Tag);

        Assert.Equal(2, project.NodeCount);
        Assert.Contains(a, project.Nodes);
        Assert.Contains(b, project.Nodes);
        Assert.Same(b, project.FindNode(b.Id));
    }

    [Fact]
    public void AnUnknownIdDoesNotResolve()
    {
        Assert.Null(TestLanguage.NewProject().FindNode(NodeId.New()));
    }
}

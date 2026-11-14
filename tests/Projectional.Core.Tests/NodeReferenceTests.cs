using Xunit;

namespace Projectional.Core.Tests;

public sealed class NodeReferenceTests
{
    [Fact]
    public void AReferenceRemembersItsTargetAndItsHint()
    {
        var id = NodeId.New();

        var reference = new NodeReference(id, "rule-a");

        Assert.Equal(id, reference.Target);
        Assert.Equal("rule-a", reference.ResolveInfo);
    }

    [Fact]
    public void TwoReferencesWithTheSameTargetAndHintAreEqual()
    {
        var id = NodeId.New();

        Assert.Equal(new NodeReference(id, "a"), new NodeReference(id, "a"));
        Assert.NotEqual(new NodeReference(id, "a"), new NodeReference(id, "b"));
    }

    [Fact]
    public void AReferenceWithAHintRendersBothParts()
    {
        var id = NodeId.Parse("11111111-1111-1111-1111-111111111111");

        Assert.Equal($"rule-a ({id})", new NodeReference(id, "rule-a").ToString());
    }

    [Fact]
    public void AReferenceWithoutAHintRendersOnlyTheTarget()
    {
        var id = NodeId.Parse("11111111-1111-1111-1111-111111111111");
        var reference = new NodeReference(id, null);

        Assert.Equal(id.ToString(), reference.ToString());
    }

    [Fact]
    public void AReferenceResolvesOnlyInsideItsProject()
    {
        var project = TestLanguage.NewProject();
        var model = project.NewModel("a");
        var node = model.NewNode(TestLanguage.Tag);
        var reference = new NodeReference(node.Id, null);

        Assert.True(reference.IsResolvedIn(project));
        Assert.False(reference.IsResolvedIn(TestLanguage.NewProject()));
        Assert.False(new NodeReference(NodeId.New(), null).IsResolvedIn(project));
    }

    [Fact]
    public void ResolutionNeedsAProject()
    {
        Assert.Throws<ArgumentNullException>(() => new NodeReference(NodeId.New(), null).IsResolvedIn(null!));
    }
}

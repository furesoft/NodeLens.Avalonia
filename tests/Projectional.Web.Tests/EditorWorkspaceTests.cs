using Xunit;

namespace Projectional.Web.Tests;

public sealed class EditorWorkspaceTests
{
    [Fact]
    public void ANewDocumentIsInFrontAndClosingItActivatesItsNeighbour()
    {
        using var workspace = new EditorWorkspace(new MemoryDocumentStorage());

        var first = workspace.New(WebLanguages.Rules, sample: true);
        var second = workspace.New(WebLanguages.StateMachine, sample: true);

        Assert.Same(second, workspace.Active);

        Assert.True(workspace.Close(second));
        Assert.Same(first, workspace.Active);

        Assert.True(workspace.Close(first));
        Assert.Null(workspace.Active);
        Assert.Empty(workspace.Documents);
    }

    [Fact]
    public void SavingAndRestoringKeepsTheDocumentsAndTheActiveTab()
    {
        var storage = new MemoryDocumentStorage();
        string id;

        using (var workspace = new EditorWorkspace(storage))
        {
            workspace.New(WebLanguages.Rules, sample: true);
            var second = workspace.New(WebLanguages.SqlSchema, sample: true);

            id = second.Id;
            workspace.Save();

            Assert.False(second.IsDirty);
        }

        using var restored = new EditorWorkspace(storage);
        Assert.Equal(2, restored.Restore());
        Assert.Equal(id, restored.Active?.Id);
        Assert.Equal(2, restored.Documents.Count);
        Assert.All(restored.Documents, document => Assert.False(document.IsDirty));
    }

    [Fact]
    public void AClosedDocumentDoesNotComeBackAfterARestore()
    {
        var storage = new MemoryDocumentStorage();

        using (var workspace = new EditorWorkspace(storage))
        {
            workspace.New(WebLanguages.Rules, sample: true);
            var second = workspace.New(WebLanguages.Rules, sample: true);

            workspace.Close(second);
            workspace.Save();
        }

        using var restored = new EditorWorkspace(storage);
        Assert.Equal(1, restored.Restore());
        Assert.Equal("Rules example", restored.Active?.Name);
    }

    [Fact]
    public void ANameIsMadeUniqueSoTwoTabsNeverShareOne()
    {
        using var workspace = new EditorWorkspace(new MemoryDocumentStorage());

        var first = workspace.New(WebLanguages.Rules, sample: true);
        var second = workspace.New(WebLanguages.Rules, sample: true);

        Assert.NotEqual(first.Name, second.Name);
    }
}

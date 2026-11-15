using Projectional.Core;
using Projectional.Workbench;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// What the tab strip promises when documents are opened, closed and selected.
/// </summary>
public sealed class DocumentSetTests
{
    [Fact]
    public void OpeningADocumentAddsATabAndSelectsIt()
    {
        using var documents = new DocumentSet();
        using var tab = OpenTab();

        documents.Add(tab);

        Assert.Equal(1, documents.Count);
        Assert.Same(tab, documents.Active);
        Assert.Same(tab, documents.Find(tab.Model));
        Assert.True(documents.IsOpen(tab.Model));
    }

    [Fact]
    public void TheSameModelCannotBeOpenedTwice()
    {
        using var documents = new DocumentSet();
        using var tab = OpenTab();
        documents.Add(tab);

        using var second = new DocumentTab(tab.Model, TestNotations.Registry);

        Assert.Throws<InvalidOperationException>(() => documents.Add(second));
        Assert.Equal(1, documents.Count);
    }

    [Fact]
    public void ClosingTheActiveDocumentSelectsTheTabThatTookItsPlace()
    {
        using var documents = new DocumentSet();
        var first = OpenTab();
        var second = OpenTab();
        var third = OpenTab();
        documents.Add(first);
        documents.Add(second);
        documents.Add(third);

        documents.Activate(second);
        documents.Remove(second);

        Assert.Equal(2, documents.Count);
        Assert.Same(third, documents.Active);
    }

    [Fact]
    public void ClosingTheLastTabSelectsTheOneBeforeIt()
    {
        using var documents = new DocumentSet();
        var first = OpenTab();
        var last = OpenTab();
        documents.Add(first);
        documents.Add(last);

        documents.Remove(last);

        Assert.Same(first, documents.Active);
    }

    [Fact]
    public void ClosingTheOnlyDocumentLeavesNothingActive()
    {
        using var documents = new DocumentSet();
        var tab = OpenTab();
        documents.Add(tab);

        Assert.True(documents.Remove(tab));

        Assert.Equal(0, documents.Count);
        Assert.Null(documents.Active);
    }

    [Fact]
    public void ClosingSomethingThatIsNotOpenReportsThatNothingHappened()
    {
        using var documents = new DocumentSet();

        using var tab = OpenTab();

        Assert.False(documents.Remove(tab));
    }

    [Fact]
    public void UnsavedDocumentsAreReportedInTabOrder()
    {
        using var documents = new DocumentSet();
        var clean = OpenTab();
        var dirty = OpenTab();
        documents.Add(clean);
        documents.Add(dirty);

        Assert.False(documents.AnyUnsaved);

        dirty.Root.Set("name", "Renamed");

        Assert.True(documents.AnyUnsaved);
        Assert.Equal([dirty], documents.Unsaved);
    }

    [Fact]
    public void OnlyAnOpenDocumentCanBeActive()
    {
        using var documents = new DocumentSet();
        documents.Add(OpenTab());

        using var stranger = OpenTab();

        Assert.Throws<ArgumentException>(() => documents.Activate(stranger));
    }

    [Fact]
    public void DisposingTheSetClosesEveryDocument()
    {
        var documents = new DocumentSet();
        var first = OpenTab();
        var second = OpenTab();
        documents.Add(first);
        documents.Add(second);

        documents.Dispose();
        first.Root.Set("name", "Renamed");
        second.Root.Set("name", "Renamed");

        Assert.Equal(0, documents.Count);
        Assert.Null(documents.Active);
        Assert.Equal(0, first.Session.History.UndoDepth);
        Assert.Equal(0, second.Session.History.UndoDepth);
    }

    private static DocumentTab OpenTab()
    {
        var model = TestLanguage.NewModel();
        var rule = model.NewNode(TestLanguage.Rule);
        model.AddRoot(rule);

        return new DocumentTab(model, TestNotations.Registry);
    }
}

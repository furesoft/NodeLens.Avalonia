using Projectional.Core;
using Projectional.Projection;
using Projectional.Workbench;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// What a tab promises about the document it holds: where it lives, what it is called, and whether it
/// still matches its file.
/// </summary>
public sealed class DocumentTabTests
{
    [Fact]
    public void AFreshTabIsCleanAndCarriesTheModelName()
    {
        var model = ModelWithRule();
        using var tab = OpenTab(model);

        Assert.False(tab.IsDirty);
        Assert.Equal(model.Name, tab.Title);
        Assert.Null(tab.FilePath);
        Assert.Same(model, tab.Model);
        Assert.Same(model.Roots[0], tab.Root);
    }

    [Fact]
    public void ATabWithoutAModelNameStillHasATitle()
    {
        var model = ModelWithRule();
        model.Rename(null);

        using var tab = OpenTab(model);

        Assert.False(string.IsNullOrWhiteSpace(tab.Title));
    }

    [Fact]
    public void AnEditMakesTheTabDirty()
    {
        using var tab = OpenTab(ModelWithRule());

        tab.Root.Set("name", "Renamed");

        Assert.True(tab.IsDirty);
    }

    [Fact]
    public void SavingMakesTheTabCleanAgain()
    {
        using var tab = OpenTab(ModelWithRule());
        tab.Root.Set("name", "Renamed");

        tab.MarkSaved(@"C:\models\test.mps");

        Assert.False(tab.IsDirty);
        Assert.Equal(@"C:\models\test.mps", tab.FilePath);
    }

    [Fact]
    public void SavingWithoutAPathKeepsTheOneItAlreadyHad()
    {
        using var tab = OpenTab(ModelWithRule());
        tab.MarkSaved(@"C:\models\test.mps");

        tab.Root.Set("name", "Renamed");
        tab.MarkSaved();

        Assert.False(tab.IsDirty);
        Assert.Equal(@"C:\models\test.mps", tab.FilePath);
    }

    [Fact]
    public void UndoingBackToTheSavedStateIsClean()
    {
        using var tab = OpenTab(ModelWithRule());
        tab.Root.Set("name", "Renamed");
        tab.MarkSaved();

        // Undoing the edit that came after the save is what returns to the file, not undoing the
        // edit the file already contains.
        tab.Root.Set("name", "Renamed again");
        tab.Session.Undo();

        Assert.False(tab.IsDirty);
    }

    [Fact]
    public void RedoingAfterThatIsDirtyAgain()
    {
        using var tab = OpenTab(ModelWithRule());
        tab.Root.Set("name", "Renamed");
        tab.MarkSaved();
        tab.Root.Set("name", "Renamed again");
        tab.Session.Undo();

        tab.Session.Redo();

        Assert.True(tab.IsDirty);
    }

    [Fact]
    public void AnEditThatJoinsTheSavedStepStillCountsAsAChange()
    {
        using var tab = OpenTab(ModelWithRule());

        tab.Session.History.CoalescingKey = "typing";
        tab.Root.Set("name", "R");
        tab.MarkSaved();

        // The save cleared the key, but the step it wrote keeps its own, so a caller that starts the
        // burst again lands back in the step that is already on disk. The document is still modified,
        // because the keystroke after the save is content the file never saw.
        tab.Session.History.CoalescingKey = "typing";
        tab.Root.Set("name", "Ru");

        Assert.Equal(1, tab.Session.History.UndoDepth);
        Assert.True(tab.IsDirty);
    }

    [Fact]
    public void AModelWithoutARootCannotBeOpened()
    {
        var model = TestLanguage.NewModel();

        Assert.Throws<InvalidOperationException>(() => new DocumentTab(model, TestNotations.Registry, MonospaceTextMeasure.Instance));
    }

    [Fact]
    public void DisposingTheTabStopsItsSessionFromRecording()
    {
        var tab = OpenTab(ModelWithRule());
        var root = tab.Root;

        tab.Dispose();
        root.Set("name", "Renamed");

        Assert.Equal(0, tab.Session.History.UndoDepth);
    }

    private static Model ModelWithRule()
    {
        var model = TestLanguage.NewModel();
        var rule = model.NewNode(TestLanguage.Rule);
        model.AddRoot(rule);

        return model;
    }

    private static DocumentTab OpenTab(Model model) => new(model, TestNotations.Registry, MonospaceTextMeasure.Instance);
}

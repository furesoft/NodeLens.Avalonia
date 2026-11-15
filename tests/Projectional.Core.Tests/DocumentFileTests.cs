using Projectional.Core;
using Projectional.Projection;
using Projectional.Samples;
using Projectional.Workbench;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// What the shell promises when a model leaves the window and comes back: the file holds the model,
/// and the tab's idea of "saved" only moves when the disk agreed.
/// </summary>
public sealed class DocumentFileTests
{
    [Fact]
    public void ASavedModelCanBeReadBack()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.Combine($"payroll{DocumentFile.Extension}");

        var model = PayrollSample.NewSampleModel();
        model.Rename("payroll");
        using var tab = OpenTab(model);

        Assert.True(DocumentFile.TrySave(tab, path, out var saveError), saveError);
        Assert.True(File.Exists(path));

        var loaded = DocumentFile.TryLoad(path, out var loadError);

        Assert.Null(loadError);
        Assert.NotNull(loaded);
        Assert.Equal(model.Name, loaded.Name);
        Assert.Equal(model.Roots.Count, loaded.Roots.Count);
        Assert.Equal(model.Roots[0].Concept.Key, loaded.Roots[0].Concept.Key);
    }

    [Fact]
    public void SavingRecordsThePathAndMakesTheTabClean()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.Combine($"payroll{DocumentFile.Extension}");

        using var tab = OpenTab(PayrollSample.NewSampleModel());
        tab.Root.Set("name", "Renamed");
        Assert.True(tab.IsDirty);

        Assert.True(DocumentFile.TrySave(tab, path, out _));

        Assert.False(tab.IsDirty);
        Assert.Equal(path, tab.FilePath);
    }

    [Fact]
    public void AFailedSaveLeavesTheDocumentDirty()
    {
        using var directory = new TemporaryDirectory();

        // A directory is not a file this app can write, so the write fails after the tab has already
        // been told it is about to be saved.
        var path = directory.Combine("a-directory");
        Directory.CreateDirectory(path);

        using var tab = OpenTab(PayrollSample.NewSampleModel());
        tab.Root.Set("name", "Renamed");

        Assert.False(DocumentFile.TrySave(tab, path, out var error));

        Assert.False(string.IsNullOrWhiteSpace(error));
        Assert.True(tab.IsDirty);
        Assert.Null(tab.FilePath);
    }

    [Fact]
    public void AFileThatIsNotAModelIsReportedRatherThanThrown()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.Combine($"broken{DocumentFile.Extension}");
        File.WriteAllText(path, "this is not a model");

        var model = DocumentFile.TryLoad(path, out var error);

        Assert.Null(model);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void AMissingFileIsReportedRatherThanThrown()
    {
        using var directory = new TemporaryDirectory();

        var model = DocumentFile.TryLoad(directory.Combine("absent.mps"), out var error);

        Assert.Null(model);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    private static DocumentTab OpenTab(Model model) =>
        new(model, RulesLanguage.CreateNotations(model.Concepts), MonospaceTextMeasure.Instance);
}

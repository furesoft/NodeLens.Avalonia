using Projectional.Languages;
using Projectional.Projection;
using Projectional.Workbench;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The project explorer answers "what is open, and what is this language made of" without walking a model, so
/// the rows are a function of the tab strip and the project folders.
/// </summary>
public sealed class ProjectExplorerTests
{
    [Fact]
    public void NoDocumentsAndNoLanguagesIsNoRows()
    {
        Assert.Empty(ProjectExplorer.Of([], []));
    }

    [Fact]
    public void OpenDocumentsAreOneGroupInTabOrder()
    {
        using var first = Tab("payroll");
        using var second = Tab("Release");

        var rows = ProjectExplorer.Of([first, second], []);

        Assert.Equal(["Documents", "payroll", "Release"], rows.Select(row => row.Text));
        Assert.Equal([0, 1, 1], rows.Select(row => row.Depth));
        Assert.Same(first, rows[1].Document);
    }

    [Fact]
    public void AModifiedDocumentCarriesTheSameStarItCarriesEverywhere()
    {
        using var tab = Tab("payroll");

        tab.Root.Set("name", "Renamed");

        var row = Assert.Single(ProjectExplorer.Of([tab], []).Skip(1));

        Assert.EndsWith("*", row.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ALanguageListsItsFoldersAndTheFilesInsideThem()
    {
        var language = Language();

        var rows = ProjectExplorer.Of([], [language]);
        var folders = rows.Where(row => row.Depth == 2).ToList();
        var files = rows.Where(row => row.Depth == 3).ToList();

        Assert.Equal(["Languages", language.Name], rows.Take(2).Select(row => row.Text));
        Assert.Contains(folders, row => row.Text == "structure");
        Assert.Contains(folders, row => row.Text == "editor");
        Assert.NotEmpty(files);
        Assert.All(files, row => Assert.NotNull(row.Path));
        Assert.All(files, row => Assert.DoesNotContain("/", row.Text, StringComparison.Ordinal));

        // Every file sits under the folder it lives in, not under a different one.
        foreach (var folder in folders)
        {
            var index = rows.ToList().IndexOf(folder);

            Assert.Equal(3, rows[index + 1].Depth);
        }
    }

    [Fact]
    public void AFileThatIsOpenCarriesItsTab()
    {
        var language = Language();
        var path = ProjectExplorer.Of([], [language]).First(row => row.Depth == 3).Path!;

        using var tab = Tab("Pipeline", path);

        // Twice on purpose: once under what is open, once under the project it belongs to.
        var rows = ProjectExplorer.Of([tab], [language]);

        Assert.Same(tab, rows.Single(row => row.Depth == 1 && row.Document is not null).Document);

        var row = rows.Single(row => row.Depth == 3 && row.Path == path);

        Assert.Same(tab, row.Document);
        Assert.Equal(Path.GetFileName(path), row.Text);
    }

    private static LanguageProject Language() =>
        LanguageProject.Open(Path.Combine(AppContext.BaseDirectory, "Declared"), out var error)
        ?? throw new InvalidOperationException(error);

    private static DocumentTab Tab(string title, string? path = null)
    {
        var model = TestLanguage.NewModel();
        var rule = model.NewNode(TestLanguage.Rule);
        model.AddRoot(rule);
        model.Rename(title);

        return new DocumentTab(model, TestNotations.Registry, MonospaceTextMeasure.Instance, path);
    }
}

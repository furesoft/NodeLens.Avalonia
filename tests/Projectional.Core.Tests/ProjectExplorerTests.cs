using Projectional.Languages;
using Projectional.Projection;
using Projectional.Workbench;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The project explorer answers "what is open, and what is this language made of" without walking a model, so the
/// rows are a function of the tab strip, the project folders and the set of nodes drawn shut.
/// </summary>
public sealed class ProjectExplorerTests
{
    private static readonly HashSet<string> NothingCollapsed = [];

    [Fact]
    public void NoDocumentsAndNoLanguagesIsNoRows()
    {
        Assert.Empty(ProjectExplorer.Of([], [], NothingCollapsed));
    }

    [Fact]
    public void OpenDocumentsAreOneGroupInTabOrder()
    {
        using var first = Tab("payroll");
        using var second = Tab("Release");

        var rows = ProjectExplorer.Of([first, second], [], NothingCollapsed);

        Assert.Equal(["Documents", "payroll", "Release"], rows.Select(row => row.Text));
        Assert.Equal([0, 1, 1], rows.Select(row => row.Depth));
        Assert.Same(first, rows[1].Document);
        Assert.Equal(ProjectNode.Document, rows[1].Kind);
    }

    [Fact]
    public void AModifiedDocumentCarriesItsStar()
    {
        using var tab = Tab("payroll");

        tab.Root.Set("name", "Renamed");

        var row = Assert.Single(ProjectExplorer.Of([tab], [], NothingCollapsed).Skip(1));

        Assert.True(row.IsDirty);
    }

    [Fact]
    public void ALanguageListsItsFoldersAndTheFilesInsideThem()
    {
        var language = Language();

        var rows = ProjectExplorer.Of([], [language], NothingCollapsed);
        var folders = rows.Where(row => row.Kind == ProjectNode.Folder).ToList();
        var files = rows.Where(row => row.Kind == ProjectNode.File).ToList();

        Assert.Equal(["Languages", language.Name], rows.Take(2).Select(row => row.Text));
        Assert.Contains(folders, row => row.Text == "structure" && row.HasChildren);
        Assert.Contains(folders, row => row.Text == "editor");
        Assert.NotEmpty(files);
        Assert.All(files, row => Assert.NotNull(row.Path));
        Assert.All(files, row => Assert.False(row.HasChildren));
        Assert.All(files, row => Assert.DoesNotContain("/", row.Text, StringComparison.Ordinal));
    }

    [Fact]
    public void ACollapsedFolderIsOneRowAndItsFilesAreNotBuilt()
    {
        var language = Language();
        var rows = ProjectExplorer.Of([], [language], NothingCollapsed);
        var structure = rows.Single(row => row.Kind == ProjectNode.Folder && row.Text == "structure");

        var collapsed = new HashSet<string> { structure.Key! };
        var after = ProjectExplorer.Of([], [language], collapsed);

        Assert.False(after.Single(row => row.Key == structure.Key).IsExpanded);
        Assert.DoesNotContain(after, row => row.Depth == 3 && row.Path?.Contains("structure", StringComparison.Ordinal) == true);
    }

    [Fact]
    public void ACollapsedHeadingDropsEverythingUnderIt()
    {
        using var tab = Tab("payroll");
        var language = Language();

        var collapsed = new HashSet<string> { ProjectExplorer.DocumentsKey, ProjectExplorer.LanguagesKey };
        var rows = ProjectExplorer.Of([tab], [language], collapsed);

        Assert.Equal(["Documents", "Languages"], rows.Select(row => row.Text));
        Assert.All(rows, row => Assert.False(row.IsExpanded));
    }

    [Fact]
    public void AFileThatIsOpenCarriesItsTab()
    {
        var language = Language();
        var path = ProjectExplorer.Of([], [language], NothingCollapsed).First(row => row.Kind == ProjectNode.File).Path!;

        using var tab = Tab("Pipeline", path);

        // Twice on purpose: once under what is open, once under the project it belongs to.
        var rows = ProjectExplorer.Of([tab], [language], NothingCollapsed);

        Assert.Same(tab, rows.Single(row => row.Depth == 1 && row.Document is not null).Document);

        var row = rows.Single(row => row.Kind == ProjectNode.File && row.Path == path);

        Assert.Same(tab, row.Document);
        Assert.Equal(Path.GetFileName(path), row.Text);
    }

    [Fact]
    public void OnlyRealFoldersAndDocumentsAreLeavesAndTheRestCanBeOpened()
    {
        var language = Language();

        var rows = ProjectExplorer.Of([], [language], NothingCollapsed);

        Assert.True(rows[0].HasChildren);                                    // the heading
        Assert.True(rows.Single(row => row.Kind == ProjectNode.Project).HasChildren);
        Assert.All(rows.Where(row => row.Kind == ProjectNode.Folder), row => Assert.True(row.HasChildren));
        Assert.All(rows.Where(row => row.Kind == ProjectNode.File), row => Assert.False(row.HasChildren));
        Assert.All(rows.Where(row => row.Kind == ProjectNode.Folder), row => Assert.True(row.IsFolder));
        Assert.All(rows.Where(row => row.Kind == ProjectNode.Project), row => Assert.True(row.IsProject));
    }

    [Fact]
    public void ARowIsDrawnAsWhatItIsAndInTheColourOfItsAspect()
    {
        var language = Language();

        var rows = ProjectExplorer.Of([], [language], NothingCollapsed);
        var heading = rows[0];
        var project = rows.Single(row => row.Kind == ProjectNode.Project);
        var folder = rows.Single(row => row.Kind == ProjectNode.Folder && row.Text == "structure");
        var file = rows.First(row => row.Kind == ProjectNode.File && row.Folder == "structure");

        // A heading leads and needs no mark; a project is a mark of its own and is drawn heavier than its rows.
        Assert.Null(heading.Icon);
        Assert.False(heading.HasIcon);
        Assert.True(heading.IsBold);
        Assert.Equal(ProjectIcons.ProjectKey, project.Icon);
        Assert.True(project.IsBold);

        // A folder is drawn with the mark of its aspect, a file with the letter of what that aspect holds.
        Assert.Equal("structure", folder.Icon);
        Assert.True(folder.HasIcon);
        Assert.False(folder.HasBadge);
        Assert.Equal("C", file.Badge);
        Assert.True(file.HasBadge);
        Assert.Equal("structure", file.Icon);
        Assert.False(file.IsBold);
    }

    [Fact]
    public void AnAspectWhoseLetterIsUnknownIsDrawnAsAPlainPage()
    {
        var row = new ProjectEntry("notes.mps", 3, ProjectNode.File, path: "notes.mps", folder: "diagrams");

        // The mark is still the aspect's, so a folder nobody taught the shell about is not silently a concept.
        Assert.Equal("diagrams", row.Icon);
        Assert.Null(row.Badge);
        Assert.True(row.HasIcon);
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
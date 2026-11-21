using Projectional.Core;
using Projectional.Editors;
using Projectional.Editing;
using Projectional.Language;
using Projectional.Languages;
using Projectional.Persistence;
using Projectional.Projection;
using Projectional.Structures;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// A language that lives in a folder: <c>structure.mps</c> and <c>editor.mps</c> become a registry and a notation
/// registry, which is everything a session needs.
/// </summary>
/// <remarks>
/// This is M2's gate in its smallest form - the language is defined entirely in project files, and a document of it
/// is then created, drawn, saved and read back. Nothing in the test writes C# for the DSL it exercises; the only C#
/// is the language that defines languages.
/// </remarks>
public sealed class LanguageProjectTests
{
    [Fact]
    public void AProjectFolderBecomesAUsableLanguage()
    {
        using var folder = new TemporaryDirectory();
        var project = folder.Root;

        WriteStructure(project);
        WriteNotation(project);

        var language = LanguageProject.Open(project, out var error);

        Assert.Null(error);
        Assert.NotNull(language);

        Assert.Equal("Rules.Rule", language.Declarations[0].Key);
        Assert.True(language.HasNotation);

        // The registry is a real one: a document of the declared language can be created and drawn.
        var model = language.NewModel();
        var rule = model.NewNode("Rules.Rule");

        model.AddRoot(rule);
        rule.Set("name", "Approved");

        Assert.Equal("rule Approved", LayoutSnapshot.Render(rule, language.Notations, width: 40));
    }

    [Fact]
    public void ADocumentOfADeclaredLanguageSurvivesARoundTrip()
    {
        using var folder = new TemporaryDirectory();

        WriteStructure(folder.Root);
        WriteNotation(folder.Root);

        var language = LanguageProject.Open(folder.Root, out _)!;
        var model = language.NewModel();
        var rule = model.NewNode("Rules.Rule");

        model.AddRoot(rule);
        rule.Set("name", "Approved");

        var path = Path.Combine(folder.Root, "sample.mps");

        ModelFile.Save(model, path);

        var loaded = ModelFile.Load(language.NewProject(), path);

        Assert.Equal(ModelXml.Serialize(model), ModelXml.Serialize(loaded));
        Assert.Equal("Approved", loaded.Roots[0].GetOrNull("name"));
    }

    [Fact]
    public void AProjectWithoutAStructureFolderIsRefused()
    {
        using var folder = new TemporaryDirectory();

        var language = LanguageProject.Open(folder.Root, out var error);

        Assert.Null(language);
        Assert.Contains("structure", error!, StringComparison.Ordinal);
    }

    [Fact]
    public void AProjectInTheOldSingleFileShapeSaysHowToSplitIt()
    {
        // The file that used to be the answer is still there, so the message names it and says what to do. "Not a
        // language project" would send its author looking for a different problem.
        using var folder = new TemporaryDirectory();

        WriteStructure(folder.Root);
        File.Move(
            Path.Combine(folder.Root, LanguageProject.StructureFolder, "Rules.Rule.mps"),
            Path.Combine(folder.Root, "structure.mps"));
        Directory.Delete(Path.Combine(folder.Root, LanguageProject.StructureFolder));

        var language = LanguageProject.Open(folder.Root, out var error);

        Assert.Null(language);
        Assert.Contains("structure.mps", error!, StringComparison.Ordinal);
        Assert.Contains("one definition per document", error!, StringComparison.Ordinal);
    }

    [Fact]
    public void ADocumentHoldingTwoDefinitionsIsRefused()
    {
        // One root per document is the shape being enforced: two definitions in one file is what the layout exists
        // to remove, and silently taking the first would hide the second.
        using var folder = new TemporaryDirectory();
        var structure = Path.Combine(folder.Root, LanguageProject.StructureFolder);

        Directory.CreateDirectory(structure);

        var model = StructureLanguage.NewModel("Two");

        model.AddRoot(model.NewNode(StructureConceptKeys.Concept));
        model.AddRoot(model.NewNode(StructureConceptKeys.Concept));
        ModelFile.Save(model, Path.Combine(structure, "Two.mps"));

        var language = LanguageProject.Open(folder.Root, out var error);

        Assert.Null(language);
        Assert.Contains("Two.mps", error!, StringComparison.Ordinal);
    }

    [Fact]
    public void ALanguageWithoutANotationStillLoads()
    {
        // A language being written passes through this: the structure is there, the notation is not. Refusing to
        // load would make "write the structure first" impossible.
        using var folder = new TemporaryDirectory();

        WriteStructure(folder.Root);

        var language = LanguageProject.Open(folder.Root, out var error);

        Assert.Null(error);
        Assert.NotNull(language);
        Assert.False(language.HasNotation);

        var model = language.NewModel();
        var rule = model.NewNode("Rules.Rule");

        model.AddRoot(rule);

        // Every node draws as a missing notation rather than throwing at the projection.
        Assert.Contains("Rules.Rule", LayoutSnapshot.Render(rule, language.Notations, width: 40), StringComparison.Ordinal);
    }

    [Fact]
    public void ABrokenDefinitionIsAMessageAndNotAnException()
    {
        using var folder = new TemporaryDirectory();

        WriteStructure(folder.Root, typeName: "nonsense");

        var language = LanguageProject.Open(folder.Root, out var error);

        Assert.Null(language);
        Assert.Contains("nonsense", error!, StringComparison.Ordinal);
    }

    [Fact]
    public void ANotationForAnUndeclaredConceptIsRefused()
    {
        // The earliest point where both halves exist is the right place to notice: a notation is lowered against
        // the concepts the structure model just declared.
        using var folder = new TemporaryDirectory();

        WriteStructure(folder.Root);
        WriteNotationFor(folder.Root, "Rules.Missing");

        var language = LanguageProject.Open(folder.Root, out var error);

        Assert.Null(language);
        Assert.Contains("Rules.Missing", error!, StringComparison.Ordinal);
    }

    [Fact]
    public void TheLanguageIsNamedAfterItsFolder()
    {
        using var folder = new TemporaryDirectory();

        WriteStructure(folder.Root);

        Assert.Equal(
            Path.GetFileName(folder.Root),
            LanguageProject.Open(folder.Root, out _)!.Name);
    }

    [Fact]
    public void ALanguageRootNamesTheLanguageAndSaysWhatItStandsOn()
    {
        // `language.mps` is the one document beside the aspect folders: it says what the project is, while every
        // other document says what a part of it is.
        using var folder = new TemporaryDirectory();

        WriteStructure(folder.Root);
        WriteRoot(folder.Root, "Pipeline", version: "1.2.0", dependencies: ["Core"], extends: ["Base"]);

        var language = LanguageProject.Open(folder.Root, out var error);

        Assert.Null(error);
        Assert.NotNull(language);
        Assert.Equal("Pipeline", language.Name);
        Assert.Equal("1.2.0", language.Version);
        Assert.Equal([new LanguageDependency("Core", null)], language.Dependencies);
        Assert.Equal([new LanguageDependency("Base", null)], language.Extends);

        // The definitions are still read: a root describes the project, it does not replace the structure.
        Assert.Equal("Rules.Rule", language.Declarations[0].Key);
    }

    [Fact]
    public void ALanguageRootWithoutAVersionLeavesItUnsaid()
    {
        using var folder = new TemporaryDirectory();

        WriteStructure(folder.Root);
        WriteRoot(folder.Root, "Rules");

        var language = LanguageProject.Open(folder.Root, out _)!;

        Assert.Null(language.Version);
        Assert.Empty(language.Dependencies);
        Assert.Empty(language.Extends);
    }

    [Fact]
    public void ALanguageRootWithTwoRootsIsRefused()
    {
        // One root per document holds for the language root too: two of them are two languages in one file, and
        // taking the first would hide the second.
        using var folder = new TemporaryDirectory();
        var model = StructureLanguage.NewModel("language");
        var first = model.NewNode(StructureConceptKeys.Language);
        var second = model.NewNode(StructureConceptKeys.Language);

        // `NewNode` already makes each node a root, which is what gives this file the two it must refuse.
        first.Set("name", "First");
        second.Set("name", "Second");

        WriteStructure(folder.Root);
        ModelFile.Save(model, Path.Combine(folder.Root, LanguageProject.LanguageFile));

        var language = LanguageProject.Open(folder.Root, out var error);

        Assert.Null(language);
        Assert.Contains(LanguageProject.LanguageFile, error!, StringComparison.Ordinal);
        Assert.Contains("2", error!, StringComparison.Ordinal);
    }

    [Fact]
    public void ALanguageRootWithoutANameIsAMessageAndNotAnException()
    {
        using var folder = new TemporaryDirectory();

        WriteStructure(folder.Root);
        WriteRoot(folder.Root, name: " ");

        var language = LanguageProject.Open(folder.Root, out var error);

        Assert.Null(language);
        Assert.Contains("name", error!, StringComparison.Ordinal);
    }

    /// <summary>Writes a <c>language.mps</c> root, the way the app would save one.</summary>
    private static void WriteRoot(
        string directory,
        string name,
        string? version = null,
        IReadOnlyList<string>? dependencies = null,
        IReadOnlyList<string>? extends = null)
    {
        var model = StructureLanguage.NewModel("language");
        var root = model.NewNode(StructureConceptKeys.Language);

        root.Set("name", name);

        if (version is not null)
        {
            root.Set(StructureLanguage.VersionSlot, version);
        }

        foreach (var dependency in dependencies ?? [])
        {
            model.NewChild(root, StructureLanguage.DependenciesSlot, StructureConceptKeys.Dependency).Set("name", dependency);
        }

        foreach (var extension in extends ?? [])
        {
            model.NewChild(root, StructureLanguage.ExtendsSlot, StructureConceptKeys.Dependency).Set("name", extension);
        }

        ModelFile.Save(model, Path.Combine(directory, LanguageProject.LanguageFile));
    }

    /// <summary>Writes a notation for a concept the structure does not declare, into its own editor document.</summary>
    private static void WriteNotationFor(string directory, string conceptKey)
    {
        var notation = new NotationBuilder();

        notation.SetRoot(notation.Notation(conceptKey), EditorConceptKeys.Sequence);

        var editor = Path.Combine(directory, LanguageProject.EditorFolder);

        Directory.CreateDirectory(editor);
        ModelFile.Save(notation.Documents[0], Path.Combine(editor, $"{conceptKey}.mps"));
    }
    private static void WriteStructure(string directory, string typeName = "string")
    {
        var definition = new DefinitionBuilder();

        definition.Concept("Rules.Rule", slots: [definition.Property("name", typeName)], nameSlot: "name");

        var structure = Path.Combine(directory, LanguageProject.StructureFolder);

        Directory.CreateDirectory(structure);
        ModelFile.Save(definition.Documents[0], Path.Combine(structure, "Rules.Rule.mps"));
    }

    /// <summary>Writes the notation of that concept: the keyword <c>rule</c>, a gap, the name.</summary>
    private static void WriteNotation(string directory)
    {
        var notation = new NotationBuilder();
        var declared = notation.Notation("Rules.Rule");
        var line = notation.SetRoot(declared, EditorConceptKeys.Line);

        notation.Word(line, EditorConceptKeys.Keyword, "rule");
        notation.Cell(line, EditorConceptKeys.Space);
        notation.Slot(line, EditorConceptKeys.Property, "name");

        var editor = Path.Combine(directory, LanguageProject.EditorFolder);

        Directory.CreateDirectory(editor);
        ModelFile.Save(notation.Documents[0], Path.Combine(editor, "Rules.Rule.mps"));
    }
}

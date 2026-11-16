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
    public void AProjectWithoutAStructureFileIsRefused()
    {
        using var folder = new TemporaryDirectory();

        var language = LanguageProject.Open(folder.Root, out var error);

        Assert.Null(language);
        Assert.Contains("structure.mps", error!, StringComparison.Ordinal);
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

        var notation = new NotationBuilder();

        notation.SetRoot(notation.Notation("Rules.Missing"), EditorConceptKeys.Sequence);

        ModelFile.Save(notation.Root.Model, Path.Combine(folder.Root, LanguageProject.EditorFile));

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

    /// <summary>Writes a one-concept structure model, optionally with a wrong property type.</summary>
    private static void WriteStructure(string directory, string typeName = "string")
    {
        var definition = new DefinitionBuilder();

        definition.Concept("Rules.Rule", slots: [definition.Property("name", typeName)], nameSlot: "name");

        ModelFile.Save(definition.Root.Model, Path.Combine(directory, LanguageProject.StructureFile));
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

        ModelFile.Save(notation.Root.Model, Path.Combine(directory, LanguageProject.EditorFile));
    }
}

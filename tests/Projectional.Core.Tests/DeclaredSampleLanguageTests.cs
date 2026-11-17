using Projectional.Core;
using Projectional.Editing;
using Projectional.Editors;
using Projectional.Languages;
using Projectional.Persistence;
using Projectional.Projection;
using Projectional.Structures;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The M2 gate: a DSL defined entirely in project files - no C# anywhere - that opens, draws, edits and
/// saves in the same editor as a language written in code.
/// </summary>
/// <remarks>
/// <para>
/// The language is <c>samples/declared-pipeline</c>: <c>structure.mps</c> declares two concepts and
/// <c>editor.mps</c> draws them, one of them as a table. Nothing about it is compiled, so "deleting the
/// language's C# code changes nothing about what the user sees" is not a claim to verify here - there is
/// no C# to delete. What these tests do verify is that the declared path is <em>sufficient</em>: the
/// folder loads, the document draws through the declared notation, a structural edit works, and the
/// result round-trips byte-identically.
/// </para>
/// <para>
/// The folder is copied next to the test assembly by the project file, so the test loads exactly the
/// files a user would open.
/// </para>
/// </remarks>
public sealed class DeclaredSampleLanguageTests
{
    private static LanguageProject Language()
    {
        var language = LanguageProject.Open(Path.Combine(AppContext.BaseDirectory, "Declared"), out var error);

        Assert.Null(error);
        Assert.NotNull(language);

        return language;
    }

    [Fact]
    public void TheFolderLoadsAsALanguageWithBothConcepts()
    {
        var language = Language();

        Assert.True(language.HasNotation);
        Assert.Contains(language.Concepts.Concepts, concept => concept.Key == "Pipeline.Pipeline");
        Assert.Contains(language.Concepts.Concepts, concept => concept.Key == "Pipeline.Stage");
    }

    [Fact]
    public void TheDeclaredNotationDrawsTheDocumentAsATable()
    {
        var language = Language();
        var model = ModelFile.Load(language.NewProject(), Path.Combine(AppContext.BaseDirectory, "Declared", "pipeline.mps"));

        Assert.Equal(
            """
            pipeline Release
              stage run          retries
              build dotnet build 1
              test  dotnet test  0
            """,
            LayoutSnapshot.Render(model, language.Notations, width: 60));
    }

    [Fact]
    public void AStructuralEditWorksOnADeclaredLanguage()
    {
        var language = Language();
        var model = ModelFile.Load(language.NewProject(), Path.Combine(AppContext.BaseDirectory, "Declared", "pipeline.mps"));
        var pipeline = model.Roots[0];

        using var session = new EditorSession(pipeline, language.Notations);

        var result = session.Apply(new InsertSiblingAction(pipeline.GetChildren("stages")[1].Id, SiblingPlace.After));

        Assert.True(result.Changed);
        Assert.Equal(3, pipeline.GetChildren("stages").Count);

        // The new stage is a node of the declared concept, and the table grew a row for it.
        Assert.Equal("Pipeline.Stage", pipeline.GetChildren("stages")[2].Concept.Key);
        Assert.Equal(3, session.Tree.Cells().OfType<TableCell>().Single().Rows.Count);
    }

    [Fact]
    public void TheShellAcceptsTheFolderAndCanStartADocumentOfIt()
    {
        var language = new Workbench.WorkbenchLanguage();
        var opened = LanguageProject.Open(Path.Combine(AppContext.BaseDirectory, "Declared"), out var error);

        Assert.Null(error);
        Assert.NotNull(opened);
        Assert.True(language.TryAdd(opened, out var addError), addError);

        // What File > Open language... does next: a document of the opened language, rooted at the concept
        // that can be a root.
        var model = Workbench.WorkbenchLanguage.NewDocument(opened);

        Assert.NotNull(model);
        Assert.Equal("Pipeline.Pipeline", model.Roots[0].Concept.Key);
    }

    [Fact]
    public void AnEditedDocumentSavesAndLoadsBackByteIdentically()
    {
        var language = Language();
        var model = ModelFile.Load(language.NewProject(), Path.Combine(AppContext.BaseDirectory, "Declared", "pipeline.mps"));
        var pipeline = model.Roots[0];

        using var session = new EditorSession(pipeline, language.Notations);

        session.Apply(new InsertSiblingAction(pipeline.GetChildren("stages")[1].Id, SiblingPlace.After));

        var directory = Directory.CreateTempSubdirectory("declared-sample");

        try
        {
            var path = Path.Combine(directory.FullName, "pipeline.mps");

            ModelFile.Save(model, path);

            var first = File.ReadAllBytes(path);
            var reloaded = ModelFile.Load(language.NewProject(), path);

            ModelFile.Save(reloaded, path);

            Assert.Equal(first, File.ReadAllBytes(path));
            Assert.Equal(3, reloaded.Roots[0].GetChildren("stages").Count);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void EveryDefinitionDocumentSavesBackByteIdentically()
    {
        // The layout is a wire format too: each definition file is one root, and re-saving it must not move a byte.
        // A file that only round-trips *after* an edit would hide a writer that reorders a split document.
        var directory = Path.Combine(AppContext.BaseDirectory, "Declared");
        var temporary = Directory.CreateTempSubdirectory("declared-definitions");

        try
        {
            var structure = Directory.EnumerateFiles(Path.Combine(directory, "structure"), "*.mps").ToList();
            var editor = Directory.EnumerateFiles(Path.Combine(directory, "editor"), "*.mps").ToList();

            Assert.Equal(2, structure.Count);
            Assert.Equal(2, editor.Count);

            foreach (var file in structure)
            {
                AssertRoundTrips(file, new Project(StructureLanguage.Concepts));
            }

            foreach (var file in editor)
            {
                AssertRoundTrips(file, new Project(EditorLanguage.Concepts));
            }
        }
        finally
        {
            temporary.Delete(recursive: true);
        }

        void AssertRoundTrips(string file, Project project)
        {
            var model = ModelFile.Load(project, file);
            var target = Path.Combine(temporary.FullName, Path.GetFileName(file));

            ModelFile.Save(model, target);

            Assert.Equal(File.ReadAllBytes(file), File.ReadAllBytes(target));
        }
    }
}

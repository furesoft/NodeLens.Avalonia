using Projectional.Checks;
using Projectional.Core;
using Projectional.ConstraintRules;
using Projectional.Editing;
using Projectional.Editors;
using Projectional.Languages;
using Projectional.Persistence;
using Projectional.Projection;
using Projectional.Semantics;
using Projectional.Structures;
using Projectional.TextGen;
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
    public void TheFolderNamesItsLanguageInItsRootDocument()
    {
        // `language.mps` beside the aspect folders is what makes the project a noun of its own: without it the name
        // would be whatever the folder is called on this machine.
        var language = Language();

        Assert.Equal("Pipeline", language.Name);
        Assert.Equal("1.0.0", language.Version);
        Assert.Empty(language.Dependencies);
        Assert.Empty(language.Extends);
    }

    [Fact]
    public void BothConceptsAreNamedThroughTheFrameworkInterface()
    {
        // Adoption in the declared path: the structure documents say `implements Framework.Named` instead of
        // nominating a name slot, and the placeholder each of them still declares is what a reference shows.
        var concepts = Language().Concepts;

        foreach (var key in new[] { "Pipeline.Pipeline", "Pipeline.Stage" })
        {
            var concept = concepts.Get(key);

            Assert.True(concept.IsKindOf(FrameworkInterfaces.Named), key);
            Assert.Equal("name", concept.NameSlot?.Name);
            Assert.Equal("unnamed", concept.NameSlot?.DefaultValue);
        }
    }

    [Fact]
    public void TheStructureDocumentsSayHowTheNamesAreWritten()
    {
        // The `named by rule:` cell as data: the language a user opens from a folder asks for PascalCase
        // pipelines and camelCase stages - a convention no C# was written for, which is the point of the cell.
        var concepts = Language().Concepts;

        Assert.Same(NameRule.Pascal, concepts.Get("Pipeline.Pipeline").NameRule);
        Assert.Same(NameRule.Camel, concepts.Get("Pipeline.Stage").NameRule);
    }

    [Fact]
    public void BothSampleDocumentsAreCleanUnderTheStandardCatalogue()
    {
        // The documents the sample ships with are what a user opens first, so the conventions their language
        // declares have to be the ones they are written in - `Release` and `Nightly`, `build`, `test`, `smoke`.
        var language = Language();
        var directory = Path.Combine(AppContext.BaseDirectory, "Declared");

        foreach (var name in (string[])["pipeline.mps", "nightly.mps"])
        {
            var model = ModelFile.Load(language.NewProject(), Path.Combine(directory, name));

            Assert.Empty(StandardChecks.All.Find(model));
        }
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
    public void TheDeclaredRulesAreTheLanguagesOwn()
    {
        // A language defined as data reports its own findings: the two documents in `constraints/` are rules, and the
        // engine runs them exactly as it runs a rule written in C#.
        var language = Language();

        Assert.True(language.HasConstraints);
        Assert.Equal(2, language.Constraints.Count);

        var model = ModelFile.Load(language.NewProject(), Path.Combine(AppContext.BaseDirectory, "Declared", "pipeline.mps"));
        var stage = model.Roots[0].GetChildren("stages")[0];

        // The shipped document says what its rules ask for, so it reports nothing.
        Assert.Empty(new ConstraintEngine(language.Constraints).Check(model));

        stage.Set("name", "unnamed");
        stage.Set("command", " dotnet build");

        var findings = new ConstraintEngine(language.Constraints).Check(model);

        Assert.Collection(
            findings,
            warning =>
            {
                Assert.Equal(DiagnosticSeverity.Warning, warning.Severity);
                Assert.Equal("command", warning.Role);
                Assert.Equal("pipeline.stage.command", warning.Code);
            },
            hint =>
            {
                Assert.Equal(DiagnosticSeverity.Hint, hint.Severity);
                Assert.Equal("name", hint.Role);
                Assert.Equal("pipeline.stage.name", hint.Code);
            });
    }

    [Fact]
    public void TheDeclaredGeneratorTurnsTheDocumentIntoAShellScript()
    {
        // The fourth declared aspect, in the same folder: a generator is a model too, and `generator/shell-script.mps`
        // is the language's only target. The stage with no command is the half a template could not say before - the
        // `whenFilled` part asks the slot, not a property's value, so a missing command becomes a comment rather than
        // an empty line.
        var language = Language();
        var generator = Assert.Single(language.Generators);

        Assert.True(language.HasGenerators);
        Assert.Equal("ShellScript", generator.Name);

        var pipeline = ModelFile.Load(language.NewProject(), Path.Combine(AppContext.BaseDirectory, "Declared", "pipeline.mps"));
        var nightly = ModelFile.Load(language.NewProject(), Path.Combine(AppContext.BaseDirectory, "Declared", "nightly.mps"));

        Assert.Equal(
            "# Release\ndotnet build\ndotnet test",
            new Generator().Generate(pipeline.Roots[0], generator).Text);

        Assert.Equal(
            "# Nightly\ndotnet build\n# smoke has no command",
            new Generator().Generate(nightly.Roots[0], generator).Text);
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
            var constraints = Directory.EnumerateFiles(Path.Combine(directory, "constraints"), "*.mps").ToList();
            var generators = Directory.EnumerateFiles(Path.Combine(directory, "generator"), "*.mps").ToList();

            Assert.Equal(2, structure.Count);
            Assert.Equal(2, editor.Count);
            Assert.Equal(2, constraints.Count);
            Assert.Single(generators);

            foreach (var file in structure)
            {
                AssertRoundTrips(file, new Project(StructureLanguage.Concepts));
            }

            foreach (var file in editor)
            {
                AssertRoundTrips(file, new Project(EditorLanguage.Concepts));
            }

            foreach (var file in constraints)
            {
                AssertRoundTrips(file, new Project(ConstraintLanguage.Concepts));
            }

            foreach (var file in generators)
            {
                AssertRoundTrips(file, new Project(TextGenLanguage.Concepts));
            }

            // The language root is a document of the structure language too, and the one file whose re-save would be
            // most tempting to skip: it sits beside the folders, not inside one.
            AssertRoundTrips(Path.Combine(directory, LanguageProject.LanguageFile), new Project(StructureLanguage.Concepts));
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

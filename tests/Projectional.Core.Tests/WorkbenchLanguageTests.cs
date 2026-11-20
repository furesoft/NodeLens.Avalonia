using Projectional.Core;
using Projectional.ConstraintRules;
using Projectional.Editors;
using Projectional.Editing;
using Projectional.Languages;
using Projectional.Persistence;
using Projectional.Projection;
using Projectional.Samples;
using Projectional.Structures;
using Projectional.TextGen;
using Projectional.TypeRules;
using Projectional.Workbench;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The shell's set of languages: the sample one, the two it is written with, and any language project opened from a
/// folder.
/// </summary>
/// <remarks>
/// The set is per shell and grows at runtime, which is the whole reason it is an instance: two shells in one process
/// must not see each other's languages, and a language opened from a folder has to join the ones already there.
/// </remarks>
public sealed class WorkbenchLanguageTests
{
    [Fact]
    public void AShellStartsWithEveryDefinitionLanguage()
    {
        var language = new WorkbenchLanguage();

        Assert.True(language.Concepts.TryGet(RulesConceptKeys.Ruleset, out _));

        // One definition per document: what a document of each definition language starts with is the definition
        // itself, so these are the concepts the shell has to know.
        Assert.True(language.Concepts.TryGet(StructureConceptKeys.Concept, out _));
        Assert.True(language.Concepts.TryGet(EditorConceptKeys.Notation, out _));
        Assert.True(language.Concepts.TryGet(TypeRuleConceptKeys.Rule, out _));
        Assert.True(language.Concepts.TryGet(TextGenConceptKeys.Generator, out _));
        Assert.True(language.Concepts.TryGet(ConstraintConceptKeys.Rule, out _));
        Assert.Empty(language.Opened);
    }

    [Fact]
    public void EveryConcreteConceptHasANotation()
    {
        // A registered concept without a notation still opens, as a missing-notation cell - the one state a user
        // cannot work around, so the composition is checked rather than trusted.
        var language = new WorkbenchLanguage();

        foreach (var concept in language.Concepts.Concepts.Where(each => !each.IsAbstract))
        {
            Assert.True(
                language.Notations.TryGet(concept.Key, out _),
                $"'{concept.Key}' has no notation in the shell's registry");
        }
    }

    [Fact]
    public void AnOpenedLanguageReadsAFileNoBuiltInFormatClaims()
    {
        // File > Import... on a file the five reference languages do not know: the answer is the importers the opened
        // language declares and the ones its generators derive, and the document that comes out opens with that
        // language's own aspects.
        var language = new WorkbenchLanguage();
        var project = LanguageProject.Open(Path.Combine(AppContext.BaseDirectory, "Declared"), out var error);

        Assert.Null(error);
        Assert.True(language.TryAdd(project!, out var why), why);

        var imported = language.TryImport("variant", "pipeline = Release\nstage = dotnet build\nstage = dotnet test", out var reason);

        Assert.Null(reason);
        Assert.NotNull(imported);
        Assert.Same(project, imported.Language);

        var root = Assert.Single(imported.Model.Roots);

        Assert.Equal("Pipeline.Pipeline", root.Concept.Key);
        Assert.Equal("Release", root.GetOrNull("name"));
        Assert.Equal(
            ["dotnet build", "dotnet test"],
            root.GetChildren("stages").Select(stage => stage.GetOrNull("command")));
    }

    [Fact]
    public void AShapeCheckNarrowsNoCandidate()
    {
        // `CanImport` answers "does the text start like a document" - and an importer whose root rule begins with an
        // anchor says no for a file that opens with a comment it declares as skipped, which is a file it reads. Using
        // the hint as a gate would refuse it, so the readers decide and the hint only picks which refusal to repeat.
        var language = new WorkbenchLanguage();
        var project = LanguageProject.Open(Path.Combine(AppContext.BaseDirectory, "Declared"), out var error);

        Assert.Null(error);
        Assert.True(language.TryAdd(project!, out var why), why);

        const string Text = "# the nightly pipeline\npipeline = \"Release\"\n\nstage = \"dotnet build\"\nstage = \"dotnet test\"\n";

        var relaxed = Assert.Single(project!.Importers, importer => importer.Format == "relaxed");

        Assert.False(relaxed.CanImport(Text));

        var imported = language.TryImport("relaxed", Text, out var reason);

        Assert.Null(reason);
        Assert.NotNull(imported);
        Assert.Equal("Release", imported.Model.Roots[0].GetOrNull("name"));
    }

    [Fact]
    public void AFileNoImporterOfTheOpenedLanguagesReadsNamesWhereItStopped()
    {
        var language = new WorkbenchLanguage();

        // Nothing to ask is not a failure to explain: the shell has its own sentence for a window that declares no
        // importer at all, and a reason here would pre-empt it.
        Assert.Null(language.TryImport("nothing", "item a", out var none));
        Assert.Null(none);

        var project = LanguageProject.Open(Path.Combine(AppContext.BaseDirectory, "Declared"), out var error);

        Assert.Null(error);
        Assert.True(language.TryAdd(project!, out var why), why);

        Assert.Null(language.TryImport("nothing", "this is not a pipeline", out var reason));

        // A place and a column are what the user needs, and they come from the importer whose rules the text most
        // nearly matched - the first one that claimed it, or the first tried when none did.
        Assert.Contains("stopped at 1:1", reason, StringComparison.Ordinal);
    }

    [Fact]
    public void ANewConceptDocumentOpensInASessionAndCanBeWritten()
    {
        var language = new WorkbenchLanguage();
        var model = language.NewConceptDefinition();

        using var session = new EditorSession(model.Roots[0], language.Notations, typing: WorkbenchLanguage.Typing);

        // One definition per document: the root *is* the concept, so naming it is the whole of writing the file.
        model.Roots[0].Set(StructureLanguage.KeySlot, "Rules.Rule");
        session.Refresh();

        Assert.Contains("Rules.Rule", LayoutSnapshot.Render(session.Layout, width: 120), StringComparison.Ordinal);
    }

    [Fact]
    public void ADefinitionSurvivesARoundTripThroughTheShellsLanguage()
    {
        var language = new WorkbenchLanguage();
        var model = language.NewConceptDefinition();

        model.Roots[0].Set(StructureLanguage.KeySlot, "Rules.Rule");

        var path = Path.Combine(Path.GetTempPath(), $"definition-{Guid.NewGuid():N}.mps");

        try
        {
            ModelFile.Save(model, path);

            var loaded = DocumentFile.TryLoad(path, language.NewProject(), out var error);

            Assert.Null(error);
            Assert.NotNull(loaded);
            Assert.Equal(ModelXml.Serialize(model), ModelXml.Serialize(loaded));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void EveryDefinitionDocumentOfTheSampleOpensAsADocument()
    {
        // What File > Open language... does after the folder loads: every definition file of the sample opens as a
        // document of its own, which is where the user edits it.
        var language = new WorkbenchLanguage();
        var directory = Path.Combine(AppContext.BaseDirectory, "Declared");

        var structure = Directory.EnumerateFiles(Path.Combine(directory, "structure"), "*.mps").ToList();
        var editor = Directory.EnumerateFiles(Path.Combine(directory, "editor"), "*.mps").ToList();

        Assert.Equal(2, structure.Count);
        Assert.Equal(2, editor.Count);

        foreach (var file in structure.Concat(editor))
        {
            var model = DocumentFile.TryLoad(file, language.NewProject(), out var error);

            Assert.Null(error);
            Assert.NotNull(model);
            Assert.Single(model.Roots);

            using var session = new EditorSession(model.Roots[0], language.Notations);

            Assert.NotEmpty(LayoutSnapshot.Render(session.Layout, width: 120));
        }
    }

    [Fact]
    public void AConceptDocumentOpensAsOneDefinition()
    {
        // One definition per document: the root is the concept, so an empty one is the concept's own sections -
        // there is no list to be ambiguous about, which is what the old two-section shape needed headings for.
        var language = new WorkbenchLanguage();
        var model = language.NewConceptDefinition();

        using var session = new EditorSession(model.Roots[0], language.Notations);

        Assert.Single(model.Roots);
        Assert.Equal(StructureConceptKeys.Concept, model.Roots[0].Concept.Key);
        Assert.Contains("concept", LayoutSnapshot.Render(session.Layout, width: 120), StringComparison.Ordinal);
    }

    [Fact]
    public void ANotationDocumentOpensAsOneNotation()
    {
        var language = new WorkbenchLanguage();
        var model = language.NewNotationDefinition();

        using var session = new EditorSession(model.Roots[0], language.Notations);

        // One notation per document, so the empty state is the notation's own sections rather than a list of them.
        Assert.Single(model.Roots);
        Assert.Equal(EditorConceptKeys.Notation, model.Roots[0].Concept.Key);
        Assert.Contains("editor for concept", LayoutSnapshot.Render(session.Layout, width: 120), StringComparison.Ordinal);
    }

    [Fact]
    public void AnOpenedLanguageJoinsTheSetAndCanBeUsed()
    {
        using var folder = new TemporaryDirectory();

        WriteLanguage(folder.Root, "Payroll.Payslip", ["name"]);

        var language = new WorkbenchLanguage();
        var opened = LanguageProject.Open(folder.Root, out _)!;

        Assert.True(language.TryAdd(opened, out var error), error);

        // The concept registry and the notation registry both know it now, which is what "usable" means.
        var model = WorkbenchLanguage.NewDocument(opened);

        Assert.NotNull(model);

        var node = model.Roots[0];

        node.Set("name", "Approved");

        Assert.Equal("Payroll.Payslip", node.Concept.Key);
        Assert.Equal("rule Approved", LayoutSnapshot.Render(node, language.Notations, width: 40));
    }

    [Fact]
    public void ALanguageThatClashesWithOneAlreadyThereIsRefusedAndChangesNothing()
    {
        // The shell keeps the registry it had: a half-added language would give documents a concept registry that
        // disagrees with the notation registry in force.
        using var folder = new TemporaryDirectory();

        WriteLanguage(folder.Root, RulesConceptKeys.Rule, ["name"]);

        var language = new WorkbenchLanguage();
        var before = language.Concepts;
        var opened = LanguageProject.Open(folder.Root, out _)!;

        Assert.False(language.TryAdd(opened, out var error));
        Assert.Contains(RulesConceptKeys.Rule, error!, StringComparison.Ordinal);
        Assert.Same(before, language.Concepts);
        Assert.Empty(language.Opened);

        // The shell still works.
        Assert.True(language.Concepts.TryGet(RulesConceptKeys.Ruleset, out _));
    }

    [Fact]
    public void TheRootConceptIsTheFirstConcreteOneTheLanguageDeclares()
    {
        // Declaration order is the language author's answer to "what is this language about", the same order that
        // ranks completion.
        using var folder = new TemporaryDirectory();

        WriteLanguage(folder.Root, "Rules.Thing", ["name"], alsoAbstract: "Rules.Element");

        var opened = LanguageProject.Open(folder.Root, out _)!;

        Assert.Equal("Rules.Thing", WorkbenchLanguage.Rootable(opened)!.Key);
        Assert.Equal("Rules.Thing", WorkbenchLanguage.NewDocument(opened)!.Roots[0].Concept.Key);
    }

    [Fact]
    public void TheSampleRuleLanguageIsRunnable()
    {
        Assert.NotNull(WorkbenchLanguage.InterpreterFor(PayrollSample.NewSampleModel()));
    }

    [Fact]
    public void ALanguageThatDefinesNoInterpreterIsNotRunnable()
    {
        // Running one of these against the sample's rules would answer with a missing rule for every concept, so the
        // shell answers "not runnable" instead of asking the interpreter at all.
        var language = new WorkbenchLanguage();

        Assert.Null(WorkbenchLanguage.InterpreterFor(language.NewConceptDefinition()));
        Assert.Null(WorkbenchLanguage.InterpreterFor(language.NewNotationDefinition()));
    }

    [Fact]
    public void AModelWithoutARootIsNotRunnable()
    {
        // Nothing to run: the concept that would decide the language is the root's.
        Assert.Null(WorkbenchLanguage.InterpreterFor(new WorkbenchLanguage().NewProject().NewModel("empty")));
    }

    [Fact]
    public void AModelOfAnOpenedLanguageBelongsToThatProject()
    {
        // Which language a document is written in is a fact about its root, not about the command that opened it -
        // and a document of the shell's own languages belongs to no folder at all, so there are no tests to run.
        using var folder = new TemporaryDirectory();

        WriteLanguage(folder.Root, "Payroll.Payslip", ["name"]);

        var language = new WorkbenchLanguage();
        var opened = LanguageProject.Open(folder.Root, out _)!;

        Assert.Null(language.ProjectFor(language.NewConceptDefinition()));

        Assert.True(language.TryAdd(opened, out var error), error);

        Assert.Same(opened, language.ProjectFor(WorkbenchLanguage.NewDocument(opened)!));
        Assert.Null(language.ProjectFor(language.NewConceptDefinition()));
        Assert.Null(language.ProjectFor(language.NewProject().NewModel("empty")));
    }

    /// <summary>Writes a one-concept language project whose notation is the keyword <c>rule</c> and the name.</summary>
    /// <remarks>
    /// One document per definition, so each concept and each notation gets its own file in its aspect folder, and
    /// the file names are the concept keys - which is also what orders them, because the project reads a folder in
    /// ordinal file-name order.
    /// </remarks>
    private static void WriteLanguage(string directory, string conceptKey, string[] properties, string? alsoAbstract = null)
    {
        var definition = new DefinitionBuilder();

        if (alsoAbstract is not null)
        {
            definition.Concept(alsoAbstract, isAbstract: true);
        }

        definition.Concept(
            conceptKey,
            slots: [.. properties.Select(property => definition.Property(property, "string"))],
            nameSlot: properties[0]);

        var structure = Path.Combine(directory, LanguageProject.StructureFolder);

        Directory.CreateDirectory(structure);

        foreach (var document in definition.Documents)
        {
            var key = document.Roots[0].GetOrNull(StructureLanguage.KeySlot)!;

            ModelFile.Save(document, Path.Combine(structure, $"{key}.mps"));
        }

        var notation = new NotationBuilder();
        var declared = notation.Notation(conceptKey);
        var line = notation.SetRoot(declared, EditorConceptKeys.Line);

        notation.Word(line, EditorConceptKeys.Keyword, "rule");
        notation.Cell(line, EditorConceptKeys.Space);
        notation.Slot(line, EditorConceptKeys.Property, properties[0]);

        var editor = Path.Combine(directory, LanguageProject.EditorFolder);

        Directory.CreateDirectory(editor);
        ModelFile.Save(notation.Documents[0], Path.Combine(editor, $"{conceptKey}.mps"));
    }
}

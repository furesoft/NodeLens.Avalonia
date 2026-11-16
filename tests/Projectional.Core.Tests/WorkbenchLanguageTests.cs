using Projectional.Core;
using Projectional.Editors;
using Projectional.Editing;
using Projectional.Languages;
using Projectional.Persistence;
using Projectional.Projection;
using Projectional.Samples;
using Projectional.Structures;
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
    public void AShellStartsWithAllThreeLanguages()
    {
        var language = new WorkbenchLanguage();

        Assert.True(language.Concepts.TryGet(RulesConceptKeys.Ruleset, out _));
        Assert.True(language.Concepts.TryGet(StructureConceptKeys.Language, out _));
        Assert.True(language.Concepts.TryGet(EditorConceptKeys.Notations, out _));
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
    public void ANewLanguageDefinitionOpensInASessionAndCanBeWritten()
    {
        var language = new WorkbenchLanguage();
        var model = language.NewLanguageDefinition();

        using var session = new EditorSession(model.Roots[0], language.Notations, typing: WorkbenchLanguage.Typing);

        var concept = model.NewChild(model.Roots[0], StructureLanguage.ConceptSlot, StructureConceptKeys.Concept);

        concept.Set(StructureLanguage.KeySlot, "Rules.Rule");
        session.Refresh();

        Assert.Contains("Rules.Rule", LayoutSnapshot.Render(session.Layout, width: 120), StringComparison.Ordinal);
    }

    [Fact]
    public void ADefinitionSurvivesARoundTripThroughTheShellsLanguage()
    {
        var language = new WorkbenchLanguage();
        var model = language.NewLanguageDefinition();

        model.NewChild(model.Roots[0], StructureLanguage.ConceptSlot, StructureConceptKeys.Concept)
            .Set(StructureLanguage.KeySlot, "Rules.Rule");

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
    public void ALanguageDefinitionOpensWithItsTwoSections()
    {
        // A new definition is two lists in two slots, so without a heading the two placeholders are the same
        // picture - and picking the wrong one to press Enter in creates the wrong kind of declaration.
        var language = new WorkbenchLanguage();
        var model = language.NewLanguageDefinition();

        using var session = new EditorSession(model.Roots[0], language.Notations);

        Assert.Equal(
            string.Join('\n', "enumerations:", "  << ... >>", "concepts:", "  << ... >>"),
            LayoutSnapshot.Render(session.Layout, width: 120));
    }

    [Fact]
    public void ANotationDefinitionOpensToo()
    {
        var language = new WorkbenchLanguage();
        var model = language.NewNotationDefinition();

        using var session = new EditorSession(model.Roots[0], language.Notations);

        // The section placeholder of the MPS shape, not a prose sentence: an empty list of notations is the same
        // state as an empty list of concepts, and one placeholder for both is one thing to learn.
        Assert.Equal(
            string.Join('\n', "notations:", "  << ... >>"),
            LayoutSnapshot.Render(session.Layout, width: 120));
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

    /// <summary>Writes a one-concept language project whose notation is the keyword <c>rule</c> and the name.</summary>
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

        ModelFile.Save(definition.Root.Model, Path.Combine(directory, LanguageProject.StructureFile));

        var notation = new NotationBuilder();
        var declared = notation.Notation(conceptKey);
        var line = notation.SetRoot(declared, EditorConceptKeys.Line);

        notation.Word(line, EditorConceptKeys.Keyword, "rule");
        notation.Cell(line, EditorConceptKeys.Space);
        notation.Slot(line, EditorConceptKeys.Property, properties[0]);

        ModelFile.Save(notation.Root.Model, Path.Combine(directory, LanguageProject.EditorFile));
    }
}

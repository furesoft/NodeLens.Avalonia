using Projectional.Core;
using Projectional.ImportRules;
using Projectional.Language;
using Projectional.Languages;
using Projectional.Persistence;
using Projectional.Semantics;
using Projectional.Structures;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>A language that declares how to read its own files, in an <c>import</c> folder.</summary>
/// <remarks>
/// The subject is deliberately a folder rather than a hand-built importer: what is being pinned is that a language
/// project carries an importer beside its structure, the way it carries a generator or a constraint.
/// </remarks>
public sealed class DeclaredImporterTests
{
    [Fact]
    public void ALanguageProjectOffersTheImporterItsImportFolderDeclares()
    {
        using var folder = new TemporaryDirectory();

        WriteStructure(folder.Root);
        WriteImporter(folder.Root, [
            ("item ", "name"),
        ]);

        var language = LanguageProject.Open(folder.Root, out var error);

        Assert.Null(error);

        var importer = Assert.IsType<DeclaredImporter>(Assert.Single(language!.Importers));

        Assert.Equal("ini", importer.Format);
    }

    [Fact]
    public void TheDeclaredRuleReadsTheTextIntoTheModel()
    {
        using var folder = new TemporaryDirectory();

        WriteStructure(folder.Root);

        var language = LanguageProject.Open(folder.Root, out var error);

        Assert.Null(error);

        var importer = Import(folder.Root);
        var model = language!.NewProject().NewModel("imported");

        var result = importer.Import(model, "item one");

        Assert.True(result.Succeeded, Describe(result));

        var root = Assert.Single(result.Roots);

        Assert.Equal("Item", root.Concept.Key);
        Assert.Equal("one", root.GetOrNull("name"));
    }

    [Fact]
    public void ACaptureWithNothingAfterItTakesTheRestOfTheText()
    {
        using var folder = new TemporaryDirectory();

        WriteStructure(folder.Root);

        var language = LanguageProject.Open(folder.Root, out _)!;
        var result = Import(folder.Root).Import(
            language.NewProject().NewModel("imported"),
            "item a name with spaces");

        Assert.True(result.Succeeded, Describe(result));
        Assert.Equal("a name with spaces", result.Roots[0].GetOrNull("name"));
    }

    [Fact]
    public void TextThatDoesNotMatchTheRuleNamesThePlaceItStopped()
    {
        using var folder = new TemporaryDirectory();

        WriteStructure(folder.Root);

        var model = LanguageProject.Open(folder.Root, out _)!.NewProject().NewModel("imported");
        var result = Import(folder.Root).Import(model, "nope");

        Assert.False(result.Succeeded);
        Assert.Equal(DeclaredImporter.UnexpectedText, Assert.Single(result.Diagnostics).Code);
        Assert.Empty(model.Roots);
    }

    [Fact]
    public void ACaptureNamingAPropertyTheConceptDoesNotHaveIsRefusedAtLoad()
    {
        using var folder = new TemporaryDirectory();

        WriteStructure(folder.Root);
        WriteImporter(folder.Root, [("item ", "nothing")]);

        Assert.Null(LanguageProject.Open(folder.Root, out var error));
        Assert.Contains("nothing", error, StringComparison.Ordinal);
    }

    [Fact]
    public void ADeclaredImporterReadsASpellingTheGeneratorNeverWrites()
    {
        // The shipped pipeline reads two spellings: its own generator's shell script, and an ini-shaped variant the
        // generator cannot produce. That difference is the whole reason a rule exists beside a template - the inverse
        // of the generator could only ever read back what it wrote.
        var language = LanguageProject.Open(Path.Combine(AppContext.BaseDirectory, "Declared"), out var error);

        Assert.Null(error);
        Assert.True(language!.TryCreateImporter("ini", out var importer, out var why), why);
        Assert.IsType<DeclaredImporter>(importer);

        var model = language.NewProject().NewModel("imported");
        var result = importer!.Import(model, "pipeline = Release\nstage = dotnet build\nstage = dotnet test");

        Assert.True(result.Succeeded, Describe(result));

        var root = Assert.Single(result.Roots);

        Assert.Equal("Release", root.GetOrNull("name"));
        Assert.Equal(
            ["dotnet build", "dotnet test"],
            root.GetChildren("stages").Select(stage => stage.GetOrNull("command")));

        // And the model it read is the same model the generator prints, which is what "a second spelling of one
        // model" means.
        var generator = language.Generators.Single(candidate => candidate.Name == "ShellScript");

        Assert.Equal("# Release\ndotnet build\ndotnet test", new Generator().Generate(root, generator).Text);
    }

    private static DeclaredImporter Import(string directory) =>
        Assert.IsType<DeclaredImporter>(Assert.Single(LanguageProject.Open(directory, out _)!.Importers));

    private static string Describe(ImportResult result) =>
        string.Join(Environment.NewLine, result.Diagnostics.Select(diagnostic => diagnostic.ToString()));

    /// <summary>One concept with a name and a detail, which is what the rule reads.</summary>
    private static void WriteStructure(string directory)
    {
        var definition = new DefinitionBuilder();

        definition.Concept(
            "Item",
            slots:
            [
                definition.Property("name", "string", defaultValue: "unnamed"),
                definition.Property("detail", "string", defaultValue: ""),
            ],
            nameSlot: "name");

        var structure = Path.Combine(directory, LanguageProject.StructureFolder);

        Directory.CreateDirectory(structure);

        foreach (var document in definition.Documents)
        {
            ModelFile.Save(document, Path.Combine(structure, $"{document.Roots[0].GetOrNull(StructureLanguage.KeySlot)}.mps"));
        }

        WriteImporter(directory, [("item ", "name")]);
    }

    /// <summary>An importer whose rule is a literal followed by one capture.</summary>
    private static void WriteImporter(string directory, IReadOnlyList<(string Literal, string Slot)> parts)
    {
        var model = ImportLanguage.NewModel();
        var importer = model.Roots[0];

        importer.Set(ImportLanguage.NameSlot, "ini");

        var rule = model.NewChild(importer, ImportLanguage.RulesSlot, ImportConceptKeys.Rule);

        rule.Set(ImportLanguage.ConceptKeySlot, "Item");

        model.NewChild(rule, ImportLanguage.PartsSlot, ImportConceptKeys.Literal)
            .Set(ImportLanguage.TextSlot, "item ");

        foreach (var (_, slot) in parts.ToList())
        {
            model.NewChild(rule, ImportLanguage.PartsSlot, ImportConceptKeys.Capture)
                .Set(ImportLanguage.SlotSlot, slot);
        }

        var imports = Path.Combine(directory, LanguageProject.ImportFolder);

        Directory.CreateDirectory(imports);
        ModelFile.Save(model, Path.Combine(imports, "ini.mps"));
    }
}

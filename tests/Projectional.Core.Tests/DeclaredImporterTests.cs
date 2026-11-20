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

    [Fact]
    public void ATokenClassDecidesWhereACaptureEnds()
    {
        // Without a class, a capture runs to the next literal - which is the wrong answer for a value that may contain
        // the character the next literal starts with. A word stops at the space, and a number after it.
        using var folder = new TemporaryDirectory();

        WriteStructure(folder.Root);
        WriteTokens(folder.Root);

        var language = LanguageProject.Open(folder.Root, out var error);

        Assert.Null(error);

        var model = language!.NewProject().NewModel("imported");
        var result = Assert.IsType<DeclaredImporter>(Assert.Single(language.Importers))
            .Import(model, "item dotnet-cli 42");

        Assert.True(result.Succeeded, Describe(result));
        Assert.Equal("dotnet-cli", result.Roots[0].GetOrNull("name"));
        Assert.Equal("42", result.Roots[0].GetOrNull("detail"));
    }

    /// <summary>An importer whose name is a word and whose detail is a number.</summary>
    private static void WriteTokens(string directory)
    {
        var model = ImportLanguage.NewModel();
        var importer = model.Roots[0];

        importer.Set(ImportLanguage.NameSlot, "ini");

        var word = model.NewChild(importer, ImportLanguage.TokensSlot, ImportConceptKeys.Word);

        word.Set(ImportLanguage.TokenNameSlot, "Name");
        word.Set(ImportLanguage.ExtraSlot, "-");

        model.NewChild(importer, ImportLanguage.TokensSlot, ImportConceptKeys.Number)
            .Set(ImportLanguage.TokenNameSlot, "Count");

        var rule = model.NewChild(importer, ImportLanguage.RulesSlot, ImportConceptKeys.Rule);

        rule.Set(ImportLanguage.ConceptKeySlot, "Item");

        model.NewChild(rule, ImportLanguage.PartsSlot, ImportConceptKeys.Literal).Set(ImportLanguage.TextSlot, "item");

        var space = model.NewChild(rule, ImportLanguage.PartsSlot, ImportConceptKeys.Literal);

        space.Set(ImportLanguage.TextSlot, " ");

        var name = model.NewChild(rule, ImportLanguage.PartsSlot, ImportConceptKeys.Capture);

        name.Set(ImportLanguage.SlotSlot, "name");
        name.Set(ImportLanguage.TokenSlot, "Name");

        model.NewChild(rule, ImportLanguage.PartsSlot, ImportConceptKeys.Literal).Set(ImportLanguage.TextSlot, " ");

        var detail = model.NewChild(rule, ImportLanguage.PartsSlot, ImportConceptKeys.Capture);

        detail.Set(ImportLanguage.SlotSlot, "detail");
        detail.Set(ImportLanguage.TokenSlot, "Count");

        var imports = Path.Combine(directory, LanguageProject.ImportFolder);

        Directory.CreateDirectory(imports);
        ModelFile.Save(model, Path.Combine(imports, "ini.mps"));
    }

    [Fact]
    public void ABoundRunHasToBeTheSameRunAgain()
    {
        // XML's closing tag: nothing else in the vocabulary compares two runs, which is why bind and matches exist.
        using var folder = new TemporaryDirectory();

        WriteStructure(folder.Root);

        WriteParts(folder.Root, (model, rule) =>
        {
            var bind = model.NewChild(rule, ImportLanguage.PartsSlot, ImportConceptKeys.Bind);
            bind.Set(ImportLanguage.AsSlot, "open");
            bind.Set(ImportLanguage.TokenSlot, "Tag");
            model.NewChild(rule, ImportLanguage.PartsSlot, ImportConceptKeys.Literal).Set(ImportLanguage.TextSlot, ">");
            model.NewChild(rule, ImportLanguage.PartsSlot, ImportConceptKeys.Capture).Set(ImportLanguage.SlotSlot, "detail");
            model.NewChild(rule, ImportLanguage.PartsSlot, ImportConceptKeys.Literal).Set(ImportLanguage.TextSlot, "</");
            var matches = model.NewChild(rule, ImportLanguage.PartsSlot, ImportConceptKeys.Matches);
            matches.Set(ImportLanguage.AsSlot, "open");
            matches.Set(ImportLanguage.TokenSlot, "Tag");
            model.NewChild(rule, ImportLanguage.PartsSlot, ImportConceptKeys.Literal).Set(ImportLanguage.TextSlot, ">");
        }, ("Tag", ImportConceptKeys.Word));

        var importer = Import(folder.Root);
        var good = importer.Import(NewModel(folder.Root), "a>x</a>");

        Assert.True(good.Succeeded, Describe(good));
        Assert.Equal("x", good.Roots[0].GetOrNull("detail"));

        var bad = importer.Import(NewModel(folder.Root), "a>x</b>");

        Assert.False(bad.Succeeded);
        Assert.Contains("has to be 'a' again", Assert.Single(bad.Diagnostics).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AFixedRunResolvesNoEscapes()
    {
        using var folder = new TemporaryDirectory();

        WriteStructure(folder.Root);

        WriteParts(folder.Root, (model, rule) =>
        {
            model.NewChild(rule, ImportLanguage.PartsSlot, ImportConceptKeys.Literal).Set(ImportLanguage.TextSlot, "item ");
            var capture = model.NewChild(rule, ImportLanguage.PartsSlot, ImportConceptKeys.Capture);
            capture.Set(ImportLanguage.SlotSlot, "name");
            capture.Set(ImportLanguage.TokenSlot, "Body");
        }, ("Body", ImportConceptKeys.Fixed));

        var importer = Import(folder.Root);
        var result = importer.Import(NewModel(folder.Root), "item <![CDATA[a\\b]]>");

        Assert.True(result.Succeeded, Describe(result));
        Assert.Equal("a\\b", result.Roots[0].GetOrNull("name"));
    }

    [Fact]
    public void AFailGivesTheRulesOwnMessage()
    {
        using var folder = new TemporaryDirectory();

        WriteStructure(folder.Root);

        WriteParts(folder.Root, (model, rule) =>
        {
            model.NewChild(rule, ImportLanguage.PartsSlot, ImportConceptKeys.Literal).Set(ImportLanguage.TextSlot, "item");
            var fail = model.NewChild(rule, ImportLanguage.PartsSlot, ImportConceptKeys.Fail);
            fail.Set(ImportLanguage.CodeSlot, "ini.trailing-comma");
            fail.Set(ImportLanguage.MessageSlot, "a trailing comma is not allowed");
        }, []);

        var result = Import(folder.Root).Import(NewModel(folder.Root), "item,");

        Assert.False(result.Succeeded);

        var diagnostic = Assert.Single(result.Diagnostics);

        Assert.Equal("ini.trailing-comma", diagnostic.Code);
        Assert.Equal("a trailing comma is not allowed", diagnostic.Message);
    }

    /// <summary>An importer with one rule whose parts a test writes, plus the token classes it declares.</summary>
    private static void WriteParts(
        string directory,
        Action<Model, Node> parts,
        params (string Key, string Concept)[] tokens)
    {
        var model = ImportLanguage.NewModel();
        var importer = model.Roots[0];

        importer.Set(ImportLanguage.NameSlot, "ini");

        foreach (var (key, concept) in tokens)
        {
            var token = model.NewChild(importer, ImportLanguage.TokensSlot, concept);

            token.Set(ImportLanguage.TokenNameSlot, key);

            if (concept == ImportConceptKeys.Fixed)
            {
                token.Set(ImportLanguage.OpenSlot, "<![CDATA[");
                token.Set(ImportLanguage.CloseSlot, "]]>");
            }
        }

        var rule = model.NewChild(importer, ImportLanguage.RulesSlot, ImportConceptKeys.Rule);

        rule.Set(ImportLanguage.ConceptKeySlot, "Item");
        parts(model, rule);

        var imports = Path.Combine(directory, LanguageProject.ImportFolder);

        Directory.CreateDirectory(imports);
        ModelFile.Save(model, Path.Combine(imports, "ini.mps"));
    }

    private static Model NewModel(string directory) =>
        LanguageProject.Open(directory, out _)!.NewProject().NewModel("imported");

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

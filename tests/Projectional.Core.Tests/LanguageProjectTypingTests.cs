using Projectional.Core;
using Projectional.Language;
using Projectional.Languages;
using Projectional.Persistence;
using Projectional.Structures;
using Projectional.TypingRules;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>A language folder declares its typing rules: the documents load, lower, and answer the editor.</summary>
/// <remarks>
/// The folder is written here rather than read from <c>samples/</c>, so the assertions are about the loader and not
/// about a file somebody may re-word: the sample's own documents are covered by
/// <c>DeclaredExpressionsTests</c>, which loads them and pins that they read back byte-identically.
/// </remarks>
public sealed class LanguageProjectTypingTests
{
    [Fact]
    public void ATypingFolderBecomesTheLanguagesRules()
    {
        using var folder = new TemporaryDirectory();

        WriteStructure(folder.Root);
        WriteRule(folder.Root, name: "number", conceptKey: "Doc.Number", claims: TypingClaims.Digits, property: "value");

        var language = LanguageProject.Open(folder.Root, out var error);

        Assert.Null(error);
        Assert.NotNull(language);

        Assert.True(language.HasTyping);

        var rule = Assert.IsType<TypingRule>(language.Typing.ClaimFor("Doc.Number", "42"));

        Assert.Equal("value", rule.PropertySlot);
    }

    [Fact]
    public void ALanguageWithoutATypingFolderDeclaresNone()
    {
        using var folder = new TemporaryDirectory();

        WriteStructure(folder.Root);

        var language = LanguageProject.Open(folder.Root, out var error);

        Assert.Null(error);
        Assert.NotNull(language);

        Assert.False(language.HasTyping);
        Assert.Null(language.Typing.ClaimFor("Doc.Number", "42"));
    }

    [Fact]
    public void ARuleThatNamesAPropertyTheConceptDoesNotHaveIsRefusedWhenTheFolderLoads()
    {
        using var folder = new TemporaryDirectory();

        WriteStructure(folder.Root);

        // `missing` is no property of Doc.Number, and the folder is the earliest moment both halves exist.
        WriteRule(folder.Root, name: "number", conceptKey: "Doc.Number", claims: TypingClaims.Digits, property: "missing");

        var language = LanguageProject.Open(folder.Root, out var error);

        Assert.Null(language);
        Assert.Contains("declares no property named 'missing'", error, StringComparison.Ordinal);
    }

    [Fact]
    public void ARuleThatClaimsTheAliasReadsTheWordsTheConceptDeclares()
    {
        // MPS' "an instance created from a typed alias": the word completion offers is the word a keystroke creates,
        // because both read the concept's declaration - so a rule that says "the alias" needs no text of its own.
        using var folder = new TemporaryDirectory();

        WriteStructure(folder.Root, alias: "num");
        WriteRule(folder.Root, name: "number", conceptKey: "Doc.Number", claims: TypingClaims.Alias, property: "value");

        var language = LanguageProject.Open(folder.Root, out var error);

        Assert.Null(error);
        Assert.NotNull(language);

        Assert.Equal("value", language.Typing.ClaimFor("Doc.Number", "num")?.PropertySlot);

        // The whole word, not a prefix and not a case-insensitive match.
        Assert.Null(language.Typing.ClaimFor("Doc.Number", "numb"));
        Assert.Null(language.Typing.ClaimFor("Doc.Number", "Num"));
    }

    [Fact]
    public void ARuleThatClaimsAnAliasTheConceptDoesNotDeclareIsRefused()
    {
        using var folder = new TemporaryDirectory();

        WriteStructure(folder.Root);
        WriteRule(folder.Root, name: "number", conceptKey: "Doc.Number", claims: TypingClaims.Alias, property: "value");

        var language = LanguageProject.Open(folder.Root, out var error);

        Assert.Null(language);
        Assert.Contains("declares none", error, StringComparison.Ordinal);
    }

    [Fact]
    public void AnExtendedLanguagesTypingRulesAreInherited()
    {
        // `extends` is the strong claim: the extending language is the base language plus more, so the base's rules
        // about a shared concept are in force in its document.
        using var coreFolder = new TemporaryDirectory();
        using var dependentFolder = new TemporaryDirectory();

        WriteNumber(coreFolder.Root);
        WriteRule(coreFolder.Root, name: "number", conceptKey: "Doc.Number", claims: TypingClaims.Digits, property: "value");
        WriteRoot(coreFolder.Root, "Core");

        WriteOtherStructure(dependentFolder.Root);
        WriteRoot(dependentFolder.Root, "Rules", extends: "Core");

        var core = LanguageProject.Open(coreFolder.Root, out var coreError);

        Assert.Null(coreError);
        Assert.NotNull(core);

        var language = LanguageProject.Open(dependentFolder.Root, new Answers(core), out var error);

        Assert.Null(error);
        Assert.NotNull(language);
        Assert.True(language.HasTyping);
        Assert.Equal("value", language.Typing.ClaimFor("Doc.Number", "42")?.PropertySlot);
        Assert.Equal(["Core"], language.Inherited.Select(inherited => inherited.Name));
    }

    [Fact]
    public void ADependedOnLanguagesTypingRulesAreNotInherited()
    {
        // A dependency lends the vocabulary and nothing else: the concept may appear in a document, and the rules the
        // base states about it are the base's own.
        using var coreFolder = new TemporaryDirectory();
        using var dependentFolder = new TemporaryDirectory();

        WriteNumber(coreFolder.Root);
        WriteRule(coreFolder.Root, name: "number", conceptKey: "Doc.Number", claims: TypingClaims.Digits, property: "value");
        WriteRoot(coreFolder.Root, "Core");

        WriteOtherStructure(dependentFolder.Root);
        WriteRoot(dependentFolder.Root, "Rules", dependency: "Core");

        var core = LanguageProject.Open(coreFolder.Root, out _)!;
        var language = LanguageProject.Open(dependentFolder.Root, new Answers(core), out var error);

        Assert.Null(error);
        Assert.NotNull(language);
        Assert.False(language.HasTyping);
        Assert.Null(language.Typing.ClaimFor("Doc.Number", "42"));

        // The vocabulary still resolved, which is the difference the two lists make.
        Assert.Equal(["Core"], language.Resolved.Select(resolved => resolved.Name));
        Assert.Empty(language.Inherited);
        Assert.True(language.Concepts.TryGet("Doc.Number", out _));
    }

    [Fact]
    public void TheLanguagesOwnTypingRuleWinsOverTheInheritedOne()
    {
        // Two rules for one concept and one burst of text: the language whose document this is wins, and its base
        // is behind it.
        using var coreFolder = new TemporaryDirectory();
        using var dependentFolder = new TemporaryDirectory();

        WriteNumber(coreFolder.Root);
        WriteRule(coreFolder.Root, name: "number", conceptKey: "Doc.Number", claims: TypingClaims.Digits, property: "value");
        WriteRoot(coreFolder.Root, "Core");

        WriteOtherStructure(dependentFolder.Root);
        WriteRule(dependentFolder.Root, name: "number", conceptKey: "Doc.Number", claims: TypingClaims.Digits, property: "other");
        WriteRoot(dependentFolder.Root, "Rules", extends: "Core");

        var core = LanguageProject.Open(coreFolder.Root, out _)!;
        var language = LanguageProject.Open(dependentFolder.Root, new Answers(core), out var error);

        Assert.Null(error);
        Assert.Equal("other", language!.Typing.ClaimFor("Doc.Number", "42")?.PropertySlot);
    }

    /// <summary>The host's answer: the languages it was given, by name.</summary>
    private sealed class Answers(params LanguageProject[] languages) : ILanguageResolver
    {
        public LanguageProject? Find(string name) =>
            languages.FirstOrDefault(language => string.Equals(language.Name, name, StringComparison.Ordinal));
    }

    /// <summary>A second concept, so the extending language declares something of its own.</summary>
    private static void WriteOtherStructure(string directory)
    {
        var definition = new DefinitionBuilder();

        definition.Concept("Doc.Text", slots: [definition.Property("value", "string")], nameSlot: "value");

        var structure = Path.Combine(directory, LanguageProject.StructureFolder);

        Directory.CreateDirectory(structure);
        ModelFile.Save(definition.Documents[0], Path.Combine(structure, "Doc.Text.mps"));
    }

    /// <summary>A concept with two properties, so a rule's property slot can tell two rules apart.</summary>
    private static void WriteNumber(string directory)
    {
        var definition = new DefinitionBuilder();

        definition.Concept(
            "Doc.Number",
            slots: [definition.Property("value", "string"), definition.Property("other", "string")],
            nameSlot: "value");

        var structure = Path.Combine(directory, LanguageProject.StructureFolder);

        Directory.CreateDirectory(structure);
        ModelFile.Save(definition.Documents[0], Path.Combine(structure, "Doc.Number.mps"));
    }

    /// <summary>The language root, saying what it stands on.</summary>
    private static void WriteRoot(string directory, string name, string? extends = null, string? dependency = null)
    {
        var model = StructureLanguage.NewModel("language");
        var root = model.NewNode(StructureConceptKeys.Language);

        root.Set("name", name);

        if (extends is not null)
        {
            model.NewChild(root, StructureLanguage.ExtendsSlot, StructureConceptKeys.Dependency).Set("name", extends);
        }

        if (dependency is not null)
        {
            model.NewChild(root, StructureLanguage.DependenciesSlot, StructureConceptKeys.Dependency).Set("name", dependency);
        }

        ModelFile.Save(model, Path.Combine(directory, LanguageProject.LanguageFile));
    }

    private static void WriteStructure(string directory, string? alias = null)
    {
        var definition = new DefinitionBuilder();

        definition.Concept(
            "Doc.Number",
            slots: [definition.Property("value", "string")],
            nameSlot: "value",
            alias: alias);

        var structure = Path.Combine(directory, LanguageProject.StructureFolder);

        Directory.CreateDirectory(structure);

        ModelFile.Save(definition.Documents[0], Path.Combine(structure, "Doc.Number.mps"));
    }

    private static void WriteRule(string directory, string name, string conceptKey, string claims, string? property = null)
    {
        var model = TypingLanguage.NewModel(name);
        var rule = model.Roots[0];

        rule.Set(TypingLanguage.ConceptKeySlot, conceptKey);
        rule.Set(TypingLanguage.ClaimsSlot, claims);

        if (property is not null)
        {
            rule.Set(TypingLanguage.PropertySlotSlot, property);
        }

        var typing = Path.Combine(directory, LanguageProject.TypingFolder);

        Directory.CreateDirectory(typing);
        ModelFile.Save(model, Path.Combine(typing, $"{name}.mps"));
    }
}

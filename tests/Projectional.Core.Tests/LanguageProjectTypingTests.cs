using Projectional.Core;
using Projectional.Language;
using Projectional.Languages;
using Projectional.Persistence;
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

    private static void WriteStructure(string directory)
    {
        var definition = new DefinitionBuilder();

        definition.Concept("Doc.Number", slots: [definition.Property("value", "string")], nameSlot: "value");

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

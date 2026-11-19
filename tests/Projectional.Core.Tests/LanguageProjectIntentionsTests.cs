using Projectional.Core;
using Projectional.IntentionRules;
using Projectional.Languages;
using Projectional.Persistence;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// A language folder declares intentions: the documents load, lower, and arrive as the provider a session registers.
/// </summary>
/// <remarks>
/// What an entry does is proven in <see cref="IntentionLoweringTests"/>; this file proves the folder - the file order,
/// the folder name, and that a rule the language cannot honour is refused at load rather than at the caret.
/// </remarks>
public sealed class LanguageProjectIntentionsTests
{
    [Fact]
    public void AnIntentionsFolderBecomesTheLanguagesProvider()
    {
        using var folder = new TemporaryDirectory();

        WriteStructure(folder.Root);
        WriteIntention(folder.Root, conceptKey: "Doc.Thing");

        var language = LanguageProject.Open(folder.Root, out var error);

        Assert.Null(error);
        Assert.NotNull(language);

        Assert.True(language.HasIntentions);
        Assert.NotNull(language.Intentions);
    }

    [Fact]
    public void AWrapTheLanguageCannotHonourIsRefusedWhenTheFolderLoads()
    {
        using var folder = new TemporaryDirectory();

        WriteStructure(folder.Root);
        WriteIntention(folder.Root, conceptKey: "Doc.Box", wrapper: "Doc.Box", wraps: "inner");

        // A box cannot hold a box, so the rule could never run - and the folder is the earliest moment both halves
        // exist.
        var language = LanguageProject.Open(folder.Root, out var error);

        Assert.Null(language);
        Assert.Contains("could never run", error, StringComparison.Ordinal);
    }

    private static void WriteStructure(string directory)
    {
        var definition = new DefinitionBuilder();

        definition.Concept("Doc.Thing", slots: [definition.Property("name", "string")], nameSlot: "name");

        definition.Concept(
            "Doc.Box",
            slots: [definition.Child("inner", "Doc.Thing"), definition.Property("name", "string")],
            nameSlot: "name");

        var structure = Path.Combine(directory, LanguageProject.StructureFolder);

        Directory.CreateDirectory(structure);

        ModelFile.Save(definition.Documents[0], Path.Combine(structure, "Doc.Thing.mps"));
        ModelFile.Save(definition.Documents[1], Path.Combine(structure, "Doc.Box.mps"));
    }

    private static void WriteIntention(string directory, string conceptKey, string wrapper = "Doc.Box", string wraps = "inner")
    {
        var model = IntentionLanguage.NewModel("wrap");
        var rule = model.Roots[0];

        rule.Set(IntentionLanguage.ConceptKeySlot, conceptKey);
        rule.Set(IntentionLanguage.LabelSlot, "Put in a box");
        rule.Set(IntentionLanguage.DescriptionSlot, "Surrounds this thing with a box.");

        var action = Assert.Single(rule.GetChildren(IntentionLanguage.ActionSlot));

        action.Set(IntentionLanguage.WrapperSlot, wrapper);
        action.Set(IntentionLanguage.WrapperChildSlot, wraps);

        var intentions = Path.Combine(directory, LanguageProject.IntentionsFolder);

        Directory.CreateDirectory(intentions);
        ModelFile.Save(model, Path.Combine(intentions, "wrap.mps"));
    }
}

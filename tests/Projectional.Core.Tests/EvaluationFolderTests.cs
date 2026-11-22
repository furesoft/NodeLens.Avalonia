using Projectional.Core;
using Projectional.EvaluationRules;
using Projectional.Language;
using Projectional.Languages;
using Projectional.Persistence;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>A language folder that states what its concepts mean, read back by <see cref="LanguageProject.Open"/>.</summary>
/// <remarks>
/// The folder is a real one, copied and extended rather than synthesised: writing a structure document by hand would test
/// the test's idea of the format instead of the reader. What the test proves is the wiring nobody can see from the
/// lowering tests - that <c>evaluation/</c> is read at all, that the rule reaches the project, and that a folder with no
/// such folder stays honestly empty.
/// </remarks>
public sealed class EvaluationFolderTests
{
    [Fact]
    public void TheRulesOfALanguageFolderReachTheProject()
    {
        var directory = Copy("Units");

        try
        {
            WriteRule(directory, "Units.Quantity", "value");

            var language = Open(directory);

            var registry = Assert.IsType<EvaluationRegistry>(language.Evaluation);

            Assert.Equal(1, registry.Count);
            Assert.NotNull(registry.RuleFor(language.Concepts.Get("Units.Quantity")));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void ALanguageFolderWithoutRulesStatesNoMeaning()
    {
        var directory = Copy("Units");

        try
        {
            // Null rather than an empty registry: "this language states no meaning" is what the page and a host report, and
            // an empty registry would say "it states these none".
            Assert.Null(Open(directory).Evaluation);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Copies the test data folder <paramref name="name"/> into a directory of its own, without the rules it ships.</summary>
    /// <remarks>
    /// <c>Units</c> states a meaning of its own now, so its <c>evaluation</c> folder is taken away before a test writes one:
    /// what these two tests are about is the reader, not what the sample happens to ship today.
    /// </remarks>
    private static string Copy(string name)
    {
        var source = Path.Combine(AppContext.BaseDirectory, name);
        var target = Path.Combine(Path.GetTempPath(), $"projectional-{name}-{Guid.NewGuid():N}");

        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var destination = Path.Combine(target, Path.GetRelativePath(source, file));

            _ = Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination);
        }

        var evaluation = Path.Combine(target, LanguageProject.EvaluationFolder);

        if (Directory.Exists(evaluation))
        {
            Directory.Delete(evaluation, recursive: true);
        }

        return target;
    }

    /// <summary>Writes one rule for <paramref name="conceptKey"/> into the folder's <c>evaluation</c> directory.</summary>
    private static void WriteRule(string directory, string conceptKey, string slot)
    {
        var model = new Project(EvaluationRuleLanguage.Concepts).NewModel("rule");
        var rule = model.NewNode(EvaluationRuleConceptKeys.Rule);
        rule.Set(EvaluationRuleLanguage.ConceptKeySlot, conceptKey);

        model.NewChild(rule, EvaluationRuleLanguage.BodySlot, EvaluationRuleConceptKeys.Property)
            .Set(EvaluationRuleLanguage.SlotSlot, slot);

        model.AddRoot(rule);

        var folder = Path.Combine(directory, LanguageProject.EvaluationFolder);
        _ = Directory.CreateDirectory(folder);
        ModelFile.Save(model, Path.Combine(folder, "quantity.mps"));
    }

    private static LanguageProject Open(string directory) =>
        LanguageProject.Open(directory, resolver: null, out var error)
            ?? throw new InvalidOperationException(error ?? "the folder did not open, and said nothing about why.");
}

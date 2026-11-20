using Projectional.Core;
using Projectional.Language;
using Projectional.Languages;
using Projectional.Persistence;
using Projectional.Semantics;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>A declared language reading back what its own generator wrote.</summary>
/// <remarks>
/// The importer is the inverse of the templates, so these tests use a real generator from a project folder rather
/// than a hand-built one: what is being pinned is that one declaration serves both directions.
/// </remarks>
public sealed class TemplateImporterTests
{
    [Fact]
    public void ADeclaredGeneratorReadsBackWhatItWrites()
    {
        var language = Open();
        var generator = Assert.Single(language.Generators);
        var importer = language.Importers.Single(candidate => candidate.Format == generator.Name);

        Assert.True(language.TryCreateImporter(generator.Name, out var named, out var error), error);
        Assert.NotNull(named);

        var source = ModelFile.Load(language.NewProject(), Path.Combine(Folder(), "pipeline.mps"));
        var written = new Generator().Generate(source.Roots[0], generator).Text;

        Assert.Equal("# Release\ndotnet build\ndotnet test", written);

        var target = language.NewProject().NewModel("imported");
        var result = importer.Import(target, written);

        Assert.True(result.Succeeded, Describe(result));

        // The promise is the round trip: the text a generator writes is the text it reads.
        Assert.Equal(written, new Generator().Generate(result.Roots[0], generator).Text);

        var stage = result.Roots[0].GetChildren("stages")[0];

        Assert.Equal("Release", result.Roots[0].GetOrNull("name"));
        Assert.Equal("dotnet build", stage.GetOrNull("command"));
    }

    [Fact]
    public void ReadingTheSameTextTwiceGivesOneUndoStepEachTime()
    {
        var language = Open();
        var importer = language.Importers.Single(candidate => candidate.Format == "ShellScript");
        var model = language.NewProject().NewModel("imported");

        var first = importer.Import(model, "# Release\ndotnet build");
        var second = importer.Import(model, "# Nightly\ndotnet pack");

        Assert.True(first.Succeeded, Describe(first));
        Assert.True(second.Succeeded, Describe(second));
        Assert.Equal(2, model.Roots.Count);
        Assert.Equal("Nightly", model.Roots[1].GetOrNull("name"));
    }

    [Fact]
    public void TextThatIsNotTheGeneratorsOutputNamesThePlaceItStopped()
    {
        var language = Open();
        var importer = language.Importers.Single(candidate => candidate.Format == "ShellScript");
        var model = language.NewProject().NewModel("imported");

        var result = importer.Import(model, "deploy everything\n");

        Assert.False(result.Succeeded);
        Assert.Empty(result.Roots);

        var diagnostic = Assert.Single(result.Diagnostics);

        Assert.Equal(TemplateImporter.UnexpectedText, diagnostic.Code);
        Assert.Equal(1, diagnostic.Line);
        Assert.Equal(1, diagnostic.Column);
        Assert.Empty(model.Roots);
    }

    [Fact]
    public void AGeneratorThatWritesAParentsPropertyCannotBeReadBack()
    {
        // The child's text carries a value the child does not own, so there is nothing to fill from the file. The
        // refusal is the whole generator's rather than a per-file surprise.
        var error = Assert.Throws<ConceptDefinitionException>(() => Build(
            TextTemplate.Of(new TextPart("- "), new ParentPart("name"), new PropertyPart("detail"))));

        Assert.Contains("cannot be read back", error.Message, StringComparison.Ordinal);
        Assert.Contains("holding node's property", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AChoiceWhoseBranchesBeginAlikeIsRefusedWhenTheImporterIsBuilt()
    {
        var error = Assert.Throws<ConceptDefinitionException>(() => Build(
            TextTemplate.Of(new WhenFilledPart(
                "detail",
                TextTemplate.Of(new TextPart("- "), new PropertyPart("detail")),
                TextTemplate.Of(new TextPart("- "), new TextPart("nothing"))))));

        Assert.Contains("cannot be read back", error.Message, StringComparison.Ordinal);
        Assert.Contains("cannot be told", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AChoiceWithNoBranchThatBeginsWithTextIsRefused()
    {
        // Neither branch can be chosen from the file, so the importer would have to guess.
        var error = Assert.Throws<ConceptDefinitionException>(() => Build(
            TextTemplate.Of(new WhenFilledPart(
                "detail",
                TextTemplate.Of(new PropertyPart("detail")),
                TextTemplate.Of(new PropertyPart("name"))))));

        Assert.Contains("no text at all", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AskingForAGeneratorTheLanguageDoesNotDeclareSaysSo()
    {
        var language = Open();

        Assert.False(language.TryCreateImporter("Nope", out var importer, out var error));

        Assert.Null(importer);
        Assert.Contains("no generator 'Nope'", error, StringComparison.Ordinal);
    }

    private static TemplateImporter Build(TextTemplate template)
    {
        var concepts = ConceptRegistry.Create(
        [
            new ConceptDeclaration(
                "Item",
                [
                    new PropertySlot("name", PropertyType.String),
                    new ChildSlot("detail", "Item", Cardinality.One),
                ]),
        ]);

        return new TemplateImporter(
            "shell",
            new TextGenerator("shell", TemplateRegistry.Empty.Add("Item", template)),
            concepts,
            ["Item"]);
    }

    private static LanguageProject Open() =>
        LanguageProject.Open(Folder(), out var error) is { } language && error is null
            ? language
            : throw new InvalidOperationException(error);

    private static string Folder() => Path.Combine(AppContext.BaseDirectory, "Declared");

    private static string Describe(ImportResult result) =>
        string.Join(Environment.NewLine, result.Diagnostics.Select(diagnostic => diagnostic.ToString()));
}

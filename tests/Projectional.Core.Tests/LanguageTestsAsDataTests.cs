using Projectional.Core;
using Projectional.Languages;
using Projectional.Projection;
using Projectional.Testing;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The door from the repository's own suite onto a language's <c>tests/</c> folder.
/// </summary>
/// <remarks>
/// <para>
/// A language's tests are documents, so the suite needs one bridge and not one test class per language: this runs
/// the folder and reports the rows. A language that ships a broken test fails here, naming the document, and a
/// language that ships none passes with nothing to say.
/// </para>
/// <para>
/// The sample's tests are copied to the output beside the documents they assert about, so the paths inside a test
/// document are the same paths the sample's own documents use.
/// </para>
/// </remarks>
public sealed class LanguageTestsAsDataTests
{
    private static LanguageProject Open(string folder)
    {
        var language = LanguageProject.Open(Path.Combine(AppContext.BaseDirectory, folder), out var error);

        Assert.Null(error);
        Assert.NotNull(language);

        return language;
    }

    [Fact]
    public void ThePipelineSamplePassesTheTestsItShips()
    {
        var report = TestRunner.Run(Open("Declared"));

        Assert.True(report.Ran > 0, "the sample ships a tests folder with at least one document");
        Assert.True(report.Succeeded, report.ToString());
    }

    [Fact]
    public void ALanguageWithoutATestsFolderReportsNothing()
    {
        var report = TestRunner.Run(Open("Workflow"));

        Assert.Equal(0, report.Ran);
        Assert.True(report.Succeeded);
    }

    [Fact]
    public void ADocumentThatIsGoneIsAFailedRow()
    {
        var folder = Copy(Open("Declared"));

        try
        {
            File.WriteAllText(
                Path.Combine(folder, "tests", "missing-document.mps"),
                """
                <?xml version="1.0" encoding="utf-8"?>
                <model formatVersion="1" id="3f0f9dbb-6a4b-4d7f-9c1e-1b7c2f0a5d31" name="missing-document">
                  <nodes>
                    <node id="2c1d8f0e-5b3a-4c9d-8e7f-0a1b2c3d4e5f" concept="Testing.Test" name="gone.mps">
                      <properties>
                        <document>gone.mps</document>
                      </properties>
                      <children role="assertion">
                        <node id="9a8b7c6d-5e4f-4a3b-8c2d-1e0f9a8b7c6d" concept="Testing.RoundTrips" />
                      </children>
                    </node>
                  </nodes>
                </model>
                """);

            var report = TestRunner.Run(LanguageProject.Open(folder, out var error)!);

            Assert.Null(error);
            Assert.Equal(2, report.Ran);
            Assert.Equal(1, report.Failed);

            var failed = report.Results.Single(result => !result.Succeeded);

            Assert.Equal("missing-document", failed.Test);
            Assert.Equal("gone.mps", failed.Document);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void TheTestDocumentDrawsAsASentence()
    {
        var model = TestingLanguage.NewModel();
        var test = model.Roots[0];

        test.Set(TestingLanguage.DocumentSlot, "pipeline.mps");
        model.NewChild(test, TestingLanguage.AssertionSlot, TestingConceptKeys.RoundTrips);

        Assert.Equal(
            "test pipeline.mps round trips",
            LayoutSnapshot.Render(model, TestingNotations.Registry, width: 80));
    }

    [Fact]
    public void ATestWithoutADocumentIsRefused()
    {
        var model = TestingLanguage.NewModel();
        var test = model.Roots[0];

        model.NewChild(test, TestingLanguage.AssertionSlot, TestingConceptKeys.RoundTrips);

        var refused = Assert.Throws<ConceptDefinitionException>(() => TestingLowering.Lower(test));

        Assert.Contains("document", refused.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ATestWithoutAnAssertionIsRefused()
    {
        var model = TestingLanguage.NewModel();
        var test = model.Roots[0];

        test.Set(TestingLanguage.DocumentSlot, "pipeline.mps");

        var refused = Assert.Throws<ConceptDefinitionException>(() => TestingLowering.Lower(test));

        Assert.Contains("assertion", refused.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ADocumentThatIsNotATestIsRefused()
    {
        var document = new Project(TestingLanguage.Concepts).NewModel("not-a-test");

        document.NewNode(TestingConceptKeys.RoundTrips);

        var refused = Assert.Throws<ConceptDefinitionException>(() => TestingLowering.Lower(document.Roots[0]));

        Assert.Contains("not a test", refused.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static string Copy(LanguageProject language)
    {
        var destination = Directory.CreateTempSubdirectory("language-tests").FullName;

        foreach (var file in Directory.EnumerateFiles(language.Directory, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(language.Directory, file);
            var target = Path.Combine(destination, relative);

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }

        return destination;
    }
}

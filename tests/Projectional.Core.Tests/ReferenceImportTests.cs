using Projectional.Json;
using Projectional.Projection;
using Projectional.Workbench;
using Projectional.Xml;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>Importing a file as a document: what comes out, and what is left when the text is refused.</summary>
/// <remarks>
/// The shell's own half of text import. Everything the model is made of is the importer's business and is pinned in the
/// language's own tests; what is pinned here is the shell's promises - the document is named after the file, it is not
/// linked to it, it starts clean, and a refused file leaves no tab behind.
/// </remarks>
public sealed class ReferenceImportTests
{
    [Fact]
    public void AJsonFileBecomesADocumentOfJson()
    {
        var outcome = ReferenceImport.Import(ReferenceLanguages.Json, "settings", "{ \"a\": 1 }");

        Assert.True(outcome.Succeeded);
        Assert.Equal("settings", outcome.Model.Name);
        Assert.Equal(JsonConceptKeys.Object, Assert.Single(outcome.Model.Roots).Concept.Key);
        Assert.Same(ReferenceLanguages.Json, outcome.Language);
    }

    [Fact]
    public void AnXmlFileBecomesADocumentOfXml()
    {
        var outcome = ReferenceImport.Import(ReferenceLanguages.Xml, "feed", "<feed><item/></feed>");

        Assert.True(outcome.Succeeded);
        Assert.Equal(XmlConceptKeys.Element, Assert.Single(outcome.Model.Roots).Concept.Key);
    }

    [Fact]
    public void ARefusedFileLeavesNoDocumentAndNamesItsPlace()
    {
        var outcome = ReferenceImport.Import(ReferenceLanguages.Json, "broken", "{ \"a\": 1, }");

        Assert.False(outcome.Succeeded);
        Assert.Empty(outcome.Model.Roots);

        var refusal = outcome.Refusal;

        Assert.NotNull(refusal);
        Assert.False(string.IsNullOrWhiteSpace(refusal.Code));
        Assert.True(refusal.Line >= 1);
        Assert.True(refusal.Column >= 1);
    }

    [Fact]
    public void TheImportedDocumentIsCleanAndNotLinkedToItsFile()
    {
        // "Imported" is not "opened": nothing has been edited, so the tab starts clean, and there is no file to save
        // back to - saving writes a .mps document or the generated text, never the file it came from.
        var outcome = ReferenceImport.Import(ReferenceLanguages.Json, "settings", "{ \"a\": 1 }");
        var language = outcome.Language;

        using var tab = new DocumentTab(
            outcome.Model,
            language.Notations,
            MonospaceTextMeasure.Instance,
            constraints: Projectional.Semantics.ConstraintSet.Empty,
            intentions: language.Intentions,
            generators: language.Generators,
            checks: language.Checks);

        Assert.False(tab.IsDirty);
        Assert.Null(tab.FilePath);
        Assert.Equal("settings", tab.Title);
        Assert.Same(language.Checks, tab.Checks);
        Assert.Same(language.Generators, tab.Generators);
    }
}

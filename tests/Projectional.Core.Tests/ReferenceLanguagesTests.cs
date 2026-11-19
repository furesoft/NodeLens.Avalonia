using Projectional.Workbench;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>Which reference language a file belongs to, and that each one brings everything a document needs.</summary>
/// <remarks>
/// The app has one language for the documents it composes itself, so the reference formats are the first documents whose
/// language is not the app's - which makes "is this file JSON or XML" and "does that language bring its notation,
/// generator, intentions and checks" shell questions rather than library ones.
/// </remarks>
public sealed class ReferenceLanguagesTests
{
    [Fact]
    public void TheTextDecidesBeforeTheFileName()
    {
        // Both importers answer a cheap shape question and the two cannot both be yes, so the text is the better
        // witness: a file named for one format that holds the other is imported as what it holds.
        Assert.Same(ReferenceLanguages.Json, ReferenceLanguages.Detect("misnamed.xml", "{ \"a\": 1 }"));
        Assert.Same(ReferenceLanguages.Xml, ReferenceLanguages.Detect("misnamed.json", "<a/>"));
    }

    [Fact]
    public void TheFileNameDecidesWhenTheTextSaysNothing()
    {
        // An empty file starts like neither shape, so the extension is the only answer left - and it is the one a user
        // expects a picker to honour.
        Assert.Same(ReferenceLanguages.Json, ReferenceLanguages.Detect("settings.json", "   "));
        Assert.Same(ReferenceLanguages.Xml, ReferenceLanguages.Detect("settings.XML", ""));
    }

    [Fact]
    public void AFileNoFormatClaimsIsNotDetected()
    {
        Assert.Null(ReferenceLanguages.Detect("notes.txt", "just prose"));
        Assert.Null(ReferenceLanguages.Detect(fileName: null, "just prose"));
    }

    [Fact]
    public void EveryConcreteConceptOfAReferenceLanguageHasANotation()
    {
        // A registered concept without a notation draws as the missing-notation cell, which is the one state a user
        // cannot work around - so a concept added to either format has to bring its notation in the same commit.
        foreach (var language in ReferenceLanguages.All)
        {
            foreach (var concept in language.Concepts.Concepts.Where(concept => !concept.IsAbstract))
            {
                Assert.True(
                    language.Notations.TryGet(concept.Key, out _),
                    $"{language.Name} declares '{concept.Key}' with no notation.");
            }
        }
    }

    [Fact]
    public void EveryReferenceLanguageBringsItsOwnBehaviour()
    {
        // The rule the reference languages follow: a format's generator, intentions and checks are its own, so nothing
        // about a JSON document is decided by a package that does not know it is looking at JSON.
        foreach (var language in ReferenceLanguages.All)
        {
            Assert.NotEmpty(language.Generators);
            Assert.NotNull(language.Intentions);
            Assert.NotEmpty(language.Checks.Checks);
            Assert.Equal(language.Format, language.Importer.Format);
        }
    }

    [Fact]
    public void TheThreeShippedFormatsAreJsonXmlAndToml()
    {
        Assert.Equal(["JSON", "XML", "TOML"], ReferenceLanguages.All.Select(language => language.Name));
    }

    [Fact]
    public void ATomlFileIsClaimedByItsEqualsSign()
    {
        // A pair answers for TOML on its own, and the extension decides when the text starts like more than one format.
        Assert.Same(ReferenceLanguages.Toml, ReferenceLanguages.Detect("settings.toml", "port = 8080"));
        Assert.Same(ReferenceLanguages.Toml, ReferenceLanguages.Detect("settings.toml", "[server]"));

        // A header looks like the start of a JSON array to a shape question, so without a name the first claimant wins -
        // the refusal that follows is the importer's answer, not a wrong document.
        Assert.Same(ReferenceLanguages.Json, ReferenceLanguages.Detect(fileName: null, "[server]"));

        Assert.False(ReferenceLanguages.Toml.Importer.CanImport("port: 8080"));
        Assert.True(ReferenceLanguages.Toml.Importer.CanImport("  port = 8080"));
    }
}

using Projectional.Persistence;
using Xunit;

namespace Projectional.Web.Tests;

public sealed class WebLanguagesTests
{
    [Fact]
    public void EveryOfferedLanguageProjectsItsExampleWithoutACellThatHasNoNotation()
    {
        // Four code-declared languages and the six declared samples under samples/.
        Assert.True(WebLanguages.All.Count >= 10, $"Only {WebLanguages.All.Count} languages were offered.");

        foreach (var language in WebLanguages.All)
        {
            using var document = new EditorDocument("id", language.Name, language, language.NewSample());
            var html = EditorMarkup.Canvas(document, new CellMetrics(9, 18));

            Assert.Contains("pn-canvas", html, StringComparison.Ordinal);
            Assert.DoesNotContain("pn-missing", html, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void EveryExampleRoundTripsByteIdentically()
    {
        foreach (var language in WebLanguages.All)
        {
            var first = ModelXml.Serialize(language.NewSample());
            var second = ModelXml.Serialize(ModelXml.Deserialize(language.NewProject(), first));

            Assert.Equal(first, second);
        }
    }

    [Fact]
    public void AnUnknownStoredLanguageFallsBackInsteadOfFailingTheRestore()
    {
        Assert.Same(WebLanguages.Rules, WebLanguages.ByName("Rules"));
        Assert.Same(WebLanguages.StateMachine, WebLanguages.ByName("StateMachine"));
        Assert.Same(WebLanguages.Rules, WebLanguages.ByName("NoSuchLanguage"));
        Assert.Same(WebLanguages.Rules, WebLanguages.ByName(null));
    }
}

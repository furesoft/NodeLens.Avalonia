using Xunit;

namespace Projectional.Web.Tests;

public sealed class ConceptBadgeTests
{
    [Theory]
    [InlineData("Rules.Rule", "R")]
    [InlineData("Sm.Machine", "M")]
    [InlineData("Atlas.Place", "P")]
    [InlineData("NoNamespace", "N")]
    [InlineData("", "?")]
    [InlineData(null, "?")]
    public void TheBadgeIsTheConceptsInitialWithoutItsNamespace(string? conceptKey, string expected)
    {
        Assert.Equal(expected, ConceptBadge.LetterOf(conceptKey));
    }

    [Fact]
    public void TheSameConceptOwnsTheSameColourInEveryRun()
    {
        var first = ConceptBadge.ColorOf("Rules.Rule");
        var second = ConceptBadge.ColorOf("Rules.Rule");

        Assert.Equal(first, second);
        Assert.StartsWith("#", first, StringComparison.Ordinal);
    }

    [Fact]
    public void TheColourIsOneOfTheDocumentedPaletteEntries()
    {
        string[] palette =
        [
            "#3F9E52", "#B8912A", "#8467C4", "#C0574B", "#2F9DB8", "#4A79C0", "#D2760A", "#8A6D3B",
        ];

        foreach (var key in new[] { "Rules.Rule", "Sm.Machine", "Atlas.Place", "Units.Quantity", "" })
        {
            Assert.Contains(ConceptBadge.ColorOf(key), palette);
        }
    }

    [Fact]
    public void TwoDifferentConceptsAreNotForcedOntoOneColour()
    {
        var colours = new[]
        {
            ConceptBadge.ColorOf("Rules.Rule"),
            ConceptBadge.ColorOf("Sm.Machine"),
            ConceptBadge.ColorOf("Atlas.Place"),
            ConceptBadge.ColorOf("Units.Quantity"),
        };

        Assert.True(colours.Distinct(StringComparer.Ordinal).Count() > 1, "the palette collapsed onto one colour");
    }
}

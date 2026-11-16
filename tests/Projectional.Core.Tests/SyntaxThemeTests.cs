using Avalonia.Media;
using Avalonia.Styling;

using Projectional.Avalonia;
using Projectional.Projection;

using Xunit;

namespace Projectional.Core.Tests;

public sealed class SyntaxThemeTests
{
    public static TheoryData<TextStyleKind> Kinds()
    {
        var data = new TheoryData<TextStyleKind>();

        foreach (var kind in Enum.GetValues<TextStyleKind>())
        {
            data.Add(kind);
        }

        return data;
    }

    public static TheoryData<bool> Themes() => new() { true, false };

    [Fact]
    public void TheLightThemePutsDarkTextOnALightSurface()
    {
        Assert.True(Luminance(SyntaxTheme.Light.Background) > Luminance(SyntaxTheme.Light.BrushOf(TextStyleKind.Default)));
    }

    [Fact]
    public void TheDarkThemePutsLightTextOnADarkSurface()
    {
        Assert.True(Luminance(SyntaxTheme.Dark.Background) < Luminance(SyntaxTheme.Dark.BrushOf(TextStyleKind.Default)));
    }

    [Theory]
    [MemberData(nameof(Themes))]
    public void TheCaretStandsOutAgainstTheSurface(bool dark)
    {
        var theme = dark ? SyntaxTheme.Dark : SyntaxTheme.Light;

        // A caret the colour of the page is a caret nobody can find, which is why the pairing exists.
        Assert.True(Math.Abs(Luminance(theme.Caret) - Luminance(theme.Background)) > 0.5);
    }

    [Theory]
    [MemberData(nameof(Themes))]
    public void TheHighlightIsTransparentEnoughToReadThrough(bool dark)
    {
        var theme = dark ? SyntaxTheme.Dark : SyntaxTheme.Light;

        // Selected text has to stay legible, so the selection is a wash rather than a fill.
        Assert.True(Opacity(theme.Highlight) < 0x80);
    }

    [Theory]
    [MemberData(nameof(Themes))]
    public void TheChromeIsCloseToThePageButNotTheSameColour(bool dark)
    {
        var theme = dark ? SyntaxTheme.Dark : SyntaxTheme.Light;
        var gap = Math.Abs(Luminance(theme.Chrome) - Luminance(theme.Background));

        // The edge of the document has to be visible, and a dark theme must not flash a light frame.
        Assert.True(gap > 0.0);
        Assert.True(gap < 0.25);
    }

    [Fact]
    public void AThemeKnowsWhetherItIsDark()
    {
        Assert.False(SyntaxTheme.Light.IsDark);
        Assert.True(SyntaxTheme.Dark.IsDark);
    }

    [Fact]
    public void AThemeCarriesTheOverlaysOfItsOwnDarkness()
    {
        // The surfaces painted *over* the page are read against it, so a dark page must not get a white
        // box: this guards the presets against a copy-paste that passes the light overlay to the dark
        // theme. The list palette is not asserted here because it carries vector icons and therefore needs
        // a rendering platform - it resolves lazily, and a probe is what checks it.
        Assert.Same(MarkerTipPalette.Light, SyntaxTheme.Light.Tip);
        Assert.Same(MarkerTipPalette.Dark, SyntaxTheme.Dark.Tip);
        Assert.NotSame(SyntaxTheme.Light.Tip, SyntaxTheme.Dark.Tip);
        Assert.NotSame(SyntaxTheme.Light.Bulb, SyntaxTheme.Dark.Bulb);
    }

    [Fact]
    public void AThemeCarriesOneOfTheShippedPalettes()
    {
        Assert.Same(SyntaxPalette.Light, SyntaxTheme.Light.Palette);
        Assert.Same(SyntaxPalette.Dark, SyntaxTheme.Dark.Palette);
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public void AThemeAnswersForEveryMeaningItsPaletteDoes(TextStyleKind kind)
    {
        Assert.Same(SyntaxTheme.Light.Palette.BrushOf(kind), SyntaxTheme.Light.BrushOf(kind));
        Assert.Same(SyntaxTheme.Dark.Palette.BrushOf(kind), SyntaxTheme.Dark.BrushOf(kind));
    }

    [Fact]
    public void TheDarkVariantPicksTheDarkTheme()
    {
        Assert.Same(SyntaxTheme.Dark, SyntaxTheme.Of(ThemeVariant.Dark));
    }

    [Fact]
    public void TheLightVariantPicksTheLightTheme()
    {
        Assert.Same(SyntaxTheme.Light, SyntaxTheme.Of(ThemeVariant.Light));
    }

    [Fact]
    public void AnUnresolvedVariantPicksTheLightTheme()
    {
        // Default means "whatever the desktop says", which is not an answer. A host that has not resolved
        // it yet gets the theme that is readable on most desktops rather than an exception.
        Assert.Same(SyntaxTheme.Light, SyntaxTheme.Of(ThemeVariant.Default));
    }

    [Fact]
    public void AVariantDerivedFromDarkPicksTheDarkTheme()
    {
        // A host may name its own variant; one that says it inherits from dark means dark. The theme must
        // follow the chain rather than compare with the two shipped variants, or that host gets a light
        // palette on a dark window.
        Assert.Same(SyntaxTheme.Dark, SyntaxTheme.Of(new ThemeVariant("Midnight", ThemeVariant.Dark)));
    }

    [Fact]
    public void AVariantDerivedFromLightPicksTheLightTheme()
    {
        Assert.Same(SyntaxTheme.Light, SyntaxTheme.Of(new ThemeVariant("Sepia", ThemeVariant.Light)));
    }

    private static double Luminance(IBrush brush)
    {
        var colour = Assert.IsAssignableFrom<ISolidColorBrush>(brush).Color;

        return ((0.2126 * colour.R) + (0.7152 * colour.G) + (0.0722 * colour.B)) / 255.0;
    }

    private static int Opacity(IBrush brush) =>
        Assert.IsAssignableFrom<ISolidColorBrush>(brush).Color.A;
}

using Avalonia.Media;

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

    private static double Luminance(IBrush brush)
    {
        var colour = Assert.IsAssignableFrom<ISolidColorBrush>(brush).Color;

        return ((0.2126 * colour.R) + (0.7152 * colour.G) + (0.0722 * colour.B)) / 255.0;
    }

    private static int Opacity(IBrush brush) =>
        Assert.IsAssignableFrom<ISolidColorBrush>(brush).Color.A;
}

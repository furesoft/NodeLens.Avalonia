using Avalonia.Media;
using Avalonia.Media.Immutable;

using Projectional.Avalonia;
using Projectional.Projection;

using Xunit;

namespace Projectional.Core.Tests;

public sealed class SyntaxPaletteTests
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

    [Theory]
    [MemberData(nameof(Kinds))]
    public void ALightPalettePaintsEveryMeaning(TextStyleKind kind)
    {
        Assert.NotNull(SyntaxPalette.Light.BrushOf(kind));
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public void ADarkPalettePaintsEveryMeaning(TextStyleKind kind)
    {
        Assert.NotNull(SyntaxPalette.Dark.BrushOf(kind));
    }

    [Fact]
    public void TheDarkPaletteIsNotTheLightPalette()
    {
        Assert.NotEqual(
            Colour(SyntaxPalette.Light.BrushOf(TextStyleKind.Keyword)),
            Colour(SyntaxPalette.Dark.BrushOf(TextStyleKind.Keyword)));
    }

    [Fact]
    public void AKindThePaletteDoesNotMentionIsPaintedLikePlainText()
    {
        var theme = SyntaxPalette.Create(
        [
            new KeyValuePair<TextStyleKind, IBrush>(TextStyleKind.Default, Brushes.Black),
            new KeyValuePair<TextStyleKind, IBrush>(TextStyleKind.Keyword, Brushes.Blue),
        ]);

        Assert.Same(Brushes.Black, theme.BrushOf(TextStyleKind.Text));
        Assert.Same(Brushes.Blue, theme.BrushOf(TextStyleKind.Keyword));
    }

    [Fact]
    public void APaletteIsAmendedByProducingAnotherOne()
    {
        var amended = SyntaxPalette.Light.With(TextStyleKind.Keyword, Brushes.Magenta);

        Assert.Same(Brushes.Magenta, amended.BrushOf(TextStyleKind.Keyword));
        Assert.Equal(Color.Parse("#0000C0"), Colour(SyntaxPalette.Light.BrushOf(TextStyleKind.Keyword)));
    }

    [Fact]
    public void APaletteWithoutADefaultColourIsRefused()
    {
        Assert.Throws<ArgumentException>(() => SyntaxPalette.Create(
        [
            new KeyValuePair<TextStyleKind, IBrush>(TextStyleKind.Keyword, Brushes.Blue),
        ]));
    }

    [Fact]
    public void APaletteCannotMentionTheSameKindTwice()
    {
        Assert.Throws<ArgumentException>(() => SyntaxPalette.Create(
        [
            new KeyValuePair<TextStyleKind, IBrush>(TextStyleKind.Default, Brushes.Black),
            new KeyValuePair<TextStyleKind, IBrush>(TextStyleKind.Default, Brushes.White),
        ]));
    }

    [Fact]
    public void APaletteWithAMissingBrushIsRefused()
    {
        Assert.Throws<ArgumentNullException>(() => SyntaxPalette.Create(
        [
            new KeyValuePair<TextStyleKind, IBrush>(TextStyleKind.Default, null!),
        ]));
    }

    private static Color Colour(IBrush brush) => Assert.IsType<ImmutableSolidColorBrush>(brush).Color;
}

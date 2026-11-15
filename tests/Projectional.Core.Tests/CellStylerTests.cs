using Projectional.Core;
using Projectional.Language;
using Projectional.Projection;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// Classification is what turns a cell into something a theme can colour: keywords and punctuation come
/// from the notation, value kinds from the property type, and the three states a slot can be in each get
/// their own style.
/// </summary>
/// <remarks>
/// The cells are constructed directly rather than projected, because this type reads a cell and nothing
/// else - a notation would only obscure which input produced which answer. That the frame passes the
/// right cells through the styler is asserted in <see cref="EditorFrameTests"/>.
/// </remarks>
public sealed class CellStylerTests
{
    private static readonly CellAddress Here = new(NodeId.New());

    [Fact]
    public void AKeywordConstantIsStyledAsAKeyword()
    {
        var cell = new ConstantCell(Here, "ruleset", ConstantKind.Keyword);

        Assert.Equal(TextStyleKind.Keyword, CellStyler.Default.StyleOf(cell));
    }

    [Fact]
    public void PunctuationIsStyledAsPunctuation()
    {
        var cell = new ConstantCell(Here, "{", ConstantKind.Punctuation);

        Assert.Equal(TextStyleKind.Punctuation, CellStyler.Default.StyleOf(cell));
    }

    [Fact]
    public void TheGapBetweenTwoCellsCarriesNoMeaning()
    {
        var cell = new SpaceCell(Here);

        Assert.Equal(TextStyleKind.Default, CellStyler.Default.StyleOf(cell));
    }

    [Theory]
    [InlineData("int")]
    [InlineData("date")]
    public void ALiteralValueIsStyledAsANumber(string propertyType)
    {
        var type = propertyType == "int" ? PropertyType.Integer : PropertyType.Date;
        var cell = new PropertyCell(Here, "amount", type, "42", isSet: true);

        Assert.Equal(TextStyleKind.Number, CellStyler.Default.StyleOf(cell));
    }

    [Fact]
    public void AStringPropertyIsStyledAsText()
    {
        var cell = new PropertyCell(Here, "name", PropertyType.String, "basic", isSet: true);

        Assert.Equal(TextStyleKind.Text, CellStyler.Default.StyleOf(cell));
    }

    [Theory]
    [InlineData("true")]
    [InlineData("false")]
    public void ABooleanReadsAsAWordOfTheLanguage(string text)
    {
        var cell = new PropertyCell(Here, "enabled", PropertyType.Boolean, text, isSet: true);

        Assert.Equal(TextStyleKind.Keyword, CellStyler.Default.StyleOf(cell));
    }

    [Fact]
    public void AnEnumerationSymbolReadsAsAWordOfTheLanguage()
    {
        var cell = new PropertyCell(Here, "op", PropertyType.Enum("+", "-"), "+", isSet: true);

        Assert.Equal(TextStyleKind.Keyword, CellStyler.Default.StyleOf(cell));
    }

    [Fact]
    public void AResolvedReferenceIsStyledAsAReference()
    {
        var cell = new ReferenceCell(Here, "tag", "basic", ReferenceState.Resolved, NodeId.New());

        Assert.Equal(TextStyleKind.Reference, CellStyler.Default.StyleOf(cell));
    }

    [Fact]
    public void AReferenceWhoseTargetIsGoneLooksDifferentFromAWorkingOne()
    {
        var cell = new ReferenceCell(Here, "tag", "<unresolved>", ReferenceState.Unresolved, NodeId.New());

        Assert.Equal(TextStyleKind.Unresolved, CellStyler.Default.StyleOf(cell));
    }

    [Fact]
    public void AnUnsetReferenceIsStyledAsAPlaceholder()
    {
        var cell = new ReferenceCell(Here, "tag", "<tag>", ReferenceState.Unset);

        Assert.Equal(TextStyleKind.Placeholder, CellStyler.Default.StyleOf(cell));
    }

    [Fact]
    public void AnEmptyChildSlotIsStyledAsAPlaceholder()
    {
        var cell = new ChildCell(Here, "extends", "<empty>");

        Assert.Equal(TextStyleKind.Placeholder, CellStyler.Default.StyleOf(cell));
    }

    [Fact]
    public void AnEmptyCollectionIsStyledAsAPlaceholder()
    {
        var cell = new CollectionCell(Here, "notes", "<empty>", [], []);

        Assert.Equal(TextStyleKind.Placeholder, CellStyler.Default.StyleOf(cell));
    }

    [Fact]
    public void AMissingNotationMarkerIsAFaultInTheLanguageAndSays()
    {
        var cell = new MissingNotationCell(Here, "Expr.Unary");

        Assert.Equal(TextStyleKind.Missing, CellStyler.Default.StyleOf(cell));
    }

    [Fact]
    public void AContainerHasNoTextAndThereforeNoStyle()
    {
        var cell = new ContainerCell(Here, CellOrientation.Horizontal, new SpaceCell(Here));

        Assert.Equal(TextStyleKind.Default, CellStyler.Default.StyleOf(cell));
    }

    [Fact]
    public void AStylerRefusesToGuessAboutNothing()
    {
        Assert.Throws<ArgumentNullException>(() => CellStyler.Default.StyleOf(null!));
    }
}

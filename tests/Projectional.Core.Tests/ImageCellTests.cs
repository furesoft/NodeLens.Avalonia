using Projectional.Core;
using Projectional.Editing;
using Projectional.Language;
using Projectional.Projection;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// A picture is not text: the notation names it, layout sizes the box the notation asked for, and the frame hands
/// the host a region and a name. These tests pin those three steps, because the alternative - a glyph typed into a
/// line - is what a projectional editor exists to avoid.
/// </summary>
/// <remarks>
/// Everything is measured by <see cref="MonospaceTextMeasure"/>, so one layout unit is one character and one row and
/// the expected numbers can be counted off the notation by hand.
/// </remarks>
public sealed class ImageCellTests
{
    private static readonly CellLayouter Layouter = new(MonospaceTextMeasure.Instance);

    [Fact]
    public void AnImageWithNoBoxIsAsTallAsTheLine()
    {
        var (layout, image) = Layout(new ImageNotationCell("key"));

        AssertRect(0, 0, 1, 1, Bounds(layout, image));
    }

    [Fact]
    public void AnImageKeepsTheBoxItsNotationAskedFor()
    {
        var (layout, image) = Layout(new ImageNotationCell("logo", width: 6, height: 3));

        AssertRect(0, 0, 6, 3, Bounds(layout, image));
        Assert.Equal(6, layout.Size.Width);
        Assert.Equal(3, layout.Size.Height);
    }

    [Fact]
    public void AnImageSitsInTheLineAndPushesWhatFollowsByItsWidth()
    {
        var node = TestLanguage.NewModel().NewNode(TestLanguage.Rule);
        var address = new CellAddress(node.Id);

        var image = new ImageCell(address, "key", width: 2, height: 1);
        var text = new ConstantCell(address, "ab", ConstantKind.Keyword);

        var layout = Layouter.Layout(new EditorTree(node, new ContainerCell(address, CellOrientation.Horizontal, image, text)));

        AssertRect(0, 0, 2, 1, Bounds(layout, image));
        AssertRect(2, 0, 2, 1, Bounds(layout, text));
    }

    [Fact]
    public void AnImageDrawsNoTextSoTheTextGridLeavesItsRoomInsteadOfAGlyph()
    {
        var (layout, image) = Layout(new ImageNotationCell("key", width: 4, height: 1));

        Assert.Equal(string.Empty, CellText.Of(image));
        Assert.False(CellText.IsVisible(image));

        // The room is real: a picture of four units keeps four columns, so the text grid shows the gap the reader
        // sees in the editor rather than collapsing the line around an image it cannot draw.
        Assert.Equal(4, layout.Size.Width);
    }

    [Fact]
    public void TheFrameCarriesThePictureAsARegionAndAKey()
    {
        var (layout, image) = Layout(new ImageNotationCell("key", width: 2, height: 1));

        var placed = Assert.Single(Frame(layout).Images);

        Assert.Same(image, placed.Cell);
        Assert.Equal("key", placed.Key);
        Assert.Equal(Bounds(layout, image), placed.Bounds);
    }

    [Fact]
    public void ADocumentWithNoPictureCarriesNone()
    {
        var node = TestLanguage.NewModel().NewNode(TestLanguage.Rule);
        var address = new CellAddress(node.Id);

        var layout = Layouter.Layout(
            new EditorTree(node, new ContainerCell(address, CellOrientation.Vertical, new ConstantCell(address, "x", ConstantKind.Keyword))));

        Assert.Empty(Frame(layout).Images);
    }

    [Fact]
    public void AnImageCellRefusesAKeyThatNamesNothing()
    {
        Assert.Throws<ArgumentException>(() => new ImageNotationCell(""));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ImageNotationCell("key", width: -1));
    }

    private static (CellLayout Layout, EditorCell Image) Layout(ImageNotationCell notation)
    {
        var node = TestLanguage.NewModel().NewNode(TestLanguage.Rule);
        var address = new CellAddress(node.Id);

        var image = new ImageCell(address, notation.Key, notation.Width, notation.Height);
        var line = new ContainerCell(address, CellOrientation.Horizontal, image);

        return (Layouter.Layout(new EditorTree(node, line)), image);
    }

    private static EditorFrame Frame(CellLayout layout) =>
        EditorFrame.Build(
            layout,
            new LayoutSize(layout.Size.Width, layout.Size.Height),
            0,
            0,
            Selection.At(CaretPosition.At(layout.Tree.Root.Address)),
            MonospaceTextMeasure.Instance);

    private static LayoutRect Bounds(CellLayout layout, EditorCell cell) =>
        layout.BoundsOf(cell) ?? throw new InvalidOperationException($"The cell {cell} was not placed.");

    private static void AssertRect(double x, double y, double width, double height, LayoutRect actual) =>
        Assert.Equal(new LayoutRect(x, y, width, height), actual);
}

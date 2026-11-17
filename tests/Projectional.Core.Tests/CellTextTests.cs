using Projectional.Core;
using Projectional.Language;
using Projectional.Projection;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// What a cell shows is a single rule shared by the snapshot renderer, the control and the caret, so
/// it is pinned here rather than only observed through any one of them.
/// </summary>
public sealed class CellTextTests
{
    private static readonly EditorTreeBuilder Builder = new(TestNotations.Registry);

    private static readonly CellAddress Here = new(NodeId.New());

    [Fact]
    public void ATextCellAnswersWithItsOwnText()
    {
        var cell = CellOf(TestEditor.BareRule(), "rule");

        Assert.Equal("rule", CellText.Of(cell));
        Assert.True(CellText.IsVisible(cell));
    }

    [Fact]
    public void AnEmptySlotAnswersWithItsPlaceholder()
    {
        var (_, body) = TestEditor.RuleWithBody();
        var notes = Tree(body).Cells().OfType<CollectionCell>().Single(cell => cell.Slot == "notes");

        Assert.True(notes.IsEmpty);
        Assert.Equal(notes.EmptyText, CellText.Of(notes));
        Assert.NotEqual(string.Empty, CellText.Of(notes));
    }

    [Fact]
    public void AnEmptyPropertyAnswersWithThePlaceholderItsNotationAskedFor()
    {
        // The notation says what belongs in the slot, which is the only thing a reader has until someone
        // types a value - a blank of one space tells them nothing.
        var cell = new PropertyCell(Here, "operator", PropertyType.Enum("+", "-"), string.Empty, isSet: false, emptyText: "<<operator>>");

        Assert.Equal("<<operator>>", CellText.Of(cell));
        Assert.True(CellText.IsVisible(cell));
    }

    [Fact]
    public void APropertyThatHoldsAValueShowsTheValueAndNotThePlaceholder()
    {
        var cell = new PropertyCell(Here, "operator", PropertyType.Enum("+", "-"), "+", isSet: true, emptyText: "<<operator>>");

        Assert.Equal("+", CellText.Of(cell));
    }

    [Fact]
    public void APropertyWithoutAPlaceholderStillAnswersWithNothing()
    {
        // The default: a language that did not ask for a stand-in draws the empty cell exactly as before.
        var cell = new PropertyCell(Here, "operator", PropertyType.Enum("+", "-"), string.Empty, isSet: false);

        Assert.Equal(string.Empty, CellText.Of(cell));
        Assert.False(CellText.IsVisible(cell));
    }

    [Fact]
    public void AContainerAnswersWithNothing()
    {
        var root = Tree(TestEditor.BareRule()).Root;

        Assert.Equal(string.Empty, CellText.Of(root));
        Assert.False(CellText.IsVisible(root));
    }

    [Fact]
    public void AskingForTheTextOfNoCellIsAnError() =>
        Assert.Throws<ArgumentNullException>(() => CellText.Of(null!));

    private static ConstantCell CellOf(Node node, string text) =>
        Tree(node).Cells().OfType<ConstantCell>().First(cell => cell.Text == text);

    private static EditorTree Tree(Node node) => Builder.Build(node);
}

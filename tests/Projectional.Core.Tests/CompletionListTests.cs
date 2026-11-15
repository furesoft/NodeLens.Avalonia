using Projectional.Editing;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The list is the part of completion that is pure: it filters, moves and widens without touching a
/// model, so every rule about it can be asserted directly instead of through the editor.
/// </summary>
/// <remarks>
/// Each gesture answers with a new list rather than mutating one, which is what lets a keystroke keep a
/// reference to the list it was given while the session holds the result.
/// </remarks>
public sealed class CompletionListTests
{
    [Fact]
    public void AnOpenListStartsWithEverythingAndTheFirstItem()
    {
        var list = NewList("Expressions.IntLiteral", "Expressions.Name", "Expressions.Binary");

        Assert.Equal(3, list.Count);
        Assert.Equal("IntLiteral", list.Selected?.Label);
        Assert.Equal(0, list.SelectedIndex);
        Assert.False(list.IsEmpty);
        Assert.Equal(string.Empty, list.Prefix);
    }

    [Fact]
    public void AnEmptyListIsRejected()
    {
        var thrown = Assert.Throws<ArgumentException>(() => new CompletionList([]));

        Assert.Equal("items", thrown.ParamName);
    }

    [Fact]
    public void ANamespacedKeyIsLabelledByItsLastSegment()
    {
        var item = new CompletionItem("Rules.Rule", CompletionKind.Node);

        Assert.Equal("Rule", item.Label);
        Assert.Equal("Rules.Rule", item.ConceptKey);
    }

    [Fact]
    public void AKeyWithoutADotIsItsOwnLabel()
    {
        Assert.Equal("Rule", new CompletionItem("Rule", CompletionKind.Node).Label);
    }

    [Fact]
    public void NarrowingKeepsOnlyTheItemsThePrefixStarts()
    {
        var list = NewList("Expressions.IntLiteral", "Expressions.Name", "Expressions.Parenthesized");

        var narrowed = list.Narrow("In");

        Assert.Equal("In", narrowed.Prefix);
        Assert.Equal(1, narrowed.Count);
        Assert.Equal("Expressions.IntLiteral", narrowed.Selected?.ConceptKey);
    }

    [Fact]
    public void NarrowingIsCaseInsensitive()
    {
        var narrowed = NewList("Expressions.IntLiteral", "Expressions.Name").Narrow("int");

        Assert.Equal(1, narrowed.Count);
    }

    [Fact]
    public void ANarrowingThatMatchesNothingLeavesTheListOpenButEmpty()
    {
        var narrowed = NewList("Expressions.IntLiteral").Narrow("z");

        Assert.True(narrowed.IsEmpty);
        Assert.Equal(0, narrowed.Count);
        Assert.Null(narrowed.Selected);

        // A mistyped character must be a backspace away from useful, not a reason to lose the proposals.
        Assert.Equal(1, narrowed.Widen().Count);
    }

    [Fact]
    public void NarrowingReturnsToTheFirstMatch()
    {
        var list = NewList("Expressions.Name", "Expressions.Binary", "Expressions.BoolLiteral").Move(2);

        Assert.Equal(2, list.SelectedIndex);

        var narrowed = list.Narrow("B").Narrow("o").Narrow("o");

        Assert.Equal(1, narrowed.Count);
        Assert.Equal(0, narrowed.SelectedIndex);
    }

    [Fact]
    public void WideningDropsTheLastCharacterAndRestoresTheMatches()
    {
        var list = NewList("Expressions.IntLiteral", "Expressions.Name").Narrow("Na");

        var widened = list.Widen();

        Assert.Equal("N", widened.Prefix);
        Assert.Equal(1, widened.Count);

        Assert.Equal(2, widened.Widen().Count);
    }

    [Fact]
    public void WideningAnEmptyPrefixChangesNothing()
    {
        var list = NewList("Expressions.IntLiteral", "Expressions.Name");

        Assert.Same(list, list.Widen());
    }

    [Fact]
    public void NarrowingByNothingChangesNothing()
    {
        var list = NewList("Expressions.IntLiteral", "Expressions.Name");

        Assert.Same(list, list.Narrow(string.Empty));
    }

    [Fact]
    public void MovingWalksTheMatchesAndWrapsAtBothEnds()
    {
        var list = NewList("A", "B", "C");

        Assert.Equal(1, list.Move(1).SelectedIndex);
        Assert.Equal(2, list.Move(1).Move(1).SelectedIndex);
        Assert.Equal(0, list.Move(1).Move(1).Move(1).SelectedIndex);
        Assert.Equal(2, list.Move(-1).SelectedIndex);
        Assert.Equal("B", list.Move(1).Selected?.Label);
    }

    [Fact]
    public void MovingZeroIsRejected()
    {
        var list = NewList("A", "B");

        var thrown = Assert.Throws<ArgumentOutOfRangeException>(() => list.Move(0));

        Assert.Equal("delta", thrown.ParamName);
    }

    [Fact]
    public void MovingAnEmptyListKeepsItEmpty()
    {
        var empty = NewList("Expressions.IntLiteral").Narrow("zzz");

        Assert.True(empty.Move(1).IsEmpty);
    }

    [Fact]
    public void TheListPrintsHowMuchIsOnOffer()
    {
        Assert.Equal("1 of 2 for 'N'", NewList("Expressions.IntLiteral", "Expressions.Name").Narrow("N").ToString());
        Assert.Equal("Expressions.IntLiteral", new CompletionItem("Expressions.IntLiteral", CompletionKind.Node).ToString());
    }

    private static CompletionList NewList(params string[] conceptKeys)
    {
        CompletionItem[] items = [.. conceptKeys.Select(key => new CompletionItem(key, CompletionKind.Node))];

        return new CompletionList(items);
    }
}

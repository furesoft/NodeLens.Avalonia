using Projectional.Core;
using Projectional.Editing;
using Projectional.Projection;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// What a double click on a value means: the word under the pointer, so the next keystroke replaces that
/// word and leaves the rest of the cell alone.
/// </summary>
/// <remarks>
/// The boundary rule is the whole feature, so it is pinned as a table of offsets against three kinds of
/// text: words separated by a space, a number with a separator in it, and a run of punctuation. The rest
/// of the tests cover the cases where there is no word to select, and that selecting is not an edit.
/// </remarks>
public sealed class SelectWordActionTests
{
    [Theory]
    [InlineData("total amount", 0, 0, 5)]
    [InlineData("total amount", 2, 0, 5)]
    [InlineData("total amount", 5, 0, 5)]
    [InlineData("total amount", 6, 6, 12)]
    [InlineData("total amount", 11, 6, 12)]
    [InlineData("total amount", 12, 6, 12)]
    [InlineData("42.5", 0, 0, 2)]
    [InlineData("42.5", 2, 0, 2)]
    [InlineData("42.5", 3, 3, 4)]
    [InlineData("42.5", 4, 3, 4)]
    [InlineData("a  b", 2, 1, 3)]
    [InlineData("->", 0, 0, 2)]
    [InlineData("total_amount", 3, 0, 12)]
    public void SelectWordSelectsTheRunOfOneKindAroundTheCaret(
        string text,
        int offset,
        int start,
        int end)
    {
        var step = TestEditor.BareStep(text);
        using var session = TestEditor.Open(step);
        var address = CellAddress.OfSlot(step.Id, "text");

        session.Select(Selection.At(CaretPosition.At(address, offset)));

        var result = session.Apply(new SelectWordAction());

        Assert.Equal(EditOutcome.Unchanged, result.Outcome);
        Assert.Equal(start, session.Selection.Start);
        Assert.Equal(end, session.Selection.End);
        Assert.True(session.Selection.HasText);
    }

    [Fact]
    public void SelectWordDoesNotChangeTheModelOrTheHistory()
    {
        var step = TestEditor.BareStep("total amount");
        using var session = TestEditor.Open(step);
        var address = CellAddress.OfSlot(step.Id, "text");

        session.Select(Selection.At(CaretPosition.At(address, 2)));

        var result = session.Apply(new SelectWordAction());

        Assert.False(result.Changed);
        Assert.Equal(0, session.History.UndoDepth);
        Assert.Equal("total amount", step.Get("text"));
    }

    [Fact]
    public void SelectWordInAKeywordLeavesTheCaretWhereTheClickPutIt()
    {
        var rule = TestEditor.BareRule();
        using var session = TestEditor.Open(rule);

        // The node's own address resolves to the first caret place with it, which is the "rule" keyword.
        session.Select(Selection.At(new CellAddress(rule.Id)));

        var result = session.Apply(new SelectWordAction());

        Assert.Equal(EditOutcome.Unchanged, result.Outcome);
        Assert.Equal(new CellAddress(rule.Id), session.Selection.Anchor.Address);
        Assert.Equal(0, session.Selection.Anchor.Offset);
        Assert.True(session.Selection.IsEmpty);
    }

    [Fact]
    public void SelectWordInAnEmptyPropertyKeepsABareCaret()
    {
        var step = TestEditor.BareStep();
        using var session = TestEditor.Open(step);
        var address = CellAddress.OfSlot(step.Id, "text");

        session.Select(Selection.At(CaretPosition.At(address, 0)));

        var result = session.Apply(new SelectWordAction());

        Assert.Equal(EditOutcome.Unchanged, result.Outcome);
        Assert.Equal(address, session.Selection.Caret.Address);
        Assert.True(session.Selection.IsEmpty);
    }

    [Fact]
    public void TypingOverASelectedWordReplacesOnlyThatWord()
    {
        var step = TestEditor.BareStep("total amount");
        using var session = TestEditor.Open(step);
        var address = CellAddress.OfSlot(step.Id, "text");

        // The point of the gesture: the word is selected, so one keystroke replaces it and the rest of the
        // value is untouched - which is exactly what a whole-cell selection would not do.
        session.Select(Selection.At(CaretPosition.At(address, 8)));
        session.Apply(new SelectWordAction());

        session.Apply(new TypeTextAction("Z"));

        Assert.Equal("total Z", step.Get("text"));
    }

    [Fact]
    public void SelectWordRejectsANullContext()
    {
        Assert.Throws<ArgumentNullException>(() => new SelectWordAction().Apply(null!));
    }
}

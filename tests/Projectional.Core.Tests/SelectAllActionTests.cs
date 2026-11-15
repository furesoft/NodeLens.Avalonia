using Projectional.Core;
using Projectional.Editing;
using Projectional.Projection;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// What Ctrl+A means in a projectional editor: the whole text of the property the caret is in, not the
/// document.
/// </summary>
/// <remarks>
/// A selection is a range inside one cell, so "everything" can only ever be one property's text. The
/// tests pin the two halves of that decision - it widens to the cell's ends, and it refuses to widen at
/// all where there is no editable text to select.
/// </remarks>
public sealed class SelectAllActionTests
{
    [Fact]
    public void SelectAllSelectsTheWholePropertyTheCaretStandsIn()
    {
        var step = TestEditor.BareStep("abc");
        using var session = TestEditor.Open(step);
        var text = CellAddress.OfSlot(step.Id, "text");

        session.Select(Selection.At(CaretPosition.At(text, 1)));

        var result = session.Apply(KeyRouter.For(EditorKey.SelectAll));

        Assert.Equal(EditOutcome.Unchanged, result.Outcome);
        Assert.Equal(text, session.Selection.Anchor.Address);
        Assert.Equal(0, session.Selection.Anchor.Offset);
        Assert.Equal(text, session.Selection.Caret.Address);
        Assert.Equal(3, session.Selection.Caret.Offset);
        Assert.True(session.Selection.HasText);
    }

    [Fact]
    public void SelectAllDoesNotChangeTheModelOrTheHistory()
    {
        var step = TestEditor.BareStep("abc");
        using var session = TestEditor.Open(step);
        var text = CellAddress.OfSlot(step.Id, "text");

        session.Select(Selection.At(CaretPosition.At(text, 1)));

        var result = session.Apply(KeyRouter.For(EditorKey.SelectAll));

        Assert.False(result.Changed);
        Assert.Equal(0, session.History.UndoDepth);
        Assert.Equal("abc", step.Get("text"));
    }

    [Fact]
    public void SelectAllInAKeywordChangesNothing()
    {
        var rule = TestEditor.BareRule();
        using var session = TestEditor.Open(rule);

        // The node's own address resolves to the first caret place with it, which is the "rule" keyword.
        session.Select(Selection.At(new CellAddress(rule.Id)));

        var result = session.Apply(KeyRouter.For(EditorKey.SelectAll));

        Assert.Equal(EditOutcome.Unchanged, result.Outcome);
        Assert.True(session.Selection.IsEmpty);
    }

    [Fact]
    public void SelectAllInAnEmptyPropertyChangesNothing()
    {
        var step = TestEditor.BareStep();
        using var session = TestEditor.Open(step);
        var text = CellAddress.OfSlot(step.Id, "text");

        session.Select(Selection.At(CaretPosition.At(text, 0)));

        var result = session.Apply(KeyRouter.For(EditorKey.SelectAll));

        Assert.Equal(EditOutcome.Unchanged, result.Outcome);
        Assert.True(session.Selection.IsEmpty);
    }

    [Fact]
    public void SelectAllInAnEmptySlotChangesNothing()
    {
        var (_, body) = TestEditor.RuleWithBody();
        using var session = TestEditor.Open(body);

        // The body's steps slot has no children, so it projects as one empty collection cell.
        session.Select(Selection.At(CellAddress.OfSlot(body.Id, "steps")));

        var result = session.Apply(KeyRouter.For(EditorKey.SelectAll));

        Assert.Equal(EditOutcome.Unchanged, result.Outcome);
        Assert.True(session.Selection.IsEmpty);
    }

    [Fact]
    public void SelectAllRejectsANullContext()
    {
        Assert.Throws<ArgumentNullException>(() => new SelectAllAction().Apply(null!));
    }
}

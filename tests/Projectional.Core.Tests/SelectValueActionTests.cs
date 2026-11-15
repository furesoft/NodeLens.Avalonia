using Projectional.Core;
using Projectional.Editing;
using Projectional.Projection;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// What a third click on a value means: the whole text of that cell, so the next keystroke replaces it.
/// </summary>
/// <remarks>
/// Clicking three times on <c>Approved</c> asks for that whole cell, not for the node it sits in and not
/// for the document, so the gesture is one rung of the Select All ladder rather than the ladder itself.
/// The tests pin that it selects the value wherever inside the cell the click landed, that a cell with
/// nothing to select is left alone, and that selecting is not an edit.
/// </remarks>
public sealed class SelectValueActionTests
{
    [Fact]
    public void SelectValueSelectsTheWholePropertyFromTheMiddle()
    {
        var step = TestEditor.BareStep("abc");
        using var session = TestEditor.Open(step);
        var text = CellAddress.OfSlot(step.Id, "text");

        session.Select(Selection.At(CaretPosition.At(text, 1)));

        var result = session.Apply(new SelectValueAction());

        Assert.Equal(EditOutcome.Unchanged, result.Outcome);
        Assert.Equal(text, session.Selection.Anchor.Address);
        Assert.Equal(0, session.Selection.Anchor.Offset);
        Assert.Equal(text, session.Selection.Caret.Address);
        Assert.Equal(3, session.Selection.Caret.Offset);
        Assert.True(session.Selection.HasText);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void SelectValueSelectsTheWholePropertyFromEitherEdge(int offset)
    {
        var step = TestEditor.BareStep("abc");
        using var session = TestEditor.Open(step);
        var text = CellAddress.OfSlot(step.Id, "text");

        session.Select(Selection.At(CaretPosition.At(text, offset)));

        session.Apply(new SelectValueAction());

        Assert.Equal(0, session.Selection.Start);
        Assert.Equal(3, session.Selection.End);
        Assert.True(session.Selection.HasText);
    }

    [Fact]
    public void SelectValueDoesNotChangeTheModelOrTheHistory()
    {
        var step = TestEditor.BareStep("abc");
        using var session = TestEditor.Open(step);
        var text = CellAddress.OfSlot(step.Id, "text");

        session.Select(Selection.At(CaretPosition.At(text, 1)));

        var result = session.Apply(new SelectValueAction());

        Assert.False(result.Changed);
        Assert.Equal(0, session.History.UndoDepth);
        Assert.Equal("abc", step.Get("text"));
    }

    [Fact]
    public void SelectValueInAKeywordLeavesTheCaretWhereTheClickPutIt()
    {
        var rule = TestEditor.BareRule();
        using var session = TestEditor.Open(rule);

        // The node's own address resolves to the first caret place with it, which is the "rule" keyword.
        session.Select(Selection.At(new CellAddress(rule.Id)));

        var result = session.Apply(new SelectValueAction());

        Assert.Equal(EditOutcome.Unchanged, result.Outcome);
        Assert.Equal(new CellAddress(rule.Id), session.Selection.Anchor.Address);
        Assert.Equal(0, session.Selection.Anchor.Offset);
        Assert.True(session.Selection.IsEmpty);
    }

    [Fact]
    public void SelectValueInAnEmptyPropertyKeepsABareCaret()
    {
        var step = TestEditor.BareStep();
        using var session = TestEditor.Open(step);
        var text = CellAddress.OfSlot(step.Id, "text");

        session.Select(Selection.At(CaretPosition.At(text, 0)));

        var result = session.Apply(new SelectValueAction());

        Assert.Equal(EditOutcome.Unchanged, result.Outcome);
        Assert.Equal(text, session.Selection.Caret.Address);
        Assert.True(session.Selection.IsEmpty);
    }

    [Fact]
    public void SelectValueInAnEmptySlotLeavesTheSelectionAlone()
    {
        var (_, body) = TestEditor.RuleWithBody();
        using var session = TestEditor.Open(body);

        // The body's steps slot has no children, so it projects as one empty collection cell.
        session.Select(Selection.At(CellAddress.OfSlot(body.Id, "steps")));

        var result = session.Apply(new SelectValueAction());

        Assert.Equal(EditOutcome.Unchanged, result.Outcome);
        Assert.Equal(CellAddress.OfSlot(body.Id, "steps"), session.Selection.Caret.Address);
        Assert.True(session.Selection.IsEmpty);
    }

    [Fact]
    public void TypingOverASelectedValueReplacesIt()
    {
        var step = TestEditor.BareStep("abc");
        using var session = TestEditor.Open(step);
        var text = CellAddress.OfSlot(step.Id, "text");

        // The point of the gesture: the whole value is selected, so one keystroke is one replacement
        // rather than an insertion into the middle of a word the user meant to overwrite.
        session.Select(Selection.At(CaretPosition.At(text, 0)));
        session.Apply(new SelectValueAction());

        session.Apply(new TypeTextAction("Z"));

        Assert.Equal("Z", step.Get("text"));
    }

    [Fact]
    public void SelectValueRejectsANullContext()
    {
        Assert.Throws<ArgumentNullException>(() => new SelectValueAction().Apply(null!));
    }
}

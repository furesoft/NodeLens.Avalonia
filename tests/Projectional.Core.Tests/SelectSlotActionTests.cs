using Projectional.Core;
using Projectional.Editing;
using Projectional.Projection;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The caret jump a rename makes: land in one slot and select what is in it, so the next keystroke replaces the
/// whole value.
/// </summary>
/// <remarks>
/// The three things worth pinning are that it is a navigation rather than an edit (the model and the history are
/// untouched), that an empty slot keeps a bare caret instead of a zero-length selection, and that an address the
/// document has moved past is refused with a reason - an offer outlives the document it was derived from.
/// </remarks>
public sealed class SelectSlotActionTests
{
    [Fact]
    public void TheCaretLandsInTheSlotWithItsTextSelected()
    {
        var (rule, _, step) = TestEditor.RuleWithStep();

        step.Set("text", "walk");

        using var session = TestEditor.Open(rule);

        // The caret is somewhere else entirely, which is what makes this a jump.
        session.Select(Selection.At(CellAddress.OfSlot(step.Id, "text")));

        var name = CellAddress.OfSlot(rule.Id, "name");
        var result = session.Apply(new SelectSlotAction(name));

        Assert.Equal(EditOutcome.Unchanged, result.Outcome);
        Assert.Equal(name, session.Selection.Anchor.Address);
        Assert.Equal(0, session.Selection.Start);
        Assert.Equal(name, session.Selection.Caret.Address);
        Assert.Equal(rule.GetOrNull("name")!.Length, session.Selection.End);
        Assert.True(session.Selection.HasText);

        // A jump is not an edit: nothing to undo, and the value is still what it was.
        Assert.False(result.Changed);
        Assert.Equal(0, session.History.UndoDepth);
        Assert.Equal("walk", step.Get("text"));
    }

    [Fact]
    public void AnEmptySlotKeepsABareCaret()
    {
        var step = TestEditor.BareStep();
        using var session = TestEditor.Open(step);

        var text = CellAddress.OfSlot(step.Id, "text");
        session.Apply(new SelectSlotAction(text));

        Assert.Equal(text, session.Selection.Caret.Address);
        Assert.Equal(session.Selection.Anchor, session.Selection.Caret);
        Assert.False(session.Selection.HasText);
    }

    [Fact]
    public void AnAddressTheDocumentHasMovedPastIsRejected()
    {
        var (rule, _, step) = TestEditor.RuleWithStep();

        step.Set("text", "walk");

        using var session = TestEditor.Open(rule);

        session.Select(Selection.At(CellAddress.OfSlot(step.Id, "text")));
        session.Apply(new DeleteNodeAction());

        var gone = CellAddress.OfSlot(step.Id, "text");
        var result = session.Apply(new SelectSlotAction(gone));

        Assert.Equal(EditOutcome.Rejected, result.Outcome);
        Assert.NotNull(result.Reason);
        Assert.False(session.Selection.HasText);
    }
}

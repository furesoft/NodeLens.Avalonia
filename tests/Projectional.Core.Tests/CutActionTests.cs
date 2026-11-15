using Projectional.Core;
using Projectional.Editing;
using Projectional.Projection;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// Cutting copies the selected text and then deletes it, as one undoable step.
/// </summary>
/// <remarks>
/// Cut is copy plus delete, so it inherits both halves' rules: without text in the selection there is
/// nothing to copy and the clipboard is left alone, and when the delete is refused the text stays on
/// the clipboard while the model keeps its value. The order matters - the clipboard is written before
/// the delete runs, so a refused cut still gives the user something to paste elsewhere.
/// </remarks>
public sealed class CutActionTests
{
    [Fact]
    public void CutCopiesTheSelectedTextAndRemovesIt()
    {
        var step = TestEditor.BareStep("abcdef");
        var clipboard = new InMemoryClipboard();
        using var session = TestEditor.Open(step, clipboard);
        var text = CellAddress.OfSlot(step.Id, "text");

        session.Select(new Selection(CaretPosition.At(text, 1), CaretPosition.At(text, 4)));

        var result = session.Apply(KeyRouter.For(EditorKey.Cut));

        Assert.Equal(EditOutcome.Applied, result.Outcome);
        Assert.True(result.Changed);
        Assert.Equal("bcd", clipboard.Text);
        Assert.Equal("aef", step.Get("text"));
    }

    [Fact]
    public void CutIsOneUndoStep()
    {
        var step = TestEditor.BareStep("abcdef");
        var clipboard = new InMemoryClipboard();
        using var session = TestEditor.Open(step, clipboard);
        var text = CellAddress.OfSlot(step.Id, "text");

        session.Select(new Selection(CaretPosition.At(text, 1), CaretPosition.At(text, 4)));
        session.Apply(KeyRouter.For(EditorKey.Cut));

        Assert.Equal(1, session.History.UndoDepth);
        Assert.True(session.Undo());
        Assert.Equal("abcdef", step.Get("text"));
    }

    [Fact]
    public void CutLeavesTheClipboardAloneWhenNothingIsSelected()
    {
        var step = TestEditor.BareStep("abcdef");
        var clipboard = new InMemoryClipboard { Text = "keep" };
        using var session = TestEditor.Open(step, clipboard);
        var text = CellAddress.OfSlot(step.Id, "text");

        session.Select(Selection.At(CaretPosition.At(text, 2)));

        var result = session.Apply(KeyRouter.For(EditorKey.Cut));

        Assert.Equal(EditOutcome.Unchanged, result.Outcome);
        Assert.Equal("keep", clipboard.Text);
        Assert.Equal("abcdef", step.Get("text"));
        Assert.Equal(0, session.History.UndoDepth);
    }

    [Fact]
    public void CutLeavesTheClipboardAloneForASelectionAcrossCells()
    {
        var step = TestEditor.BareStep("abcdef");
        var clipboard = new InMemoryClipboard { Text = "keep" };
        using var session = TestEditor.Open(step, clipboard);
        var index = CellAddress.OfSlot(step.Id, "index");
        var text = CellAddress.OfSlot(step.Id, "text");

        // A selection spanning two cells is not text, so there is nothing to cut.
        session.Select(new Selection(CaretPosition.At(index, 0), CaretPosition.At(text, 2)));

        var result = session.Apply(KeyRouter.For(EditorKey.Cut));

        Assert.Equal(EditOutcome.Unchanged, result.Outcome);
        Assert.Equal("keep", clipboard.Text);
        Assert.Equal("abcdef", step.Get("text"));
    }

    [Fact]
    public void CutInAKeywordLeavesTheClipboardAlone()
    {
        var rule = TestEditor.BareRule();
        var clipboard = new InMemoryClipboard { Text = "keep" };
        using var session = TestEditor.Open(rule, clipboard);

        // The node's own address resolves to the first caret place with it, which is the "rule" keyword.
        session.Select(Selection.At(new CellAddress(rule.Id)));

        var result = session.Apply(KeyRouter.For(EditorKey.Cut));

        Assert.Equal(EditOutcome.Unchanged, result.Outcome);
        Assert.Equal("keep", clipboard.Text);
    }

    [Fact]
    public void CutInAnEmptyPropertyLeavesTheClipboardAlone()
    {
        var step = TestEditor.BareStep();
        var clipboard = new InMemoryClipboard { Text = "keep" };
        using var session = TestEditor.Open(step, clipboard);
        var text = CellAddress.OfSlot(step.Id, "text");

        session.Select(Selection.At(CaretPosition.At(text, 0)));

        var result = session.Apply(KeyRouter.For(EditorKey.Cut));

        Assert.Equal(EditOutcome.Unchanged, result.Outcome);
        Assert.Equal("keep", clipboard.Text);
    }

    [Fact]
    public void CutKeepsTheTextOnTheClipboardWhenTheDeleteIsRefused()
    {
        var step = TestEditor.BareStep();
        step.Set("index", 7);

        var clipboard = new InMemoryClipboard();
        using var session = TestEditor.Open(step, clipboard);
        var index = CellAddress.OfSlot(step.Id, "index");

        session.Select(new Selection(CaretPosition.At(index, 0), CaretPosition.At(index, 1)));

        var result = session.Apply(KeyRouter.For(EditorKey.Cut));

        // Emptying an integer cell would leave nothing the type accepts, so the delete is refused.
        Assert.Equal(EditOutcome.Rejected, result.Outcome);
        Assert.False(result.Changed);

        // The copy already happened, so the user can still paste the number somewhere else.
        Assert.Equal("7", clipboard.Text);
        Assert.Equal(7, step.GetInt("index"));
        Assert.Equal(0, session.History.UndoDepth);
    }

    [Fact]
    public void CutRejectsANullContext()
    {
        Assert.Throws<ArgumentNullException>(() => new CutAction().Apply(null!));
    }
}

using Projectional.Core;
using Projectional.Editing;
using Projectional.Projection;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// Copying takes the text of the selection and leaves everything else alone.
/// </summary>
/// <remarks>
/// Copy is the one clipboard action that must never touch the model: it produces no change set, so it
/// cannot appear in the undo history, and it only writes the clipboard when the selection actually
/// covers text. A selection spanning more than one cell is not text, so it copies nothing rather than
/// guessing which cell the user meant.
/// </remarks>
public sealed class CopyActionTests
{
    [Fact]
    public void CopyPutsTheSelectedTextOnTheClipboard()
    {
        var step = TestEditor.BareStep("abcdef");
        var clipboard = new InMemoryClipboard();
        using var session = TestEditor.Open(step, clipboard);
        var text = CellAddress.OfSlot(step.Id, "text");

        session.Select(new Selection(CaretPosition.At(text, 1), CaretPosition.At(text, 4)));

        var result = session.Apply(KeyRouter.For(EditorKey.Copy));

        Assert.Equal(EditOutcome.Unchanged, result.Outcome);
        Assert.Equal("bcd", clipboard.Text);
    }

    [Fact]
    public void CopyDoesNotChangeTheModelOrTheHistory()
    {
        var step = TestEditor.BareStep("abcdef");
        var clipboard = new InMemoryClipboard();
        using var session = TestEditor.Open(step, clipboard);
        var text = CellAddress.OfSlot(step.Id, "text");

        session.Select(new Selection(CaretPosition.At(text, 1), CaretPosition.At(text, 4)));

        var result = session.Apply(KeyRouter.For(EditorKey.Copy));

        Assert.False(result.Changed);
        Assert.Equal(0, session.History.UndoDepth);
        Assert.Equal("abcdef", step.Get("text"));
    }

    [Fact]
    public void CopyLeavesTheClipboardAloneWhenNothingIsSelected()
    {
        var step = TestEditor.BareStep("abcdef");
        var clipboard = new InMemoryClipboard { Text = "keep" };
        using var session = TestEditor.Open(step, clipboard);
        var text = CellAddress.OfSlot(step.Id, "text");

        session.Select(Selection.At(CaretPosition.At(text, 2)));

        var result = session.Apply(KeyRouter.For(EditorKey.Copy));

        Assert.Equal(EditOutcome.Unchanged, result.Outcome);
        Assert.Equal("keep", clipboard.Text);
    }

    [Fact]
    public void CopyLeavesTheClipboardAloneForASelectionAcrossCells()
    {
        var step = TestEditor.BareStep("abcdef");
        var clipboard = new InMemoryClipboard { Text = "keep" };
        using var session = TestEditor.Open(step, clipboard);
        var index = CellAddress.OfSlot(step.Id, "index");
        var text = CellAddress.OfSlot(step.Id, "text");

        // The step's empty index cell and its text cell are two cells, so the selection is not text.
        session.Select(new Selection(CaretPosition.At(index, 0), CaretPosition.At(text, 2)));

        var result = session.Apply(KeyRouter.For(EditorKey.Copy));

        Assert.Equal(EditOutcome.Unchanged, result.Outcome);
        Assert.Equal("keep", clipboard.Text);
    }

    [Fact]
    public void CopyInAKeywordLeavesTheClipboardAlone()
    {
        var rule = TestEditor.BareRule();
        var clipboard = new InMemoryClipboard { Text = "keep" };
        using var session = TestEditor.Open(rule, clipboard);

        // The node's own address resolves to the first caret place with it, which is the "rule" keyword.
        session.Select(Selection.At(new CellAddress(rule.Id)));

        var result = session.Apply(KeyRouter.For(EditorKey.Copy));

        Assert.Equal(EditOutcome.Unchanged, result.Outcome);
        Assert.Equal("keep", clipboard.Text);
    }

    [Fact]
    public void CopyInAnEmptyPropertyLeavesTheClipboardAlone()
    {
        var step = TestEditor.BareStep();
        var clipboard = new InMemoryClipboard { Text = "keep" };
        using var session = TestEditor.Open(step, clipboard);
        var text = CellAddress.OfSlot(step.Id, "text");

        session.Select(Selection.At(CaretPosition.At(text, 0)));

        var result = session.Apply(KeyRouter.For(EditorKey.Copy));

        Assert.Equal(EditOutcome.Unchanged, result.Outcome);
        Assert.Equal("keep", clipboard.Text);
    }

    [Fact]
    public void CopyRejectsANullContext()
    {
        Assert.Throws<ArgumentNullException>(() => new CopyAction().Apply(null!));
    }
}

using Projectional.Core;
using Projectional.Editing;
using Projectional.Projection;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// Pasting types the clipboard's text into the property the caret is in.
/// </summary>
/// <remarks>
/// Paste is typing, so it inherits typing's rules: a selection is replaced, an empty caret inserts, and a
/// value the property's type would refuse is refused. The one thing paste adds is that an empty clipboard is
/// nothing to do rather than a failure, and that a paste never merges into the typing around it.
/// </remarks>
public sealed class PasteActionTests
{
    [Fact]
    public void PasteInsertsTheClipboardTextAtTheCaret()
    {
        var step = TestEditor.BareStep();
        var clipboard = new InMemoryClipboard { Text = "abc" };
        using var session = TestEditor.Open(step, clipboard);
        var text = CellAddress.OfSlot(step.Id, "text");

        session.Select(Selection.At(CaretPosition.At(text, 0)));

        var result = session.Apply(KeyRouter.For(EditorKey.Paste));

        Assert.Equal(EditOutcome.Applied, result.Outcome);
        Assert.True(result.Changed);
        Assert.Equal("abc", step.Get("text"));
        Assert.Equal(1, session.History.UndoDepth);
    }

    [Fact]
    public void PasteReplacesTheSelection()
    {
        var step = TestEditor.BareStep("abcdef");
        var clipboard = new InMemoryClipboard { Text = "XY" };
        using var session = TestEditor.Open(step, clipboard);
        var text = CellAddress.OfSlot(step.Id, "text");

        session.Select(new Selection(CaretPosition.At(text, 1), CaretPosition.At(text, 4)));

        var result = session.Apply(KeyRouter.For(EditorKey.Paste));

        Assert.Equal(EditOutcome.Applied, result.Outcome);
        Assert.Equal("aXYef", step.Get("text"));
    }

    [Fact]
    public void PasteDoesNothingWhenTheClipboardIsEmpty()
    {
        var step = TestEditor.BareStep("abc");
        var clipboard = new InMemoryClipboard();
        using var session = TestEditor.Open(step, clipboard);
        var text = CellAddress.OfSlot(step.Id, "text");

        session.Select(Selection.At(CaretPosition.At(text, 1)));

        var result = session.Apply(KeyRouter.For(EditorKey.Paste));

        Assert.Equal(EditOutcome.Unchanged, result.Outcome);
        Assert.False(result.Changed);
        Assert.Equal("abc", step.Get("text"));
        Assert.Equal(0, session.History.UndoDepth);
        Assert.Equal(1, session.Selection.Caret.Offset);
    }

    [Fact]
    public void PasteDoesNothingWhenTheClipboardHoldsAnEmptyString()
    {
        var step = TestEditor.BareStep("abc");
        var clipboard = new InMemoryClipboard { Text = "" };
        using var session = TestEditor.Open(step, clipboard);
        var text = CellAddress.OfSlot(step.Id, "text");

        session.Select(Selection.At(CaretPosition.At(text, 1)));

        var result = session.Apply(KeyRouter.For(EditorKey.Paste));

        Assert.Equal(EditOutcome.Unchanged, result.Outcome);
        Assert.Equal("abc", step.Get("text"));
        Assert.Equal(0, session.History.UndoDepth);
    }

    [Fact]
    public void PasteIntoAnIntegerCellIsRejected()
    {
        var step = TestEditor.BareStep();
        step.Set("index", 7);

        var clipboard = new InMemoryClipboard { Text = "abc" };
        using var session = TestEditor.Open(step, clipboard);
        var index = CellAddress.OfSlot(step.Id, "index");

        session.Select(new Selection(CaretPosition.At(index, 0), CaretPosition.At(index, 1)));

        var result = session.Apply(KeyRouter.For(EditorKey.Paste));

        // "abc" is not an integer, so the model refuses the replacement and keeps its value.
        Assert.Equal(EditOutcome.Rejected, result.Outcome);
        Assert.False(result.Changed);
        Assert.Equal(7, step.GetInt("index"));
        Assert.Equal(0, session.History.UndoDepth);
    }

    [Fact]
    public void PasteIsItsOwnUndoStep()
    {
        var step = TestEditor.BareStep();
        var clipboard = new InMemoryClipboard { Text = "de" };
        using var session = TestEditor.Open(step, clipboard);
        var text = CellAddress.OfSlot(step.Id, "text");

        session.Select(Selection.At(CaretPosition.At(text, 0)));
        session.Apply(KeyRouter.Type(session, "c"));

        Assert.Equal("c", step.Get("text"));
        Assert.Equal(1, session.History.UndoDepth);

        session.Apply(KeyRouter.For(EditorKey.Paste));

        // A paste never merges into the typing around it, so it starts a step of its own.
        Assert.Equal("cde", step.Get("text"));
        Assert.Equal(2, session.History.UndoDepth);

        Assert.True(session.Undo());
        Assert.Equal("c", step.Get("text"));
    }

    [Fact]
    public void PasteRejectsANullContext()
    {
        Assert.Throws<ArgumentNullException>(() => new PasteAction().Apply(null!));
    }
}

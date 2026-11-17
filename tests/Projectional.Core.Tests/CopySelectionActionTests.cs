using Projectional.Core;
using Projectional.Editing;
using Projectional.Projection;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// Copy over a selection that covers nodes puts a fragment on the clipboard and changes nothing in the document.
/// </summary>
/// <remarks>
/// The structural counterpart of <see cref="CopyActionTests"/>: the same gesture, the same "no change set",
/// and the other payload. Which of the two a Ctrl+C means is <see cref="KeyRouter"/>'s decision, so these tests
/// go through it rather than through the action directly - the routing is what the user actually presses.
/// </remarks>
public sealed class CopySelectionActionTests
{
    [Fact]
    public void CopyingASelectionOfNodesWritesAFragmentAndLeavesTheDocumentAlone()
    {
        var (body, steps) = TestEditor.BodyWithSteps("first", "second");
        var clipboard = new InMemoryClipboard();
        using var session = TestEditor.Open(body, clipboard);

        TestEditor.SelectRange(session, steps[0], steps[1]);

        var result = session.Apply(KeyRouter.For(session, EditorKey.Copy));

        Assert.Equal(EditOutcome.Unchanged, result.Outcome);
        Assert.False(result.Changed);
        Assert.Equal(0, session.History.UndoDepth);

        var fragment = Assert.IsType<ModelFragment>(clipboard.Fragment);

        Assert.Equal([steps[0].Id, steps[1].Id], fragment.Roots.Select(root => root.Origin));
        Assert.Equal(2, body.GetChildren("steps").Count);
    }

    [Fact]
    public void CopyingStructureReplacesTextTheClipboardHeldBefore()
    {
        var (body, steps) = TestEditor.BodyWithSteps("first", "second");
        var clipboard = new InMemoryClipboard { Text = "stale" };
        using var session = TestEditor.Open(body, clipboard);

        TestEditor.SelectRange(session, steps[0], steps[1]);
        session.Apply(KeyRouter.For(session, EditorKey.Copy));

        // One payload at a time: a paste must never act on what the user copied before.
        Assert.Null(clipboard.Text);
        Assert.NotNull(clipboard.Fragment);
    }

    [Fact]
    public void CopyingInsideOneValueStaysTextAndWritesNoFragment()
    {
        var step = TestEditor.BareStep("abcdef");
        var clipboard = new InMemoryClipboard();
        using var session = TestEditor.Open(step, clipboard);
        var text = CellAddress.OfSlot(step.Id, "text");

        session.Select(new Selection(CaretPosition.At(text, 1), CaretPosition.At(text, 4)));
        session.Apply(KeyRouter.For(session, EditorKey.Copy));

        Assert.Equal("bcd", clipboard.Text);
        Assert.Null(clipboard.Fragment);
    }
}

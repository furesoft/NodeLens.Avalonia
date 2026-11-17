using Projectional.Core;
using Projectional.Editing;
using Projectional.Projection;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// Cut over a selection that covers nodes copies the fragment and then removes the nodes, as one undo step.
/// </summary>
/// <remarks>
/// The two halves are the two actions this delegates to, so what these tests pin is the ordering and the undo
/// boundary: the copy happens even if the removal were refused, and the whole cut is one history step rather
/// than a copy followed by a delete the user has to undo twice.
/// </remarks>
public sealed class CutSelectionActionTests
{
    [Fact]
    public void CuttingASelectionRemovesTheNodesAndKeepsThemOnTheClipboard()
    {
        var (body, steps) = TestEditor.BodyWithSteps("first", "second", "third");
        var clipboard = new InMemoryClipboard();
        using var session = TestEditor.Open(body, clipboard);

        TestEditor.SelectRange(session, steps[0], steps[1]);

        var result = session.Apply(KeyRouter.For(session, EditorKey.Cut));

        Assert.Equal(EditOutcome.Applied, result.Outcome);
        Assert.Equal([steps[2]], body.GetChildren("steps"));

        var fragment = Assert.IsType<ModelFragment>(clipboard.Fragment);

        Assert.Equal([steps[0].Id, steps[1].Id], fragment.Roots.Select(root => root.Origin));
    }

    [Fact]
    public void ACutIsOneUndoStep()
    {
        var (body, steps) = TestEditor.BodyWithSteps("first", "second", "third");
        var clipboard = new InMemoryClipboard();
        using var session = TestEditor.Open(body, clipboard);

        TestEditor.SelectRange(session, steps[0], steps[1]);
        session.Apply(KeyRouter.For(session, EditorKey.Cut));

        Assert.True(session.Undo());

        Assert.Equal(steps, body.GetChildren("steps"));
        Assert.False(session.History.CanUndo);
    }

    [Fact]
    public void CuttingTextStaysTextAndWritesNoFragment()
    {
        var step = TestEditor.BareStep("abcdef");
        var clipboard = new InMemoryClipboard();
        using var session = TestEditor.Open(step, clipboard);
        var text = CellAddress.OfSlot(step.Id, "text");

        session.Select(new Selection(CaretPosition.At(text, 1), CaretPosition.At(text, 4)));
        session.Apply(KeyRouter.For(session, EditorKey.Cut));

        Assert.Equal("bcd", clipboard.Text);
        Assert.Null(clipboard.Fragment);
        Assert.Equal("aef", step.Get("text"));
    }
}

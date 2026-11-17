using Projectional.Editing;
using Projectional.Projection;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The routing table is the single place where a key becomes an action, so it is asked two things:
/// that every key it declares is answered at all, and that the keys whose meaning is not obvious land
/// on the move the user meant.
/// </summary>
/// <remarks>
/// An unrouted key throws by design rather than answering <see langword="null"/>, so a missing arm is
/// only visible when somebody presses that key. <see cref="EveryKeyIsRouted"/> is what turns that into
/// a build-time-ish failure: adding a member to <see cref="EditorKey"/> without writing its arm fails
/// the suite instead of the user's session.
/// </remarks>
public sealed class KeyRouterTests
{
    [Fact]
    public void EveryKeyIsRouted()
    {
        foreach (var key in Enum.GetValues<EditorKey>())
        {
            var action = KeyRouter.For(key);

            Assert.NotNull(action);
        }
    }

    [Fact]
    public void AnUnknownKeyIsARefusedRoute()
    {
        var unknown = (EditorKey)999;

        var error = Assert.Throws<ArgumentOutOfRangeException>(() => KeyRouter.For(unknown));

        Assert.Equal("key", error.ParamName);
    }

    [Theory]
    [InlineData(EditorKey.PageUp, CaretMove.PageUp)]
    [InlineData(EditorKey.PageDown, CaretMove.PageDown)]
    [InlineData(EditorKey.ControlHome, CaretMove.DocumentStart)]
    [InlineData(EditorKey.ControlEnd, CaretMove.DocumentEnd)]
    public void ThePageAndDocumentKeysMoveTheCaret(EditorKey key, CaretMove expected)
    {
        var action = Assert.IsType<MoveCaretAction>(KeyRouter.For(key));

        Assert.Equal(expected, action.Move);
    }

    [Fact]
    public void CopyOverANodeSelectionIsAStructuralCopy()
    {
        var (body, steps) = TestEditor.BodyWithSteps("first", "second");
        using var session = TestEditor.Open(body);

        TestEditor.SelectRange(session, steps[0], steps[1]);

        Assert.IsType<CopySelectionAction>(KeyRouter.For(session, EditorKey.Copy));
        Assert.IsType<CutSelectionAction>(KeyRouter.For(session, EditorKey.Cut));
    }

    [Fact]
    public void CopyInsideOneValueStaysATextCopy()
    {
        var step = TestEditor.BareStep("abcdef");
        using var session = TestEditor.Open(step);
        var text = CellAddress.OfSlot(step.Id, "text");

        session.Select(new Selection(CaretPosition.At(text, 1), CaretPosition.At(text, 4)));

        Assert.IsType<CopyAction>(KeyRouter.For(session, EditorKey.Copy));
        Assert.IsType<CutAction>(KeyRouter.For(session, EditorKey.Cut));
    }

    [Fact]
    public void PasteFollowsThePayloadRatherThanTheCaret()
    {
        var step = TestEditor.BareStep("abcdef");
        var clipboard = new InMemoryClipboard { Text = "typing" };
        using var session = TestEditor.Open(step, clipboard);

        Assert.IsType<PasteAction>(KeyRouter.For(session, EditorKey.Paste));

        // A fragment wins because it is the more specific thing the user just copied; the text action would
        // write nothing here, since writing text is not what a fragment is.
        clipboard.Fragment = ModelFragment.Of([TestEditor.BareStep()]);

        Assert.IsType<PasteFragmentAction>(KeyRouter.For(session, EditorKey.Paste));
    }
}

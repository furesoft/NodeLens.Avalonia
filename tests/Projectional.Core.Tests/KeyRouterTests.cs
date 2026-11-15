using Projectional.Editing;
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
}

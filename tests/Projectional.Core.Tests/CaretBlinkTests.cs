using Projectional.Editing;
using Projectional.Projection;

using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The blink phase and the rule that decides where the caret blinks at all.
/// </summary>
/// <remarks>
/// Both are decisions rather than paint calls, so they are tested here and not through a renderer: the
/// control asks <see cref="CaretBlink.Advance"/> once per tick and repaints when it says so.
/// </remarks>
public sealed class CaretBlinkTests
{
    [Fact]
    public void ThePhaseAlternates()
    {
        var blink = new CaretBlink();

        Assert.True(blink.IsVisible);

        Assert.True(blink.Advance(blinking: true));
        Assert.False(blink.IsVisible);

        Assert.True(blink.Advance(blinking: true));
        Assert.True(blink.IsVisible);
    }

    [Fact]
    public void ResetShowsTheCaretAgain()
    {
        var blink = new CaretBlink();
        blink.Advance(blinking: true);

        blink.Reset();

        Assert.True(blink.IsVisible);
    }

    [Fact]
    public void ACaretThatDoesNotBlinkIsShownAndCostsNoRepaint()
    {
        var blink = new CaretBlink();

        Assert.False(blink.Advance(blinking: false));
        Assert.True(blink.IsVisible);

        // The second tick still has nothing to do, so a window that lost the focus does not repaint.
        Assert.False(blink.Advance(blinking: false));
    }

    [Fact]
    public void ACaretThatStopsBlinkingBecomesVisibleOnTheFirstTick()
    {
        var blink = new CaretBlink();
        blink.Advance(blinking: true);

        Assert.False(blink.IsVisible);
        Assert.True(blink.Advance(blinking: false));
        Assert.True(blink.IsVisible);
    }

    [Fact]
    public void TheCaretBlinksInsideAProperty()
    {
        var rule = TestEditor.BareRule();
        using var session = TestEditor.Open(rule);

        // The session starts on the name property, which is where typing goes.
        Assert.Equal("name", session.Selection.Caret.Address.Slot);
        Assert.True(CaretBlink.Blinks(session.Tree, session.Selection.Caret));
    }

    [Fact]
    public void TheCaretDoesNotBlinkOnAKeyword()
    {
        using var session = TestEditor.Open(TestEditor.BareRule());

        var keyword = CaretNavigation.First(session.Tree);

        Assert.False(CaretBlink.Blinks(session.Tree, keyword));
    }

    [Fact]
    public void TheCaretDoesNotBlinkWhereTheLayoutHasNothing()
    {
        var rule = TestEditor.BareRule();
        using var session = TestEditor.Open(rule);

        var nowhere = CaretPosition.At(CellAddress.OfSlot(rule.Id, "steps"));

        Assert.False(CaretBlink.Blinks(session.Tree, nowhere));
    }
}

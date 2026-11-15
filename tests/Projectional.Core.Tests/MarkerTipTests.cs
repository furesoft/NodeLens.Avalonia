using Projectional.Core;
using Projectional.Editing;
using Projectional.Projection;
using Projectional.Semantics;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// A tooltip has to be placed before it is drawn, and it has to say something a reader can act on. These
/// are the two answers that need no window: how the text breaks, and which corner of the page the box
/// belongs in.
/// </summary>
/// <remarks>
/// The measure is the monospace one, so a width in these tests is a number of characters and the expected
/// lines can be counted off the message rather than guessed at.
/// </remarks>
public sealed class MarkerTipTests
{
    private const double Wide = 200;

    [Fact]
    public void OneFindingIsOneLine()
    {
        string[] expected = ["Warning: the rule is unreachable"];

        Assert.Equal(expected, MarkerTip.Lines([Finding("the rule is unreachable")], Wide, MonospaceTextMeasure.Instance));
    }

    [Fact]
    public void TheSeverityLeadsSoTheBoxSaysHowBadItIs()
    {
        string[] expected = ["Error: something broke"];

        Assert.Equal(
            expected,
            MarkerTip.Lines([Finding("something broke", DiagnosticSeverity.Error)], Wide, MonospaceTextMeasure.Instance));
    }

    [Fact]
    public void TheCheckThatSaidItIsNamedBesideTheMessage()
    {
        // The code is for grouping and suppression, so it belongs next to the message rather than instead
        // of it - a reader who wants to silence a check has to know which one spoke.
        string[] expected = ["Warning: a message (W0042)"];

        Assert.Equal(
            expected,
            MarkerTip.Lines([Finding("a message", code: "W0042")], Wide, MonospaceTextMeasure.Instance));
    }

    [Fact]
    public void AnEmptyCodeAddsNoParentheses()
    {
        string[] expected = ["Warning: a message"];

        Assert.Equal(expected, MarkerTip.Lines([Finding("a message", code: "")], Wide, MonospaceTextMeasure.Instance));
    }

    [Fact]
    public void TwoFindingsAreSeparatedByOneBlankLine()
    {
        string[] expected = ["Error: first", string.Empty, "Warning: second"];

        Assert.Equal(
            expected,
            MarkerTip.Lines(
                [
                    Finding("first", DiagnosticSeverity.Error),
                    Finding("second", DiagnosticSeverity.Warning),
                ],
                Wide,
                MonospaceTextMeasure.Instance));
    }

    [Fact]
    public void AMessageIsWrappedToTheWidthItIsGiven()
    {
        // "Warning:" is eight characters and the box is ten wide, so the message starts on the second line
        // rather than making the box wider than the room it was given.
        string[] expected = ["Warning:", "aaaa bbbb", "cccc"];

        Assert.Equal(
            expected,
            MarkerTip.Lines([Finding("aaaa bbbb cccc")], maxWidth: 10, MonospaceTextMeasure.Instance));
    }

    [Fact]
    public void AWordWiderThanTheBoxIsBrokenRatherThanOverflowing()
    {
        // An overflowing line would make the width the box was placed with a lie.
        string[] expected = ["Warning:", "aaaaaaaaaa", "aa"];

        Assert.Equal(
            expected,
            MarkerTip.Lines([Finding("aaaaaaaaaaaa")], maxWidth: 10, MonospaceTextMeasure.Instance));
    }

    [Fact]
    public void ABoxNarrowerThanOneGlyphStillAdvances()
    {
        // One character per line is the floor: taking nothing would loop forever. Joining the lines back up
        // is what proves nothing was dropped on the way.
        var lines = MarkerTip.Lines([Finding("ab")], maxWidth: 0.5, MonospaceTextMeasure.Instance);

        Assert.Equal("Warning:ab", string.Concat(lines));
    }

    [Fact]
    public void NothingToSayIsNoLines() =>
        Assert.Empty(MarkerTip.Lines([], Wide, MonospaceTextMeasure.Instance));

    [Fact]
    public void RunsOfBlanksCollapse()
    {
        string[] expected = ["a b"];

        Assert.Equal(expected, MarkerTip.Wrap("a   b", Wide, MonospaceTextMeasure.Instance));
    }

    [Fact]
    public void TheBoxSitsBelowTheMarkerItBelongsTo()
    {
        // Below, so it never covers the squiggle it is explaining.
        var box = MarkerTip.Place(new LayoutRect(10, 10, 5, 1), new LayoutSize(20, 10), new LayoutSize(100, 100));

        Assert.Equal(new LayoutRect(10, 13, 20, 10), box);
    }

    [Fact]
    public void ABoxAtTheBottomFlipsAboveTheMarker()
    {
        // The last line of a page scrolled to the end has no room below it.
        var box = MarkerTip.Place(new LayoutRect(10, 90, 5, 1), new LayoutSize(20, 10), new LayoutSize(100, 100));

        Assert.Equal(new LayoutRect(10, 78, 20, 10), box);
    }

    [Fact]
    public void ABoxPastTheRightEdgeIsPulledBack()
    {
        var box = MarkerTip.Place(new LayoutRect(95, 10, 5, 1), new LayoutSize(20, 10), new LayoutSize(100, 100));

        Assert.Equal(80, box.X);
    }

    [Fact]
    public void ABoxBiggerThanThePageIsPinnedToTheCorner()
    {
        // Pinned rather than allowed to hang off both sides, where nothing of it could be read.
        var box = MarkerTip.Place(new LayoutRect(10, 10, 5, 1), new LayoutSize(200, 200), new LayoutSize(100, 100));

        Assert.Equal(new LayoutRect(0, 0, 200, 200), box);
    }

    [Fact]
    public void AFindingListIsRequired() =>
        Assert.Throws<ArgumentNullException>(() => MarkerTip.Lines(null!, Wide, MonospaceTextMeasure.Instance));

    [Fact]
    public void AMeasureIsRequired() =>
        Assert.Throws<ArgumentNullException>(() => MarkerTip.Lines([], Wide, null!));

    [Fact]
    public void ALineWidthMustBePositive() =>
        Assert.Throws<ArgumentOutOfRangeException>(
            () => MarkerTip.Lines([], maxWidth: 0, MonospaceTextMeasure.Instance));

    private static Diagnostic Finding(
        string message,
        DiagnosticSeverity severity = DiagnosticSeverity.Warning,
        string? code = null) =>
        new(severity, NodeId.New(), message, code: code);
}

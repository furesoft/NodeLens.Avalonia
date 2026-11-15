using Projectional.Core;
using Projectional.Editing;
using Projectional.Semantics;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// A tooltip may only open when the pointer is on the squiggle, not when it is on the word the squiggle
/// is under. This is the test that draws that line.
/// </summary>
/// <remarks>
/// The cases are about the strip rather than about the geometry: a marker is placed on a line and the
/// question is only whether a point falls in the bottom of it. A line is one unit tall here and the band
/// is a fraction of that, so the numbers in the assertions are the strip and not the line.
/// </remarks>
public sealed class MarkerHitTestTests
{
    [Fact]
    public void APointOnTheWaveFindsTheFinding()
    {
        var hit = Assert.NotNull(MarkerHitTest.At([Mark(x: 3, width: 4)], x: 4, y: 0.5, bandHeight: 0.5));

        Assert.Equal("marked", Assert.Single(hit.Diagnostics).Message);
        Assert.Equal(3, hit.Anchor.X);
    }

    [Fact]
    public void APointAboveTheWaveFindsNothing()
    {
        // The word itself: opening a tooltip here would cover the text the reader is reading.
        Assert.Null(MarkerHitTest.At([Mark(x: 3, width: 4)], x: 4, y: 0.2, bandHeight: 0.5));
    }

    [Fact]
    public void APointLeftOfTheMarkerFindsNothing() =>
        Assert.Null(MarkerHitTest.At([Mark(x: 3, width: 4)], x: 2.9, y: 0.5, bandHeight: 0.5));

    [Fact]
    public void TheRightEdgeOfAMarkerIsNotOnIt()
    {
        // Half-open like every other rectangle in the layout, so two markers that touch do not both answer.
        Assert.Null(MarkerHitTest.At([Mark(x: 3, width: 4)], x: 7, y: 0.5, bandHeight: 0.5));
    }

    [Fact]
    public void TheBottomOfALineIsNotOnIt() =>
        Assert.Null(MarkerHitTest.At([Mark(x: 3, width: 4)], x: 4, y: 1, bandHeight: 0.5));

    [Fact]
    public void ABandTallerThanTheLineCoversTheWholeLine()
    {
        // A line with no room left for the wave still has to be reachable, or a marker at a tiny font size
        // would be invisible and unusable at once.
        Assert.NotNull(MarkerHitTest.At([Mark(x: 3, width: 4)], x: 4, y: 0.2, bandHeight: 5));
    }

    [Fact]
    public void NothingMarkedFindsNothing() => Assert.Null(MarkerHitTest.At([], x: 0, y: 0, bandHeight: 1));

    [Fact]
    public void TheWorstSeverityComesFirst()
    {
        // The page draws one squiggle per stretch and the worse severity wins, so the tooltip has to lead
        // with the finding that was painted - otherwise the reader sees a red wave and an amber message.
        var hit = Assert.NotNull(MarkerHitTest.At(
            [
                Mark(x: 0, width: 5, DiagnosticSeverity.Warning),
                Mark(x: 0, width: 5, DiagnosticSeverity.Error),
            ],
            x: 1,
            y: 0.5,
            bandHeight: 0.5));

        Assert.Equal(2, hit.Diagnostics.Count);
        Assert.Equal(DiagnosticSeverity.Error, hit.Diagnostics[0].Severity);
        Assert.Equal(DiagnosticSeverity.Warning, hit.Diagnostics[1].Severity);
    }

    [Fact]
    public void EveryFindingOnTheStretchIsReturned()
    {
        // The one that was not painted is reachable only through the tooltip, so dropping it here would make
        // it unreachable everywhere.
        var hit = Assert.NotNull(MarkerHitTest.At(
            [
                Mark(x: 0, width: 5, DiagnosticSeverity.Hint),
                Mark(x: 0, width: 5, DiagnosticSeverity.Error),
                Mark(x: 0, width: 5, DiagnosticSeverity.Info),
            ],
            x: 1,
            y: 0.5,
            bandHeight: 0.5));

        Assert.Equal(3, hit.Diagnostics.Count);
        Assert.Equal(DiagnosticSeverity.Error, hit.Diagnostics[0].Severity);
        Assert.Equal(DiagnosticSeverity.Info, hit.Diagnostics[1].Severity);
        Assert.Equal(DiagnosticSeverity.Hint, hit.Diagnostics[2].Severity);
    }

    [Fact]
    public void OneFindingOnTwoLinesIsReturnedOnce()
    {
        // A finding over a value that wrapped is one finding, however many waves it is stroked as.
        var diagnostic = new Diagnostic(DiagnosticSeverity.Error, NodeId.New(), "wrapped");

        var hit = Assert.NotNull(MarkerHitTest.At(
            [Line(diagnostic, x: 0, y: 0), Line(diagnostic, x: 0, y: 1)],
            x: 1,
            y: 1.5,
            bandHeight: 0.5));

        Assert.Same(diagnostic, Assert.Single(hit.Diagnostics));
        Assert.Equal(1, hit.Anchor.Y);
    }

    [Fact]
    public void TwoIdenticalFindingsAreTwoFindings()
    {
        // Two checks that happen to word the same sentence are two results, not a duplicate - deduping by
        // value would hide one of them.
        var left = new Diagnostic(DiagnosticSeverity.Error, NodeId.New(), "same");
        var right = new Diagnostic(DiagnosticSeverity.Error, left.Target, "same");

        var hit = Assert.NotNull(MarkerHitTest.At(
            [Line(left, x: 0, y: 0), Line(right, x: 0, y: 0)],
            x: 1,
            y: 0.5,
            bandHeight: 0.5));

        Assert.Equal(2, hit.Diagnostics.Count);
    }

    [Fact]
    public void TheAnchorIsTheFirstMarkerOnThePoint()
    {
        // The box is placed against it, so it has to be the one highest in the document - a tip pinned to a
        // marker further down would float away from the squiggle the pointer is on.
        var hit = Assert.NotNull(MarkerHitTest.At(
            [
                Mark(x: 0, width: 5, DiagnosticSeverity.Error, y: 0),
                Mark(x: 0, width: 5, DiagnosticSeverity.Error, y: 0),
            ],
            x: 1,
            y: 0.5,
            bandHeight: 0.5));

        Assert.Equal(0, hit.Anchor.Y);
        Assert.Equal(5, hit.Anchor.Width);
    }

    [Fact]
    public void AMarkerListIsRequired() =>
        Assert.Throws<ArgumentNullException>(() => MarkerHitTest.At(null!, x: 0, y: 0, bandHeight: 1));

    [Fact]
    public void ABandCannotBeNegative() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => MarkerHitTest.At([], x: 0, y: 0, bandHeight: -1));

    private static MarkerLine Mark(
        double x,
        double width,
        DiagnosticSeverity severity = DiagnosticSeverity.Warning,
        double y = 0,
        double height = 1) =>
        new(default, new Diagnostic(severity, NodeId.New(), "marked"), x, y, width, height, 0);

    private static MarkerLine Line(Diagnostic diagnostic, double x, double y) =>
        new(default, diagnostic, x, y, 5, 1, (int)y);
}

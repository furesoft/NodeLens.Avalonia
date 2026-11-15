using Projectional.Core;
using Projectional.Editing;
using Projectional.Semantics;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// Several findings can cover the same characters; a page draws one squiggle per stretch, not one per
/// finding. This is the merge between the two.
/// </summary>
/// <remarks>
/// The cases are about overlap rather than about geometry: markers are placed on a line and the question
/// is only whether two of them describe the same stretch of it. Heights and severities are compared
/// because both decide what the reader sees, and touching markers are the interesting case - they must
/// stay apart, or a warning would be repainted in an error's colour.
/// </remarks>
public sealed class MarkerRunsTests
{
    [Fact]
    public void OneMarkerIsOneRun()
    {
        var run = Assert.Single(MarkerRuns.Of([Mark(x: 3, width: 4, DiagnosticSeverity.Warning)]));

        Assert.Equal(3, run.X);
        Assert.Equal(4, run.Width);
        Assert.Equal(0, run.Y);
        Assert.Equal(1, run.Height);
        Assert.Equal(DiagnosticSeverity.Warning, run.Severity);
    }

    [Fact]
    public void TwoFindingsOnTheSameTextAreOneRun()
    {
        // The same characters flagged twice is one squiggle. Two would be a smear, and the reader cannot
        // count them anyway.
        var runs = MarkerRuns.Of(
        [
            Mark(x: 2, width: 6, DiagnosticSeverity.Warning),
            Mark(x: 2, width: 6, DiagnosticSeverity.Error),
        ]);

        var run = Assert.Single(runs);

        Assert.Equal(2, run.X);
        Assert.Equal(6, run.Width);
        Assert.Equal(DiagnosticSeverity.Error, run.Severity);
    }

    [Fact]
    public void OverlappingMarkersAreMergedIntoOne()
    {
        var run = Assert.Single(MarkerRuns.Of(
        [
            Mark(x: 0, width: 10, DiagnosticSeverity.Error),
            Mark(x: 5, width: 10, DiagnosticSeverity.Error),
        ]));

        Assert.Equal(0, run.X);
        Assert.Equal(15, run.Width);
    }

    [Fact]
    public void AContainedMarkerDoesNotWidenTheRun()
    {
        var run = Assert.Single(MarkerRuns.Of(
        [
            Mark(x: 0, width: 20, DiagnosticSeverity.Error),
            Mark(x: 5, width: 5, DiagnosticSeverity.Error),
        ]));

        Assert.Equal(20, run.Width);
    }

    [Fact]
    public void AChainOfMarkersCollapsesIntoOneRun()
    {
        var run = Assert.Single(MarkerRuns.Of(
        [
            Mark(x: 0, width: 10, DiagnosticSeverity.Error),
            Mark(x: 8, width: 10, DiagnosticSeverity.Error),
            Mark(x: 16, width: 14, DiagnosticSeverity.Error),
        ]));

        Assert.Equal(0, run.X);
        Assert.Equal(30, run.Width);
    }

    [Fact]
    public void TheWorseSeverityWins()
    {
        // Severity is declared from least to most, so the worse of two is the larger - no lookup table.
        Assert.Equal(
            DiagnosticSeverity.Error,
            Assert.Single(MarkerRuns.Of(
            [
                Mark(x: 0, width: 10, DiagnosticSeverity.Warning),
                Mark(x: 5, width: 10, DiagnosticSeverity.Error),
            ])).Severity);

        Assert.Equal(
            DiagnosticSeverity.Error,
            Assert.Single(MarkerRuns.Of(
            [
                Mark(x: 0, width: 10, DiagnosticSeverity.Error),
                Mark(x: 5, width: 10, DiagnosticSeverity.Warning),
            ])).Severity);
    }

    [Fact]
    public void AMilderSeverityDoesNotDowngradeARun()
    {
        var run = Assert.Single(MarkerRuns.Of(
        [
            Mark(x: 0, width: 10, DiagnosticSeverity.Hint),
            Mark(x: 2, width: 2, DiagnosticSeverity.Info),
        ]));

        Assert.Equal(DiagnosticSeverity.Info, run.Severity);
    }

    [Fact]
    public void MarkersThatMerelyTouchStayTwoRuns()
    {
        // A warning that ends exactly where an error begins is two findings, and repainting the warning as
        // an error would be a lie about severity.
        var runs = MarkerRuns.Of(
        [
            Mark(x: 0, width: 5, DiagnosticSeverity.Warning),
            Mark(x: 5, width: 5, DiagnosticSeverity.Error),
        ]);

        Assert.Equal(2, runs.Count);
        Assert.Equal(DiagnosticSeverity.Warning, runs[0].Severity);
        Assert.Equal(5, runs[0].Width);
        Assert.Equal(DiagnosticSeverity.Error, runs[1].Severity);
        Assert.Equal(5, runs[1].X);
    }

    [Fact]
    public void SeparateStretchesOnOneLineStaySeparate()
    {
        var runs = MarkerRuns.Of(
        [
            Mark(x: 0, width: 5, DiagnosticSeverity.Error),
            Mark(x: 20, width: 5, DiagnosticSeverity.Error),
        ]);

        Assert.Equal(2, runs.Count);
        Assert.Equal(20, runs[1].X);
    }

    [Fact]
    public void MarkersOnDifferentLinesAreDifferentRuns()
    {
        var runs = MarkerRuns.Of(
        [
            Mark(x: 0, width: 5, DiagnosticSeverity.Error, y: 0),
            Mark(x: 0, width: 5, DiagnosticSeverity.Warning, y: 1),
        ]);

        Assert.Equal(2, runs.Count);
        Assert.Equal(0, runs[0].Y);
        Assert.Equal(DiagnosticSeverity.Error, runs[0].Severity);
        Assert.Equal(1, runs[1].Y);
        Assert.Equal(DiagnosticSeverity.Warning, runs[1].Severity);
    }

    [Fact]
    public void MergedRunsKeepTheTallestLine()
    {
        var run = Assert.Single(MarkerRuns.Of(
        [
            Mark(x: 0, width: 10, DiagnosticSeverity.Error, height: 1),
            Mark(x: 5, width: 10, DiagnosticSeverity.Error, height: 3),
        ]));

        Assert.Equal(3, run.Height);
    }

    [Fact]
    public void RunsComeBackInReadingOrder()
    {
        // A page walks the list to draw, so the answer must not depend on the order the checks ran in.
        var runs = MarkerRuns.Of(
        [
            Mark(x: 20, width: 5, DiagnosticSeverity.Error, y: 2),
            Mark(x: 10, width: 5, DiagnosticSeverity.Error, y: 0),
            Mark(x: 0, width: 5, DiagnosticSeverity.Error, y: 0),
        ]);

        Assert.Equal(3, runs.Count);
        Assert.Equal(0, runs[0].X);
        Assert.Equal(10, runs[1].X);
        Assert.Equal(2, runs[2].Y);
    }

    [Fact]
    public void NothingMarkedIsAnEmptyAnswer() => Assert.Empty(MarkerRuns.Of([]));

    [Fact]
    public void AMarkerListIsRequired() =>
        Assert.Throws<ArgumentNullException>(() => MarkerRuns.Of(null!));

    private static MarkerLine Mark(
        double x,
        double width,
        DiagnosticSeverity severity,
        double y = 0,
        double height = 1) =>
        new(default, new Diagnostic(severity, NodeId.New(), "marked"), x, y, width, height, 0);
}

using Projectional.Languages;
using Projectional.Testing;
using Projectional.Workbench;
using Xunit;

using TestResult = Projectional.Testing.TestResult;

namespace Projectional.Core.Tests;

/// <summary>The rows a run of a language's tests becomes in the output panel.</summary>
/// <remarks>
/// The window resolves a chosen row back to its test by position, so the contract this pins is not cosmetic: one row
/// per result, in the runner's order, and the summary in the headline rather than as a row of its own.
/// </remarks>
public sealed class TestOutputTests
{
    [Fact]
    public void OneRowPerTestInTheRunnersOrder()
    {
        var tests = new TestReport(
        [
            new TestResult("draws", "pipeline.mps", "draws", Succeeded: true),
            new TestResult("script", "pipeline.mps", "replays", Succeeded: false, "expected", "actual"),
        ]);

        var report = TestOutput.Report(tests, Declared());

        Assert.False(report.Succeeded);
        Assert.Equal(2, report.Rows.Count);
        Assert.Contains("2 test(s), 1 passed, 1 failed", report.Headline, StringComparison.Ordinal);

        Assert.Equal("passed  draws  draws  pipeline.mps", report.Rows[0].Text);
        Assert.Equal("FAILED  script  replays  pipeline.mps", report.Rows[1].Text);

        // A test row is about a document rather than a node, so it has no address in the document in front.
        Assert.All(report.Rows, row => Assert.Null(row.Place));
    }

    [Fact]
    public void ALanguageWithoutTestsSaysThereWasNothingToRun()
    {
        var report = TestOutput.Report(TestReport.Empty, Declared());

        Assert.True(report.Succeeded);
        Assert.Empty(report.Rows);
        Assert.Contains("nothing to run", report.Headline, StringComparison.Ordinal);
    }

    private static LanguageProject Declared() =>
        LanguageProject.Open(Path.Combine(AppContext.BaseDirectory, "Declared"), out var error) is { } language
            && error is null
            ? language
            : throw new InvalidOperationException(error);
}

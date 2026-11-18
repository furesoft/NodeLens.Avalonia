using Projectional.Avalonia;
using Projectional.Expressions;
using Projectional.Samples;
using Projectional.Workbench;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>Running a document, and what the output panel is handed afterwards.</summary>
public sealed class DocumentRunTests
{
    [Fact]
    public void AMatchingRuleAnswersItsOutcome()
    {
        var model = PayrollSample.NewSampleModel();

        var report = DocumentRun.Of(model.Roots[0], RulesEvaluation.Registry, "amount=1500");

        Assert.True(report.Succeeded);
        Assert.StartsWith("Accept in ", report.Headline, StringComparison.Ordinal);
    }

    [Fact]
    public void NoMatchingRuleSaysSoInsteadOfInventingAnOutcome()
    {
        var model = PayrollSample.NewSampleModel();

        var report = DocumentRun.Of(model.Roots[0], RulesEvaluation.Registry, "amount=500");

        Assert.False(report.Succeeded);
        Assert.Equal("No rule matched, so the ruleset has no outcome.", report.Headline);
        Assert.NotEmpty(report.Rows);
    }

    [Fact]
    public void TheTraceIsPostOrderAndEveryRowPointsAtItsNode()
    {
        var model = PayrollSample.NewSampleModel();

        var report = DocumentRun.Of(model.Roots[0], RulesEvaluation.Registry, "amount=1500");

        Assert.Equal(5, report.Rows.Count);

        // Children first, the ruleset last: that is the order the values were finished, and the order the answer is
        // read back in. A row names the concept and the value, not the property behind it.
        Assert.EndsWith("= 1500", report.Rows[0].Text, StringComparison.Ordinal);
        Assert.EndsWith("= 1000", report.Rows[1].Text, StringComparison.Ordinal);
        Assert.EndsWith("= true", report.Rows[3].Text, StringComparison.Ordinal);
        Assert.EndsWith("Rules.Ruleset = Accept", report.Rows[^1].Text, StringComparison.Ordinal);
        Assert.All(report.Rows, row => Assert.NotNull(row.Place));
    }

    [Fact]
    public void ANameTheRunDoesNotBindFailsNamingIt()
    {
        var model = PayrollSample.NewSampleModel();

        var report = DocumentRun.Of(model.Roots[0], RulesEvaluation.Registry);

        Assert.False(report.Succeeded);
        Assert.Equal("'amount' has no value in this run.", report.Headline);
    }

    [Fact]
    public void AnUnreadableLineIsReportedWithoutRunning()
    {
        var model = PayrollSample.NewSampleModel();

        var report = DocumentRun.Of(model.Roots[0], RulesEvaluation.Registry, "amount");

        Assert.False(report.Succeeded);
        Assert.Empty(report.Rows);
        Assert.Contains("name=value", report.Headline, StringComparison.Ordinal);
    }

    [Fact]
    public void AConceptTheInterpreterDoesNotCoverFailsNamingTheConcept()
    {
        // The registry has an interpreter, but not for this document's root - which is what a language whose
        // interpreter is only half written looks like.
        var model = PayrollSample.NewSampleModel();

        var report = DocumentRun.Of(model.Roots[0], ExpressionsEvaluation.Registry);

        Assert.False(report.Succeeded);
        Assert.Contains("Rules.Ruleset", report.Headline, StringComparison.Ordinal);
    }

    [Fact]
    public void ASpentBudgetStopsTheRunInsteadOfHanging()
    {
        var model = PayrollSample.NewSampleModel();

        var report = DocumentRun.Of(model.Roots[0], RulesEvaluation.Registry, "amount=1500", budget: 1);

        Assert.False(report.Succeeded);
        Assert.Contains("budget allows", report.Headline, StringComparison.Ordinal);
    }
}

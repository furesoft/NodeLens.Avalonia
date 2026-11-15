using Projectional.Core;
using Projectional.Expressions;
using Projectional.Samples;
using Projectional.Semantics;
using Projectional.Workbench;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// What the workbench reports as "still called what it was born as" - the reference host check that gives
/// the marker tooltip something real to show.
/// </summary>
public sealed class DefaultNamesTests
{
    [Fact]
    public void ANewDocumentReportsItsUnnamedRuleset()
    {
        var model = PayrollSample.NewEmptyModel();

        var finding = Assert.Single(DefaultNames.Of(model));

        Assert.Equal(DiagnosticSeverity.Hint, finding.Severity);
        Assert.Equal(DefaultNames.Code, finding.Code);
        Assert.Equal("name", finding.Role);
        Assert.Equal(model.Roots[0].Id, finding.Target);
    }

    [Fact]
    public void NamingTheRulesetSilencesTheFinding()
    {
        var model = PayrollSample.NewEmptyModel();
        model.Roots[0].Set("name", "Payroll");

        Assert.Empty(DefaultNames.Of(model));
    }

    [Fact]
    public void TheMessageSaysWhichConceptAndWhichPlaceholder()
    {
        var finding = Assert.Single(DefaultNames.Of(PayrollSample.NewEmptyModel()));

        Assert.Contains("ruleset", finding.Message, StringComparison.Ordinal);
        Assert.Contains("\"unnamed\"", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ARuleIsReportedAsWellAsTheRoot()
    {
        var model = PayrollSample.NewEmptyModel();
        var ruleset = model.Roots[0];
        ruleset.Set("name", "Payroll");

        var rule = model.NewChild(ruleset, "rules", RulesConceptKeys.Rule);

        var finding = Assert.Single(DefaultNames.Of(model));

        Assert.Equal(rule.Id, finding.Target);
        Assert.Contains("rule", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FindingsFollowDocumentOrder()
    {
        var model = PayrollSample.NewEmptyModel();
        var ruleset = model.Roots[0];
        var first = model.NewChild(ruleset, "rules", RulesConceptKeys.Rule);
        var second = model.NewChild(ruleset, "rules", RulesConceptKeys.Rule);

        var targets = DefaultNames.Of(model).Select(finding => finding.Target).ToArray();

        Assert.Equal([ruleset.Id, first.Id, second.Id], targets);
    }

    [Fact]
    public void TheSampleDocumentReportsNothing()
    {
        Assert.Empty(DefaultNames.Of(PayrollSample.NewSampleModel()));
    }

    [Fact]
    public void AModelWithoutRootsReportsNothing()
    {
        Assert.Empty(DefaultNames.Of(RulesLanguage.NewProject().NewModel("empty")));
    }

    [Fact]
    public void AConceptThatDeclaresNoPlaceholderIsNotReported()
    {
        // Expressions.Name has a name slot, but it declares no default for it, so an empty name is a name
        // the user has not typed yet rather than a placeholder they were meant to replace.
        var model = RulesLanguage.NewProject().NewModel("expressions");
        model.AddRoot(model.NewNode(ExpressionsConceptKeys.Name));

        Assert.Empty(DefaultNames.Of(model));
    }

    [Fact]
    public void ANullModelIsRefused()
    {
        Assert.Throws<ArgumentNullException>(() => DefaultNames.Of(null!));
    }
}

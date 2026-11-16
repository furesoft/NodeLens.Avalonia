using Projectional.Core;
using Projectional.Editing;
using Projectional.Expressions;
using Projectional.Samples;
using Projectional.Semantics;
using Projectional.Workbench;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// What the workbench reports as "still called what it was born as" - the reference host check, and the
/// first repair a host supplies through the public surface alone.
/// </summary>
/// <remarks>
/// The check answers with fixes rather than with diagnostics, because every finding it makes has a cure.
/// The assertions therefore read through <c>fix.Diagnostic</c>: that is the same object a marker set is
/// built from, so the finding asserted here is the finding the editor draws.
/// </remarks>
public sealed class DefaultNamesTests
{
    [Fact]
    public void ANewDocumentReportsItsUnnamedRuleset()
    {
        var model = PayrollSample.NewEmptyModel();

        var fix = Assert.Single(DefaultNames.Of(model));
        var finding = fix.Diagnostic;

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
        var message = Assert.Single(DefaultNames.Of(PayrollSample.NewEmptyModel())).Diagnostic.Message;

        Assert.Contains("ruleset", message, StringComparison.Ordinal);
        Assert.Contains("\"unnamed\"", message, StringComparison.Ordinal);
    }

    [Fact]
    public void ARuleIsReportedAsWellAsTheRoot()
    {
        var model = PayrollSample.NewEmptyModel();
        var ruleset = model.Roots[0];
        ruleset.Set("name", "Payroll");

        var rule = model.NewChild(ruleset, "rules", RulesConceptKeys.Rule);

        var finding = Assert.Single(DefaultNames.Of(model)).Diagnostic;

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

        var targets = DefaultNames.Of(model).Select(fix => fix.Diagnostic.Target).ToArray();

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
    public void TheRepairNamesTheNodeAfterItsConcept()
    {
        var fix = Assert.Single(DefaultNames.Of(PayrollSample.NewEmptyModel()));

        Assert.Equal("Name it \"Ruleset1\"", fix.Label);
        Assert.False(string.IsNullOrEmpty(fix.Description));
        Assert.False(fix.IsDisabled);
    }

    [Fact]
    public void TwoUnnamedNodesAreOfferedDifferentNames()
    {
        // Both are called "unnamed" and both are unnamed, so a repair that simply appended "1" would offer
        // the same new name twice and only work if the user applied them one at a time.
        var model = PayrollSample.NewEmptyModel();
        var ruleset = model.Roots[0];
        ruleset.Set("name", "Payroll");
        model.NewChild(ruleset, "rules", RulesConceptKeys.Rule);
        model.NewChild(ruleset, "rules", RulesConceptKeys.Rule);

        Assert.Equal(
            ["Name it \"Rule1\"", "Name it \"Rule2\""],
            DefaultNames.Of(model).Select(fix => fix.Label));
    }

    [Fact]
    public void ApplyingTheRepairNamesTheNode()
    {
        var model = PayrollSample.NewEmptyModel();
        var ruleset = model.Roots[0];
        var fix = Assert.Single(DefaultNames.Of(model));

        using var session = new EditorSession(
            ruleset,
            RulesLanguage.CreateNotations(RulesLanguage.Concepts),
            typing: RulesLanguage.TypingRules);

        session.Markers = new MarkerSet([fix.Diagnostic]);
        session.Fixes = new FixSet(fix);

        Assert.Equal(EditOutcome.Applied, session.Apply(fix).Outcome);
        Assert.Equal("Ruleset1", ruleset.GetOrNull("name"));
        Assert.Empty(DefaultNames.Of(model));
    }

    [Fact]
    public void ANullModelIsRefused()
    {
        Assert.Throws<ArgumentNullException>(() => DefaultNames.Of(null!));
    }
}
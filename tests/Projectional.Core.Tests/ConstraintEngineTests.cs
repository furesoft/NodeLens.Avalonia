using Projectional.Core;
using Projectional.Expressions;
using Projectional.Samples;
using Projectional.Semantics;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The constraint engine: a language's rules run over a document, and the findings come back in document
/// order.
/// </summary>
public sealed class ConstraintEngineTests
{
    [Fact]
    public void AConstraintReportsWhatItFinds()
    {
        var model = RulesLanguage.NewProject().NewModel("rules");
        var ruleset = model.NewNode(RulesConceptKeys.Ruleset);
        ruleset.Set("name", string.Empty);

        var findings = new ConstraintEngine(
            ConstraintSet.Empty.Add(RulesConceptKeys.Ruleset, new NeedsAName())).Check(model);

        var finding = Assert.Single(findings);
        Assert.Equal(ruleset.Id, finding.Target);
        Assert.Equal(DiagnosticSeverity.Error, finding.Severity);
        Assert.Equal("ruleset.needs-name", finding.Code);
    }

    [Fact]
    public void AConceptInheritsTheConstraintsOfItsSuperConcepts()
    {
        var model = ExpressionsLanguage.NewProject().NewModel("expressions");
        var literal = model.NewNode("Expressions.IntLiteral");

        // Declared on the abstract Expression, asked for a concrete one.
        var findings = new ConstraintEngine(
            ConstraintSet.Empty.Add("Expressions.Expression", new AlwaysComplains())).Check(model);

        Assert.Equal(literal.Id, Assert.Single(findings).Target);
    }

    [Fact]
    public void FindingsComeBackInDocumentOrder()
    {
        var model = RulesLanguage.NewProject().NewModel("rules");
        var ruleset = model.NewNode(RulesConceptKeys.Ruleset);
        ruleset.Set("name", "Payroll");

        var first = model.NewChild(ruleset, RulesLanguage.RulesSlot, RulesConceptKeys.Rule);
        first.Set("name", "First");

        var second = model.NewChild(ruleset, RulesLanguage.RulesSlot, RulesConceptKeys.Rule);
        second.Set("name", "Second");

        var findings = new ConstraintEngine(
            ConstraintSet.Empty.Add(RulesConceptKeys.Rule, new AlwaysComplains())).Check(model);

        Assert.Equal([first.Id, second.Id], findings.Select(finding => finding.Target));
    }

    [Fact]
    public void ASubtreeCheckAnswersOnlyThatSubtree()
    {
        var model = RulesLanguage.NewProject().NewModel("rules");
        var ruleset = model.NewNode(RulesConceptKeys.Ruleset);
        ruleset.Set("name", "Payroll");

        var first = model.NewChild(ruleset, RulesLanguage.RulesSlot, RulesConceptKeys.Rule);
        first.Set("name", "First");

        var second = model.NewChild(ruleset, RulesLanguage.RulesSlot, RulesConceptKeys.Rule);
        second.Set("name", "Second");

        var engine = new ConstraintEngine(ConstraintSet.Empty.Add(RulesConceptKeys.Rule, new AlwaysComplains()));

        // The seam an incremental caller uses: recheck the part that changed, not the document.
        Assert.Equal([second.Id], engine.Check(second).Select(finding => finding.Target));
    }

    [Fact]
    public void AConstraintCanAskWhatAReferenceMayPointAt()
    {
        var model = RulesLanguage.NewProject().NewModel("rules");
        var ruleset = model.NewNode(RulesConceptKeys.Ruleset);
        ruleset.Set("name", "Payroll");

        var rule = model.NewChild(ruleset, RulesLanguage.RulesSlot, RulesConceptKeys.Rule);
        rule.Set("name", "Approved");

        var findings = new ConstraintEngine(
            ConstraintSet.Empty.Add(RulesConceptKeys.Rule, new NeedsAVisibleName("Payroll"))).Check(model);

        // The ruleset is an ancestor, so the default chain sees it.
        Assert.Empty(findings);
    }

    [Fact]
    public void AConstraintThatFindsNothingReportsNothing()
    {
        var model = RulesLanguage.NewProject().NewModel("rules");
        model.NewNode(RulesConceptKeys.Ruleset).Set("name", "Payroll");

        Assert.Empty(new ConstraintEngine(ConstraintSet.Empty).Check(model));
    }

    [Fact]
    public void ARuleDeclaredForEveryNodeRunsForEveryConcept()
    {
        // The case a concept key cannot express: a language's concepts need not share a root, so a rule
        // about "any node with a reference" would have to be repeated for each of them.
        var model = ExpressionsLanguage.NewProject().NewModel("expressions");
        var binary = model.NewNode(ExpressionsConceptKeys.Binary);
        model.NewChild(binary, "left", ExpressionsConceptKeys.IntLiteral);
        model.NewChild(binary, "right", ExpressionsConceptKeys.StringLiteral);

        var findings = new ConstraintEngine(ConstraintSet.Empty.Everywhere(new AlwaysComplains())).Check(model);

        Assert.Equal(3, findings.Count);
        Assert.Equal(3, findings.Select(finding => finding.Target).Distinct().Count());
    }

    [Fact]
    public void ARuleForEveryNodeRunsBeforeTheConceptsOwn()
    {
        var model = RulesLanguage.NewProject().NewModel("rules");

        // Cleared rather than left alone: a property with a default is materialized, so "unset" is
        // "unnamed" and the rule would have nothing to say.
        model.NewNode(RulesConceptKeys.Ruleset).Set("name", string.Empty);

        var findings = new ConstraintEngine(
            ConstraintSet.Empty
                .Add(RulesConceptKeys.Ruleset, new NeedsAName())
                .Everywhere(new AlwaysComplains())).Check(model);

        Assert.Equal(["always.", "a ruleset needs a name."], findings.Select(finding => finding.Message));
    }

    [Fact]
    public void AComposedSetKeepsTheOrderItWasComposedIn()
    {
        var first = ConstraintSet.Empty.Everywhere(new Named("first."));
        var second = ConstraintSet.Empty.Everywhere(new Named("second.")).Add(RulesConceptKeys.Ruleset, new Named("third."));

        var model = RulesLanguage.NewProject().NewModel("rules");
        model.NewNode(RulesConceptKeys.Ruleset).Set("name", "Payroll");

        var findings = new ConstraintEngine(first.With(second)).Check(model);

        Assert.Equal(["first.", "second.", "third."], findings.Select(finding => finding.Message));
    }

    [Fact]
    public void ComposingAnEmptySetChangesNothing()
    {
        var once = ConstraintSet.Empty.Everywhere(new AlwaysComplains());

        Assert.Equal(1, once.With(ConstraintSet.Empty).Count);
        Assert.Equal(1, once.Count);
    }

    [Fact]
    public void AConstraintCanPointAtAChildItIsNotAbout()
    {
        var model = RulesLanguage.NewProject().NewModel("rules");
        var ruleset = model.NewNode(RulesConceptKeys.Ruleset);
        ruleset.Set("name", "Payroll");

        var rule = model.NewChild(ruleset, RulesLanguage.RulesSlot, RulesConceptKeys.Rule);
        rule.Set("name", "Approved");

        var findings = new ConstraintEngine(
            ConstraintSet.Empty.Add(RulesConceptKeys.Ruleset, new ComplainsAboutItsFirstRule())).Check(model);

        var finding = Assert.Single(findings);
        Assert.Equal(rule.Id, finding.Target);
        Assert.Equal("name", finding.Role);
    }

    /// <summary>A ruleset whose name is empty.</summary>
    private sealed class NeedsAName : IConstraint
    {
        public void Check(ConstraintContext context)
        {
            if (context.Node.GetOrNull("name") is not { Length: > 0 })
            {
                context.Error("a ruleset needs a name.", role: "name", code: "ruleset.needs-name");
            }
        }
    }

    private sealed class AlwaysComplains : IConstraint
    {
        public void Check(ConstraintContext context) => context.Warning("always.");
    }

    /// <summary>Reports a fixed message, so a test can read the order rules ran in.</summary>
    private sealed class Named(string message) : IConstraint
    {
        public void Check(ConstraintContext context) => context.Warning(message);
    }

    /// <summary>A rule whose name is not visible from its own scope.</summary>
    private sealed class NeedsAVisibleName(string name) : IConstraint
    {
        public void Check(ConstraintContext context)
        {
            if (context.ScopeOf("inherits").Resolve(name).Kind != ScopeResolutionKind.Found)
            {
                context.Error($"'{name}' is not visible here.");
            }
        }
    }

    /// <summary>A ruleset that reports on its first rule rather than on itself.</summary>
    private sealed class ComplainsAboutItsFirstRule : IConstraint
    {
        public void Check(ConstraintContext context)
        {
            var rules = context.Node.GetChildren(RulesLanguage.RulesSlot);

            if (rules.Count > 0 && rules[0] is { } rule)
            {
                context.Report(DiagnosticSeverity.Warning, rule, "this rule is first.", role: "name");
            }
        }
    }
}

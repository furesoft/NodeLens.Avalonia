using Projectional.Core;
using Projectional.Samples;
using Projectional.Samples.Checks;
using Projectional.Semantics;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The sample language's own constraints, which is the engine's first real consumer.
/// </summary>
public sealed class SampleConstraintsTests
{
    [Fact]
    public void ARuleWithoutAConditionIsReported()
    {
        var model = RulesLanguage.NewProject().NewModel("rules");
        var ruleset = model.NewNode(RulesConceptKeys.Ruleset);
        ruleset.Set("name", "Payroll");

        var rule = model.NewChild(ruleset, RulesLanguage.RulesSlot, RulesConceptKeys.Rule);
        rule.Set("name", "Approved");

        var finding = Assert.Single(new ConstraintEngine(SampleConstraints.All).Check(model));

        Assert.Equal(rule.Id, finding.Target);
        Assert.Equal("condition", finding.Role);
        Assert.Equal(SampleConstraints.MissingConditionCode, finding.Code);
        Assert.Equal(DiagnosticSeverity.Error, finding.Severity);
    }

    [Fact]
    public void TheSampleDocumentIsClean()
    {
        // The shipped sample has a condition, so the app must not greet a user with a finding on it.
        Assert.Empty(new ConstraintEngine(SampleConstraints.All, types: SampleTypes.All).Check(PayrollSample.NewSampleModel()));
    }

    [Fact]
    public void AConditionThatIsNotATruthValueIsReported()
    {
        var model = RulesLanguage.NewProject().NewModel("rules");
        var ruleset = model.NewNode(RulesConceptKeys.Ruleset);
        ruleset.Set("name", "Payroll");

        var rule = model.NewChild(ruleset, RulesLanguage.RulesSlot, RulesConceptKeys.Rule);
        rule.Set("name", "Approved");

        // A number is not a condition, and the type rule for a literal is what says so.
        model.NewChild(rule, "condition", "Expressions.IntLiteral").Set("value", "42");

        var finding = Assert.Single(
            new ConstraintEngine(SampleConstraints.All, types: SampleTypes.All).Check(model));

        Assert.Equal(SampleConstraints.ConditionNotBooleanCode, finding.Code);
        Assert.Contains("int", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AComparisonIsATruthValueAndIsNotReported()
    {
        var model = RulesLanguage.NewProject().NewModel("rules");
        var ruleset = model.NewNode(RulesConceptKeys.Ruleset);
        ruleset.Set("name", "Payroll");

        var rule = model.NewChild(ruleset, RulesLanguage.RulesSlot, RulesConceptKeys.Rule);
        rule.Set("name", "Approved");

        var comparison = model.NewChild(rule, "condition", "Expressions.Binary");
        comparison.Set("operator", ">");
        model.NewChild(comparison, "left", "Expressions.IntLiteral").Set("value", "1000");
        model.NewChild(comparison, "right", "Expressions.IntLiteral").Set("value", "10");

        Assert.Empty(new ConstraintEngine(SampleConstraints.All, types: SampleTypes.All).Check(model));
    }

    [Fact]
    public void AConditionOfUnknownTypeIsNotReported()
    {
        var model = RulesLanguage.NewProject().NewModel("rules");
        var ruleset = model.NewNode(RulesConceptKeys.Ruleset);
        ruleset.Set("name", "Payroll");

        var rule = model.NewChild(ruleset, RulesLanguage.RulesSlot, RulesConceptKeys.Rule);
        rule.Set("name", "Approved");

        // A name's type is unknown, and one mistake must not become two findings.
        model.NewChild(rule, "condition", "Expressions.Name").Set("name", "amount");

        Assert.Empty(new ConstraintEngine(SampleConstraints.All, types: SampleTypes.All).Check(model));
    }
}

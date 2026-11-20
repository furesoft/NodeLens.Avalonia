using Projectional.Core;
using Projectional.Language;
using Projectional.Expressions;
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
        Assert.Empty(new ConstraintEngine(SampleConstraints.All, types: ExpressionsLanguage.Types).Check(PayrollSample.NewSampleModel()));
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
            new ConstraintEngine(SampleConstraints.All, types: ExpressionsLanguage.Types).Check(model));

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

        Assert.Empty(new ConstraintEngine(SampleConstraints.All, types: ExpressionsLanguage.Types).Check(model));
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

        Assert.Empty(new ConstraintEngine(SampleConstraints.All, types: ExpressionsLanguage.Types).Check(model));
    }

    [Fact]
    public void AnOperationOnTwoDifferentTypesIsReported()
    {
        // Without this rule the expression is typed as an int and nothing is said about it, because the
        // operator decides the type and never looks at what it was given.
        var model = ExpressionsLanguage.NewProject().NewModel("expressions");
        var binary = model.NewNode(ExpressionsConceptKeys.Binary);

        binary.Set("operator", "+");

        var left = model.NewChild(binary, "left", ExpressionsConceptKeys.IntLiteral);
        var right = model.NewChild(binary, "right", ExpressionsConceptKeys.StringLiteral);

        left.Set("value", "1");
        right.Set("value", "x");

        var finding = Assert.Single(
            new ConstraintEngine(SampleConstraints.All, types: ExpressionsLanguage.Types).Check(model));

        Assert.Equal(binary.Id, finding.Target);
        Assert.Equal("operator", finding.Role);
        Assert.Equal(SampleConstraints.OperandsNotComparableCode, finding.Code);
        Assert.Equal(DiagnosticSeverity.Error, finding.Severity);
        Assert.Contains("'+'", finding.Message, StringComparison.Ordinal);
        Assert.Contains("int", finding.Message, StringComparison.Ordinal);
        Assert.Contains("string", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnOperationWhoseOperandIsUnknownIsNotReported()
    {
        // A name is what this language cannot type, and a finding on every expression that reads one would turn
        // one missing answer into a wall of them.
        var model = ExpressionsLanguage.NewProject().NewModel("expressions");
        var binary = model.NewNode(ExpressionsConceptKeys.Binary);

        binary.Set("operator", "+");
        model.NewChild(binary, "left", ExpressionsConceptKeys.Name).Set("name", "amount");
        model.NewChild(binary, "right", ExpressionsConceptKeys.IntLiteral).Set("value", "1");

        Assert.Empty(new ConstraintEngine(SampleConstraints.All, types: ExpressionsLanguage.Types).Check(model));
    }

    [Fact]
    public void AnOperationWithAMissingSideIsNotReported()
    {
        // An incomplete node is not the author's mistake to be told about.
        var model = ExpressionsLanguage.NewProject().NewModel("expressions");
        var binary = model.NewNode(ExpressionsConceptKeys.Binary);

        binary.Set("operator", "+");
        model.NewChild(binary, "left", ExpressionsConceptKeys.IntLiteral).Set("value", "1");

        Assert.Empty(new ConstraintEngine(SampleConstraints.All, types: ExpressionsLanguage.Types).Check(model));
    }

    [Fact]
    public void AnOperationInsideARuleConditionIsReported()
    {
        // The realistic shape: the expression language has no documents of its own in the samples, but its nodes
        // are what a rule's condition is made of, so this rule is live wherever expressions actually appear.
        var model = RulesLanguage.NewProject().NewModel("rules");
        var ruleset = model.NewNode(RulesConceptKeys.Ruleset);
        ruleset.Set("name", "Payroll");

        var rule = model.NewChild(ruleset, RulesLanguage.RulesSlot, RulesConceptKeys.Rule);
        rule.Set("name", "Approved");

        var comparison = model.NewChild(rule, "condition", ExpressionsConceptKeys.Binary);
        comparison.Set("operator", "<");
        model.NewChild(comparison, "left", ExpressionsConceptKeys.IntLiteral).Set("value", "1");
        model.NewChild(comparison, "right", ExpressionsConceptKeys.StringLiteral).Set("value", "x");

        // The comparison is a truth value, so the condition rule stays silent; what is wrong is the operands.
        var finding = Assert.Single(
            new ConstraintEngine(SampleConstraints.All, types: ExpressionsLanguage.Types).Check(model));

        Assert.Equal(SampleConstraints.OperandsNotComparableCode, finding.Code);
        Assert.Equal(comparison.Id, finding.Target);
    }

    [Fact]
    public void AnOperationWithNoOperatorIsNotReported()
    {
        var model = ExpressionsLanguage.NewProject().NewModel("expressions");
        var binary = model.NewNode(ExpressionsConceptKeys.Binary);

        model.NewChild(binary, "left", ExpressionsConceptKeys.IntLiteral).Set("value", "1");
        model.NewChild(binary, "right", ExpressionsConceptKeys.StringLiteral).Set("value", "x");

        Assert.Empty(new ConstraintEngine(SampleConstraints.All, types: ExpressionsLanguage.Types).Check(model));
    }

    [Fact]
    public void EachNonBooleanLogicalOperandIsReportedWhereItStands()
    {
        // The comparability rule cannot catch this: `1 && 2` has two operands of one type, so they are perfectly
        // comparable. What is wrong is that neither is a truth value, which is the one-way question.
        var model = ExpressionsLanguage.NewProject().NewModel("expressions");
        var binary = model.NewNode(ExpressionsConceptKeys.Binary);

        binary.Set("operator", "&&");
        model.NewChild(binary, "left", ExpressionsConceptKeys.IntLiteral).Set("value", "1");
        model.NewChild(binary, "right", ExpressionsConceptKeys.IntLiteral).Set("value", "2");

        var findings = new ConstraintEngine(SampleConstraints.All, types: ExpressionsLanguage.Types).Check(model);

        Assert.Equal(2, findings.Count);
        Assert.All(findings, finding => Assert.Equal(SampleConstraints.OperandNotBooleanCode, finding.Code));
        Assert.Equal(["left", "right"], findings.Select(finding => finding.Role));
        Assert.All(findings, finding => Assert.Contains("int", finding.Message, StringComparison.Ordinal));
    }

    [Fact]
    public void ALogicalOperationOnTwoTruthValuesIsNotReported()
    {
        var model = ExpressionsLanguage.NewProject().NewModel("expressions");
        var binary = model.NewNode(ExpressionsConceptKeys.Binary);

        binary.Set("operator", "||");
        model.NewChild(binary, "left", ExpressionsConceptKeys.BoolLiteral).Set("value", "true");
        model.NewChild(binary, "right", ExpressionsConceptKeys.BoolLiteral).Set("value", "false");

        Assert.Empty(new ConstraintEngine(SampleConstraints.All, types: ExpressionsLanguage.Types).Check(model));
    }

    [Fact]
    public void AnOperatorThatIsNotLogicalIsLeftToTheComparabilityRule()
    {
        // `1 + 2` is arithmetic: two whole numbers are comparable and neither has to be a truth value, so nothing is
        // reported. Pinning this keeps the operator set from growing into "every binary operator requires bool".
        var model = ExpressionsLanguage.NewProject().NewModel("expressions");
        var binary = model.NewNode(ExpressionsConceptKeys.Binary);

        binary.Set("operator", "+");
        model.NewChild(binary, "left", ExpressionsConceptKeys.IntLiteral).Set("value", "1");
        model.NewChild(binary, "right", ExpressionsConceptKeys.IntLiteral).Set("value", "2");

        Assert.Empty(new ConstraintEngine(SampleConstraints.All, types: ExpressionsLanguage.Types).Check(model));
    }

    [Fact]
    public void ANonBooleanNegationIsReported()
    {
        var model = ExpressionsLanguage.NewProject().NewModel("expressions");
        var unary = model.NewNode(ExpressionsConceptKeys.Unary);

        unary.Set("operator", "!");
        model.NewChild(unary, "operand", ExpressionsConceptKeys.IntLiteral).Set("value", "1");

        var finding = Assert.Single(
            new ConstraintEngine(SampleConstraints.All, types: ExpressionsLanguage.Types).Check(model));

        Assert.Equal(SampleConstraints.OperandNotBooleanCode, finding.Code);
        Assert.Equal("operand", finding.Role);
    }

    [Fact]
    public void ANegationOfATruthValueIsNotReported()
    {
        var model = ExpressionsLanguage.NewProject().NewModel("expressions");
        var unary = model.NewNode(ExpressionsConceptKeys.Unary);

        unary.Set("operator", "!");
        model.NewChild(unary, "operand", ExpressionsConceptKeys.BoolLiteral).Set("value", "true");

        Assert.Empty(new ConstraintEngine(SampleConstraints.All, types: ExpressionsLanguage.Types).Check(model));
    }

    [Fact]
    public void ALogicalOperandOfUnknownTypeIsNotReported()
    {
        var model = ExpressionsLanguage.NewProject().NewModel("expressions");
        var binary = model.NewNode(ExpressionsConceptKeys.Binary);

        binary.Set("operator", "&&");
        model.NewChild(binary, "left", ExpressionsConceptKeys.Name).Set("name", "ready");
        model.NewChild(binary, "right", ExpressionsConceptKeys.BoolLiteral).Set("value", "true");

        Assert.Empty(new ConstraintEngine(SampleConstraints.All, types: ExpressionsLanguage.Types).Check(model));
    }
}

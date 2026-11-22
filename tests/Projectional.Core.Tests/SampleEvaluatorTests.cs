using Projectional.Core;
using Projectional.EvaluationRules;
using Projectional.Language;
using Projectional.Semantics;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>A whole evaluator for a language, written as rules and nothing else.</summary>
/// <remarks>
/// The other tests prove one body at a time against the expression vocabulary. This one builds a language's meaning the
/// way its author would: a grade report answers <c>pass</c> or <c>fail</c> from the points it scored, in a rule that reads
/// a property, supplies its own operator symbol and writes both answers as constants. Nothing here is hand-written C#, so
/// what is proved is that a language's evaluation can live in a model rather than in the assembly that declares it.
/// </remarks>
public sealed class SampleEvaluatorTests
{
    [Theory]
    [InlineData(0, "fail")]
    [InlineData(42, "fail")]
    [InlineData(49, "fail")]
    [InlineData(50, "pass")]
    [InlineData(97, "pass")]
    public void TheEvaluatorAnswersFromThePointsTheReportScored(int points, string expected)
    {
        var (rules, reports) = Evaluator();
        var report = Report(reports, points);

        var result = new Interpreter(rules).Run(report, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Failure is null, result.Failure?.Message);
        Assert.Equal(new StringValue(expected), result.Value);
    }

    [Fact]
    public void AReportThatScoredNothingIsRefusedRatherThanGuessed()
    {
        var (rules, reports) = Evaluator();
        var report = reports.NewNode(GradeLanguage.Report);
        report.Set(GradeLanguage.NameSlot, "essay");
        reports.AddRoot(report);

        var result = new Interpreter(rules).Run(report, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal($"'{GradeLanguage.PointsSlot}' of {GradeLanguage.Report} holds no valid int.", result.Failure?.Message);
    }

    /// <summary>The language's meaning as one rule - pass at fifty points, fail below - and a document of reports.</summary>
    /// <remarks>
    /// The rule is its own document in its own project, the way a language ships it, and the reports are a project in the
    /// language it describes: the two are only ever joined by the concept key the rule names.
    /// </remarks>
    private static (EvaluationRegistry Rules, Model Reports) Evaluator()
    {
        var rules = new Project(EvaluationRuleLanguage.Concepts).NewModel("grades");
        var rule = rules.NewNode(EvaluationRuleConceptKeys.Rule);
        rule.Set(EvaluationRuleLanguage.ConceptKeySlot, GradeLanguage.Report);
        rules.AddRoot(rule);

        var choose = rules.NewChild(rule, EvaluationRuleLanguage.BodySlot, EvaluationRuleConceptKeys.Choose);

        // The symbol lives on the body, because a report has no operator of its own to read one from.
        var comparison = rules.NewChild(choose, EvaluationRuleLanguage.ConditionSlot, EvaluationRuleConceptKeys.Operator);
        comparison.Set(EvaluationRuleLanguage.SymbolSlot, ">=");

        var scored = rules.NewChild(comparison, EvaluationRuleLanguage.LeftSlot, EvaluationRuleConceptKeys.Property);
        scored.Set(EvaluationRuleLanguage.SlotSlot, GradeLanguage.PointsSlot);

        Constant(rules.NewChild(comparison, EvaluationRuleLanguage.RightSlot, EvaluationRuleConceptKeys.Constant), "int", "50");
        Constant(rules.NewChild(choose, EvaluationRuleLanguage.ThenSlot, EvaluationRuleConceptKeys.Constant), "string", "pass");
        Constant(rules.NewChild(choose, EvaluationRuleLanguage.OtherwiseSlot, EvaluationRuleConceptKeys.Constant), "string", "fail");

        return (EvaluationRuleLowering.CreateRegistry([rule], GradeLanguage.Concepts), new Project(GradeLanguage.Concepts).NewModel("reports"));
    }

    private static void Constant(Node constant, string kind, string value)
    {
        constant.Set("kind", kind);
        constant.Set(EvaluationRuleLanguage.ValueSlot, value);
    }

    private static Node Report(Model model, int points)
    {
        var report = model.NewNode(GradeLanguage.Report);
        report.Set(GradeLanguage.NameSlot, "essay");
        report.Set(GradeLanguage.PointsSlot, points.ToString(System.Globalization.CultureInfo.InvariantCulture));
        model.AddRoot(report);

        return report;
    }
}

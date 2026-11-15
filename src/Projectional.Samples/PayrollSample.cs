using Projectional.Core;
using Projectional.Expressions;

namespace Projectional.Samples;

/// <summary>
/// Builds the document the design documents use as their example, so that the standalone editor,
/// the tests and the docs all show the same thing.
/// </summary>
public static class PayrollSample
{
    /// <summary>
    /// A model holding one empty ruleset, which is what a host shows for a brand new document.
    /// </summary>
    public static Model NewEmptyModel()
    {
        var model = NewProject().NewModel("payroll");
        model.AddRoot(model.NewNode(RulesConceptKeys.Ruleset));

        return model;
    }

    /// <summary>
    /// A model holding <c>ruleset Payroll</c> with the single rule
    /// <c>rule Approved when amount &gt; 1000 then Accept</c>.
    /// </summary>
    public static Model NewSampleModel()
    {
        var model = NewEmptyModel();
        var ruleset = model.Roots[0];
        ruleset.Set("name", "Payroll");

        var rule = model.NewChild(ruleset, "rules", RulesConceptKeys.Rule);
        rule.Set("name", "Approved");
        rule.Set("outcome", RuleOutcomes.Accept);

        var condition = model.NewChild(rule, "condition", ExpressionsConceptKeys.Binary);
        condition.Set("operator", ">");

        var amount = model.NewChild(condition, "left", ExpressionsConceptKeys.Name);
        amount.Set("name", "amount");

        var threshold = model.NewChild(condition, "right", ExpressionsConceptKeys.IntLiteral);
        threshold.Set("value", 1000);

        return model;
    }

    private static Project NewProject() => RulesLanguage.NewProject();
}

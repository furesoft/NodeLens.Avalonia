using Projectional.Core;
using Projectional.Expressions;
using Projectional.Samples;

namespace Projectional.Screenshots;

/// <summary>The documents the screenshots are taken of.</summary>
/// <remarks>
/// Every document is built through the public model API a host would use, so a picture cannot show a
/// state the language does not allow. They are shaped to put one feature on show: an empty condition for
/// completion, a rejected rule for emphasis, three rules for line numbers.
/// </remarks>
internal static class Documents
{
    /// <summary>The sample document: one accepting rule over a threshold.</summary>
    internal static Model Sample() => PayrollSample.NewSampleModel();

    /// <summary>Two rules, the second one rejected so the sample emphasis has something to strike.</summary>
    internal static Model AcceptedAndRejected()
    {
        var model = NewPayroll();

        AddRule(model, "Approved", RuleOutcomes.Accept, Comparison(model, ">", "amount", 1000));
        AddRule(model, "Vendor", RuleOutcomes.Reject, NameReference(model, "blocked"));

        return model;
    }

    /// <summary>Three rules, which is enough for a gutter, a table and a marker per severity.</summary>
    internal static Model ThreeRules()
    {
        var model = NewPayroll();

        AddRule(model, "Approved", RuleOutcomes.Accept, Comparison(model, ">", "amount", 1000));
        AddRule(model, "Manager", RuleOutcomes.Review, Comparison(model, ">=", "amount", 5000));
        AddRule(model, "Vendor", RuleOutcomes.Reject, NameReference(model, "blocked"));

        return model;
    }

    /// <summary>One rule awaiting a decision, which both a repair and an intention apply to.</summary>
    internal static Model ReviewRule()
    {
        var model = NewPayroll();

        AddRule(model, "Manager", RuleOutcomes.Review, Comparison(model, ">", "amount", 5000));

        return model;
    }

    /// <summary>One rule with nothing in its condition, which is the slot completion fills.</summary>
    internal static Model OpenCondition()
    {
        var model = NewPayroll();

        AddRule(model, "Approved", RuleOutcomes.Review, condition: null);

        return model;
    }

    /// <summary>One rule whose condition is a binary expression with no operator chosen yet.</summary>
    /// <remarks>
    /// A <c>Binary</c> created from the completion list is born without its operator, so the drawing has to say
    /// that one is expected - this document is that state, and nothing else in the picture distracts from it.
    /// </remarks>
    internal static Model MissingOperator()
    {
        var model = NewPayroll();

        AddRule(model, "Approved", RuleOutcomes.Accept, rule =>
        {
            var binary = model.NewChild(rule, "condition", ExpressionsConceptKeys.Binary);

            model.NewChild(binary, "left", ExpressionsConceptKeys.IntLiteral).Set("value", 1000);
            model.NewChild(binary, "right", ExpressionsConceptKeys.IntLiteral).Set("value", 2000);
        });

        return model;
    }

    private static Model NewPayroll()
    {
        var model = RulesLanguage.NewProject().NewModel("payroll");
        var ruleset = model.NewNode(RulesConceptKeys.Ruleset);

        model.AddRoot(ruleset);
        ruleset.Set("name", "Payroll");

        return model;
    }

    private static void AddRule(Model model, string name, string outcome, Action<Node>? condition)
    {
        var ruleset = model.Roots[0];
        var rule = model.NewChild(ruleset, RulesLanguage.RulesSlot, RulesConceptKeys.Rule);

        rule.Set("name", name);
        rule.Set("outcome", outcome);

        condition?.Invoke(rule);
    }

    private static Action<Node> Comparison(Model model, string @operator, string name, int threshold) => rule =>
    {
        var binary = model.NewChild(rule, "condition", ExpressionsConceptKeys.Binary);

        binary.Set("operator", @operator);

        var left = model.NewChild(binary, "left", ExpressionsConceptKeys.Name);

        left.Set("name", name);

        var right = model.NewChild(binary, "right", ExpressionsConceptKeys.IntLiteral);

        right.Set("value", threshold);
    };

    private static Action<Node> NameReference(Model model, string name) => rule =>
    {
        var reference = model.NewChild(rule, "condition", ExpressionsConceptKeys.Name);

        reference.Set("name", name);
    };
}
